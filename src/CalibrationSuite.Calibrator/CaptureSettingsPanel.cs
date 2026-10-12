using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using CalibrationSuite.Core;

namespace CalibrationSuite.Calibrator;

public sealed class CaptureSettingsPanel : Grid
{
    private sealed record Field(string Label, TextBox Input, TextBlock Error);
    private readonly Dictionary<string, Field> fields = [];
    private readonly List<Border> cards = [];
    private readonly TextBlock validation = Ui.Text("", 13, Ui.Error);
    private bool twoColumns;
    public event Action? Changed;

    public CaptureSettingsPanel()
    {
        var defaults = new CaptureSettings();
        var grid = Group("Target grid", "Full tablet area → full screen · X right / Y down");
        AddField(grid, nameof(defaults.RowCount), "Rows", defaults.RowCount);
        AddField(grid, nameof(defaults.ColumnCount), "Columns", defaults.ColumnCount);
        AddField(grid, nameof(defaults.GridInsetMm), "Grid inset", defaults.GridInsetMm, "mm");
        var poses = Group("Pen poses", "0 directions or levels = upright only.");
        AddField(poses, nameof(defaults.AzimuthCount), "Azimuth directions", defaults.AzimuthCount);
        AddField(poses, nameof(defaults.NonUprightAltitudeCount), "Non-upright levels", defaults.NonUprightAltitudeCount);
        AddField(poses, nameof(defaults.MinAltitudeDeg), "Minimum altitude", defaults.MinAltitudeDeg, "°", "50° altitude allows 40° of lean from upright.");
        var sampling = Group("Sampling");
        AddField(sampling, nameof(defaults.CaptureWindowMs), "Burst duration", defaults.CaptureWindowMs, "ms", "Minimum time to hold a valid pose during capture.");
        AddField(sampling, nameof(defaults.MinSamplesPerCapture), "Minimum reports", defaults.MinSamplesPerCapture);
        AddField(sampling, nameof(defaults.ReportGapTimeoutMs), "Report gap timeout", defaults.ReportGapTimeoutMs, "ms");
        AddField(sampling, nameof(defaults.CaptureTimeoutMs), "Capture timeout", defaults.CaptureTimeoutMs, "ms");
        var quality = Group("Quality limits", "Stability: 95th-percentile deviation from the median.");
        AddField(quality, nameof(defaults.AngleToleranceDeg), "Angle tolerance", defaults.AngleToleranceDeg, "°", "Allowed difference from the requested pen angle. A larger value makes the target easier to hit.");
        AddField(quality, nameof(defaults.PositionStabilityMm), "Position stability", defaults.PositionStabilityMm, "mm", "Allowed tip movement during capture. Smaller values require a steadier hold.");
        AddField(quality, nameof(defaults.TiltStabilityDeg), "Tilt stability", defaults.TiltStabilityDeg, "°", "Allowed tilt variation during capture. Smaller values require a steadier hold.");
        Children.Add(validation);
        ArrangeCards(false);
        SizeChanged += (_, _) => { bool wide = ActualWidth >= 620; if (wide != twoColumns) ArrangeCards(wide); };
    }

    private StackPanel Group(string title, string? hint = null)
    {
        var panel = new StackPanel();
        var heading = Ui.Heading(title); heading.Margin = new(0, 0, 0, 8); panel.Children.Add(heading);
        if (hint != null) panel.Children.Add(Ui.Text(hint, 12, Ui.Muted));
        var card = Ui.Card(panel); cards.Add(card); Children.Add(card); return panel;
    }

    private void ArrangeCards(bool wide)
    {
        twoColumns = wide; ColumnDefinitions.Clear(); RowDefinitions.Clear();
        int columns = wide ? 2 : 1;
        for (int i = 0; i < columns; i++) ColumnDefinitions.Add(new());
        for (int i = 0; i < cards.Count / columns + 1; i++) RowDefinitions.Add(new() { Height = GridLength.Auto });
        for (int i = 0; i < cards.Count; i++)
        {
            SetRow(cards[i], i / columns); SetColumn(cards[i], i % columns);
            cards[i].Margin = new(0, 0, wide && i % columns == 0 ? 12 : 0, 12);
        }
        SetRow(validation, cards.Count / columns); SetColumnSpan(validation, columns);
    }

