# Calibration Suite implementation plan

## Authority and execution rules

`SPEC.md` governs requirements, formulas, defaults, and exclusions; this plan defines implementation order and review gates. Read the current spec before starting/resuming. Never weaken or edit it to suit implementation. Raise contradictions or material behavior decisions before affected work.

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
- Direct RPC initialization/report reconstruction via OTD's plugin manager; tablet selection by reference contents and applied-profile resolution. Diagnose unsupported reports and indistinguishable devices.
- Serialized background acquisition: bounded queues, monotonic timing, cancellation, debug cleanup, disconnect/tablet-change/resynchronization handling, and periodic settings checks. Add burst-boundary checks in milestone 4.
- Mapping preflight, OTD's public transformation matrix, physical-display selection, and Windows/WPF coordinate adapter. Restart on geometry changes with refreshed geometry.
- Minimal main screen and full-screen acquisition preview for checking future capture UI. Enable preview debug acquisition only in explicitly started capture setup; cancel on exit. Show tablet/profile, target, and latest raw position/tilt. Angle guidance requires verified device semantics.

**Automated checks:** build, tablet-reference matching, reconstruction failures, overflow/stall cancellation, debug cleanup, settings changes, and coordinate conversion across monitor origins/DPI. Use applicable real OTD types; record pending daemon/device verification.

**User checks:** connect the intended tablet; verify profile, targets, live readings, pen contact, cancellation, disconnect, and display/settings changes. Confirm upright and all four tilt directions before using angle guidance. Record unsupported behavior and untested monitor configurations.

**Gate:** stop for review of integration and target placement.

## Milestone 2 — Calibration data and correction core

**Outcome:** deterministic, mathematically verified correction from measured data, independent of UI.

Implement:

- Version 2 measured/identity models, schema/geometry validation, and diagnostics. Preserve version 1 until replacement/reset; never recover measurements from coefficients.
- Spec section 2 grid/pose generation and settings validation, including resolved coordinates and measured orientation semantics.
- Deterministic node triangulation, capture separation, hull checks, barycentric interpolation, and nearest-boundary clamping. Distinguish captured-upright checks from origin coverage required for every loaded mesh.
- Lookup using original raw position, four independent node evaluations, bilinear error blending, and correction; support nonuniform grids and differing sample counts.
- Immutable prepared snapshots, compact serialization, and decoded-content comparison for OTD status. Exclude guidance metadata from interpolation/runtime behavior.

**Automated checks:** schema/version failures, geometry tolerance, grid/default pose counts, node vertex reproduction, triangle/grid continuity, hull projection, sample-order determinism, usable collinear/cocircular cases, invalid meshes, identity, and large-offset/gradient spatial residuals. Use small independently calculated fixtures. Confirm lookup-only clamping and no movement of measurements to requested poses.

**User checks:** review example measurements, expected/actual correction, and rejection diagnostics. No physical calibration required; device accuracy remains unverified.

**Gate:** stop for review of data handling and mathematical behavior.

## Milestone 3 — OTD correction filter and package

**Outcome:** standard OTD plugin loading/property workflow applies the shared evaluator.

Implement:

- Full positioned pipeline contract, plugin metadata, `CalibrationJson` property/tooltip, tablet injection, and dependency-load snapshot preparation.
- Position-only mutation and exactly-once emission of the original report, preserving type, other fields, event order, and boxed-struct behavior. Implement all spec section 4 pass-through cases.
- Identity fallback and diagnostics for invalid data, initialization failures, and persistent unsupported input. No expensive report-path work or per-report logging.
- Standard package and OTD installation instructions. Diagnose selected-profile entry presence, enablement, duplication, and applied stored-order compatibility; unknown behavior means unverified compatibility.

**Automated checks:** injection/activation, valid/invalid payloads, identity, paired-report eligibility, concrete report preservation, event order, eraser/proximity handling, and filter-order classification. Inspect report-path operations/allocations for prohibited work.

