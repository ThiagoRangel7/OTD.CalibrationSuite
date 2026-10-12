# Calibration Suite - Specification (OTD 0.6.7, PreTransform)

Calibration Suite calibrates display tablets only, by placing the physical pen tip on targets shown on the tablet screen. It interpolates one error field from measured screen positions and pen orientations to correct display-tablet pointer errors. No separate position/tilt stages, fitted coefficients, or global solver.

## 1. Components and OTD integration

- **Calibrator:** separately launched Windows WPF app for guidance, raw-report capture, session review, and copying calibration data.
- **Correction Filter:** normal OTD plugin package running in-process at `PipelinePosition.PreTransform`; changes only pen position, preserving report types, other fields, and event order.
- **Shared core:** data validation, capture calculations, mesh construction, and correction evaluation for both components. The filter and automated tests use the same evaluator; keep WPF and RPC outside the core.

### Ownership and standard workflows

OTD owns driver configuration and persistence; the Calibrator owns measurement and review. Reuse existing OTD UI/public APIs; add only calibration-specific behavior. Workflow: configure OTD, capture/review in the Calibrator, copy the result, paste into the filter's standard property editor, and Save in OTD. Users never author JSON or edit configuration files.

| Responsibility                                                   | Existing workflow to use                                                                             |
| ---------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------- |
| Install/update/remove the filter                                 | OTD Plugin Manager and its supported plugin package workflow                                         |
| Select output mode, tablet/display areas, clipping, and bindings | OTD's existing profile and area controls                                                             |
| Enable/disable filters and edit calibration data                 | OTD Filters UI and its generated property controls                                                   |
| Apply and persist calibration                                    | OTD Apply/Save, together with the rest of the selected settings                                      |
| Back up, restore, or manually switch calibrations                | OTD settings Open/Save as and presets; these include the embedded filter property and other settings |
| Reset this calibration                                           | Clear its property in OTD and Save; disable its checkbox for a temporary bypass                      |
| Discover the tablet, obtain geometry, and acquire parsed reports    | Existing daemon RPC contracts and report types                                                       |
| Investigate plugin/runtime failures                              | OTD logs and existing diagnostics UI/export                                                          |
| Guide measurements and review captures                    | Calibration-specific UI and shared core                                                              |

Do not add a plugin installer, settings editor, preset manager, calibration-file library, synchronization service, custom IPC server, or OTD fork. OTD 0.6.7's generated property editor supports ordinary values, including strings, but no supported wizard/button hook to host or launch the Calibrator; `ActionAttribute` is unimplemented. Property setters must not access files, launch processes, or show UI. Using OTD desktop APIs does not require embedding Eto in WPF.

**Raw** means OTD-parsed values before filters/transforms, not HID bytes. Positions, targets, and errors use raw digitizer units; orientation uses `ITiltReport.Tilt.X/Y` (`TiltX/TiltY` below). Pixels are UI-only. Reuse OTD parsers, including device-specific tilt decoding/inversions.

### Acquisition

Use `OpenTabletDriver.Desktop.RPC.RpcClient<IDriverDaemon>` on the `OpenTabletDriver.Daemon` pipe. Initialize as OTD does: call `GetApplicationInfo()`, set `AppInfo.Current`, and initialize/load `DesktopPluginManager` for that installation so `DebugReportData.ToObject()` resolves built-in/installed report types. Use compatible OTD 0.6.7 assemblies (the source tree targets .NET 8). Do not duplicate the plugin loader, report DTO hierarchy, or settings serializer. Diagnose unknown/unloadable report types; never guess them.

Subscribe directly to `DeviceReport`; enqueue reports for a serialized background consumer and send only display updates to WPF. This small adapter adds capture timing/queue bounds absent from the debugger UI. Avoid the UX `DaemonRpcClient` wrapper: it dispatches every report to Eto's UI thread.

`SetTabletDebug(true)` makes `DeviceReader` reparse bytes into debug clones. `DebugReportData` contains `Tablet`, report-type `Path`, and serialized `Data`, with no hardware timestamp/sequence number. Use OTD reports, never OS cursor readings or another HID parser.

Connect automatically on startup and retry while OTD is unavailable. Require exactly one connected tablet via GetTablets(); use it automatically and display its name. With zero or multiple tablets, show instructions and disable Start. Match incoming TabletReference contents, not RPC object identity. Physical tablet/display association is the user's responsibility; do not build selectors, device registries, or monitor matching heuristics.

The Calibrator does not read OTD profiles or settings. It uses OTD for installation/plugin metadata, tablet geometry, and raw reports. Handle actual disconnects and TabletsChanged by stopping acquisition. Settings or Resynchronize alone do not invalidate measurements; do not poll GetSettings or inspect output modes, mappings, or filter order. Snapshot physical display geometry at Start; cancel on display geometry/DPI changes and use fresh geometry on the next Start.

