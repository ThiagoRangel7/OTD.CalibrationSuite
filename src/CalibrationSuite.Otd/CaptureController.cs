using System.Diagnostics;
using CalibrationSuite.Core;
using OpenTabletDriver.Configurations.Parsers.Huion;
using OpenTabletDriver.Plugin.Tablet;

namespace CalibrationSuite.Otd;

public static class CaptureDevice
{
    // Physical cardinal directions were reported by the user on 2026-09-13.
    // Do not generalize this projection to other OTD parsers or tablet models.
    public static bool Supported(TabletReference tablet) => tablet.Properties.Name == "Huion Kamvas 13 (Gen 3)"
        && tablet.Identifiers.Any(i => i.ReportParser == typeof(GianoReportParser).FullName);
    public static string ReportType => typeof(GianoReport).FullName!;
    public static RequestedPose Orientation(Point2 tilt, double tolerance) =>
        OrientationGuidance.FromAngularTilt(tilt, -1, 1, tolerance);
    public static SourceGeometry Geometry(TabletReference tablet)
    {
        var d = tablet.Properties.Specifications.Digitizer;
        return new(d.MaxX, d.MaxY, d.Width, d.Height);
    }
}

public sealed record CaptureProgress(int Index, int Captured, int Total, Point2 Target, RequestedPose Pose,
    bool Active, int BurstSamples, bool Complete, string Message)
{
    public CaptureBurstStatus BurstStatus { get; init; }
    public double BurstProgress { get; init; }
    public double BurstDurationMs { get; init; }
    public double? AcceptedAtMs { get; init; }
    public int? AcceptedIndex { get; init; }
}

// The lock only joins UI commands with the serialized acquisition consumer. WPF never samples a burst.
public sealed class CaptureController
{
    private readonly object sync = new();
    private readonly CaptureDraft draft;
    private readonly CaptureBurst burst;
    private readonly string? requiredReportType;
    private double? acceptedAt;
    private int? acceptedIndex;
    public static double Now => Stopwatch.GetTimestamp() * 1000.0 / Stopwatch.Frequency;

    public CaptureController(CaptureDraft draft, Func<Point2, RequestedPose> orientation, string? requiredReportType = null)
    {
        this.draft = draft; this.requiredReportType = requiredReportType;
        burst = new(draft.Settings, draft.Geometry, orientation);
    }

    public void Read(ReportReading reading)
    {
        lock (sync)
        {
            if (draft.Complete) return;
            double time = reading.Timestamp * 1000.0 / Stopwatch.Frequency;
            if (reading.Kind == ReadingKind.Auxiliary) { burst.CheckTimeout(time); return; }
            if (reading.Kind != ReadingKind.Paired || reading.Position == null || reading.Tilt == null)
            { burst.Reject(time, reading.Status); return; }
            if (requiredReportType != null && reading.ReportType != requiredReportType)
            { burst.Reject(time, "Orientation is not verified for this report type"); return; }
            var p = reading.Position.Value; var t = reading.Tilt.Value;
            var sample = burst.Add(time, new(p.X, p.Y), new(t.X, t.Y));
            if (sample == null) return;
            try
            {
                int index = draft.CurrentIndex;
                draft.Store(sample);
                acceptedAt = time; acceptedIndex = index;
            }
            catch (CalibrationException ex) { burst.Cancel(ex.Message, retry: true); }
        }
    }

    public bool Start(double now)
    {
        lock (sync) return !draft.Complete && burst.Start(draft.CurrentPose, now);
    }
    public void Previous()
    {
        lock (sync)
        {
            burst.Cancel("Previous pose — press Space to replace its measurement."); draft.Previous();
            acceptedAt = null; acceptedIndex = null;
        }
    }
    public void Restart()
    {
        lock (sync)
        {
            burst.Cancel("Capture restarted — press Space when ready."); draft.Restart();
            acceptedAt = null; acceptedIndex = null;
        }
    }
    public void Stop()
    {
        lock (sync) burst.Cancel("Burst cancelled. Completed measurements are preserved.");
    }
    public CaptureProgress Snapshot(double now)
    {
        lock (sync)
        {
            burst.CheckTimeout(now);
            return new(draft.CurrentIndex, draft.CapturedCount, draft.Total, draft.CurrentTarget, draft.CurrentPose,
                burst.Active, burst.Count, draft.Complete, burst.Message)
            {
                BurstStatus = burst.Status, BurstProgress = burst.Progress, BurstDurationMs = burst.DurationMs,
                AcceptedAtMs = acceptedAt, AcceptedIndex = acceptedIndex
            };
        }
    }
}
