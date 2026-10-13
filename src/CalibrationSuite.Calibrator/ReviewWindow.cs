using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using CalibrationSuite.Core;

namespace CalibrationSuite.Calibrator;

public sealed class ReviewWindow : Window
{
    private readonly CalibrationWorkspace workspace;
    private readonly ComboBox source = new() { Margin = new(0, 8, 0, 8), MinWidth = 200 };
    private readonly DataGrid table = new()
    {
        IsReadOnly = true,
        AutoGenerateColumns = false,
        CanUserAddRows = false,
        SelectionMode = DataGridSelectionMode.Single,
        SelectionUnit = DataGridSelectionUnit.FullRow
    };
    private readonly TextBlock description = Ui.Text();
    private readonly TextBlock summary = Ui.Heading("");
    private readonly Button accept = new() { Content = "Accept Calibration" };
    private readonly Button pose = new() { Content = "Recapture Pose" };
    private readonly Button node = new() { Content = "Recapture Node" };
    private readonly Button discard = new() { Content = "Discard Unaccepted Data" };
    public bool RecaptureRequested { get; private set; }
    private CaptureDraft? Selected => (source.SelectedItem as ComboBoxItem)?.Tag as CaptureDraft;

    public ReviewWindow(CalibrationWorkspace workspace)
    {
        this.workspace = workspace;
        SetResourceReference(StyleProperty, typeof(Window));
        Title = "Review Calibration";
        Width = Math.Min(1080, SystemParameters.WorkArea.Width);
        Height = Math.Min(700, SystemParameters.WorkArea.Height);
        MinWidth = 560;
        MinHeight = 400;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        source.Margin = new(0, 6, 0, 6);
        summary.FontSize = 14;
        summary.Margin = new(0, 0, 0, 2);
        description.FontSize = 12;
        description.Foreground = Ui.Muted;
        description.Margin = new(0, 0, 0, 8);
        var top = new StackPanel { Children = { Ui.Heading("Review calibration", 24), source, summary, description } };
        DockPanel.SetDock(top, Dock.Top);
        var buttons = new WrapPanel();
        var close = new Button { Content = "Close" };
        foreach (var button in new[] { accept, pose, node, discard, close })
        {
            button.FontSize = 13;
            button.Padding = new(12, 7, 12, 7);
            button.Margin = new(0, 0, 8, 6);
            buttons.Children.Add(button);
        }
        Ui.Primary(accept);
        var footer = new StackPanel
        {
            Margin = new(0, 10, 0, 0),
            Children = { buttons, Ui.Text("Session data → Copy on the main screen → paste in OTD → Save.", 12, Ui.Muted) }
        };
        DockPanel.SetDock(footer, Dock.Bottom);
        table.FontSize = 13;
        table.RowHeight = 34;
        table.GridLinesVisibility = DataGridGridLinesVisibility.Horizontal;
        table.HorizontalGridLinesBrush = Ui.Brush("#E3E9F1");
        table.BorderBrush = Ui.Brush("#B9C6D6");
        table.AlternatingRowBackground = Ui.Brush("#F2F5FA");
        Content = new DockPanel { Margin = new(24), Background = Ui.Brush("#F5F7FB"), Children = { top, footer, table } };
        foreach (var (header, property) in new[] { ("Node / pose", "Address"), ("Requested pose", "Requested"),
            ("Target (raw)", "Target"), ("Measured position", "Position"), ("Measured tilt", "Tilt"), ("Error (raw)", "Error") })
            table.Columns.Add(new DataGridTextColumn { Header = header, Binding = new Binding(property), Width = DataGridLength.Auto });
        if (workspace.Session != null)
            source.Items.Add(new ComboBoxItem { Content = "Unfinished capture", Tag = workspace.Session });
        if (workspace.Candidate != null)
            source.Items.Add(new ComboBoxItem { Content = "Candidate — awaiting acceptance", Tag = workspace.Candidate });
        if (workspace.Working != null)
            source.Items.Add(new ComboBoxItem { Content = "Accepted calibration — session only", Tag = workspace.Working });
        source.SelectionChanged += (_, _) => Refresh();
        source.SelectedIndex = 0;
        table.SelectionChanged += (_, _) =>
        {
            bool recapturable = Selected != null && (ReferenceEquals(Selected, workspace.Candidate) || ReferenceEquals(Selected, workspace.Working));
            pose.IsEnabled = node.IsEnabled = recapturable && table.SelectedItem != null;
        };
        accept.Click += (_, _) => { workspace.Accept(); Close(); };
        discard.Click += (_, _) => { workspace.Discard(); Close(); };
        pose.Click += (_, _) => Recapture(false);
        node.Click += (_, _) => Recapture(true);
        close.Click += (_, _) => Close();
    }

    private void Recapture(bool wholeNode)
    {
        if (table.SelectedItem is not ReviewRow row || Selected == null)
            return;
        if (workspace.Session != null && MessageBox.Show(this, "Replace the unfinished capture with this recapture? Accepted data is preserved.",
            "Recapture", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;
        workspace.Recapture(Selected, row.Index, wholeNode);
        RecaptureRequested = true;
        Close();
    }

    private void Refresh()
    {
        var data = Selected;
        if (data == null)
            return;
        bool isCandidate = ReferenceEquals(data, workspace.Candidate), isWorking = ReferenceEquals(data, workspace.Working);
        accept.IsEnabled = isCandidate && workspace.Session == null;
        pose.IsEnabled = node.IsEnabled = isCandidate || isWorking;
        discard.IsEnabled = !isWorking;
        summary.Text = $"{data.CapturedCount} / {data.Total} measurements · " + (isWorking ? "✓ Accepted" : isCandidate ? "○ Candidate" : "… Unfinished");
        description.Text = isWorking ? "Ready to copy from the main screen. Select a row to recapture."
            : isCandidate ? workspace.Session == null ? "Accept replaces accepted data in this session. Select a row to recapture."
                : "Finish or discard the unfinished capture before accepting this candidate."
            : "Resume from the main screen. Accepted data is preserved.";
        static string Pair(Point2 p) => $"({p.X:F1}; {p.Y:F1})";
        table.ItemsSource = Enumerable.Range(0, data.Total).Select(i =>
        {
            var sample = data.Sample(i);
            var target = data.Target(i);
            return new ReviewRow(i, $"{i / data.Poses.Count + 1} / {i % data.Poses.Count + 1}", CaptureWindow.Pose(data.Poses[i % data.Poses.Count]),
                Pair(target), sample == null ? "Not captured" : Pair(sample.Position), sample == null ? "—" : Pair(sample.Tilt),
                sample == null ? "—" : Pair(sample.Position - target));
        }).ToArray();
        table.SelectedIndex = 0;
    }
    private sealed record ReviewRow(int Index, string Address, string Requested, string Target, string Position, string Tilt, string Error);
}
