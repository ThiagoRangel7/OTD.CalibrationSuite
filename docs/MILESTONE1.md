# Milestone 1 review — 2026-09-13

**Approved to advance.** After reviewing the simplified workflow, the user explicitly requested "Implement milestone 2" on 2026-09-13. This records approval to advance, not completion of the physical checks below. The following implementation and validation evidence describes milestone 1; see [milestone 2](MILESTONE2.md) for subsequent core work.

## Implemented behavior

- Automatic OTD connection and idle retry, single-tablet detection, and a displayed tablet name. No Connect/Refresh button or tablet/display selectors.
- User moves the normal window onto the tablet screen. Start selects its monitor by largest intersection and opens an opaque borderless full-screen preview.
- Crosshairs use raw target fractions of the full WPF viewport, independent of OTD output mode, cursor mapping, profiles, or filters. Full-area, aligned digitizer/display geometry is the supported setup.
- OTD remains the source of raw paired position/tilt. No GetSettings, profile snapshots, output-mode construction, settings polling, Resynchronize-based cancellation, or filter inspection.
- Pen absence clears stale readings after 500 ms and displays Waiting for pen. The current target is preserved and readings resume without restarting.
- Disconnect/tablet-change, display/DPI-change, overflow, stale queued work, and blocked consumers still stop acquisition. Bounded debug cleanup, including late enable replies, remains.
- The filter is still a pass-through shell. Actual capture bursts, data interpolation, candidate review, copying, and correction remain in later milestones. The revised future workflow reviews only current-session measurements, with user-owned paste/Save in OTD; it does not reload prior calibration or compare applied settings.

Main files: MainWindow.cs and PreviewWindow.cs (workflow/presentation), DisplayGeometry.cs (Windows monitor/full-screen placement), PreviewGeometry.cs (raw-to-viewport targets), TabletSelection.cs and DaemonConnection.cs (raw input source), AcquisitionSession.cs (processing/cleanup). AppliedConfiguration.cs was removed.

Covered spec areas: section 1 workflow/acquisition/mapping, section 2 preview geometry, relevant section 3 presentation/input behavior, section 5 OTD ownership, section 7 integration contracts. These are implementation changes, not claims of physical calibration accuracy.

## Validation

- Release build: **0 warnings, 0 errors**.
- **25 synthetic tests passed**, zero failures/skips. Tests cover single/ambiguous/changed tablets, content matching, paired and malformed reports, direct raw-to-viewport mapping at several DPI scales, pen waiting/resumption, overflow/blocked-parser cancellation, cleanup ordering/failures, and repeat-event timing.
- Removed tests for the intentionally deleted profile/settings/mapping restrictions were replaced with tests for the new behavior. The synthetic RPC server has no GetSettings method and exercises OTD initialization/report reconstruction using real OTD types.
- Installed-daemon **read-only test passed** on 2026-09-13: Huion Kamvas 13 (Gen 3). It read installation/tablet metadata only; no settings access or debug enablement.
- The first live-check request was rejected by automatic approval review because credits ran out. After the user resumed the task, the approved retry passed.
- Production C# decreased from 703 to 543 lines, about **23%**, including replacement monitor detection and automatic connection logic.
- Diff and whitespace checks performed. No commits, publishing, filter installation, OTD settings edits, or physical capture were performed.

Exact current validation commands, from the repository:

~~~powershell
dotnet build CalibrationSuite.sln -c Release --no-restore -p:BuildDate=2026-09-12
dotnet test tests/CalibrationSuite.Tests/CalibrationSuite.Tests.csproj -c Release --no-build --no-restore --filter 'Category!=LiveDaemon' --logger 'trx;LogFileName=milestone1.trx'
$env:CALIBRATION_LIVE_OTD = '1'
dotnet test tests/CalibrationSuite.Tests/CalibrationSuite.Tests.csproj -c Release --no-build --no-restore --filter Category=LiveDaemon --logger 'console;verbosity=detailed' --logger 'trx;LogFileName=live-daemon.trx'
Remove-Item Env:CALIBRATION_LIVE_OTD
git diff --check
~~~

Test results are in the ignored tests/CalibrationSuite.Tests/TestResults directory. Prior source preparation, locked restore, deterministic rebuild, and hidden startup checks passed for the earlier milestone 1 implementation. Those historical checks do not establish the new visible window placement or physical-device behavior.

## User review checks — pending

Launch from the repository:

~~~powershell
dotnet run --project src/CalibrationSuite.Calibrator -c Release --no-build --no-restore
~~~

1. **Automatic readiness.** Start with OTD stopped, then start it with the intended tablet connected. Expect the app to become ready without clicking Connect. Test zero/multiple tablets if available; expect an instruction to connect only the intended tablet.
2. **Full-screen placement.** Move the window to the tablet screen and Start. Expect no borders/title bar and coverage of the entire monitor, including its taskbar area. Try overlapping two monitors; the one containing most of the window should be used. Esc returns to the movable main window.
3. **Targets and raw readings.** Inspect all nine targets with arrows/R. The center target should be screen-centered; the grid uses a 15 mm active-digitizer inset. Hover and touch the physical tip to the crosshair. Expect responsive keyboard input and current raw position/tilt/pressure.
4. **Pen pause.** Lift beyond sensing range for more than two seconds. Expect Waiting for pen with the same active target, then live readings again on return. This is not a disconnect. Record upright and all four tilt directions before future angle guidance.
5. **Mapping independence.** Record and temporarily change OTD cursor mapping, then try preview again. Crosshair placement must remain unchanged. Disable other filters to reduce cursor distractions; raw debug measurements precede them. Restore mapping/filters afterward.
6. **Cancellation/failures.** Esc, repeated start/cancel, main-window close during preview, tablet unplug, and daemon restart should end acquisition cleanly. If debug cleanup is unconfirmed, restart daemon and Calibrator. Do not use OTD's debugger concurrently.
7. **Geometry/DPI.** Change display resolution/scaling/layout during preview. Expect cancellation and fresh full-screen geometry on the next Start. Check negative monitor origins and mixed DPI where available; restore Windows display settings afterward.

## Outstanding

**Verified on device:** only read-only detection of the actual tablet through its running daemon. No physical pen/contact/aiming/tilt-accuracy check has been performed.

**Not yet verified:** new visible window/full-screen placement, monitor-overlap selection, real pen pause/resume/contact, geometry/DPI changes, actual debug cleanup during physical disconnect, and all orientation semantics. These remain user review checks, not passing automated checks.

Exact HidSharpCore package notice verification and final dependency distribution remain pending for delivery. SPEC.md retains mathematical validation/capture-quality requirements and excludes guided accuracy-testing UI.

Milestone 1's physical/device checks remain pending and are carried into milestone 2's review record.
