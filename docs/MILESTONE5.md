# Milestone 5 — visual guidance and UI usability

> Historical milestone. Its physical-angle guidance and model restriction were superseded by the reported-coordinate workflow; see [current scope and verification](RELEASE.md).

**Status: implemented; awaiting review of the 2026-10-12 crosshair feedback and capture defaults.** Implemented following the 2026-10-04 request, on top of milestone 4's `2ec8a32` checkpoint. The user requested precise guidance and capture-time feedback at the aiming crosshair, then reported that the poses were too difficult to hit with the starting limits. Local feedback and more forgiving defaults are implemented. Automated and offscreen visual checks passed; physical capture usability, the reported upright discrepancy, and angle/correction checks remain pending.

## Implemented presentation

- Setup opens with one instruction and a connection card. All 13 inputs are visible in Target grid, Pen poses, Sampling, and Quality limits; the layout uses two columns when space permits and one scrolling column in smaller windows. Labels, values, and units align. Parse/range/spacing/inset errors appear beside the relevant input using the same core validation rules. Calibrate, Resume, Review, and Copy retain their existing availability and behavior.
- Full-screen capture integrates the top view into the physical crosshair: a thin cyan arrow begins at the exact mapped target center and aims into the unfilled tolerance outline. Crosshair arms and target change with capture state; the white open nib marker and cyan live tip retain distinct colors. A nearby glyph, heading, and short action/retry hint supply capture-time feedback. Placement clears the nib, arrow, target boundary, capture arc, and both corner cards, retaining a valid position while the pen moves. Corner cards contain only overall count/bar, requested/current numeric angles, one-decimal raw target/position/tilt/error, burst counts, and static keyboard hints. The guide fits the screen and clears both cards by 12 DIPs. See the revised [layout/scene prototype](milestone5-layout.svg).
- Ready uses green arms/target, small arm end caps, and nearby ◎/Space guidance; capturing uses cyan arms/target, nearby ◷/hold guidance, and a thin progress arc around the nib. Captured uses a temporary nearby ✓ naming the completed target/pose; the next pose's arms/target retain their own readiness. Retry uses ↻, a short local reason, and a dashed target; once the pose is eligible again the arms/target become green and the local heading says Ready to retry. Waiting uses gray dotted arms/target, nearby …, and hides the live arrow. Accepted feedback follows successful storage, including replacement of an existing measurement, and clears on navigation/restart. Burst progress is the smaller of eligible-report duration/minimum duration and report count/minimum count, capped at 1; it never fills from idle animation time. Local hint space and corner-card heights remain fixed across ordinary capture states.
- Review shows a short candidate/accepted/unfinished summary, readable rows with scrolling, a primary Accept action, recapture/discard controls, and the session-only Copy → paste → Save reminder. Setup copy feedback names OTD's Calibration data field and Save. Candidate/working ownership, close reminders, and clipboard payloads retain milestone 4's behavior.
- Starting capture limits are now ±8° pose tolerance, 200 ms minimum burst duration, 20 reports, 0.5 mm position stability, and 3° raw-tilt stability. The former limits were ±3°, 250 ms, 20 reports, 0.25 mm, and 2°. Wider tolerance enlarges the actual target region; a shorter burst and modestly looser deviation limits reduce the demand for a perfectly steady hold. The 3 × 3 grid, eight azimuths, two tilt levels, and 50° minimum altitude retain the existing 153-capture coverage. Altitude spacing remains 20°, exceeding twice the 8° tolerance; azimuth spacing is 45°. These are starting values for device review, with every per-report gate and measured-data rule retained.

## Scene and direction contract

The small open white nib marker is the physical aiming crosshair's center, derived from the same raw-target mapping as before. The cyan arrow begins there and continues in the direction opposite the eraser. The separate 3D scene and corner top-view inset have been replaced by this single screen-aligned view.

For guidance altitude `h` and tip-to-eraser azimuth `a`, the top view uses X right and Y down:

