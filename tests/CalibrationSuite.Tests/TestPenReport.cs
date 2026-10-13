using System.Numerics;
using OpenTabletDriver.Plugin.Tablet;

namespace CalibrationSuite.Tests;

internal sealed class TestPenReport : IAbsolutePositionReport, ITiltReport, IProximityReport, IEraserReport
{
    public byte[] Raw { get; set; } = [];
    public Vector2 Position { get; set; }
    public Vector2 Tilt { get; set; }
    public bool NearProximity { get; set; }
    public uint HoverDistance { get; set; }
    public bool Eraser { get; set; }
}
