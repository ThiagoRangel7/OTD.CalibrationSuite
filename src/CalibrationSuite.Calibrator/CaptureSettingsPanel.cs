using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using CalibrationSuite.Core;

namespace CalibrationSuite.Calibrator;

public sealed class CaptureSettingsPanel : StackPanel
{
    private readonly Dictionary<string, TextBox> fields = [];
    public event Action? Changed;

    public CaptureSettingsPanel()
    {
        AddField(this, "Rows", "3"); AddField(this, "Columns", "3"); AddField(this, "Grid inset (mm)", "15");
        AddField(this, "Azimuth directions", "8"); AddField(this, "Non-upright altitude levels", "2");
        AddField(this, "Minimum altitude (degrees)", "50");
        Children.Add(new TextBlock { Text = "Set either pose count to 0 for upright-only calibration. Minimum altitude 50° allows 40° of lean.",
            TextWrapping = TextWrapping.Wrap, Margin = new(0, 6, 0, 8) });
        var advanced = new StackPanel();
        AddField(advanced, "Angle tolerance (degrees)", "3"); AddField(advanced, "Burst duration (ms)", "250");
        AddField(advanced, "Minimum reports", "20"); AddField(advanced, "Position stability (mm)", "0.25");
        AddField(advanced, "Tilt stability (degrees)", "2"); AddField(advanced, "Report gap timeout (ms)", "500");
        AddField(advanced, "Capture timeout (ms)", "2000");
        Children.Add(new Expander { Header = "Advanced capture settings", Content = advanced, Margin = new(0, 4, 0, 8) });
    }

    private void AddField(Panel parent, string label, string initial)
    {
        var row = new DockPanel { Margin = new(0, 2, 0, 2) };
        var box = new TextBox { Text = initial.Replace(".", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator),
            Width = 100, Padding = new(5), HorizontalContentAlignment = HorizontalAlignment.Right };
        fields.Add(label, box); DockPanel.SetDock(box, Dock.Right); row.Children.Add(box);
        row.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
        parent.Children.Add(row); box.TextChanged += (_, _) => Changed?.Invoke();
    }

    public CaptureSettings Read()
    {
        int Count(string key) => int.TryParse(fields[key].Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out int value)
            ? value : throw new CalibrationException($"{key} must be a whole number.");
        double Number(string key) => double.TryParse(fields[key].Text, NumberStyles.Float, CultureInfo.CurrentCulture, out double value)
            ? value : throw new CalibrationException($"{key} must be a number.");
        var settings = new CaptureSettings
        {
            RowCount = Count("Rows"), ColumnCount = Count("Columns"), GridInsetMm = Number("Grid inset (mm)"),
            AzimuthCount = Count("Azimuth directions"), NonUprightAltitudeCount = Count("Non-upright altitude levels"),
            MinAltitudeDeg = Number("Minimum altitude (degrees)"), AngleToleranceDeg = Number("Angle tolerance (degrees)"),
            CaptureWindowMs = Count("Burst duration (ms)"), MinSamplesPerCapture = Count("Minimum reports"),
            PositionStabilityMm = Number("Position stability (mm)"), TiltStabilityDeg = Number("Tilt stability (degrees)"),
            ReportGapTimeoutMs = Count("Report gap timeout (ms)"), CaptureTimeoutMs = Count("Capture timeout (ms)")
        };
        settings.Validate(); return settings;
    }
}