Debug enablement is daemon-global, without a state query/ownership token. Enable only for explicitly started preview/capture; disable on completion/cancellation and unsubscribe on disconnect. Concurrent OTD debugger use is unsupported. Bound queues and cancel on overflow, stale queued work, or blocked processing. Ordinary pen absence is not a failed session: show Waiting for pen, hide stale live values, and preserve progress. During a capture burst, a report gap invalidates only that attempt; the user can retry.

### Mapping and pipeline

The user moves the normal Calibrator window onto the physical tablet display and presses Start. Use the monitor containing the largest part of that window (nearest monitor if off-screen), then show an opaque, borderless full-screen preview covering the entire monitor, not its work area. No display dropdown, identifier overlays, or automatic tablet-to-display detection.

Supported setup: the full active digitizer area corresponds to the full screen, with aligned X/right and Y/down axes. Non-display tablets and rotated, cropped, or otherwise mismatched digitizer/display arrangements are outside scope. Explain this setup; do not infer physical compatibility from a model name or aspect ratio.

Target placement is independent of OTD cursor mapping. For a raw target (x,y), place the crosshair at (x / MaxX * viewportWidth, y / MaxY * viewportHeight) in full-screen WPF window-local units. Obtain monitor bounds from Windows; use per-monitor DPI awareness and WPF viewport dimensions. Negative desktop origins affect window placement only. Never construct an OTD output mode or inject a pointer for calibration.

Ask users to disable other filters for a predictable, undistracting cursor and to aim with the physical nib, not the cursor. Raw debug reports precede filters, so other filters do not alter these measurements. The user is responsible for correct aiming and setup; keep measurable capture quality gates, not filter classification/enforcement.

The installed correction filter still runs at PreTransform, retains OTD's binding/transform/clipping/output stages, and must receive unmodified parsed position/tilt. Document that position/tilt-changing filters must not precede it; leave filter configuration in OTD. Restore temporarily disabled filters after calibration as appropriate.

### Profiles and initialization

Expose `CalibrationJson` with `[Property("Calibration data")]`, an empty-string default, and a paste/Save/clear-to-reset tooltip. OTD generates a text box and stores the string in the selected profile's `Filters` entry. No file-path property, custom property type/editor, or per-sample properties. The Calibrator copies JSON as an internal payload.

OTD matches Profile.Tablet to TabletReference.Properties.Name and constructs filters from that profile. The user chooses the destination profile when pasting. Same-name tablets share a profile, with no per-unit/per-pen switching. The Calibrator does not load, compare, manage, or select profiles; OTD settings/presets remain the durable storage workflow.

Implement `IPositionedPipelineElement<IDeviceReport>` with a public parameterless constructor, plugin name, `Position`, `Consume`, and `Emit`. Obtain geometry via `[TabletReference]`; prepare caches in `[OnDependencyLoad]` after settings/tablet injection, never in the JSON setter. Settings activation creates a new filter; add no file watcher or hot-reload mechanism. Runtime requires exactly one enabled Calibration Suite entry in the selected profile. Document the single-entry requirement in OTD; do not inspect profile entries from the Calibrator. Filter absence/disablement does not prevent calibration generation.

## 2. Measurements, layout, and orientation

At target `T`, store accepted burst medians: raw position `p` and tilt `t`. Derive `error = p - T`; each node maps measured tilt to measured position error. Individual reports need not persist.

Requested poses guide capture/acceptance only: requested `(20, 0)` captured at `(19, 2)` stays at `(19, 2)` with its measured position/error. Upright is an ordinary measured vertex near `(0, 0)`, never forced to zero.

### Settings

| Setting                 | Meaning                                                         | Default    |
| ----------------------- | --------------------------------------------------------------- | ---------- |
| RowCount / ColumnCount  | Screen-grid rows / columns                                      | 3 / 3      |
| GridInsetMm             | Distance from active digitizer edges, not bezel or fixed pixels | 15 mm      |
| AzimuthCount            | Equally spaced directions                                       | 8          |
| NonUprightAltitudeCount | Non-upright levels                                              | 2          |
| MinAltitudeDeg          | Minimum altitude above the tablet plane                         | 50 degrees |
| AngleToleranceDeg       | Pose tolerance                                                  | 8 degrees  |
| CaptureWindowMs         | Minimum burst duration                                          | 200 ms     |
| MinSamplesPerCapture    | Minimum eligible reports                                        | 20         |
| PositionStabilityMm     | Maximum 95th-percentile distance from median position           | 0.5 mm     |
| TiltStabilityDeg        | Maximum 95th-percentile distance from median raw tilt           | 3 degrees  |
| ReportGapTimeoutMs      | Maximum gap without an eligible report                          | 500 ms     |
| CaptureTimeoutMs        | Maximum attempt duration                                        | 2000 ms    |

