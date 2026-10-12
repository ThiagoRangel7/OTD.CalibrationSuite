using System.Numerics;
using CalibrationSuite.Core;
using CalibrationSuite.Otd;
using Newtonsoft.Json;
using OpenTabletDriver.Configurations.Parsers.Huion;
using OpenTabletDriver.Desktop.RPC;
using Xunit;

namespace CalibrationSuite.Tests;

// Synthetic device bytes check software centering, not the pen's physical zero.
public class OrientationPipelineTests
{
    [Fact]
    public void ZeroTiltStaysCenteredThroughGianoParsingAndRpcReconstruction()
    {
        var tilt = ReadTilt(0, 0);
        Assert.Equal(default, tilt);
        foreach (double tolerance in new[] { 0, 0.25, 3, 12 })
        {
            var pose = CaptureDevice.Orientation(tilt, tolerance);
            Assert.Equal(new RequestedPose(90), pose);
            Assert.True(OrientationGuidance.WithinTolerance(pose, new(90), tolerance));
            Assert.Equal(default, OrientationScene.ArrowTip(pose));
        }
    }

    [Fact]
    public void EverySupportedSignedAxisValueHasAnEqualOppositeArrowAndUprightGate()
    {
        for (int angle = -89; angle <= 89; angle++)
        {
            double displacement = Math.Sin(angle * Math.PI / 180);
            var x = CaptureDevice.Orientation(ReadTilt(angle, 0), 0);
            var y = CaptureDevice.Orientation(ReadTilt(0, angle), 0);
            var xTip = OrientationScene.ArrowTip(x);
            var yTip = OrientationScene.ArrowTip(y);
            Assert.Equal(90 - Math.Abs(angle), x.AltitudeDeg, 10);
            Assert.Equal(x.AltitudeDeg, y.AltitudeDeg, 10);
            Assert.Equal(displacement, xTip.X, 10);
            Assert.Equal(0, xTip.Y, 10);
            Assert.Equal(0, yTip.X, 10);
            Assert.Equal(-displacement, yTip.Y, 10);
            Assert.Equal(OrientationGuidance.WithinTolerance(x, new(90), 3),
                OrientationGuidance.WithinTolerance(y, new(90), 3));
        }
    }

    [Fact]
    public void OppositeAndExchangedRawAxesPreserveLeanAndUprightEligibility()
    {
        int[] angles = [-60, -20, -10, -5, -3, -1, 0, 1, 3, 5, 10, 20, 60];
        foreach (int x in angles)
        foreach (int y in angles)
        {
            var pose = CaptureDevice.Orientation(ReadTilt(x, y), 0);
            var opposite = CaptureDevice.Orientation(ReadTilt(-x, -y), 0);
            var exchanged = CaptureDevice.Orientation(ReadTilt(y, x), 0);
            Assert.Equal(pose.AltitudeDeg, opposite.AltitudeDeg, 10);
            Assert.Equal(pose.AltitudeDeg, exchanged.AltitudeDeg, 10);
            var tip = OrientationScene.ArrowTip(pose);
            var oppositeTip = OrientationScene.ArrowTip(opposite);
            var exchangedTip = OrientationScene.ArrowTip(exchanged);
            Assert.Equal(-tip.X, oppositeTip.X, 10);
            Assert.Equal(-tip.Y, oppositeTip.Y, 10);
            Assert.Equal(-tip.Y, exchangedTip.X, 10);
            Assert.Equal(-tip.X, exchangedTip.Y, 10);
            foreach (double tolerance in new[] { 0.25, 3, 12 })
            {
                bool upright = OrientationGuidance.WithinTolerance(pose, new(90), tolerance);
                Assert.Equal(upright, OrientationGuidance.WithinTolerance(opposite, new(90), tolerance));
                Assert.Equal(upright, OrientationGuidance.WithinTolerance(exchanged, new(90), tolerance));
            }
        }
    }

    private static Point2 ReadTilt(int x, int y)
    {
        // Giano negates signed bytes 10/11; use the inverse to encode known parsed tilt.
        byte[] bytes = new byte[12];
        bytes[1] = 0x80;
        bytes[10] = unchecked((byte)-x);
        bytes[11] = unchecked((byte)-y);
        var report = new GianoReportParser().Parse(bytes);
        var debug = new DebugReportData(IntegrationTests.Tablet(), report);
        var reconstructed = JsonConvert.DeserializeObject<DebugReportData>(JsonConvert.SerializeObject(debug))!;
        var reading = ReportReadingFactory.Read(reconstructed, 42);
        Assert.Equal(ReadingKind.Paired, reading.Kind);
        Assert.Equal(CaptureDevice.ReportType, reading.ReportType);
        Assert.Equal(new Vector2(x, y), reading.Tilt);
        return new(reading.Tilt!.Value.X, reading.Tilt.Value.Y);
    }
}
