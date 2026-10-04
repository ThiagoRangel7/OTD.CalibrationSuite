using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CalibrationSuite.Core;
using CalibrationSuite.Otd;
using Microsoft.Win32;
using OpenTabletDriver.Plugin.Tablet;

namespace CalibrationSuite.Calibrator;

public sealed class MainWindow : Window
{
    private readonly DaemonConnection daemon = new();
    private readonly CalibrationWorkspace workspace = new();
    private readonly CaptureSettingsPanel settings = new();
    private readonly TextBlock tabletName = Text();
    private readonly TextBlock total = Text();
    private readonly TextBlock state = Text();
    private readonly TextBlock status = Text();
    private readonly Button start = new() { Content = "Calibrate", IsEnabled = false };
    private readonly Button resume = new() { Content = "Resume Capture", Visibility = Visibility.Collapsed };
    private readonly Button review = new() { Content = "Review Calibration", IsEnabled = false };
    private readonly Button copy = new() { Content = "Copy Calibration", IsEnabled = false };
    private readonly CancellationTokenSource lifetime = new();
    private AcquisitionSession? acquisition;
    private TabletReference? readyTablet;
    private bool running, restartRequired, closingAccepted;

    private static TextBlock Text(string value = "") => new() { Text = value, TextWrapping = TextWrapping.Wrap, Margin = new(0, 8, 0, 8) };

    public MainWindow()
    {
        Title = "Calibration Suite"; Width = Math.Min(720, SystemParameters.WorkArea.Width); Height = Math.Min(800, SystemParameters.WorkArea.Height); MinWidth = 540; MinHeight = 420;
        Background = new SolidColorBrush(Color.FromRgb(246, 247, 249));
        var panel = new StackPanel { Margin = new(28) };
        panel.Children.Add(new TextBlock { Text = "Calibration Suite", FontSize = 30, FontWeight = FontWeights.SemiBold });
        panel.Children.Add(Text("Move this window onto your tablet screen. Its full drawing area must match the full screen, with X right and Y down. Aim with the physical nib, not the cursor."));
        panel.Children.Add(Text("Connect only that tablet and close OTD's tablet debugger. Disable other filters while calibrating, then restore them afterward. You may lift the pen and pause between captures."));
        tabletName.Text = "Connecting to OTD…";
        panel.Children.Add(tabletName); panel.Children.Add(settings); panel.Children.Add(total);
        var actions = new WrapPanel();
        foreach (var button in new[] { start, resume, review, copy })
        { button.Padding = new(12, 7, 12, 7); button.Margin = new(0, 6, 8, 6); actions.Children.Add(button); }
        panel.Children.Add(actions); panel.Children.Add(state);
        panel.Children.Add(Text(CalibrationWorkspace.CopyReminder)); panel.Children.Add(status);
        Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        settings.Changed += Refresh;
        Loaded += async (_, _) => await MonitorTabletAsync();
        start.Click += async (_, _) => await CaptureAsync(true);
        resume.Click += async (_, _) => await CaptureAsync(false);
        review.Click += async (_, _) => await ReviewAsync();
        copy.Click += (_, _) =>
        {
            try { Clipboard.SetText(workspace.CopyPayload()); status.Text = "Calibration copied. " + CalibrationWorkspace.CopyReminder; }
            catch (Exception ex) { status.Text = $"Could not copy calibration: {ex.Message}. Try Copy Calibration again."; }
        };
        SystemEvents.DisplaySettingsChanged += OnDisplayChanged;
        Closing += OnClosing;
        Closed += (_, _) => SystemEvents.DisplaySettingsChanged -= OnDisplayChanged;
        Refresh();
    }

