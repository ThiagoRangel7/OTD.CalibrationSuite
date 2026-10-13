using System.Diagnostics;
using System.Numerics;
using CalibrationSuite.Core;
using CalibrationSuite.Otd;
using Xunit;

namespace CalibrationSuite.Tests;

public class OrientationVisualTests
{
    [Theory]
    [InlineData(40, 0, 1, 0)]
    [InlineData(0, 40, 0, -1)]
    [InlineData(-40, 0, -1, 0)]
    [InlineData(0, -40, 0, 1)]
    [InlineData(20, 20, 0.5, -0.5)]
    public void ReportedCoordinatePlotHasKnownAxesAndScale(double x, double y, double plotX, double plotY)
    {
        Assert.Equal(new Point2(plotX, plotY), OrientationScene.ArrowTip(new(x, y), 40));
    }
    [Theory]
    [InlineData(1920, 1080)]
    [InlineData(960, 540)]
    [InlineData(640, 360)]
    [InlineData(480, 320)]
    public void OverlayProtectsTheAimingPointAtEdgesAndCenterIncludingSmallDpiViewports(double width, double height)
    {
        double margin = height < 560 ? 12 : 24;
        double pw = CaptureOverlay.Width(width, margin), ph = 208;
        foreach (double x in new[] { 0.01, 0.15, 0.5, 0.85, 0.99 })
            foreach (double y in new[] { 0.01, 0.15, 0.5, 0.85, 0.99 })
            {
                var target = new Point2(x * width, y * height);
                var overlay = CaptureOverlay.Place(width, height, target, pw, ph, margin);
                Assert.InRange(overlay.Left, margin, width - margin - pw);
                Assert.InRange(overlay.Top, margin, height - margin - ph);
                double nearestX = Math.Clamp(target.X, overlay.Left, overlay.Left + pw);
                double nearestY = Math.Clamp(target.Y, overlay.Top, overlay.Top + ph);
                double distance = Math.Sqrt(Math.Pow(target.X - nearestX, 2) + Math.Pow(target.Y - nearestY, 2));
                Assert.True(distance >= 32 - 1e-8, $"Aiming point {target} too close to HUD: {distance}"); // 20-DIP reticle + 12-DIP gap.
                double secondaryHeight = 106;
                var secondary = CaptureOverlay.Place(width, height, target, pw, secondaryHeight, margin, overlay);
                Assert.False(secondary.Overlaps(overlay, 8));
                nearestX = Math.Clamp(target.X, secondary.Left, secondary.Left + pw);
                nearestY = Math.Clamp(target.Y, secondary.Top, secondary.Top + secondaryHeight);
                distance = Math.Sqrt(Math.Pow(target.X - nearestX, 2) + Math.Pow(target.Y - nearestY, 2));
                Assert.True(distance >= 32 - 1e-8, $"Aiming point {target} too close to readings: {distance}");
                double radius = CaptureOverlay.GuidanceRadius(width, height, target, overlay, secondary);
                Assert.InRange(radius, 0, CaptureOverlay.MaxGuidanceRadius);
                Assert.True(radius + 5 <= Math.Min(Math.Min(target.X, width - target.X), Math.Min(target.Y, height - target.Y)) || radius == 0);
                Assert.True(radius + 12 <= overlay.DistanceTo(target));
                Assert.True(radius + 12 <= secondary.DistanceTo(target));
            }
    }

    private static readonly SourceGeometry Geometry = new(20000, 10000, 200, 200);
    private static readonly CaptureSettings Settings = new()
    {
        RowCount = 2,
        ColumnCount = 2,
        TiltDirections = 0,
        CaptureWindowMs = 10,
        MinSamplesPerCapture = 4,
        CaptureTimeoutMs = 100,
        ReportGapTimeoutMs = 40
    };
    private static ReportReading Reading(double time) => new((long)Math.Round(time * Stopwatch.Frequency / 1000), new(100, 100), Vector2.Zero, 0, "Paired raw pen reading");

    [Fact]
    public void BurstProgressUsesEligibleReportSpanAndBothMinima()
    {
        var capture = new CaptureController(new(Settings, Geometry));
        capture.Start(0);
        capture.Read(Reading(-1)); Assert.Equal(0, capture.Snapshot(0).BurstProgress);
        capture.Read(Reading(20)); Assert.Equal(0, capture.Snapshot(20).BurstProgress);
        capture.Read(Reading(25)); Assert.Equal(0.5, capture.Snapshot(25).BurstProgress, 6);
        capture.Read(Reading(25)); Assert.Equal(0.5, capture.Snapshot(25).BurstProgress, 6);
        capture.Read(Reading(25)); Assert.Equal(0.5, capture.Snapshot(25).BurstProgress, 6);
        capture.Read(Reading(30));
        var accepted = capture.Snapshot(30);
        Assert.Equal(1, accepted.BurstProgress); Assert.Equal(10, accepted.BurstDurationMs, 6);
        Assert.Equal(CaptureBurstStatus.Accepted, accepted.BurstStatus);
        Assert.Equal(0, accepted.AcceptedIndex); Assert.Equal(1, accepted.Index);
        Assert.Equal(CaptureCue.Accepted, CaptureFeedback.Resolve(accepted, Reading(0), 3, 30).Cue);
        Assert.Equal(CaptureCue.Ready, CaptureFeedback.Resolve(accepted, Reading(0), 3, 880).Cue);
        Assert.Equal(CaptureCue.Waiting, CaptureFeedback.Resolve(accepted, null, 3, 880).Cue);
    }

