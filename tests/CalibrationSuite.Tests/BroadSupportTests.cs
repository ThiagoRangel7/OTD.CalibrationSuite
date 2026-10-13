using System.Diagnostics;
using System.Numerics;
using CalibrationSuite.Core;
using CalibrationSuite.Filter;
using CalibrationSuite.Otd;
using OpenTabletDriver.Plugin.Tablet;
using Xunit;

namespace CalibrationSuite.Tests;

public class BroadSupportTests
{
    private static readonly CaptureSettings Settings = new()
    {
        RowCount = 2, ColumnCount = 2, TiltDirections = 4, TiltLevels = 1,
        CaptureWindowMs = 10, MinSamplesPerCapture = 2, ReportGapTimeoutMs = 40, CaptureTimeoutMs = 100
    };
    private static ReportReading Reading(double time, Point2 position, Point2? tilt) =>
        new((long)Math.Round(time * Stopwatch.Frequency / 1000), new((float)position.X, (float)position.Y),
            tilt is { } t ? new Vector2((float)t.X, (float)t.Y) : null, 0, "Pen");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CaptureCopyAndFilterWorkWithOrWithoutTilt(bool reportsTilt)
    {
        var tablet = IntegrationTests.Tablet();
        tablet.Properties.Name = "Any display tablet";
        var geometry = CaptureDevice.Geometry(tablet);
        var workspace = new CalibrationWorkspace(); workspace.Begin(Settings, geometry);
        var draft = workspace.Session!; var capture = new CaptureController(draft);
        Point2 reference = new(120, -75), error = new(2, -3);
        capture.Read(Reading(0, draft.CurrentTarget, reportsTilt ? reference : null));
        Assert.Equal(reportsTilt ? 20 : 4, draft.Total);
        double time = 10;
        while (!draft.Complete)
        {
            var position = draft.CurrentTarget + error;
            Point2? tilt = reportsTilt ? draft.RequestedTilt ?? reference : null;
            capture.Start(time); capture.Read(Reading(time, position, tilt)); capture.Read(Reading(time + 10, position, tilt));
            time += 20;
        }
        workspace.Finish(); workspace.Accept();
        string json = workspace.CopyPayload();
        var prepared = PreparedCalibration.Load(json, geometry, out var diagnostic);
        Assert.Null(diagnostic); Assert.Equal(reportsTilt, prepared.RequiresTilt);
        var filter = new CalibrationFilter { Tablet = tablet, CalibrationJson = json }; filter.Initialize();
        IDeviceReport report = reportsTilt
            ? new TiltTabletReport { Raw = [1], Position = new(10000, 5000), Tilt = new(125, -70), Pressure = 321, PenButtons = [true] }
            : new TabletReport { Raw = [1], Position = new(10000, 5000), Pressure = 321, PenButtons = [true] };
        IDeviceReport? emitted = null; filter.Emit += r => emitted = r;
        filter.Consume(report);
        Assert.Same(report, emitted);
        Assert.Equal(new Vector2(9998, 5003), ((IAbsolutePositionReport)report).Position);
        Assert.Equal(321u, ((ITabletReport)report).Pressure);
    }

    [Fact]
    public void NoTiltReportBlocksTiltPosesForTheSession()
    {
        var draft = new CaptureDraft(Settings, CaptureDevice.Geometry(IntegrationTests.Tablet()));
        var capture = new CaptureController(draft);
        Assert.Null(capture.Snapshot(0).ReportsTilt);
        capture.Read(Reading(0, draft.CurrentTarget, null));
        var state = capture.Snapshot(0);
        Assert.False(state.ReportsTilt);
        Assert.False(state.UsesTilt);
        Assert.Equal(4, state.Total);
        Assert.Null(state.RequestedTilt);

        // Later reports cannot reintroduce tilt poses into this position-only dataset.
        capture.Start(10);
        capture.Read(Reading(10, draft.CurrentTarget, new(5, -10)));
        capture.Read(Reading(20, draft.CurrentTarget, new(5, -10)));
        Assert.Equal(1, draft.CapturedCount);
        Assert.Equal(4, draft.Total);
        Assert.False(capture.Snapshot(20).UsesTilt);
        Assert.Equal(draft.Target(1), draft.CurrentTarget);
    }

