using Xunit;
using CalibrationSuite.Core;
using static CalibrationSuite.Tests.CoreFixtures;

namespace CalibrationSuite.Tests;

public class InterpolationTests
{
    [Fact]
    public void EveryMeasuredVertexIncludingOffsetUprightReproducesItsOwnError()
    {
        Point2 target = new(300, 500);
        Point2[] tilts = [new(-40, -40), new(-40, 40), new(1, 2), new(40, -40), new(40, 40), new(0, -40)];
        var samples = Samples(target, t => new(t.X * t.X / 10 + 9, t.Y * 2 - 3), tilts);
        var mesh = NodeMesh.Prepare(samples, target);
        foreach (var sample in samples)
            Near(sample.Position - target, mesh.Evaluate(sample.Tilt));
        Near(new(9.1, 1), mesh.Evaluate(new(1, 2)));
    }

    [Fact]
    public void BarycentricInterpolationMatchesIndependentTriangleCalculation()
    {
        // Triangle origin, (40,0), (0,40): at (10,20), weights are 1/4, 1/4, 1/2.
        Point2[] tilts = [new(0, 0), new(40, 0), new(0, 40), new(-40, 0), new(0, -40)];
        var samples = Samples(default, t => t == default ? new(4, 8) : t.X == 40 ? new(20, -4) : new(8, 12), tilts);
        Near(new(10, 7), NodeMesh.Prepare(samples, default).Evaluate(new(10, 20)));
    }

    [Theory]
    [InlineData(60, 10, 40, 10)]
    [InlineData(60, 60, 40, 40)]
    [InlineData(-60, -10, -40, -10)]
    [InlineData(0, -80, 0, -40)]
    public void OutsideHullProjectsToNearestEdgeOrCorner(double x, double y, double expectedX, double expectedY)
    {
        var mesh = NodeMesh.Prepare(Samples(default, t => t), default);
        Near(new(expectedX, expectedY), mesh.Evaluate(new(x, y)));
    }

    [Fact]
    public void PolygonalRingClampsInsideItsCircumcircle()
    {
        Point2[] tilts = [new(40, 0), new(0, 40), new(-40, 0), new(0, -40)];
        var mesh = NodeMesh.Prepare(Samples(default, t => t, tilts), default);
        Near(new(20, 20), mesh.Evaluate(new(28, 28)));
    }

    [Fact]
    public void ObliqueHullProjectionInterpolatesTheMeasuredEdgeErrors()
    {
        Point2[] tilts = [new(-30, -10), new(10, -30), new(30, 10), new(-10, 30), default];
        var mesh = NodeMesh.Prepare(Samples(default, t => new(t.X * t.X, t.Y * t.Y), tilts), default);
        // (35,-5) projects to (25,0), 3/4 along (10,-30) -> (30,10).
        Near(new(700, 300), mesh.Evaluate(new(35, -5)));
    }

    [Fact]
    public void InteriorCollinearAndBoundaryCollinearVerticesAreRetained()
    {
        Point2[] tilts = [.. Tilts, new(-10, 0), new(10, 0), new(0, -40), new(0, 40)];
        var samples = Samples(default, t => new(t.X * t.X + t.Y, t.Y * t.Y - t.X), tilts);
        var mesh = NodeMesh.Prepare(samples, default);
        foreach (var sample in samples)
            Near(sample.Position, mesh.Evaluate(sample.Tilt));
        Near(new(40, 1600), mesh.Evaluate(new(0, 80)));
    }

    [Fact]
    public void CocircularAndNearlyCollinearSamplesAreDeterministicUnderReordering()
    {
        Point2[] tilts = [.. Tilts, new(-10, 1e-10), new(10, -1e-10)];
        var samples = Samples(default, t => new(t.X * t.X + 1, t.X * t.Y + t.Y), tilts);
        var original = NodeMesh.Prepare(samples, default);
        var random = new Random(123);
        for (int i = 0; i < 12; i++)
        {
            var shuffled = samples.OrderBy(_ => random.Next()).ToArray();
            var reordered = NodeMesh.Prepare(shuffled, default);
            foreach (var tilt in new Point2[] { new(3, 7), new(0, 0), new(60, 8), new(-5, -7) })
                Assert.Equal(original.Evaluate(tilt), reordered.Evaluate(tilt));
        }
    }

