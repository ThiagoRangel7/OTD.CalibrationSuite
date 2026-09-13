# Calibration Suite

A Windows calibrator for **display tablets**, using raw pen position and tilt from OTD 0.6.7. Milestone 1 currently provides a full-screen target/readings preview; capture, review, copying, and correction arrive in later milestones.

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
dotnet test tests/CalibrationSuite.Tests/CalibrationSuite.Tests.csproj -c Release --no-build --no-restore --filter 'Category!=LiveDaemon'
~~~

Preparation verifies source hashes and copies unmodified OTD sources to ignored artifacts/otd-source; it does not build/edit the adjacent repository. NuGet packages stay under artifacts/packages; lock files and fixed OTD BuildDate make inputs repeatable.

The runnable executable is src/CalibrationSuite.Calibrator/bin/Release/net8.0-windows/CalibrationSuite.Calibrator.exe. Keep the complete output directory together. The filter remains a pass-through shell, not a finished distributable package.

An optional read-only installed-daemon check (no settings reads or debug enablement):

~~~powershell
$env:CALIBRATION_LIVE_OTD = '1'
dotnet test tests/CalibrationSuite.Tests/CalibrationSuite.Tests.csproj -c Release --no-build --no-restore --filter Category=LiveDaemon --logger 'console;verbosity=detailed'
Remove-Item Env:CALIBRATION_LIVE_OTD
~~~

Run it under the same Windows user as OTD. See [milestone evidence and device checks](docs/MILESTONE1.md) and [dependencies](docs/DEPENDENCIES.md).

## Intended completed workflow

Open → move to tablet screen → calibrate → review/accept → copy → paste into the intended OTD filter profile and Save. The Calibrator will retain measurements only during the current app session. It will not load prior OTD calibration, compare applied settings, or manage profiles. OTD owns durable storage and filter configuration.
