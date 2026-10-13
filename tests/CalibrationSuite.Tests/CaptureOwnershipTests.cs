using CalibrationSuite.Core;
using Xunit;

namespace CalibrationSuite.Tests;

public class CaptureOwnershipTests
{
    private static readonly SourceGeometry Geometry = new(20000, 10000, 200, 100);
    private static readonly CaptureSettings Settings = new() { RowCount = 2, ColumnCount = 2, TiltDirections = 0 };

    [Fact]
    public void LayoutViewsCannotChangeTargetsOrRequestedPoses()
    {
        var draft = new CaptureDraft(Settings, Geometry);
        var target = draft.CurrentTarget;
        Fill(draft);
        var grid = draft.ToData().Grid!;
        grid.PreTransformRowCoordinates[0] = double.NaN;
        grid.PreTransformColumnCoordinates[0] = -1;

        Assert.Equal(target, draft.Target(0));
        Assert.Throws<NotSupportedException>(() => ((IList<RequestedPose>)draft.Poses)[0] = new(10, 20));
        Assert.Equal(new RequestedPose(0), draft.CurrentPose);

        var recapture = draft.Recapture(0, false);
        Assert.Equal(target, recapture.CurrentTarget);
        Assert.Equal(new RequestedPose(0), recapture.CurrentPose);
    }

    [Fact]
    public void ExportedDataCannotChangeTheCapturedMeasurements()
    {
        var workspace = new CalibrationWorkspace();
        workspace.Begin(Settings, Geometry);
        var original = workspace.Session!;
        Fill(original);
        workspace.Finish();

        Assert.Same(original, workspace.Candidate);

        workspace.Accept();
        Assert.Same(original, workspace.Working);
        var exported = original.ToData();
        exported.Grid!.PreTransformRowCoordinates[0] = double.NaN;
        exported.Nodes![0][0].Samples[0] = new(new(999, 999), new(999, 999));
        Assert.Equal(workspace.CopyPayload(), original.ToData().ToJson());
    }

    [Fact]
    public void RecaptureEditsDoNotChangeCompletedResultsOrTheirAcceptedPayload()
    {
        var workspace = new CalibrationWorkspace();
        workspace.Begin(Settings, Geometry);
        Fill(workspace.Session!);
        workspace.Finish();
        workspace.Accept();
        var original = workspace.Working!;
        string accepted = workspace.CopyPayload();

        workspace.Recapture(original, 2, false);
        var recapture = workspace.Session!;
        Assert.Null(recapture.Sample(2));
        recapture.Store(new(recapture.CurrentTarget + new Point2(10, 20), default));
        workspace.Finish();

        Assert.Equal(original.Target(2), original.Sample(2)!.Position);
        Assert.Equal(accepted, workspace.CopyPayload());
        workspace.Accept();
        Assert.Equal(new Point2(10, 20), workspace.Working!.Sample(2)!.Position - original.Target(2));
    }

    [Fact]
    public void InvalidNewSettingsPreserveCandidateAndUnfinishedRecapture()
    {
        var workspace = new CalibrationWorkspace();
        workspace.Begin(Settings, Geometry);
        Fill(workspace.Session!);
        workspace.Finish();
        var candidate = workspace.Candidate!;
        workspace.Recapture(candidate, 1, false);
        var session = workspace.Session;

        Assert.Throws<CalibrationException>(() => workspace.Begin(Settings with { RowCount = 1 }, Geometry));

        Assert.Same(candidate, workspace.Candidate);
        Assert.Same(session, workspace.Session);
    }

    [Fact]
    public void ExtremeTiltCountsAreRejectedWhileUprightOnlyIgnoresUnusedDirections()
    {
        var settings = Settings with
        {
            TiltDirections = int.MaxValue,
            TiltLevels = 2,
            TiltTolerance = 1e-9
        };
        Assert.Throws<CalibrationException>(() => settings.Validate());
        (settings with
        {
            TiltLevels = 0
        }).Validate();
    }

    private static void Fill(CaptureDraft draft)
    {
        while (!draft.Complete)
            draft.Store(new(draft.CurrentTarget, default));
    }
}
