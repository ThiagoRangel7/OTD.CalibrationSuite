using System.Numerics;
using CalibrationSuite.Core;
using CalibrationSuite.Filter;
using Newtonsoft.Json;
using OpenTabletDriver.Desktop;
using OpenTabletDriver.Desktop.Reflection;
using OpenTabletDriver.Plugin;
using OpenTabletDriver.Plugin.Logging;
using OpenTabletDriver.Plugin.Output;
using OpenTabletDriver.Plugin.Tablet;
using Xunit;

namespace CalibrationSuite.Tests;

public class FilterTests
{
    private static TabletReference Tablet()
    {
        var tablet = IntegrationTests.Tablet();
        var d = tablet.Properties.Specifications.Digitizer;
        d.MaxX = d.MaxY = 1000; d.Width = d.Height = 100;
        return tablet;
    }

    private static CalibrationFilter Filter(string? json = null)
    {
        var filter = new CalibrationFilter { Tablet = Tablet(), CalibrationJson = json ?? CoreFixtures.Data().ToJson() };
        filter.Initialize();
        return filter;
    }

    [Fact]
    public void PackageInstallsThroughOtdAndSettingsRoundTripActivatesSnapshot()
    {
        var previous = AppInfo.PluginManager;
        var previousInfo = AppInfo.Current;
        var directory = Path.Combine(Path.GetTempPath(), "CalibrationSuite.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root != null && !File.Exists(Path.Combine(root.FullName, "CalibrationSuite.slnx"))) root = root.Parent;
            Assert.NotNull(root);
            var configuration = typeof(FilterTests).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyConfigurationAttribute), false)
                .Cast<System.Reflection.AssemblyConfigurationAttribute>().Single().Configuration;
            var package = Path.Combine(root.FullName, "artifacts/filter-package", configuration, "CalibrationSuite.zip");
            Assert.True(File.Exists(package), "Build the filter project to generate its package before running tests.");
            AppInfo.Current = new AppInfo { AppDataDirectory = directory };
            AppInfo.PluginManager = new DesktopPluginManager();
            Assert.True(AppInfo.PluginManager.InstallPlugin(package));
            var context = Assert.Single(AppInfo.PluginManager.GetLoadedPlugins());
            Assert.Equal("CalibrationSuite", context.GetMetadata().Name);
            Assert.Equal(new[] { "CalibrationSuite.Core.dll", "CalibrationSuite.Filter.dll", "NetTopologySuite.dll" },
                context.Directory.GetFiles("*.dll").Select(f => f.Name).OrderBy(n => n));
            Assert.Equal(configuration == "Debug" ? 2 : 0, context.Directory.GetFiles("*.pdb").Length);
            var type = Assert.Single(AppInfo.PluginManager.PluginTypes, t => t.FullName == typeof(CalibrationFilter).FullName);
            var store = new PluginSettingStore(type);
            store[nameof(CalibrationFilter.CalibrationJson)].SetValue(CoreFixtures.Data().ToJson());
            store = JsonConvert.DeserializeObject<PluginSettingStore>(JsonConvert.SerializeObject(store))!;
            var tablet = Tablet();
            var filter = store.Construct<IPositionedPipelineElement<IDeviceReport>>(tablet);
            Assert.NotNull(filter);
            Assert.Same(tablet, type.GetProperty("Tablet")!.GetValue(filter));
            type.GetProperty("CalibrationJson")!.SetValue(filter, "malformed setter does not change the active snapshot");
            IDeviceReport report = new TiltTabletReport { Position = new(500, 500), Tilt = new(12, -8) };
            filter.Consume(report);
            Assert.Equal(new Vector2(498, 497), ((IAbsolutePositionReport)report).Position);
            store[nameof(CalibrationFilter.CalibrationJson)].SetValue("");
            var cleared = store.Construct<IPositionedPipelineElement<IDeviceReport>>(tablet);
            cleared.Consume(report);
            Assert.Equal(new Vector2(498, 497), ((IAbsolutePositionReport)report).Position);
        }
        finally
        {
            AppInfo.PluginManager = previous; AppInfo.Current = previousInfo;
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void BoxedStructAndClassRetainFieldsAndEmitSynchronouslyInOrder()
    {
        var filter = Filter();
        byte[] raw = [1, 2]; bool[] buttons = [true, false];
        IDeviceReport a = new TiltTabletReport { Position = new(100, 200), Tilt = new(8, 9), Pressure = 321, Raw = raw, PenButtons = buttons };
        var b = new Pen { Position = new(500, 500), Tilt = new(0, 0), NearProximity = true, HoverDistance = 3 };
        var emitted = new List<IDeviceReport>();
        filter.Emit += emitted.Add;
        filter.Consume(a); Assert.Single(emitted);
        filter.Consume(b);
        Assert.Equal(PipelinePosition.PreTransform, filter.Position);
        Assert.Equal(2, emitted.Count); Assert.Same(a, emitted[0]); Assert.Same(b, emitted[1]);
        var actual = (TiltTabletReport)a;
        Assert.Equal(new Vector2(98, 197), actual.Position);
        Assert.Equal(new Vector2(8, 9), actual.Tilt); Assert.Equal(321u, actual.Pressure);
        Assert.Same(raw, actual.Raw); Assert.Same(buttons, actual.PenButtons);
        Assert.Equal(new Vector2(498, 497), b.Position); Assert.Equal(3u, b.HoverDistance);
    }

    [Fact]
    public void IneligibleReportsPassUnchangedWithoutReusingTilt()
    {
        var filter = Filter();
        IDeviceReport[] reports = [
            new TiltTabletReport { Position = new(500, 500) },
            new TabletReport { Position = new(500, 500) },
            new OutOfRangeReport([4]),
            new Pen { Position = new(500, 500), NearProximity = false },
            new Pen { Position = new(500, 500), NearProximity = true, Eraser = true },
            new TiltTabletReport { Position = new(500, 500), Tilt = new(float.NaN, 0) },
            new TiltTabletReport { Position = new(float.PositiveInfinity, 0) },
            new Auxiliary { Raw = [6] }, new TiltOnly { Tilt = new(12, 0) }
        ];
        var before = reports.Skip(1).Select(JsonConvert.SerializeObject).ToArray();
        var emitted = new List<IDeviceReport>(); filter.Emit += emitted.Add;
        foreach (var report in reports) filter.Consume(report);
        Assert.Equal(reports.Length, emitted.Count);
        for (int i = 0; i < reports.Length; i++) Assert.Same(reports[i], emitted[i]);
        Assert.Equal(before, reports.Skip(1).Select(JsonConvert.SerializeObject));
    }

    [Theory]
    [InlineData("")]
    [InlineData("{\"mode\":\"identity\"}")]
    [InlineData("broken")]
    [InlineData("{\"mode\":\"unknown\"}")]
    public void IdentityAndInvalidDataPreservePosition(string json)
    {
        IDeviceReport report = new TiltTabletReport { Position = new(500, 500) };
        Filter(json).Consume(report);
        Assert.Equal(new Vector2(500, 500), ((IAbsolutePositionReport)report).Position);
    }

    [Fact]
    public void InitializationFailuresAndGeometryMismatchLogAndUseIdentity()
    {
        var messages = new List<LogMessage>();
        EventHandler<LogMessage> listener = (_, m) => messages.Add(m);
        Log.Output += listener; messages.Clear();
        try
        {
            var filter = new CalibrationFilter { CalibrationJson = CoreFixtures.Data().ToJson() };
            filter.Initialize();
            filter.Tablet = IntegrationTests.Tablet(); filter.Initialize();
            IDeviceReport report = new TiltTabletReport { Position = new(500, 500) };
            filter.Consume(report);
            Assert.Equal(new Vector2(500, 500), ((IAbsolutePositionReport)report).Position);
            Assert.Equal(2, messages.Count);
            Assert.All(messages, m => Assert.Contains("identity", m.Message));
        }
        finally { Log.Output -= listener; }
    }

    [Fact]
    public void SharedCorrectionRunsBeforeOtdTransformAndPreservesUnclippedOutput()
    {
        var filter = Filter(CoreFixtures.Data((p, t) => new(p.X / 10 + t.X, p.Y / 10 + t.Y)).ToJson());
        using var output = new ScalingOutput { Elements = [filter] };
        IDeviceReport report = new TiltTabletReport { Position = new(500, 500), Tilt = new(10, -10) };
        output.Read(report);
        Assert.Same(report, output.Last);
        Assert.Equal(new Vector2(880, 920), ((IAbsolutePositionReport)report).Position);

        var huge = Filter(CoreFixtures.Data((_, _) => new(-double.MaxValue / 16, 0)).ToJson());
        report = new TiltTabletReport { Position = new(500, 500) };
        huge.Consume(report);
        Assert.Equal(new Vector2(500, 500), ((IAbsolutePositionReport)report).Position);
    }

    [Fact]
    public void PersistentUnsupportedInputLogsOnceAndEligiblePathDoesNotAllocate()
    {
        var filter = Filter(); var messages = new List<LogMessage>();
        EventHandler<LogMessage> listener = (_, m) => messages.Add(m);
        Log.Output += listener; messages.Clear();
        try
        {
            IDeviceReport missing = new TabletReport();
            for (int i = 0; i < 1000; i++) filter.Consume(missing);
            Assert.Single(messages);
            IDeviceReport valid = new TiltTabletReport { Position = new(500, 500) };
            for (int i = 0; i < 1000; i++) filter.Consume(valid);
            long start = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++) filter.Consume(valid);
            Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - start);
            Assert.Single(messages);
        }
        finally { Log.Output -= listener; }
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
    private sealed class ScalingOutput : OutputMode
    {
        public IDeviceReport? Last { get; private set; }
        protected override Matrix3x2 CreateTransformationMatrix() => Matrix3x2.Identity;
        protected override IAbsolutePositionReport Transform(IAbsolutePositionReport report)
        {
            report.Position *= 2;
            return report;
        }
        protected override void OnOutput(IDeviceReport report) => Last = report;
    }
    private sealed class Auxiliary : IDeviceReport { public byte[] Raw { get; set; } = []; }
    private sealed class TiltOnly : ITiltReport
    {
        public byte[] Raw { get; set; } = [];
        public Vector2 Tilt { get; set; }
    }
}
