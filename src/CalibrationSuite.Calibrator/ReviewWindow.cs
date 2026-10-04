using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using CalibrationSuite.Core;

namespace CalibrationSuite.Calibrator;

public sealed class ReviewWindow : Window
{
    private readonly CalibrationWorkspace workspace;
    private readonly ComboBox source = new() { Margin = new(0, 8, 0, 8), MinWidth = 200 };
    private readonly DataGrid table = new() { IsReadOnly = true, AutoGenerateColumns = false, CanUserAddRows = false,
        SelectionMode = DataGridSelectionMode.Single, SelectionUnit = DataGridSelectionUnit.FullRow, MinHeight = 150 };
    private readonly TextBlock description = new() { TextWrapping = TextWrapping.Wrap, Margin = new(0, 8, 0, 12) };
    private readonly Button accept = new() { Content = "Accept Calibration" };
    private readonly Button pose = new() { Content = "Recapture Pose" };
    private readonly Button node = new() { Content = "Recapture Node" };
    private readonly Button discard = new() { Content = "Discard Unaccepted Data" };
    public CaptureDraft? RecaptureSource { get; private set; }
    public int RecaptureIndex { get; private set; }
    public bool WholeNode { get; private set; }
    private CaptureDraft? Selected => (source.SelectedItem as ComboBoxItem)?.Tag as CaptureDraft;

    public ReviewWindow(CalibrationWorkspace workspace)
    {
        this.workspace = workspace;
        Title = "Review Calibration"; Width = Math.Min(1080, SystemParameters.WorkArea.Width); Height = Math.Min(660, SystemParameters.WorkArea.Height); MinWidth = 650; MinHeight = 360;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var layout = new DockPanel { Margin = new(20) };
        var top = new StackPanel(); top.Children.Add(source); top.Children.Add(description);
        DockPanel.SetDock(top, Dock.Top); layout.Children.Add(top);
        var buttons = new WrapPanel { Margin = new(0, 12, 0, 0) };
        var close = new Button { Content = "Close" };
        foreach (var button in new[] { accept, pose, node, discard, close })
        { button.Margin = new(0, 0, 8, 4); button.Padding = new(10, 6, 10, 6); buttons.Children.Add(button); }
        DockPanel.SetDock(buttons, Dock.Bottom); layout.Children.Add(buttons); layout.Children.Add(table); Content = layout;
        foreach (var (header, property) in new[] { ("Node / pose", "Address"), ("Requested pose", "Requested"),
            ("Target (raw)", "Target"), ("Measured position", "Position"), ("Measured tilt", "Tilt"), ("Error (raw)", "Error") })
            table.Columns.Add(new DataGridTextColumn { Header = header, Binding = new Binding(property), Width = DataGridLength.Auto });
        if (workspace.Session != null) source.Items.Add(new ComboBoxItem { Content = "Unfinished capture", Tag = workspace.Session });
        if (workspace.Candidate != null) source.Items.Add(new ComboBoxItem { Content = "Candidate — awaiting acceptance", Tag = workspace.Candidate });
        if (workspace.Working != null) source.Items.Add(new ComboBoxItem { Content = "Accepted calibration — session only", Tag = workspace.Working });
        source.SelectionChanged += (_, _) => Refresh(); source.SelectedIndex = 0;
        accept.Click += (_, _) => { workspace.Accept(); Close(); };
        discard.Click += (_, _) => { workspace.Discard(); Close(); };
        pose.Click += (_, _) => Recapture(false); node.Click += (_, _) => Recapture(true);
        close.Click += (_, _) => Close();
    }

    private void Recapture(bool wholeNode)
    {
        if (table.SelectedItem is not ReviewRow row || Selected == null) return;
        if (workspace.Session != null && MessageBox.Show(this, "Replace the unfinished capture with this recapture? Accepted data is preserved.",
            "Recapture", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        RecaptureSource = Selected; RecaptureIndex = row.Index; WholeNode = wholeNode; Close();
    }

    private void Refresh()
    {
        var data = Selected;
        if (data == null) return;
        bool isCandidate = ReferenceEquals(data, workspace.Candidate), isWorking = ReferenceEquals(data, workspace.Working);
        accept.IsEnabled = isCandidate && workspace.Session == null;
        pose.IsEnabled = node.IsEnabled = isCandidate || isWorking;
        discard.IsEnabled = !isWorking;
        description.Text = $"{data.CapturedCount}/{data.Total} measurements. " + (isWorking
            ? "Accepted in this app session. Use Copy Calibration on the main screen, then paste and Save in OTD."
            : isCandidate ? "Accept enables copying and replaces accepted data in memory. It does not apply or save anything in OTD."
            : "Resume from the main screen. Backspace revisits previous poses; the accepted calibration is preserved.");
        static string Pair(Point2 p) => $"({p.X:F1}; {p.Y:F1})";
        table.ItemsSource = Enumerable.Range(0, data.Total).Select(i =>
        {
            var sample = data.Sample(i); var target = data.Target(i);
            return new ReviewRow(i, $"{i / data.Poses.Count + 1} / {i % data.Poses.Count + 1}", CaptureWindow.Pose(data.Poses[i % data.Poses.Count]),
                Pair(target), sample == null ? "Not captured" : Pair(sample.Position), sample == null ? "—" : Pair(sample.Tilt),
                sample == null ? "—" : Pair(sample.Position - target));
        }).ToArray();
        table.SelectedIndex = 0;
    }
    private sealed record ReviewRow(int Index, string Address, string Requested, string Target, string Position, string Tilt, string Error);
}