Sampling/stability defaults are adjustable settings requiring device validation, not universal noise/report-rate guarantees. On 2026-10-12, the user reported that default poses were too hard to hit. Widened pose tolerance from 3 to 8 degrees, shortened the burst from 250 to 200 ms, and relaxed the position/tilt deviation limits from 0.25 mm/2 degrees to 0.5 mm/3 degrees. Keep the 20-report minimum and existing pose coverage: default altitude spacing is 20 degrees, exceeding twice the new 8-degree tolerance; azimuth spacing is 45 degrees. Accepted samples retain their measured tilt/position rather than snapping to requested poses. This tuning does not establish or adjust the pen's physical upright reference.

Use the UI label **Minimum altitude**: 50 degrees permits at most 40 degrees lean from upright. Default altitudes: 90, 70, 50 degrees. Initially keep these Calibrator controls in session state; add no runtime properties, OTD profile, or settings persistence layer.

For `N` non-upright levels, minimum altitude `M`, and `A` azimuths:

```text
altitude[j] = 90 - j * (90 - M) / N    j = 1..N
azimuth[k]  = 360 * k / A              k = 0..A-1
captures   = R * C * (1 + A * N)      default: 153
```

Add upright once per node; show total captures before starting. Require integer counts (`R,C >= 2`, `A,N >= 0`; when both are positive, `A >= 3`), positive sampling/stability parameters, `CaptureTimeoutMs > CaptureWindowMs`, `0 < M < 90`, angle tolerance below 90 degrees, and, when capturing non-upright poses, altitude/azimuth spacing greater than twice the angle tolerance. If either A or N is zero, capture only upright at each target (R*C captures); skip non-upright pose generation and spacing checks. Limit a session to 10,000 captures and reject larger layouts before allocation. Verify usable varying tilt for full tilt calibration: `ITiltReport` declares no attainable range. Explain rejection of incompatible settings or inadequate orientation coverage.

### Grid

Use the detected tablet's `Specifications.Digitizer.Width/Height` as `Wmm/Hmm` and `MaxX/MaxY` as `Wraw/Hraw`; require finite positive values. OTD's model has zero origin and no origin field. For inset `m`, require `0 < m < min(Wmm,Hmm)/2`:

```text
mx = m / Wmm * Wraw
my = m / Hmm * Hraw
X[c] = mx + c / (C - 1) * (Wraw - 2 * mx)
Y[r] = my + r / (R - 1) * (Hraw - 2 * my)
```

Persist resolved grid coordinates; reject invalid geometry/insets without silently changing layout. Visit nodes top-left to bottom-right, capturing upright, then every altitude for azimuth 1, every altitude for azimuth 2, and so on before the next node. Show only the active target.

### Angles for guidance and gates

Altitude is 90 degrees upright and approaches 0 at the tablet plane; inclination from upright is `90 - altitude`. Azimuth follows the eraser direction relative to the tip:

| Eraser direction | Left    | Down | Right | Up  |
| ---------------- | ------- | ---- | ----- | --- |
| Azimuth          | 0 / 360 | 90   | 180   | 270 |

Compare azimuth circularly: 359 and 1 differ by 2 degrees. At upright, display it as unavailable and omit its gate.

If reported tilt axes are signed angular components, convert their tangents to screen-aligned tip-to-eraser components `sx,sy` (right/down positive), then:

```text
altitude = atan2(1, sqrt(sx*sx + sy*sy)) in degrees
azimuth  = wrap360(atan2(sy, -sx) in degrees)
```

Verify device axis semantics/signs in all four directions and upright; never assume raw signs match this projection. Guidance conversion never alters stored/interpolated tilt; runtime needs no derived angles. OTD's `DisableTilt` only controls pointer tilt forwarding and need not change for acquisition.

The initial capture adapter supports the user-verified Huion Kamvas 13 (Gen 3) with OTD's Giano parser/report: `sx = -tan(TiltX)` and `sy = tan(TiltY)`, using degrees. Reject nonfinite axes or magnitudes at least 90 degrees. Other tablet/parser combinations remain unavailable for capture until their orientation semantics are established; do not reuse this projection by guessing. Cardinal signs are recorded in docs/MILESTONE1.md. Known-angle scale, diagonal physical conversion, and attainable poses remain device review checks; mathematical conversion tests cannot establish them.

## 3. Capture, review, and UI

Start with no accepted working calibration. Keep unfinished capture sessions, completed candidates, and accepted working data separately in memory for this app session. Do not read previously saved data from OTD, compare applied contents, or add calibration-file import/export. Closing the app loses these measurements unless the user has pasted and saved them in OTD.

Completion offers Accept Calibration, Recapture, and Discard. Only acceptance replaces working data; cancellation/restart/discard preserves it. Targeted node/pose recapture preserves other samples and rebuilds affected geometry. Review and recapture apply to measurements created during this app session, not previously saved OTD data. This is measurement review, not an accuracy-validation session.

Working calibration means the Calibrator's accepted in-memory dataset, not the correction currently running in OTD. Acceptance enables copying without applying/saving in OTD; state this beside the action. Accepted results remain session-only until saved in OTD; show pending status before closing. Use OTD presets for durable previous versions.

### A capture attempt

