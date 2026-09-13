# Calibration Suite implementation plan

## Authority and execution rules

`SPEC.md` guides product behavior; this plan records implementation order and progress. Prefer simpler approaches when they preserve important behavior, and update these documents when a better approach replaces an earlier design. The user's 2026-09-13 simplification request explicitly authorizes this judgment. Avoid speculative features, defensive layers, and tests that merely repeat implementation details.

Milestones describe feature scope. The current simplification pass covers implemented milestones 1 and 2; it does not start the unfinished filter or capture features. Work autonomously within the user's authorized scope.

Before work, inspect the working tree and recorded findings, and identify the relevant behavior and checks. Committed code, uncommitted changes, tests, and these documents are all open to improvement under the user's simplification request; none is a design constraint merely because it already exists.

Verify integration contracts against `../OpenTabletDriver-0.6.7` sources in spec section 7. Inspect `../Sample Plugins` for examples, packaging conventions, and implementation ideas, not as authoritative guidance or presumed best practice. Evaluate any borrowed approach against this spec, OTD 0.6.7 contracts, correctness, maintainability, and runtime cost; choose a better approach when warranted. Adjacent repositories are references only: do not modify them or fork OTD. Check dependency API compatibility, licenses, and deployment requirements; record choices. Use a robust deterministic geometry library.

Keep WPF/RPC outside the shared core; use one evaluator for the filter and automated tests. OTD owns settings/persistence. The Calibrator's only daemon mutation is temporary `SetTabletDebug` acquisition. Retain the spec's other architectural exclusions.

## Verification and review protocol

Run relevant checks, fix failures, and inspect the diff. Prefer existing tests and independent expectations for math, report handling, and integration boundaries. Synthetic fixtures cannot establish hardware accuracy; retain outstanding device checks without claiming they passed.

Keep build/run instructions in README and record meaningful changes, verification results, and unresolved limitations here or in the milestone notes. Add manual checks when behavior needs them. Avoid repeating unchanged review checklists. Commits, publishing, and releases require user authorization.

## Milestone 1 — Build, acquisition, and target mapping

**Outcome:** OTD-connected Calibrator displaying mapped targets and live parsed pen readings; capture comes later.

Implement:

- Solution with shared core, OTD filter, WPF Calibrator, and test projects; compatible OTD 0.6.7 references and reproducible builds. A pass-through filter shell suffices until milestone 3.
- Automatic RPC connection/report reconstruction via OTD's plugin manager; require one tablet, display its name, and match report reference contents. Diagnose unsupported reports and zero/multiple devices.
- Serialized acquisition: bounded queues, monotonic timing, cancellation, debug cleanup, and disconnect/tablet-change handling. Pen absence waits without ending the session. No settings reads/polling or cancellation on settings/resynchronization alone.
- User moves the window to the tablet screen; Start uses its monitor and opens full-screen. Map raw target fractions to WPF viewport dimensions independently of OTD settings. Restart on geometry/DPI changes with refreshed monitor bounds.
- Minimal main screen and full-screen acquisition preview for checking future capture UI. Enable preview debug acquisition only in explicitly started capture setup; cancel on exit. Show detected tablet, target, raw position/tilt, and Waiting for pen when absent. Angle guidance requires verified device semantics.

**Automated checks:** build, tablet-reference matching, reconstruction failures, overflow/consumer-stall cancellation, debug cleanup, pen absence/resumption, and raw-to-viewport coordinate conversion at different DPI scales. Verify native monitor placement, negative origins, and mixed DPI in the user checks. Use applicable real OTD types; record pending daemon/device verification.

**User checks:** connect the intended tablet; verify automatic detection, targets, readings, pen lifting/contact, cancellation, disconnect, and display changes. OTD mapping changes must not affect targets. Confirm upright and all four tilt directions before using angle guidance. Record unsupported behavior and untested monitor configurations.

**Gate:** stop for review of integration and target placement.

## Milestone 2 — Calibration data and correction core

**Outcome:** deterministic, mathematically verified correction from measured data, independent of UI.

Implement:

- Measured/identity models, schema/geometry validation, and diagnostics. Support only the current unreleased data structure; no migration or backward-compatibility code.
- Spec section 2 grid/pose generation and settings validation, including resolved coordinates and measured orientation semantics.
- Deterministic node triangulation, capture separation, hull checks, barycentric interpolation, and nearest-boundary clamping. Distinguish captured-upright checks from origin coverage required for every loaded mesh.
- Lookup using original raw position, four independent node evaluations, bilinear error blending, and correction; support nonuniform grids and differing sample counts.
- Immutable prepared snapshots and compact serialization. Exclude guidance metadata from interpolation/runtime behavior.

