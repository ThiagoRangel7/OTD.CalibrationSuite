using System.Diagnostics;
using System.Numerics;
using CalibrationSuite.Core;
using CalibrationSuite.Otd;
using OpenTabletDriver.Desktop.RPC;
using OpenTabletDriver.Plugin.Tablet;
using Xunit;

namespace CalibrationSuite.Tests;

public class CaptureTests
{
    private static readonly SourceGeometry Geometry = new(20000, 10000, 200, 200);
    private static readonly CaptureSettings Settings = new()
    {
        RowCount = 2,
        ColumnCount = 2,
        AzimuthCount = 0,
        CaptureWindowMs = 10,
        MinSamplesPerCapture = 2,
        CaptureTimeoutMs = 100,
        ReportGapTimeoutMs = 40,
        AngleToleranceDeg = 3,
        PositionStabilityMm = 0.25,
        TiltStabilityDeg = 2
    };
    private static RequestedPose Orientation(Point2 tilt) => CaptureDevice.Orientation(tilt, 3);
    private static CaptureBurst Burst(CaptureSettings? settings = null) => new(settings ?? Settings, Geometry, Orientation);

    [Fact]
    public void DurationStartsAtFirstEligibleReportAndBothMinimaMustPass()
    {
        var burst = Burst(Settings with { MinSamplesPerCapture = 3 });
        Assert.True(burst.Start(new(90), 0)); Assert.False(burst.Start(new(90), 2));
        Assert.Null(burst.Add(-1, default, default)); Assert.Equal(0, burst.Count);
        Assert.Null(burst.Add(20, new(100, 200), default));
        Assert.Null(burst.Add(25, new(100, 200), default));
        Assert.Null(burst.Add(29, new(100, 200), default));
        Assert.NotNull(burst.Add(30, new(100, 200), default));
        Assert.False(burst.Active); Assert.Equal(4, burst.Count);
        burst.Start(new(90), 50);
        Assert.Null(burst.Add(50, default, default)); Assert.Null(burst.Add(60, default, default));
        Assert.NotNull(burst.Add(60, default, default)); // Repeated event/time is a distinct sample.
    }

    [Fact]
    public void EvenMediansUseActualMeasurementsRatherThanRequestedPoses()
    {
        var burst = Burst(); burst.Start(new(90), 0);
        burst.Add(0, new(100, 200), new(0, 1));
        var sample = burst.Add(10, new(102, 204), new(2, 0));
        Assert.Equal(new Point2(101, 202), sample!.Position);
        Assert.Equal(new Point2(1, 0.5), sample.Tilt);
        Assert.Equal(new RequestedPose(90), sample.RequestedPose);
    }

    [Fact]
    public void BurstStatisticsDoNotRetainValuesFromLongerPreviousAttempts()
    {
        var burst = Burst();
        burst.Start(new(90), 0);
        for (int i = 0; i < 12; i++) burst.Add(0, new(1000, 2000), default);
        Assert.NotNull(burst.Add(10, new(1000, 2000), default));
        burst.Start(new(90), 20);
        burst.Add(20, new(10, 20), new(0, 1));
        var result = burst.Add(30, new(12, 24), new(2, 0));
        Assert.Equal(new Point2(11, 22), result!.Position);
        Assert.Equal(new Point2(1, 0.5), result.Tilt);
    }

    [Fact]
    public void RestartingPartialRecaptureClearsOnlyItsStoredSelection()
    {
        var original = new CaptureDraft(Settings with { AzimuthCount = 4, NonUprightAltitudeCount = 1 }, Geometry);
        Fill(original);
        var recapture = original.Recapture(7, true);
        recapture.Previous();
        Assert.Equal(5, recapture.CurrentIndex);
        recapture.Store(new(recapture.CurrentTarget, default));
        Assert.Equal(original.Total - 4, recapture.CapturedCount);
        recapture.Previous();
        recapture.Previous();
        Assert.Equal(5, recapture.CurrentIndex);
        recapture.Restart();
        Assert.Equal(original.Total - 5, recapture.CapturedCount);
        recapture.Restart();
        Assert.Equal(original.Total - 5, recapture.CapturedCount);
        Assert.Equal(original.Total, original.CapturedCount);
        Assert.Same(original.Sample(0), recapture.Sample(0));
    }

