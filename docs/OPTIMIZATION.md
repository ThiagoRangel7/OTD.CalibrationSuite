# Repository optimization — 2026-10-12

The first-party source, tests, WPF verification harness, build configuration, dependency preparation, and packaging were reviewed. This pass reduces repeated processing and allocation while retaining the existing acquisition, capture, correction, and persistence rules. Dependency versions and pinned upstream sources remain as recorded in [DEPENDENCIES.md](DEPENDENCIES.md).

## Earlier optimization changes

- The earlier `TabletSelection` implementation compared OTD 0.6.7 fields directly to avoid JSON allocation. The final simplification below replaces this manual field mirror with the existing serializer's structural comparison. Complete contract, dictionary/list, bundled configuration, and field-mutation checks are retained.
- `NodeMesh` prepares triangle bounds, edge vectors, and inverse denominators once. Cheap bounds checks skip irrelevant triangles before robust NTS orientation tests. Nearest-hull projection still retains boundary samples, and unusable boundary arithmetic now has an explicit diagnostic. Prepared correction remains allocation-free.
- `CaptureBurst` reuses one sorting buffer for four medians and two nearest-rank percentiles. It retains all original reports and the existing per-report angle gates, even-median rule, stability thresholds, timing, and retry behavior. A smaller later burst cannot include stale scratch-buffer values.
- `CaptureDraft` maintains its captured count as measurements are stored or cleared. Replacement, rejected capture, repeated restart, and targeted recapture preserve the count correctly. Export copies contiguous node samples and prepares each mesh once; capture-only checks already ran before each completed node was committed.
- Acceptance serializes a validated snapshot once. Repeated copying returns that accepted payload without rebuilding meshes or JSON. New capture, recapture, and discard preserve it until another successful acceptance.
- `CaptureSettings.PoseCount` supplies layout counts consistently and pose generation allocates its final array directly. Geometry validation avoids a temporary collection.
- WPF cards keep automatic heights rather than forcing new layout through repeated height resets. Fonts and sizing update when the viewport changes. Caption collision checks reuse the widened target outline until pose, tolerance, anchor, or scale changes. Core overlay placement avoids LINQ iterators and temporary corner arrays.
- The filter's package target tracks assemblies, Debug symbols, metadata, notices, script, and project definition. Unchanged builds retain the existing ZIP; changed inputs or missing output regenerate it using the existing deterministic package script.

## Earlier optimization measurements

The new `scripts/PerformanceChecks` runner uses synthetic data, a round-tripped Huion configuration, a 10,000-slot draft, a default 3 × 3 / 17-pose evaluator, and 64-report bursts. It warms each action for 1,000 iterations and reports the median of five passes plus managed bytes per operation. Outputs are consumed to prevent unused-result elimination. It opens no daemon connection.

The baseline was rebuilt from the pre-change core and reference-comparison sources at commit `1da0d31`, using the same runner and pinned dependencies. Both runs used Release, SDK 10.0.401, and the installed .NET 8 runtime on this Windows host.

| Operation | Baseline median | First pass median | Baseline → first pass allocation |
| --- | ---: | ---: | ---: |
| Complete tablet-reference comparison | 60.31 µs | 0.72 µs | 44,480 → 0 bytes |
| Captured-count access, 10,000 slots | 17.73 µs | Constant-time field read | 32 → 0 bytes |
| Correction inside tilt hull | 0.264 µs | 0.118 µs | 0 → 0 bytes |
| Correction outside tilt hull | 1.377 µs | 0.960 µs | 0 → 0 bytes |
| Capture burst, 64 reports | 29.60 µs | 21.42 µs | 14,912 → 2,656 bytes |

These are local microbenchmarks; CPU scheduling and tiered compilation affect timing. The constant count getter is below useful timing precision in this harness. The first pass reduced burst allocation by approximately 82% and removed reference-comparison JSON allocation. The subsequent readability refinement below reduces burst allocation further.

