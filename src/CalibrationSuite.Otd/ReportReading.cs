using System.Numerics;
using CalibrationSuite.Core;
using OpenTabletDriver.Desktop.RPC;
using OpenTabletDriver.Plugin.Tablet;

namespace CalibrationSuite.Otd;

public sealed record ReportReading(long Timestamp, Vector2? Position, Vector2? Tilt, uint? Pressure, string Status);

public static class ReportReadingFactory
{
    public static ReportReading Read(DebugReportData data, long timestamp)
    {
        object report;
        try { report = data.ToObject(); }
        catch (Exception ex) { throw new InvalidOperationException($"Cannot reconstruct OTD report '{data.Path}'. Check installed parser/plugins and OTD logs: {ex.Message}", ex); }
        if (report is not IDeviceReport)
            throw new InvalidOperationException($"Reconstructed '{data.Path}' is not an OTD device report.");
        Vector2? position = report is IAbsolutePositionReport p ? p.Position : null;
        Vector2? tilt = report is ITiltReport t ? t.Tilt : null;
        uint? pressure = report is ITabletReport pen ? pen.Pressure : null;
        var status = report switch
        {
            OutOfRangeReport => "Out of range",
            IProximityReport { NearProximity: false } => "Proximity lost",
            IEraserReport { Eraser: true } => "Eraser — nib calibration unavailable",
            _ when position == null => "Auxiliary report — no paired position/tilt",
            _ when tilt == null => "Unsupported pen report — tilt missing",
            _ when !PreviewGeometry.Finite(position.Value) || !PreviewGeometry.Finite(tilt.Value) => "Nonfinite pen reading",
            _ => "Paired raw pen reading — angle semantics unverified"
        };
        return new(timestamp, position, tilt, pressure, status);
    }
}