    [Fact]
    public void AcceptedCopyIsASnapshotOfTheAcceptedMeasurements()
    {
        var workspace = new CalibrationWorkspace();
        workspace.Begin(Settings, Geometry);
        Fill(workspace.Session!, new(1, 2));
        workspace.Finish();
        workspace.Accept();
        string accepted = workspace.CopyPayload();
        workspace.Working!.ToData().Grid!.PreTransformRowCoordinates[0] = double.NaN;
        Assert.Equal(accepted, workspace.CopyPayload());
        workspace.Begin(Settings, Geometry);
        workspace.Discard();
        Assert.Equal(accepted, workspace.CopyPayload());
    }

    [Fact]
    public void EveryReportMustPassTheGateEvenWhenMedianWouldPass()
    {
        var burst = Burst(Settings with { MinSamplesPerCapture = 3 }); burst.Start(new(90), 0);
        burst.Add(0, default, default); burst.Add(5, default, new(4, 0));
        Assert.False(burst.Active); Assert.Equal(0, burst.Count);
        Assert.Null(burst.Add(10, default, default)); Assert.Contains("Pose", burst.Message);
    }

    [Fact]
    public void UprightOmitsAzimuthAndTiltPosesUseCircularDistance()
    {
        var burst = new CaptureBurst(Settings, Geometry, _ => new(50, 359)); burst.Start(new(50, 1), 0);
        burst.Add(0, default, default); Assert.NotNull(burst.Add(10, default, default));
        var upright = new CaptureBurst(Settings, Geometry, _ => new(89, 170)); upright.Start(new(90), 0);
        upright.Add(0, default, default); Assert.NotNull(upright.Add(10, default, default));
    }

    [Fact]
    public void GapAndTotalTimeoutRejectOnlyTheAttemptAndRetryStartsEmpty()
    {
        var burst = Burst(); burst.Start(new(90), 0); burst.CheckTimeout(41);
        Assert.False(burst.Active); Assert.Contains("gap", burst.Message);
        burst.Start(new(90), 100); burst.Add(100, default, default);
        Assert.Null(burst.Add(141, default, default)); Assert.False(burst.Active);
        burst.Start(new(90), 200); burst.Add(200, default, default);
        Assert.NotNull(burst.Add(210, default, default));
        var countLimited = Burst(Settings with { MinSamplesPerCapture = 100 }); countLimited.Start(new(90), 0);
        foreach (int t in new[] { 0, 30, 60, 90, 101 }) Assert.Null(countLimited.Add(t, default, default));
        Assert.Contains("timed out", countLimited.Message); Assert.False(countLimited.Active);
    }

    [Theory]
    [InlineData(0, 2, true)] // X displacement: 2 raw = .02 mm.
    [InlineData(1, 2, false)] // Y displacement: 2 raw = .04 mm.
    [InlineData(1, 1, true)]
    public void PositionStabilityConvertsEachAxisToMillimeters(int axis, int displacement, bool accepted)
    {
        var burst = Burst(Settings with { PositionStabilityMm = 0.02 }); burst.Start(new(90), 0);
        burst.Add(0, axis == 0 ? new(-displacement, 0) : new(0, -displacement), default);
        var result = burst.Add(10, axis == 0 ? new(displacement, 0) : new(0, displacement), default);
        Assert.Equal(accepted, result != null);
    }

    [Theory]
    [InlineData(20, 1, true)]
    [InlineData(20, 2, false)]
    [InlineData(21, 2, false)]
    public void NearestRank95PercentileDoesNotTrimSamples(int count, int outliers, bool accepted)
    {
        var burst = Burst(Settings with { MinSamplesPerCapture = count }); burst.Start(new(90), 0);
        CalibrationSample? result = null;
        for (int i = 0; i < count; i++) result = burst.Add(i, i < outliers ? new(1000, 0) : default, default);
        Assert.Equal(accepted, result != null);
        if (accepted) Assert.Equal(default, result!.Position);
    }

