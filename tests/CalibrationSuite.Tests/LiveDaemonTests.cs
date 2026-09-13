using CalibrationSuite.Otd;
using Xunit;
using Xunit.Abstractions;

namespace CalibrationSuite.Tests;

public sealed class LiveDaemonFactAttribute : FactAttribute
{
    public LiveDaemonFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("CALIBRATION_LIVE_OTD") != "1")
            Skip = "Opt-in read-only check: set CALIBRATION_LIVE_OTD=1 with OTD 0.6.7 running.";
    }
}

public class LiveDaemonTests(ITestOutputHelper output)
{
    [LiveDaemonFact]
    [Trait("Category", "LiveDaemon")]
    public async Task InstalledDaemonReadOnlyConnectionAndSingleTablet()
    {
        var daemon = new DaemonConnection();
        await daemon.Connect().WaitAsync(TimeSpan.FromSeconds(5));
        var tablet = TabletSelection.Single(await daemon.GetTablets().WaitAsync(TimeSpan.FromSeconds(3)));
        output.WriteLine($"Detected tablet: {tablet.Properties.Name}");
        Assert.True(daemon.Ready);
        // No settings reads or debug enablement.
    }
}
