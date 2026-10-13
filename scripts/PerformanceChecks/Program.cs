using System.Diagnostics;
using System.Globalization;
using CalibrationSuite.Core;
using CalibrationSuite.Otd;
using Newtonsoft.Json;
using OpenTabletDriver.Plugin.Tablet;

// Synthetic checks only. No daemon connection, device input, or settings changes.
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
var assembly = typeof(OpenTabletDriver.Configurations.Parsers.Huion.GianoReport).Assembly;
using var stream = assembly.GetManifestResourceStream("OpenTabletDriver.Configurations.Configurations.Huion.Kamvas 13 (Gen 3).json")!;
using var reader = new StreamReader(stream);
var configuration = JsonConvert.DeserializeObject<TabletConfiguration>(reader.ReadToEnd())!;
var tablet = new TabletReference(configuration, configuration.DigitizerIdentifiers);
var incoming = JsonConvert.DeserializeObject<TabletReference>(JsonConvert.SerializeObject(tablet))!;

bool matched = false;
var matchesTablet = TabletSelection.CreateMatcher(tablet);
Measure("Tablet reference comparison", 1000, () => matched = matchesTablet(incoming));
if (!matched) throw new InvalidOperationException("Round-tripped tablet did not match.");

var geometry = new SourceGeometry(20000, 10000, 200, 120);
var largeDraft = new CaptureDraft(new() { RowCount = 100, ColumnCount = 100, TiltDirections = 0 }, geometry);
int captured = -1;
Measure("Capture count (10,000 slots)", 10000, () => Volatile.Write(ref captured, largeDraft.CapturedCount));
if (captured != 0) throw new InvalidOperationException("Empty draft has captured measurements.");

var draft = new CaptureDraft(new(), geometry);
while (!draft.Complete)
{
    var pose = draft.CurrentPose;
    double magnitude = Math.Tan((90 - pose.Magnitude) * Math.PI / 180);
    double angle = (pose.DirectionDeg ?? 0) * Math.PI / 180;
    var tilt = new Point2(Math.Atan(magnitude * Math.Cos(angle)) * 180 / Math.PI,
        Math.Atan(magnitude * Math.Sin(angle)) * 180 / Math.PI);
    var target = draft.CurrentTarget;
    draft.Store(new(target + new Point2(2 + target.X / 1000 + tilt.X / 10, -3 + target.Y / 1000 + tilt.Y / 10), tilt));
}
var prepared = PreparedCalibration.Prepare(draft.ToData(), geometry);
int index = 0;
Point2 corrected = default;
Measure("Correction inside tilt hull", 100000, () =>
    corrected = prepared.Correct(new(9000 + index++ % 1000, 4000), new(index % 30 - 15, index % 20 - 10)));
Measure("Correction outside tilt hull", 100000, () =>
    corrected = prepared.Correct(new(9000 + index++ % 1000, 4000), new(70, index % 80 - 40)));
if (!corrected.IsFinite) throw new InvalidOperationException("Correction is nonfinite.");

var burst = new CaptureBurst(new() { CaptureWindowMs = 60, MinSamplesPerCapture = 64 }, geometry);
CalibrationSample? sample = null;
Measure("Capture burst (64 reports)", 1000, () =>
{
    burst.Start(null, 0);
    for (int i = 0; i < 64; i++)
        sample = burst.Add(i, new(10000 + i % 3, 5000 + i % 2), default);
});
if (sample == null) throw new InvalidOperationException("Synthetic burst was not accepted.");

static void Measure(string name, int repetitions, Action action)
{
    for (int i = 0; i < 1000; i++)
        action();
    var times = new double[5];
    var allocations = new long[5];
    for (int run = 0; run < times.Length; run++)
    {
        var watch = Stopwatch.StartNew();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < repetitions; i++)
            action();
        allocations[run] = (GC.GetAllocatedBytesForCurrentThread() - before) / repetitions;
        times[run] = watch.Elapsed.TotalMicroseconds / repetitions;
    }
    Array.Sort(times);
    Array.Sort(allocations);
    Console.WriteLine($"{name}: median {times[2]:F4} us/op, {allocations[2]:N0} bytes/op ({repetitions:N0} operations/pass, 5 passes).");
}