```text
arrow tip = nib center + R * (cos(h) cos(a), -cos(h) sin(a))
inner target radius = R * cos(min(90°, requested altitude + tolerance))
outer target radius = R * cos(max(0°, requested altitude - tolerance))
tilted target azimuth span = requested azimuth ± tolerance, wrapping at 360°
```

Thus eraser-left / azimuth 0 points the arrow right; eraser-down / 90 points it up; eraser-right / 180 points it left; eraser-up / 270 points it down. Upright collapses to the nib. The current pose comes from the existing Huion/Giano adapter; the guide requests that conversion with zero upright tolerance to retain small live directions, while displayed angles and per-report gates keep the configured upright tolerance. No raw stored tilt or runtime interpolation coordinate is changed.

Upright's valid space is a circle around the nib with radius `R * sin(tolerance)` and no azimuth restriction. Tilted poses use an annular sector, preserving the independent altitude and circular-azimuth gate bounds. A decorative sphere would overstate valid space; the outline follows those bounds instead. No fill or minimum marker size obscures the valid region. All guide strokes are one DIP, the nib marker has a two-DIP radius, and the open arrowhead is four DIPs long and four DIPs wide. The guide radius `R` is at most 200 DIPs and adapts to display edges and card clearance, without shifting the physical anchor. At 3° tolerance and maximum scale, the upright target's radius is about 10.5 DIPs; tighter tolerances shrink it.

`OrientationGuide` uses native WPF vector drawing, cached frozen pens, and reusable target geometry. Geometry is rebuilt only when the requested pose or tolerance changes; live movement redraws the arrow. There is no new rendering package, acquisition work on the UI thread, or second raw-axis conversion. The earlier authoritative Giano byte/sign trace remains applicable; no integration contract changed.

## Recorded verification — 2026-10-12 capture defaults

- Release solution build passed with zero warnings/errors in the existing isolated `artifacts/focus-review/Release` output. **166 non-live tests passed**; results are in ignored `tests/CalibrationSuite.Tests/TestResults/milestone5-defaults-release.trx`. Existing threshold-focused capture tests now specify their stricter 3°/0.25 mm/2° fixture limits explicitly so default tuning does not weaken the gate, stability, or boundary checks. Timeout validation uses the configured burst duration rather than assuming 250 ms.
- Re-rendered the actual setup and capture controls with the new defaults and checked the wider target regions. Existing native target-boundary, local-feedback placement/color, thin-stroke, stable-scale, retry/next-pose, small/high-DPI, keyboard, and mouse/touch checks passed. No new tests or dependencies were added for this tuning.
- The settings panel reads defaults from the shared capture settings. New captures use the resolved inputs; Resume retains the unfinished draft's settings. The open older application keeps its displayed values until the user edits them or restarts a rebuilt app. Median measurements are still stored at their actual raw coordinates; relaxed tolerance neither snaps them to requested poses nor adjusts the unresolved physical upright reference. Device usability and actual correction accuracy remain review checks.

## Recorded verification — 2026-10-12 crosshair feedback

- Debug and Release solution builds passed with zero warnings/errors. The normal Release output was locked by the user's running Calibrator, so Release was built and checked in `artifacts/focus-review/Release` using `-p:OutDir`; the running session was left open. **166 non-live tests passed in each configuration.** Results are in ignored `tests/CalibrationSuite.Tests/TestResults/milestone5-focus-{debug,release}.trx` files.
- Extended the actual WPF verifier to assert state colors in the crosshair arms, local feedback bounds and text height, separation from the precision landmarks and corner cards, and absence of actionable capture labels in the corner cards. Tested upright, all cardinal/diagonal raw directions, center and four edge targets, reversed live arrows, small/high-DPI layouts, and upright tolerances of 0.25°/3°/12°/80°. Checked invalid-to-valid retry recovery and that a previous capture's success does not make the next unready pose green.
- Inspected updated full-screen renders and the six-state `artifacts/visual-checks/crosshair-feedback-gallery.png`, including small ready/retry and next-pose success fixtures. Existing target-boundary, thin-stroke, stable-scale, setup/review, keyboard, and mouse/touch checks still passed. Release update/layout over 300 changing readings was about 0.68 ms median / 0.87 ms p95 on this host; actual contact/presentation latency remains unverified.
- This revision changes presentation only. Sampling, pose/stability gates, raw tilt, the unresolved physical upright reference, accepted-data ownership, and OTD application remain as before. No new package or physical accuracy evidence.

