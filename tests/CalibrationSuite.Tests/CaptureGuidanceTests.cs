using CalibrationSuite.Core;
using CalibrationSuite.Otd;
using Xunit;

namespace CalibrationSuite.Tests;

public class CaptureGuidanceTests
{
    [Fact]
    public void UprightGateAndLiveArrowShareMeasurementsWithDifferentAzimuthSuppression()
    {
        var pose = CaptureDevice.Orientation(new(1, 0), 8);
        var livePose = CaptureDevice.Orientation(new(1, 0), 0);
        Assert.Null(pose.AzimuthDeg);
        Assert.Equal(0, livePose.AzimuthDeg);
        Assert.Equal(pose.AltitudeDeg, livePose.AltitudeDeg);
        Assert.NotEqual(default, OrientationScene.ArrowTip(livePose));
    }

    [Theory]
    [InlineData(70, true, CaptureCue.Ready)]
    [InlineData(50, false, CaptureCue.Adjusting)]
    public void AcceptedFeedbackUsesTheNextRequestedPoseForCrosshairReadiness(double altitude, bool ready, CaptureCue guideCue)
    {
        var state = new CaptureProgress(1, 1, 20, default, new(70, 0), false, 0, false, "Captured.")
        { AcceptedAtMs = 1000, AcceptedIndex = 0 };
        var feedback = CaptureFeedback.Resolve(state, new(altitude, 0), 8, 1010);
        Assert.Equal(CaptureCue.Accepted, feedback.Cue);
        Assert.Equal(ready, feedback.PoseReady);
        Assert.Equal(guideCue, feedback.GuideCue);
        Assert.False(feedback.ReadyToRetry);
    }

    [Theory]
    [InlineData(70, true, "Ready to retry")]
    [InlineData(50, false, "Try again")]
    public void RetryReadinessAndHeadingComeFromTheSamePoseGate(double altitude, bool ready, string heading)
    {
        var state = new CaptureProgress(1, 1, 20, default, new(70, 0), false, 0, false, "Pose left the angle tolerance")
        { BurstStatus = CaptureBurstStatus.Retry };
        var feedback = CaptureFeedback.Resolve(state, new(altitude, 0), 8, 1000);
        Assert.Equal(CaptureCue.Retry, feedback.Cue);
        Assert.Equal(ready, feedback.ReadyToRetry);
        Assert.Equal(heading, feedback.Heading);
        Assert.Equal(CaptureCue.Retry, feedback.GuideCue);
    }
}
