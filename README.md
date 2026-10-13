# Calibration Suite for OpenTabletDriver

A Windows calibrator for **display tablets**, using raw pen position and optional reported tilt from OTD 0.6.7. The app guides full-screen measurement capture, reviews and recaptures measurements, and copies accepted calibration into OTD. The OTD filter applies the shared measured correction evaluator.

This project has not been released. Earlier designs are iterations toward the first release, not supported data versions. Calibration data follows only the current structure, with no version tags, migrations, or backward-compatibility code. Development changes may require new calibration data.

Capture supports OTD absolute pen reports with or without tilt. Support derives from OTD's report interfaces and digitizer geometry, without a tablet-model catalog or device-specific settings. Guidance uses reported coordinates. See [current scope and release verification](docs/RELEASE.md).

## Run

Start OTD with only the tablet being calibrated connected. From this repository:

~~~powershell
dotnet run --project src/CalibrationSuite.Calibrator -c Release --no-build --no-restore
~~~

The app connects automatically. Move its normal window onto the tablet screen, choose capture settings, and press **Calibrate**. All inputs stay visible in **Target grid**, **Reported tilt**, **Sampling**, and **Quality limits**; smaller windows scroll. The default is 3 × 3 targets with up to 17 poses each (153 captures). When the first eligible pen report has no OTD `ITiltReport`, tilt calibration is blocked for that session and the screen switches to **upright positions only**: one sample per target (9 captures). Hold the pen upright at every crosshair. Tilt is not measured in this mode, so the app cannot verify that posture or compensate for orientation-dependent error.

OTD's tablet metadata has no reliable tilt-capability flag. The app checks reports during capture, without model-specific guesses or interpreting zero tilt as missing support. If a device exposes tilt that does not respond usefully to pen movement, set **Tilt directions** or **Tilt levels** to 0 to choose upright position-only capture yourself.

The starting limits are **8 reported units of tilt tolerance**, a **200 ms burst** with at least **20 reports**, **0.5 mm position stability**, and **3 reported units of tilt stability**. Stability measures the 95th-percentile deviation from the burst median. Tilt range and tolerance use the device's reported coordinates; they do not claim physical pen angles.

At each target, the first stable pose establishes a reported-tilt reference. Later targets spread around that value. Hold a comfortable central pose for the reference; reduce **Tilt range** if targets are hard to reach. Neither the reference nor the coverage hull needs to contain raw (0, 0). Stored tilt retains actual measurements when reported, never a snapped target. Missing tilt uses an unused zero placeholder in position-only data.
Use the keyboard in the opaque full-screen window:

- **Space:** hold the physical nib on the crosshair in the requested pose and capture one burst. Every tilted-pose report must meet the reported-tilt tolerance; the burst must meet both duration and sample minima and the position/tilt stability limits. Pressure does not trigger capture.
- **Backspace:** revisit the previous pose, including across targets.
- **R:** restart the unfinished capture; during targeted recapture, restart only that selection.
- **Esc:** cancel the active burst and return to the main screen. Completed measurements remain available through **Resume Capture** or **Review Calibration**.

The thin cyan arrow at the crosshair plots the current reported tilt relative to that target's reference (X right, Y up). Place its tip inside the unfilled target circle. For the reference, hold a comfortable pose; for position-only captures, hold upright. Aim with the nib and hold still. The small open white nib marker and state-colored crosshair remain precise. Green means ready, cyan means capturing, gray means waiting; progress and retry/success cues stay beside the crosshair. Raw readings remain in the corner cards.
Lifting the pen between bursts preserves progress. A gap, pose failure, or excessive movement rejects only that attempt with a retry reason. Disconnects, geometry/DPI changes, overflow, or blocked processing end acquisition; resume with fresh geometry when ready. If debug cleanup is unconfirmed, restart OTD and Calibrator first. Close OTD's tablet debugger before capture; concurrent debugger use is unsupported.

Completion opens **Review Calibration**. Inspect requested poses, measured positions/tilts, and raw errors. Select a row for **Recapture Pose** or **Recapture Node**, **Discard Unaccepted Data**, or **Accept Calibration**. Acceptance replaces only the in-memory working calibration. New captures, retries, and discards preserve accepted data until another candidate is accepted.

**Copy Calibration** copies only accepted data. Paste it into the intended OTD profile's **Calibration data** field and **Save**. Copying does not apply or save anything in OTD. Closing the app loses its session measurements; the close reminder remains even after copying because the app cannot observe an OTD save. Use OTD presets/settings backups for durable versions.

