namespace CalibrationSuite.Core;

public sealed class CaptureDraft
{
    private readonly CalibrationGrid grid;
    private CalibrationSample?[] samples;
    private readonly int firstIndex;
    private int endIndex;
    private int cursor;
    public CaptureSettings Settings { get; private set; }
    public SourceGeometry Geometry { get; }
    public IReadOnlyList<RequestedPose> Poses { get; private set; }
    public bool UsesTilt => Poses.Count > 1;
    public Point2? ReferenceTilt => samples[CurrentIndex / Poses.Count * Poses.Count]?.Tilt;
    public Point2? RequestedTilt => UsesTilt && CurrentIndex % Poses.Count != 0 && ReferenceTilt is { } reference
        ? reference + CurrentPose.Offset : null;
    public int Total => samples.Length;
    public int CapturedCount { get; private set; }
    public bool Complete => cursor == endIndex;
    public int CurrentIndex => Math.Min(cursor, endIndex - 1);
    public RequestedPose CurrentPose => Poses[CurrentIndex % Poses.Count];
    public Point2 CurrentTarget => Target(CurrentIndex);

    public CaptureDraft(CaptureSettings settings, SourceGeometry geometry)
    {
        Settings = settings;
        Geometry = geometry;
        grid = settings.CreateGrid(geometry);
        Poses = Array.AsReadOnly(settings.CreatePoses());
        samples = new CalibrationSample?[settings.TotalCaptures];
        endIndex = samples.Length;
    }

    private CaptureDraft(CaptureDraft source, int firstIndex, int count)
    {
        Settings = source.Settings;
        Geometry = source.Geometry;
        grid = source.grid;
        Poses = source.Poses;
        samples = (CalibrationSample?[])source.samples.Clone();
        this.firstIndex = firstIndex;
        endIndex = firstIndex + count;
        CapturedCount = source.CapturedCount;
        Restart();
    }

    public Point2 Target(int index) => new(grid.PreTransformColumnCoordinates[index / Poses.Count % Settings.ColumnCount],
        grid.PreTransformRowCoordinates[index / Poses.Count / Settings.ColumnCount]);
    public CalibrationSample? Sample(int index) => samples[index];

    // Resolve report capability before the first measurement; retain all other settings.
    public void UsePositionOnly()
    {
        if (!UsesTilt || CapturedCount != 0) return;
        Settings = Settings with { TiltDirections = 0, TiltLevels = 0 };
        Poses = Array.AsReadOnly(Settings.CreatePoses());
        samples = new CalibrationSample?[Settings.TotalCaptures];
        endIndex = samples.Length;
    }

    public CaptureDraft Recapture(int index, bool wholeNode)
    {
        if (!Complete) throw new CalibrationException("Complete this capture before targeted recapture; use Previous while capturing.");
        if ((uint)index >= samples.Length) throw new ArgumentOutOfRangeException(nameof(index));
        int start = wholeNode ? index / Poses.Count * Poses.Count : index;
        return new(this, start, wholeNode ? Poses.Count : 1);
    }

    public void Previous()
    {
        if (cursor > firstIndex) cursor--;
    }

    public void Restart()
    {
        // Full capture and pose/node recapture each cover one contiguous range.
        for (int i = firstIndex; i < endIndex; i++)
        {
            if (samples[i] == null) continue;
            samples[i] = null;
            CapturedCount--;
        }
        cursor = firstIndex;
    }

    public void Store(CalibrationSample sample)
    {
        if (Complete) throw new InvalidOperationException("Capture is already complete.");

        int index = CurrentIndex;
        int nodeStart = index / Poses.Count * Poses.Count;
        int capturedInNode = 1;
        for (int i = nodeStart; i < nodeStart + Poses.Count; i++)
        {
            if (i == index || samples[i] is not { } existing) continue;
            if (existing.Tilt == sample.Tilt)
                throw new CalibrationException("Duplicate measured tilt. Recapture with distinct reported values.");
            capturedInNode++;
        }

        sample = sample with { RequestedPose = CurrentPose };
        if (capturedInNode == Poses.Count)
        {
            var node = new CalibrationSample[Poses.Count];
            Array.Copy(samples, nodeStart, node, 0, node.Length);
            node[index % Poses.Count] = sample;
            NodeMesh.Prepare(node, CurrentTarget);
        }

        // Commit only after every validation succeeds; failed replacements preserve the old sample.
        if (samples[index] == null) CapturedCount++;
        samples[index] = sample;
        cursor++;
    }

    public CalibrationData ToData()
    {
        if (!Complete) throw new CalibrationException("Capture every requested pose before accepting.");
        return new CalibrationData
        {
            Mode = "measured",
            SourceGeometry = Geometry,
            Grid = new((double[])grid.PreTransformRowCoordinates.Clone(), (double[])grid.PreTransformColumnCoordinates.Clone()),
            Nodes = samples.Cast<CalibrationSample>()
                .Chunk(Poses.Count).Select(node => new CalibrationNode(node))
                .Chunk(Settings.ColumnCount).ToArray()
        };
    }
}
