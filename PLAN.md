# Calibration Suite implementation plan

## Authority and execution rules

`SPEC.md` guides product behavior; this plan records implementation order and progress. Prefer simpler approaches when they preserve important behavior, and update these documents when a better approach replaces an earlier design. The user's 2026-09-13 simplification request explicitly authorizes this judgment. Avoid speculative features, defensive layers, and tests that merely repeat implementation details.

Milestones describe feature scope. The 2026-10-04 request "Implement milestone 4 cleanly" authorized guided capture and review following the successful milestone 3 checks. The subsequent request authorized committing that implementation and documenting a new visual/UX milestone 5. Milestone 4 is checkpointed at `2ec8a32`; the user then requested "implement milestone 5", authorizing its visual implementation. Milestone 5 is implemented and awaiting user review. Requirement audit and delivery remain milestone 6.

Before work, inspect the working tree and recorded findings, and identify the relevant behavior and checks. Committed code, uncommitted changes, tests, and these documents are all open to improvement under the user's simplification request; none is a design constraint merely because it already exists.

Verify integration contracts against `../OpenTabletDriver-0.6.7` sources in spec section 7. Inspect `../Sample Plugins` for examples, packaging conventions, and implementation ideas, not as authoritative guidance or presumed best practice. Evaluate any borrowed approach against this spec, OTD 0.6.7 contracts, correctness, maintainability, and runtime cost; choose a better approach when warranted. Adjacent repositories are references only: do not modify them or fork OTD. Check dependency API compatibility, licenses, and deployment requirements; record choices. Use a robust deterministic geometry library.

Keep WPF/RPC outside the shared core; use one evaluator for the filter and automated tests. OTD owns settings/persistence. The Calibrator's only daemon mutation is temporary `SetTabletDebug` acquisition. Retain the spec's other architectural exclusions.

## Verification and review protocol

Run relevant checks, fix failures, and inspect the diff. Prefer existing tests and independent expectations for math, report handling, and integration boundaries. Synthetic fixtures cannot establish hardware accuracy; retain outstanding device checks without claiming they passed.

Keep build/run instructions in README and record meaningful changes, verification results, and unresolved limitations here or in the milestone notes. Add manual checks when behavior needs them. Avoid repeating unchanged review checklists. Commits, publishing, and releases require user authorization.

## Repository optimization — 2026-10-12

The user requested a full optimization, cleanup, and simplification pass. The initial review covered the first-party core, filter, acquisition, WPF screens, tests, dependency preparation, and packaging. That pass introduced direct OTD reference comparison, cached mesh arithmetic/bounds, reusable burst-statistics storage, constant-time capture counts, accepted-payload snapshots, shared pose counts, cached guidance hit-test geometry, automatic WPF card sizing, and incremental filter packaging. The final simplification replaces the manual reference comparer and repeated export validation, as recorded below.

At that point, Debug and Release builds and 175 non-live tests passed. The suite covered all bundled tablet configurations, complete serialized reference contents, the manual comparer's allocation-free matching, recapture bookkeeping, accepted snapshots, unrelated report rejection, reusable burst statistics, and oblique hull projection. Offscreen visual/input, locked restore, and package regeneration checks passed. Final counts and the matcher tradeoff are in the [measured results and verification](docs/OPTIMIZATION.md).

This pass retains the current product behavior and dependency versions. Milestone 5's user review and the outstanding physical capture/correction checks remain pending; optimization measurements do not establish hardware accuracy.

The first readability pass built mesh caches during validation, used the library's bounds type, replaced numeric burst selectors with named selectors, treated requested poses as immutable values, replaced allocated capture itineraries with contiguous range bounds, and simplified WPF cards/cache invalidation. At that stage, production C# was 18 lines smaller than the optimization pass and synthetic burst allocation fell from 2,656 to 80 bytes. The final simplification keeps the interpolation/burst work, removes added wrappers and internal guards, and accepts JSON matching allocation for less code. Details are in the optimization report.

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

- **Capture order implemented:** upright, then all altitude levels for azimuth 1, all levels for azimuth 2, and so on, before the next crosshair. Spec traversal, CreatePoses, and order tests now agree. This changes guidance order only, not mesh construction.
- **Upright-only capture implemented:** zero non-upright altitude levels or zero azimuths generate only upright. A single measured sample supplies a constant node error through the existing spatial blend and current schema; multi-sample nodes keep their mesh checks. Tests cover loading, evaluation, invalid counts/data, and mixed nodes. Milestone 4 exposes the controls and enforces the upright pose gate.
- **Confirmed burst acceptance:** retain the current spec: every eligible report must satisfy pose tolerance, followed by burst medians and position/tilt stability checks. Keep gap/duration/sample limits. The proposed median-only pose gate was declined.
- **Presentation implemented in milestone 5:** a thin native WPF arrow continues from the physical aiming crosshair into an unfilled target tolerance region. The user's 2026-10-05 clarification replaces the separate corner 3D/top-view scene. The beyond-nib direction explicitly reverses the existing tip-to-eraser guidance convention; see docs/MILESTONE5.md for the mapping and checks.

