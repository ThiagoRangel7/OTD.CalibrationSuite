using CalibrationSuite.Core;
using CalibrationSuite.Otd;
using Xunit;

namespace CalibrationSuite.Tests;

public class CaptureGuidanceTests
{
    private static ReportReading Reading(float x) => new(0, new(10, 20), new(x, -10), 0, "Pen");
    private static CaptureProgress State => new(1, 1, 20, default, new(20, 0), false, 0, false, "Captured.")
    { UsesTilt = true, ReferenceTilt = new(5, -10), RequestedTilt = new(25, -10), AcceptedAtMs = 1000, AcceptedIndex = 0 };

    [Theory]
    [InlineData(25, true, CaptureCue.Ready)]
    [InlineData(5, false, CaptureCue.Adjusting)]
    public void AcceptedFeedbackUsesTheNextRawTargetForReadiness(float x, bool ready, CaptureCue guideCue)
    {
        var feedback = CaptureFeedback.Resolve(State, Reading(x), 8, 1010);
        Assert.Equal(CaptureCue.Accepted, feedback.Cue);
        Assert.Equal(ready, feedback.PoseReady);
        Assert.Equal(guideCue, feedback.GuideCue);
    }

    [Theory]
    [InlineData(25, true, "Ready to retry")]
    [InlineData(5, false, "Try again")]
    public void RetryRetainsItsReasonAfterReturningToTheGate(float x, bool ready, string heading)
    {
        var feedback = CaptureFeedback.Resolve(State with { BurstStatus = CaptureBurstStatus.Retry, Message = "Pose left the tilt tolerance" }, Reading(x), 8, 1010);
        Assert.Equal(CaptureCue.Retry, feedback.Cue);
        Assert.Equal(ready, feedback.ReadyToRetry);
        Assert.Equal(heading, feedback.Heading);
        Assert.Contains("Pose", feedback.Hint);
    }

    [Fact]
    public void ReferenceAndPositionOnlyReadinessNeedAPenButNoZeroTilt()
    {
        var reference = State with { RequestedTilt = null, AcceptedAtMs = null };
        Assert.Equal("Set tilt reference", CaptureFeedback.Resolve(reference, Reading(75), 8, 0).Heading);
        Assert.Equal(CaptureCue.Waiting, CaptureFeedback.Resolve(reference, null, 8, 0).Cue);
        var positionOnly = reference with { UsesTilt = false };
        var feedback = CaptureFeedback.Resolve(positionOnly, Reading(0) with { Tilt = null }, 8, 0);
        Assert.True(feedback.PoseReady);
        Assert.Contains("upright", feedback.Hint);
        Assert.False(CaptureFeedback.Resolve(State, Reading(25) with { Tilt = null }, 8, 2000).PoseReady);
    }
}
