using System.Windows;
using System.Windows.Media;
using CalibrationSuite.Core;
using CalibrationSuite.Otd;

namespace CalibrationSuite.Calibrator;

// Reported-tilt guidance shares the physical nib target. Outlines have constant
// DIP widths even when the guide scales; report acquisition stays outside this view.
public sealed class OrientationGuide : FrameworkElement
{
    private static readonly Pen NibLine = Line("#F0F6FF", 1);
    private static readonly Pen LiveLine = Line("#73DEFF", 1);
    private static readonly Pen TargetLine = Line("#FFD18B", 1);
    private static readonly Pen ReadyLine = Line("#76E7C2", 1);
    private static readonly Pen ReadyRetryLine = Line("#76E7C2", 1, DashStyles.Dash);
    private static readonly Pen WaitingLine = Line("#91A0B5", 1, DashStyles.Dot);
    private static readonly Pen RetryLine = Line("#FFD18B", 1, DashStyles.Dash);
    private readonly MatrixTransform placement = new();
    private Geometry outline = Geometry.Empty;
    private Geometry? outlineStroke;
    private Point2? requested, current;
    private double tolerance = -1, scale = 1, progress;
    private CaptureCue cue;
    private bool readyToRetry;

    public Point2 Anchor { get; private set; }
    public double Radius { get; private set; }

    // Local feedback must clear the precision landmarks and the capture arc.
    public bool OverlapsGuidance(Rect bounds)
    {
        bounds.Inflate(4, 4);
        if (bounds.IntersectsWith(new Rect(Anchor.X - 28, Anchor.Y - 28, 56, 56))) return true;
        if (bounds.IntersectsWith(outline.Bounds))
        {
            var stroke = outlineStroke ??= outline.GetFlattenedPathGeometry(0.1, ToleranceType.Absolute).GetWidenedPathGeometry(TargetLine);
            if (stroke.FillContainsWithDetail(new RectangleGeometry(bounds)) != IntersectionDetail.Empty) return true;
        }
        if (current is not { } live) return false;
        var tip = OrientationScene.ArrowTip(live, scale) * Radius + Anchor;
        var arrowBounds = new Rect(new Point(Anchor.X, Anchor.Y), new Point(tip.X, tip.Y));
        arrowBounds.Inflate(2, 2);
        return bounds.IntersectsWith(arrowBounds);
    }

    public OrientationGuide() { IsHitTestVisible = false; ClipToBounds = true; }

    public void Place(Point2 anchor, double radius)
    {
        if (anchor == Anchor && radius == Radius) return;
        Anchor = anchor; Radius = radius;
        placement.Matrix = new(radius, 0, 0, radius, anchor.X, anchor.Y);
        outlineStroke = null;
        InvalidateVisual();
    }

    public void Update(Point2? next, Point2? live, double toleranceUnits, double tiltScale, CaptureCue nextCue, double burstProgress, bool retryReady = false)
    {
        if (next == requested && live == current && toleranceUnits == tolerance && tiltScale == scale && nextCue == cue && burstProgress == progress && retryReady == readyToRetry) return;
        if (next != requested || toleranceUnits != tolerance || tiltScale != scale)
        {
            if (next is { } target)
            {
                var point = OrientationScene.ArrowTip(target, tiltScale);
                outline = new EllipseGeometry(new(point.X, point.Y), toleranceUnits / tiltScale, toleranceUnits / tiltScale) { Transform = placement };
            }
            else outline = Geometry.Empty;
            outlineStroke = null;
        }
        requested = next; current = live; tolerance = toleranceUnits; scale = tiltScale; cue = nextCue; progress = burstProgress; readyToRetry = retryReady;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        var center = new Point(Anchor.X, Anchor.Y);
        Point End(Point2 offset)
        {
            var tip = OrientationScene.ArrowTip(offset, scale);
            return new(center.X + tip.X * Radius, center.Y + tip.Y * Radius);
        }

        var stateLine = cue switch
        {
            CaptureCue.Ready => ReadyLine,
            CaptureCue.Capturing => LiveLine,
            CaptureCue.Waiting => WaitingLine,
            CaptureCue.Retry when readyToRetry => ReadyRetryLine,
            CaptureCue.Retry => RetryLine,
            _ => TargetLine
        };
        // Keep the live tip distinct from the tolerance boundary in every state.
        dc.DrawGeometry(null, stateLine, outline);
        foreach (var (dx, dy) in new[] { (-1, 0), (1, 0), (0, -1), (0, 1) })
            dc.DrawLine(stateLine, new(center.X + dx * 7, center.Y + dy * 7), new(center.X + dx * 20, center.Y + dy * 20));

        // Small end caps distinguish readiness by shape as well as color.
        if (cue == CaptureCue.Ready || cue == CaptureCue.Retry && readyToRetry)
            foreach (var (dx, dy) in new[] { (-1, 0), (1, 0), (0, -1), (0, 1) })
                dc.DrawLine(ReadyLine, new(center.X + dx * 20 - dy * 3, center.Y + dy * 20 + dx * 3),
                    new(center.X + dx * 20 + dy * 3, center.Y + dy * 20 - dx * 3));

        if (cue == CaptureCue.Capturing && progress > 0)
        {
            double angle = Math.Min(progress, 0.9999) * Math.PI * 2;
            var arc = new StreamGeometry();
            using (var path = arc.Open())
            {
                path.BeginFigure(new(center.X, center.Y - 24), false, false);
                path.ArcTo(new(center.X + 24 * Math.Sin(angle), center.Y - 24 * Math.Cos(angle)), new(24, 24),
                    0, angle > Math.PI, SweepDirection.Clockwise, true, false);
            }
            dc.DrawGeometry(null, LiveLine, arc);
        }
        if (current is { } live)
        {
            var tip = End(live); var direction = tip - center;
            dc.DrawLine(LiveLine, center, tip);
            if (direction.Length >= 6)
            {
                direction.Normalize();
                var tail = tip - direction * 4; var side = new Vector(-direction.Y, direction.X) * 2;
                dc.DrawLine(LiveLine, tail + side, tip); dc.DrawLine(LiveLine, tail - side, tip);
            }
            else dc.DrawEllipse(null, LiveLine, tip, 1, 1);
        }
        dc.DrawEllipse(null, NibLine, center, 2, 2);
    }

    private static Pen Line(string color, double width, DashStyle? dash = null)
    {
        var pen = new Pen(Ui.Brush(color), width);
        if (dash != null) pen.DashStyle = dash;
        pen.Freeze(); return pen;
    }

}
