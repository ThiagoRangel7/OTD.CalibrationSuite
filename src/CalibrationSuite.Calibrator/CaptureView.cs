using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CalibrationSuite.Core;
using CalibrationSuite.Otd;

namespace CalibrationSuite.Calibrator;

// Presentation only: snapshots come from the acquisition consumer, never from UI input or animation.
public sealed class CaptureView : Grid
{
    private readonly CaptureSettings settings;
    private readonly SourceGeometry geometry;
    private readonly int poseCount;
    private readonly Canvas canvas = new();
    private readonly Border hud;
    private readonly Border details;
    private readonly StackPanel panel = new();
    private readonly OrientationGuide guide = new();
    private readonly StackPanel feedback = new() { Name = "CrosshairFeedback", Width = 200, Height = 64 };
    private readonly TextBlock address = Text("", 15), count = Text("", 12, true);
    private readonly TextBlock symbol = Text("", 20), heading = Text("", 14), hint = Text("", 12);
    private readonly TextBlock requested = Text("", 13), current = Text("", 13);
    private readonly TextBlock raw = Text("", 12, true), keys = Text("Space capture · Backspace previous\nR restart · Esc return / keep progress", 12);
    private readonly ProgressBar overall = new()
    {
        Height = 4,
        Minimum = 0,
        BorderThickness = new(0),
        Background = Ui.Brush("#3D5069"),
        Foreground = Ui.Brush("#76E7C2")
    };
    private CaptureProgress? snapshot;
    private Size layoutSize;
    private Point2 feedbackAnchor;
    private RequestedPose? feedbackPose;
    private Rect feedbackBounds = Rect.Empty;

    private static TextBlock Text(string value, double size, bool muted = false) => Ui.Text(value, size, Ui.Brush(muted ? "#BDCDDF" : "#F0F6FF"));

    public CaptureView(CaptureSettings settings, SourceGeometry geometry, string tabletName)
    {
        this.settings = settings; this.geometry = geometry; poseCount = settings.PoseCount;
        Background = Ui.Brush("#101A28"); IsHitTestVisible = false;
        Children.Add(canvas); canvas.Children.Add(guide);
        var summary = new StackPanel();
        var device = Text(tabletName, 11, true); device.TextWrapping = TextWrapping.NoWrap; device.TextTrimming = TextTrimming.CharacterEllipsis;
        summary.Children.Add(device); address.FontWeight = FontWeights.SemiBold; summary.Children.Add(address);
        count.Margin = new(0, 3, 0, 4); summary.Children.Add(count); summary.Children.Add(overall);
        panel.Children.Add(summary);
        var cueLine = new Grid();
        cueLine.ColumnDefinitions.Add(new() { Width = new(28) }); cueLine.ColumnDefinitions.Add(new());
        symbol.VerticalAlignment = VerticalAlignment.Center; cueLine.Children.Add(symbol);
        heading.FontWeight = FontWeights.SemiBold; heading.VerticalAlignment = VerticalAlignment.Center;
        heading.TextWrapping = TextWrapping.NoWrap; heading.TextTrimming = TextTrimming.CharacterEllipsis;
        SetColumn(heading, 1); cueLine.Children.Add(heading); feedback.Children.Add(cueLine);
        hint.Height = 34; hint.LineHeight = 16; hint.TextTrimming = TextTrimming.CharacterEllipsis;
        feedback.Children.Add(hint);
        canvas.Children.Add(feedback);
        var angles = new StackPanel { Margin = new(0, 2, 0, 6) };
        angles.Children.Add(requested); angles.Children.Add(current); panel.Children.Add(angles);
        var secondary = new StackPanel(); raw.Margin = new(0, 0, 0, 8); secondary.Children.Add(raw);
        keys.Foreground = Ui.Brush("#DDE9F8"); secondary.Children.Add(keys);
        hud = new Border { Name = "CaptureSummary", Child = panel, CornerRadius = new(12), Background = Ui.Brush("#1C2A3D"), BorderBrush = Ui.Brush("#40546F"), BorderThickness = new(1), Padding = new(16) };
        canvas.Children.Add(hud);
        details = new Border
        {
            Name = "CaptureDetails",
            Child = secondary,
            CornerRadius = new(10),
            Background = Ui.Brush("#1C2A3D"),
            BorderBrush = Ui.Brush("#40546F"),
            BorderThickness = new(1),
            Padding = new(12)
        };
        canvas.Children.Add(details);
        SizeChanged += (_, _) => Place();
    }

