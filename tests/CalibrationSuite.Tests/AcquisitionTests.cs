using CalibrationSuite.Otd;
using OpenTabletDriver.Desktop.RPC;
using OpenTabletDriver.Plugin.Tablet;
using Xunit;
using System.Collections.Concurrent;

namespace CalibrationSuite.Tests;

public sealed class FakeDaemon : IAcquisitionDaemon
{
    public TabletReference Tablet { get; } = IntegrationTests.Tablet();
    public event EventHandler<DebugReportData>? DeviceReport;
    public event EventHandler? Invalidated;
    public List<bool> DebugCalls { get; } = [];
    public TaskCompletionSource Enabled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Func<bool, Task>? DebugAction { get; set; }
    public TabletReference[]? ConnectedTablets { get; set; }
    public Task SetDebug(bool enabled)
    {
        DebugCalls.Add(enabled);
        if (enabled) Enabled.TrySetResult();
        return DebugAction?.Invoke(enabled) ?? Task.CompletedTask;
    }
    public Task<TabletReference[]> GetTablets() => Task.FromResult(ConnectedTablets ?? [Tablet]);
    public void Send(DebugReportData? report = null) => DeviceReport?.Invoke(this, report ?? IntegrationTests.Report(Tablet));
    public void Invalidate() => Invalidated?.Invoke(this, EventArgs.Empty);
    public bool HasSubscribers => DeviceReport != null || Invalidated != null;
}

public class AcquisitionTests
{
    private sealed class PausedContext : SynchronizationContext
    {
        private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> pending = new();
        public override void Post(SendOrPostCallback callback, object? state) => pending.Enqueue((callback, state));
        public void Drain() { while (pending.TryDequeue(out var item)) item.Callback(item.State); }
    }

    [Fact]
    public async Task DisconnectCleansUpWithoutPumpingTheCallingContext()
    {
        var daemon = new FakeDaemon(); var session = Session(daemon);
        var context = new PausedContext();
        var previous = SynchronizationContext.Current;
        Task<string> run;
        try
        {
            SynchronizationContext.SetSynchronizationContext(context);
            run = session.RunAsync();
        }
        finally { SynchronizationContext.SetSynchronizationContext(previous); }
        try
        {
            await daemon.Enabled.Task.WaitAsync(TimeSpan.FromSeconds(3));
            daemon.Invalidate();
            Assert.Contains("disconnected", await run.WaitAsync(TimeSpan.FromSeconds(3)));
            Assert.Equal(new[] { true, false }, daemon.DebugCalls);
            Assert.False(daemon.HasSubscribers);
        }
        finally { session.Cancel(); context.Drain(); }
    }

    private static AcquisitionSession Session(FakeDaemon daemon, AcquisitionOptions? options = null) =>
        new(daemon, daemon.Tablet, options ?? new(StallMilliseconds: 1000));

    [Fact]
    public async Task CancelDisablesAndUnsubscribes()
    {
        var daemon = new FakeDaemon(); var session = Session(daemon);
        var run = Task.Run(session.RunAsync); await daemon.Enabled.Task.WaitAsync(TimeSpan.FromSeconds(3));
        session.Cancel("User cancelled");
        Assert.Contains("User cancelled", await run.WaitAsync(TimeSpan.FromSeconds(3)));
        Assert.Equal(new[] { true, false }, daemon.DebugCalls);
        Assert.False(daemon.HasSubscribers);
    }

    [Fact]
    public async Task CancelBeforeStartNeverEnablesDebug()
    {
        var daemon = new FakeDaemon(); var session = Session(daemon); session.Cancel();
        await session.RunAsync(); Assert.Empty(daemon.DebugCalls); Assert.False(daemon.HasSubscribers);
    }

    [Fact]
    public async Task OverflowCancelsRatherThanSilentlyDroppingReports()
    {
        var daemon = new FakeDaemon();
        daemon.DebugAction = enabled => { if (enabled) { daemon.Send(); daemon.Send(); } return Task.CompletedTask; };
        var result = await Session(daemon, new(Capacity: 1)).RunAsync();
        Assert.Contains("overflow", result); Assert.Equal(new[] { true, false }, daemon.DebugCalls);
    }

