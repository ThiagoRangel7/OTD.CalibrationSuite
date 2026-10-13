# Milestone 2: data and correction core

Implemented in September 2026. Later milestones add capture and the runtime filter; [SPEC](../SPEC.md) describes the current contract.

## Core design

- [CalibrationData](../src/CalibrationSuite.Core/CalibrationData.cs) serializes measured/identity data.
- [CaptureSettings](../src/CalibrationSuite.Core/CaptureSettings.cs) validates settings and resolves targets/poses.
- [NodeMesh](../src/CalibrationSuite.Core/NodeMesh.cs) interpolates measured tilt errors and projects out-of-coverage queries to the nearest boundary.
- [PreparedCalibration](../src/CalibrationSuite.Core/PreparedCalibration.cs) validates data, copies evaluator inputs, and blends four node errors across the grid.

NetTopologySuite owns triangulation; preparation checks retained samples and usable arithmetic. One-sample nodes provide constant spatial error. Current multi-sample meshes need distinct measured tilts, not raw-zero coverage or a physical-upright constraint. Requested poses are descriptive metadata.

## Evidence and examples

The initial warning-free Release build passed 93 synthetic cases. Current tests retain malformed-data/geometry rejection, snapshot ownership, vertex reproduction, continuity, hull projection, nonuniform grids, sample-order determinism, and spatial residual checks.

- Independent triangle example: at tilt `(10, 20)`, weights `1/4, 1/4, 1/2` produce error `(10, 7)`.
- Hull example: `(60, 10)` projects to `(40, 10)`; `(60, 60)` projects to `(40, 40)`.
- Spatial example: error `(0.4*x, 0.2*y)` corrects raw `(500, 500)` to `(300, 400)`. A measured `(840, 360)` at target `(600, 300)` corrects to `(504, 288)`, leaving residual `(-96, -12)`.

The last example shows why this field is not an exact inverse. Fixtures are fictional and must not be installed on hardware. Extreme floating-point geometry can still be unusable; rejected loads use identity with a diagnostic. [Dependency notes](DEPENDENCIES.md#geometry-and-presentation) explain the library choice, and [release checks](RELEASE.md) cover device verification.
