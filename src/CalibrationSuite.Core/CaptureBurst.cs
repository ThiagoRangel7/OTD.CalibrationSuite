namespace CalibrationSuite.Core;

public enum CaptureBurstStatus { Idle, Capturing, Accepted, Retry }

// Driven by monotonic ingress times, in milliseconds. The caller serializes commands/reports.
public sealed class CaptureBurst(CaptureSettings settings, SourceGeometry geometry, Func<Point2, RequestedPose> orientation)
{
    private readonly List<(Point2 Position, Point2 Tilt)> samples = [];
    private RequestedPose requested = new(90);
    private double started, first, last;
    public CaptureBurstStatus Status { get; private set; }
    public bool Active => Status == CaptureBurstStatus.Capturing;
    public int Count => samples.Count;
    public double DurationMs => samples.Count == 0 ? 0 : Math.Max(0, last - first);
    public double Progress => Math.Min(1, Math.Min((double)Count / settings.MinSamplesPerCapture, DurationMs / settings.CaptureWindowMs));
    public string Message { get; private set; } = "Aim with the physical nib, hold the requested pose, then press Space.";

    public bool Start(RequestedPose pose, double now)
    {
        if (Active) return false;
        requested = pose; started = first = last = now; samples.Clear(); Status = CaptureBurstStatus.Capturing;
        Message = "Capturing — hold still.";
        return true;
    }

    public void Cancel(string message, bool retry = false)
    { Status = retry ? CaptureBurstStatus.Retry : CaptureBurstStatus.Idle; samples.Clear(); Message = message; }

    public void Reject(double time, string message)
    {
        if (Active && time >= started) Cancel(message + " — press Space to retry.", retry: true);
    }

    public void CheckTimeout(double now)
    {
        if (!Active) return;
        if (now - last > settings.ReportGapTimeoutMs) Cancel("Report gap — return the pen and press Space to retry.", retry: true);
        else if (now - started > settings.CaptureTimeoutMs) Cancel("Capture timed out before duration and sample minima were met. Press Space to retry.", retry: true);
    }

    public CalibrationSample? Add(double time, Point2 position, Point2 tilt)
    {
        if (!Active || time < started) return null; // Reports queued before the key press cannot enter the burst.
        CheckTimeout(time);
        if (!Active) return null;
        if (!position.IsFinite || !tilt.IsFinite) { Cancel("Nonfinite pen reading — retry.", retry: true); return null; }
        RequestedPose current;
        try { current = orientation(tilt); }
        catch (CalibrationException ex) { Cancel(ex.Message, retry: true); return null; }
        if (!OrientationGuidance.WithinTolerance(current, requested, settings.AngleToleranceDeg))
        { Cancel("Pose left the angle tolerance — hold the requested pose and retry.", retry: true); return null; }
        if (samples.Count == 0) first = time;
        last = time;
        samples.Add((position, tilt)); // Identical events still count.
        if (time - first < settings.CaptureWindowMs || samples.Count < settings.MinSamplesPerCapture) return null;

        static double Median(IEnumerable<double> values)
        {
            var sorted = values.Order().ToArray();
            int middle = sorted.Length / 2;
            return sorted.Length % 2 == 1 ? sorted[middle] : sorted[middle - 1] / 2 + sorted[middle] / 2;
        }
        var p = new Point2(Median(samples.Select(s => s.Position.X)), Median(samples.Select(s => s.Position.Y)));
        var t = new Point2(Median(samples.Select(s => s.Tilt.X)), Median(samples.Select(s => s.Tilt.Y)));
        static double Length(Point2 v) => Math.Sqrt(Point2.Dot(v, v));
        int rank = (int)Math.Ceiling(0.95 * samples.Count) - 1;
        var position95 = samples.Select(s => Length(new((s.Position.X - p.X) * geometry.WidthMm / geometry.MaxX,
            (s.Position.Y - p.Y) * geometry.HeightMm / geometry.MaxY))).Order().ElementAt(rank);
        var tilt95 = samples.Select(s => Length(s.Tilt - t)).Order().ElementAt(rank);
        if (position95 > settings.PositionStabilityMm || tilt95 > settings.TiltStabilityDeg)
        {
            Cancel($"Movement too large (95% position {position95:F2} mm, tilt {tilt95:F2}°). Hold still and retry.", retry: true);
            return null;
        }
        Status = CaptureBurstStatus.Accepted; Message = "Captured.";
        return new(p, t, requested);
    }
}