Implement:

- Capture settings/defaults, total-count preview, traversal order, active-target display, and verified angle guidance/gates.
- Burst state machine: keyboard trigger, eligible paired reports, per-report gates, duration/sample minima, timeouts, medians, and specified percentile stability checks. A report gap rejects only the active burst; pen absence between bursts preserves progress.
- Responsive full-screen keyboard control ignoring pen/mouse/touch UI input; latest one-decimal readings and readiness/retry feedback.
- Separate unfinished session, candidate, and working states; node/pose recapture, restart/cancel/discard, acceptance, pending-close handling.
- Current-session review/accept/discard, accepted-data copying, and paste-and-Save reminders. No saved calibration loading, selectors, refresh workflow, or applied-content comparison.

**Automated checks:** gates/timers, identical repeat events, even medians, percentile boundaries, no stale tilt or pressure trigger, failed-burst rejection, keyboard overlap suppression, recapture preservation, candidate/working transitions, close-with-session-data reminders and clipboard semantics.

**User checks:** capture a valid small layout; test every keyboard action/retry path. Discard without losing working data, then accept/copy another candidate. Paste/Save in OTD. Check contact responsiveness, waiting between bursts, and display-change cancellation. Review/recapture current-session measurements. Try actual pointer behavior with correction enabled and bypassed; restore other filters after isolation checks. No guided accuracy-testing UI or validated/unvalidated status is included.

**Gate:** stop for review of capture usability and data preservation.

## Milestone 5 — Visual guidance and UI usability

**Outcome:** readable setup and capture screens that make calibration easy to follow, with shorter instructions, clear visual hierarchy, and precise arrow/target guidance integrated into the aiming crosshair.

The user's milestone 4 review identified too much instructional text at the top of the setup window and too much text during full-screen capture. The clarification is explicit: retain relevant controls and options, and improve their presentation. Replacing them with a few presets or hiding them behind additional disclosure is not the requested solution. See [milestone 5 design brief](docs/MILESTONE5.md).

Implement:

- Shorten the setup introduction to a brief actionable instruction. Organize connection readiness, grid settings, pose settings, sampling/quality controls, capture count, and actions using clear headings, spacing, alignment, labels, and units. Keep relevant inputs visible and easy to find; compact layout and scrolling handle smaller windows. Preserve the existing configuration capabilities.
- Make full-screen capture primarily visual: a prominent active crosshair, current versus requested orientation, distinct ready/capturing/accepted/retry cues, burst progress, overall progress, and compact keyboard hints. Use a short contextual message for failures and waiting. Keep required readings legible in a secondary region without obscuring the physical aiming point.
- Follow the user's 2026-10-05 clarification: the top view belongs at the physical aiming crosshair, with the arrow originating at its center and aiming into a shape representing valid pose space. Use thin lines, an open precise nib marker, a small arrowhead, and a clear unfilled target whose boundaries reflect the angle tolerance. Upright, cardinal, and diagonal poses must remain unambiguous. Resolve the eraser-to-tip arrow versus tip-to-eraser azimuth convention explicitly. Visual direction must match the physical pen and the existing verified guidance/gates; raw stored tilt is unaffected.
- Begin the visual work with a concrete layout/scene mockup or prototype. Use the existing WPF stack where practical; justify any new rendering dependency. Apply the hierarchy consistently to review/accept/copy feedback while preserving those actions and their data ownership.
- Drive visuals from existing acquisition/capture state. Retain keyboard triggering, per-report gates, stability checks, traversal, input suppression, pen-absence handling, candidate/working separation, and the session-only/paste-and-Save workflow.

**Automated/visual checks:** retain the milestone 4 regression suite. Add focused checks for any new orientation-to-visual mapping and state transitions; compare upright/cardinal/diagonal projections with independent expectations. Inspect rendered layouts at ordinary and small/high-DPI viewport sizes, checking target occlusion, labels, visible controls, contrast, and ready/accepted distinctions that do not rely on color alone. Verify visual updates leave acquisition and keyboard input responsive.