1. `Space` starts one burst; suppress autorepeat/overlap.
2. Require finite position and tilt in the same eligible pen report. Ignore unrelated auxiliary reports; reject out-of-range, reported proximity loss, and eraser input via available OTD interfaces. Never combine reports or reuse stale tilt. Pressure neither triggers capture nor requires a positive threshold.
3. Gate every report without prediction/smoothing: upright altitude must be within tolerance of 90; other poses require altitude and circular azimuth within tolerance. Any gate failure discards the attempt.
4. Collect until **both** duration and sample minima are met, within gap/total timeouts. Measure sample duration from the first eligible report; total timeout and the initial report gap start at the key press. Exclude queued reports received before the trigger. Use a monotonic local clock; count each event once, including identical repeats. Without hardware timestamps/sequences, some upstream loss is undetectable. A report gap during a burst rejects that burst with a retry message; pen absence between bursts preserves progress. Actual disconnects or blocked processing stop acquisition.
5. Compute component-wise position X/Y and tilt X/Y medians over the same burst; average the middle two for even counts. For each report, compute Euclidean distances from median position (convert each displacement axis independently to mm) and median tilt (raw degrees). Sort each list; the 95th percentile is one-based rank `ceil(0.95 * count)`. Reject if either stability limit is exceeded; never accept by trimming outliers.
6. Store median position/tilt, which represent the burst but need not match an individual report. Never substitute requested poses or use linear medians of wrapped azimuth as interpolation coordinates.

Median/stability checks reduce noise/movement, not systematic aiming error; pose gates cannot establish on-target aiming.

### Windows and actions

The opaque full-screen WPF window must remain responsive during pen contact and ignore stylus/mouse/touch UI input (click, drag, hold). Show active target/progress, requested/current angles, latest raw position/tilt/error against target, and capture readiness/progress/retry reasons. Display latest reports to one decimal place; only stored captures use medians.

| Key       | Action                                          |
| --------- | ----------------------------------------------- |
| Space     | Attempt capture                                 |
| Backspace | Recapture previous pose, including across nodes |
| R         | Restart unfinished session                      |
| Esc       | Cancel the active burst, return to main, retain completed measurements for resume/review |

Main screen shows automatic connection readiness, the single tablet name, and session/candidate/accepted-data state. It does not show profile/display selectors, filter status, Default/Calibrated classification of OTD data, or applied/saved/runtime status.

Main actions: Calibrate, Review Calibration, and Copy Calibration. In milestone 1 only, label the entry Start preview because measurements are not yet captured. Connect automatically without a Connect/Refresh button. Review opens current-session measurements; Copy uses accepted data and instructs the user to paste into the intended OTD profile's Calibration data field and Save.

Show Resume Capture for an unfinished session; its settings remain those captured at Start. Resume uses fresh tablet/display geometry and requires matching digitizer geometry. Starting a new calibration explicitly discards unaccepted data after confirmation. Review can discard unfinished/candidate data while preserving accepted data. Targeted recapture begins from a completed candidate or accepted result, copies it, and clears only the selected pose/node in the draft. During targeted recapture, R restarts that selection. Backspace during ordinary capture can cross node boundaries. Pending data and accepted measurements always cause a close reminder, even after Copy; the app cannot observe whether OTD was saved.

Use OTD for reset, enable/disable, backup/restore, and presets. Add no Calibrator Import/Export, Reset Working Calibration, or Apply/Save actions. Runtime filter loading still validates persisted calibration through the shared core.

### Visual UX — milestone 5

The milestone 4 user review identified excessive setup instructions and full-screen text. Shorten instructions and improve readability through typography, spacing, grouping, alignment, and action/status hierarchy. Preserve relevant configuration options and keep their controls visible and discoverable in the setup layout, including sampling/quality settings. Use responsive layout and scrolling where needed; do not treat hiding inputs or replacing them with a few presets as the requested simplification.

Full-screen guidance shares the active physical aiming crosshair, following the user's 2026-10-05 clarification. A thin cyan arrow starts at the exact mapped crosshair center and continues beyond the nib in a screen-aligned top view. Its endpoint is `(cos(h) cos(a), -cos(h) sin(a))` times the guide radius, reversing the established tip-to-eraser convention in X-right/Y-down coordinates. Place the arrow tip inside the unfilled target outline: a circle around the nib for upright, or a curved annular sector for tilted poses. Its radial and azimuth boundaries represent the existing altitude and circular-azimuth tolerance gates, without an enlarged decorative target or minimum target size. The crosshair, arrow, arrowhead, target boundary, and capture arc use one-DIP strokes; the nib marker has an open two-DIP radius and the arrowhead is small and unfilled. Native WPF vector drawing replaces the separate corner 3D/top-view scene. The visualization uses the existing verified guidance adapter, with zero upright tolerance for live direction continuity; displayed azimuth and capture gates keep the configured tolerance. Stored raw tilt remains unchanged. The revised prototype and checked WPF renders are recorded in docs/MILESTONE5.md.