Run with:

```powershell
dotnet run --project scripts/PerformanceChecks -c Release
```

## Readability refinement

The user's follow-up requested simpler, readable code while retaining the measured gains. Production C# is **18 lines smaller** than the first optimization pass; regression tests and the benchmark runner are retained. Changes reduce duplication and state rather than compressing statements:

- Mesh preparation builds its cached triangles during validation, avoiding a second construction pass. NTS's cached `Envelope` supplies inclusive triangle bounds in place of bespoke minimum/maximum calculations. Robust predicates, deterministic ordering, and prepared boundary arithmetic remain.
- Burst medians use named static coordinate selectors instead of numeric coordinate codes. One deviation helper supplies both position and tilt percentiles, preserving the original millimeter conversion and sorting buffer.
- `RequestedPose` is a small immutable value (`readonly record struct`), consistent with `Point2`. Orientation conversion no longer allocates a pose object per report. Its JSON properties, required altitude, nullable azimuth, and optional sample guidance keep their existing representation.
- Capture traversal stores the first and exclusive end indices of its selected range. Full capture, single-pose recapture, and node recapture are all contiguous; an allocated itinerary array was unnecessary. Restart uses that range directly, and Previous stops at its first index. Existing recapture tests now also check that boundary.
- Capture cards use a `StackPanel` instead of an automatic-row grid with a row-adding helper. The widened outline cache uses null to mean invalid, removing a separate dirty flag. Overlay placement lists four explicit corners rather than deriving them from index arithmetic.

The same runner measured the first pass immediately before editing and the simplified implementation afterward:

| Operation | Before simplification | After simplification | Allocation before → after |
| --- | ---: | ---: | ---: |
| Complete tablet-reference comparison | 0.722 µs | 0.730 µs | 0 → 0 bytes |
| Captured-count access | Constant time | Constant time | 0 → 0 bytes |
| Correction inside tilt hull | 0.118 µs | 0.121 µs | 0 → 0 bytes |
| Correction outside tilt hull | 0.940 µs | 0.941 µs | 0 → 0 bytes |
| Capture burst, 64 reports | 20.00 µs | 21.07 µs | 2,656 → 80 bytes |

The original matching/interpolation gains remain, with small timing variation between runs. Burst allocation drops another 97%; the remaining 80 bytes are the accepted sample, approximately 99.5% below the original baseline. A full 10,000-slot session also avoids its former 40,000-byte itinerary payload.

## Earlier optimization verification

- Locked restores for the solution and performance runner pass; existing dependency locks are unchanged.
- Debug and Release solution builds pass with zero warnings/errors. Both configurations pass **175 non-live tests**, up from 166. Existing tests retain vertex reproduction, continuity, determinism, hull clamping, capture gates, cleanup, report identity/order, and OTD Plugin Manager installation checks.
- New regressions cover complete reference contents and every bundled tablet configuration, allocation-free matching, unrelated tablet reports, shorter subsequent bursts, partial recapture/repeated restart counts, accepted payload snapshots, and independently calculated projection onto an oblique hull edge.
- Offscreen WPF verification passes normal, small, and high-DPI capture layouts, all feedback states/directions, keyboard/input suppression, and setup/review. A reused capture view also resizes between normal/compact layouts and adapts to changing reading lengths without stale placement or clipping. Representative renders were inspected. After simplification, the final 300-update run measured 0.62 ms median / 0.75 ms p95, in the same sub-millisecond range as the baseline.
- Incremental Release packaging preserves ZIP modification time on an unchanged build, recreates a missing ZIP with identical bytes, and regenerates after metadata modification. Existing Debug/Release installation tests verify included DLLs and symbol selection.
- Formatting verification of the changed core/comparer and Git whitespace checks pass.

Installed hardware accuracy, physical orientation semantics, and the outstanding milestone 4/5 user checks remain pending. These synthetic checks do not establish them. No release has been published.

## Simplification

