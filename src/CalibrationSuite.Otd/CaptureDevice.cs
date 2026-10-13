using CalibrationSuite.Core;
using OpenTabletDriver.Plugin.Tablet;

namespace CalibrationSuite.Otd;

public static class CaptureDevice
{
    public static SourceGeometry Geometry(TabletReference tablet)
    {
        var digitizer = tablet.Properties.Specifications.Digitizer;
        return new(digitizer.MaxX, digitizer.MaxY, digitizer.Width, digitizer.Height);
    }
}