Following the user's 2026-10-12 clarification, all feedback needed while aiming or capturing must be visible at the crosshair. Amber arms/target mean adjust; green arms/target with small end caps mean ready; cyan arms/target and an eligible-report progress arc around the nib mean capture; gray dotted arms/target mean waiting. A nearby state glyph and short action/retry hint clear the nib, live arrow, target boundary, capture arc, and information cards, retaining their placement while it remains clear. Successful storage shows a temporary check and identifies the completed target/pose; the next pose's crosshair keeps its own gate-derived state. Retry keeps a dashed target and its short reason; returning to a valid pose changes the arms/target to green and shows Ready to retry. Keep overall progress, numeric angles/raw values, burst counts, and static keyboard hints in corner cards for inspection between attempts. Do not require a corner label to decide whether to capture, hold, retry, or return the pen. Presentation changes must not modify sampling, stored tilt, or capture/data-preservation rules.

Distinguish ready, capturing, accepted, waiting, and retry states with icons, compact progress, and short contextual text. Waiting hides the live arrow; retry also dashes the target outline. Keep essential readings legible in a secondary card. Compact status/angle and readings/keyboard cards occupy free corners without overlapping. The guide stays anchored to the physical target; its radius, up to 200 DIPs, fits the display and maintains 12 DIPs of clearance from both cards. Reserve burst/hint space to avoid changing scale during normal capture-state updates. Burst progress reflects the minimum of eligible-report duration and sample-count fractions; successful storage emits a temporary captured cue, including for replacements, and navigation/restart clears it. The visual update consumes capture snapshots; measurement acceptance, data ownership, and OTD integration retain their established contracts. See [milestone 5 implementation and review](docs/MILESTONE5.md). Physical usability/angle/correction verification remains pending.

## 4. Interpolation and runtime

### Interpolation decision

Retain triangles over actual measured `(TiltX, TiltY)` for tilt correction, and bilinear blending across screen targets. Requested azimuth/altitude poses guide the user; they do not replace measured coordinates or determine mesh connectivity. Capture order does not affect the resulting correction.

The requested rings suggest triangles around upright and quads between rings, but accepted measurements need not form regular rings. Measured-tilt triangulation handles these variations and upright with one evaluation rule. Use the library to choose the triangulation, then check that samples are retained, triangle arithmetic is usable, and the hull provides coverage. A second all-samples in-circle audit is unnecessary: interpolation requires usable triangles, not an independently certified Delaunay optimum.

This is a practical design choice, not a claim that triangles are more accurate. Triangle interpolation reproduces vertex errors and is continuous, but its slope can change at edges and the chosen diagonal can affect intermediate estimates. Quads can produce different estimates and are not automatically more accurate. Reconsider interpolation only with a reasoned comparison using the same measured inputs and additional held-out measurements. Such a comparison does not add a guided accuracy-testing feature to the initial release.

Capture gates do not eliminate runtime boundary handling: users can draw beyond recorded tilt coverage, and polygonal rings can exclude intermediate directions even at an otherwise captured lean. Keep nearest-boundary clamping. Preserve correction variation across both screen position and tilt.

### Within a node

Triangulate measured `(TiltX,TiltY)` points, initially using Delaunay. This interpolator belongs in the shared core; OTD settings/pipeline APIs do not provide it. Use a compatible geometry library with robust predicates and deterministic behavior, not a new triangulation engine. Measurements are authoritative; build derived mesh/lookup caches at node completion/load. Inside each triangle, blend vertex errors barycentrically:

```text
nodeError(t) = w0*error0 + w1*error1 + w2*error2
w0 + w1 + w2 = 1
```

This reproduces vertex errors and is continuous across shared edges. Evaluate relevant nodes' independent meshes/boundaries at current raw tilt, including upright without a special branch. Alternative orientation coordinates change intermediate results and require documented justification and validation.

Reject new captures within 0.1 degree Euclidean tilt distance of another sample and request recapture. For multi-sample nodes, completion requires a usable nondegenerate mesh with captured upright strictly inside its hull; identify failing poses before advancing.

Every multi-sample mesh, including OTD-loaded data, must contain `(0, 0)` strictly inside its hull, surrounding vertical. Loaded data need not identify an upright sample: validate measured geometry, not guidance metadata. This check neither creates an origin sample nor moves recorded points.

Sort vertices lexicographically by measured `(TiltX, TiltY)` before triangulation; resolve ties deterministically so sample reordering cannot change correction. Use robust predicates and retain distinct samples; interior collinearity/cocircular rings are not inherently invalid. Never jitter vertices or invent errors; clearly reject unusable loaded geometry.

Outside each node's convex hull, project to the nearest boundary point by raw tilt-coordinate distance and interpolate there. Never extrapolate or modify report tilt. Polygonal rings can clamp near the altitude limit even within the requested range; validate this separately.

