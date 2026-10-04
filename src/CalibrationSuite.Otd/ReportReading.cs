using System.Numerics;
using CalibrationSuite.Core;
using OpenTabletDriver.Desktop.RPC;
using OpenTabletDriver.Plugin.Tablet;

namespace CalibrationSuite.Otd;

public enum ReadingKind { Paired, Auxiliary, Rejected }
public sealed record ReportReading(long Timestamp, Vector2? Position, Vector2? Tilt, uint? Pressure, string Status,
    ReadingKind Kind = ReadingKind.Paired, string? ReportType = null);

public static class ReportReadingFactory
{
    public static ReportReading Read(DebugReportData data, long timestamp)
    {
        object report;
        try { report = data.ToObject(); }
        catch (Exception ex) { throw new InvalidOperationException($"Cannot reconstruct OTD report '{data.Path}'. Check installed parser/plugins and OTD logs: {ex.Message}", ex); }
        if (report is not IDeviceReport deviceReport)
            throw new InvalidOperationException($"Reconstructed '{data.Path}' is not an OTD device report.");
        return Read(deviceReport, timestamp);
    }

    public static ReportReading Read(IDeviceReport report, long timestamp)
    {
        Vector2? position = report is IAbsolutePositionReport p ? p.Position : null;
        Vector2? tilt = report is ITiltReport t ? t.Tilt : null;
        uint? pressure = report is ITabletReport pen ? pen.Pressure : null;
        var (kind, status) = report switch
        {
            OutOfRangeReport => (ReadingKind.Rejected, "Out of range"),
            IProximityReport { NearProximity: false } => (ReadingKind.Rejected, "Proximity lost"),
            IEraserReport { Eraser: true } => (ReadingKind.Rejected, "Eraser — nib calibration unavailable"),
            _ when position == null && tilt == null => (ReadingKind.Auxiliary, "Auxiliary report — no paired position/tilt"),
            _ when position == null => (ReadingKind.Rejected, "Unsupported pen report — position missing"),
            _ when tilt == null => (ReadingKind.Rejected, "Unsupported pen report — tilt missing"),
            _ when !PreviewGeometry.Finite(position.Value) || !PreviewGeometry.Finite(tilt.Value) => (ReadingKind.Rejected, "Nonfinite pen reading"),
            _ => (ReadingKind.Paired, "Paired raw pen reading")
        };
        return new(timestamp, position, tilt, pressure, status, kind, report.GetType().FullName);
    }
}
