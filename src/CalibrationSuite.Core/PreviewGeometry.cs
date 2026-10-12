using System.Numerics;

namespace CalibrationSuite.Core;

public static class PreviewGeometry
{
    public static bool Finite(Vector2 value) => float.IsFinite(value.X) && float.IsFinite(value.Y);

    // The full digitizer covers the full preview. WPF supplies viewport dimensions in DIPs.
    public static Vector2 ToDisplay(Vector2 rawTarget, Vector2 rawSize, Vector2 viewportSize) =>
        rawTarget / rawSize * viewportSize;
}
