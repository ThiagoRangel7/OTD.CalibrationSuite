# Milestone 2 review — 2026-09-13

**Awaiting user review.** The user explicitly requested implementation of milestone 2, approving advancement from milestone 1 with its physical checks still pending. Milestone 3 has not begun.

## Implemented

- Measured/identity models and compact, case-sensitive JSON. Required measured fields, finite values, geometry compatibility, increasing grid coordinates, node dimensions, and usable meshes are validated. Unknown optional properties are ignored. Empty data is identity; malformed/unsupported data returns identity with a diagnostic.
- Configurable grid and pose generation with all spec defaults and validation. Default layout is nine targets and seventeen poses per target, or 153 captures. PreviewGeometry now reuses the shared grid calculation. No new capture UI is added.
- Angle guidance from explicitly screen-aligned tip-to-eraser tangents, upright azimuth suppression, and circular azimuth distance. No device axis/sign adapter is enabled without physical verification. Stored raw tilt is unchanged.
- Lexicographically sorted measured samples, NTS Delaunay triangulation, robust orientation checks, and barycentric error interpolation. Exact duplicate tilts are rejected; distinct collinear/cocircular samples are retained when a usable mesh exists.
- Every loaded multi-sample node strictly surrounds raw tilt origin. Multi-sample capture completion separately requires its explicitly identified measured upright inside the hull and rejects poses within 0.1 degree of one another. Loaded data need not identify upright or contain requested-pose metadata.
- Outside-hull lookup projects to the nearest raw-tilt boundary segment, including collinear boundary samples. Four nodes independently evaluate their meshes before spatial blending on the possibly nonuniform target grid. Only lookup is clamped; correction subtracts the blended error from original raw position.
- Prepared snapshots own private copies of evaluator inputs and perform no per-report managed allocation in the tested paths. Source arrays remain editable for future session UI; edits cannot alter an existing snapshot.

Covered requirements: spec section 2 layout/orientation math, section 4 interpolation and preparation, and section 5 schema/geometry/identity behavior. Capture burst processing remains milestone 4, and report mutation/logging/OTD property activation remain milestone 3. The filter is still pass-through and the app still previews readings.

## Where to review