## Recorded verification — 2026-10-05 review revision

- Debug and Release solution builds passed with zero warnings/errors. **163 non-live tests passed in each configuration.** The revised orientation tests cover top-view cardinal/diagonal direction, near-upright continuity, tolerance-dependent upright radius, projected altitude/azimuth bounds including wraparound, panel separation, and guide clearance. Milestone 4 capture/acquisition/data regressions remain included. Results are in ignored `tests/CalibrationSuite.Tests/TestResults/milestone5-crosshair-{debug,release}.trx` files.
- The actual WPF outline geometry was checked against the existing pose gate at points just inside/outside its boundaries. Fixtures cover upright, cardinal/diagonal poses, 360° wrap, and 0.25°/3°/12° tolerances. Drawing assertions require unfilled shapes and strokes no thicker than one DIP. Capture assertions verify the physical anchor, screen/card clearance, separate card bounds, readable contents, and stable guide scale through ordinary capture-state transitions.
- Re-rendered all six capture cues at 1920 × 1080, 960 × 540, and 640 × 360 DIPs, using 200% raster DPI for smaller fixtures. Center/edge targets, geometry retry reasons, a nine-direction gallery, precision close-ups, and setup/review fixtures were inspected. The updated images are in ignored `artifacts/visual-checks`, including `capture-normal-ready.png`, `capture-small-ready.png`, `orientation-gallery.png`, and `precision-*.png`.
- Synthetic Space/Backspace/R/Esc and mouse/touch suppression passed. Release update/layout over 300 changing readings was about 0.7 ms median / 0.8 ms p95 on this host. These measurements do not establish actual pen-contact or GPU presentation latency. Used the existing locked restore with `--no-restore`; no packages changed and no fresh online audit is claimed.

## Upright discrepancy investigation — 2026-10-12

The user reports approximately `(5, -10)` raw tilt at what they perceive as perpendicular, and readings approaching `(0, 0)` with a slight eraser-down lean; the arrow shortens during that adjustment. This is a user observation, not a verified physical angle. Under the current angular-component assumption, `(5, -10)` means about 11.14° inclination (78.86° altitude), with the beyond-nib arrow pointing down/right. Moving the reported axes toward zero therefore produces the observed shortening.

Traced the pinned Giano parser's signed bytes 10/11 through report reconstruction, the capture adapter, upright gating, and rendering. No additive tilt offset or downward preference was found. Added three synthetic pipeline regression tests: exact zero through actual Giano parsing and serialized RPC reconstruction, every supported signed single-axis value (-89° through 89°), and opposite/exchanged axes including the reported `(5, -10)` pair. They establish software centering and directional symmetry, not the device's physical zero. All **166 non-live Release tests passed**; the existing offscreen WPF verifier also passed. Results are in ignored `tests/CalibrationSuite.Tests/TestResults/upright-bias-audit-release.trx`.

Physical zero remains unresolved. Check perpendicularity relative to the tablet surface using an independent right-angle reference, then compare raw tilt with the barrel rotated 180° while keeping its axis perpendicular. A consistent nonzero result at verified perpendicularity would establish a device/pen zero discrepancy; barrel-dependent readings would help distinguish its source. No guidance offset or measured-data correction was introduced on the basis of visual impression alone. Milestone 5 still awaits device/usability review.

