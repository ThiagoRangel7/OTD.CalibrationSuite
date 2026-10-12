using System.Diagnostics;
using System.Numerics;
using CalibrationSuite.Core;
using CalibrationSuite.Otd;
using Xunit;

namespace CalibrationSuite.Tests;

public class OrientationVisualTests
{
    [Fact]
    public void UprightArrowCollapsesToThePhysicalNibInTopView()
    {
        Assert.Equal(new Point2(0, 0), OrientationScene.ArrowTip(new(90)));
    }

    [Theory]
    [InlineData(0, 0.8660254038, 0)] // Eraser left => arrow right.
    [InlineData(90, 0, -0.8660254038)] // Eraser down => arrow up.
    [InlineData(180, -0.8660254038, 0)]
    [InlineData(270, 0, 0.8660254038)]
    public void CardinalTopViewOpposesTheEraserAtThirtyDegrees(double azimuth, double x, double y)
    {
        var tip = OrientationScene.ArrowTip(new(30, azimuth));
        Assert.Equal(x, tip.X, 6); Assert.Equal(y, tip.Y, 6);
    }

    [Theory]
    [InlineData(35.264389682754654, 35.264389682754654, 0.5, -0.5)]
    [InlineData(-35.264389682754654, 35.264389682754654, -0.5, -0.5)]
    [InlineData(-35.264389682754654, -35.264389682754654, -0.5, 0.5)]
    [InlineData(35.264389682754654, -35.264389682754654, 0.5, 0.5)]
    public void DiagonalRawTiltUsesTheVerifiedAdapterAndReversesBothPlanarAxes(double rawX, double rawY, double x, double y)
    {
        var tilt = new Point2(rawX, rawY);
        var pose = CaptureDevice.Orientation(tilt, 0);
        var tip = OrientationScene.ArrowTip(pose);
        Assert.Equal(x, tip.X, 6); Assert.Equal(y, tip.Y, 6);
        Assert.Equal(new Point2(rawX, rawY), tilt); // Visualization never replaces measured coordinates.
    }

