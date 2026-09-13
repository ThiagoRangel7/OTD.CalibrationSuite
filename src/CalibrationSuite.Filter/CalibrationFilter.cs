using OpenTabletDriver.Plugin.Attributes;
using OpenTabletDriver.Plugin.Output;
using OpenTabletDriver.Plugin.Tablet;

namespace CalibrationSuite.Filter;

[PluginName("Calibration Suite (preview shell)")]
public sealed class CalibrationFilter : IPositionedPipelineElement<IDeviceReport>
{
    public PipelinePosition Position => PipelinePosition.PreTransform;
    public event Action<IDeviceReport>? Emit;
    public void Consume(IDeviceReport report) => Emit?.Invoke(report);
}
