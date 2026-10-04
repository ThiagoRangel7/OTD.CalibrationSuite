namespace CalibrationSuite.Core;

public sealed record CaptureSettings
{
    public const int MaximumCaptures = 10000;
    public int RowCount { get; init; } = 3;
    public int ColumnCount { get; init; } = 3;
    public double GridInsetMm { get; init; } = 15;
    public int AzimuthCount { get; init; } = 8;
    public int NonUprightAltitudeCount { get; init; } = 2;
    public double MinAltitudeDeg { get; init; } = 50;
    public double AngleToleranceDeg { get; init; } = 3;
    public int CaptureWindowMs { get; init; } = 250;
    public int MinSamplesPerCapture { get; init; } = 20;
    public double PositionStabilityMm { get; init; } = 0.25;
    public double TiltStabilityDeg { get; init; } = 2;
    public int ReportGapTimeoutMs { get; init; } = 500;
    public int CaptureTimeoutMs { get; init; } = 2000;
    private bool HasTiltPoses => AzimuthCount > 0 && NonUprightAltitudeCount > 0;
    public int TotalCaptures
    {
        get
        {
            try
            {
                int count = checked(RowCount * ColumnCount * (HasTiltPoses ? checked(1 + AzimuthCount * NonUprightAltitudeCount) : 1));
                return count <= MaximumCaptures ? count : throw new CalibrationException($"Use at most {MaximumCaptures:N0} captures per session.");
            }
            catch (OverflowException) { throw new CalibrationException("Capture counts are too large."); }
        }
    }

    public void Validate()
    {
        if (RowCount < 2 || ColumnCount < 2 || AzimuthCount < 0 || NonUprightAltitudeCount < 0
            || (HasTiltPoses && AzimuthCount < 3))
            throw new CalibrationException("Require at least two rows/columns and nonnegative pose counts; tilt capture requires at least three azimuths.");
        if (!new[] { GridInsetMm, MinAltitudeDeg, AngleToleranceDeg, PositionStabilityMm, TiltStabilityDeg }
                .All(v => double.IsFinite(v) && v > 0)
            || CaptureWindowMs <= 0 || MinSamplesPerCapture <= 0 || ReportGapTimeoutMs <= 0
            || CaptureTimeoutMs <= CaptureWindowMs || MinAltitudeDeg >= 90 || AngleToleranceDeg >= 90)
            throw new CalibrationException("Capture parameters must be positive and finite, minimum altitude below 90, and timeout longer than the burst.");
        if (HasTiltPoses && ((90 - MinAltitudeDeg) / NonUprightAltitudeCount <= 2 * AngleToleranceDeg
            || 360.0 / AzimuthCount <= 2 * AngleToleranceDeg))
            throw new CalibrationException("Altitude and azimuth spacing must exceed twice the angle tolerance.");
        _ = TotalCaptures;
    }

    public CalibrationGrid CreateGrid(SourceGeometry geometry)
    {
        Validate(); geometry.Validate();
        if (GridInsetMm >= Math.Min(geometry.WidthMm, geometry.HeightMm) / 2)
            throw new CalibrationException("Grid inset must be less than half each digitizer dimension.");
        static double[] Axis(int count, double inset, double maximum) => Enumerable.Range(0, count)
            .Select(i => inset + i / (count - 1.0) * (maximum - 2 * inset)).ToArray();
        return new(Axis(RowCount, GridInsetMm / geometry.HeightMm * geometry.MaxY, geometry.MaxY),
            Axis(ColumnCount, GridInsetMm / geometry.WidthMm * geometry.MaxX, geometry.MaxX));
    }

    public RequestedPose[] CreatePoses()
    {
        Validate();
        var poses = new List<RequestedPose> { new(90) };
        if (!HasTiltPoses) return poses.ToArray();
        for (int azimuth = 0; azimuth < AzimuthCount; azimuth++)
            for (int level = 1; level <= NonUprightAltitudeCount; level++)
                poses.Add(new(90 - level * (90 - MinAltitudeDeg) / NonUprightAltitudeCount,
                    360.0 * azimuth / AzimuthCount));
        return poses.ToArray();
    }
}

public static class OrientationGuidance
{
    public static RequestedPose FromAngularTilt(Point2 tilt, double screenXSign, double screenYSign, double uprightToleranceDeg)
    {
        if (!tilt.IsFinite || Math.Abs(tilt.X) >= 90 || Math.Abs(tilt.Y) >= 90
            || Math.Abs(screenXSign) != 1 || Math.Abs(screenYSign) != 1)
            throw new CalibrationException("Tilt is outside the supported signed angular range (-90, 90).");
        return FromScreenTangents(new(screenXSign * Math.Tan(tilt.X * Math.PI / 180),
            screenYSign * Math.Tan(tilt.Y * Math.PI / 180)), uprightToleranceDeg);
    }

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
        return new(altitude, 90 - altitude <= uprightToleranceDeg ? null
            : (Math.Atan2(tangents.Y, -tangents.X) * 180 / Math.PI + 360) % 360);
    }

    public static double AzimuthDistance(double a, double b) => Math.Abs(Math.IEEERemainder(a - b, 360));
}