    [Fact]
    public void LiveDirectionRemainsContinuousInsideTheUprightGate()
    {
        var tilt = new Point2(1, -1);
        var gate = CaptureDevice.Orientation(tilt, 3);
        Assert.Null(gate.AzimuthDeg);
        Assert.True(OrientationGuidance.WithinTolerance(gate, new(90), 3));
        var tip = OrientationScene.ArrowTip(CaptureDevice.Orientation(tilt, 0));
        Assert.Equal(0.01744975, tip.X, 6); Assert.Equal(0.01744975, tip.Y, 6);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.25)]
    [InlineData(3)]
    [InlineData(12)]
    public void UprightTargetReflectsTheConfiguredToleranceWithoutAMinimumDecorativeRadius(double tolerance)
    {
        var space = OrientationScene.TargetSpace(new(90), tolerance);
        Assert.Equal(0, space.InnerRadius);
        Assert.Equal(Math.Sin(tolerance * Math.PI / 180), space.OuterRadius, 12);
        Assert.Null(space.AzimuthDeg);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(45)]
    [InlineData(90)]
    [InlineData(180)]
    [InlineData(270)]
    [InlineData(359)]
    public void TiltTargetMatchesAltitudeAndCircularAzimuthGates(double azimuth)
    {
        var requested = new RequestedPose(70, azimuth);
        var space = OrientationScene.TargetSpace(requested, 3);
        Assert.Equal(0.2923717047, space.InnerRadius, 9); // 73° altitude.
        Assert.Equal(0.3907311285, space.OuterRadius, 9); // 67° altitude.
        foreach (double altitude in new[] { 66.9, 67.01, 70, 72.99, 73.1 })
            foreach (double delta in new[] { -3.1, -2.99, 0, 2.99, 3.1 })
            {
                var current = new RequestedPose(altitude, (azimuth + delta + 360) % 360);
                var tip = OrientationScene.ArrowTip(current);
                double radius = Math.Sqrt(Point2.Dot(tip, tip));
                double tipAzimuth = Math.Atan2(-tip.Y, tip.X) * 180 / Math.PI;
                bool insideOutline = radius >= space.InnerRadius && radius <= space.OuterRadius
                    && Math.Abs(Math.IEEERemainder(tipAzimuth - space.AzimuthDeg!.Value, 360)) <= space.ToleranceDeg;
                Assert.Equal(OrientationGuidance.WithinTolerance(current, requested, 3), insideOutline);
            }
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
        AzimuthCount = 0,
        CaptureWindowMs = 10,
        MinSamplesPerCapture = 4,
        CaptureTimeoutMs = 100,
        ReportGapTimeoutMs = 40
    };
    private static ReportReading Reading(double time) => new((long)Math.Round(time * Stopwatch.Frequency / 1000), new(100, 100), Vector2.Zero, 0, "Paired raw pen reading");

    [Fact]
    public void BurstProgressUsesEligibleReportSpanAndBothMinima()
    {
        var capture = new CaptureController(new(Settings, Geometry), _ => new(90));
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
        Assert.Equal(CaptureCue.Accepted, CaptureFeedback.Resolve(accepted, new(90), 3, 30).Cue);
        Assert.Equal(CaptureCue.Ready, CaptureFeedback.Resolve(accepted, new(90), 3, 880).Cue);
        Assert.Equal(CaptureCue.Waiting, CaptureFeedback.Resolve(accepted, null, 3, 880).Cue);
    }

    [Fact]
    public void NavigationAndRestartRemoveAcceptedCuesAndRetriesCannotAppearAccepted()
    {
        var capture = new CaptureController(new(Settings with { MinSamplesPerCapture = 2 }, Geometry), _ => new(90));
        capture.Start(0); capture.Read(Reading(0)); capture.Read(Reading(10));
        capture.Previous(); Assert.Null(capture.Snapshot(11).AcceptedAtMs);
        Assert.Equal(CaptureCue.Ready, CaptureFeedback.Resolve(capture.Snapshot(11), new(90), 3, 11).Cue);
        capture.Start(11); Assert.Equal(CaptureCue.Capturing, CaptureFeedback.Resolve(capture.Snapshot(11), new(90), 3, 11).Cue);
        var retry = capture.Snapshot(52);
        Assert.Equal(CaptureBurstStatus.Retry, retry.BurstStatus); Assert.Equal(0, retry.BurstProgress);
        Assert.Equal(CaptureCue.Retry, CaptureFeedback.Resolve(retry, new(90), 3, 52).Cue);
        Assert.Equal(1, retry.Captured); // Retry preserves the prior measurement.
        capture.Restart(); var reset = capture.Snapshot(53);
        Assert.Equal(0, reset.Captured); Assert.Null(reset.AcceptedAtMs); Assert.Equal(CaptureBurstStatus.Idle, reset.BurstStatus);
    }

    [Fact]
    public void ReadinessStillRequiresAltitudeAndCircularAzimuthAndCannotUseMissingPose()
    {
        var state = new CaptureProgress(0, 0, 4, default, new(50, 1), false, 0, false, "");
        Assert.Equal(CaptureCue.Ready, CaptureFeedback.Resolve(state, new(50, 359), 3, 0).Cue);
        Assert.Equal(CaptureCue.Adjusting, CaptureFeedback.Resolve(state, new(54, 1), 3, 0).Cue);
        Assert.Equal(CaptureCue.Adjusting, CaptureFeedback.Resolve(state, new(50, 5), 3, 0).Cue);
        Assert.Equal(CaptureCue.Waiting, CaptureFeedback.Resolve(state, null, 3, 0).Cue);
    }

    [Fact]
    public void ReplacingAnExistingMeasurementStillProducesANewAcceptedCue()
    {
        var draft = new CaptureDraft(Settings with { MinSamplesPerCapture = 2 }, Geometry);
        var capture = new CaptureController(draft, _ => new(90));
        capture.Start(0); capture.Read(Reading(0)); capture.Read(Reading(10)); capture.Previous();
        capture.Start(12); capture.Read(Reading(12)); capture.Read(Reading(22));
        var snapshot = capture.Snapshot(22);
        Assert.Equal(1, snapshot.Captured); Assert.Equal(0, snapshot.AcceptedIndex); Assert.Equal(22, snapshot.AcceptedAtMs!.Value, 6);
        Assert.Equal(CaptureCue.Accepted, CaptureFeedback.Resolve(snapshot, new(90), 3, 22).Cue);
    }

    [Fact]
    public void SettingDiagnosticsIdentifyTheInputWithoutChangingValidationRules()
    {
        Assert.Equal(nameof(CaptureSettings.RowCount), Assert.Throws<CalibrationException>(() => (Settings with { RowCount = 1 }).Validate()).ParameterName);
        Assert.Equal(nameof(CaptureSettings.CaptureTimeoutMs), Assert.Throws<CalibrationException>(() => (Settings with { CaptureTimeoutMs = 10 }).Validate()).ParameterName);
        Assert.Equal(nameof(CaptureSettings.GridInsetMm), Assert.Throws<CalibrationException>(() => (Settings with { GridInsetMm = 100 }).CreateGrid(Geometry)).ParameterName);
        Assert.Equal(nameof(CaptureSettings.AzimuthCount), Assert.Throws<CalibrationException>(() => (Settings with { AzimuthCount = 2, NonUprightAltitudeCount = 1 }).Validate()).ParameterName);
    }

    [Fact]
    public void FailedMeshStorageKeepsARecoveryHintAndNeverAppearsAccepted()
    {
        var state = new CaptureProgress(4, 4, 20, default, new(50, 270), false, 0, false,
            "Measured tilt hull must strictly surround (0, 0); recapture missing directions.")
        { BurstStatus = CaptureBurstStatus.Retry, AcceptedAtMs = 999 };
        var feedback = CaptureFeedback.Resolve(state, new(50, 270), 3, 1000);
        Assert.Equal(CaptureCue.Retry, feedback.Cue);
        Assert.Contains("coverage", feedback.Hint); Assert.Contains("Backspace", feedback.Hint);
        Assert.Equal("Measured tilt hull must strictly surround (0, 0); recapture missing directions.", state.Message);
    }
}