    [Fact]
    public void TiltStabilityUsesRawEuclideanDegreesAndNonfiniteReadingsFail()
    {
        var burst = Burst(Settings with { AngleToleranceDeg = 10 }); burst.Start(new(90), 0);
        // The conversion suppresses azimuth near upright; both reports are individually in tolerance.
        burst.Add(0, default, new(-2, -2)); Assert.Null(burst.Add(10, default, new(2, 2)));
        Assert.Contains("Movement", burst.Message);
        burst.Start(new(90), 20); Assert.Null(burst.Add(20, new(double.NaN, 0), default));
        Assert.False(burst.Active);
    }

    [Theory]
    [InlineData(45, 0, 45, 0)]
    [InlineData(-45, 0, 45, 180)]
    [InlineData(0, 45, 45, 90)]
    [InlineData(0, -45, 45, 270)]
    [InlineData(35.264389682754654, 35.264389682754654, 45, 45)]
    public void HuionProjectionMatchesVerifiedDirectionsAndIndependentDiagonalMath(double x, double y, double altitude, double azimuth)
    {
        var angle = Orientation(new(x, y));
        Assert.Equal(altitude, angle.AltitudeDeg, 8); Assert.Equal(azimuth, angle.AzimuthDeg!.Value, 8);
        Assert.Throws<CalibrationException>(() => Orientation(new(90, 0)));
    }

    private static void Fill(CaptureDraft draft, Point2 offset = default)
    {
        while (!draft.Complete)
        {
            var pose = draft.CurrentPose;
            double magnitude = Math.Tan((90 - pose.AltitudeDeg) * Math.PI / 180), azimuth = (pose.AzimuthDeg ?? 0) * Math.PI / 180;
            var tilt = new Point2(Math.Atan(magnitude * Math.Cos(azimuth)) * 180 / Math.PI,
                Math.Atan(magnitude * Math.Sin(azimuth)) * 180 / Math.PI);
            draft.Store(new(draft.CurrentTarget + offset, tilt));
        }
    }

    [Theory]
    [InlineData(0.06, 0.08, false)]
    [InlineData(0.101, 0, true)]
    public void CaptureSeparationUsesMeasuredTiltAndRejectsTheMinimumBoundary(double x, double y, bool accepted)
    {
        var draft = new CaptureDraft(Settings with { AzimuthCount = 4, NonUprightAltitudeCount = 1 }, Geometry);
        draft.Store(new(draft.CurrentTarget, default));
        var sample = new CalibrationSample(draft.CurrentTarget, new(x, y));
        if (accepted) draft.Store(sample);
        else Assert.Throws<CalibrationException>(() => draft.Store(sample));
        Assert.Equal(accepted ? 2 : 1, draft.CapturedCount);
        Assert.Equal(accepted ? 2 : 1, draft.CurrentIndex);
    }

    [Fact]
    public void AcceptedDataSurvivesRestartDiscardAndRecaptureUntilAnotherAcceptance()
    {
        var workspace = new CalibrationWorkspace(); Assert.False(workspace.HasSessionData);
        Assert.Throws<InvalidOperationException>(() => workspace.CopyPayload());
        workspace.Begin(Settings, Geometry); Assert.True(workspace.HasSessionData);
        Assert.Throws<CalibrationException>(() => workspace.Finish());
        Fill(workspace.Session!, new(1, 2)); workspace.Finish();
        Assert.Null(workspace.Working); Assert.Throws<InvalidOperationException>(() => workspace.CopyPayload());
        workspace.Accept(); string accepted = workspace.CopyPayload();
        workspace.Recapture(workspace.Working!, 1, false);
        Assert.Equal(accepted, workspace.CopyPayload());
        workspace.Session!.Restart(); workspace.Discard(); Assert.Equal(accepted, workspace.CopyPayload());
        workspace.Begin(Settings, Geometry); Fill(workspace.Session!, new(3, 4)); workspace.Finish();
        Assert.Equal(accepted, workspace.CopyPayload()); workspace.Discard(); Assert.Equal(accepted, workspace.CopyPayload());
        workspace.Recapture(workspace.Working!, 1, false); Fill(workspace.Session!, new(5, 6)); workspace.Finish(); workspace.Accept();
        var data = CalibrationData.Parse(workspace.CopyPayload());
        Assert.Equal(new Point2(5, 6), data.Nodes![0][1].Samples[0].Position - workspace.Working!.Target(1));
        Assert.Equal(new Point2(1, 2), data.Nodes[0][0].Samples[0].Position - workspace.Working.Target(0));
        Assert.True(workspace.HasSessionData); // Copy does not claim an OTD save or remove the close reminder.
    }

