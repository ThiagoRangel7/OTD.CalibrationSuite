namespace CalibrationSuite.Core;

public static class OrientationGuidance
{
    // Guidance and sampling use the same distance in the tablet's reported coordinates.
    public static bool WithinTolerance(Point2 current, Point2 requested, double tolerance)
    {
        var delta = current - requested;
        return Point2.Dot(delta, delta) <= tolerance * tolerance;
    }
}
