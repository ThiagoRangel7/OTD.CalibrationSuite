using System.Numerics;

namespace CalibrationSuite.Core;

public static class PreviewGeometry
{
    public static bool Finite(Vector2 value) => float.IsFinite(value.X) && float.IsFinite(value.Y);

    public static Vector2[] Targets(float widthMm, float heightMm, float maxX, float maxY)
    {
        if (!new[] { widthMm, heightMm, maxX, maxY }.All(v => float.IsFinite(v) && v > 0)
            || Math.Min(widthMm, heightMm) <= 30)
            throw new InvalidOperationException("Digitizer geometry must be finite and positive and support the 15 mm preview inset.");
        float x = 15 / widthMm * maxX, y = 15 / heightMm * maxY;
        return Enumerable.Range(0, 9).Select(i => new Vector2(
            x + (i % 3) / 2f * (maxX - 2 * x), y + (i / 3) / 2f * (maxY - 2 * y))).ToArray();
    }

    // The full digitizer covers the full preview. WPF supplies viewport dimensions in DIPs.
    public static Vector2 ToDisplay(Vector2 rawTarget, Vector2 rawSize, Vector2 viewportSize) =>
        rawTarget / rawSize * viewportSize;
}
