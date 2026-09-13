# Calibration Suite implementation plan

## Authority and execution rules

`SPEC.md` governs requirements, formulas, defaults, and exclusions; this plan defines implementation order and review gates. Read the current spec before starting/resuming. Do not change it to suit implementation; revise it only for explicitly authorized requirement changes, such as the simplified display-tablet workflow approved on 2026-09-13. Raise contradictions or material behavior decisions before affected work.

**Implement one milestone at a time; stop for explicit user approval before advancing.** Passing builds, silence, status questions, and feedback are not approval. Fix review findings within the current milestone and resubmit. Work autonomously within its authorized scope; resolve routine choices without asking. This plan does not authorize milestone 1.

Before each milestone:

1. Inspect the working tree and applicable instructions; preserve user changes.
2. Confirm preceding approval and read recorded findings.
3. Identify requirements, dependencies, work, and acceptance checks. Include necessary prerequisites; exclude speculative future features.

Verify integration contracts against `../OpenTabletDriver-0.6.7` sources in spec section 7. Inspect `../Sample Plugins` for examples, packaging conventions, and implementation ideas, not as authoritative guidance or presumed best practice. Evaluate any borrowed approach against this spec, OTD 0.6.7 contracts, correctness, maintainability, and runtime cost; choose a better approach when warranted. Adjacent repositories are references only: do not modify them or fork OTD. Check dependency API compatibility, licenses, and deployment requirements; record choices. Use a robust deterministic geometry library.

Keep WPF/RPC outside the shared core; use one evaluator for the filter and automated tests. OTD owns settings/persistence. The Calibrator's only daemon mutation is temporary `SetTabletDebug` acquisition. Retain the spec's other architectural exclusions.

## Verification and review protocol

Deliver each milestone buildable and reviewed: run relevant checks, fix failures, and inspect the diff. Test mathematical invariants, report handling, state transitions, and integration boundaries against independent expectations. Label synthetic reports/fixtures; they cannot establish hardware accuracy. Add no permanent diagnostic app or calibration-file workflow solely for tests.

At every stop, update the progress record below and provide:

- Implemented behavior, covered spec sections, and files/artifacts.
- Exact build/test/launch commands used and results.
- Numbered user checks: prerequisites, expected outcomes, and temporary OTD setup cleanup.
- Defects, deferred requirements, and pending tablet/display checks.
- Explicit notice that implementation is paused for review.

Distinguish **implemented**, **verified automatically**, **verified on device**, and **not yet verified**. Never mark unperformed checks passed. Record approval to advance with outstanding checks and carry them forward; approval is not verification. Final completion requires resolving required checks and known defects.

Document working build/run commands in a concise README. Keep plan status/evidence sufficient to resume without conversation memory. Milestone completion alone does not authorize commits, publishing, or releases.

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

- Version 2 measured/identity models, schema/geometry validation, and diagnostics. Preserve version 1 until replacement/reset; never recover measurements from coefficients.
- Spec section 2 grid/pose generation and settings validation, including resolved coordinates and measured orientation semantics.
- Deterministic node triangulation, capture separation, hull checks, barycentric interpolation, and nearest-boundary clamping. Distinguish captured-upright checks from origin coverage required for every loaded mesh.
- Lookup using original raw position, four independent node evaluations, bilinear error blending, and correction; support nonuniform grids and differing sample counts.
- Immutable prepared snapshots and compact serialization. Exclude guidance metadata from interpolation/runtime behavior.

**Automated checks:** schema/version failures, geometry tolerance, grid/default pose counts, node vertex reproduction, triangle/grid continuity, hull projection, sample-order determinism, usable collinear/cocircular cases, invalid meshes, identity, and large-offset/gradient spatial residuals. Use small independently calculated fixtures. Confirm lookup-only clamping and no movement of measurements to requested poses.

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
| 1: Build, acquisition, mapping | Awaiting user review | Approved simpler workflow implemented; clean Release build, 25 synthetic tests, installed-daemon read-only detection passed. See docs/MILESTONE1.md for commands and pending device/UI checks. | User explicitly authorized the workflow revision and implementation on 2026-09-13; milestone 2 not approved. |
| 2: Data and correction core | Not started | — | — |
| 3: Filter and package | Not started | — | — |
| 4: Capture and review | Not started | — | — |
| 5: Audit and delivery | Not started | — | — |

Current stop: reviewing the explicitly approved 2026-09-13 simplified display-tablet workflow within milestone 1. No approval to advance to milestone 2. See docs/MILESTONE1.md for current evidence and pending physical-device checks.