    public void Update(CaptureProgress state, ReportReading? latest, double now)
    {
        snapshot = state;
        RequestedPose? livePose = null;
        string? unavailable = latest?.Status;
        if (latest is { Kind: ReadingKind.Paired, Tilt: { } tilt } && latest.ReportType == CaptureDevice.ReportType)
        {
            try { livePose = CaptureDevice.Orientation(new(tilt.X, tilt.Y), 0); }
            catch (CalibrationException ex) { unavailable = ex.Message; }
        }
        else if (latest is { Kind: ReadingKind.Paired }) unavailable = "Orientation is unverified for this report";
        // Calculate direction once; only the gate/display suppresses near-upright azimuth.
        var pose = livePose is { } live && 90 - live.AltitudeDeg <= settings.AngleToleranceDeg
            ? live with { AzimuthDeg = null } : livePose;
        var cue = CaptureFeedback.Resolve(state, pose, settings.AngleToleranceDeg, now, unavailable);
        var color = Ui.Brush(cue.Cue switch
        {
            CaptureCue.Ready => "#76E7C2",
            CaptureCue.Capturing => "#73DEFF",
            CaptureCue.Accepted => "#EFFDF8",
            CaptureCue.Retry when cue.ReadyToRetry => "#76E7C2",
            CaptureCue.Retry or CaptureCue.Adjusting => "#FFD18B",
            _ => "#91A0B5"
        });
        symbol.Text = cue.Symbol; symbol.Foreground = color; heading.Text = cue.Heading; heading.Foreground = color; hint.Text = cue.Hint;
        if (cue.Cue == CaptureCue.Accepted && state.AcceptedIndex is { } acceptedIndex)
            hint.Text = $"Target {acceptedIndex / poseCount + 1} · pose {acceptedIndex % poseCount + 1} captured";
        address.Text = $"Target {state.Index / poseCount + 1} / {settings.RowCount * settings.ColumnCount} · Pose {state.Index % poseCount + 1} / {poseCount}";
        count.Text = $"{state.Captured} / {state.Total} measurements"; overall.Maximum = state.Total; overall.Value = state.Captured;
        requested.Text = "Requested: " + CompactPose(state.Pose); current.Text = "Current: " + (pose is { } angles ? CompactPose(angles) : "—");
        static Vector2? Finite(Vector2? p) => p is { } v && PreviewGeometry.Finite(v) ? v : null;
        static string Pair(Vector2? p) => p is { } v ? $"({v.X:F1}; {v.Y:F1})" : "—";
        var target = new Vector2((float)state.Target.X, (float)state.Target.Y);
        raw.Text = $"Target     {Pair(target)} raw\nPosition  {Pair(Finite(latest?.Position))} raw\nTilt          {Pair(Finite(latest?.Tilt))} °\nError       {Pair(Finite(latest?.Position) - target)} raw\nBurst       {state.BurstSamples} reports · {state.BurstDurationMs:F0} ms";
        guide.Update(state.Pose, livePose, settings.AngleToleranceDeg,
            cue.GuideCue, state.BurstProgress, cue.ReadyToRetry);
        Place();
    }

    private static string CompactPose(RequestedPose pose) => $"alt {pose.AltitudeDeg:F1}° · az {(pose.AzimuthDeg is { } a ? $"{a:F1}°" : "—")}";

