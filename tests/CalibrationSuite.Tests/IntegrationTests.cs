using System.Numerics;
using CalibrationSuite.Core;
using CalibrationSuite.Filter;
using CalibrationSuite.Otd;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenTabletDriver.Desktop.RPC;
using OpenTabletDriver.Plugin.Output;
using OpenTabletDriver.Plugin.Tablet;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace CalibrationSuite.Tests;

// All tablets and reports in this project are synthetic; none establishes device accuracy.
public class IntegrationTests
{
    [Fact]
    public void SourceBuiltOtdRetainsEmbeddedTabletConfigurations()
    {
        var assembly = typeof(OpenTabletDriver.Configurations.Parsers.Huion.GianoReport).Assembly;
        const string resource = "OpenTabletDriver.Configurations.Configurations.Huion.Kamvas 13 (Gen 3).json";
        using var stream = assembly.GetManifestResourceStream(resource);
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream);
        var configuration = JsonConvert.DeserializeObject<TabletConfiguration>(reader.ReadToEnd());
        Assert.Equal("Huion Kamvas 13 (Gen 3)", configuration!.Name);
    }

    public static TabletReference Tablet() => new(new TabletConfiguration
    {
        Name = "Synthetic tablet",
        Specifications = new() { Digitizer = new() { Width = 200, Height = 100, MaxX = 20000, MaxY = 10000 } }
    }, [new DeviceIdentifier { VendorID = 1234, ProductID = 5678 }]);

    public static DebugReportData Report(TabletReference tablet) => new(tablet,
        new TiltTabletReport { Raw = [], Position = new(345, 678), Tilt = new(-12, 23), Pressure = 321, PenButtons = [true] });

    [Fact]
    public void ReferenceContentsSurviveRpcRoundTripAndDistinguishIdentifiers()
    {
        var a = Tablet();
        var b = JsonConvert.DeserializeObject<TabletReference>(JsonConvert.SerializeObject(a))!;
        Assert.NotSame(a, b);
        var matches = TabletSelection.CreateMatcher(a);
        Assert.True(matches(b));
        Assert.Throws<InvalidOperationException>(() => TabletSelection.Single([a, b]));
        b.Identifiers.First().ProductID++;
        Assert.False(matches(b));
        Assert.Same(a, TabletSelection.Single([a]));
        Assert.Throws<InvalidOperationException>(() => TabletSelection.Single([]));
    }

    [Fact]
    public void RealBoxedReportReconstructionRetainsPairedValuesAndPressure()
    {
        var reading = ReportReadingFactory.Read(Report(Tablet()), 42);
        Assert.Equal(new Vector2(345, 678), reading.Position);
        Assert.Equal(new Vector2(-12, 23), reading.Tilt);
        Assert.Equal(321u, reading.Pressure);
        Assert.Equal(42, reading.Timestamp);
    }

    [Fact]
    public void ReconstructionDoesNotGuessUnknownOrMalformedTypes()
    {
        var report = Report(Tablet()); report.Path = "Does.Not.Exist";
        Assert.Contains(report.Path, Assert.Throws<InvalidOperationException>(() => ReportReadingFactory.Read(report, 0)).Message);
        report.Path = typeof(TiltTabletReport).FullName; report.Data = new JValue("invalid");
        Assert.Throws<InvalidOperationException>(() => ReportReadingFactory.Read(report, 0));
    }

    [Fact]
    public void MissingTiltDoesNotReuseEarlierTilt()
    {
        _ = ReportReadingFactory.Read(Report(Tablet()), 0);
        var report = new DebugReportData(Tablet(), new TabletReport { Raw = [], Position = new(1, 2) });
        var reading = ReportReadingFactory.Read(report, 1);
        Assert.Null(reading.Tilt);
        Assert.Equal(ReadingKind.Paired, reading.Kind);
        Assert.Contains("no tilt reported", reading.Status);
    }

    [Fact]
    public void UninitializedFilterEmitsOriginalBoxExactlyOnceInOrder()
    {
        var filter = new CalibrationFilter();
        IDeviceReport a = new TiltTabletReport { Raw = [], Position = new(1, 2) };
        IDeviceReport b = new TabletReport { Raw = [], Position = new(3, 4) };
        var emitted = new List<IDeviceReport>(); filter.Emit += emitted.Add;
        filter.Consume(a); filter.Consume(b);
        Assert.Equal(PipelinePosition.PreTransform, filter.Position);
        Assert.Equal(2, emitted.Count); Assert.Same(a, emitted[0]); Assert.Same(b, emitted[1]);
        Assert.Equal(new Vector2(1, 2), ((IAbsolutePositionReport)a).Position);
    }

    [Theory]
    [InlineData(1920, 1080, 1)]
    [InlineData(2560, 1440, 1.5)]
    [InlineData(3840, 2160, 2)]
    public void FullScreenTargetsUseRawFractionsAtAnyDpi(float width, float height, float scale)
    {
        var viewport = new Vector2(width / scale, height / scale);
        var rawSize = new Vector2(20000, 10000);
        Assert.Equal(Vector2.Zero, PreviewGeometry.ToDisplay(Vector2.Zero, rawSize, viewport));
        Assert.Equal(viewport, PreviewGeometry.ToDisplay(rawSize, rawSize, viewport));
        Assert.Equal(viewport / 2, PreviewGeometry.ToDisplay(rawSize / 2, rawSize, viewport));
        var inset = PreviewGeometry.ToDisplay(new(1500, 1500), rawSize, viewport);
        Assert.Equal(viewport.X * 0.075f, inset.X, 3);
        Assert.Equal(viewport.Y * 0.15f, inset.Y, 3);
    }

}
