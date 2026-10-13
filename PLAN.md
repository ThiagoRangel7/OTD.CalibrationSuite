# Project status

[The specification](SPEC.md) defines behavior. This file records progress; [README](README.md) explains how to build and use the app.

## Working rules

Prefer fewer abstractions and less duplicate work while preserving behavior. Keep WPF and RPC outside the shared core. OTD owns settings, profiles, installation, and persistence; the Calibrator's only daemon mutation is temporary debug acquisition.

Check integration against the pinned OTD source and use sample plugins as examples, not requirements. Leave adjacent repositories unchanged. Run relevant tests, inspect the diff, and record real limitations. Synthetic data does not establish physical accuracy. Commit and publish only when authorized by the user.

## Milestones

| Milestone | State | Evidence |
| --- | --- | --- |
| 1. Build, acquisition, target mapping | Implemented; basic device checks reported passing | [Acquisition notes](docs/MILESTONE1.md) |
| 2. Calibration data and correction | Implemented; synthetic math/data checks pass | [Core notes](docs/MILESTONE2.md) |
| 3. OTD filter and package | Implemented; user reported live identity-data checks passing | [Filter notes](docs/MILESTONE3.md) |
| 4. Capture and review | Implemented; device workflow review pending | [Capture notes](docs/MILESTONE4.md) |
| 5. Visual guidance and usability | Implemented; device usability review pending | [UI notes](docs/MILESTONE5.md) |
| 6. Requirement audit and delivery | Packaging/lifecycle checks implemented; final workflow and publication pending | [Release checklist](docs/RELEASE.md) |

The current app supports absolute pen reports with or without tilt through OTD interfaces. Guidance plots reported coordinates around each target's measured reference. There is no tablet-model catalog or physical-angle gate. Position-only sessions ask the user to hold upright without claiming to verify posture.

## Simplification — 2026-10-12

Code cleanup removes redundant capture state, recapture handoff fields, duplicate test reports, and manual hashing. WPF layout uses native collection initializers. Sampling thresholds, measured JSON, interpolation, dependency pins, and all controls are retained.

Documentation now separates usage, specification, maintenance, and release checks. Milestone notes retain useful evidence and limitations without repeating commands or superseded designs. [Optimization notes](docs/OPTIMIZATION.md) retain the runtime decisions and benchmark baseline.

## Remaining work

- Complete the [device workflow checklist](docs/RELEASE.md#final-device-check): position-only and tilt capture, review/recapture, OTD Save/backup/restore, and enabled/bypassed correction.
- Review target reachability, contact responsiveness, and usability on actual hardware. Broader device and monitor configurations remain unverified.
- Review release candidates, then publish and submit registry metadata after explicit authorization.

Guided accuracy-testing sessions, held-out comparison reports, calibration libraries, and automatic profile/pen switching are deferred. Exact physical-angle certification is not a release gate.
