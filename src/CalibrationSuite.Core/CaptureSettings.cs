namespace CalibrationSuite.Core;

public sealed record CaptureSettings
{
    public const int MaximumCaptures = 10000;
    public int RowCount { get; init; } = 3;
    public int ColumnCount { get; init; } = 3;
    public double GridInsetMm { get; init; } = 15;
    public int TiltDirections { get; init; } = 8;
    public int TiltLevels { get; init; } = 2;
    public double TiltRange { get; init; } = 40;
    public double TiltTolerance { get; init; } = 8;
    public int CaptureWindowMs { get; init; } = 200;
    public int MinSamplesPerCapture { get; init; } = 20;
    public double PositionStabilityMm { get; init; } = 0.5;
    public double TiltStability { get; init; } = 3;
    public int ReportGapTimeoutMs { get; init; } = 500;
    public int CaptureTimeoutMs { get; init; } = 2000;
    private bool HasTiltPoses => TiltDirections > 0 && TiltLevels > 0;

    public int PoseCount => HasTiltPoses ? 1 + TiltDirections * TiltLevels : 1;
    public int TotalCaptures => RowCount * ColumnCount * PoseCount;

    public void Validate(SourceGeometry? geometry = null)
    {
        static void Require(bool condition, string property, string message)
        {
            if (!condition) throw new CalibrationException(message, property);
        }

        Require(RowCount >= 2, nameof(RowCount), "Use at least 2 rows.");
        Require(ColumnCount >= 2, nameof(ColumnCount), "Use at least 2 columns.");
        Require(TiltDirections >= 0, nameof(TiltDirections), "Use 0 or more tilt directions.");
        Require(TiltLevels >= 0, nameof(TiltLevels), "Use 0 or more tilt levels.");
        Require(!HasTiltPoses || TiltDirections >= 3, nameof(TiltDirections), "Tilt capture needs at least 3 directions.");
        RequirePositive(GridInsetMm, nameof(GridInsetMm), "Grid inset");
        RequirePositive(TiltRange, nameof(TiltRange), "Tilt range");
        RequirePositive(TiltTolerance, nameof(TiltTolerance), "Tilt tolerance");
        RequirePositive(PositionStabilityMm, nameof(PositionStabilityMm), "Position stability");
        RequirePositive(TiltStability, nameof(TiltStability), "Tilt stability");
        Require(CaptureWindowMs > 0, nameof(CaptureWindowMs), "Burst duration must be positive.");
        Require(MinSamplesPerCapture > 0, nameof(MinSamplesPerCapture), "Minimum reports must be positive.");
        Require(ReportGapTimeoutMs > 0, nameof(ReportGapTimeoutMs), "Report gap timeout must be positive.");
        Require(CaptureTimeoutMs > CaptureWindowMs, nameof(CaptureTimeoutMs), "Capture timeout must exceed burst duration.");
        Require(!HasTiltPoses || TiltRange / TiltLevels > 2 * TiltTolerance,
            nameof(TiltLevels), "Tilt level spacing must exceed twice the tolerance; use fewer levels or a larger range.");
        // Divide the limit before multiplying counts, so even extreme input cannot overflow.
        long poses = HasTiltPoses ? 1L + (long)TiltDirections * TiltLevels : 1;
        if (poses > MaximumCaptures / RowCount / ColumnCount)
            throw new CalibrationException($"Use at most {MaximumCaptures:N0} captures per session.");
        if (geometry == null) return;
        geometry.Validate();
        if (GridInsetMm >= Math.Min(geometry.WidthMm, geometry.HeightMm) / 2)
            throw new CalibrationException("Grid inset must be less than half each digitizer dimension.", nameof(GridInsetMm));
    }

    private static void RequirePositive(double value, string property, string label)
    {
        if (!double.IsFinite(value) || value <= 0)
            throw new CalibrationException($"{label} must be positive and finite.", property);
    }

    public CalibrationGrid CreateGrid(SourceGeometry geometry)
    {
        Validate(geometry);
        static double[] Axis(int count, double inset, double maximum) => Enumerable.Range(0, count)
            .Select(i => inset + i / (count - 1.0) * (maximum - 2 * inset)).ToArray();
        return new(Axis(RowCount, GridInsetMm / geometry.HeightMm * geometry.MaxY, geometry.MaxY),
            Axis(ColumnCount, GridInsetMm / geometry.WidthMm * geometry.MaxX, geometry.MaxX));
    }

    public RequestedPose[] CreatePoses()
    {
        var poses = new RequestedPose[PoseCount];
        poses[0] = new(0);
        if (!HasTiltPoses) return poses;
        int index = 1;
        for (int azimuth = 0; azimuth < TiltDirections; azimuth++)
            for (int level = 1; level <= TiltLevels; level++)
                poses[index++] = new(level * TiltRange / TiltLevels,
                    360.0 * azimuth / TiltDirections);
        return poses;
    }
}