    [Fact]
    public void NodeRecapturePreservesOtherNodesAndRebuildsAUsableMeasuredMesh()
    {
        var settings = Settings with { AzimuthCount = 4, NonUprightAltitudeCount = 1 };
        var original = new CaptureDraft(settings, Geometry); Fill(original);
        var draft = original.Recapture(7, true);
        Assert.Equal(5, draft.CurrentIndex); Assert.Equal(original.Total - 5, draft.CapturedCount);
        Fill(draft, new(10, 20));
        var data = draft.ToData();
        Assert.Same(original.Sample(0), draft.Sample(0));
        Assert.NotEqual(original.Sample(5), draft.Sample(5));
        var evaluator = PreparedCalibration.Prepare(data, Geometry);
        var target = draft.Target(5); Assert.Equal(target - new Point2(10, 20), evaluator.Correct(target, default));
    }

    [Fact]
    public void PreviousCrossesNodesAndRejectedReplacementPreservesSamples()
    {
        var draft = new CaptureDraft(Settings, Geometry);
        draft.Store(new(draft.CurrentTarget, default)); draft.Store(new(draft.CurrentTarget, default));
        draft.Previous(); Assert.Equal(1, draft.CurrentIndex); draft.Previous(); Assert.Equal(0, draft.CurrentIndex);
        draft.Store(new(draft.CurrentTarget + new Point2(3, 0), default)); Assert.Equal(2, draft.CapturedCount);
        var tiltDraft = new CaptureDraft(Settings with { AzimuthCount = 4, NonUprightAltitudeCount = 1 }, Geometry);
        tiltDraft.Store(new(tiltDraft.CurrentTarget, default));
        Assert.Throws<CalibrationException>(() => tiltDraft.Store(new(tiltDraft.CurrentTarget, new(0.05, 0))));
        Assert.Equal(1, tiltDraft.CapturedCount); Assert.Equal(1, tiltDraft.CurrentIndex);
        Assert.Throws<CalibrationException>(() => tiltDraft.ToData());
    }

    private static ReportReading Reading(double time, ReadingKind kind = ReadingKind.Paired, Vector2? tilt = null, uint pressure = 0) =>
        new((long)(time * Stopwatch.Frequency / 1000), new(100, 100), tilt ?? Vector2.Zero, pressure, "Rejected pen", kind);

    [Fact]
    public void ControllerIgnoresAuxiliaryReportsAndNeverCapturesFromPressureOrStaleTilt()
    {
        var draft = new CaptureDraft(Settings, Geometry); var capture = new CaptureController(draft, Orientation);
        capture.Read(Reading(0, pressure: 1000)); Assert.Equal(0, draft.CapturedCount);
        capture.Start(1); capture.Read(Reading(0, ReadingKind.Rejected)); // Queued before Space.
        capture.Read(Reading(1)); capture.Read(Reading(6, ReadingKind.Auxiliary)); capture.Read(Reading(11));
        Assert.Equal(1, draft.CapturedCount); // Zero pressure is eligible.
        capture.Start(20); capture.Read(Reading(20));
        capture.Read(new((long)(25 * Stopwatch.Frequency / 1000), new(100, 100), null, 1000, "Tilt missing", ReadingKind.Rejected));
        capture.Read(Reading(30)); Assert.Equal(1, draft.CapturedCount);
        Assert.False(capture.Snapshot(30).Active);
        capture.Start(40); capture.Read(Reading(40)); capture.Read(Reading(70, ReadingKind.Auxiliary));
        Assert.Contains("gap", capture.Snapshot(81).Message); Assert.Equal(1, draft.CapturedCount);
        capture.Previous(); Assert.Equal(0, draft.CurrentIndex);
        capture.Restart(); Assert.Equal(0, draft.CapturedCount);
    }

