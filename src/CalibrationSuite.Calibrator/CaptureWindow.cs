using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using CalibrationSuite.Otd;
using CalibrationSuite.Core;

namespace CalibrationSuite.Calibrator;

public sealed class CaptureWindow : Window
{
    private readonly CaptureView view;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(33) };
    private readonly AcquisitionSession acquisition;
    private readonly CaptureController capture;
    private bool positioned;
    public event Action? GeometryChanged;

    public CaptureWindow(AcquisitionSession acquisition, CaptureController capture, CaptureDraft draft, string tabletName, DisplayGeometry display)
    {
        this.acquisition = acquisition; this.capture = capture;
        SetResourceReference(StyleProperty, typeof(Window)); Background = Ui.Brush("#101A28");
        Title = "Calibration Suite — capture"; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        view = new(draft.Settings, draft.Geometry, tabletName); Content = view;
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
    internal static string Pose(RequestedPose value) => $"altitude {value.AltitudeDeg:F1}° · azimuth {(value.AzimuthDeg is { } a ? $"{a:F1}°" : "— (upright)")}";

    private void Render()
    {
        double now = CaptureController.Now;
        var state = capture.Snapshot(now);
        if (state.Complete) { acquisition.Cancel("Capture complete. Review the candidate before accepting."); Close(); return; }
        if (!positioned) return;
        view.Update(state, acquisition.Latest, now);
    }
}