    [Fact]
    public void MissingTiltAfterCapabilityDetectionNeverDowngradesOrReusesTilt()
    {
        var geometry = CaptureDevice.Geometry(IntegrationTests.Tablet());
        var draft = new CaptureDraft(Settings, geometry); var capture = new CaptureController(draft);
        capture.Start(0);
        capture.Read(Reading(0, draft.CurrentTarget, new(5, -10)));
        capture.Read(Reading(10, draft.CurrentTarget, new(5, -10)));
        Assert.Equal(new Point2(5, -10), draft.ReferenceTilt);
        Assert.Equal(new Point2(45, -10), draft.RequestedTilt);
        capture.Start(20);
        capture.Read(Reading(20, draft.CurrentTarget, draft.RequestedTilt));
        capture.Read(Reading(25, draft.CurrentTarget, null));
        capture.Read(Reading(30, draft.CurrentTarget, draft.RequestedTilt));
        Assert.True(draft.UsesTilt); Assert.Equal(1, draft.CapturedCount);
        Assert.Equal(CaptureBurstStatus.Retry, capture.Snapshot(30).BurstStatus);
    }

    [Fact]
    public void PositionOnlyCorrectionIgnoresUnusableTiltAndTiltDataNeedsTilt()
    {
        var data = CoreFixtures.Data();
        foreach (var row in data.Nodes!)
            foreach (var node in row)
                node.Samples[0] = node.Samples[0] with { RequestedPose = new(90) };
        var tiltFilter = new CalibrationFilter { Tablet = Tablet(), CalibrationJson = data.ToJson() }; tiltFilter.Initialize();
        IDeviceReport missing = new TabletReport { Position = new(500, 500) };
        tiltFilter.Consume(missing);
        Assert.Equal(new Vector2(500, 500), ((IAbsolutePositionReport)missing).Position);
        for (int r = 0; r < 2; r++)
            for (int c = 0; c < 2; c++)
                data.Nodes[r][c] = new([data.Nodes[r][c].Samples[0]]);
        var positionFilter = new CalibrationFilter { Tablet = Tablet(), CalibrationJson = data.ToJson() }; positionFilter.Initialize();
        IDeviceReport invalidTilt = new TiltTabletReport { Position = new(500, 500), Tilt = new(float.NaN, 0) };
        positionFilter.Consume(invalidTilt);
        Assert.Equal(new Vector2(498, 497), ((IAbsolutePositionReport)invalidTilt).Position);
    }

    private static TabletReference Tablet()
    {
        var tablet = IntegrationTests.Tablet();
        var digitizer = tablet.Properties.Specifications.Digitizer;
        digitizer.MaxX = digitizer.MaxY = 1000; digitizer.Width = digitizer.Height = 100;
        return tablet;
    }

    [Fact]
    public void BuiltInHuionXpPenAndPositionOnlyReportsUseTheSameContracts()
    {
        byte[] bytes = new byte[12]; bytes[1] = 0x80;
        IDeviceReport[] reports = [
            new OpenTabletDriver.Configurations.Parsers.Huion.GianoReportParser().Parse(bytes),
            new OpenTabletDriver.Configurations.Parsers.XP_Pen.XP_PenPressureOffsetTiltTabletReport(bytes),
            new TabletReport { Raw = bytes }
        ];
        var tablet = Tablet();
        var data = CoreFixtures.Data();
        for (int r = 0; r < 2; r++)
            for (int c = 0; c < 2; c++) data.Nodes![r][c] = new([data.Nodes[r][c].Samples[0]]);
        var filter = new CalibrationFilter { Tablet = tablet, CalibrationJson = data.ToJson() }; filter.Initialize();
        foreach (var report in reports)
        {
            ((IAbsolutePositionReport)report).Position = new(500, 500);
            var debug = new OpenTabletDriver.Desktop.RPC.DebugReportData(tablet, report);
            var reading = ReportReadingFactory.Read(debug, 0);
            Assert.Equal(ReadingKind.Paired, reading.Kind);
            Assert.Equal(new Vector2(500, 500), reading.Position);
            Assert.Equal(report is ITiltReport, reading.Tilt.HasValue);
            filter.Consume(report);
            Assert.Equal(new Vector2(498, 497), ((IAbsolutePositionReport)report).Position);
            Assert.Same(bytes, report.Raw);
        }
    }

    [Fact]
    public void HistoricalOptionalGuidanceMetadataDoesNotInvalidateMeasuredData()
    {
        var document = Newtonsoft.Json.Linq.JObject.Parse(CoreFixtures.Data().ToJson());
        var sample = document["nodes"]![0]![0]!["samples"]![0]!;
        sample["requestedPose"] = new Newtonsoft.Json.Linq.JObject { ["altitudeDeg"] = 90, ["azimuthDeg"] = 0 };
        var prepared = PreparedCalibration.Load(document.ToString(), CoreFixtures.Geometry, out var diagnostic);
        Assert.Null(diagnostic);
        CoreFixtures.Near(new(498, 497), prepared.Correct(new(500, 500), default));
    }
}
