using Xunit;
using CalibrationSuite.Core;

namespace CalibrationSuite.Tests;

// Fictional measurements; none of these fixtures should be installed on a tablet.
internal static class CoreFixtures
{
    public static readonly SourceGeometry Geometry = new(1000, 1000, 100, 100);
    public static readonly Point2[] Tilts = [new(-40, -40), new(-40, 40), new(0, 0), new(40, -40), new(40, 40)];

    public static CalibrationSample[] Samples(Point2 target, Func<Point2, Point2> error, Point2[]? tilts = null) =>
        (tilts ?? Tilts).Select(t => new CalibrationSample(target + error(t), t)).ToArray();

    public static CalibrationData Data(Func<Point2, Point2, Point2>? error = null,
        double[]? rows = null, double[]? columns = null)
    {
        error ??= (_, _) => new(2, 3);
        rows ??= [150, 850];
        columns ??= [150, 850];
        return new()
        {
            Mode = "measured",
            SourceGeometry = Geometry,
            Grid = new(rows, columns),
            Nodes = rows.Select(y => columns.Select(x =>
                new CalibrationNode(Samples(new(x, y), t => error(new(x, y), t)))).ToArray()).ToArray()
        };
    }

    public static void Near(Point2 expected, Point2 actual, double tolerance = 1e-9)
    {
        Assert.InRange(Math.Abs(expected.X - actual.X), 0, tolerance);
        Assert.InRange(Math.Abs(expected.Y - actual.Y), 0, tolerance);
    }
}