Supported setup: the full active digitizer covers the full screen, with X pointing right and Y down. Rotated/cropped/misaligned configurations and non-display tablets are outside scope. Aim with the physical nib. Disable other filters to keep the cursor undistracting, then restore them afterward; raw acquisition precedes filters. OTD cursor mapping does not determine crosshair placement. The Calibrator does not read or change profiles/settings.

## Build and debug through the editor

Use **Visual Studio 2026** with the **.NET desktop development** workload and the pinned **.NET SDK 10.0.401**. Windows PowerShell 5.1 (included with Windows) runs the build helpers. The Calibrator needs the .NET 8 Windows Desktop runtime. Internet is needed for the first dependency restore/source download.

1. In Visual Studio choose **Open a project or solution**, then select **CalibrationSuite.slnx**.
2. Choose **Build → Build Solution**. The first build downloads and verifies the pinned OTD source automatically; later builds reuse it. No neighboring OTD checkout or preparation command is needed.
3. In **View → Solution Explorer**, right-click **CalibrationSuite.Calibrator → Set as Startup Project**.
4. Select **Debug** in the toolbar. Start OTD normally, then click the green **Start** button to build and debug the Calibrator.
5. Open **Test → Test Explorer** and choose **Run All Tests**. The live-daemon test is opt-in; other tests use synthetic data.

For a normal **Release** usability review, build Release and choose **Debug → Start Without Debugging** (**Ctrl+F5**). This launches the app without attaching a debugger to the optimized build.

A breakpoint is a red dot added by clicking beside a line number. The debugger pauses there so you can inspect variables. Pausing acquisition can activate its intentional stall/time limits.

**VS Code:** open this repository folder and install the recommended **C# Dev Kit** extension. The workspace selects CalibrationSuite.slnx. Choose **Terminal → Run Build Task → Build Debug**, or **Run and Debug → Calibrator → Start Debugging**. **Terminal → Run Task** also offers **Build Release** and **Test**. The configured tasks perform the same builds as Visual Studio.

### Build and debug the filter

Building **CalibrationSuite.Filter**, or the whole solution, automatically creates a ZIP:

- Debug: **artifacts/filter-package/Debug/CalibrationSuite.zip**, including matching PDB debugging symbols.
- Release: **artifacts/filter-package/Release/CalibrationSuite.zip**, for normal installation.

Unchanged builds reuse the ZIP. Changes to its assemblies, Debug symbols, metadata, notices, or packaging script regenerate it; a missing ZIP is recreated automatically.

To debug the filter, install the Debug ZIP through OTD's Plugin Manager, then restart OTD so the new assembly is loaded. This is an explicit installation step; building never overwrites your installed plugin or settings.

In Visual Studio choose **Debug → Attach to Process**, select **OpenTabletDriver.Daemon**, and attach using managed .NET debugging. In VS Code select **Attach to OTD daemon** in **Run and Debug** and choose that same process. Set a breakpoint in CalibrationFilter.Consume and move the pen. To catch Initialize, attach first, then choose **Apply** in OTD. If a breakpoint is hollow/unbound, confirm that the installed DLL and local PDB are from the same build; reinstall the Debug ZIP and restart OTD after changes. VS Code includes local symbol search paths; in Visual Studio use **Debug → Windows → Modules → Load Symbols** if needed.

Keep a mouse available: pausing the daemon pauses tablet processing. Do not install fictional measured data; use identity for initial live checks. Calibrator launch and filter attachment are separate debug sessions.

### Command-line equivalent and outputs

These commands are optional; editor builds do the same work:

~~~powershell
dotnet build CalibrationSuite.slnx -c Debug
dotnet test tests/CalibrationSuite.Tests/CalibrationSuite.Tests.csproj -c Debug --no-build --filter 'Category!=LiveDaemon'
dotnet build CalibrationSuite.slnx -c Release
~~~

Build distributable archives with **scripts/Package-Release.ps1**. It produces the plugin ZIP, a separate Windows x64 Calibrator ZIP, corresponding source/build inputs, registry metadata, and checksums under **artifacts/releases/0.1.0.0**. See [release preparation and installation](docs/RELEASE.md).

The source revision and archive checksum are in **dependencies/otd/source.json**. OTD project definitions and package locks are committed there so editors can load all ten projects before the source download. Source files are unmodified and cached under **artifacts/otd-source**; NuGet packages are under **artifacts/packages**. The filter uses the official OTD Plugin package. See [dependency maintenance](docs/DEPENDENCIES.md) and [build workflow verification](docs/BUILD-WORKFLOW.md).

An optional read-only installed-daemon check:

~~~powershell
$env:CALIBRATION_LIVE_OTD = '1'
dotnet test tests/CalibrationSuite.Tests/CalibrationSuite.Tests.csproj -c Debug --no-build --filter Category=LiveDaemon --logger 'console;verbosity=detailed'
Remove-Item Env:CALIBRATION_LIVE_OTD
~~~