    [Fact]
    public async Task PenAbsenceWaitsAndReportsResumeWithoutRestart()
    {
        var daemon = new FakeDaemon(); var session = Session(daemon, new(StallMilliseconds: 80, RpcTimeoutMilliseconds: 80));
        var run = Task.Run(session.RunAsync);
        await daemon.Enabled.Task.WaitAsync(TimeSpan.FromSeconds(3));
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        daemon.Send();
        while (session.Latest == null) await Task.Delay(10, deadline.Token);
        await Task.Delay(650);
        Assert.Null(session.Latest);
        Assert.False(run.IsCompleted);
        daemon.Send();
        while (session.Latest == null) await Task.Delay(10, deadline.Token);
        Assert.Equal(321u, session.Latest.Pressure);
        session.Cancel(); await run;
        Assert.Equal(new[] { true, false }, daemon.DebugCalls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(1)]
    public async Task ChangedOrAmbiguousInputNeverEnablesDebug(int count)
    {
        var daemon = new FakeDaemon();
        var replacement = IntegrationTests.Tablet();
        replacement.Properties.Name = "Different tablet";
        daemon.ConnectedTablets = Enumerable.Repeat(replacement, count).ToArray();
        await Session(daemon).RunAsync();
        Assert.Empty(daemon.DebugCalls);
        Assert.False(daemon.HasSubscribers);
    }
    [Fact]
    public async Task InvalidationCancelsAndUnsubscribes()
    {
        var daemon = new FakeDaemon(); var session = Session(daemon);
        var run = Task.Run(session.RunAsync); await daemon.Enabled.Task.WaitAsync(TimeSpan.FromSeconds(3));
        daemon.Invalidate();
        Assert.Contains("disconnected", await run.WaitAsync(TimeSpan.FromSeconds(3)));
        Assert.False(daemon.HasSubscribers); Assert.Equal(new[] { true, false }, daemon.DebugCalls);
    }

    [Fact]
    public async Task CancellationDuringEnableWaitsThenDisables()
    {
        var daemon = new FakeDaemon(); var enable = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        daemon.DebugAction = on => on ? enable.Task : Task.CompletedTask;
        var session = Session(daemon); var run = Task.Run(session.RunAsync);
        await daemon.Enabled.Task.WaitAsync(TimeSpan.FromSeconds(3)); session.Cancel();
        Assert.Equal(new[] { true }, daemon.DebugCalls);
        enable.SetResult(); await run.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(new[] { true, false }, daemon.DebugCalls);
    }

    [Fact]
    public async Task ReconstructionFailureCancelsAndCleansUp()
    {
        var daemon = new FakeDaemon(); var session = Session(daemon);
        var run = Task.Run(session.RunAsync); await daemon.Enabled.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var report = IntegrationTests.Report(daemon.Tablet); report.Path = "Unknown.Report"; daemon.Send(report);
        Assert.Contains("Cannot reconstruct", await run.WaitAsync(TimeSpan.FromSeconds(3)));
        Assert.False(daemon.HasSubscribers); Assert.Equal(new[] { true, false }, daemon.DebugCalls);
    }

    [Fact]
    public async Task CleanupFailureIsVisible()
    {
        var daemon = new FakeDaemon(); daemon.DebugAction = on => on ? Task.CompletedTask : Task.FromException(new IOException("Disconnected"));
        var session = Session(daemon, new(StallMilliseconds: 50));
        var run = Task.Run(session.RunAsync);
        await daemon.Enabled.Task.WaitAsync(TimeSpan.FromSeconds(3));
        session.Cancel();
        var message = await run;
        Assert.Contains("cleanup could not be confirmed", message);
        Assert.Contains("Restart OTD and Calibrator", message);
        Assert.True(session.RestartRequired);
        Assert.False(daemon.HasSubscribers);
        Assert.Equal(new[] { true, false }, daemon.DebugCalls);
    }

    [Fact]
    public async Task EnableReplyAfterCleanupTimeoutStillDisablesExactlyOnce()
    {
        var daemon = new FakeDaemon();
        var enable = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var disabled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        daemon.DebugAction = on =>
        {
            if (on) return enable.Task;
            disabled.SetResult();
            return Task.CompletedTask;
        };
        var session = Session(daemon, new(RpcTimeoutMilliseconds: 50));
        var run = Task.Run(session.RunAsync);
        await daemon.Enabled.Task.WaitAsync(TimeSpan.FromSeconds(3));
        session.Cancel();
        Assert.Contains("cleanup could not be confirmed", await run.WaitAsync(TimeSpan.FromSeconds(3)));
        Assert.True(session.RestartRequired);
        Assert.Equal(new[] { true }, daemon.DebugCalls);
        enable.SetResult();
        await disabled.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(new[] { true, false }, daemon.DebugCalls);
    }

    [Fact]
    public async Task FailedEnableReplyStillAttemptsDisable()
    {
        var daemon = new FakeDaemon();
        daemon.DebugAction = on => on ? Task.FromException(new IOException("Enable reply failed")) : Task.CompletedTask;
        var session = Session(daemon);
        Assert.Contains("Enable reply failed", await session.RunAsync());
        Assert.Equal(new[] { true, false }, daemon.DebugCalls);
        Assert.False(session.RestartRequired);
    }

    [Fact]
    public async Task EachIdenticalEventIsConsumedOnceWithMonotonicIngressTime()
    {
        var daemon = new FakeDaemon(); var times = new List<long>();
        var consumed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var session = new AcquisitionSession(daemon, daemon.Tablet,
            reconstruct: (report, time) => { times.Add(time); if (times.Count == 2) consumed.SetResult(); return ReportReadingFactory.Read(report, time); });
        var run = Task.Run(session.RunAsync); await daemon.Enabled.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var report = IntegrationTests.Report(daemon.Tablet); daemon.Send(report); daemon.Send(report);
        await consumed.Task.WaitAsync(TimeSpan.FromSeconds(3)); session.Cancel(); await run;
        Assert.Equal(2, times.Count); Assert.True(times[1] >= times[0]);
    }

    [Fact]
    public async Task BlockedParserCannotPreventDebugCleanup()
    {
        var daemon = new FakeDaemon();
        // Buffer a report before the worker starts, so its first read need not yield.
        daemon.DebugAction = on => { if (on) daemon.Send(); return Task.CompletedTask; };
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var session = new AcquisitionSession(daemon, daemon.Tablet,
            new(StallMilliseconds: 80, RpcTimeoutMilliseconds: 80),
            reconstruct: (report, time) => { entered.SetResult(); release.Wait(); return ReportReadingFactory.Read(report, time); });
        var run = Task.Run(session.RunAsync);
        try
        {
            await daemon.Enabled.Task.WaitAsync(TimeSpan.FromSeconds(3));
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Contains("consumer stalled", await run.WaitAsync(TimeSpan.FromSeconds(3)));
            Assert.True(session.RestartRequired);
            Assert.False(daemon.HasSubscribers);
            Assert.Equal(new[] { true, false }, daemon.DebugCalls);
        }
        finally { release.Set(); }
    }
}