**User checks:** install via Plugin Manager; verify property/tooltip, enable/bypass, clear-to-identity, and Save/Apply. Confirm harmless earlier filters are allowed and conflicting earlier modifiers diagnosed. Initially use identity data live; fictional measurements stay in tests. Check measured correction after capture becomes available.

**Gate:** stop for review of plugin installation and runtime integration.

## Milestone 4 — Guided capture and candidate review

**Outcome:** complete calibration capture, review/recapture, acceptance, and copying into OTD.

Implement:

- Capture settings/defaults, total-count preview, traversal order, active-target display, and verified angle guidance/gates.
- Burst state machine: keyboard trigger, eligible paired reports, per-report gates, duration/sample minima, timeouts, medians, and specified percentile stability checks. Check settings before burst start/acceptance using milestone 1 integration.
- Responsive full-screen keyboard control ignoring pen/mouse/touch UI input; latest one-decimal readings and readiness/retry feedback.
- Separate unfinished session, candidate, and working states; node/pose recapture, restart/cancel/discard, acceptance, pending-close handling, and optional guidance recovery for loaded samples.
- Main-screen states, accepted-data copying, explicit pending-data choices on refresh/tablet switch, and applied-content comparison with Save reminders. Distinguish applied settings, disk persistence, and runtime evidence.

**Automated checks:** gates/timers, identical repeat events, even medians, percentile boundaries, no stale tilt or pressure trigger, failed-burst rejection, keyboard overlap suppression, recapture preservation, candidate/working transitions, pending-data choices, and clipboard/status semantics.

**User checks:** capture a valid small layout; test every keyboard action/retry path. Discard without losing working data, then accept/copy another candidate. Paste/Save in OTD, refresh, and verify status. Check contact responsiveness and settings-change cancellation. Review/recapture loaded calibration as well as new candidates. Try actual pointer behavior with correction enabled and bypassed; restore other filters after isolation checks. No guided accuracy-testing UI or validated/unvalidated status is included.

**Gate:** stop for review of capture usability and data preservation.

## Milestone 5 — Requirement audit and delivery

**Outcome:** reproducible filter package and separately launched Calibrator satisfying the initial-release spec, with verified behavior and documented device limits.

Complete:

- Audit every initial-release requirement in spec sections 1–6 against implementation/evidence; recheck integration against section 7. Record a requirement-level checklist beyond the coverage map below.
- Fix omissions/defects, remove temporary scaffolding, complete build/install/use documentation, and check both deliverables' dependency distribution/licenses.
- Run the relevant full suite and package/build checks. Verify install/update/removal, settings/preset round-trips, clear/bypass, profile selection, failed-plugin diagnostics, and applied-versus-saved status.
- Revisit pending device checks: orientation semantics, capture defaults, manual pointer behavior between captured positions/tilts and near hull boundaries, monitor DPI/origins, and sampling/rendering during contact. Retain spec limits on identity, settings polling, and physical accuracy claims.

**User checks:** follow documented installation/calibration, verify correction/bypass, restart OTD/Calibrator to check saved data, and test OTD settings backup/restore. Review accuracy evidence and unverified configurations.

**Gate:** stop for final acceptance. Completion requires resolved work/checks and recorded user acceptance and verification evidence.

## Deferred beyond initial release

Guided accuracy-testing sessions and their comparison reports, test-pose generation, and validation state are deferred under spec section 6. They are not an implementation milestone or delivery gate. Retain capture quality checks, data validation, automated math tests, and manual OTD/device checks. Specify any future testing feature separately before scheduling implementation.

## Coverage map

| Spec area | Primary milestones |
| --- | --- |
| 1: Components, RPC, mapping, pipeline, profiles, initialization | 1, 3, 4, 5 |
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
| 1: Build, acquisition, mapping | Not started | — | — |
| 2: Data and correction core | Not started | — | — |
| 3: Filter and package | Not started | — | — |
| 4: Capture and review | Not started | — | — |
| 5: Audit and delivery | Not started | — | — |

Current stop: plan written; awaiting user review and authorization for milestone 1.
