using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CalibrationSuite.Calibrator;

internal static class Ui
{
    private static readonly Dictionary<string, Brush> brushes = [];
    public static readonly Brush Ink = Brush("#203044");
    public static readonly Brush Muted = Brush("#52647A");
    public static readonly Brush Accent = Brush("#1768AB");
    public static readonly Brush Error = Brush("#A53228");
    public static Brush Brush(string color)
    {
        if (brushes.TryGetValue(color, out var cached))
            return cached;
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        brushes.Add(color, brush);
        return brush;
    }
    public static TextBlock Text(string text = "", double size = 14, Brush? color = null) => new()
    {
        Text = text,
        FontSize = size,
        Foreground = color ?? Ink,
        TextWrapping = TextWrapping.Wrap
    };
    public static TextBlock Heading(string text, double size = 16)
    {
        var value = Text(text, size);
        value.FontWeight = FontWeights.SemiBold;
        return value;
    }
    public static Border Card(UIElement content) => new()
    {
        Background = Brushes.White,
        BorderBrush = Brush("#DFE5EE"),
        BorderThickness = new(1),
        CornerRadius = new(10),
        Padding = new(14),
        Child = content
    };
    public static void Primary(Button button) => button.SetResourceReference(FrameworkElement.StyleProperty, "PrimaryButton");
}
