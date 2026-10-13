namespace CalibrationSuite.Core;

// A plot of reported tilt, not a physical pen-angle projection. X right, Y up.
public static class OrientationScene
{
    public static Point2 ArrowTip(Point2 offset, double scale) => new(offset.X / scale, -offset.Y / scale);

    public static Point2 PointAt(double radius, double azimuthDeg)
    {
        double azimuth = azimuthDeg * Math.PI / 180;
        return new(radius * Math.Cos(azimuth), radius * Math.Sin(azimuth));
    }

}

public readonly record struct OverlayBounds(double Left, double Top, double Width, double Height)
{
    public double DistanceTo(Point2 point)
    {
        double dx = Math.Max(Math.Max(Left - point.X, point.X - Left - Width), 0);
        double dy = Math.Max(Math.Max(Top - point.Y, point.Y - Top - Height), 0);
        return Math.Sqrt(dx * dx + dy * dy);
    }

    public bool Overlaps(OverlayBounds other, double gap) =>
        Left < other.Left + other.Width + gap && Left + Width + gap > other.Left
        && Top < other.Top + other.Height + gap && Top + Height + gap > other.Top;
}

public static class CaptureOverlay
{
    public const double MaxGuidanceRadius = 200;

    // Compact information cards leave room for the guide around a central nib.
    public static double Width(double viewportWidth, double margin) =>
        Math.Min(320, Math.Max(0, viewportWidth / 2 - margin - 80));

    // The guide stays anchored to the raw target. Only its scale changes to fit
    // the display and both cards; neither the target region nor its tolerance is inflated.
    public static double GuidanceRadius(double width, double height, Point2 target, OverlayBounds primary, OverlayBounds secondary)
    {
        double radius = Math.Min(MaxGuidanceRadius, Math.Min(target.X - 5, width - target.X - 5));
        radius = Math.Min(radius, Math.Min(target.Y - 5, height - target.Y - 5));
        radius = Math.Min(radius, Math.Min(primary.DistanceTo(target) - 12, secondary.DistanceTo(target) - 12));
        return Math.Max(0, radius);
    }

    public static OverlayBounds Place(double width, double height, Point2 target,
        double panelWidth, double panelHeight, double margin, OverlayBounds? avoid = null)
    {
        double right = Math.Max(margin, width - panelWidth - margin), bottom = Math.Max(margin, height - panelHeight - margin);
        var best = new OverlayBounds(0, 0, panelWidth, panelHeight);
        double distance = double.NegativeInfinity;
        ReadOnlySpan<Point2> corners = stackalloc Point2[]
        { new(margin, margin), new(right, margin), new(margin, bottom), new(right, bottom) };
        foreach (var corner in corners)
        {
            var bounds = new OverlayBounds(corner.X, corner.Y, panelWidth, panelHeight);
            if (avoid is { } occupied && bounds.Overlaps(occupied, 8)) continue;
            double nextDistance = bounds.DistanceTo(target);
            if (nextDistance <= distance) continue;
            best = bounds;
            distance = nextDistance;
        }
        return best;
    }
}
