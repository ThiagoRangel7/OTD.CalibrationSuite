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
    public double AngleToleranceDeg { get; init; } = 8;
    public int CaptureWindowMs { get; init; } = 200;
    public int MinSamplesPerCapture { get; init; } = 20;
    public double PositionStabilityMm { get; init; } = 0.5;
    public double TiltStabilityDeg { get; init; } = 3;
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
        static void Require(bool condition, string property, string message)
        { if (!condition) throw new CalibrationException(message, property); }
        Require(RowCount >= 2, nameof(RowCount), "Use at least 2 rows.");
        Require(ColumnCount >= 2, nameof(ColumnCount), "Use at least 2 columns.");
        Require(AzimuthCount >= 0, nameof(AzimuthCount), "Use 0 or more azimuth directions.");
        Require(NonUprightAltitudeCount >= 0, nameof(NonUprightAltitudeCount), "Use 0 or more non-upright levels.");
        Require(!HasTiltPoses || AzimuthCount >= 3, nameof(AzimuthCount), "Tilt capture needs at least 3 azimuth directions.");
        foreach (var (value, property, label) in new[] { (GridInsetMm, nameof(GridInsetMm), "Grid inset"),
            (MinAltitudeDeg, nameof(MinAltitudeDeg), "Minimum altitude"), (AngleToleranceDeg, nameof(AngleToleranceDeg), "Angle tolerance"),
            (PositionStabilityMm, nameof(PositionStabilityMm), "Position stability"), (TiltStabilityDeg, nameof(TiltStabilityDeg), "Tilt stability") })
            Require(double.IsFinite(value) && value > 0, property, $"{label} must be positive and finite.");
        Require(MinAltitudeDeg < 90, nameof(MinAltitudeDeg), "Minimum altitude must be below 90°.");
        Require(AngleToleranceDeg < 90, nameof(AngleToleranceDeg), "Angle tolerance must be below 90°.");
        Require(CaptureWindowMs > 0, nameof(CaptureWindowMs), "Burst duration must be positive.");
        Require(MinSamplesPerCapture > 0, nameof(MinSamplesPerCapture), "Minimum reports must be positive.");
        Require(ReportGapTimeoutMs > 0, nameof(ReportGapTimeoutMs), "Report gap timeout must be positive.");
        Require(CaptureTimeoutMs > CaptureWindowMs, nameof(CaptureTimeoutMs), "Capture timeout must exceed burst duration.");
        Require(!HasTiltPoses || (90 - MinAltitudeDeg) / NonUprightAltitudeCount > 2 * AngleToleranceDeg,
            nameof(NonUprightAltitudeCount), "Altitude spacing must exceed twice the angle tolerance; use fewer levels or a lower minimum altitude.");
        Require(!HasTiltPoses || 360.0 / AzimuthCount > 2 * AngleToleranceDeg,
            nameof(AzimuthCount), "Azimuth spacing must exceed twice the angle tolerance; use fewer directions.");
        _ = TotalCaptures;
    }

    public CalibrationGrid CreateGrid(SourceGeometry geometry)
    {
        Validate(); geometry.Validate();
        if (GridInsetMm >= Math.Min(geometry.WidthMm, geometry.HeightMm) / 2)
            throw new CalibrationException("Grid inset must be less than half each digitizer dimension.", nameof(GridInsetMm));
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
