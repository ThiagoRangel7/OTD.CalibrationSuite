using Xunit;
using CalibrationSuite.Core;
using static CalibrationSuite.Tests.CoreFixtures;

namespace CalibrationSuite.Tests;

public class CaptureLayoutTests
{
    [Fact]
    public void DefaultsResolveNineTargetsAndSeventeenPosesInSpecifiedOrder()
    {
        var settings = new CaptureSettings();
        var grid = settings.CreateGrid(new(20000, 10000, 200, 100));
        Assert.Equal(new double[] { 1500, 5000, 8500 }, grid.PreTransformRowCoordinates);
        Assert.Equal(new double[] { 1500, 10000, 18500 }, grid.PreTransformColumnCoordinates);
        var poses = settings.CreatePoses();
        Assert.Equal(153, grid.PreTransformRowCoordinates.Length * grid.PreTransformColumnCoordinates.Length * poses.Length);
        Assert.Equal(new RequestedPose(90), poses[0]);
        Assert.Equal(new RequestedPose(70, 0), poses[1]);
        Assert.Equal(new RequestedPose(50, 0), poses[2]);
        Assert.Equal(new RequestedPose(70, 45), poses[3]);
        Assert.Equal(new RequestedPose(70, 315), poses[15]);
        Assert.Equal(new RequestedPose(50, 315), poses[^1]);
    }

    [Fact]
    public void CustomCountsResolveEndpointsAndTraversal()
    {
        var settings = new CaptureSettings { RowCount = 2, ColumnCount = 4, GridInsetMm = 10, AzimuthCount = 4, NonUprightAltitudeCount = 1 };
        var grid = settings.CreateGrid(Geometry);
        Assert.Equal(new double[] { 100, 900 }, grid.PreTransformRowCoordinates);
        Assert.Equal(100, grid.PreTransformColumnCoordinates[0]);
        Assert.Equal(900, grid.PreTransformColumnCoordinates[^1]);
        Assert.Equal(5, settings.CreatePoses().Length);
    }

    [Fact]
    public void InvalidSettingsAndGeometryAreRejectedWithoutAdjustingThem()
    {
        CaptureSettings defaults = new();
        CaptureSettings[] invalid =
        [
            defaults with { RowCount = 1 }, defaults with { ColumnCount = 1 }, defaults with { AzimuthCount = 2 },
            defaults with { NonUprightAltitudeCount = -1 }, defaults with { AzimuthCount = -1 }, defaults with { MinAltitudeDeg = 90 },
            defaults with { MinAltitudeDeg = 0 }, defaults with { AngleToleranceDeg = 10 },
            defaults with { AzimuthCount = 60 }, defaults with { NonUprightAltitudeCount = 7 },
            defaults with { GridInsetMm = 50 }, defaults with { GridInsetMm = double.NaN },
            defaults with { CaptureWindowMs = 0 }, defaults with { CaptureTimeoutMs = 250 },
            defaults with { MinSamplesPerCapture = 0 }, defaults with { ReportGapTimeoutMs = 0 },
            defaults with { PositionStabilityMm = 0 }, defaults with { TiltStabilityDeg = double.PositiveInfinity }
        ];
        foreach (var settings in invalid) Assert.Throws<CalibrationException>(() => settings.CreateGrid(Geometry));
        Assert.Throws<CalibrationException>(() => defaults.CreateGrid(Geometry with { MaxX = 0 }));
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(8, 0)]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(60, 0)]
    [InlineData(0, 100)]
    public void EitherZeroCountProducesOnlyUprightAtEveryGridTarget(int azimuths, int levels)
    {
        var settings = new CaptureSettings { AzimuthCount = azimuths, NonUprightAltitudeCount = levels };
        Assert.Equal(new RequestedPose(90), Assert.Single(settings.CreatePoses()));
        var grid = settings.CreateGrid(Geometry);
        Assert.Equal(9, grid.PreTransformRowCoordinates.Length * grid.PreTransformColumnCoordinates.Length);
    }

    [Theory]
    [InlineData(-1, 0, 0)]
    [InlineData(0, 1, 90)]
    [InlineData(1, 0, 180)]
    [InlineData(0, -1, 270)]
    public void VerifiedScreenTangentDirectionsProduceSpecifiedGuidance(double x, double y, double azimuth)
    {
        var pose = OrientationGuidance.FromScreenTangents(new(x, y), 3);
        Assert.Equal(45, pose.AltitudeDeg, 10); Assert.Equal(azimuth, pose.AzimuthDeg);
    }

    [Fact]
    public void UprightOmitsAzimuthAndAzimuthComparisonWraps()
    {
        Assert.Equal(new RequestedPose(90), OrientationGuidance.FromScreenTangents(default, 3));
        Assert.Null(OrientationGuidance.FromScreenTangents(new(Math.Tan(Math.PI / 180), 0), 3).AzimuthDeg);
        Assert.Equal(2, OrientationGuidance.AzimuthDistance(359, 1));
        Assert.Equal(180, OrientationGuidance.AzimuthDistance(0, 180));
        Assert.Throws<CalibrationException>(() => OrientationGuidance.FromScreenTangents(new(double.NaN, 0), 3));
    }
}