    private void Place()
    {
        if (snapshot == null || ActualWidth <= 0 || ActualHeight <= 0) return;
        bool compact = ActualHeight < 560;
        double margin = compact ? 12 : 24;
        if (layoutSize != RenderSize)
        {
            layoutSize = RenderSize;
            hud.Width = CaptureOverlay.Width(ActualWidth, margin);
            hud.Padding = new(compact ? 10 : 16);
            address.FontSize = compact ? 12 : 15;
            count.FontSize = compact ? 11 : 12;
            foreach (var text in new[] { raw, keys, requested, current }) text.FontSize = compact ? 11 : 13;
            raw.LineHeight = keys.LineHeight = compact ? 13 : 17;
            details.Width = hud.Width;
            details.Padding = new(compact ? 9 : 12);
            guide.Width = ActualWidth;
            guide.Height = ActualHeight;
        }
        var local = PreviewGeometry.ToDisplay(new((float)snapshot.Target.X, (float)snapshot.Target.Y),
            new((float)geometry.MaxX, (float)geometry.MaxY), new((float)ActualWidth, (float)ActualHeight));
        hud.Measure(new(hud.Width, double.PositiveInfinity));
        details.Measure(new(details.Width, double.PositiveInfinity));
        // Auto height lets WPF reuse measurement when text and viewport are unchanged.
        // Toggling Height through NaN every frame invalidated the whole card layout.
        double hudHeight = hud.DesiredSize.Height, detailsHeight = details.DesiredSize.Height;
        var target = new Point2(local.X, local.Y);
        var primary = CaptureOverlay.Place(ActualWidth, ActualHeight, target, hud.Width, hudHeight, margin);
        Canvas.SetLeft(hud, primary.Left); Canvas.SetTop(hud, primary.Top);
        var secondary = CaptureOverlay.Place(ActualWidth, ActualHeight, target, details.Width, detailsHeight, margin, primary);
        Canvas.SetLeft(details, secondary.Left); Canvas.SetTop(details, secondary.Top);
        guide.Place(target, CaptureOverlay.GuidanceRadius(ActualWidth, ActualHeight, target, primary, secondary));
        PlaceFeedback(target, snapshot.Pose, new(primary.Left, primary.Top, primary.Width, primary.Height),
            new(secondary.Left, secondary.Top, secondary.Width, secondary.Height));
    }

    private void PlaceFeedback(Point2 target, RequestedPose requestedPose, Rect primary, Rect secondary)
    {
        double width = feedback.Width, height = feedback.Height;
        var viewport = new Rect(4, 4, Math.Max(0, ActualWidth - 8), Math.Max(0, ActualHeight - 8));
        bool Clear(Rect bounds) => viewport.Contains(bounds) && !bounds.IntersectsWith(primary)
            && !bounds.IntersectsWith(secondary) && !guide.OverlapsGuidance(bounds);

        // Keep a valid placement while the pen moves; the caption should not jitter.
        if (target != feedbackAnchor || requestedPose != feedbackPose || !Clear(feedbackBounds))
        {
            var candidates = new List<Rect>();
            foreach (double gap in new[] { 36d, 52, 76 })
            {
                foreach (var (left, top) in new[]
                {
                    (target.X - width / 2, target.Y + gap),
                    (target.X - width / 2, target.Y - gap - height),
                    (target.X + gap, target.Y - height / 2),
                    (target.X - gap - width, target.Y - height / 2),
                    (target.X + gap, target.Y + gap),
                    (target.X - gap - width, target.Y + gap),
                    (target.X + gap, target.Y - gap - height),
                    (target.X - gap - width, target.Y - gap - height)
                })
                    candidates.Add(new(Math.Clamp(left, 4, Math.Max(4, ActualWidth - width - 4)),
                        Math.Clamp(top, 4, Math.Max(4, ActualHeight - height - 4)), width, height));
            }
            feedbackBounds = candidates.FirstOrDefault(Clear, candidates[0]);
            feedbackAnchor = target; feedbackPose = requestedPose;
        }
        Canvas.SetLeft(feedback, feedbackBounds.Left); Canvas.SetTop(feedback, feedbackBounds.Top);
    }
}