    [Fact]
    public void TriangleEdgesAreContinuousForNonlinearMeasuredErrors()
    {
        var mesh = NodeMesh.Prepare(Samples(default, t => new(t.X * t.X, t.Y * t.Y)), default);
        var onEdge = mesh.Evaluate(new(10, 10));
        Near(onEdge, mesh.Evaluate(new(10 - 1e-8, 10 + 1e-8)), 1e-5);
        Near(onEdge, mesh.Evaluate(new(10 + 1e-8, 10 - 1e-8)), 1e-5);
    }

    [Fact]
    public void InvalidHullsDuplicatesAndInsufficientCoverageAreRejected()
    {
        Point2[][] invalid =
        [
            [new(-1, 0), new(0, 0), new(1, 0), new(2, 0)], // collinear
            [new(-40, -40), new(-40, 40), new(40, 0)], // only three
            [.. Tilts, new(0, 0)] // duplicate
        ];
        foreach (var tilts in invalid)
            Assert.Throws<CalibrationException>(() => NodeMesh.Prepare(Samples(default, t => t, tilts), default));
    }

    [Fact]
    public void LoadedMeshesNeedNoOriginCoverageOrCaptureSeparation()
    {
        Point2[] ring = [new(-40, -40), new(-40, 40), new(40, -40), new(40, 40)];
        var samples = Samples(default, t => t, ring);
        Near(default, NodeMesh.Prepare(samples, default).Evaluate(default));
        var translated = samples.Select(s => s with { Tilt = s.Tilt + new Point2(100, 200) }).ToArray();
        Near(default, NodeMesh.Prepare(translated, default).Evaluate(new(100, 200)));
        var close = Samples(default, t => t, [.. Tilts, new(0.05, 0)]);
        _ = NodeMesh.Prepare(close, default);
    }

    [Fact]
    public void SpatialBlendUsesNonuniformGridAndOriginalPositionWithExpectedResidual()
    {
        var data = Data((target, _) => new(target.X * 0.4, target.Y * 0.2), [100, 300, 900], [100, 600, 900]);
        var prepared = PreparedCalibration.Prepare(data, Geometry);
        // Raw (500,500) -> error (200,100), using the original lookup only.
        Near(new(300, 400), prepared.Correct(new(500, 500), default));
        // At target (600,300), capture is (840,360). The field is approximate, not its inverse.
        // Lookup at capture yields (336,72), leaving residual (-96,-12) relative to the target.
        Near(new(504, 288), prepared.Correct(new(840, 360), default));
        Near(prepared.Correct(new(600 - 1e-8, 300 - 1e-8), default),
            prepared.Correct(new(600 + 1e-8, 300 + 1e-8), default), 1e-7);
    }

    [Fact]
    public void LookupClampDoesNotClampTheActualOutputToTheGrid()
    {
        var prepared = PreparedCalibration.Prepare(Data(), Geometry);
        Near(new(-22, 1097), prepared.Correct(new(-20, 1100), default));
    }

    [Fact]
    public void FourNodesEvaluateTheirOwnMeshesAndBoundariesBeforeBlending()
    {
        var data = Data();
        Point2[][] rings =
        [
            [new(-40, -40), new(-40, 40), new(40, -40), new(40, 40)],
            [new(-20, -20), new(-20, 20), new(20, -20), new(20, 20), new(0, 0)],
            [new(-30, 0), new(0, 30), new(30, 0), new(0, -30)],
            [new(-40, -40), new(-40, 40), new(40, -40), new(40, 40), new(0, 0), new(10, 0)]
        ];
        for (int r = 0; r < 2; r++)
            for (int c = 0; c < 2; c++)
            {
                var target = new Point2(data.Grid!.PreTransformColumnCoordinates[c], data.Grid.PreTransformRowCoordinates[r]);
                data.Nodes![r][c] = new(Samples(target, t => t, rings[r * 2 + c]));
            }
        // At tilt (30,20), projections are (30,20),(20,20),(20,10),(30,20).
        Near(new(475, 482.5), PreparedCalibration.Prepare(data, Geometry).Correct(new(500, 500), new(30, 20)));
    }

    [Fact]
    public void EvaluationHasNoManagedAllocationsAndUnusableInputPassesThrough()
    {
        var prepared = PreparedCalibration.Prepare(Data(), Geometry);
        for (int i = 0; i < 100; i++)
            _ = prepared.Correct(new(400, 500), new(90, 12));
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++)
            _ = prepared.Correct(new(400, 500), new(i % 80, 12));
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(new Point2(400, 500), prepared.Correct(new(400, 500), new(double.NaN, 12)));
    }
}