A node with exactly one finite measured sample represents upright-only calibration: `nodeError(t) = sample.position - target` for any usable tilt. Retain its actual measured tilt; do not force it to zero. It requires no triangles or hull-coverage check. Capture completion still identifies that sample as upright, with pose eligibility enforced by the capture workflow. Loading does not infer upright from raw signs or requested-pose metadata. Zero, two, or three samples are invalid, not a fallback to constant correction.

### Across nodes

For raw position `p` and tilt `t`, clamp only the position lookup to the grid rectangle, select its cell, and bilinearly blend its four node errors evaluated at `t`. Arrays use `[row][column]`; `u,v` are normalized horizontal/vertical cell coordinates:

```text
E = (1-u)*(1-v)*E[r][c] + u*(1-v)*E[r][c+1]
  + (1-u)*v*E[r+1][c] + u*v*E[r+1][c+1]
correctedPosition = p - E
```

Look up cells using original raw position, never corrected position or a captured-position mesh. Target coordinates index nodes; captured positions supply errors. This approximate error field is not an exact inverse: measure residuals at training targets, especially with large offsets/gradients; never promise exact target reproduction.

Never clamp actual output to the inset grid. Update position via the original report's `IAbsolutePositionReport` and emit that report once, preserving concrete type and all other fields; never substitute a generic tablet report or copy a boxed struct into another object. Pass through unchanged: identity data, auxiliary/out-of-range/eraser reports, reported proximity loss, and reports without usable paired pen position/tilt. Never apply nib calibration to erasers or assume upright for missing tilt. Use OTD `Log` for initialization failures and persistent unsupported input, without per-report logging. The Calibrator may explain compatibility issues observed in its own input.

Prepare immutable calibration snapshots outside the report path. No per-report file I/O, fitting, mesh construction, large allocations, or long-lived locks.

## 5. Data and persistence

Calibration Suite has not been released. All development revises the same initial release; earlier designs are not supported formats. There are no schema version fields, migrations, or backward-compatibility guarantees. Update the producer, consumer, examples, and tests together when the data structure changes. Measured data requires every field below except `requestedPose`.

| Field                                | Meaning                                                                                 |
| ------------------------------------ | --------------------------------------------------------------------------------------- |
| `mode`                            | `"measured"`                                                 |
| `sourceGeometry.maxX/maxY`           | OTD raw digitizer maxima at capture                                                     |
| `sourceGeometry.widthMm/heightMm`    | Active digitizer dimensions in millimeters at capture                                   |
| `grid.preTransformRowCoordinates`    | Raw Y coordinates; array length is row count                                            |
| `grid.preTransformColumnCoordinates` | Raw X coordinates; array length is column count                                         |
| `nodes[r][c].samples`                | Measurements for target `(columnCoordinates[c], rowCoordinates[r])`; zero-based indices |
| Sample `position.x/y`                | Captured burst median position, in raw digitizer units                                  |
| Sample `tilt.x/y`                    | Captured burst median OTD tilt axes, in degrees                                         |
| Sample `requestedPose`               | Optional capture guidance in degrees; never interpolation coordinates                   |

- Names/enum strings are case-sensitive; numbers finite; counts integers. Ignore unknown optional properties; reject unknown modes.
- Grid lines strictly increase within raw digitizer bounds, with at least two rows/columns and exactly matching `nodes` dimensions. Require exactly one finite sample per constant-error node, or at least four distinct samples and usable geometry per tilt-dependent node (section 4). Use measured mode and the sample schema: sample count determines evaluation, with no new flag. Allow nonuniform grids/differing sample counts, including constant and triangulated nodes in the same dataset; evaluate each independently before spatial blending.
- Derive targets from row/column coordinates and errors as `position - target`; never persist redundant authoritative errors.
- Compare all four `sourceGeometry` fields to injected OTD geometry with relative tolerance `1e-6`. Mismatches require recalibration, never rescaling. This guards configuration changes/incompatible pastes; matching geometry neither proves tablet/pen equivalence nor replaces OTD profile selection.

The Calibrator writes guidance, e.g. `"requestedPose": { "altitudeDeg": 70, "azimuthDeg": 90 }`: altitude in `(0, 90]`, non-upright azimuth in `[0, 360)`, upright azimuth omitted. Runtime-loaded samples may omit it; the Calibrator reviews only current-session measurements. Within upright angle tolerance, request upright without an azimuth gate. This affects only the new capture, never existing measurements/classification; replace measurements only on acceptance. Runtime ignores guidance; generation preferences stay local.

Illustrative 2x2 document: fictional 100 x 100 mm, 1000 x 1000 raw digitizer, 15 mm inset, fewer poses than default. Do not reuse as calibration:

