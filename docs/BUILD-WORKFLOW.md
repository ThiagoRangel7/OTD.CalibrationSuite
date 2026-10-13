# Build and debug

Use .NET SDK **10.0.401**, the .NET 8 Windows Desktop runtime, and Windows PowerShell 5.1. [README](../README.md) covers normal Release use.

## Editor builds

**Visual Studio 2026:** install .NET desktop development, open `CalibrationSuite.slnx`, build the solution, and set **CalibrationSuite.Calibrator** as startup project. Start OTD, choose Debug, and press **F5**. Use Release and **Ctrl+F5** for normal usability review. Test Explorer runs the synthetic suite; live-daemon checks are opt-in.

**VS Code:** open this folder and install the recommended C# Dev Kit extension. The workspace selects the solution. Use **Build Debug**, **Build Release**, and **Test** tasks; **Calibrator** launches the app under the debugger.

The first build downloads and verifies pinned OTD source. Later builds reuse `artifacts/otd-source`; NuGet packages are cached in `artifacts/packages`. No neighboring checkout or preparation command is needed. Build once to populate source navigation.

## Filter debugging

Building the filter also creates `artifacts/filter-package/<Configuration>/CalibrationSuite.zip`. Debug includes matching PDBs; Release excludes them. Unchanged builds reuse the ZIP; changed inputs or a missing ZIP regenerate it.

1. Install the Debug ZIP through OTD Plugin Manager and restart OTD.
2. In Visual Studio, use **Debug → Attach to Process → OpenTabletDriver.Daemon**, with managed .NET debugging. In VS Code, choose **Attach to OTD daemon**.
3. Break in `CalibrationFilter.Consume` and move the pen. To catch `Initialize`, attach before choosing Apply in OTD.

For unbound breakpoints, reinstall the matching DLL/PDB build and restart OTD. VS Code has local symbol paths; Visual Studio offers **Debug → Windows → Modules → Load Symbols**. Keep a mouse available: pausing the daemon pauses tablet processing. Pausing acquisition can trigger its timeout/stall checks.

Building never installs the filter or changes OTD settings. Use empty identity data for initial live checks, not fictional measurements.

## Commands

```powershell
dotnet build CalibrationSuite.slnx -c Debug
dotnet test tests/CalibrationSuite.Tests/CalibrationSuite.Tests.csproj -c Debug --no-build --no-restore --filter 'Category!=LiveDaemon'
dotnet build CalibrationSuite.slnx -c Release
```

Optional read-only daemon check, under the same Windows user as OTD:

```powershell
$env:CALIBRATION_LIVE_OTD = '1'
dotnet test tests/CalibrationSuite.Tests/CalibrationSuite.Tests.csproj -c Release --no-build --no-restore --filter Category=LiveDaemon --logger 'console;verbosity=detailed'
Remove-Item Env:CALIBRATION_LIVE_OTD
```

This reads installation/tablet metadata without enabling debug or accessing settings.

## Recorded checks

A fresh checkout built without adjacent source; cached preparation also worked without network. All six solution configuration/platform combinations built. Visual Studio 2026 and VS Code loaded the solution; normal Visual Studio Build Solution completed successfully. An earlier `devenv /Build` run emitted an automatic-restore exception, so it was not counted as a clean editor check.

The user reported build, launch, tests, and Release filter installation passing on 2026-10-03. Interactive filter breakpoints and broader editor/device configurations remain unverified. [Dependency maintenance](DEPENDENCIES.md#updating) describes pin changes.