    [Fact]
    public void NavigationAndRestartRemoveAcceptedCuesAndRetriesCannotAppearAccepted()
    {
        var capture = new CaptureController(new(Settings with { MinSamplesPerCapture = 2 }, Geometry));
        capture.Start(0); capture.Read(Reading(0)); capture.Read(Reading(10));
        capture.Previous(); Assert.Null(capture.Snapshot(11).AcceptedAtMs);
        Assert.Equal(CaptureCue.Ready, CaptureFeedback.Resolve(capture.Snapshot(11), Reading(0), 3, 11).Cue);
        capture.Start(11); Assert.Equal(CaptureCue.Capturing, CaptureFeedback.Resolve(capture.Snapshot(11), Reading(0), 3, 11).Cue);
        var retry = capture.Snapshot(52);
        Assert.Equal(CaptureBurstStatus.Retry, retry.BurstStatus); Assert.Equal(0, retry.BurstProgress);
        Assert.Equal(CaptureCue.Retry, CaptureFeedback.Resolve(retry, Reading(0), 3, 52).Cue);
        Assert.Equal(1, retry.Captured); // Retry preserves the prior measurement.
        capture.Restart(); var reset = capture.Snapshot(53);
        Assert.Equal(0, reset.Captured); Assert.Null(reset.AcceptedAtMs); Assert.Equal(CaptureBurstStatus.Idle, reset.BurstStatus);
    }

    [Fact]
    public void ReadinessUsesTheReportedCoordinateCircleAndNeedsAFreshPenReading()
    {
        var state = new CaptureProgress(1, 1, 20, default, new(40, 0), false, 0, false, "")
        { UsesTilt = true, ReferenceTilt = new(100, 200), RequestedTilt = new(140, 200) };
        Assert.Equal(CaptureCue.Ready, CaptureFeedback.Resolve(state, Reading(0) with { Tilt = new(143, 204) }, 5, 0).Cue);
        Assert.Equal(CaptureCue.Adjusting, CaptureFeedback.Resolve(state, Reading(0) with { Tilt = new(144, 204) }, 5, 0).Cue);
        Assert.Equal(CaptureCue.Waiting, CaptureFeedback.Resolve(state, null, 5, 0).Cue);
    }

    [Fact]
    public void ReplacingAnExistingMeasurementStillProducesANewAcceptedCue()
    {
        var draft = new CaptureDraft(Settings with { MinSamplesPerCapture = 2 }, Geometry);
        var capture = new CaptureController(draft);
        capture.Start(0); capture.Read(Reading(0)); capture.Read(Reading(10)); capture.Previous();
        capture.Start(12); capture.Read(Reading(12)); capture.Read(Reading(22));
        var snapshot = capture.Snapshot(22);
        Assert.Equal(1, snapshot.Captured); Assert.Equal(0, snapshot.AcceptedIndex); Assert.Equal(22, snapshot.AcceptedAtMs!.Value, 6);
        Assert.Equal(CaptureCue.Accepted, CaptureFeedback.Resolve(snapshot, Reading(0), 3, 22).Cue);
    }

    [Fact]
    public void SettingDiagnosticsIdentifyTheInputWithoutChangingValidationRules()
    {
        Assert.Equal(nameof(CaptureSettings.RowCount), Assert.Throws<CalibrationException>(() => (Settings with { RowCount = 1 }).Validate()).ParameterName);
        Assert.Equal(nameof(CaptureSettings.CaptureTimeoutMs), Assert.Throws<CalibrationException>(() => (Settings with { CaptureTimeoutMs = 10 }).Validate()).ParameterName);
        Assert.Equal(nameof(CaptureSettings.GridInsetMm), Assert.Throws<CalibrationException>(() => (Settings with { GridInsetMm = 100 }).CreateGrid(Geometry)).ParameterName);
        Assert.Equal(nameof(CaptureSettings.TiltDirections), Assert.Throws<CalibrationException>(() => (Settings with { TiltDirections = 2, TiltLevels = 1 }).Validate()).ParameterName);
    }

    [Fact]
    public void FailedMeshStorageKeepsARecoveryHintAndNeverAppearsAccepted()
    {
        var state = new CaptureProgress(4, 4, 20, default, new(50, 270), false, 0, false,
            "Tilt mesh does not retain every distinct sample; recapture unusable or collinear coverage.")
        { BurstStatus = CaptureBurstStatus.Retry, AcceptedAtMs = 999 };
        var feedback = CaptureFeedback.Resolve(state, Reading(0), 3, 1000);
        Assert.Equal(CaptureCue.Retry, feedback.Cue);
        Assert.Contains("coverage", feedback.Hint); Assert.Contains("revisit", feedback.Hint);
        Assert.Equal("Tilt mesh does not retain every distinct sample; recapture unusable or collinear coverage.", state.Message);
    }
}
