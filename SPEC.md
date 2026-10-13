# Calibration Suite specification

A Windows calibrator and OTD 0.6.7 PreTransform filter for display tablets. The app measures pen position error at screen targets; the filter interpolates those measurements. This is an approximate error field, not an exact inverse or a physical-accuracy guarantee.

## Components and ownership

- **Calibrator:** WPF guidance, raw-report acquisition, session review, and copying accepted data.
- **Filter:** changes only position on the original OTD report and emits it once, in order.
- **Core:** shared data validation, capture calculations, mesh construction, and correction. No WPF or RPC dependency.

OTD owns installation, profiles, filter ordering, Apply/Save, presets, backups, and logs. The app does not inspect or edit settings, load earlier calibrations, or manage calibration files. Its only daemon mutation is temporary `SetTabletDebug` acquisition. Property setters only store values; preparation runs after tablet/settings injection through `OnDependencyLoad`.

Users capture/review in the app, copy accepted data, paste into the intended profile's standard **Calibration data** string property, and **Save** in OTD. Apply changes runtime without proving a disk save. Clear and Save resets this calibration; disabling its filter bypasses correction while retaining data. Same-name tablets share an OTD profile, so destination tablet/pen selection remains the user's responsibility.

## Acquisition and display mapping

Use `RpcClient<IDriverDaemon>` on `OpenTabletDriver.Daemon`. Follow OTD initialization: `GetApplicationInfo`, `AppInfo.Current`, and `DesktopPluginManager.Load`. Reconstruct `DebugReportData` with OTD's `ToObject`; diagnose unknown types rather than guessing. Subscribe directly to `DeviceReport`, enqueue bounded work, and process reports serially off the UI thread. WPF displays snapshots.

Connect automatically and retry while idle. Require exactly one connected tablet and match serialized reference contents, not object identity. OTD debug reports contain no hardware timestamp or sequence number; timestamp each event at ingress with a monotonic clock. Identical events count separately, and some upstream loss is undetectable.