The first refactor added wrapper classes and a read-only lifecycle without enough simplification. The user's follow-up prioritizes fewer abstractions and less internal defensive code. The revised implementation removes `CaptureSession`, `CaptureLayout`, `CaptureOrientation`, and `OverlayPosition`, along with their forwarding methods and duplicate checks.

- The main window coordinates acquisition and capture directly. Drafts own their resolved grid/poses; recapture copies samples. Accepted JSON stays a cached snapshot. Completed-result freeze state and guards against internal API misuse are removed.
- Settings validation checks the capture limit once using overflow-safe arithmetic. Geometry/inset validation no longer builds temporary grid arrays on every editor update. The count getters operate on validated settings.
- Capture separation is checked when storing a pose, and mesh/coverage checks run when a node is complete. Export groups the flat sample array into nodes/rows with `Chunk`, without reconstructing already-validated meshes. Runtime loading still validates pasted calibration in full.
- The view computes live orientation once and suppresses azimuth for the gate near upright. The Huion adapter uses its verified signs directly; an unused generic sign-configuration API is removed. One overlay placement method returns complete bounds, replacing primary/secondary wrappers and repeated conversions. The unused preview-only target generator is removed.
- Tablet matching delegates structural equality to Newtonsoft.Json instead of maintaining separate comparers for every OTD model and collection. Acquisition prepares the expected JSON tree once. This automatically follows the serialized contract and preserves dictionary-order independence, list order, and decimal dimensions. It trades allocation-free matching for less maintenance code; the runtime correction filter is unaffected.

Functional regressions remain for report eligibility, queue/cleanup failures, sampling gates, capture preservation, recapture, export snapshots, interpolation, and actual WPF layout/input. Tests of deleted wrappers and internal misuse guards were removed with those APIs. No runtime package, calibration JSON, sampling threshold, or correction equation changes are introduced.

Measured against the saved working source at the start of this simplification pass, across first-party `src/**/*.cs` only (excluding build outputs, tests, and docs):

| Measure | Original working source | First refactor | Final |
| --- | ---: | ---: | ---: |
| Production C# lines | 2,229 | 2,484 | 2,227 |
| C# syntax tokens | 22,410 | 23,024 | 21,015 |
| Type declarations | 51 | 54 | 50 |
| Methods, constructors, and local functions | 171 | 187 | 159 |

This pass removes **257 production lines (10.3%)**, **2,009 syntax tokens (8.7%)**, four types, and 28 method/constructor/local-function declarations from the first refactor. Compared with the source before either refactor, line count is nearly unchanged while syntax tokens fall **6.2%** and 12 declarations disappear. Token counts use the SDK's Roslyn parser and exclude whitespace/comments, so formatting cannot account for that decrease. The original source was reconstructed from Git HEAD plus the patch saved before the first refactor; these comparisons include the pre-existing uncommitted work and are not against Git HEAD alone.

Final verification: Debug and Release build with zero warnings/errors and pass **188 non-live tests each**. The 0.1° separation boundary is now tested through draft storage rather than a deleted helper. Offscreen WPF/input checks pass; all **62 rendered PNGs are byte-identical** to the first refactor's renders. The final synthetic update/layout run measured 0.65 ms median / 1.05 ms p95. Formatting and Git whitespace checks pass. Hardware verification remains pending.

The final performance runner measures prepared tablet matching at **30.59 µs and 22,240 bytes per incoming reference**, versus approximately 0.80 µs / zero bytes with the manual comparer. At 200 reports/second this corresponds to about 6 ms of matching work and 4.4 MB of allocation per second, excluding RPC/report reconstruction. This is an explicit acquisition cost accepted for simpler code; it does not affect the drawing filter. Count access and correction still allocate zero bytes; correction medians are 0.123 µs inside coverage and 0.956 µs outside. Accepted 64-report bursts retain 80 bytes of allocation and a 20.43 µs median. These are local synthetic measurements.
