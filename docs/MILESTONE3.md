# Milestone 3 — filter and package review

Implemented on 2026-09-13 following the user's request to implement the next milestone. The user reported the build and live identity-data review checks passing on 2026-10-03. Milestone 4 was subsequently implemented and checkpointed; see [capture review](MILESTONE4.md) and the implemented [milestone 5 visual work](MILESTONE5.md).

## Implementation

`CalibrationFilter` is a public parameterless OTD positioned element named **Calibration Suite**, at PreTransform. Its standard **Calibration data** string has an empty default and paste/Save/clear tooltip. Tablet injection and OnDependencyLoad prepare the shared immutable evaluator; the setter only stores text. New OTD settings activation creates a new instance. Missing geometry, invalid data, and preparation exceptions leave identity with an OTD diagnostic.

Eligible reports carry position and tilt together. Only Position is changed through the existing interface reference, including boxed structs. Eraser, lost proximity, OutOfRange, auxiliary, missing-pair, and nonfinite reports pass unchanged. Finite double results that overflow OTD's float position also pass unchanged. The original report is emitted synchronously once, preserving all other fields and event order. No cached tilt, pressure gate, output clipping, or report reconstruction is introduced.

The report path performs interface checks, finite checks, and the shared evaluator call. No parsing, triangulation, I/O, locks, or routine logging occurs there. The first 100 unusable pen reports across an activation trigger one warning; the counter then saturates. Ordinary auxiliary/eraser/absence reports do not count. Unsupported reports retain identity behavior independently of subsequent eligible reports.

`scripts/Package-Filter.ps1` writes `artifacts/filter-package/Release/CalibrationSuite.zip` with DLLs at the ZIP root, matching OTD's installer. It includes the two Calibration Suite assemblies, NetTopologySuite 2.6.0, metadata, and notices. It excludes host-owned assemblies. This is a development artifact with local version 0.1.0, no download URL or publication. NetTopologySuite's complete license was obtained from the pinned v2.6.0 source; the file is retained in `packaging` for offline packaging.

## Source contract review

Reviewed OTD 0.6.7 PluginSettingStore (settings → TabletReference → OnDependencyLoad), OutputMode/PipelineManager (ordered PreTransform elements), report interfaces and TiltTabletReport (mutable boxed struct), Log, DesktopPluginManager/DesktopPluginContext (ZIP extraction/loading), PluginMetadata, and PluginManagerWindow (install and generated metadata workflow). The adjacent Tilt-Calibration example's project was inspected; no sample code or dependencies were copied. Adjacent repositories were not modified.

## Verification

Locked restore passed (a forced refresh cleared cached sandbox network audit warnings), Release solution build passed with zero warnings/errors, and all **103** non-live tests passed. Repeated packaging produced SHA-256 `EB39748FC6DF7A2EFEEC80A6021B2BC8F7974CD898E6D4A8F0B49ED53B5D93D2`. No dependency locks changed.

Run the README build/package/test commands. The suite includes real OTD package installation into a unique temporary application-data directory, plugin discovery, reflection construction, geometry injection, measured activation, property serialization round-trip, and clear-to-identity. It does not connect to or change the installed daemon.

Tests cover original boxed/class reports and fields, synchronous event order, pass-through cases, initialization failures, bounded unsupported diagnostics, the actual OTD PreTransform-to-transform chain, float overflow fallback, and zero allocated bytes across 1,000 warmed eligible correction calls. Existing core tests retain independent interpolation expectations. All measurements used are fictional.

## Live review checklist

1. Install the ZIP via OTD Plugin Manager and verify the filter name, property label, and tooltip. Use empty identity data initially.
2. Keep exactly one enabled entry in the intended profile; ensure no position/tilt-changing filter precedes it. Verify enable/bypass, clear, Apply, and Save using OTD's controls.
3. Check normal pen/eraser/proximity behavior and OTD logs. Confirm saved identity behavior after restarting OTD; review update/removal and settings/preset round-trips during delivery checks.
4. After milestone 4 capture is available, verify measured correction on hardware. Do not install fictional test measurements. Prior angle-scale, diagonal, monitor, and physical-accuracy limits remain open.

The original implementation turn performed automated checks only. The subsequent user verification below records the live checks actually reported.

## User verification — 2026-10-03

The user reported "all these tests passed" after following the conversational build and plugin review instructions:

- Open/build the solution and launch the Calibrator preview; run the non-live automated tests.
- Build Release and install artifacts/filter-package/Release/CalibrationSuite.zip through OTD Plugin Manager, then restart OTD.
- Verify the Calibration Suite filter, Calibration data field, and tooltip.
- Leave calibration data empty, enable the filter, and Save; normal pen behavior remains unchanged.
- Toggle enable/bypass and exercise Apply/Save without unexpected pointer changes.
- Restart OTD and confirm settings persistence and normal pen behavior.
- Inspect OTD's Console for plugin-loading warnings/errors; none were reported.

This is user-reported verification on the user's setup, not a new assistant-run test result. The previously documented automated baseline is 104 non-live tests after the build-workflow update; no new test output or exact count was supplied with this report.

Live identity-data integration checks are complete for that setup. Real measured correction and accuracy still require milestone 4 capture. Interactive filter breakpoints, update/removal, settings/preset backup round-trips, and broader device/configuration coverage were not part of the conversational checklist and remain for their planned checks. This record does not begin milestone 4.
