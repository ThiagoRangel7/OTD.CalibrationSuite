using System.Diagnostics;
using System.Threading.Channels;
using OpenTabletDriver.Desktop.RPC;
using OpenTabletDriver.Plugin.Tablet;

namespace CalibrationSuite.Otd;

public sealed record AcquisitionOptions(int Capacity = 512, int StallMilliseconds = 2000,
    int RpcTimeoutMilliseconds = 3000);

public sealed class AcquisitionSession
{
    private readonly IAcquisitionDaemon daemon;
    private readonly TabletReference tablet;
    private readonly AcquisitionOptions options;
    private readonly Func<DebugReportData, long, ReportReading> reconstruct;
    private readonly CancellationTokenSource stop = new();
    private readonly Channel<(DebugReportData Data, long Timestamp)> queue;
    private string? reason;
    private ReportReading? latest;
    private int started;
    public ReportReading? Latest => Volatile.Read(ref latest) is { } reading
        && Stopwatch.GetElapsedTime(reading.Timestamp).TotalMilliseconds < 500 ? reading : null;
    public bool CleanupConfirmed { get; private set; } = true;
    public bool WorkerStopped { get; private set; } = true;

    public AcquisitionSession(IAcquisitionDaemon daemon, TabletReference tablet,
        AcquisitionOptions? options = null, Func<DebugReportData, long, ReportReading>? reconstruct = null)
    {
        this.daemon = daemon;
        this.tablet = tablet;
        this.options = options ?? new();
        this.reconstruct = reconstruct ?? ReportReadingFactory.Read;
        if (this.options.Capacity <= 0 || this.options.StallMilliseconds <= 0
            || this.options.RpcTimeoutMilliseconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(options));
        queue = Channel.CreateBounded<(DebugReportData, long)>(new BoundedChannelOptions(this.options.Capacity)
        { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });
    }

    public void Cancel(string message = "Preview cancelled.")
    {
        Interlocked.CompareExchange(ref reason, message, null);
        stop.Cancel();
    }

    private void OnInvalidated(object? sender, EventArgs e) => Cancel("Daemon disconnected or connected tablets changed. Start again when the intended tablet is ready.");

    private void OnReport(object? sender, DebugReportData report)
    {
        if (stop.IsCancellationRequested) return;
        // Timestamp at ingress, before filtering/reconstruction. No hardware timing is available.
        if (!queue.Writer.TryWrite((report, Stopwatch.GetTimestamp())))
            Cancel("Report queue overflow. Preview cancelled; retry after reducing system load.");
    }

    public async Task<string> RunAsync()
    {
        if (Interlocked.Exchange(ref started, 1) != 0) throw new InvalidOperationException("Session already started.");
        daemon.DeviceReport += OnReport;
        daemon.Invalidated += OnInvalidated;
        Task? enable = null;
        Task? worker = null;
        try
        {
            var connected = TabletSelection.Single(await daemon.GetTablets().WaitAsync(RpcTimeout, stop.Token));
            if (!TabletSelection.Same(tablet, connected))
                throw new InvalidOperationException("Connected tablet changed. Start again.");
            stop.Token.ThrowIfCancellationRequested();
            enable = daemon.SetDebug(true);
            await enable.WaitAsync(RpcTimeout, stop.Token);
            // The cancellation timer runs independently of a blocked report deserializer.
            stop.CancelAfter(ConsumerTimeout);
            worker = ConsumeAsync();
            await worker.WaitAsync(stop.Token);
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested)
        {
            reason ??= "Report consumer stalled. Preview cancelled; restart Calibrator if the parser does not stop.";
        }
        catch (Exception ex) { Cancel($"Preview stopped: {ex.Message}"); }
        finally
        {
            stop.Cancel();
            daemon.DeviceReport -= OnReport;
            daemon.Invalidated -= OnInvalidated;
            queue.Writer.TryComplete();
            if (enable != null)
            {
                CleanupConfirmed = false;
                // One ordered cleanup task also handles an enable reply arriving after timeout.
                var cleanup = DisableAfterEnableAsync(enable);
                try
                {
                    await cleanup.WaitAsync(RpcTimeout);
                    CleanupConfirmed = true;
                }
                catch (Exception ex)
                {
                    reason = $"{reason} Debug cleanup could not be confirmed ({ex.Message}). Close OTD's debugger or restart the daemon before retrying.";
                    _ = ObserveAsync(cleanup);
                }
            }
            if (worker != null)
            {
                try { await worker.WaitAsync(TimeSpan.FromMilliseconds(200)); }
                catch (TimeoutException)
                {
                    WorkerStopped = false;
                    reason += " Report parser has not stopped; restart Calibrator before retrying.";
                    _ = ObserveAsync(worker);
                }
                catch { /* The worker failure or cancellation has already ended this session. */ }
            }
        }
        return reason ?? "Preview ended.";
    }

    private TimeSpan RpcTimeout => TimeSpan.FromMilliseconds(options.RpcTimeoutMilliseconds);
    private TimeSpan ConsumerTimeout => TimeSpan.FromMilliseconds(Math.Max(options.StallMilliseconds, options.RpcTimeoutMilliseconds));

    private static async Task ObserveAsync(Task task)
    {
        // The session has already reported the failure/timeout; observe any late exception.
        try { await task.ConfigureAwait(false); } catch { }
    }

    private async Task DisableAfterEnableAsync(Task enable)
    {
        try { await enable.ConfigureAwait(false); } catch { /* Disable even after a failed reply. */ }
        await daemon.SetDebug(false).ConfigureAwait(false);
    }

    private async Task ConsumeAsync()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(20));
        while (await timer.WaitForNextTickAsync(stop.Token))
        {
            int processed = 0;
            while (processed++ < options.Capacity && queue.Reader.TryRead(out var item))
            {
                stop.Token.ThrowIfCancellationRequested();
                if (Stopwatch.GetElapsedTime(item.Timestamp).TotalMilliseconds > options.StallMilliseconds)
                    throw new InvalidOperationException("Report consumer stalled; stale queue discarded.");
                if (!TabletSelection.Same(tablet, item.Data.Tablet)) continue;
                var reading = reconstruct(item.Data, item.Timestamp);
                stop.Token.ThrowIfCancellationRequested();
                Volatile.Write(ref latest, reading);
            }
            stop.CancelAfter(ConsumerTimeout);
        }
    }
}
