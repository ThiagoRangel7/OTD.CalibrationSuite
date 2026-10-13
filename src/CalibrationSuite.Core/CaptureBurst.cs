namespace CalibrationSuite.Core;

public enum CaptureBurstStatus { Idle, Capturing, Accepted, Retry }

// Driven by monotonic ingress times, in milliseconds. The caller serializes commands/reports.
public sealed class CaptureBurst(CaptureSettings settings, SourceGeometry geometry)
{
    private readonly List<(Point2 Position, Point2 Tilt)> samples = [];
    private double[] scratch = [];
    private Point2? requested;
    private double started, first, last;
    public CaptureBurstStatus Status { get; private set; }
    public bool Active => Status == CaptureBurstStatus.Capturing;
    public int Count => samples.Count;
    public double DurationMs => samples.Count == 0 ? 0 : Math.Max(0, last - first);
    public double Progress => Math.Min(1, Math.Min((double)Count / settings.MinSamplesPerCapture, DurationMs / settings.CaptureWindowMs));
    public string Message { get; private set; } = "Aim with the physical nib, hold the requested pose, then press Space.";

    public bool Start(Point2? tiltTarget, double now)
    {
        if (Active) return false;
        requested = tiltTarget; started = first = last = now; samples.Clear(); Status = CaptureBurstStatus.Capturing;
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
        if (requested is { } target && !OrientationGuidance.WithinTolerance(tilt, target, settings.TiltTolerance))
        { Cancel("Pose left the tilt tolerance — hold the requested pose and retry.", retry: true); return null; }
        if (samples.Count == 0) first = time;
        last = time;
        samples.Add((position, tilt)); // Identical events still count.
        if (time - first < settings.CaptureWindowMs || samples.Count < settings.MinSamplesPerCapture) return null;

        // One reusable buffer serves all four medians and both percentiles.
        // Sort a copy of each coordinate; every original report still contributes.
        if (scratch.Length < samples.Count) scratch = new double[samples.Capacity];
        var p = new Point2(Median(static s => s.Position.X), Median(static s => s.Position.Y));
        var t = new Point2(Median(static s => s.Tilt.X), Median(static s => s.Tilt.Y));
        double position95 = Deviation95(p, position: true), tilt95 = Deviation95(t, position: false);
        if (!double.IsFinite(position95) || !double.IsFinite(tilt95)
            || position95 > settings.PositionStabilityMm || tilt95 > settings.TiltStability)
        {
            Cancel($"Movement too large (95% position {position95:F2} mm, tilt {tilt95:F2} reported units). Hold still and retry.", retry: true);
            return null;
        }
        Status = CaptureBurstStatus.Accepted; Message = "Captured.";
        return new(p, t);
    }

    private double Median(Func<(Point2 Position, Point2 Tilt), double> coordinate)
    {
        for (int i = 0; i < samples.Count; i++) scratch[i] = coordinate(samples[i]);
        Array.Sort(scratch, 0, samples.Count);
        int middle = samples.Count / 2;
        return samples.Count % 2 == 1 ? scratch[middle] : scratch[middle - 1] / 2 + scratch[middle] / 2;
    }

    private double Deviation95(Point2 median, bool position)
    {
        for (int i = 0; i < samples.Count; i++)
        {
            var delta = (position ? samples[i].Position : samples[i].Tilt) - median;
            if (position) delta = new(delta.X * geometry.WidthMm / geometry.MaxX, delta.Y * geometry.HeightMm / geometry.MaxY);
            scratch[i] = Math.Sqrt(Point2.Dot(delta, delta));
        }
        Array.Sort(scratch, 0, samples.Count);
        return scratch[(int)Math.Ceiling(0.95 * samples.Count) - 1];
    }
}
