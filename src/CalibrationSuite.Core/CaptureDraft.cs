namespace CalibrationSuite.Core;

public sealed class CaptureDraft
{
    private readonly CalibrationSample?[] samples;
    private readonly int[] itinerary;
    private int cursor;
    public CaptureSettings Settings { get; }
    public SourceGeometry Geometry { get; }
    public CalibrationGrid Grid { get; }
    public IReadOnlyList<RequestedPose> Poses { get; }
    public int Total => samples.Length;
    public int CapturedCount => samples.Count(s => s != null);
    public bool Complete => cursor == itinerary.Length;
    public int CurrentIndex => itinerary[Math.Min(cursor, itinerary.Length - 1)];
    public RequestedPose CurrentPose => Poses[CurrentIndex % Poses.Count];
    public Point2 CurrentTarget => Target(CurrentIndex);

    public CaptureDraft(CaptureSettings settings, SourceGeometry geometry)
    {
        Settings = settings; Geometry = geometry; Grid = settings.CreateGrid(geometry); Poses = settings.CreatePoses();
        samples = new CalibrationSample?[settings.TotalCaptures];
        itinerary = Enumerable.Range(0, samples.Length).ToArray();
    }

    private CaptureDraft(CaptureDraft source, int[] selected)
    {
        Settings = source.Settings; Geometry = source.Geometry; Grid = source.Grid; Poses = source.Poses;
        samples = (CalibrationSample?[])source.samples.Clone(); itinerary = selected;
        foreach (int i in selected) samples[i] = null;
    }

    public Point2 Target(int index) => new(Grid.PreTransformColumnCoordinates[index / Poses.Count % Settings.ColumnCount],
        Grid.PreTransformRowCoordinates[index / Poses.Count / Settings.ColumnCount]);
    public CalibrationSample? Sample(int index) => samples[index];

    public CaptureDraft Recapture(int index, bool wholeNode)
    {
        if (!Complete) throw new CalibrationException("Complete this capture before targeted recapture; use Previous while capturing.");
        if ((uint)index >= samples.Length) throw new ArgumentOutOfRangeException(nameof(index));
        int start = wholeNode ? index / Poses.Count * Poses.Count : index;
        return new(this, Enumerable.Range(start, wholeNode ? Poses.Count : 1).ToArray());
    }

    public void Previous() { if (cursor > 0) cursor--; }
    public void Restart()
    {
        foreach (int i in itinerary) samples[i] = null;
        cursor = 0;
    }

    public void Store(CalibrationSample sample)
    {
        if (Complete) throw new InvalidOperationException("Capture is already complete.");
        int index = CurrentIndex, nodeStart = index / Poses.Count * Poses.Count;
        var node = samples.Skip(nodeStart).Take(Poses.Count).ToArray();
        node[index % Poses.Count] = null;
        if (!NodeMesh.CanAddCapture(node.OfType<CalibrationSample>(), sample.Tilt))
            throw new CalibrationException("Measured tilt is within 0.1° of another pose. Recapture with distinct orientations.");
        sample = sample with { RequestedPose = CurrentPose };
        node[index % Poses.Count] = sample;
        if (node.All(s => s != null)) NodeMesh.CompleteCapture(node.OfType<CalibrationSample>().ToArray(), CurrentTarget, 0);
        samples[index] = sample;
        cursor++;
    }

    public CalibrationData ToData()
    {
        if (!Complete || samples.Any(s => s == null)) throw new CalibrationException("Capture every requested pose before accepting.");
        var nodes = new CalibrationNode[Settings.RowCount][];
        for (int row = 0; row < nodes.Length; row++)
        {
            nodes[row] = new CalibrationNode[Settings.ColumnCount];
            for (int col = 0; col < nodes[row].Length; col++)
            {
                int start = (row * Settings.ColumnCount + col) * Poses.Count;
                var node = samples.Skip(start).Take(Poses.Count).OfType<CalibrationSample>().ToArray();
                NodeMesh.CompleteCapture(node, Target(start), 0);
                nodes[row][col] = new(node);
            }
        }
        var data = new CalibrationData { Mode = "measured", SourceGeometry = Geometry,
            Grid = new((double[])Grid.PreTransformRowCoordinates.Clone(), (double[])Grid.PreTransformColumnCoordinates.Clone()), Nodes = nodes };
        _ = PreparedCalibration.Prepare(data, Geometry);
        return data;
    }
}

// This state belongs to one app lifetime. OTD alone owns durable storage.
public sealed class CalibrationWorkspace
{
    public CaptureDraft? Session { get; private set; }
    public CaptureDraft? Candidate { get; private set; }
    public CaptureDraft? Working { get; private set; }
    public bool HasSessionData => Session != null || Candidate != null || Working != null;
    public const string CopyReminder = "Paste into the intended OTD profile's Calibration data field and Save. Copying does not apply or save it in OTD.";
    public const string CloseReminder = "Closing loses all measurements in this app session. Accepted calibration is only durable after you paste it into OTD and Save. Close anyway?";

    public void Begin(CaptureSettings settings, SourceGeometry geometry)
    {
        var draft = new CaptureDraft(settings, geometry);
        Session = draft; Candidate = null;
    }
    public void Recapture(CaptureDraft source, int index, bool wholeNode) => Session = source.Recapture(index, wholeNode);
    public void Finish()
    {
        if (Session == null) throw new InvalidOperationException("No unfinished capture.");
        _ = Session.ToData();
        Candidate = Session; Session = null;
    }
    public void Accept()
    {
        if (Session != null) throw new InvalidOperationException("Finish or discard the unfinished capture before acceptance.");
        if (Candidate == null) throw new InvalidOperationException("No candidate to accept.");
        _ = Candidate.ToData();
        Working = Candidate; Candidate = null; Session = null;
    }
    public void Discard() { Session = null; Candidate = null; }
    public string CopyPayload() => Working?.ToData().ToJson() ?? throw new InvalidOperationException("Accept a calibration before copying.");
}
