# Calibration Suite

A Windows calibrator for **display tablets**, using raw pen position and tilt from OTD 0.6.7. The app provides a full-screen target/readings preview. The OTD filter applies the shared measured correction evaluator; capture, review, and copying arrive in milestone 4.

This project has not been released. Earlier designs are iterations toward the first release, not supported data versions. Calibration data follows only the current structure, with no version tags, migrations, or backward-compatibility code. Development changes may require new calibration data.

## Run

Start OTD with only the tablet being calibrated connected. From this repository:

~~~powershell
dotnet run --project src/CalibrationSuite.Calibrator -c Release --no-build --no-restore
~~~

The app connects automatically. Move its normal window onto the tablet's screen and press **Start preview**. The preview fills the monitor containing most of the window, including the taskbar area. No Connect button or tablet/display selector is needed.

Use **← / →** for the nine targets, **R** for the first target, and **Esc** to return. Lift the pen whenever needed: after 500 ms without reports the live values clear and the preview shows **Waiting for pen**, without resetting the target. Actual disconnects, display/DPI changes, queue overflow, or blocked processing stop acquisition. Start again to acquire fresh device/display geometry.

Supported setup: the full active digitizer covers the full screen, with X pointing right and Y down. Rotated/cropped/misaligned configurations and non-display tablets are outside scope. You identify the tablet screen and aim with the physical tip. Disable other OTD filters to keep the cursor undistracting, then restore them afterward; raw acquisition precedes filters. The current OTD output mode and cursor mapping do not determine crosshair placement.

Close OTD's tablet debugger before starting. Debug acquisition is daemon-global; concurrent debugger use is unsupported. Cleanup runs on exit/cancellation. If cleanup is unconfirmed, restart OTD and Calibrator before retrying. No OTD profile/settings are read or changed. Tilt is shown as parsed, without angle guidance until actual device semantics are verified.

## Build and test

Prerequisites: Windows, PowerShell 7, .NET SDK **10.0.401** (pinned), .NET 8 Windows Desktop runtime, and the reviewed adjacent source tree at ../OpenTabletDriver-0.6.7.

~~~powershell
./scripts/Prepare-Otd.ps1
dotnet restore CalibrationSuite.sln --locked-mode -p:RestorePackagesWithLockFile=true
dotnet build CalibrationSuite.sln -c Release --no-restore -p:BuildDate=2026-09-12
./scripts/Package-Filter.ps1
dotnet test tests/CalibrationSuite.Tests/CalibrationSuite.Tests.csproj -c Release --no-build --no-restore --filter 'Category!=LiveDaemon'
~~~

Preparation verifies source hashes and copies unmodified OTD sources to ignored artifacts/otd-source; it does not build/edit the adjacent repository. NuGet packages stay under artifacts/packages; lock files and fixed OTD BuildDate make inputs repeatable.

The runnable executable is src/CalibrationSuite.Calibrator/bin/Release/net8.0-windows/CalibrationSuite.Calibrator.exe. Keep the complete output directory together. The filter ZIP is `artifacts/filter-package/CalibrationSuite.zip`. Rebuild and repackage before tests: the package test installs this ZIP into an isolated temporary OTD data directory, without touching your installed daemon or settings.

## Install and review the filter

In OTD 0.6.7, open **Plugins → Plugin Manager**, choose **Install plugin...** (or drag the ZIP into that window), and select `artifacts/filter-package/CalibrationSuite.zip`. Keep the stable ZIP filename when updating. The package includes the filter, shared core, NetTopologySuite, metadata, and notices; it does not replace OTD assemblies. Update/removal use the same Plugin Manager.

In the intended tablet profile's **Filters** tab, add **Calibration Suite** and keep exactly one enabled entry. It runs at **PreTransform** and must receive unchanged parsed position and tilt: position/tilt-changing filters must not precede it. Configure ordering in OTD; the Calibrator does not inspect it. Same-name tablets share an OTD profile, so confirm the destination tablet and pen yourself.

For this milestone, review live behavior with **Calibration data** empty (identity). Verify its paste/Save/clear tooltip, enable/bypass, **Apply**, and **Save**. Save persists and applies; Apply alone does not prove a disk save. Clear the property and Save to reset; disable the checkbox to bypass while retaining data. Do not paste fictional measurements from the specification or tests onto real hardware. Actual measured correction can be checked after capture becomes available.

After milestone 4, paste accepted Calibrator data into **Calibration data** and Save. OTD settings and presets retain this string; no separate calibration file is needed. Invalid data or geometry mismatch uses identity and reports the reason in OTD logs. Repeated unsupported pen input produces one warning per activation; missing tilt is never treated as upright, and eraser/out-of-proximity reports pass unchanged. See [milestone 3 review checks](docs/MILESTONE3.md).

An optional read-only installed-daemon check (no settings reads or debug enablement):

~~~powershell
$env:CALIBRATION_LIVE_OTD = '1'
dotnet test tests/CalibrationSuite.Tests/CalibrationSuite.Tests.csproj -c Release --no-build --no-restore --filter Category=LiveDaemon --logger 'console;verbosity=detailed'
Remove-Item Env:CALIBRATION_LIVE_OTD
~~~

Run it under the same Windows user as OTD. See [milestone evidence and device checks](docs/MILESTONE1.md) and [dependencies](docs/DEPENDENCIES.md).

## Review the correction core

Start with [milestone 2 examples and checks](docs/MILESTONE2.md). The core has no WPF or OTD dependency: `CalibrationData` handles calibration JSON, `CaptureSettings` resolves grid/poses, `NodeMesh` interpolates measured tilt errors, and `PreparedCalibration` combines four nodes to correct raw position. Loading invalid data returns identity with a diagnostic; no original JSON is rewritten. Preparing copies evaluator inputs so subsequent edits cannot affect a running snapshot.

The core also supports upright-only calibration: zero azimuths or zero non-upright levels produces one pose per target. A single sample supplies a constant error at that target, blended across the screen using the same formula. Its UI controls arrive with capture in milestone 4.

The automated suite covers these mathematical and data rules using fictional measurements. It does not establish physical accuracy. The visible app remains a preview.

## Intended completed workflow

Open → move to tablet screen → calibrate → review/accept → copy → paste into the intended OTD filter profile and Save. The Calibrator will retain measurements only during the current app session. It will not load prior OTD calibration, compare applied settings, or manage profiles. OTD owns durable storage and filter configuration.
