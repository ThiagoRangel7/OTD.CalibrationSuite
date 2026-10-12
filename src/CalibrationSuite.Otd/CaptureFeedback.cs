using CalibrationSuite.Core;

namespace CalibrationSuite.Otd;

public enum CaptureCue { Waiting, Adjusting, Ready, Capturing, Accepted, Retry }

public sealed record CaptureFeedback(CaptureCue Cue, string Symbol, string Heading, string Hint)
{
    public static CaptureFeedback Resolve(CaptureProgress state, RequestedPose? current, double tolerance, double now, string? unavailable = null)
    {
        if (state.Active) return new(CaptureCue.Capturing, "◷", "Capturing", "Hold the nib and pen still");
        if (state.BurstStatus == CaptureBurstStatus.Retry)
            return new(CaptureCue.Retry, "↻", "Try again", ShortReason(state.Message));
        if (state.AcceptedAtMs is { } accepted && now >= accepted && now - accepted < 850)
            return new(CaptureCue.Accepted, "✓", "Captured", "Next pose · Space when ready");
        if (current == null) return new(CaptureCue.Waiting, "…", "Waiting for pen", unavailable == null ? "Return the nib · progress is kept" : ShortReason(unavailable));
        bool ready = OrientationGuidance.WithinTolerance(current, state.Pose, tolerance);
        return ready ? new(CaptureCue.Ready, "◎", "Pose ready", "Nib on crosshair · Space to capture")
            : new(CaptureCue.Adjusting, "↗", "Adjust the pen", "Arrow tip inside the amber outline");
    }

    private static string ShortReason(string reason) => reason switch
    {
        _ when reason.StartsWith("Report gap", StringComparison.Ordinal) => "Report gap · return the pen, then Space",
        _ when reason.StartsWith("Capture timed out", StringComparison.Ordinal) => "Too few reports or too little time · Space retries",
        _ when reason.StartsWith("Pose left", StringComparison.Ordinal) => "Pose moved outside tolerance · Space retries",
        _ when reason.StartsWith("Movement too large", StringComparison.Ordinal) => "Too much movement · hold still, then Space",
        _ when reason.StartsWith("Measured tilt is within", StringComparison.Ordinal) || reason.StartsWith("Poses ", StringComparison.Ordinal)
            || reason.StartsWith("Duplicate measured", StringComparison.Ordinal) => "Tilt poses overlap · Backspace to recapture",
        _ when reason.StartsWith("Measured tilt hull", StringComparison.Ordinal) => "Missing tilt coverage · Backspace to recapture",
        _ when reason.StartsWith("Captured upright pose", StringComparison.Ordinal) => "Upright outside coverage · revisit earlier poses",
        _ when reason.StartsWith("Tilt triangle", StringComparison.Ordinal) || reason.StartsWith("Tilt mesh", StringComparison.Ordinal)
            || reason.StartsWith("Unusable tilt geometry", StringComparison.Ordinal) => "Unusable tilt coverage · revisit earlier poses",
        _ when reason.StartsWith("Tilt is outside", StringComparison.Ordinal) => "Tilt outside ±90° · adjust the pen, then Space",
        _ when reason.StartsWith("Nonfinite", StringComparison.Ordinal) => "Invalid reading · return the pen, then Space",
        _ when reason.StartsWith("Eraser", StringComparison.Ordinal) => "Use the nib · Space retries",
        _ when reason.StartsWith("Proximity", StringComparison.Ordinal) || reason.StartsWith("Out of range", StringComparison.Ordinal) => "Pen left range · return it, then Space",
        _ => reason
    };
}