## Initial implementation verification — 2026-10-04

- Debug and Release solution builds passed with zero warnings/errors. **153 non-live tests passed in each configuration**, retaining all 133 milestone 4 cases and adding 20 cases for upright/cardinal/diagonal arrow projections, near-upright continuity, panel clearance, eligible-report progress, successful replacement/retry/navigation cues, geometry recovery hints, and setting diagnostics. Results are in the ignored `tests/CalibrationSuite.Tests/TestResults/milestone5-{debug,release}.trx` files.
- `scripts/VisualChecks` renders the actual WPF controls offscreen without connecting to OTD, acquiring debug reports, showing windows, or changing the clipboard. Checked setup at 900 × 780 and 540 × 420 DIPs; capture at 1920 × 1080, 960 × 540, and 640 × 360 DIPs, with 200% raster DPI on smaller fixtures. All six capture cues, center/edge aiming points, upright plus eight compass directions, validation, geometry retry reasons, and candidate/accepted/small review layouts were rendered and inspected. Layout assertions check guide space, readings/hints, and a complete review row with scrollbars. Generated PNGs remain in ignored `artifacts/visual-checks`.
- Synthetic routed Space/Backspace/R/Esc events and mouse/touch suppression passed against the capture window. The existing per-report acquisition regression still passes. Release update/layout timings over 300 changing synthetic readings were about 0.6 ms median / 0.8 ms p95 on this host; these do not measure device/OS pen latency or GPU presentation during actual contact.
- The verifier's locked restore passed using the existing package cache. An online NuGet metadata refresh failed because Windows TLS credentials were unavailable (`SEC_E_NO_CREDENTIALS`); this run does not establish a fresh vulnerability audit. Package versions and rendering dependencies did not change, and audit/warning settings were not disabled.

Reproduce from the repository root:

~~~powershell
dotnet build CalibrationSuite.slnx -c Debug --no-restore
dotnet test tests/CalibrationSuite.Tests/CalibrationSuite.Tests.csproj -c Debug --no-build --no-restore --filter 'Category!=LiveDaemon'
dotnet build CalibrationSuite.slnx -c Release --no-restore
dotnet test tests/CalibrationSuite.Tests/CalibrationSuite.Tests.csproj -c Release --no-build --no-restore --filter 'Category!=LiveDaemon'
dotnet run --project scripts/VisualChecks -c Release
~~~

For an offline repeat with already cached dependencies, restore the verifier with `dotnet restore scripts/VisualChecks/VisualChecks.csproj --locked-mode --source artifacts/packages`, then run it with `--no-restore`. This is a cache check, not a fresh online audit.

## User review