```json
{
  "mode": "measured",
  "sourceGeometry": {
    "maxX": 1000,
    "maxY": 1000,
    "widthMm": 100,
    "heightMm": 100
  },
  "grid": {
    "preTransformRowCoordinates": [150, 850],
    "preTransformColumnCoordinates": [150, 850]
  },
  "nodes": [
    [
      {
        "samples": [
          { "position": { "x": 152, "y": 151 }, "tilt": { "x": 0, "y": 0 } },
          { "position": { "x": 156, "y": 151 }, "tilt": { "x": 39, "y": 1 } },
          { "position": { "x": 152, "y": 155 }, "tilt": { "x": -1, "y": 40 } },
          { "position": { "x": 148, "y": 151 }, "tilt": { "x": -40, "y": 0 } },
          { "position": { "x": 152, "y": 147 }, "tilt": { "x": 1, "y": -39 } }
        ]
      },
      {
        "samples": [
          { "position": { "x": 853, "y": 151 }, "tilt": { "x": 0, "y": 1 } },
          { "position": { "x": 858, "y": 151 }, "tilt": { "x": 40, "y": 0 } },
          { "position": { "x": 853, "y": 155 }, "tilt": { "x": 0, "y": 39 } },
          { "position": { "x": 848, "y": 151 }, "tilt": { "x": -39, "y": -1 } },
          { "position": { "x": 853, "y": 147 }, "tilt": { "x": 0, "y": -40 } }
        ]
      }
    ],
    [
      {
        "samples": [
          { "position": { "x": 152, "y": 852 }, "tilt": { "x": 1, "y": 0 } },
          { "position": { "x": 156, "y": 852 }, "tilt": { "x": 40, "y": 1 } },
          { "position": { "x": 152, "y": 857 }, "tilt": { "x": 0, "y": 40 } },
          { "position": { "x": 148, "y": 852 }, "tilt": { "x": -40, "y": 0 } },
          { "position": { "x": 152, "y": 847 }, "tilt": { "x": -1, "y": -39 } }
        ]
      },
      {
        "samples": [
          { "position": { "x": 853, "y": 852 }, "tilt": { "x": 0, "y": 0 } },
          { "position": { "x": 858, "y": 852 }, "tilt": { "x": 39, "y": -1 } },
          { "position": { "x": 853, "y": 857 }, "tilt": { "x": 1, "y": 40 } },
          { "position": { "x": 848, "y": 852 }, "tilt": { "x": -40, "y": 0 } },
          { "position": { "x": 853, "y": 847 }, "tilt": { "x": 0, "y": -40 } }
        ]
      }
    ]
  ]
}
```

Identity requires no grid, geometry, or samples:

```json
{ "mode": "identity" }
```

In the runtime filter, before activation, validate schema, geometry compatibility, dimensions, and sample meshes. Invalid/unsupported data yields identity behavior with OTD-log diagnostics.

### Applying, saving, and status

Copy accepted working data as complete compact single-line JSON to the clipboard. In OTD: select the intended tablet, open Filters, select Calibration Suite, paste into **Calibration data**, enable it, and **Save** (persists and applies). OTD serializes the string automatically; users never escape JSON or edit settings files. **Apply** updates runtime without proving a disk save.

Section 1's clear-and-Save workflow resets only this calibration to identity; the enable checkbox bypasses correction while retaining data. OTD settings Open/Save as and presets preserve embedded calibration for backup/restore/manual switching, but affect the whole settings document. Preset selection/application does not prove a disk save. Add no separate calibration-file workflow.

The Calibrator never calls GetSettings, SetSettings, or ResetSettings and never reads or writes OTD settings files. Its only daemon mutation is temporary SetTabletDebug acquisition. Leave configuration/persistence and destination-profile choice in OTD.

Copying changes no OTD settings. Show a paste-and-Save reminder without claiming that the result was applied, saved, or executed. Use OTD logs for runtime failures. Do not add applied-content comparison or a parallel log viewer.

## 6. Verification and deferred accuracy testing

The initial release includes capture quality gates, structural data validation, automated correction tests, and manual device/integration checks. It does not include guided accuracy-validation sessions, generated test targets/poses, before/after error reports, comparison against previous calibration, or validation-result tracking and skip actions. These are deferred and do not block initial delivery.

After accepting, copying, and saving calibration in OTD, users try actual cursor behavior and can return to the still-open Calibrator to review/recapture current-session measurements. After closing the Calibrator, a new capture is required. Acceptance confirms the dataset to copy; it does not certify physical accuracy. Manual device checks should cover positions and orientations between captures and near coverage boundaries, with correction enabled and bypassed. Record observed behavior and limitations without inventing an accuracy threshold.

A future guided accuracy-testing feature may reuse acquisition and the shared evaluator to compare raw, candidate, and previous correction on identical held-out reports. Its detailed workflow and defaults require a separate specification before implementation. Do not build placeholder UI or validation state for it now.

### Required implementation checks

