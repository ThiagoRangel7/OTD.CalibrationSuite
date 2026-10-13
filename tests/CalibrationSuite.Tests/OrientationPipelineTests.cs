using System.Numerics;
using CalibrationSuite.Core;
using CalibrationSuite.Otd;
using Newtonsoft.Json;
using OpenTabletDriver.Configurations.Parsers.Huion;
using OpenTabletDriver.Desktop.RPC;
using Xunit;

namespace CalibrationSuite.Tests;

public class OrientationPipelineTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(5, -10)]
    [InlineData(-60, 40)]
    [InlineData(80, -80)]
    public void ParsedTiltSurvivesRpcWithoutPhysicalAngleConversion(int x, int y)
    {
        byte[] bytes = new byte[12];
        bytes[1] = 0x80;
        bytes[10] = unchecked((byte)-x);
        bytes[11] = unchecked((byte)-y);
        var report = new GianoReportParser().Parse(bytes);
        var debug = new DebugReportData(IntegrationTests.Tablet(), report);
        var reconstructed = JsonConvert.DeserializeObject<DebugReportData>(JsonConvert.SerializeObject(debug))!;
        var reading = ReportReadingFactory.Read(reconstructed, 42);
        Assert.Equal(ReadingKind.Paired, reading.Kind);
        Assert.Equal(new Vector2(x, y), reading.Tilt);
        Assert.Equal(typeof(GianoReport).FullName, reading.ReportType);
    }

    [Fact]
    public void ReportedGateIsIndependentOfTheReferenceOffsetAndPhysicalZero()
    {
        var offset = new Point2(120, -75);
        Assert.True(OrientationGuidance.WithinTolerance(offset + new Point2(3, 4), offset, 5));
        Assert.False(OrientationGuidance.WithinTolerance(offset + new Point2(4, 4), offset, 5));
        Assert.Equal(new Point2(0.3, -0.4), OrientationScene.ArrowTip(new(3, 4), 10));
    }
}