    private void Refresh()
    {
        bool ready = readyTablet != null && CaptureDevice.Supported(readyTablet) && !running && !restartRequired;
        settings.IsEnabled = !running; start.IsEnabled = false;
        try
        {
            var value = settings.Read();
            int poses = value.AzimuthCount == 0 || value.NonUprightAltitudeCount == 0 ? 1
                : checked(1 + value.AzimuthCount * value.NonUprightAltitudeCount);
            int count = value.TotalCaptures;
            if (readyTablet != null) _ = value.CreateGrid(CaptureDevice.Geometry(readyTablet));
            total.Text = $"{value.RowCount} × {value.ColumnCount} targets · {poses} poses each · {count} captures total";
            start.IsEnabled = ready;
        }
        catch (Exception ex) when (ex is CalibrationException or OverflowException) { total.Text = ex is OverflowException ? "Capture counts are too large." : ex.Message; }
        resume.Visibility = workspace.Session != null ? Visibility.Visible : Visibility.Collapsed;
        resume.IsEnabled = ready;
        review.IsEnabled = !running && workspace.HasSessionData;
        copy.IsEnabled = !running && workspace.Working != null;
        state.Text = (workspace.Session is { } draft ? $"Unfinished capture: {draft.CapturedCount}/{draft.Total}. " : "")
            + (workspace.Candidate != null ? "Candidate awaits review and acceptance. " : "")
            + (workspace.Working != null ? "Accepted calibration available — session only." : "No accepted calibration yet.");
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
                    if (!running && !lifetime.IsCancellationRequested)
                    {
                        readyTablet = tablet;
                        tabletName.Text = tablet.Properties.Name + (CaptureDevice.Supported(tablet)
                            ? " — ready. Guidance uses verified tilt directions. Check known physical angles during first-use review."
                            : " — capture unavailable: orientation guidance has not been verified for this tablet/parser.");
                    }
                }
                catch (Exception ex)
                {
                    if (!running) { readyTablet = null; tabletName.Text = $"Start OTD and connect your display tablet. {ex.Message}"; }
                }
                Refresh();
            }
            try { await Task.Delay(1000, lifetime.Token); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task CaptureAsync(bool newCapture, bool openReview = true)
    {
        if (running || restartRequired || lifetime.IsCancellationRequested) return;
        if (newCapture && (workspace.Session != null || workspace.Candidate != null)
            && MessageBox.Show(this, "Start a new calibration and discard unaccepted measurements? Accepted data is preserved.",
                "New calibration", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        running = true; Refresh();
        CaptureWindow? window = null; CaptureController? controller = null;
        bool complete = false;
        try
        {
            var tablet = TabletSelection.Single(await daemon.GetTablets().WaitAsync(TimeSpan.FromSeconds(3), lifetime.Token));
            lifetime.Token.ThrowIfCancellationRequested();
            if (!CaptureDevice.Supported(tablet)) throw new CalibrationException("Orientation guidance is not verified for this tablet/parser.");
            var geometry = CaptureDevice.Geometry(tablet);
            var display = DisplayGeometry.ForWindow(this);
            if (newCapture) workspace.Begin(settings.Read(), geometry);
            var draft = workspace.Session ?? throw new CalibrationException("No unfinished capture to resume.");
            if (!draft.Geometry.Matches(geometry)) throw new CalibrationException("Tablet geometry changed. Start a new calibration.");
            if (!draft.Complete)
            {
                controller = new(draft, tilt => CaptureDevice.Orientation(tilt, draft.Settings.AngleToleranceDeg), CaptureDevice.ReportType);
                acquisition = new(daemon, tablet); acquisition.Reading += controller.Read;
                window = new(acquisition, controller, draft, tablet.Properties.Name, display) { Owner = this };
                window.GeometryChanged += CancelForDisplayChange;
                window.Show();
                status.Text = await acquisition.RunAsync();
                restartRequired = acquisition.RestartRequired;
            }
            if (draft.Complete) { workspace.Finish(); complete = true; }
        }
        catch (Exception ex) { status.Text = ex.Message; }
        finally
        {
            window?.Close(); controller?.Stop(); acquisition = null; running = false; Refresh();
            if (lifetime.IsCancellationRequested) Close();
        }
        if (complete && openReview && !lifetime.IsCancellationRequested) await ReviewAsync();
    }

    private async Task ReviewAsync()
    {
        while (!lifetime.IsCancellationRequested)
        {
            var window = new ReviewWindow(workspace) { Owner = this };
            window.ShowDialog(); Refresh();
            if (window.RecaptureSource is not { } source) return;
            workspace.Recapture(source, window.RecaptureIndex, window.WholeNode);
            Refresh(); await CaptureAsync(false, openReview: false);
            if (workspace.Session != null) return;
        }
    }

    private void OnDisplayChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(CancelForDisplayChange);
    private void CancelForDisplayChange() => acquisition?.Cancel("Display geometry/DPI changed. Move this window to the tablet screen and resume with fresh geometry.");
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!closingAccepted && workspace.HasSessionData && MessageBox.Show(this, CalibrationWorkspace.CloseReminder,
            "Session measurements", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        { e.Cancel = true; return; }
        closingAccepted = true; e.Cancel = running; lifetime.Cancel(); acquisition?.Cancel();
    }
}