    [Fact]
    public async Task AcquisitionDeliversEveryMatchedReportToCaptureWithoutUiSampling()
    {
        var daemon = new FakeDaemon(); var draft = new CaptureDraft(Settings, Geometry);
        var capture = new CaptureController(draft, Orientation); var acquisition = new AcquisitionSession(daemon, daemon.Tablet);
        var received = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int count = 0;
        acquisition.Reading += r => { capture.Read(r); if (Interlocked.Increment(ref count) == 2) received.TrySetResult(); };
        var run = acquisition.RunAsync();
        try
        {
            await daemon.Enabled.Task.WaitAsync(TimeSpan.FromSeconds(3)); capture.Start(CaptureController.Now);
            var raw = new DebugReportData(daemon.Tablet, new TiltTabletReport { Raw = [], Position = new(100, 100), Tilt = default, Pressure = 0 });
            daemon.Send(raw); await Task.Delay(20); daemon.Send(raw);
            await received.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Equal(1, draft.CapturedCount);
        }
        finally { acquisition.Cancel(); await run.WaitAsync(TimeSpan.FromSeconds(3)); }
        Assert.Equal(new[] { true, false }, daemon.DebugCalls);
    }

    [Fact]
    public void RealOtdInterfacesRejectErasersProximityLossAndUnpairedInput()
    {
        var pen = new Pen { NearProximity = true };
        Assert.Equal(ReadingKind.Paired, ReportReadingFactory.Read(pen, 0).Kind);
        pen.Eraser = true; Assert.Equal(ReadingKind.Rejected, ReportReadingFactory.Read(pen, 1).Kind);
        pen.Eraser = false; pen.NearProximity = false;
        Assert.Equal(ReadingKind.Rejected, ReportReadingFactory.Read(pen, 2).Kind);
        pen.NearProximity = true; pen.Tilt = new(float.NaN, 0);
        Assert.Equal(ReadingKind.Rejected, ReportReadingFactory.Read(pen, 3).Kind);
        Assert.Equal(ReadingKind.Rejected, ReportReadingFactory.Read(new OutOfRangeReport([]), 4).Kind);
        Assert.Equal(ReadingKind.Rejected, ReportReadingFactory.Read(new TabletReport { Raw = [] }, 5).Kind);
        Assert.Equal(ReadingKind.Auxiliary, ReportReadingFactory.Read(new AuxReport { Raw = [] }, 6).Kind);
    }

    [Fact]
    public void UnsupportedTabletOrReportNeverAssumesHuionOrientation()
    {
        var tablet = IntegrationTests.Tablet(); Assert.False(CaptureDevice.Supported(tablet));
        tablet.Properties.Name = "Huion Kamvas 13 (Gen 3)"; Assert.False(CaptureDevice.Supported(tablet));
        tablet.Identifiers.First().ReportParser = typeof(OpenTabletDriver.Configurations.Parsers.Huion.GianoReportParser).FullName!;
        Assert.True(CaptureDevice.Supported(tablet));
        var draft = new CaptureDraft(Settings, Geometry);
        var controller = new CaptureController(draft, Orientation, CaptureDevice.ReportType);
        controller.Start(0); controller.Read(Reading(0)); controller.Read(Reading(10));
        Assert.Equal(0, draft.CapturedCount); Assert.Contains("not verified", controller.Snapshot(10).Message);
    }

