# Calibration Suite for OpenTabletDriver

A Windows app and OTD 0.6.7 filter for calibrating display tablets. Capture pen measurements, review them, then copy the accepted calibration into OTD.

This project has not been released. Development changes may require a new capture.

## Build and run

Requirements: Windows, .NET SDK **10.0.401**, the **.NET 8 Windows Desktop runtime**, and OTD **0.6.7**. The first build needs internet access to download pinned dependencies and OTD source.

```powershell
dotnet build CalibrationSuite.slnx -c Release
dotnet run --project src/CalibrationSuite.Calibrator -c Release --no-build --no-restore
```

In Visual Studio 2026, install **.NET desktop development**, open `CalibrationSuite.slnx`, and set **CalibrationSuite.Calibrator** as the startup project. Build Release and press **Ctrl+F5**. VS Code users can install the recommended C# Dev Kit extension and use the supplied build/debug tasks. See [build and debugging instructions](docs/BUILD-WORKFLOW.md).

## Calibrate

1. Start OTD with only the display tablet being calibrated connected. Its full digitizer must cover the full screen, with X right and Y down. Rotated or cropped mappings and non-display tablets are outside scope.
2. Move the app window to the tablet screen. Close OTD's tablet debugger, disable other filters to reduce cursor distractions, and choose **Calibrate**. Aim with the physical nib.
3. At each crosshair, hold a comfortable first pose to set the reported-tilt reference. For later poses, place the cyan arrow tip inside the target circle. Press **Space** and hold still through the burst.
4. Review the measurements. Use **Recapture Pose** or **Recapture Node** if needed, then **Accept Calibration**.
5. Install the filter as described below. **Copy Calibration**, paste into the intended OTD profile's **Calibration data** field, and **Save**. Restore other filters afterward.

The default grid has 3 × 3 targets and up to 17 poses per target: 153 captures. Starting limits are 8 reported units of tilt tolerance, a 200 ms burst with at least 20 reports, 0.5 mm position stability, and 3 reported units of tilt stability.

Tilt guidance uses the device's reported coordinates, not physical pen angles. The first reference can be nonzero. Reduce **Tilt range** if targets are hard to reach.

If the first eligible pen report has no tilt, the session switches to **upright positions only**: one capture per target. Hold the pen upright; the app cannot verify posture or correct orientation-dependent error without tilt. Set **Tilt directions** or **Tilt levels** to 0 to choose this mode yourself if reported tilt is unusable.

| Key | Action |
| --- | --- |
| Space | Capture one burst; pressure does not trigger capture |
| Backspace | Revisit the previous pose, including across targets |
| R | Restart the unfinished capture, or just the targeted recapture selection |
| Esc | Return to the main screen and keep completed measurements |

Green means ready, cyan means capturing, and gray means waiting for the pen. Local cues explain success and retries; raw readings stay in corner cards. Lifting between bursts preserves progress. A gap, wrong pose, or excessive movement rejects only that attempt. Use **Resume Capture** after leaving the capture screen.

Disconnects, display/DPI changes, queue overflow, or stalled processing end acquisition. Resume with fresh geometry; changed digitizer geometry requires a new calibration. If debug cleanup is unconfirmed, restart OTD and the Calibrator.

Acceptance replaces only the app's in-memory accepted data. New captures, retries, and discards preserve it. Closing the app loses session measurements; copying does not save them in OTD. Use OTD settings backups or presets for durable storage.

## Install the filter

Building creates `artifacts/filter-package/Release/CalibrationSuite.zip`.

1. In OTD, open **Plugins → Plugin Manager → Install plugin...**, select the ZIP, and restart OTD.
2. In the intended profile's **Filters** tab, add exactly one enabled **Calibration Suite** entry. Place it before filters that change position or tilt.
3. Paste your accepted calibration into **Calibration data** and **Save**. Empty data gives identity correction. Clear it and Save to reset, or disable the entry to bypass while retaining data.

Use the same ZIP filename for updates. Plugin Manager handles update/removal. Invalid data or mismatched geometry falls back to identity and reports the reason in OTD logs. Position-only data works without tilt; tilt-dependent data leaves missing-tilt reports unchanged. Eraser and out-of-range reports pass through.

The app does not read or change OTD profiles or settings. Confirm the destination tablet and pen yourself; same-name tablets share an OTD profile. Use only your measurements on hardware, never the fictional test fixtures.

## Development checks

```powershell
dotnet test tests/CalibrationSuite.Tests/CalibrationSuite.Tests.csproj -c Release --no-build --no-restore --filter 'Category!=LiveDaemon'
dotnet run --project scripts/VisualChecks -c Release
dotnet run --project scripts/PerformanceChecks -c Release
```

The visual checker writes PNGs to `artifacts/visual-checks`. These checks use synthetic data and do not establish hardware accuracy.

- [Specification](SPEC.md): data, capture rules, correction math, and integration contracts.
- [Project status](PLAN.md): completed work and remaining checks.
- [Dependencies](docs/DEPENDENCIES.md): pins, licenses, acquisition limits, and updates.
- [Release preparation](docs/RELEASE.md): archives and final device workflow checklist.
- [Optimization notes](docs/OPTIMIZATION.md): retained runtime improvements and measurements.

## License

Calibration Suite is [MIT licensed](LICENSE). Third-party components retain their licenses; release archives include notices and the corresponding OTD source/build inputs.