1. Launch a rebuilt Release normally and check the new ±8°/200 ms/20-report/0.5 mm/3° starting limits. Locate and edit every setup input, and verify nearby validation and total-count feedback. Resize the setup window and scroll its single-column layout. If using an older running app, enter these values for a new capture; Resume keeps the prior settings.
2. Capture a 2 × 2 upright-only layout, then a small tilt layout. Keep the physical nib on the open white crosshair center and put the cyan arrow tip inside the amber outline. Check upright, left/right/up/down, and diagonals. Judge whether the nib and arrow tip remain easy to locate, the direction is intuitive, and the target boundary is clear on the actual tablet. Try a tighter angle tolerance to confirm the valid region shrinks.
3. Keep your gaze on the crosshair while exercising ready/capturing/captured, waiting between bursts, and retries from movement/gaps. Confirm the arm colors/end caps, capture arc, nearby success/retry cues, and short action/retry reasons are enough to follow the attempt without reading the corners. Return to a valid pose after rejection and check Ready to retry. Check Space, Backspace, R, and Esc, including contact responsiveness and suppression of stylus/mouse/touch controls.
4. Revisit the [milestone 4 data-preservation checklist](MILESTONE4.md#user-review): resume, candidate discard, targeted recapture, acceptance, copying, intended OTD profile paste/Save, and enabled/bypassed correction. Check actual display scaling/placement changes.

Known physical angle scale, diagonal conversion, capture defaults, corrected-pointer behavior, and broader monitor configurations remain open. The visual redesign and synthetic renders do not establish physical accuracy. Milestone 6 has not begun.

## Original design brief

## User feedback and design intent

The setup window's instructions at the top are too long, and the full-screen capture window is too text-heavy to follow while aiming and tilting the pen. The user wants shorter instructions, clearer visual hierarchy, and a better layout. They explicitly clarified that this does **not** mean removing options, reducing the number of controls, or hiding relevant inputs. Preserve configuration access while making the screen easier to read.

## Setup window

- Keep the opening instruction brief and actionable. Place necessary setup reminders where they are relevant instead of stacking paragraphs above the controls.
- Give connection readiness, grid layout, pose settings, sampling/quality settings, capture count, and primary actions distinct visual roles. Use readable typography, grouped controls, aligned values, consistent units, and adequate spacing.
- Keep relevant calibration controls visible and discoverable in the setup layout, including the sampling/quality inputs currently grouped under Advanced. Do not replace them with a few presets or bury them in more collapsed sections. Smaller windows may scroll; controls must remain reachable and clearly labeled.
- Place validation feedback near the affected setting and keep the next action apparent. Differentiate Calibrate, Resume, Review, and Copy through hierarchy and current state while retaining all applicable actions.

## Full-screen guidance

- Give the physical aiming crosshair and orientation guide priority. Keep the aiming point clear of instructions, geometry, and status panels.
- Develop the user's concept of an arrow continuing from the pen tip toward a target point/sphere. The guide should show how the actual pen orientation differs from the requested pose without making the user translate angle numbers mentally.
- Define the scene in a concrete mockup/prototype: nib anchor, live arrow, requested target, view/camera, depth cues, and how alignment appears. A reference plane, shadow, or other depth cue may help; these details are design decisions to evaluate, not an already approved scene.
- Explicitly resolve arrow direction. An arrow continuing beyond the nib follows eraser-to-tip, whereas spec azimuth describes tip-to-eraser. Document and test that visual reversal so moving the real pen produces an intuitive response. Use the established guidance projection and retain raw tilt for measurements and correction.
- Make ready, capturing, accepted, waiting, and retry states distinct with shape/icon/progress cues as well as color. A ready pose must not look like an accepted measurement. Show compact burst and overall progress, brief keyboard hints, and one short contextual failure reason.
- Keep required live angles/position/tilt/error legible in a secondary region. Long explanatory prose should not compete with the guide. Existing keyboard actions, capture gates, retries, and data-preservation behavior remain the functional baseline.

## Implementation and verification

Use the existing WPF presentation and capture snapshots where practical. Justify any rendering dependency; do not introduce a second orientation model or move acquisition/sampling onto the UI thread. Carry the same hierarchy into review/accept/copy feedback where useful.

Retain the milestone 4 regression tests. Add focused tests for new visual projection/state behavior where they catch real errors. Inspect normal and small/high-DPI layouts for target occlusion, text clipping, visible controls, contrast, and ambiguous 3D poses. Check upright, all cardinal directions, diagonals, and behavior near upright. Verify keyboard responsiveness during contact and that stylus/mouse/touch input does not activate capture UI controls.

User review should establish that settings are easy to find, instructions are short enough to act on, and the arrow/target scene makes the requested motion clear. Revisit the [milestone 4 review checklist](MILESTONE4.md#user-review) through the improved UI. Known-angle scale, diagonal physical conversion, capture defaults, and actual corrected-pointer behavior still need device evidence; the checkpoint commit and visual plan do not establish them.

Final requirement audit, package distribution, licensing, installation/update/removal, and settings/preset verification move to **milestone 6** with their existing scope.