Enable debug only for capture and always attempt ordered enable-then-disable cleanup. OTD has no debug ownership token, so concurrent debugger use is unsupported. Disconnects, tablet changes, display/DPI changes, queue overflow, stale work, or blocked processing stop acquisition. Pen absence between bursts preserves progress; hide stale readings. Unconfirmed cleanup requires restart. [Dependency notes](docs/DEPENDENCIES.md#acquisition-limits) give the implementation limits.

The user moves the setup window onto the tablet screen. Windows selects its largest-intersection monitor, or the nearest monitor if off-screen. Show an opaque borderless window covering that entire monitor, including the taskbar area.

Support aligned full-digitizer/full-screen mapping: X right, Y down. Rotated/cropped mappings and non-display tablets are outside scope. Place raw target `(x, y)` at:

```text
screenX = x / MaxX * viewportWidth
screenY = y / MaxY * viewportHeight
```

WPF viewport dimensions are in DIPs; monitor origins affect window placement only. Never use cursor position, OTD transforms, or injected pointer output for measurement. Ask users to aim with the physical nib and disable other filters to reduce cursor distractions. Debug reports precede filters. Runtime correction must precede filters that change position or tilt, with exactly one enabled Calibration Suite entry.

## Capture settings and targets

| Setting | Default |
| --- | --- |
| Rows / columns | 3 / 3 |
| Grid inset | 15 mm |
| Tilt directions / levels | 8 / 2 |
| Tilt range / tolerance | 40 / 8 reported units |
| Burst duration / minimum reports | 200 ms / 20 |
| Position / tilt stability | 0.5 mm / 3 reported units |
| Report gap / total capture timeout | 500 / 2000 ms |

Require at least two rows/columns, nonnegative integer direction/level counts, positive finite limits, and a total timeout longer than the burst. If both tilt counts are positive, require at least three directions and level spacing greater than twice the tilt tolerance. Reject more than 10,000 captures before allocating.

For `R` rows, `C` columns, `A` directions, and `N` levels, visit targets row-major. At each target, capture one reference, then levels `1..N` in each direction `0..A-1`. The total is `R*C*(1+A*N)` when tilt is enabled, otherwise `R*C`. Direction `k` is `360*k/A` degrees in the reported-coordinate plane; level `j` has offset magnitude `j*TiltRange/N`.

Resolve the grid from positive finite digitizer maxima/dimensions. For inset `m`, require `0 < m < min(WidthMm, HeightMm)/2`:

```text
insetX = m / WidthMm * MaxX
insetY = m / HeightMm * MaxY
X[c] = insetX + c / (C - 1) * (MaxX - 2*insetX)
Y[r] = insetY + r / (R - 1) * (MaxY - 2*insetY)
```

Persist resolved grid coordinates and reject invalid input without silently changing it.

## Reported tilt

Use unchanged `ITiltReport.Tilt` values. The first stable measurement at each target sets its reference; later requested tilts are that reference plus the generated offset. Reference and coverage need not include raw `(0, 0)`.

OTD metadata has no reliable tilt-capability flag. If the first eligible pen report lacks tilt, switch the draft to position-only and block tilt poses for that session, even if later reports include tilt. Zero tilt is not evidence of missing support. Users can choose position-only by setting either tilt count to zero.

In position-only mode, ask the user to hold upright; posture cannot be verified without tilt. Retain actual tilt when reported, otherwise use an unused zero placeholder. A tilt session that later loses paired tilt rejects that attempt; never reuse earlier tilt or change modes silently.

Guidance is a linear reported-coordinate plot at the nib: X right, Y up. A thin arrow shows live tilt relative to the reference; a circular outline shows the requested offset and tolerance. It makes no claim about physical pen angle, perpendicularity, or eraser direction. No model-specific adapter is needed.

## Capture and review

Space starts one burst; suppress autorepeat and overlapping attempts. Capture rules:

1. Require finite absolute pen position and, in tilt mode, paired finite tilt. Ignore auxiliary reports; reject eraser, out-of-range, and reported proximity loss. Pressure does not trigger or gate capture.
2. For tilted poses, every report must lie within the requested reported-coordinate tolerance circle. Reference and position-only captures have no pose gate.
3. Collect until both duration and report minima pass. Burst duration starts at the first eligible report; initial gap and total timeout start at Space. Exclude events received before Space. A failed gate, gap, timeout, or stability check discards only this attempt.
4. Compute component-wise position and tilt medians, averaging the two middle values for even counts. Compute each report's Euclidean deviation from those medians. Convert position displacement on each axis to millimeters; leave tilt in reported units. Stability uses sorted one-based rank `ceil(0.95*count)`, without trimming outliers.
5. Store median position and actual median tilt. Never substitute requested coordinates. Reject duplicate measured tilts; validate each completed node's mesh before committing its final sample. Failed replacements preserve earlier data.

These gates limit noise and movement; they cannot verify aiming accuracy.

Keep unfinished drafts, completed candidates, and accepted data separately in memory. Only acceptance replaces accepted data. Targeted recapture copies a completed candidate/accepted result and clears the selected pose or node; other samples remain. New capture, cancellation, restart, and discard preserve accepted data. Copy uses a cached accepted JSON snapshot.

| Key | Action |
| --- | --- |
| Space | Capture one burst |
| Backspace | Revisit previous pose, including across targets; stay within a targeted recapture selection |
| R | Clear and restart the current capture/recapture selection |
| Esc | Cancel acquisition, return to setup, preserve completed measurements |

Resume retains draft settings and checks digitizer geometry; use fresh display geometry. Starting over confirms discard of unaccepted data. Review shows requested poses, targets, measured positions/tilts, and errors. Recapture works only on completed results. Acceptance is unavailable while an unfinished draft exists.

Keep all 13 settings visible in responsive groups; smaller windows scroll. Full-screen capture ignores mouse/stylus/touch actions. Show precise one-DIP crosshair/arrow/outline strokes, a small open nib marker, local readiness/progress/retry cues, and numeric readings in corner cards. Scale guidance to fit without moving the nib or enlarging tolerance. Closing always warns about session data, even after Copy: the app cannot observe an OTD Save.

## Correction

At target `T`, each sample stores measured position `p` and tilt `t`. Its error is `p - T`. Requested-pose metadata never affects interpolation.

A one-sample node returns a constant error. Multi-sample nodes require at least four distinct finite tilts and a usable mesh retaining every sample; zero, two, or three samples are invalid. Sort tilts lexicographically, then use NetTopologySuite Delaunay triangulation with zero snapping tolerance and robust orientation predicates. Do not jitter, merge samples, or repeat the library's in-circle audit. Collinear boundary samples and cocircular configurations are usable when a valid mesh retains them.

Within a triangle, interpolate vertex errors barycentrically. Outside a node's convex hull, project to the nearest raw-tilt boundary segment and interpolate there; never extrapolate. Triangle interpolation is continuous but can change slope at edges. It is a practical choice, not a claim of superior physical accuracy.

For raw position `p`, clamp only the grid lookup, find its cell, and evaluate each of the four node meshes at current tilt. Blend using horizontal/vertical fractions `u,v`:

```text
error = (1-u)*(1-v)*E[r,c] + u*(1-v)*E[r,c+1]
      + (1-u)*v*E[r+1,c]   + u*v*E[r+1,c+1]
correctedPosition = p - error
```

Lookup uses original raw position, not corrected or captured position. Output is not clamped to the inset grid. Large offsets/gradients can leave residual error even at training targets.

Prepare private evaluator snapshots outside the report path. Correction performs no parsing, mesh construction, I/O, allocation, or long-lived locking. Mutate only the original report's position, including boxed structs. Identity, auxiliary, eraser, out-of-range, lost-proximity, unusable position, and missing/nonfinite required tilt pass unchanged. Unrepresentable float output also passes unchanged. Log initialization failures and one warning after repeated unusable pen reports, without per-report logging.

## Data contract

The project is unreleased: there are no schema versions, migrations, or backward-compatibility guarantees. Update producer, consumer, examples, and tests together.

| Field | Meaning |
| --- | --- |
| `mode` | `identity` or `measured` |
| `sourceGeometry` | Required measured-mode `maxX`, `maxY`, `widthMm`, `heightMm` |
| `grid.preTransformRowCoordinates` | Raw Y target coordinates |
| `grid.preTransformColumnCoordinates` | Raw X target coordinates |
| `nodes[r][c].samples` | Measurements for that row/column target |
| Sample `position.x/y`, `tilt.x/y` | Required measured medians |
| Sample `requestedPose` | Optional descriptive `magnitude` and `directionDeg` |

Names/modes are case-sensitive. Ignore unknown optional properties; reject unknown modes and missing/null required data. Require finite numbers, increasing bounded grid axes with at least two coordinates, and matching node dimensions. Nonuniform grids and different sample counts, including mixed constant/tilt nodes, are allowed.

Compare all four geometry fields with relative tolerance `1e-6`; mismatches require recalibration, never rescaling. Geometry equality does not prove tablet/pen equivalence. Derive errors and targets instead of storing them redundantly.

A minimal fictional position-only example follows. It illustrates the schema and must not be installed on hardware:

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
          { "position": { "x": 152, "y": 153 }, "tilt": { "x": 0, "y": 0 } }
        ]
      },
      {
        "samples": [
          { "position": { "x": 852, "y": 153 }, "tilt": { "x": 0, "y": 0 } }
        ]
      }
    ],
    [
      {
        "samples": [
          { "position": { "x": 152, "y": 853 }, "tilt": { "x": 0, "y": 0 } }
        ]
      },
      {
        "samples": [
          { "position": { "x": 852, "y": 853 }, "tilt": { "x": 0, "y": 0 } }
        ]
      }
    ]
  ]
}
```

Identity is `{ "mode": "identity" }`; an empty filter property also gives identity. Validate pasted data fully before activation. Invalid data or geometry yields identity with an OTD diagnostic, without rewriting the input JSON.

## Verification

Use independent expectations for vertex reproduction, triangle/grid continuity, hull projection, spatial residuals, reported-coordinate gates, nonzero references, invalid-data fallback, and snapshot ownership. Integration checks cover real OTD package loading/settings serialization, report preservation/order, bounded acquisition/cleanup, capture/retry/recapture, WPF layouts/input, and copied-data correction.

[Release checks](docs/RELEASE.md) cover the remaining device workflow. Synthetic checks cannot establish accuracy, physical angle scale, or broad hardware compatibility. Guided accuracy-testing sessions and held-out before/after reports are deferred; do not add placeholder UI for them.

## OTD contract references

Use the pinned source under `artifacts/otd-source`, prepared by a normal build. The adjacent `OpenTabletDriver-0.6.7` and `Sample Plugins` trees were also reviewed as read-only references. Key upstream files:

| Contract | Paths within OTD |
| --- | --- |
| Acquisition/reconstruction | `OpenTabletDriver.Desktop/Contracts/IDriverDaemon.cs`, `OpenTabletDriver.Desktop/RPC/DebugReportData.cs`, `OpenTabletDriver/Devices/DeviceReader.cs` |
| Plugin loading/injection | `OpenTabletDriver.Desktop/Reflection/DesktopPluginManager.cs`, `OpenTabletDriver.Desktop/Reflection/PluginSettingStore.cs` |
| Reports/geometry | `OpenTabletDriver.Plugin/Tablet/ITiltReport.cs`, `OpenTabletDriver.Plugin/Tablet/DigitizerSpecifications.cs` |
| Pipeline ordering | `OpenTabletDriver.Plugin/Output/OutputMode.cs`, `OpenTabletDriver.Plugin/Output/PipelineManager.cs` |
| Profiles/settings/presets | `OpenTabletDriver.Desktop/Profiles/Profile.cs`, `OpenTabletDriver.Desktop/PresetManager.cs`, `OpenTabletDriver.Daemon/DriverDaemon.cs` |
| Generated property UI and Save/Apply | `OpenTabletDriver.UX/Controls/GeneratedControls.cs`, `OpenTabletDriver.UX/Controls/PluginSettingStoreEditor.cs`, `OpenTabletDriver.UX/MainForm.cs` |
| Installation/UI extension limits | `OpenTabletDriver.UX/Windows/Plugins/PluginManagerWindow.cs`, `OpenTabletDriver.Plugin/Attributes/ActionAttribute.cs` |

UX/Daemon source is available in the exact source ZIP or adjacent reference tree. These contracts do not define universal tilt signs or physical accuracy.
