# Milestone 3: OTD filter and package

Implemented in September 2026. The user reported live identity-data checks passing on **2026-10-03**.

[CalibrationFilter](../src/CalibrationSuite.Filter/CalibrationFilter.cs) runs at PreTransform. OTD injects geometry/settings, then `OnDependencyLoad` prepares the shared evaluator. The standard **Calibration data** string is stored by OTD. Only the original report's position changes; concrete type, other fields, emission count, and order remain.

Position-only data does not require tilt. Tilt-dependent data leaves missing/unusable tilt unchanged. Auxiliary, eraser, lost-proximity, and out-of-range reports pass through. Invalid data, geometry mismatch, or unrepresentable output uses identity. Repeated unusable pen reports trigger one bounded warning per activation.

## Evidence

The initial Release build was warning-free and passed 103 non-live tests. Package tests use OTD's actual Plugin Manager in temporary directories and check loading, property serialization, injected geometry, snapshot activation, report preservation, pipeline ordering, and allocation-free correction. Later delivery tests add update/removal and full settings backup/restore.

The user reported solution build/launch/tests, Release ZIP installation and restart, filter/property/tooltip visibility, empty-data identity behavior, enable/bypass, Apply/Save, persisted settings after restart, and no plugin errors in OTD's Console. This is user-reported evidence for that setup.

Real measured correction, broader configurations, and interactive filter breakpoints were not established by those checks. [README](../README.md#install-the-filter) gives installation instructions; [build notes](BUILD-WORKFLOW.md#filter-debugging) cover debugging; [release checks](RELEASE.md#final-device-check) list remaining device work.
