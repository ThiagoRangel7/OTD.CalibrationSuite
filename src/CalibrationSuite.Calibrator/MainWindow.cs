using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
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
    private readonly TextBlock tabletDetail = Ui.Text("", 12, Ui.Muted);
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

    private static TextBlock Text(string value = "") => Ui.Text(value);

    public MainWindow()
    {
        SetResourceReference(StyleProperty, typeof(Window));
        Title = "Calibration Suite"; Width = Math.Min(920, SystemParameters.WorkArea.Width); Height = Math.Min(850, SystemParameters.WorkArea.Height); MinWidth = 560; MinHeight = 420;
        var panel = new StackPanel { Margin = new(24) };
        panel.Children.Add(Ui.Heading("Calibration Suite", 28));
        var intro = Text("Move this window to your tablet screen, then calibrate."); intro.Margin = new(0, 4, 0, 16); panel.Children.Add(intro);
        tabletName.Text = "Connecting to OTD…";
        tabletName.FontWeight = FontWeights.SemiBold;
        tabletDetail.Text = "Connect one display tablet and start OTD."; tabletDetail.Margin = new(0, 4, 0, 0);
        var connection = new StackPanel(); connection.Children.Add(tabletName); connection.Children.Add(tabletDetail);
        var connectionCard = Ui.Card(connection); connectionCard.Margin = new(0, 0, 0, 12); panel.Children.Add(connectionCard);
        panel.Children.Add(settings);
        total.FontSize = 17; total.FontWeight = FontWeights.SemiBold; total.Margin = new(0, 0, 0, 10); panel.Children.Add(total);
        var actions = new WrapPanel();
        foreach (var button in new[] { start, resume, review, copy })
        { button.Margin = new(0, 0, 8, 8); actions.Children.Add(button); }
        Ui.Primary(start); start.ToolTip = "Aim with the physical nib. Disable other filters during capture, then restore them.";
        copy.ToolTip = CalibrationWorkspace.CopyReminder;
        panel.Children.Add(actions);
        var setupHint = Ui.Text("Close OTD's debugger. Aim with the nib; you may lift or pause between captures. Disable other filters, then restore them.", 12, Ui.Muted);
        setupHint.Margin = new(0, 0, 0, 12); panel.Children.Add(setupHint);
        state.Margin = new(0, 0, 0, 4); panel.Children.Add(state);
        var saveHint = Ui.Text("Accepted data → Copy → paste in OTD → Save. Measurements stay in this app session until saved in OTD.", 12, Ui.Muted);
        panel.Children.Add(saveHint);
        status.Margin = new(0, 12, 0, 0); status.Foreground = Ui.Accent; panel.Children.Add(status);
        Content = new ScrollViewer { Content = panel, Background = Ui.Brush("#F5F7FB"), VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        settings.Changed += Refresh;
        Loaded += async (_, _) => await MonitorTabletAsync();
        start.Click += async (_, _) => await CaptureAsync(true);
        resume.Click += async (_, _) => await CaptureAsync(false);
        review.Click += async (_, _) => await ReviewAsync();
        copy.Click += (_, _) =>
        {
            try { Clipboard.SetText(workspace.CopyPayload()); status.Text = "✓ Calibration copied. Paste into OTD's Calibration data field and Save."; }
            catch (Exception ex) { status.Text = $"Could not copy calibration: {ex.Message}. Try Copy Calibration again."; }
        };
        SystemEvents.DisplaySettingsChanged += OnDisplayChanged;
        Closing += OnClosing;
        Closed += (_, _) => SystemEvents.DisplaySettingsChanged -= OnDisplayChanged;
        Refresh();
    }

    private void Refresh()
    {
        bool ready = readyTablet != null && !running && !restartRequired;
        settings.IsEnabled = !running; start.IsEnabled = false;
        try
        {
            var value = settings.Read(readyTablet == null ? null : CaptureDevice.Geometry(readyTablet));
            int poses = value.PoseCount;
            int count = value.TotalCaptures;
            total.Foreground = Ui.Ink;
            total.Text = $"Up to {count} captures  ·  {value.RowCount} × {value.ColumnCount} targets  ·  {poses} poses each";
            start.IsEnabled = ready;
        }
        catch (CalibrationException)
        { total.Foreground = Ui.Error; total.Text = "Check capture settings before starting."; }
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
                        tabletName.Text = "● Ready · " + tablet.Properties.Name;
                        tabletName.Foreground = Ui.Ink;
                        tabletDetail.Text = "Display tablet · full-area mapping. No reported tilt: upright positions only.";
                    }
                }
                catch (Exception ex)
                {
                    if (!running)
                    {
                        readyTablet = null; tabletName.Text = "Waiting for OTD / tablet"; tabletName.Foreground = Ui.Ink;
                        tabletDetail.Text = $"Start OTD and connect one display tablet. {ex.Message}";
                    }
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
        running = true;
        Refresh();
        CaptureWindow? window = null;
        CaptureController? controller = null;
        bool complete = false;
        try
        {
            var tablet = TabletSelection.Single(await daemon.GetTablets().WaitAsync(TimeSpan.FromSeconds(3), lifetime.Token));
            lifetime.Token.ThrowIfCancellationRequested();
            var geometry = CaptureDevice.Geometry(tablet);
            var display = DisplayGeometry.ForWindow(this);
            if (newCapture) workspace.Begin(settings.Read(), geometry);
            var draft = workspace.Session ?? throw new CalibrationException("No unfinished capture to resume.");
            if (!draft.Geometry.Matches(geometry)) throw new CalibrationException("Tablet geometry changed. Start a new calibration.");
            if (!draft.Complete)
            {
                controller = new(draft);
                acquisition = new(daemon, tablet);
                acquisition.Reading += controller.Read;
                window = new(acquisition, controller, draft, tablet.Properties.Name, display) { Owner = this };
                window.GeometryChanged += CancelForDisplayChange;
                window.Show();
                status.Text = await acquisition.RunAsync();
                restartRequired = acquisition.RestartRequired;
            }
            if (draft.Complete)
            {
                workspace.Finish();
                complete = true;
            }
        }
        catch (Exception ex) { status.Text = ex.Message; }
        finally
        {
            window?.Close();
            controller?.Stop();
            acquisition = null;
            running = false;
            Refresh();
            if (lifetime.IsCancellationRequested) Close();
        }
        if (complete && openReview && !lifetime.IsCancellationRequested) await ReviewAsync();
    }

    private async Task ReviewAsync()
    {
        while (!lifetime.IsCancellationRequested)
        {
            var window = new ReviewWindow(workspace) { Owner = this };
            window.ShowDialog();
            Refresh();
            if (window.RecaptureSource is not { } source) return;
            workspace.Recapture(source, window.RecaptureIndex, window.WholeNode);
            Refresh();
            await CaptureAsync(false, openReview: false);
            if (workspace.Session != null) return;
        }
    }

    private void OnDisplayChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(CancelForDisplayChange);
    private void CancelForDisplayChange() => acquisition?.Cancel("Display geometry/DPI changed. Move this window to the tablet screen and resume with fresh geometry.");
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!closingAccepted && workspace.HasSessionData && MessageBox.Show(this, CalibrationWorkspace.CloseReminder,
            "Session measurements", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            e.Cancel = true;
            return;
        }
        closingAccepted = true;
        e.Cancel = running;
        lifetime.Cancel();
        acquisition?.Cancel();
    }
}
