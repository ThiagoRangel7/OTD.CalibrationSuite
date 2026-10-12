using System.Numerics;
using CalibrationSuite.Core;
using CalibrationSuite.Otd;
using OpenTabletDriver.Configurations.Parsers.Huion;
using OpenTabletDriver.Desktop.RPC;
using Xunit;

namespace CalibrationSuite.Tests;

public class CaptureAcquisitionTests
{
    private static readonly CaptureSettings Settings = new()
    {
        RowCount = 2,
        ColumnCount = 2,
        AzimuthCount = 0,
        CaptureWindowMs = 10,
        MinSamplesPerCapture = 2
    };

    private static FakeDaemon Daemon()
    {
        var daemon = new FakeDaemon();
        daemon.Tablet.Properties.Name = "Huion Kamvas 13 (Gen 3)";
        daemon.Tablet.Identifiers.First().ReportParser = typeof(GianoReportParser).FullName!;
        return daemon;
    }

    private static CaptureDraft Draft(FakeDaemon daemon) => new(Settings, CaptureDevice.Geometry(daemon.Tablet));

    private static DebugReportData Report(FakeDaemon daemon) => new(daemon.Tablet,
        new GianoReport { Raw = [], Position = new(1500, 1500), Tilt = Vector2.Zero, PenButtons = [] });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReportDeliveryAndEveryStopPathPreserveStoredMeasurements(bool disconnect)
    {
        var daemon = Daemon();
        var draft = Draft(daemon);
        var controller = new CaptureController(draft, tilt => CaptureDevice.Orientation(tilt, Settings.AngleToleranceDeg), CaptureDevice.ReportType);
        var acquisition = new AcquisitionSession(daemon, daemon.Tablet);
        acquisition.Reading += controller.Read;
        var run = acquisition.RunAsync();
        try
        {
            await daemon.Enabled.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.True(controller.Start(CaptureController.Now));
            daemon.Send(Report(daemon));
            await Task.Delay(20);
            daemon.Send(Report(daemon));
            await WaitUntil(() => draft.CapturedCount == 1);
            Assert.Equal(new Point2(1500, 1500), draft.Sample(0)!.Position);

            Assert.True(controller.Start(CaptureController.Now));
            daemon.Send(Report(daemon));
            await WaitUntil(() => controller.Snapshot(CaptureController.Now).BurstSamples == 1);
            if (disconnect) daemon.Invalidate();
            else acquisition.Cancel("User cancelled");

            string result = await run.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Contains(disconnect ? "disconnected" : "User cancelled", result);
            controller.Stop();
            Assert.False(controller.Snapshot(CaptureController.Now).Active);
            Assert.Equal(1, draft.CapturedCount);
            Assert.False(daemon.HasSubscribers);
            Assert.Equal(new[] { true, false }, daemon.DebugCalls);
        }
        finally
        {
            acquisition.Cancel();
            await run.WaitAsync(TimeSpan.FromSeconds(3));
            controller.Stop();
        }
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        while (!condition()) await Task.Delay(5, deadline.Token);
    }
}
