namespace CalibrationSuite.Core;

public sealed class PreparedCalibration
{
    private readonly double[] rows, columns;
    private readonly NodeMesh[,] nodes;
    public bool IsIdentity => nodes.Length == 0;
    public bool RequiresTilt { get; }
    public static PreparedCalibration Identity { get; } = new([], [], new NodeMesh[0, 0], false);

    private PreparedCalibration(double[] rows, double[] columns, NodeMesh[,] nodes, bool requiresTilt)
    {
        this.rows = rows; this.columns = columns; this.nodes = nodes; RequiresTilt = requiresTilt;
    }

    public static PreparedCalibration Load(string? json, SourceGeometry geometry, out string? diagnostic)
    {
        diagnostic = null;
        if (string.IsNullOrWhiteSpace(json)) return Identity;
        try { return Prepare(CalibrationData.Parse(json), geometry); }
        catch (CalibrationException ex) { diagnostic = ex.Message; return Identity; }
    }

    public static PreparedCalibration Prepare(CalibrationData data, SourceGeometry geometry)
    {
        if (data.Mode == "identity") return Identity;
        if (data.Mode != "measured") throw new CalibrationException("Unsupported calibration mode.");
        if (data.SourceGeometry == null || data.Grid == null || data.Nodes == null)
            throw new CalibrationException("Measured data requires sourceGeometry, grid, and nodes.");
        geometry.Validate(); data.SourceGeometry.Validate();
        if (!data.SourceGeometry.Matches(geometry))
            throw new CalibrationException("Calibration geometry does not match this tablet; recalibration is required.");
        var rows = ValidateAxis(data.Grid.PreTransformRowCoordinates, data.SourceGeometry.MaxY, "row");
        var columns = ValidateAxis(data.Grid.PreTransformColumnCoordinates, data.SourceGeometry.MaxX, "column");
        if (data.Nodes.Length != rows.Length || data.Nodes.Any(row => row == null || row.Length != columns.Length))
            throw new CalibrationException("Node dimensions must match grid rows and columns.");
        var nodes = new NodeMesh[rows.Length, columns.Length];
        bool requiresTilt = false;
        for (int r = 0; r < rows.Length; r++)
            for (int c = 0; c < columns.Length; c++)
            {
                try
                {
                    var node = data.Nodes[r][c] ?? throw new CalibrationException("Missing node.");
                    nodes[r, c] = NodeMesh.Prepare(node.Samples, new(columns[c], rows[r]));
                    requiresTilt |= node.Samples.Length > 1;
                }
                catch (CalibrationException ex) { throw new CalibrationException($"Node [{r}][{c}]: {ex.Message}"); }
            }
        return new(rows, columns, nodes, requiresTilt);
    }

    private static double[] ValidateAxis(double[] values, double maximum, string name)
    {
        if (values == null || values.Length < 2) throw new CalibrationException($"Require at least two {name} coordinates.");
        for (int i = 0; i < values.Length; i++)
            if (!double.IsFinite(values[i]) || values[i] < 0 || values[i] > maximum || (i > 0 && values[i] <= values[i - 1]))
                throw new CalibrationException($"Grid {name} coordinates must be finite, strictly increasing, and within digitizer bounds.");
        return (double[])values.Clone();
    }

    public Point2 Correct(Point2 position, Point2 tilt)
    {
        if (IsIdentity || !position.IsFinite || RequiresTilt && !tilt.IsFinite) return position;
        if (!RequiresTilt) tilt = default;
        var (r, v) = Locate(rows, position.Y);
        var (c, u) = Locate(columns, position.X);
        var error = nodes[r, c].Evaluate(tilt) * ((1 - u) * (1 - v))
            + nodes[r, c + 1].Evaluate(tilt) * (u * (1 - v))
            + nodes[r + 1, c].Evaluate(tilt) * ((1 - u) * v)
            + nodes[r + 1, c + 1].Evaluate(tilt) * (u * v);
        var corrected = position - error;
        return corrected.IsFinite ? corrected : position;
    }

    private static (int Cell, double Fraction) Locate(double[] axis, double value)
    {
        value = Math.Clamp(value, axis[0], axis[^1]);
        int index = Array.BinarySearch(axis, value);
        int cell = Math.Clamp(index < 0 ? ~index - 1 : index, 0, axis.Length - 2);
        return (cell, (value - axis[cell]) / (axis[cell + 1] - axis[cell]));
    }
}