| Checks               | Coverage                                                                                                                                                                                                                                                                                                                                                                                                                                                                   |
| -------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Math/data            | Exact node tilt-vertex reproduction within numeric tolerance; combined spatial residuals including large offsets/gradients; triangle/grid continuity; hull clamping; upright and circular azimuth/signs; identity, geometry mismatches, malformed data                                                                                                                                                                                                |
| Workflow/integration | Candidate accept/discard and session-only pending data; OTD Plugin Manager, generated property paste/clear, enable checkbox, Save/Apply, settings/presets round-trip; filter PreTransform stage and documented ordering responsibilities; report reconstruction/interfaces/events; automatic connection, single-tablet detection, and pen absence/resumption; monitor DPI/offsets; sampling/rendering during pen contact |
| Device               | Orientation semantics, capture defaults, held-out positional/tilt accuracy; mathematical tests alone do not establish hardware accuracy                                                                                                                                                                                                                                                                                                                                    |

## 7. OTD source references

Integration was checked against the adjacent `OpenTabletDriver-0.6.7` tree. These sources establish OTD behavior, not universal tilt signs/ranges or physical accuracy.

Use the adjacent `Sample Plugins` tree as a source of examples and ideas, including packaging conventions. Its implementations are not necessarily optimal or appropriate for this project. Evaluate approaches against this specification and OTD 0.6.7's actual contracts, considering correctness, maintainability, and runtime cost; do not copy patterns solely because a sample uses them. Both adjacent trees are read-only references for this project.

| Concern                                     | Source paths within that tree                                                                                                                                                                                                                                                                                   |
| ------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Profiles/filter construction                | `OpenTabletDriver.Desktop/Profiles/Profile.cs`; `OpenTabletDriver.Desktop/Profiles/ProfileCollection.cs`; `OpenTabletDriver.Daemon/DriverDaemon.cs` (`SetSettings`, `SetOutputModeElements`)                                                                                                                    |
| Settings/injection                          | `OpenTabletDriver.Desktop/Reflection/PluginSettingStore.cs`                                                                                                                                                                                                                                                     |
| Pipeline order                              | `OpenTabletDriver.Plugin/Output/OutputMode.cs`; `OpenTabletDriver.Plugin/Output/PipelineManager.cs`                                                                                                                                                                                                             |
| Mapping/dimensions                          | `OpenTabletDriver.Plugin/Output/AbsoluteOutputMode.cs`; `OpenTabletDriver.Plugin/Tablet/DigitizerSpecifications.cs`                                                                                                                                                                                             |
| Debug reports/RPC                           | `OpenTabletDriver/Devices/DeviceReader.cs`; `OpenTabletDriver.Desktop/RPC/DebugReportData.cs`; `OpenTabletDriver.Desktop/RPC/RpcClient.cs`; `OpenTabletDriver.Desktop/Contracts/IDriverDaemon.cs`; `OpenTabletDriver.UX/RPC/DaemonRpcClient.cs`                                                                 |
| Tilt                                        | `OpenTabletDriver.Plugin/Tablet/ITiltReport.cs`; `OpenTabletDriver.Plugin/Tablet/TiltTabletReport.cs`; `OpenTabletDriver.Configurations/Parsers/Wacom/IntuosV2/IntuosV2Report.cs`                                                                                                                               |
| UI/save/apply                               | `OpenTabletDriver.UX/Controls/GeneratedControls.cs`; `OpenTabletDriver.UX/MainForm.cs` (`SaveSettings`, `ApplySettings`); `DriverDaemon.GetSettings`                                                                                                                                                            |
| Filter selection/ordering limitations       | `OpenTabletDriver.UX/Controls/PluginSettingStoreCollectionEditor.cs`; `OpenTabletDriver.Desktop/Reflection/PluginSettingStoreCollection.cs` (`FromType`)                                                                                                                                                        |
| Plugin installation and UI extension limits | `OpenTabletDriver.UX/Windows/Plugins/PluginManagerWindow.cs`; `OpenTabletDriver.UX/Controls/PluginSettingStoreEditor.cs`; `OpenTabletDriver.Plugin/Attributes/ActionAttribute.cs`                                                                                                                               |
| Existing client/type initialization         | `OpenTabletDriver.UX/MainForm.cs` (`HandleDaemonConnected`); `OpenTabletDriver.Desktop/Reflection/DesktopPluginManager.cs`; `OpenTabletDriver.Desktop/RPC/DebugReportData.cs` (`ToObject`); `OpenTabletDriver.Console/Program.IPC.cs`; `Directory.Build.props`                                                  |
| OTD cursor mapping/display (reference only)                | `OpenTabletDriver.Desktop/Output/AbsoluteMode.cs`; `OpenTabletDriver.Desktop/Interop/DesktopInterop.cs`; `OpenTabletDriver.Desktop/Interop/Display/WindowsDisplay.cs`; `OpenTabletDriver.UX/Controls/Output/AbsoluteModeEditor.cs`; `OpenTabletDriver.Desktop/Interop/Input/Absolute/WindowsAbsolutePointer.cs` |
| Backup/restore/presets                      | `OpenTabletDriver.UX/MainForm.cs` (`LoadSettingsDialog`, `SaveSettingsDialog`, `SavePresetDialog`, `PresetButtonHandler`); `OpenTabletDriver.Desktop/PresetManager.cs`                                                                                                                                          |
