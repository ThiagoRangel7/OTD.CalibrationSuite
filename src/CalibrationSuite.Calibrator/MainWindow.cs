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
    private Task<string>? running;
    private PreviewWindow? preview;
    private bool busy, restartRequired;

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
        start.Click += async (_, _) => await StartAsync();
        SystemEvents.DisplaySettingsChanged += OnDisplayChanged;
        Closing += OnClosing;
        Closed += (_, _) => SystemEvents.DisplaySettingsChanged -= OnDisplayChanged;
    }

    private async Task MonitorTabletAsync()
    {
        while (!lifetime.IsCancellationRequested)
        {
            if (!busy && !restartRequired)
            {
                try
                {
                    await daemon.Connect().WaitAsync(TimeSpan.FromSeconds(3), lifetime.Token);
                    var tablet = TabletSelection.Single(await daemon.GetTablets().WaitAsync(TimeSpan.FromSeconds(3), lifetime.Token));
                    if (!busy && !restartRequired && !lifetime.IsCancellationRequested) { tabletName.Text = tablet.Properties.Name; start.IsEnabled = true; }
                }
                catch (Exception ex)
                {
                    if (!busy) { tabletName.Text = $"Start OTD and connect your display tablet. {ex.Message}"; start.IsEnabled = false; }
                }
            }
            try { await Task.Delay(1000, lifetime.Token); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task StartAsync()
    {
        if (busy || restartRequired || lifetime.IsCancellationRequested) return;
        busy = true; start.IsEnabled = false;
        try
        {
            var tablet = TabletSelection.Single(await daemon.GetTablets().WaitAsync(TimeSpan.FromSeconds(3), lifetime.Token));
            lifetime.Token.ThrowIfCancellationRequested();
            var display = DisplayGeometry.ForWindow(this);
            session = new(daemon, tablet);
            preview = new(session, tablet, display) { Owner = this };
            preview.GeometryChanged += CancelForDisplayChange;
            preview.Show();
            running = Task.Run(session.RunAsync);
            status.Text = await running;
            restartRequired = !session.CleanupConfirmed || !session.WorkerStopped;
            if (restartRequired) status.Text += " Restart OTD and Calibrator before retrying.";
        }
        catch (Exception ex) { status.Text = ex.Message; }
        finally { preview?.Close(); preview = null; session = null; running = null; busy = false; }
    }

    private void OnDisplayChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(CancelForDisplayChange);

    private void CancelForDisplayChange()
    {
        session?.Cancel("Display geometry/DPI changed. Move this window to the tablet screen and start again.");
        preview?.Close();
    }

    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (running == null) { lifetime.Cancel(); return; }
        e.Cancel = true;
        if (lifetime.IsCancellationRequested) return;
        lifetime.Cancel(); session?.Cancel(); preview?.Close();
        await running;
        running = null; Close();
    }
}
