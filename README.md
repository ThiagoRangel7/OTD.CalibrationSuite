# Calibration Suite

A Windows calibrator for **display tablets**, using raw pen position and tilt from OTD 0.6.7. The app guides full-screen measurement capture, reviews and recaptures measurements, and copies accepted calibration into OTD. The OTD filter applies the shared measured correction evaluator.

This project has not been released. Earlier designs are iterations toward the first release, not supported data versions. Calibration data follows only the current structure, with no version tags, migrations, or backward-compatibility code. Development changes may require new calibration data.

Milestone 4 capture/review is checkpointed at `2ec8a32`; physical review remains pending. The next planned work is [milestone 5: visual guidance and UI usability](docs/MILESTONE5.md), with shorter instructions, clearer hierarchy, visible relevant controls, and 3D arrow/target guidance. Final audit and delivery are milestone 6.

## Run

Start OTD with only the tablet being calibrated connected. From this repository:

~~~powershell
dotnet run --project src/CalibrationSuite.Calibrator -c Release --no-build --no-restore
~~~

The app connects automatically. Move its normal window onto the tablet screen, choose capture settings, and press **Calibrate**. The default is 3 × 3 targets with 17 poses each (153 captures). Set **Azimuth directions** or **Non-upright altitude levels** to 0 for upright-only calibration. A small 2 × 2 upright-only layout is useful for the first workflow check. Settings live only in this app session; invalid settings are explained rather than adjusted silently.

Capture currently supports the **Huion Kamvas 13 (Gen 3)** with OTD's Giano parser, using the cardinal directions you verified. Known physical angle scale, diagonal conversion, and reachable poses still need the [milestone 4 device review](docs/MILESTONE4.md). Other tablet/parser combinations are unavailable for capture until their orientation semantics are verified. This limit applies to capture guidance, not the filter's raw-tilt interpolation.

Use the keyboard in the opaque full-screen window:

- **Space:** hold the physical nib on the crosshair in the requested pose and capture one burst. Every report must meet the angle tolerance; the burst must meet both duration and sample minima and the position/tilt stability limits. Pressure does not trigger capture.
- **Backspace:** revisit the previous pose, including across targets.
- **R:** restart the unfinished capture; during targeted recapture, restart only that selection.
- **Esc:** cancel the active burst and return to the main screen. Completed measurements remain available through **Resume Capture** or **Review Calibration**.

At each target, capture upright, then all altitude levels for the first azimuth, all levels for the next azimuth, and so on. Azimuth describes the eraser direction: 0° left, 90° down, 180° right, 270° up. Altitude is 90° upright. **Minimum altitude** 50° allows at most 40° of lean. Live readings use the latest report; stored measurements use burst medians.

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

Keep the Calibrator's complete **src/CalibrationSuite.Calibrator/bin/Release/net8.0-windows** folder together, including DLLs and notices. The user needs a running compatible OTD 0.6.7 daemon, but never source code or build tools. Runtime dependency redistribution review remains part of milestone 6; this is a development output, not a published release.

The source revision and archive checksum are in **dependencies/otd/source.json**. OTD project definitions and package locks are committed there so editors can load all ten projects before the source download. Source files are unmodified and cached under **artifacts/otd-source**; NuGet packages are under **artifacts/packages**. The filter uses the official OTD Plugin package. See [dependency maintenance](docs/DEPENDENCIES.md) and [build workflow verification](docs/BUILD-WORKFLOW.md).

An optional read-only installed-daemon check:

~~~powershell
$env:CALIBRATION_LIVE_OTD = '1'
dotnet test tests/CalibrationSuite.Tests/CalibrationSuite.Tests.csproj -c Debug --no-build --filter Category=LiveDaemon --logger 'console;verbosity=detailed'
Remove-Item Env:CALIBRATION_LIVE_OTD
~~~

## Install and review the filter

In OTD 0.6.7, open **Plugins → Plugin Manager**, choose **Install plugin...** (or drag the ZIP into that window), and select `artifacts/filter-package/Release/CalibrationSuite.zip`. Keep the stable ZIP filename when updating. The package includes the filter, shared core, NetTopologySuite, metadata, and notices; it does not replace OTD assemblies. Update/removal use the same Plugin Manager.

In the intended tablet profile's **Filters** tab, add **Calibration Suite** and keep exactly one enabled entry. It runs at **PreTransform** and must receive unchanged parsed position and tilt: position/tilt-changing filters must not precede it. Configure ordering in OTD; the Calibrator does not inspect it. Same-name tablets share an OTD profile, so confirm the destination tablet and pen yourself.

For initial installation review, use **Calibration data** empty (identity). Verify its paste/Save/clear tooltip, enable/bypass, **Apply**, and **Save**. Save persists and applies; Apply alone does not prove a disk save. Clear the property and Save to reset; disable the checkbox to bypass while retaining data. Do not paste fictional measurements from the specification or tests onto real hardware. Then capture your own measurements and check actual correction as described in docs/MILESTONE4.md.

Paste accepted Calibrator data into **Calibration data** and Save. OTD settings and presets retain this string; no separate calibration file is needed. Invalid data or geometry mismatch uses identity and reports the reason in OTD logs. Repeated unsupported pen input produces one warning per activation; missing tilt is never treated as upright, and eraser/out-of-proximity reports pass unchanged. See [milestone 3 review checks](docs/MILESTONE3.md).

An optional read-only installed-daemon check (no settings reads or debug enablement):

~~~powershell
$env:CALIBRATION_LIVE_OTD = '1'
dotnet test tests/CalibrationSuite.Tests/CalibrationSuite.Tests.csproj -c Release --no-build --no-restore --filter Category=LiveDaemon --logger 'console;verbosity=detailed'
Remove-Item Env:CALIBRATION_LIVE_OTD
~~~

Run it under the same Windows user as OTD. See [milestone evidence and device checks](docs/MILESTONE1.md) and [dependencies](docs/DEPENDENCIES.md).

## Review the correction core

Start with [milestone 2 examples and checks](docs/MILESTONE2.md). The core has no WPF or OTD dependency: `CalibrationData` handles calibration JSON, `CaptureSettings` resolves grid/poses, `NodeMesh` interpolates measured tilt errors, and `PreparedCalibration` combines four nodes to correct raw position. Loading invalid data returns identity with a diagnostic; no original JSON is rewritten. Preparing copies evaluator inputs so subsequent edits cannot affect a running snapshot.

The core also supports upright-only calibration: zero azimuths or zero non-upright levels produces one pose per target. A single sample supplies a constant error at that target, blended across the screen using the same formula. The capture settings expose this option.

The automated suite covers these mathematical and data rules using fictional measurements. It does not establish physical accuracy. Capture and review checks are described in docs/MILESTONE4.md.

## Calibration workflow

Open → move to tablet screen → calibrate → review/accept → copy → paste into the intended OTD filter profile and Save. The Calibrator retains measurements only during the current app session. It does not load prior OTD calibration, compare applied settings, or manage profiles. OTD owns durable storage and filter configuration.
