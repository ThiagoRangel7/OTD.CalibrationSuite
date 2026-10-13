# Milestone 1: acquisition and target mapping

Implemented in September 2026; the user reported the basic device checklist passing on **2026-09-13** with a Huion Kamvas 13 (Gen 3).

The app connects automatically, requires one tablet, reconstructs raw OTD reports, and places a borderless preview on the setup window's monitor. Targets use raw digitizer fractions of the WPF viewport, independently of cursor mapping. Pen absence preserves progress; disconnects, display changes, overflow, or blocked processing stop acquisition with bounded debug cleanup.

## Evidence

The initial Release build was warning-free and passed 25 synthetic acquisition/mapping tests. A read-only installed-daemon check found the user's tablet without settings access or debug enablement. The user reported connection, full-screen placement, contact responsiveness, pen waiting/resumption, repeated exit/restart, disconnect recovery, and a display-setting check working.

The user also reported these raw tilt observations while keeping the tip on the display:

| Eraser direction | Tilt X | Tilt Y |
| --- | ---: | ---: |
| Left | 50 | 0 |
| Right | -50 | 0 |
| Top | 0 | -50 |
| Bottom | 0 | 50 |
| Upright | 0 | 0 |

The pinned Huion configuration selects `GianoReportParser`; `GianoReport` negates signed bytes 10 and 11. These observations describe that setup, not universal physical-angle semantics. Current guidance uses reported coordinates directly; the earlier physical projection was removed.

Broader monitor/device configurations and measured correction accuracy were not established. Use [build commands](BUILD-WORKFLOW.md) and the [current device checklist](RELEASE.md#final-device-check), rather than the old preview-only workflow.