**Automated checks:** schema failures, geometry tolerance, grid/default pose counts, node vertex reproduction, triangle/grid continuity, hull projection, sample-order determinism, usable collinear/cocircular cases, invalid meshes, identity, and large-offset/gradient spatial residuals. Use small independently calculated fixtures. Confirm lookup-only clamping and no movement of measurements to requested poses.

**User checks:** review example measurements, expected/actual correction, and rejection diagnostics. No physical calibration required; device accuracy remains unverified.

**Gate:** stop for review of data handling and mathematical behavior.

## Milestone 3 — OTD correction filter and package

**Outcome:** standard OTD plugin loading/property workflow applies the shared evaluator.

Implement:

- Full positioned pipeline contract, plugin metadata, `CalibrationJson` property/tooltip, tablet injection, and dependency-load snapshot preparation.
- Position-only mutation and exactly-once emission of the original report, preserving type, other fields, event order, and boxed-struct behavior. Implement all spec section 4 pass-through cases.
- Identity fallback and diagnostics for invalid data, initialization failures, and persistent unsupported input. No expensive report-path work or per-report logging.
- Standard package and OTD installation instructions. Document the single enabled entry and filter ordering in OTD; do not build Calibrator profile/filter inspection.

**Automated checks:** injection/activation, valid/invalid payloads, identity, paired-report eligibility, concrete report preservation, event order, eraser/proximity handling, and PreTransform stage. Inspect report-path operations/allocations for prohibited work.

**User checks:** install via Plugin Manager; verify property/tooltip, enable/bypass, clear-to-identity, and Save/Apply. Follow documented filter-order instructions; the Calibrator does not classify or enforce other filters. Initially use identity data live; fictional measurements stay in tests. Check measured correction after capture becomes available.

**Gate:** stop for review of plugin installation and runtime integration.

## Milestone 4 — Guided capture and candidate review

**Outcome:** complete calibration capture, review/recapture, acceptance, and copying into OTD.

### Capture follow-ups from milestone 2 review

Retain the full tilt interpolation approach following the 2026-09-13 discussion. The upright-only core extension is complete; carry the remaining UI and capture items forward as marked below:

- **Requested capture order:** upright, then all altitude levels for azimuth 1, all levels for azimuth 2, and so on, before the next crosshair. Update the spec traversal sentence, CreatePoses, and order tests together when implementing capture. This changes guidance order only, not mesh construction.
- **Upright-only core implemented in milestone 2:** zero non-upright altitude levels or zero azimuths generate only upright. A single measured sample supplies a constant node error through the existing spatial blend and current schema; multi-sample nodes keep their mesh checks. Tests cover loading, evaluation, invalid counts/data, and mixed nodes. Expose the controls and enforce upright pose capture in milestone 4.
- **Confirmed burst acceptance:** retain the current spec: every eligible report must satisfy pose tolerance, followed by burst medians and position/tilt stability checks. Keep gap/duration/sample limits. The proposed median-only pose gate was declined.
- **Presentation idea for later evaluation:** an arrow continuing from the pen tip toward a target point/sphere. Work out depth readability and explicitly reconcile its eraser-to-tip direction with the spec's tip-to-eraser azimuth convention. No 3D UI or rendering dependency is committed by this note.

Implement:

- Capture settings/defaults, total-count preview, traversal order, active-target display, and verified angle guidance/gates.
- Burst state machine: keyboard trigger, eligible paired reports, per-report gates, duration/sample minima, timeouts, medians, and specified percentile stability checks. A report gap rejects only the active burst; pen absence between bursts preserves progress.
- Responsive full-screen keyboard control ignoring pen/mouse/touch UI input; latest one-decimal readings and readiness/retry feedback.
- Separate unfinished session, candidate, and working states; node/pose recapture, restart/cancel/discard, acceptance, pending-close handling.
- Current-session review/accept/discard, accepted-data copying, and paste-and-Save reminders. No saved calibration loading, selectors, refresh workflow, or applied-content comparison.

**Automated checks:** gates/timers, identical repeat events, even medians, percentile boundaries, no stale tilt or pressure trigger, failed-burst rejection, keyboard overlap suppression, recapture preservation, candidate/working transitions, close-with-session-data reminders and clipboard semantics.

**User checks:** capture a valid small layout; test every keyboard action/retry path. Discard without losing working data, then accept/copy another candidate. Paste/Save in OTD. Check contact responsiveness, waiting between bursts, and display-change cancellation. Review/recapture current-session measurements. Try actual pointer behavior with correction enabled and bypassed; restore other filters after isolation checks. No guided accuracy-testing UI or validated/unvalidated status is included.

**Gate:** stop for review of capture usability and data preservation.

## Milestone 5 — Requirement audit and delivery

**Outcome:** reproducible filter package and separately launched Calibrator satisfying the initial-release spec, with verified behavior and documented device limits.

Complete:

- Audit every initial-release requirement in spec sections 1–6 against implementation/evidence; recheck integration against section 7. Record a requirement-level checklist beyond the coverage map below.
- Fix omissions/defects, remove temporary scaffolding, complete build/install/use documentation, and check both deliverables' dependency distribution/licenses.
- Run the relevant full suite and package/build checks. Verify install/update/removal, settings/preset round-trips, clear/bypass, user-selected destination profiles and OTD failed-plugin diagnostics.
- Revisit pending device checks: orientation semantics, capture defaults, manual pointer behavior between captured positions/tilts and near hull boundaries, monitor DPI/origins, and sampling/rendering during contact. Retain spec limits on identity, supported full-area/aligned-display setup, and physical accuracy claims.

**User checks:** follow documented installation/calibration, verify correction/bypass, restart OTD to check its saved calibration (the Calibrator does not reload it), and test OTD settings backup/restore. Review accuracy evidence and unverified configurations.

**Gate:** stop for final acceptance. Completion requires resolved work/checks and recorded user acceptance and verification evidence.

## Deferred beyond initial release

Guided accuracy-testing sessions and their comparison reports, test-pose generation, and validation state are deferred under spec section 6. They are not an implementation milestone or delivery gate. Retain capture quality checks, data validation, automated math tests, and manual OTD/device checks. Specify any future testing feature separately before scheduling implementation.

## Coverage map

| Spec area | Primary milestones |
| --- | --- |
| 1: Components, automatic RPC, full-screen mapping, pipeline, initialization | 1, 3, 4, 5 |
| 2: Measurements, settings, grid, orientation | 1, 2, 4 |
| 3: Capture, review, UI | 4 |
| 4: Interpolation and runtime | 2, 3 |
| 5: Schema, persistence, application, status | 2, 3, 4, 5 |
| 6: Required verification checks | 2–5 |
| 7: OTD source verification | 1, 3, 5; revisit as needed |

## Progress record

States: Not started → In progress → Awaiting user review → Approved. Review fixes return that milestone to In progress. Record explicit approval evidence before advancing.

| Milestone | State | Evidence / findings | User approval |
| --- | --- | --- | --- |
| 1: Build, acquisition, mapping | Approved | Simplified workflow implemented; 25 synthetic tests and read-only device detection passed at that milestone. Basic physical checklist reported passing by the user on 2026-09-13; cardinal tilt signs recorded. Additional configurations and angle-scale/accuracy checks remain in docs/MILESTONE1.md. | User explicitly requested "Implement milestone 2" on 2026-09-13. Approval to advance is not device verification. |
| 2: Data and correction core | Awaiting user review | Calibration data, layout, immutable measured meshes, and correction evaluator implemented. Locked restore, Release build, and 93 synthetic tests passed. See docs/MILESTONE2.md. | Implementation explicitly requested on 2026-09-13; milestone 3 not approved. |
| 3: Filter and package | Not started | — | — |
| 4: Capture and review | Not started | — | — |
| 5: Audit and delivery | Not started | — | — |

Current stop: milestone 2 review. No approval to advance to milestone 3. See docs/MILESTONE2.md for core verification and carried-forward physical-device checks.

2026-09-13 simplification review:

- Removed the second Delaunay in-circle audit and format/version machinery. Retained sample/triangle/hull validation and invalid-data identity fallback.
- Replaced acquisition polling with direct channel reads and a processing-only stall timer. Parser timeout no longer depends on RPC timeout. Ordered debug cleanup remains independent of the worker; one `RestartRequired` flag describes whether retry is allowed.
- One async click handler owns preview startup through cleanup, with a single running flag instead of a separately stored acquisition task and window-level preview field. Closing during startup now also waits for that operation. Display cancellation closes the preview after bounded cleanup.
- Prepared nodes use one rectangular array. Removed unused readiness state, exception wrapping, and small redundant allocations; target totals come from the layout.
- Updated existing tests for restart behavior and buffered-parser cleanup, and removed compatibility-specific test cases and the constant-string assertion. Release solution build passed with zero warnings/errors; all 93 synthetic cases passed using the README commands. No dependencies or features added. Basic physical checks subsequently passed per user report; additional configurations and accuracy checks remain.

- Review fixes: RunAsync dispatches the session to the thread pool so daemon-disconnect cleanup does not await the calling UI context. Cleanup-failure instructions explicitly require restarting OTD and Calibrator. A paused-context regression test and recovery-message assertion failed before these fixes and passed afterward.

2026-09-13 device follow-up: user reports the physical checklist passing. For Huion Kamvas 13 (Gen 3), eraser left/right/top/bottom gives (50,0)/(-50,0)/(0,-50)/(0,50), upright (0,0). OTD configuration selects GianoReportParser, whose GianoReport negates signed bytes 10 and 11. Carry this source trace and observed sign convention into milestone 4 guidance; known-angle scale and diagonal conversion remain to verify. No milestone 3 implementation was started by this update.
