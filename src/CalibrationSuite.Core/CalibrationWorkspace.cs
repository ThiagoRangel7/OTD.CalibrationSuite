namespace CalibrationSuite.Core;

// This state belongs to one app lifetime. OTD alone owns durable storage.
public sealed class CalibrationWorkspace
{
    private string? acceptedPayload;

    public CaptureDraft? Session { get; private set; }
    public CaptureDraft? Candidate { get; private set; }
    public CaptureDraft? Working { get; private set; }
    public bool HasSessionData => Session != null || Candidate != null || Working != null;

    public const string CopyReminder = "Paste into the intended OTD profile's Calibration data field and Save. Copying does not apply or save it in OTD.";
    public const string CloseReminder = "Closing loses all measurements in this app session. Accepted calibration is only durable after you paste it into OTD and Save. Close anyway?";

    public void Begin(CaptureSettings settings, SourceGeometry geometry)
    {
        var draft = new CaptureDraft(settings, geometry);
        Session = draft;
        Candidate = null;
    }

    public void Recapture(CaptureDraft source, int index, bool wholeNode) =>
        Session = source.Recapture(index, wholeNode);

    public void Finish()
    {
        var candidate = Session ?? throw new InvalidOperationException("No unfinished capture.");
        if (!candidate.Complete)
            throw new CalibrationException("Capture every requested pose before accepting.");
        Candidate = candidate;
        Session = null;
    }

    public void Accept()
    {
        if (Session != null)
            throw new InvalidOperationException("Finish or discard the unfinished capture before acceptance.");
        var candidate = Candidate ?? throw new InvalidOperationException("No candidate to accept.");

        acceptedPayload = candidate.ToData().ToJson();
        Working = candidate;
        Candidate = null;
    }

    public void Discard()
    {
        Session = null;
        Candidate = null;
    }

    public string CopyPayload() => acceptedPayload
        ?? throw new InvalidOperationException("Accept a calibration before copying.");
}
