using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CalibrationSuite.Otd;
using Microsoft.Win32;

namespace CalibrationSuite.Calibrator;

public sealed class MainWindow : Window
{
    private readonly DaemonConnection daemon = new();
    private readonly TextBlock tabletName = new() { Margin = new(0, 16, 0, 0) };
    private readonly TextBlock status = new() { Margin = new(0, 12, 0, 0) };
    private readonly Button start = new() { Content = "Start preview", IsEnabled = false };
    private readonly CancellationTokenSource lifetime = new();
    private AcquisitionSession? session;
    private bool running, restartRequired;

    public MainWindow()
    {
        Title = "Calibration Suite"; Width = 620; Height = 440; MinWidth = 500; MinHeight = 360;
        Background = new SolidColorBrush(Color.FromRgb(246, 247, 249));
        var panel = new StackPanel { Margin = new(28) };
        panel.Children.Add(new TextBlock { Text = "Calibration Suite", FontSize = 30, FontWeight = FontWeights.SemiBold });
        panel.Children.Add(new TextBlock { Text = "Display-tablet acquisition preview · OTD 0.6.7", Margin = new(0, 4, 0, 20) });
        panel.Children.Add(new TextBlock { Text = "Move this window onto your tablet's screen, then start the full-screen preview." });
        panel.Children.Add(new TextBlock { Text = "Connect only that tablet. Its full drawing area must match the full screen, with the same orientation. Disable other filters in OTD to avoid a distracting cursor; aim with the physical pen tip.", Margin = new(0, 12, 0, 12) });
        panel.Children.Add(new TextBlock { Text = "Close OTD's tablet debugger. You can lift the pen and pause between targets. This milestone previews readings only; it does not capture or save calibration." });
        panel.Children.Add(tabletName); panel.Children.Add(start); panel.Children.Add(status);
        Content = new ScrollViewer { Content = panel };
        Loaded += async (_, _) => await MonitorTabletAsync();
        start.Click += async (_, _) =>
        {
            if (running || restartRequired || lifetime.IsCancellationRequested) return;
            running = true;
            await StartAsync();
            running = false;
            if (lifetime.IsCancellationRequested) Close();
        };
        SystemEvents.DisplaySettingsChanged += OnDisplayChanged;
        Closing += OnClosing;
        Closed += (_, _) => SystemEvents.DisplaySettingsChanged -= OnDisplayChanged;
    }

    private async Task MonitorTabletAsync()
    {
        while (!lifetime.IsCancellationRequested)
        {
            if (!running && !restartRequired)
            {
                try
                {
                    await daemon.Connect().WaitAsync(TimeSpan.FromSeconds(3), lifetime.Token);
                    var tablet = TabletSelection.Single(await daemon.GetTablets().WaitAsync(TimeSpan.FromSeconds(3), lifetime.Token));
                    if (!running && !restartRequired && !lifetime.IsCancellationRequested) { tabletName.Text = tablet.Properties.Name; start.IsEnabled = true; }
                }
                catch (Exception ex)
                {
                    if (!running) { tabletName.Text = $"Start OTD and connect your display tablet. {ex.Message}"; start.IsEnabled = false; }
                }
            }
            try { await Task.Delay(1000, lifetime.Token); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task StartAsync()
    {
        start.IsEnabled = false;
        PreviewWindow? preview = null;
        try
        {
            var tablet = TabletSelection.Single(await daemon.GetTablets().WaitAsync(TimeSpan.FromSeconds(3), lifetime.Token));
            lifetime.Token.ThrowIfCancellationRequested();
            var display = DisplayGeometry.ForWindow(this);
            session = new(daemon, tablet);
            preview = new(session, tablet, display) { Owner = this };
            preview.GeometryChanged += CancelForDisplayChange;
            preview.Show();
            status.Text = await session.RunAsync();
            restartRequired = session.RestartRequired;
        }
        catch (Exception ex) { status.Text = ex.Message; }
        finally { preview?.Close(); session = null; }
    }

    private void OnDisplayChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(CancelForDisplayChange);

    private void CancelForDisplayChange() =>
        session?.Cancel("Display geometry/DPI changed. Move this window to the tablet screen and start again.");

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        e.Cancel = running;
        lifetime.Cancel();
        session?.Cancel();
    }
}