**User checks:** locate and adjust every relevant setup option without navigating hidden controls; understand where to start from the brief instruction and hierarchy. Capture a small layout using the visual guide, then try full tilt directions and retry/waiting states. Confirm the arrow starts at the nib's physical aiming point, direction is intuitive, and the nib, arrow tip, and actual tolerance boundary stay precise and visible. Verify pen contact does not activate controls or stall the display. Revisit the outstanding milestone 4 data-preservation and hardware checks through the improved UI; record results rather than assuming that a visual redesign establishes accuracy.

**Gate:** stop for review of readability, visual guidance, and calibration usability. Physical angle-scale, diagonal, and correction checks remain open until supported by device evidence.

## Milestone 6 — Requirement audit and delivery

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
| 1: Components, automatic RPC, full-screen mapping, pipeline, initialization | 1, 3, 4, 5, 6 |
| 2: Measurements, settings, grid, orientation | 1, 2, 4, 5, 6 |
| 3: Capture, review, UI | 4, 5, 6 |
| 4: Interpolation and runtime | 2, 3, 6 |
| 5: Schema, persistence, application, status | 2, 3, 4, 5, 6 |
| 6: Required verification checks | 2–6 |
| 7: OTD source verification | 1, 3, 6; revisit as needed |

## Progress record

States: Not started → In progress → Awaiting user review → Approved. Review fixes return that milestone to In progress. Record explicit approval evidence before advancing.

| Milestone | State | Evidence / findings | User approval |
| --- | --- | --- | --- |
| 1: Build, acquisition, mapping | Approved | Simplified workflow implemented; 25 synthetic tests and read-only device detection passed at that milestone. Basic physical checklist reported passing by the user on 2026-09-13; cardinal tilt signs recorded. Additional configurations and angle-scale/accuracy checks remain in docs/MILESTONE1.md. | User explicitly requested "Implement milestone 2" on 2026-09-13. Approval to advance is not device verification. |
| 2: Data and correction core | Approved | Calibration data, layout, immutable measured meshes, and correction evaluator implemented. Locked restore, Release build, and 93 synthetic tests passed. See docs/MILESTONE2.md. | User requested "Implement the next milestone" on 2026-09-13, authorizing milestone 3. This is not physical-device verification. |
| 3: Filter and package | Approved | Positioned correction filter, injected snapshot activation, bounded diagnostics, and standard ZIP package implemented. User reported build and live identity-data review checks passing on 2026-10-03; see docs/MILESTONE3.md. | User requested milestone 4 on 2026-10-04. Approval to advance is not measured-correction verification. |
| 4: Capture and review | Approved | Implementation checkpoint `2ec8a32`; Debug/Release builds and 133 non-live tests per configuration passed. User found presentation too dense; reworked in milestone 5. Physical/data-preservation review remains pending in docs/MILESTONE4.md. | User requested milestone 5 on 2026-10-04, authorizing advancement. This is not physical verification or final usability acceptance. |
| 5: Visual guidance and UI usability | Awaiting user review | All settings visible in responsive groups; thin top-view arrow and exact tolerance outline at the physical crosshair; local readiness colors, capture arc, success/retry cues and actions, secondary readings, and review/copy hierarchy. Debug/Release builds and 166 non-live cases per configuration passed. Actual WPF focus/placement/input/target-boundary checks passed; see docs/MILESTONE5.md. Reported physical upright discrepancy remains unresolved. | Implementation requested on 2026-10-04. Integrated precision guidance requested on 2026-10-05; capture-time feedback at the crosshair requested on 2026-10-12. Both revisions implemented. Physical/usability review remains pending. |
| 6: Audit and delivery | Not started | Renumbered from the former milestone 5; delivery scope retained. | — |

Current state: broad report-interface support and reported-coordinate guidance are implemented. Exact physical-angle/upright certification is removed from the release gate at the user's request. Milestone 6 packaging/licensing/lifecycle checks are implemented; final functional user workflow review and publication remain pending. See docs/RELEASE.md.

2026-09-13 simplification review:

- Removed the second Delaunay in-circle audit and format/version machinery. Retained sample/triangle/hull validation and invalid-data identity fallback.
- Replaced acquisition polling with direct channel reads and a processing-only stall timer. Parser timeout no longer depends on RPC timeout. Ordered debug cleanup remains independent of the worker; one `RestartRequired` flag describes whether retry is allowed.
- One async click handler owns preview startup through cleanup, with a single running flag instead of a separately stored acquisition task and window-level preview field. Closing during startup now also waits for that operation. Display cancellation closes the preview after bounded cleanup.
- Prepared nodes use one rectangular array. Removed unused readiness state, exception wrapping, and small redundant allocations; target totals come from the layout.
- Updated existing tests for restart behavior and buffered-parser cleanup, and removed compatibility-specific test cases and the constant-string assertion. Release solution build passed with zero warnings/errors; all 93 synthetic cases passed using the README commands. No dependencies or features added. Basic physical checks subsequently passed per user report; additional configurations and accuracy checks remain.

