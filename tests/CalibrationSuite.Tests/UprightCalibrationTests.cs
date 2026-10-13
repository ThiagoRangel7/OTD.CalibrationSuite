using CalibrationSuite.Core;
using Xunit;
using static CalibrationSuite.Tests.CoreFixtures;

namespace CalibrationSuite.Tests;

public class UprightCalibrationTests
{
    [Fact]
    public void OneMeasuredSampleGivesConstantErrorWithoutMovingItsTiltToZero()
    {
        var sample = new CalibrationSample(new(153, 148), new(1, -2), new(90));
        var mesh = NodeMesh.Prepare([sample], new(150, 150));
        foreach (var tilt in new Point2[] { default, sample.Tilt, new(40, -30), new(-80, 90) })
            Near(new(3, -2), mesh.Evaluate(tilt));
    }

    [Fact]
    public void SingleSampleJsonRoundTripKeepsSpatialVariationAndLookupOnlyClamping()
    {
        var data = Data();
        for (int r = 0; r < 2; r++)
            for (int c = 0; c < 2; c++)
            {
                var target = new Point2(data.Grid!.PreTransformColumnCoordinates[c], data.Grid.PreTransformRowCoordinates[r]);
                data.Nodes![r][c] = new([new(target + new Point2(target.X * 0.1, target.Y * 0.2), new(1, -2))]);
            }
        var json = data.ToJson();
        var roundTrip = CalibrationData.Parse(json);
        Assert.Equal(new Point2(1, -2), Assert.Single(roundTrip.Nodes![0][0].Samples).Tilt);
        var prepared = PreparedCalibration.Load(json, Geometry, out var diagnostic);
        Assert.Null(diagnostic);
        Assert.False(prepared.IsIdentity);
        Near(new(450, 400), prepared.Correct(new(500, 500), new(30, -20)));
        Near(new(450, 400), prepared.Correct(new(500, 500), default));
        Near(new(-35, 930), prepared.Correct(new(-20, 1100), new(-45, 60)));
        roundTrip.Nodes[0][0].Samples[0] = new(default, default);
        Near(new(450, 400), prepared.Correct(new(500, 500), default));
    }

    [Fact]
    public void ConstantAndTriangulatedNodesUseTheSameSpatialBlend()
    {
        var data = Data((_, t) => t);
        data.Nodes![0][0] = new([new(new(154, 158), new(1, 2))]);
        // At grid center: one constant error (4,8), three tilt errors (20,12).
        Near(new(484, 489), PreparedCalibration.Prepare(data, Geometry).Correct(new(500, 500), new(20, 12)));
    }

    [Fact]
    public void SingleSampleStillRequiresFiniteDataAndTwoOrThreeSamplesAreNotConstantFallbacks()
    {
        foreach (int count in new[] { 0, 2, 3 })
            Assert.Throws<CalibrationException>(() => NodeMesh.Prepare(Samples(default, t => t).Take(count).ToArray(), default));
        Assert.Throws<CalibrationException>(() => NodeMesh.Prepare([new(new(double.NaN, 0), default)], default));
        Assert.Throws<CalibrationException>(() => NodeMesh.Prepare([new(default, new(0, double.PositiveInfinity))], default));
        var data = Data();
        data.Nodes![0][0] = new([new(new(154, 158), new(1, 2))]);
        var prepared = PreparedCalibration.Prepare(data, Geometry);
        Assert.Equal(new Point2(500, 500), prepared.Correct(new(500, 500), new(double.NaN, 0)));
    }
}
