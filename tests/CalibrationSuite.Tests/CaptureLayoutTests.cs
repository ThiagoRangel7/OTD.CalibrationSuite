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
        Assert.Equal(new RequestedPose(0), poses[0]);
        Assert.Equal(new RequestedPose(20, 0), poses[1]);
        Assert.Equal(new RequestedPose(40, 0), poses[2]);
        Assert.Equal(new RequestedPose(20, 45), poses[3]);
        Assert.Equal(new RequestedPose(20, 315), poses[15]);
        Assert.Equal(new RequestedPose(40, 315), poses[^1]);
    }

    [Fact]
    public void CustomCountsResolveEndpointsAndTraversal()
    {
        var settings = new CaptureSettings { RowCount = 2, ColumnCount = 4, GridInsetMm = 10, TiltDirections = 4, TiltLevels = 1 };
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
            defaults with { RowCount = 1 }, defaults with { ColumnCount = 1 }, defaults with { TiltDirections = 2 },
            defaults with { TiltLevels = -1 }, defaults with { TiltDirections = -1 }, defaults with { TiltRange = double.NaN },
            defaults with { TiltRange = 0 }, defaults with { TiltTolerance = 10 },
            defaults with { TiltLevels = 7 },
            defaults with { GridInsetMm = 50 }, defaults with { GridInsetMm = double.NaN },
            defaults with { CaptureWindowMs = 0 }, defaults with { CaptureTimeoutMs = defaults.CaptureWindowMs },
            defaults with { MinSamplesPerCapture = 0 }, defaults with { ReportGapTimeoutMs = 0 },
            defaults with { PositionStabilityMm = 0 }, defaults with { TiltStability = double.PositiveInfinity }
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
        var settings = new CaptureSettings { TiltDirections = azimuths, TiltLevels = levels };
        Assert.Equal(new RequestedPose(0), Assert.Single(settings.CreatePoses()));
        var grid = settings.CreateGrid(Geometry);
        Assert.Equal(9, grid.PreTransformRowCoordinates.Length * grid.PreTransformColumnCoordinates.Length);
    }

    [Theory]
    [InlineData(0, 40, 0)]
    [InlineData(90, 0, 40)]
    [InlineData(180, -40, 0)]
    [InlineData(270, 0, -40)]
    public void TiltTargetsCoverReportedCoordinateDirections(double direction, double x, double y)
    {
        var offset = new RequestedPose(40, direction).Offset;
        Assert.Equal(x, offset.X, 8); Assert.Equal(y, offset.Y, 8);
    }
}