## Install and review the filter

In OTD 0.6.7, open **Plugins → Plugin Manager**, choose **Install plugin...** (or drag the ZIP into that window), and select `artifacts/filter-package/Release/CalibrationSuite.zip`. Keep the stable ZIP filename when updating. The package includes the filter, shared core, NetTopologySuite, metadata, and notices; it does not replace OTD assemblies. Update/removal use the same Plugin Manager.

In the intended tablet profile's **Filters** tab, add **Calibration Suite** and keep exactly one enabled entry. It runs at **PreTransform** and must receive unchanged parsed position and, for tilt-dependent data, tilt: position/tilt-changing filters must not precede it. Configure ordering in OTD; the Calibrator does not inspect it. Same-name tablets share an OTD profile, so confirm the destination tablet and pen yourself.

For initial installation review, use **Calibration data** empty (identity). Verify its paste/Save/clear tooltip, enable/bypass, **Apply**, and **Save**. Save persists and applies; Apply alone does not prove a disk save. Clear the property and Save to reset; disable the checkbox to bypass while retaining data. Do not paste fictional measurements from the specification or tests onto real hardware. Then capture your own measurements and check enabled/bypassed correction using the short checklist in docs/RELEASE.md.

Paste accepted Calibrator data into **Calibration data** and Save. OTD settings and presets retain this string; no separate calibration file is needed. Invalid data or geometry mismatch uses identity and reports the reason in OTD logs. Repeated unsupported pen input produces one warning per activation; tilt-dependent data passes missing-tilt reports unchanged; position-only data corrects them, and eraser/out-of-proximity reports pass unchanged. See [milestone 3 review checks](docs/MILESTONE3.md).

An optional read-only installed-daemon check (no settings reads or debug enablement):

~~~powershell
$env:CALIBRATION_LIVE_OTD = '1'
dotnet test tests/CalibrationSuite.Tests/CalibrationSuite.Tests.csproj -c Release --no-build --no-restore --filter Category=LiveDaemon --logger 'console;verbosity=detailed'
Remove-Item Env:CALIBRATION_LIVE_OTD
~~~

Run it under the same Windows user as OTD. See [milestone evidence and device checks](docs/MILESTONE1.md) and [dependencies](docs/DEPENDENCIES.md).

## Review the correction core

Start with [milestone 2 examples and checks](docs/MILESTONE2.md). The core has no WPF or OTD dependency: `CalibrationData` handles calibration JSON, `CaptureSettings` resolves grid/poses, `NodeMesh` interpolates measured tilt errors, and `PreparedCalibration` combines four nodes to correct raw position. Loading invalid data returns identity with a diagnostic; no original JSON is rewritten. Preparing copies evaluator inputs so subsequent edits cannot affect a running snapshot.

Capture drafts own their grid/poses, and targeted recapture copies the measurements. Accepted JSON is cached as a snapshot. Settings and reports are validated when they enter the workflow; each completed node is checked once. The main window coordinates acquisition directly, while capture views render snapshots and send commands.

Position-only calibration stores one measured error per grid target and blends across the screen using the same spatial formula. It works on tablets without tilt and can also be selected on tilt-capable tablets.

The automated suite covers these mathematical and data rules using fictional measurements. It does not establish physical accuracy. The current functional workflow checklist is in docs/RELEASE.md.

The offscreen WPF verifier renders setup, all capture states/directions, and review without connecting to OTD or opening capture windows:

~~~powershell
dotnet run --project scripts/VisualChecks -c Release
~~~

PNGs are written to `artifacts/visual-checks`. See [milestone 5](docs/MILESTONE5.md) for checked sizes, synthetic input checks, and remaining physical review.

Run the repeatable synthetic performance checks with:

~~~powershell
dotnet run --project scripts/PerformanceChecks -c Release
~~~

The runner measures tablet comparison, large-session progress reads, correction inside/outside tilt coverage, and capture-burst statistics without connecting to a daemon. See the [optimization report](docs/OPTIMIZATION.md) for measured results, changes, and verification.

## Calibration workflow

Open → move to tablet screen → calibrate → review/accept → copy → paste into the intended OTD filter profile and Save. The Calibrator retains measurements only during the current app session. It does not load prior OTD calibration, compare applied settings, or manage profiles. OTD owns durable storage and filter configuration.

## License

Calibration Suite's own code is [MIT licensed](LICENSE). OpenTabletDriver-derived build inputs and distributed components retain their existing licenses. The release archives include third-party notices and the corresponding OTD source/build inputs.
