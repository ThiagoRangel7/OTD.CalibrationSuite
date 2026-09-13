using System.Numerics;

namespace CalibrationSuite.Core;

public static class PreviewGeometry
{
    public static bool Finite(Vector2 value) => float.IsFinite(value.X) && float.IsFinite(value.Y);

    public static Vector2[] Targets(float widthMm, float heightMm, float maxX, float maxY)
    {
        try
        {
            var grid = new CaptureSettings().CreateGrid(new(maxX, maxY, widthMm, heightMm));
            return grid.PreTransformRowCoordinates.SelectMany(y => grid.PreTransformColumnCoordinates
                .Select(x => new Vector2((float)x, (float)y))).ToArray();
        }
        catch (CalibrationException ex) { throw new InvalidOperationException(ex.Message, ex); }
    }

    // The full digitizer covers the full preview. WPF supplies viewport dimensions in DIPs.
    public static Vector2 ToDisplay(Vector2 rawTarget, Vector2 rawSize, Vector2 viewportSize) =>
        rawTarget / rawSize * viewportSize;
}
