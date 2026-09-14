using System.Numerics;
using CalibrationSuite.Core;
using OpenTabletDriver.Plugin;
using OpenTabletDriver.Plugin.Attributes;
using OpenTabletDriver.Plugin.DependencyInjection;
using OpenTabletDriver.Plugin.Output;
using OpenTabletDriver.Plugin.Tablet;

namespace CalibrationSuite.Filter;

[PluginName("Calibration Suite")]
public sealed class CalibrationFilter : IPositionedPipelineElement<IDeviceReport>
{
    private PreparedCalibration calibration = PreparedCalibration.Identity;
    private int unsupportedCount;

    [Property("Calibration data")]
    [DefaultPropertyValue("")]
    [ToolTip("Paste accepted calibration data from the Calibrator, then Save in OTD. Clear and Save to reset to identity; disable the filter to bypass while retaining data.")]
    public string CalibrationJson { get; set; } = "";

    [TabletReference]
    public TabletReference? Tablet { get; set; }

    [OnDependencyLoad]
    public void Initialize()
    {
        calibration = PreparedCalibration.Identity;
        unsupportedCount = 0;
        try
        {
            var digitizer = Tablet?.Properties.Specifications.Digitizer
                ?? throw new CalibrationException("Tablet digitizer geometry was not injected.");
            calibration = PreparedCalibration.Load(CalibrationJson,
                new(digitizer.MaxX, digitizer.MaxY, digitizer.Width, digitizer.Height), out var diagnostic);
            if (diagnostic != null) Log.Write("Calibration Suite", diagnostic + " Using identity.", LogLevel.Warning);
        }
        catch (Exception ex)
        {
            Log.Write("Calibration Suite", $"Initialization failed: {ex.Message} Using identity.", LogLevel.Error);
        }
    }

    public PipelinePosition Position => PipelinePosition.PreTransform;
    public event Action<IDeviceReport>? Emit;
    public void Consume(IDeviceReport report)
    {
        if (!calibration.IsIdentity
            && report is not OutOfRangeReport
            && report is not IProximityReport { NearProximity: false }
            && report is not IEraserReport { Eraser: true })
        {
            if (report is IAbsolutePositionReport position && report is ITiltReport tilt)
            {
                var raw = position.Position;
                var orientation = tilt.Tilt;
                if (float.IsFinite(raw.X) && float.IsFinite(raw.Y)
                    && float.IsFinite(orientation.X) && float.IsFinite(orientation.Y))
                {
                    var corrected = calibration.Correct(new(raw.X, raw.Y), new(orientation.X, orientation.Y));
                    var result = new Vector2((float)corrected.X, (float)corrected.Y);
                    if (float.IsFinite(result.X) && float.IsFinite(result.Y))
                        position.Position = result; // Interface reference mutates the original box, including structs.
                    else Unsupported();
                }
                else Unsupported();
            }
            else if (report is IAbsolutePositionReport || report is ITiltReport) Unsupported();
        }
        Emit?.Invoke(report);
    }

    private void Unsupported()
    {
        // Bounded counter and one diagnostic per activation; auxiliary/eraser/absent reports do not count.
        if (unsupportedCount < 100 && ++unsupportedCount == 100)
            Log.Write("Calibration Suite", "Repeated unusable pen reports (missing paired position/tilt, nonfinite input, or unrepresentable correction). These reports pass through unchanged.", LogLevel.Warning);
    }
}
