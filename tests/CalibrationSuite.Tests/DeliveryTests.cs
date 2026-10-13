using System.IO.Compression;
using System.Numerics;
using System.Reflection;
using CalibrationSuite.Filter;
using Newtonsoft.Json;
using OpenTabletDriver.Desktop;
using OpenTabletDriver.Desktop.Profiles;
using OpenTabletDriver.Desktop.Reflection;
using OpenTabletDriver.Plugin.Output;
using OpenTabletDriver.Plugin.Tablet;
using Xunit;

namespace CalibrationSuite.Tests;

public class DeliveryTests
{
    [Fact]
    public void InstallUpdateSettingsBackupRestoreClearAndRemovalUseOtdContracts()
    {
        var previous = AppInfo.PluginManager; var previousInfo = AppInfo.Current;
        string directory = Path.Combine(Path.GetTempPath(), "CalibrationSuite.DeliveryTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root != null && !File.Exists(Path.Combine(root.FullName, "CalibrationSuite.slnx"))) root = root.Parent;
            Assert.NotNull(root);
            string configuration = typeof(DeliveryTests).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()!.Configuration;
            string package = Path.Combine(root.FullName, "artifacts/filter-package", configuration, "CalibrationSuite.zip");
            using (var archive = ZipFile.OpenRead(package)) Assert.Contains(archive.Entries, e => e.FullName == "LICENSE");
            AppInfo.Current = new AppInfo { AppDataDirectory = directory };
            AppInfo.PluginManager = new DesktopPluginManager();
            Assert.True(AppInfo.PluginManager.InstallPlugin(package));
            var first = Assert.Single(AppInfo.PluginManager.GetLoadedPlugins());
            string pluginPath = first.Directory.FullName;
            File.WriteAllText(Path.Combine(pluginPath, "obsolete.txt"), "old package file");
            Assert.True(AppInfo.PluginManager.InstallPlugin(package));
            Assert.False(File.Exists(Path.Combine(pluginPath, "obsolete.txt")));
            var context = Assert.Single(AppInfo.PluginManager.GetLoadedPlugins());
            Assert.Equal(context.Directory.Name, context.GetMetadata().Name);
            Assert.Equal("ThiagoRangel7", context.GetMetadata().Owner);
            Assert.Equal(new Version(0, 1, 0, 0), context.GetMetadata().PluginVersion);
            var type = Assert.Single(AppInfo.PluginManager.PluginTypes, t => t.FullName == typeof(CalibrationFilter).FullName);
            string payload = CoreFixtures.Data().ToJson();
            var store = new PluginSettingStore(type) { Enable = true };
            store[nameof(CalibrationFilter.CalibrationJson)].SetValue(payload);
            var settings = new Settings
            {
                Profiles = new ProfileCollection(new[] {
                    new Profile { Tablet = "Intended display tablet", Filters = new() { store } },
                    new Profile { Tablet = "Other profile" }
                })
            };
            var file = new FileInfo(Path.Combine(directory, "settings-backup.json"));
            settings.Serialize(file);
            Assert.True(Settings.TryDeserialize(file, out var restored));
            var saved = Assert.Single(restored.Profiles[0].Filters);
            Assert.True(saved.Enable); Assert.Empty(restored.Profiles[1].Filters);
            Assert.Equal(payload, saved[nameof(CalibrationFilter.CalibrationJson)].GetValue<string>());
            var tablet = IntegrationTests.Tablet();
            var digitizer = tablet.Properties.Specifications.Digitizer;
            digitizer.MaxX = digitizer.MaxY = 1000; digitizer.Width = digitizer.Height = 100;
            var filter = saved.Construct<IPositionedPipelineElement<IDeviceReport>>(tablet);
            IDeviceReport report = new TiltTabletReport { Position = new(500, 500), Tilt = new(12, -8) };
            filter.Consume(report);
            Assert.Equal(new Vector2(498, 497), ((IAbsolutePositionReport)report).Position);
            saved.Enable = false;
            restored.Serialize(file);
            Assert.True(Settings.TryDeserialize(file, out var bypassed));
            Assert.False(bypassed.Profiles[0].Filters[0].Enable);
            Assert.Equal(payload, bypassed.Profiles[0].Filters[0][nameof(CalibrationFilter.CalibrationJson)].GetValue<string>());
            saved[nameof(CalibrationFilter.CalibrationJson)].SetValue("");
            saved.Construct<IPositionedPipelineElement<IDeviceReport>>(tablet).Consume(report);
            Assert.Equal(new Vector2(498, 497), ((IAbsolutePositionReport)report).Position);
            Assert.True(AppInfo.PluginManager.UninstallPlugin(context));
            Assert.Empty(AppInfo.PluginManager.GetLoadedPlugins()); Assert.False(Directory.Exists(pluginPath));
            Assert.True(Settings.TryDeserialize(file, out var afterRemoval));
            Assert.Equal(payload, afterRemoval.Profiles[0].Filters[0][nameof(CalibrationFilter.CalibrationJson)].GetValue<string>());
        }
        finally
        {
            AppInfo.PluginManager = previous; AppInfo.Current = previousInfo;
            Directory.Delete(directory, true);
        }
    }
}
