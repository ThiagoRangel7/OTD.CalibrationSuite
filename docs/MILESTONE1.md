# Milestone 1 review — 2026-09-13

**Approved to advance.** After reviewing the simplified workflow, the user explicitly requested "Implement milestone 2" on 2026-09-13. The physical checklist was subsequently reported passing by the user on 2026-09-13; details and remaining limits are recorded below. The following implementation and validation evidence describes milestone 1; see [milestone 2](MILESTONE2.md) for subsequent core work.

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
dotnet build CalibrationSuite.slnx -c Release --no-restore
dotnet test tests/CalibrationSuite.Tests/CalibrationSuite.Tests.csproj -c Release --no-build --no-restore --filter 'Category!=LiveDaemon' --logger 'trx;LogFileName=milestone1.trx'
$env:CALIBRATION_LIVE_OTD = '1'
dotnet test tests/CalibrationSuite.Tests/CalibrationSuite.Tests.csproj -c Release --no-build --no-restore --filter Category=LiveDaemon --logger 'console;verbosity=detailed' --logger 'trx;LogFileName=live-daemon.trx'
Remove-Item Env:CALIBRATION_LIVE_OTD
git diff --check
~~~

Test results are in the ignored tests/CalibrationSuite.Tests/TestResults directory. Prior source preparation, locked restore, deterministic rebuild, and hidden startup checks passed for the earlier milestone 1 implementation. Those historical checks do not establish the new visible window placement or physical-device behavior.

## User review checks

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

**User-verified on 2026-09-13:** the user reported completing the conversational physical checklist with everything working as intended on their Huion Kamvas 13 (Gen 3): automatic connection and full-screen placement, targets/live readings and contact responsiveness, pen absence/resumption, tilt directions, repeated exit/restart, and disconnect recovery. They reported all tests passing, including the suggested display-setting check; individual resolution/scaling configurations were not specified. This is user-reported verification, not an automated or assistant-observed device test.

**Not established by that report:** exhaustive monitor-overlap/negative-origin/mixed-DPI configurations, zero/multiple-device setups, deliberate OTD mapping changes, the numeric angular scale at a known physical lean, diagonal angle conversion, or calibration accuracy. These broader cases are not implied by the basic checklist passing.

Exact HidSharpCore package notice verification and final dependency distribution remain pending for delivery. SPEC.md retains mathematical validation/capture-quality requirements and excludes guided accuracy-testing UI.

The basic physical checklist is now user-verified. Carry only the additional configurations and accuracy/angle-scale checks above forward.

## Recorded tilt directions and OTD source trace

The user kept the tip on the display and reported these raw values:

| Eraser direction | TiltX | TiltY | Spec azimuth |
| --- | ---: | ---: | ---: |
| Left | 50 | 0 | 0 degrees |
| Right | -50 | 0 | 180 degrees |
| Top | 0 | -50 | 270 degrees |
| Bottom | 0 | 50 | 90 degrees |
| Upright | 0 | 0 | unavailable |

In the adjacent OTD 0.6.7 tree, `OpenTabletDriver.Configurations/Configurations/Huion/Kamvas 13 (Gen 3).json` selects `OpenTabletDriver.Configurations.Parsers.Huion.GianoReportParser`. Its pen-report branch constructs `GianoReport`, which reads signed bytes 10 and 11 and negates both for TiltX/TiltY. The configuration supplies parser selection, not physical direction or angle-scale metadata; `ITiltReport` exposes only a Vector2 with no documented physical axis convention.

These observations establish horizontal sign reversal and matching vertical sign relative to our screen-aligned tip-to-eraser components. Assuming the reported axes are angular components in degrees, guidance should receive `sx = -tan(TiltX)` and `sy = tan(TiltY)` (convert degrees to radians for tan). The user did not measure a known 50-degree physical lean, so the readings establish directions, not angular-scale accuracy. Raw tilt used for stored samples and correction remains unchanged. No device-specific runtime adapter was added during this documentation update.
