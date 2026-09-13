using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using CalibrationSuite.Otd;
using CalibrationSuite.Core;
using OpenTabletDriver.Plugin.Tablet;

namespace CalibrationSuite.Calibrator;

public sealed class PreviewWindow : Window
{
    private readonly Canvas canvas = new() { IsHitTestVisible = false };
    private readonly TextBlock readings = new() { Foreground = Brushes.White, FontSize = 18, Margin = new(24), IsHitTestVisible = false };
    private readonly Ellipse ring = new() { Width = 24, Height = 24, Stroke = Brushes.Orange, StrokeThickness = 2 };
    private readonly Line horizontal = new() { X1 = -18, X2 = 18, Stroke = Brushes.White, StrokeThickness = 1 };
    private readonly Line vertical = new() { Y1 = -18, Y2 = 18, Stroke = Brushes.White, StrokeThickness = 1 };
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(33) };
    private readonly AcquisitionSession session;
    private readonly Vector2[] targets;
    private readonly Vector2 rawSize;
    private readonly string tabletName;
    private int targetIndex;
    private bool positioned;
    public event Action? GeometryChanged;

    public PreviewWindow(AcquisitionSession session, TabletReference tablet, DisplayGeometry display)
    {
        this.session = session;
        var d = tablet.Properties.Specifications.Digitizer;
        targets = PreviewGeometry.Targets(d.Width, d.Height, d.MaxX, d.MaxY);
        rawSize = new(d.MaxX, d.MaxY); tabletName = tablet.Properties.Name;
        Title = "Calibration Suite — acquisition preview";
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        Background = new SolidColorBrush(Color.FromRgb(20, 23, 28));
        var grid = new Grid();
        grid.Children.Add(canvas); grid.Children.Add(readings); Content = grid;
        canvas.Children.Add(ring); canvas.Children.Add(horizontal); canvas.Children.Add(vertical);
        PreviewMouseDown += Ignore; PreviewMouseUp += Ignore; PreviewMouseMove += Ignore;
        PreviewMouseWheel += Ignore; PreviewStylusDown += Ignore; PreviewStylusUp += Ignore;
        PreviewStylusMove += Ignore; PreviewStylusSystemGesture += Ignore;
        PreviewTouchDown += Ignore; PreviewTouchUp += Ignore; PreviewTouchMove += Ignore;
        Stylus.SetIsPressAndHoldEnabled(this, false);
        Stylus.SetIsFlicksEnabled(this, false);
        Stylus.SetIsTapFeedbackEnabled(this, false);
        Stylus.SetIsTouchFeedbackEnabled(this, false);
        DpiChanged += (_, _) =>
        {
            if (positioned) Dispatcher.BeginInvoke(() => GeometryChanged?.Invoke());
        };
        SourceInitialized += (_, _) => display.Place(this);
        ContentRendered += (_, _) => { display.Place(this); positioned = true; ShowTarget(); timer.Start(); };
        SizeChanged += (_, _) => ShowTarget();
        timer.Tick += (_, _) => RenderReadings();
        Closed += (_, _) => { timer.Stop(); session.Cancel(); };
        PreviewKeyDown += (_, e) =>
        {
            e.Handled = true;
            if (e.IsRepeat) return;
            if (e.Key == Key.Escape) { Close(); return; }
            if (e.Key == Key.Right) targetIndex = Math.Min(targets.Length - 1, targetIndex + 1);
            if (e.Key == Key.Left) targetIndex = Math.Max(0, targetIndex - 1);
            if (e.Key == Key.R) targetIndex = 0;
            ShowTarget();
        };
    }

    private static void Ignore(object? sender, InputEventArgs e) => e.Handled = true;

    private void ShowTarget()
    {
        if (!positioned) return;
        var target = targets[targetIndex];
        var local = PreviewGeometry.ToDisplay(target, rawSize, new((float)ActualWidth, (float)ActualHeight));
        Canvas.SetLeft(ring, local.X - 12); Canvas.SetTop(ring, local.Y - 12);
        Canvas.SetLeft(horizontal, local.X); Canvas.SetTop(horizontal, local.Y);
        Canvas.SetLeft(vertical, local.X); Canvas.SetTop(vertical, local.Y);
        RenderReadings();
    }

    private void RenderReadings()
    {
        var target = targets[targetIndex];
        var latest = session.Latest;
        static string Pair(Vector2? value) => value is { } v ? $"{v.X:F1}, {v.Y:F1}" : "unavailable";
        readings.Text = $"{tabletName}\nTarget {targetIndex + 1}/9 · raw {Pair(target)}\n"
            + $"Position: {Pair(latest?.Position)}   Tilt: {Pair(latest?.Tilt)}\n"
            + $"Raw error: {Pair(latest?.Position - target)}   Pressure: {latest?.Pressure.ToString() ?? "unavailable"}\n"
            + $"{latest?.Status ?? "Waiting for pen — progress is preserved"}\n\n"
            + "← / →: inspect targets   R: first target   Esc: cancel\n"
            + "Preview only; no measurements saved. Angle guidance is unavailable until device semantics are verified.";
    }
}
