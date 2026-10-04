using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using CalibrationSuite.Otd;
using CalibrationSuite.Core;

namespace CalibrationSuite.Calibrator;

public sealed class CaptureWindow : Window
{
    private readonly Canvas canvas = new() { IsHitTestVisible = false };
    private readonly TextBlock readings = new() { Foreground = Brushes.White, FontSize = 17, Margin = new(24),
        TextWrapping = TextWrapping.Wrap, IsHitTestVisible = false };
    private readonly Ellipse ring = new() { Width = 24, Height = 24, Stroke = Brushes.Orange, StrokeThickness = 2 };
    private readonly Line horizontal = new() { X1 = -18, X2 = 18, Stroke = Brushes.White, StrokeThickness = 1 };
    private readonly Line vertical = new() { Y1 = -18, Y2 = 18, Stroke = Brushes.White, StrokeThickness = 1 };
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(33) };
    private readonly AcquisitionSession acquisition;
    private readonly CaptureController capture;
    private readonly CaptureDraft draft;
    private readonly string tabletName;
    private bool positioned;
    public event Action? GeometryChanged;

    public CaptureWindow(AcquisitionSession acquisition, CaptureController capture, CaptureDraft draft, string tabletName, DisplayGeometry display)
    {
        this.acquisition = acquisition; this.capture = capture; this.draft = draft; this.tabletName = tabletName;
        Title = "Calibration Suite — capture"; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        Background = new SolidColorBrush(Color.FromRgb(20, 23, 28));
        var grid = new Grid(); grid.Children.Add(canvas); grid.Children.Add(readings); Content = grid;
        canvas.Children.Add(ring); canvas.Children.Add(horizontal); canvas.Children.Add(vertical);
        PreviewMouseDown += Ignore; PreviewMouseUp += Ignore; PreviewMouseMove += Ignore; PreviewMouseWheel += Ignore;
        PreviewStylusDown += Ignore; PreviewStylusUp += Ignore; PreviewStylusMove += Ignore; PreviewStylusSystemGesture += Ignore;
        PreviewTouchDown += Ignore; PreviewTouchUp += Ignore; PreviewTouchMove += Ignore;
        Stylus.SetIsPressAndHoldEnabled(this, false); Stylus.SetIsFlicksEnabled(this, false);
        Stylus.SetIsTapFeedbackEnabled(this, false); Stylus.SetIsTouchFeedbackEnabled(this, false);
        DpiChanged += (_, _) => { if (positioned) Dispatcher.BeginInvoke(() => GeometryChanged?.Invoke()); };
        SourceInitialized += (_, _) => display.Place(this);
        ContentRendered += (_, _) => { display.Place(this); positioned = true; Render(); timer.Start(); Activate(); Focus(); };
        SizeChanged += (_, _) => { if (positioned) Render(); };
        timer.Tick += (_, _) => Render();
        Closed += (_, _) => { timer.Stop(); capture.Stop(); acquisition.Cancel(); };
        PreviewKeyDown += (_, e) =>
        {
            e.Handled = true;
            if (e.IsRepeat) return;
            switch (e.Key)
            {
                case Key.Escape: Close(); return;
                case Key.Space: capture.Start(CaptureController.Now); break;
                case Key.Back: capture.Previous(); break;
                case Key.R: capture.Restart(); break;
            }
            Render();
        };
        PreviewKeyUp += (_, e) => e.Handled = true;
    }

    private static void Ignore(object? sender, InputEventArgs e) => e.Handled = true;
    private static string Pair(Vector2? value) => value is { } v ? $"({v.X:F1}; {v.Y:F1})" : "unavailable";
    internal static string Pose(RequestedPose value) => $"altitude {value.AltitudeDeg:F1}° · azimuth {(value.AzimuthDeg is { } a ? $"{a:F1}°" : "— (upright)")}";

    private void Render()
    {
        var state = capture.Snapshot(CaptureController.Now);
        if (state.Complete) { acquisition.Cancel("Capture complete. Review the candidate before accepting."); Close(); return; }
        if (!positioned) return;
        var target = new Vector2((float)state.Target.X, (float)state.Target.Y);
        if (canvas.ActualWidth <= 0 || canvas.ActualHeight <= 0) return;
        readings.FontSize = Math.Clamp(canvas.ActualHeight / 40, 11, 17);
        readings.Margin = new(canvas.ActualHeight < 600 ? 16 : 24);
        var local = PreviewGeometry.ToDisplay(target, new((float)draft.Geometry.MaxX, (float)draft.Geometry.MaxY), new((float)canvas.ActualWidth, (float)canvas.ActualHeight));
        Canvas.SetLeft(ring, local.X - 12); Canvas.SetTop(ring, local.Y - 12);
        Canvas.SetLeft(horizontal, local.X); Canvas.SetTop(horizontal, local.Y);
        Canvas.SetLeft(vertical, local.X); Canvas.SetTop(vertical, local.Y);
        readings.VerticalAlignment = local.Y < canvas.ActualHeight / 2 ? VerticalAlignment.Bottom : VerticalAlignment.Top;
        var latest = acquisition.Latest;
        string current = "unavailable", readiness = latest?.Status ?? "Waiting for pen — progress is preserved";
        if (latest is { Kind: ReadingKind.Paired, Tilt: { } tilt } && latest.ReportType == CaptureDevice.ReportType)
        {
            try
            {
                var pose = CaptureDevice.Orientation(new(tilt.X, tilt.Y), draft.Settings.AngleToleranceDeg);
                current = Pose(pose);
                readiness = OrientationGuidance.WithinTolerance(pose, state.Pose, draft.Settings.AngleToleranceDeg)
                    ? "Pose ready — aim with the physical nib and press Space" : "Adjust the pen to the requested pose";
            }
            catch (CalibrationException ex) { readiness = ex.Message; }
        }
        else if (latest is { Kind: ReadingKind.Paired }) readiness = "Orientation is not verified for this report type";
        ring.Stroke = state.Active ? Brushes.LimeGreen : Brushes.Orange;
        readings.Text = $"{tabletName} · {state.Captured}/{state.Total} measurements\n"
            + $"Target {state.Index / draft.Poses.Count + 1}/{draft.Settings.RowCount * draft.Settings.ColumnCount}, pose {state.Index % draft.Poses.Count + 1}/{draft.Poses.Count} · raw {Pair(target)}\n"
            + $"Requested: {Pose(state.Pose)}\nCurrent: {current}\n"
            + "Eraser direction: 0° left · 90° down · 180° right · 270° up\n"
            + $"Position: {Pair(latest?.Position)}   Tilt: {Pair(latest?.Tilt)}   Error: {Pair(latest?.Position - target)}\n"
            + $"{readiness}\n{state.Message}{(state.Active ? $" ({state.BurstSamples} reports)" : "")}\n\n"
            + "Space: capture · Backspace: previous pose · R: restart this capture · Esc: return and keep progress";
    }
}
