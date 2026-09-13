using System.IO.Pipes;
using CalibrationSuite.Otd;
using OpenTabletDriver.Desktop;
using OpenTabletDriver.Desktop.RPC;
using OpenTabletDriver.Plugin.Tablet;
using StreamJsonRpc;
using Xunit;

namespace CalibrationSuite.Tests;

// A synthetic daemon on a unique test pipe, using actual OTD DTOs and StreamJsonRpc.
public sealed class SyntheticRpcDaemon
{
    public TabletReference Tablet { get; } = IntegrationTests.Tablet();
    public List<bool> DebugCalls { get; } = [];
    public event EventHandler<DebugReportData>? DeviceReport;
    public Task<AppInfo> GetApplicationInfo() => Task.FromResult(new AppInfo
    { AppDataDirectory = Path.Combine(Path.GetTempPath(), "CalibrationSuite.Tests", "rpc") });
    public Task<IEnumerable<TabletReference>> GetTablets() => Task.FromResult<IEnumerable<TabletReference>>([Tablet]);
    public Task SetTabletDebug(bool enabled)
    {
        DebugCalls.Add(enabled);
        if (enabled) DeviceReport?.Invoke(this, IntegrationTests.Report(Tablet));
        return Task.CompletedTask;
    }
}

public class RpcTests
{
    [Fact]
    public async Task DirectRpcInitializesPluginManagerAndReconstructsDaemonEvents()
    {
        string pipe = "CalibrationSuite.Tests." + Guid.NewGuid().ToString("N");
        using var server = new NamedPipeServerStream(pipe, PipeDirection.InOut, 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        var connection = new DaemonConnection(pipe);
        var connect = connection.Connect();
        await server.WaitForConnectionAsync().WaitAsync(TimeSpan.FromSeconds(5));
        using var rpc = new JsonRpc(server);
        var synthetic = new SyntheticRpcDaemon();
        rpc.AddLocalRpcTarget(synthetic); rpc.StartListening();
        await connect.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(connection.Ready);
        Assert.Contains(AppInfo.PluginManager.PluginTypes, t => t.FullName == typeof(TiltTabletReport).FullName);
        var tablet = Assert.Single(await connection.GetTablets());
        Assert.NotSame(synthetic.Tablet, tablet);
        Assert.True(TabletSelection.Same(synthetic.Tablet, tablet));

        var received = new TaskCompletionSource<ReportReading>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.DeviceReport += (_, data) => received.TrySetResult(ReportReadingFactory.Read(data, 99));
        var session = new AcquisitionSession(connection, tablet);
        var run = Task.Run(session.RunAsync);
        var reading = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(321u, reading.Pressure);
        session.Cancel(); await run.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(session.CleanupConfirmed);
        Assert.Equal(new[] { true, false }, synthetic.DebugCalls);
    }
}
