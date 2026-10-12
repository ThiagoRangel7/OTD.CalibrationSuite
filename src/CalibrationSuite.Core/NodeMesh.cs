using NetTopologySuite.Algorithm;
using NetTopologySuite.Geometries;
using NetTopologySuite.Triangulate;

namespace CalibrationSuite.Core;

public sealed class NodeMesh
{
    private readonly Point2[] tilts, errors;
    private readonly Triangle[] triangles;
    private readonly BoundaryEdge[] boundary;

    private readonly record struct Triangle(int A, int B, int C, Point2 AB, Point2 AC,
        double InverseArea, Envelope Bounds);
    private readonly record struct BoundaryEdge(int A, int B, Point2 Delta, double InverseLengthSquared);

    private NodeMesh(Point2[] tilts, Point2[] errors, Triangle[] triangles, BoundaryEdge[] boundary)
    {
        this.tilts = tilts;
        this.errors = errors;
        this.triangles = triangles;
        this.boundary = boundary;
    }

    public static NodeMesh Prepare(CalibrationSample[] samples, Point2 target, Point2? capturedUpright = null)
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
        var triangles = new List<Triangle>();
        Span<int> ids = stackalloc int[3];
        var retained = new bool[tilts.Length];
        for (int i = 0; i < mesh.NumGeometries; i++)
        {
            var polygon = mesh.GetGeometryN(i);
            var vertices = polygon.Coordinates;
            for (int j = 0; j < ids.Length; j++) ids[j] = indices[new Point2(vertices[j].X, vertices[j].Y)];
            ids.Sort();
            int a = ids[0], b = ids[1], c = ids[2];
            if (Side(tilts[a], tilts[b], tilts[c]) < 0) (b, c) = (c, b);
            var ab = tilts[b] - tilts[a];
            var ac = tilts[c] - tilts[a];
            double area = Point2.Cross(ab, ac);
            if (!double.IsFinite(area) || area <= 0 || !double.IsFinite(1 / area))
                throw new CalibrationException("Tilt triangle is numerically unusable; recapture its poses.");
            triangles.Add(new(a, b, c, ab, ac, 1 / area, polygon.EnvelopeInternal));
            retained[a] = retained[b] = retained[c] = true;
        }
        triangles.Sort(static (left, right) => (left.A, left.B, left.C).CompareTo((right.A, right.B, right.C)));
        if (retained.Any(value => !value))
            throw new CalibrationException("Tilt mesh does not retain every distinct sample; recapture unusable or collinear coverage.");
        var edges = triangles.SelectMany(t => new[] { (t.A, t.B), (t.B, t.C), (t.C, t.A) });
        var boundary = edges.GroupBy(e => (Math.Min(e.Item1, e.Item2), Math.Max(e.Item1, e.Item2)))
            .Where(g => g.Count() == 1).Select(g => g.First()).Order().Select(e => PrepareEdge(e, tilts)).ToArray();
        var result = new NodeMesh(tilts, errors, triangles.ToArray(), boundary);
        if (!result.StrictlyContains(default))
            throw new CalibrationException("Measured tilt hull must strictly surround (0, 0); recapture missing directions.");
        if (capturedUpright is { } upright && !result.StrictlyContains(upright))
            throw new CalibrationException("Captured upright pose must be strictly inside the measured hull.");
        return result;
    }

    private static BoundaryEdge PrepareEdge((int A, int B) edge, Point2[] tilts)
    {
        var delta = tilts[edge.B] - tilts[edge.A];
        double lengthSquared = Point2.Dot(delta, delta);
        if (!double.IsFinite(lengthSquared) || lengthSquared <= 0 || !double.IsFinite(1 / lengthSquared))
            throw new CalibrationException("Tilt boundary is numerically unusable; recapture its poses.");
        return new(edge.A, edge.B, delta, 1 / lengthSquared);
    }

    internal static bool CaptureTiltsSeparated(Point2 a, Point2 b) => DistanceSquared(a, b) > 0.1 * 0.1;
    private static double DistanceSquared(Point2 a, Point2 b) => Point2.Dot(a - b, a - b);
    private static int Side(Point2 a, Point2 b, Point2 p) =>
        CGAlgorithmsDD.OrientationIndex(a.X, a.Y, b.X, b.Y, p.X, p.Y);
    private bool StrictlyContains(Point2 p)
    {
        if (boundary.Length < 3) return false;
        foreach (var edge in boundary)
            if (Side(tilts[edge.A], tilts[edge.B], p) <= 0) return false;
        return true;
    }

    public Point2 Evaluate(Point2 tilt)
    {
        if (!tilt.IsFinite) return default;
        if (errors.Length == 1) return errors[0];
        foreach (var triangle in triangles)
        {
            // Bounds reject unrelated triangles cheaply; the robust predicates still
            // decide inclusion at edges and nearly collinear measured vertices.
            if (!triangle.Bounds.Covers(tilt.X, tilt.Y)) continue;
            int a = triangle.A, b = triangle.B, c = triangle.C;
            if (Side(tilts[a], tilts[b], tilt) < 0 || Side(tilts[b], tilts[c], tilt) < 0
                || Side(tilts[c], tilts[a], tilt) < 0) continue;
            var at = tilt - tilts[a];
            double v = Point2.Cross(at, triangle.AC) * triangle.InverseArea;
            double w = Point2.Cross(triangle.AB, at) * triangle.InverseArea;
            return errors[a] * (1 - v - w) + errors[b] * v + errors[c] * w;
        }

        // Boundary edges include collinear captured vertices, retaining their measured errors.
        double nearest = double.PositiveInfinity;
        Point2 error = default;
        foreach (var boundaryEdge in boundary)
        {
            int a = boundaryEdge.A, b = boundaryEdge.B;
            var edge = boundaryEdge.Delta;
            double fraction = Math.Clamp(Point2.Dot(tilt - tilts[a], edge) * boundaryEdge.InverseLengthSquared, 0, 1);
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