    private void AddField(StackPanel parent, string key, string label, object initial, string unit = "", string? hint = null)
    {
        var row = new Grid { Margin = new(0, 2, 0, 2) };
        row.ColumnDefinitions.Add(new()); row.ColumnDefinitions.Add(new() { Width = new(72) }); row.ColumnDefinitions.Add(new() { Width = new(32) });
        row.RowDefinitions.Add(new() { Height = GridLength.Auto }); row.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var box = new TextBox { Text = Convert.ToString(initial, CultureInfo.CurrentCulture), MinHeight = 30,
            HorizontalContentAlignment = HorizontalAlignment.Right, ToolTip = hint ?? label };
        AutomationProperties.SetName(box, label + (unit.Length > 0 ? $" ({unit})" : ""));
        var caption = Ui.Text(label, 13); caption.VerticalAlignment = VerticalAlignment.Center; caption.Margin = new(0, 0, 8, 0);
        var units = Ui.Text(unit, 13, Ui.Muted); units.VerticalAlignment = VerticalAlignment.Center; units.Margin = new(6, 0, 0, 0);
        var error = Ui.Text("", 12, Ui.Error); error.Visibility = Visibility.Collapsed;
        fields.Add(key, new(label, box, error));
        SetColumn(box, 1); SetColumn(units, 2); SetRow(error, 1); SetColumnSpan(error, 3);
        row.Children.Add(caption); row.Children.Add(box); row.Children.Add(units); row.Children.Add(error);
        int footer = parent.Children.Count > 1 && parent.Children[parent.Children.Count - 1] is TextBlock ? parent.Children.Count - 1 : parent.Children.Count;
        parent.Children.Insert(footer, row);
        box.TextChanged += (_, _) => Changed?.Invoke();
    }

    public CaptureSettings Read(SourceGeometry? geometry = null)
    {
        validation.Visibility = Visibility.Collapsed;
        foreach (var field in fields.Values) { field.Input.ClearValue(Control.BorderBrushProperty); field.Error.Visibility = Visibility.Collapsed; }
        int Count(string key) => int.TryParse(fields[key].Input.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out int value)
            ? value : throw new CalibrationException($"{fields[key].Label} must be a whole number.", key);
        double Number(string key) => double.TryParse(fields[key].Input.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out double value)
            ? value : throw new CalibrationException($"{fields[key].Label} must be a number.", key);
        try
        {
            var value = new CaptureSettings
            {
                RowCount = Count(nameof(CaptureSettings.RowCount)), ColumnCount = Count(nameof(CaptureSettings.ColumnCount)),
                GridInsetMm = Number(nameof(CaptureSettings.GridInsetMm)), AzimuthCount = Count(nameof(CaptureSettings.AzimuthCount)),
                NonUprightAltitudeCount = Count(nameof(CaptureSettings.NonUprightAltitudeCount)), MinAltitudeDeg = Number(nameof(CaptureSettings.MinAltitudeDeg)),
                AngleToleranceDeg = Number(nameof(CaptureSettings.AngleToleranceDeg)), CaptureWindowMs = Count(nameof(CaptureSettings.CaptureWindowMs)),
                MinSamplesPerCapture = Count(nameof(CaptureSettings.MinSamplesPerCapture)), PositionStabilityMm = Number(nameof(CaptureSettings.PositionStabilityMm)),
                TiltStabilityDeg = Number(nameof(CaptureSettings.TiltStabilityDeg)), ReportGapTimeoutMs = Count(nameof(CaptureSettings.ReportGapTimeoutMs)),
                CaptureTimeoutMs = Count(nameof(CaptureSettings.CaptureTimeoutMs))
            };
            value.Validate(geometry);
            return value;
        }
        catch (CalibrationException ex)
        {
            if (ex.ParameterName is { } key && fields.TryGetValue(key, out var field))
            { field.Input.BorderBrush = Ui.Error; field.Error.Text = ex.Message; field.Error.Visibility = Visibility.Visible; }
            else { validation.Text = ex.Message; validation.Visibility = Visibility.Visible; }
            throw;
        }
    }
}
