using NetTopologySuite.Algorithm;
using NetTopologySuite.Geometries;
using NetTopologySuite.Triangulate;

namespace CalibrationSuite.Core;

public sealed class NodeMesh
{
    private readonly Point2[] tilts, errors;
    private readonly (int A, int B, int C)[] triangles;
    private readonly (int A, int B)[] boundary;

    private NodeMesh(Point2[] tilts, Point2[] errors, (int, int, int)[] triangles, (int, int)[] boundary)
    {
        this.tilts = tilts; this.errors = errors; this.triangles = triangles; this.boundary = boundary;
    }

    public static NodeMesh Prepare(CalibrationSample[] samples, Point2 target)
    {
        if (!target.IsFinite || samples == null || (samples.Length != 1 && samples.Length < 4)
            || samples.Any(s => s == null || !s.Tilt.IsFinite || !s.Position.IsFinite))
            throw new CalibrationException("Require one upright sample or at least four tilt samples, with finite positions, tilts, and target.");
        var sorted = samples.Length == 1 ? samples : samples.OrderBy(s => s.Tilt.X).ThenBy(s => s.Tilt.Y).ToArray();
        var errors = sorted.Select(s => s.Position - target).ToArray();
        if (errors.Any(e => !e.IsFinite)) throw new CalibrationException("Measured position errors overflow.");
        // A single measured error is constant in tilt; the spatial blend needs no special case.
        if (samples.Length == 1) return new([], errors, [], []);

        var tilts = sorted.Select(s => s.Tilt).ToArray();
        var indices = new Dictionary<Point2, int>();
        for (int i = 0; i < tilts.Length; i++)
            if (!indices.TryAdd(tilts[i], i))
                throw new CalibrationException("Duplicate measured tilts cannot define distinct errors; recapture those poses.");
        var builder = new DelaunayTriangulationBuilder { Tolerance = 0 };
        var coordinates = tilts.Select(p => new Coordinate(p.X, p.Y)).ToArray();
        builder.SetSites(coordinates);
        GeometryCollection mesh;
        try { mesh = builder.GetTriangles(new GeometryFactory()); }
        catch (Exception ex) { throw new CalibrationException($"Unusable tilt geometry: {ex.Message}"); }
        var triangles = new List<(int A, int B, int C)>();
        for (int i = 0; i < mesh.NumGeometries; i++)
        {
            var ids = mesh.GetGeometryN(i).Coordinates.Take(3).Select(p => indices[new Point2(p.X, p.Y)]).Order().ToArray();
            int a = ids[0], b = ids[1], c = ids[2];
            if (Side(tilts[a], tilts[b], tilts[c]) < 0) (b, c) = (c, b);
            double area = Point2.Cross(tilts[b] - tilts[a], tilts[c] - tilts[a]);
            if (!double.IsFinite(area) || area <= 0 || !double.IsFinite(1 / area))
                throw new CalibrationException("Tilt triangle is numerically unusable; recapture its poses.");
            triangles.Add((a, b, c));
        }
        triangles.Sort();
        if (triangles.SelectMany(t => new[] { t.A, t.B, t.C }).Distinct().Count() != tilts.Length)
            throw new CalibrationException("Tilt mesh does not retain every distinct sample; recapture unusable or collinear coverage.");
        var edges = triangles.SelectMany(t => new[] { (t.A, t.B), (t.B, t.C), (t.C, t.A) });
        var boundary = edges.GroupBy(e => (Math.Min(e.Item1, e.Item2), Math.Max(e.Item1, e.Item2)))
            .Where(g => g.Count() == 1).Select(g => g.First()).Order().ToArray();
        var result = new NodeMesh(tilts, errors, triangles.ToArray(), boundary);
        if (!result.StrictlyContains(default))
            throw new CalibrationException("Measured tilt hull must strictly surround (0, 0); recapture missing directions.");
        return result;
    }

    // Capture-only checks use measured geometry and an explicit upright index, never requested-pose metadata.
    public static NodeMesh CompleteCapture(CalibrationSample[] samples, Point2 target, int uprightIndex)
    {
        var mesh = Prepare(samples, target);
        for (int i = 0; i < samples.Length; i++)
            for (int j = 0; j < i; j++)
                if (!Separated(samples[j].Tilt, samples[i].Tilt))
                    throw new CalibrationException($"Poses {j} and {i} are within 0.1 degree; recapture them.");
        if (uprightIndex < 0 || uprightIndex >= samples.Length)
            throw new CalibrationException("Captured upright pose index is invalid.");
        if (samples.Length > 1 && !mesh.StrictlyContains(samples[uprightIndex].Tilt))
            throw new CalibrationException($"Captured upright pose {uprightIndex} must be strictly inside the measured hull.");
        return mesh;
    }

    public static bool CanAddCapture(IEnumerable<CalibrationSample> samples, Point2 measuredTilt) =>
        measuredTilt.IsFinite && samples.All(s => Separated(s.Tilt, measuredTilt));

    private static bool Separated(Point2 a, Point2 b) => DistanceSquared(a, b) > 0.1 * 0.1;
    private static double DistanceSquared(Point2 a, Point2 b) => Point2.Dot(a - b, a - b);
    private static int Side(Point2 a, Point2 b, Point2 p) =>
        CGAlgorithmsDD.OrientationIndex(a.X, a.Y, b.X, b.Y, p.X, p.Y);
    private bool StrictlyContains(Point2 p) => boundary.Length >= 3
        && boundary.All(e => Side(tilts[e.A], tilts[e.B], p) > 0);

    public Point2 Evaluate(Point2 tilt)
    {
        if (!tilt.IsFinite) return default;
        if (errors.Length == 1) return errors[0];
        foreach (var (a, b, c) in triangles)
        {
            if (Side(tilts[a], tilts[b], tilt) < 0 || Side(tilts[b], tilts[c], tilt) < 0
                || Side(tilts[c], tilts[a], tilt) < 0) continue;
            var ab = tilts[b] - tilts[a]; var ac = tilts[c] - tilts[a]; var at = tilt - tilts[a];
            double denominator = Point2.Cross(ab, ac);
            double v = Point2.Cross(at, ac) / denominator, w = Point2.Cross(ab, at) / denominator;
            return errors[a] * (1 - v - w) + errors[b] * v + errors[c] * w;
        }

        // Boundary edges include collinear captured vertices, retaining their measured errors.
        double nearest = double.PositiveInfinity;
        Point2 error = default;
        foreach (var (a, b) in boundary)
        {
            var edge = tilts[b] - tilts[a];
            double fraction = Math.Clamp(Point2.Dot(tilt - tilts[a], edge) / Point2.Dot(edge, edge), 0, 1);
            double distance = DistanceSquared(tilt, tilts[a] + edge * fraction);
            if (distance < nearest)
            {
                nearest = distance;
                error = errors[a] * (1 - fraction) + errors[b] * fraction;
            }
        }
        return error;
    }
}