- Review fixes: RunAsync dispatches the session to the thread pool so daemon-disconnect cleanup does not await the calling UI context. Cleanup-failure instructions explicitly require restarting OTD and Calibrator. A paused-context regression test and recovery-message assertion failed before these fixes and passed afterward.

2026-09-13 device follow-up: user reports the physical checklist passing. For Huion Kamvas 13 (Gen 3), eraser left/right/top/bottom gives (50,0)/(-50,0)/(0,-50)/(0,50), upright (0,0). OTD configuration selects GianoReportParser, whose GianoReport negates signed bytes 10 and 11. Carry this source trace and observed sign convention into milestone 4 guidance; known-angle scale and diagonal conversion remain to verify. No milestone 3 implementation was started by this update.

2026-09-14 build/editor workflow update (explicitly authorized before milestone 4):
- Migrated the sole solution to CalibrationSuite.slnx, preserving all projects and configurations.
- Replaced the adjacent-source prerequisite with verified, pinned source acquisition during builds; kept upstream project definitions for fresh-checkout editor loading.
- Switched the filter to the official Plugin NuGet package, automated Debug/Release ZIP creation, and added editor launch/build/attach configuration.
- Updated UI-first instructions and Calibrator output notices. Validation and remaining live-debug limitations are recorded in docs/BUILD-WORKFLOW.md.
- Milestone 4 has not started. The user subsequently reported the milestone 3 live identity-data checks passing on 2026-10-03.

2026-10-03 user review: build/editor checks, Release ZIP installation, filter/property presentation, empty-data enable/bypass, Apply/Save, persistence after OTD restart, and Console inspection all reported passing. Recorded as user-verified, without claiming measured correction accuracy or interactive breakpoint verification. Existing staged infrastructure changes are preserved; milestone 4 remains not started.

2026-10-04 milestone 4: implemented capture settings/traversal, serialized per-report burst gates and medians/stability, keyboard capture/recovery, current-session review and targeted recapture, accepted-data copying, and close reminders. Guidance is limited to the Huion/Giano semantics already recorded; physical angular scale, diagonals, and correction accuracy remain review checks. Both builds and 133 non-live tests per configuration passed, together with offscreen WPF layout/acceptance checks. See docs/MILESTONE4.md. Subsequently committed as `2ec8a32` at the user's request.

2026-10-04 visual review follow-up: the user found the setup instructions and full-screen text too dense to follow. They clarified that relevant controls/options should stay visible; the requested improvement is shorter instructions, better readability, layout, and visual hierarchy. The 3D arrow continuing from the tip toward a target point/sphere is the preferred guidance concept. Committed the current implementation before updating these documents. Added milestone 5 for visual/UX work and moved final audit/delivery to milestone 6. No new UI implementation or hardware verification is claimed by this planning update.

2026-10-04 milestone 5: implemented following the user's request. Began with a vector layout/scene prototype, then rendered actual WPF setup, capture, and review controls. Retained all 13 settings without disclosure and moved secondary readings away from the 3D guide after small-viewport inspection. The native scene explicitly reverses tip-to-eraser guidance to a beyond-nib arrow and adds a top view for foreshortening. Added typed capture cues, eligible-report progress, and successful-storage feedback without changing gates or data ownership. Debug/Release builds and 153 non-live tests per configuration passed; offscreen DPI/layout checks and synthetic keyboard/mouse/touch routing passed. Locked cached restore passed; online NuGet metadata refresh was unavailable due to Windows TLS credentials. No new rendering dependency, live capture, OTD setting changes, or physical accuracy evidence. Milestone 5 awaits usability/device review; milestone 6 has not started.

2026-10-05 milestone 5 review revision: the user requested the top view at the physical crosshair and emphasized thin, precise landmarks. Updated the prototype, then replaced the separate 3D/top-view scene with a one-DIP arrow originating at the raw target's mapped center. Upright and tilted target outlines represent the configured gate bounds, without fills or decorative enlargement. Compact information cards and adaptive guide scale protect the nib and arrow tip. Debug/Release builds and 163 non-live tests per configuration passed; native WPF boundary, thin-stroke, panel/anchor, DPI/layout, and keyboard/mouse/touch checks passed. Awaiting review of the revised guidance; no new physical accuracy evidence or milestone 6 work.

