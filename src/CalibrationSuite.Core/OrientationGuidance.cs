namespace CalibrationSuite.Core;

public static class OrientationGuidance
{
    public static bool WithinTolerance(RequestedPose current, RequestedPose requested, double tolerance) =>
        Math.Abs(current.AltitudeDeg - requested.AltitudeDeg) <= tolerance
        && (requested.AzimuthDeg == null || current.AzimuthDeg is { } azimuth
            && AzimuthDistance(azimuth, requested.AzimuthDeg.Value) <= tolerance);

    // Input is already converted to screen-aligned tip-to-eraser tangents by a device-verified adapter.
    // This API deliberately makes no assumptions about OTD's raw tilt signs or axis order.
    public static RequestedPose FromScreenTangents(Point2 tangents, double uprightToleranceDeg)
    {
        if (!tangents.IsFinite || !double.IsFinite(uprightToleranceDeg) || uprightToleranceDeg < 0 || uprightToleranceDeg >= 90)
            throw new CalibrationException("Guidance requires finite screen tangents and an upright tolerance in [0, 90).");

        double altitude = Math.Atan2(1, Math.Sqrt(Point2.Dot(tangents, tangents))) * 180 / Math.PI;
        double? azimuth = 90 - altitude <= uprightToleranceDeg ? null
            : (Math.Atan2(tangents.Y, -tangents.X) * 180 / Math.PI + 360) % 360;
        return new(altitude, azimuth);
    }

    public static double AzimuthDistance(double a, double b) => Math.Abs(Math.IEEERemainder(a - b, 360));
}
