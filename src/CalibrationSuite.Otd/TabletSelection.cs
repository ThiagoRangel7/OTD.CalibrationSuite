using Newtonsoft.Json.Linq;
using OpenTabletDriver.Plugin.Tablet;

namespace CalibrationSuite.Otd;

public static class TabletSelection
{
    public static TabletReference Single(TabletReference[] tablets) => tablets.Length switch
    {
        1 => tablets[0],
        0 => throw new InvalidOperationException("Connect your display tablet to OTD."),
        _ => throw new InvalidOperationException("Connect only the display tablet being calibrated; disconnect other tablets.")
    };

    // RPC reconstructs references; object identity cannot identify the selected input.
    public static bool Same(TabletReference? a, TabletReference? b) => a != null && b != null
        && JToken.DeepEquals(JToken.FromObject(a), JToken.FromObject(b));
}
