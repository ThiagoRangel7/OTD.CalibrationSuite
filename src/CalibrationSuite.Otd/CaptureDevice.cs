using CalibrationSuite.Core;
using OpenTabletDriver.Configurations.Parsers.Huion;
using OpenTabletDriver.Plugin.Tablet;

namespace CalibrationSuite.Otd;

public static class CaptureDevice
{
    // Physical cardinal directions were reported by the user on 2026-09-13.
    // Do not generalize this projection to other OTD parsers or tablet models.
    public static bool Supported(TabletReference tablet) => tablet.Properties.Name == "Huion Kamvas 13 (Gen 3)"
        && tablet.Identifiers.Any(i => i.ReportParser == typeof(GianoReportParser).FullName);

    public static string ReportType => typeof(GianoReport).FullName!;

    public static RequestedPose Orientation(Point2 tilt, double tolerance)
    {
        if (!tilt.IsFinite || Math.Abs(tilt.X) >= 90 || Math.Abs(tilt.Y) >= 90)
            throw new CalibrationException("Tilt is outside the supported signed angular range (-90, 90).");
        return OrientationGuidance.FromScreenTangents(new(-Math.Tan(tilt.X * Math.PI / 180),
            Math.Tan(tilt.Y * Math.PI / 180)), tolerance);
    }

    public static SourceGeometry Geometry(TabletReference tablet)
    {
        var digitizer = tablet.Properties.Specifications.Digitizer;
        return new(digitizer.MaxX, digitizer.MaxY, digitizer.Width, digitizer.Height);
    }
}
