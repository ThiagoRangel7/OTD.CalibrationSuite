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

    // Use OTD's serialized contract, including dictionary contents and ordered lists.
    public static Func<TabletReference?, bool> CreateMatcher(TabletReference tablet)
    {
        var expected = JToken.FromObject(tablet);
        return incoming => incoming != null && JToken.DeepEquals(expected, JToken.FromObject(incoming));
    }
}