1. [PreparedCalibration.cs](../src/CalibrationSuite.Core/PreparedCalibration.cs): start at Correct for the overall formula, then Prepare/Load for validation and identity diagnostics.
2. [NodeMesh.cs](../src/CalibrationSuite.Core/NodeMesh.cs): measured error construction, hull rules, barycentric evaluation, and boundary clamping.
3. [CalibrationData.cs](../src/CalibrationSuite.Core/CalibrationData.cs): schema and compact serialization. [CaptureLayout.cs](../src/CalibrationSuite.Core/CaptureLayout.cs): defaults, grid, requested poses, and angle conventions.
4. [InterpolationTests.cs](../tests/CalibrationSuite.Tests/InterpolationTests.cs), [CalibrationDataTests.cs](../tests/CalibrationSuite.Tests/CalibrationDataTests.cs), and [CaptureLayoutTests.cs](../tests/CalibrationSuite.Tests/CaptureLayoutTests.cs): independent expected values and rejection cases. [Dependency decisions](DEPENDENCIES.md#correction-geometry) explain the geometry library.

## Validation

**93 synthetic tests passed**, zero failures/skips: 67 core cases and 26 integration/acquisition cases. Locked restore passed. The Release solution build passed with zero warnings/errors. Whitespace/diff checks passed.

From the repository on Windows with the README prerequisites and prepared OTD sources:

~~~powershell
dotnet restore CalibrationSuite.sln --locked-mode --force
dotnet build CalibrationSuite.sln -c Release --no-restore -p:BuildDate=2026-09-12
dotnet test tests/CalibrationSuite.Tests/CalibrationSuite.Tests.csproj -c Release --no-build --no-restore --filter 'Category!=LiveDaemon' --logger 'trx;LogFileName=milestone2.trx'
git diff --check
~~~

The first restricted-network locked restore could not retrieve NuGet vulnerability metadata. A network-enabled forced locked restore refreshed those cached failures successfully; no audit or warning policy was disabled. Test results are in the ignored tests/CalibrationSuite.Tests/TestResults directory. No live daemon check was rerun because this milestone changes no RPC behavior.

Tests cover field omission/null/nonfinite values, unsupported modes/casing, all four geometry tolerance fields, immutable snapshots, default/custom layouts, circular angles, vertex reproduction including offset upright, triangle/grid continuity, nearest hull edges/corners, polygonal-ring clamping, independent node boundaries, differing sample counts, collinear/cocircular configurations, sample-order determinism, and original-position residuals.

## User review checks

These fixtures are fictional and must not be pasted into OTD. No OTD setting changes or physical calibration are required for this review.

1. **Run the commands above.** Expect a clean build and 93 passing tests. Run the four core test classes alone with `--filter 'FullyQualifiedName~CalibrationDataTests|FullyQualifiedName~CaptureLayoutTests|FullyQualifiedName~InterpolationTests|FullyQualifiedName~UprightCalibrationTests'` if reviewing only the 67 core cases.
2. **Check interpolation.** In BarycentricInterpolationMatchesIndependentTriangleCalculation, weights at tilt (10,20) are 1/4, 1/4, 1/2 and the expected error is (10,7). In OutsideHullProjectsToNearestEdgeOrCorner, (60,10) projects to (40,10), while (60,60) projects to (40,40).
3. **Check the residual limit.** SpatialBlendUsesNonuniformGridAndOriginalPositionWithExpectedResidual uses error (0.4*x, 0.2*y). Raw (500,500) corrects to (300,400). A capture at (840,360) for target (600,300) corrects to (504,288), leaving residual (-96,-12). This is the specified approximate field, not an exact inverse or an accuracy guarantee.
4. **Check data rejection and preservation.** Review the required-field and source-array mutation tests. Expect useful diagnostics and unchanged source data, with identity fallback on rejected loads. Guidance metadata must not move a measured sample or affect correction.
5. **Check capture-only rules.** LoadedMeshesNeedOriginCoverageButNotAnUprightSampleOrCaptureSeparation accepts a surrounding ring without upright metadata, but rejects that ring as a completed capture when its identified upright lies on the boundary. Close new captures require recapture; distinct loaded samples are not merged.

## Carried forward and remaining

The simplification review retained measured-triangle interpolation, sample retention, usable arithmetic, and hull coverage checks. It removed the separate in-circle audit: the library chooses the triangulation, and interpolation does not require an independently certified Delaunay optimum. See [the spec's interpolation decision](../SPEC.md#interpolation-decision) for the rationale and limits. JSON deserializes directly into the current model. No format/version tags or compatibility paths remain; this is an unreleased project.

The upright-only core/data follow-up is implemented: either zero count generates upright alone; one finite measured sample defines a constant error with no triangle construction. Calibration JSON and spatial blending are unchanged. Mixed constant and triangulated nodes also evaluate naturally. Ten added tests cover zero counts, preserved measured tilt, JSON round-trip, spatial correction and boundary behavior, capture completion, mixed nodes, and invalid data. The per-report finite paired-position/tilt eligibility rule remains unchanged. UI controls and actual capture belong to milestone 4.

Azimuth-first traversal and arrow/sphere guidance remain under [milestone 4 follow-ups](../PLAN.md#capture-follow-ups-from-milestone-2-review). Current CreatePoses still visits all azimuths at each altitude for full tilt capture. The median-only pose-gate proposal was declined; retain per-report pose tolerance followed by medians and stability checks. Burst capture is not implemented, and milestone 3 is not yet authorized.

The physical monitor placement, real pen pause/contact/tilt directions, DPI changes, and disconnect cleanup checks in [milestone 1](MILESTONE1.md#user-review-checks--pending) remain unverified. The prior Huion detection check establishes only read-only connectivity. Mathematical tests do not validate aiming, device signs/ranges, capture defaults, or hardware accuracy.

Numerically unusable geometry is rejected with a diagnostic. Library predicates and synthetic edge cases do not promise usable meshes for arbitrary extreme floating-point inputs. NTS's notice must accompany distribution; the earlier exact HidSharpCore notice check is also still pending for delivery.

**Paused at the milestone 2 review gate.** No filter installation, OTD settings edits, physical capture, commit, or publishing was performed for this milestone.

Review fixes verified: daemon-disconnect cleanup finishes without pumping the caller's synchronization context, and cleanup-failure instructions name both OTD and Calibrator. The new paused-context test and updated recovery-message assertion reproduced the regressions before the fixes. Session execution is dispatched inside RunAsync; the UI lifecycle remains unchanged.
