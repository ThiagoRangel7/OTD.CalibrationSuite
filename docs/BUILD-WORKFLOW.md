# Build and editor workflow verification — 2026-09-14

This infrastructure change precedes milestone 4. It does not change capture, correction, or persistence behavior.

## Changes

- CalibrationSuite.slnx is the only solution. The SDK migration preserves all ten projects and Debug/Release with Any CPU/x64/x86 (the original platforms map to Any CPU project builds).
- The five OTD project definitions and upstream build properties are retained verbatim under dependencies/otd, alongside their package locks and license. They can load before any generated source exists. Shared targets attach the downloaded sources and preserve embedded tablet-configuration resource names.
- OTD source commit 736003ed72c8bbb28033b039d5a0bb76c344145c and archive SHA-256 are pinned in source.json. Normal builds prepare source automatically; there is no adjacent-folder dependency. Parallel MSBuild workers share a preparation lock.
- The filter uses OpenTabletDriver.Plugin 0.6.7 from NuGet with runtime assets excluded. The Calibrator continues to use source-built OTD Desktop and OTD report reconstruction.
- Building the filter creates a configuration-specific ZIP. Debug includes its two matching PDBs; Release excludes them. Installation and daemon restart remain explicit user actions.
- VS Code has build/test tasks, Calibrator launch, daemon attach, and a default .slnx selection. The Calibrator has a standard Visual Studio launch profile.
- Calibrator outputs include OTD/NTS notices and dependency documentation. Full redistribution review remains milestone 6 (renumbered when the visual milestone was added).

## Evidence

- Clean copy of repository inputs under artifacts/fresh-checkout, with no adjacent OTD source: ordinary Debug build downloaded and verified source, restored packages, built all projects, and passed all 103 tests that existed at that point.
- Final Debug and Release suites: **104 tests each passed**, including the new embedded Huion configuration check and Debug/Release package symbol checks.
- All six solution configuration/platform combinations build with zero warnings/errors through dotnet.
- Cached source preparation succeeds with the download function replaced by a throwing stub: the cached path needs no network.
- Calibrator Release process startup reached idle and responded; read-only installed-daemon detection passed after the dependency change. No live plugin replacement or OTD settings mutation was performed.
- VS Code with C# Dev Kit 3.20.207 opened CalibrationSuite.slnx, selected Debug/Any CPU, completed solution processing and package restoration. Confirmed in the extension's logs.
- Visual Studio 2026 18.10 opened the solution in its UI. Build Solution from the UI completed with **10 succeeded, 0 failed**. Immediate devenv /Build invocations also built all ten projects but emitted a ProjectNotNominatedException from automatic restore; the normal UI build after solution load completed without that warning. Do not treat the command-line invocation as a clean editor-restore check.
- The Debug/Release install test uses OTD's real Plugin Manager in an isolated temporary data directory. It verifies the official-package-compiled filter works with the source-built host interfaces.

Interactive breakpoints in the installed filter were not exercised: deploying a Debug ZIP and restarting the user's daemon remains the documented explicit workflow. No new physical accuracy claims are made. The user subsequently reported Release installation and live identity-data review checks passing on 2026-10-03; see docs/MILESTONE3.md. Interactive breakpoint verification remains pending.

## Updating

Update the Plugin NuGet version and the source revision together to a reviewed release. Replace upstream definitions verbatim and review their dependency changes; update locks explicitly, then return to normal locked restores. Re-run the build, resource, package, daemon, and editor checks. The README is the UI-first development guide.

## User review — 2026-10-03

The user reported the provided build/editor review and plugin installation checklist passing, including solution build, Calibrator launch, automated tests, and Release ZIP installation into OTD. This supplements the recorded implementation evidence above; it does not claim a new assistant-run build or expand testing to all editor/debugger combinations. See docs/MILESTONE3.md for the live identity-data results and remaining checks.
