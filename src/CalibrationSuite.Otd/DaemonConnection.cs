using OpenTabletDriver.Desktop;
using OpenTabletDriver.Desktop.Contracts;
using OpenTabletDriver.Desktop.Reflection;
using OpenTabletDriver.Desktop.RPC;
using OpenTabletDriver.Plugin.Tablet;

namespace CalibrationSuite.Otd;

public interface IAcquisitionDaemon
{
    event EventHandler<DebugReportData>? DeviceReport;
    event EventHandler? Invalidated;
    Task SetDebug(bool enabled);
    Task<TabletReference[]> GetTablets();
}

public sealed class DaemonConnection : IAcquisitionDaemon
{
    private readonly RpcClient<IDriverDaemon> client;
    private Task? connecting;
    public bool Ready { get; private set; }
    public event EventHandler<DebugReportData>? DeviceReport;
    public event EventHandler? Invalidated;

    public DaemonConnection(string pipeName = "OpenTabletDriver.Daemon")
    {
        client = new(pipeName);
        client.Disconnected += (_, _) =>
        {
            Ready = false;
            connecting = null;
            Invalidated?.Invoke(this, EventArgs.Empty);
        };
    }

    public Task Connect()
    {
        if (connecting?.IsFaulted == true) connecting = null;
        return connecting ??= Initialize();
    }

    private async Task Initialize()
    {
        if (!client.IsConnected) await client.Connect().ConfigureAwait(false);
        AppInfo.Current = await client.Instance.GetApplicationInfo().ConfigureAwait(false);
        AppInfo.PluginManager = new DesktopPluginManager();
        AppInfo.PluginManager.Load();
        client.Instance.DeviceReport += (_, report) => DeviceReport?.Invoke(this, report);
        client.Instance.TabletsChanged += (_, _) => Invalidated?.Invoke(this, EventArgs.Empty);
        Ready = true;
    }

    public Task SetDebug(bool enabled) => client.Instance.SetTabletDebug(enabled);
    public async Task<TabletReference[]> GetTablets() => (await client.Instance.GetTablets().ConfigureAwait(false)).ToArray();
}