2026-10-12 milestone 5 review revision: the user requested capture-time feedback at the crosshair so aiming does not require reading corner labels. Added green readiness arms/target and arm end caps, cyan capture arms/target with the existing eligible-report arc, gray dotted waiting guidance, and local state/action/success/retry feedback. Recovered retry readiness turns green while retaining its failure reason. Nearby feedback avoids the precision landmarks/cards and identifies the completed pose without making the next pose look accepted. Corner cards hold numeric/session information only. Debug and isolated-output Release builds passed; 166 non-live cases passed in each configuration; actual WPF focus/bounds/direction/tolerance/retry/next-pose checks and visual inspection passed. The running Calibrator session was preserved. The user's earlier reported upright `(5, -10)` discrepancy remains a physical-reference investigation; no tilt offset was introduced. Awaiting usability/device review; milestone 6 remains not started.

2026-10-12 capture defaults: the user reported that target poses were too difficult to hit and requested more reasonable defaults. Changed angle tolerance 3° → 8°, minimum burst 250 → 200 ms, position stability 0.25 → 0.5 mm, and raw tilt stability 2° → 3°. Retained 20 reports and the existing grid/pose coverage; 20° altitude spacing remains greater than twice the new tolerance. Added concise setting tooltips and updated current documentation/prototype. Release build, 166 non-live tests, and actual WPF target/feedback/layout/input checks passed in the isolated review output. Existing strict-threshold regression fixtures remain explicit and unchanged in meaning. New captures use the resolved values; resumed drafts retain their settings. Awaiting device usability review; no physical upright-reference or accuracy claim.

2026-10-12 repository simplification: the first refactor added structure without sufficient code reduction. Following the user's correction, removed the capture-session/layout/orientation wrapper types and completed-draft freeze state, consolidated overlay placement, eliminated repeated mesh/separation validation and the unused preview target generator, and replaced the manual tablet-field comparers with a prepared JSON structural matcher. The acquisition matcher now allocates per report; correction remains allocation-free. Final production code is 2,227 lines / 21,015 syntax tokens, compared with 2,484 / 23,024 after the first refactor and 2,229 / 22,410 before it. Debug/Release builds and 188 non-live tests each pass, together with actual WPF/input checks and unchanged renders. See docs/OPTIMIZATION.md for costs and evidence. Physical review and milestone 6 remain pending.

2026-10-12 release scope revision: the user explicitly includes display tablets with and without tilt, Windows only, aligned full-digitizer/full-display mapping. They chose reported-coordinate guidance at the crosshair instead of physical-angle conversions. Removed the Huion/Giano whitelist, physical-angle adapter, raw-zero hull constraint, and fixed 0.1-degree capture separation. A first stable measurement at each target supplies the reference; later samples use reported-coordinate circles. Tablets without tilt automatically resolve to position-only data, and the filter applies that spatial correction without requiring ITiltReport. Tilt-dependent data still rejects missing tilt without reuse or silent downgrade. Existing measured data remains usable because runtime ignores optional guidance metadata.

Milestone 6 implementation: added the project's MIT license, updated metadata/description, generated versioned plugin/Calibrator/source archives and checksums, completed pinned HidSharpCore notice retrieval, retained GPL/LGPL and other dependency notices, and added actual OTD install/update/removal plus full settings backup/restore/bypass/clear checks in isolated app-data directories. Package preparation does not publish or mutate the installed daemon. Physical angle certification is unnecessary under the new model; a short functional capture/copy/Save/restart/bypass check remains before publication.
Final automated verification for this scope: locked restore and Debug/Release solution builds pass with zero warnings/errors; 177 non-live tests per configuration pass. Actual WPF reference/position-only/report-coordinate boundary, feedback, layout, and input checks pass. MessagePack's known advisories were resolved by the pinned 2.5.301 build overlay; the current transitive vulnerability audit is clear. Production C# is 2,173 lines / 20,279 tokens / 149 declarations, compared with 2,227 / 21,015 / 159 at the previous checkpoint. Generated release candidates contain plugin, Windows Calibrator, actual corresponding source/build inputs, notices, metadata, and hashes. User workflow review/publication remain pending.

2026-10-12 tablet-support clarification: capability derives from OTD report interfaces and digitizer geometry, with no model catalog or per-model settings. The first eligible pen report without ITiltReport blocks tilt poses for the session, even if later reports include tilt. Setup and capture instructions explicitly ask position-only users to hold upright and explain the tilt restriction. OTD has no reliable metadata capability flag; constant zero is not interpreted as missing support. Users can select directions/levels zero if reported tilt is unusable. Upright posture cannot be verified without tilt measurements.