    [Fact]
    public void FullTiltCaptureProducesAcceptedJsonUsableByTheRuntimeEvaluator()
    {
        var workspace = new CalibrationWorkspace();
        workspace.Begin(Settings with { AzimuthCount = 4, NonUprightAltitudeCount = 2 }, Geometry);
        var draft = workspace.Session!; var controller = new CaptureController(draft, Orientation);
        double time = 0;
        while (!draft.Complete)
        {
            var pose = draft.CurrentPose;
            double radius = Math.Tan((90 - pose.AltitudeDeg) * Math.PI / 180), azimuth = (pose.AzimuthDeg ?? 0) * Math.PI / 180;
            var tilt = new Vector2((float)(Math.Atan(radius * Math.Cos(azimuth)) * 180 / Math.PI),
                (float)(Math.Atan(radius * Math.Sin(azimuth)) * 180 / Math.PI));
            var target = draft.CurrentTarget;
            var position = new Vector2((float)target.X + 2, (float)target.Y - 3);
            Assert.True(controller.Start(time));
            controller.Read(Reading(time, tilt: tilt) with { Position = position });
            controller.Read(Reading(time + 10, tilt: tilt) with { Position = position });
            time += 20;
            Assert.Equal((int)(time / 20), draft.CapturedCount);
        }
        workspace.Finish(); workspace.Accept();
        var prepared = PreparedCalibration.Load(workspace.CopyPayload(), Geometry, out var diagnostic);
        Assert.Null(diagnostic); Assert.False(prepared.IsIdentity);
        var raw = new Point2(11000, 6000);
        Assert.Equal(raw - new Point2(2, -3), prepared.Correct(raw, new(10, 5)));
    }

    [Fact]
    public void PoseReplacementDoesNotAdvanceOnAnInvalidHull()
    {
        var draft = new CaptureDraft(Settings with { AzimuthCount = 4, NonUprightAltitudeCount = 1 }, Geometry); Fill(draft);
        var replacement = draft.Recapture(4, false);
        replacement.Previous(); Assert.Equal(4, replacement.CurrentIndex);
        Assert.Throws<CalibrationException>(() => replacement.Store(new(replacement.CurrentTarget, new(20, 20))));
        Assert.False(replacement.Complete); Assert.Null(replacement.Sample(4)); Assert.NotNull(draft.Sample(4));
        Fill(replacement); Assert.NotNull(replacement.ToData());
    }

    [Fact]
    public void OldCandidateCannotBeAcceptedWhileRecaptureIsUnfinished()
    {
        var workspace = new CalibrationWorkspace(); workspace.Begin(Settings, Geometry); Fill(workspace.Session!); workspace.Finish();
        workspace.Recapture(workspace.Candidate!, 0, false);
        Assert.Throws<InvalidOperationException>(() => workspace.Accept()); Assert.Null(workspace.Working);
        workspace.Discard(); Assert.False(workspace.HasSessionData);
    }

    [Fact]
    public void ExtremeCountsAreRejectedBeforeAllocatingLayoutOrReviewRows()
    {
        Assert.Throws<CalibrationException>(() => new CaptureDraft(Settings with { RowCount = int.MaxValue }, Geometry));
        Assert.Throws<CalibrationException>(() => (Settings with { RowCount = 100, ColumnCount = 101 }).Validate());
        Assert.Throws<CalibrationException>(() => (Settings with { AngleToleranceDeg = 90 }).Validate());
    }

    [Fact]
    public void TimeoutBoundaryAndStabilityBoundaryAreInclusive()
    {
        var burst = Burst(Settings with { CaptureWindowMs = 40 }); burst.Start(new(90), 0);
        burst.Add(0, default, default);
        Assert.NotNull(burst.Add(40, default, default));
        var stable = Burst(); stable.Start(new(90), 0); stable.Add(0, default, new(-2, 0));
        Assert.NotNull(stable.Add(10, default, new(2, 0))); // Exactly 2 degrees of tilt stability.
    }

    private sealed class Pen : IAbsolutePositionReport, ITiltReport, IProximityReport, IEraserReport
    {
        public byte[] Raw { get; set; } = [];
        public Vector2 Position { get; set; }
        public Vector2 Tilt { get; set; }
        public bool NearProximity { get; set; }
        public uint HoverDistance { get; set; }
        public bool Eraser { get; set; }
    }
}
