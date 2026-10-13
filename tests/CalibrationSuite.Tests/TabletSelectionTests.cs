using CalibrationSuite.Otd;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenTabletDriver.Plugin.Tablet;
using Xunit;

namespace CalibrationSuite.Tests;

public class TabletSelectionTests
{
    private static TabletReference RichTablet()
    {
        var identifier = new DeviceIdentifier
        {
            VendorID = 1234,
            ProductID = 5678,
            ReportParser = "Synthetic.Parser",
            InputReportLength = 12,
            OutputReportLength = 16,
            FeatureReportLength = 8,
            FeatureInitReport = [[1, 2, 3]],
            OutputInitReport = [[4, 5]],
            DeviceStrings = new() { [1] = "Pen", [2] = "Tablet" },
            InitializationStrings = [3, 4],
            Attributes = new() { ["mode"] = "pen" }
        };
        var configuration = new TabletConfiguration
        {
            Name = "Synthetic tablet",
            Attributes = new() { ["first"] = "one", ["second"] = "two" },
            DigitizerIdentifiers = [identifier],
            AuxiliaryDeviceIdentifiers = [identifier],
            Specifications = new()
            {
                Digitizer = new()
                {
                    WidthAsDecimal = 123.456789m,
                    HeightAsDecimal = 98.7654321m,
                    MaxX = 20000,
                    MaxY = 10000
                },
                Pen = new()
                {
                    MaxPressure = 8192,
                    ButtonCount = 2
                },
                AuxiliaryButtons = new()
                {
                    ButtonCount = 8
                },
                MouseButtons = new()
                {
                    ButtonCount = 3
                },
                Touch = new()
                {
                    Width = 100,
                    Height = 80,
                    MaxX = 1000,
                    MaxY = 800
                },
                Wheels = [new() { RelativeWheelSteps = 24, ButtonCount = 1 },
                    new() { AbsoluteWheelMax = 360, AngleOfZeroReading = 45, ButtonCount = 2 }],
                Strips = [new() { StepCount = 256, IsRelative = true }]
            }
        };
        return new(configuration, [identifier]);
    }

    private static TabletReference Clone(TabletReference tablet) =>
        JsonConvert.DeserializeObject<TabletReference>(JsonConvert.SerializeObject(tablet))!;

    [Fact]
    public void ComparisonDistinguishesEveryPopulatedSerializedField()
    {
        var original = RichTablet();
        var json = JObject.Parse(JsonConvert.SerializeObject(original));
        var matches = TabletSelection.CreateMatcher(original);
        Assert.True(matches(Clone(original)));
        foreach (var leaf in json.Descendants().OfType<JValue>().Where(v => v.Type != JTokenType.Null))
        {
            var changed = (JObject)json.DeepClone();
            var value = changed.SelectToken(leaf.Path)!;
            value.Replace(leaf.Type switch
            {
                JTokenType.Integer => new JValue(leaf.Value<long>() + 1),
                JTokenType.Float => new JValue(leaf.Value<decimal>() + 1),
                JTokenType.Boolean => new JValue(!leaf.Value<bool>()),
                JTokenType.String when leaf.Path.Contains("InitReport", StringComparison.Ordinal) =>
                    new JValue(Convert.ToBase64String([.. Convert.FromBase64String(leaf.Value<string>()!), 9])),
                JTokenType.String => new JValue(leaf.Value<string>() + " changed"),
                _ => throw new InvalidOperationException($"Unexpected fixture value: {leaf.Type}")
            });
            var incoming = changed.ToObject<TabletReference>()!;
            Assert.False(matches(incoming), $"Changed {leaf.Path} was not distinguished.");
        }
    }

    [Fact]
    public void DictionaryOrderIsIrrelevantButSequenceOrderAndNullCollectionsMatter()
    {
        var original = RichTablet();
        var incoming = Clone(original);
        var matches = TabletSelection.CreateMatcher(original);
        incoming.Properties.Attributes = new()
        {
            ["second"] = "two",
            ["first"] = "one"
        };
        Assert.True(matches(incoming));
        incoming.Identifiers = incoming.Identifiers.Where(_ => true); // Non-list RPC consumers still compare by contents.
        Assert.True(matches(incoming));
        incoming.Identifiers.First().InitializationStrings!.Reverse();
        Assert.False(matches(incoming));
        incoming = Clone(original);
        incoming.Properties.AuxiliaryDeviceIdentifiers = null;
        Assert.False(matches(incoming));
        incoming.Properties.AuxiliaryDeviceIdentifiers = [];
        Assert.False(matches(incoming));
        Assert.False(matches(null));
    }

    [Fact]
    public void AllBundledTabletConfigurationsSurviveReferenceRoundTrip()
    {
        var assembly = typeof(OpenTabletDriver.Configurations.Parsers.Huion.GianoReport).Assembly;
        var resources = assembly.GetManifestResourceNames().Where(n => n.EndsWith(".json", StringComparison.Ordinal)).ToArray();
        Assert.NotEmpty(resources);
        foreach (var resource in resources)
        {
            using var stream = assembly.GetManifestResourceStream(resource)!;
            using var reader = new StreamReader(stream);
            var configuration = JsonConvert.DeserializeObject<TabletConfiguration>(reader.ReadToEnd())!;
            var tablet = new TabletReference(configuration, configuration.DigitizerIdentifiers);
            Assert.True(TabletSelection.CreateMatcher(tablet)(Clone(tablet)), resource);
        }
    }

    [Fact]
    public void MatchingUsesTheTabletContentsAtAcquisitionStart()
    {
        var tablet = RichTablet();
        var incoming = Clone(tablet);
        var matches = TabletSelection.CreateMatcher(tablet);
        tablet.Properties.Specifications.Pen.MaxPressure++;
        Assert.True(matches(incoming));
        Assert.False(matches(tablet));
    }
}
