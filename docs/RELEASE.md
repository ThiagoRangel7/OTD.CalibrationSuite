# Current scope and release preparation

This is the current delivery checklist. Earlier milestone notes describe historical physical-angle guidance and its restrictions, superseded by the 2026-10-12 reported-coordinate workflow.

## Supported scope

- Windows x64 Calibrator and OpenTabletDriver 0.6.7.
- One display tablet, with full active digitizer mapped to the full display, X right and Y down.
- OTD absolute pen-position reports, with optional paired tilt. No tablet-model/parser whitelist.
- When the first eligible pen report lacks ITiltReport, tilt poses are blocked for the session and the screen instructs upright position-only capture. Users can choose this mode themselves by setting directions or levels to zero if reported tilt is unusable.
- Rotated/cropped/misaligned mappings and non-display tablets are outside scope. OTD does not expose a reliable display-tablet classification; the user is responsible for the physical setup.

Tilt support derives from OTD report interfaces; its tablet metadata has no reliable capability flag. There are no model-specific settings or catalogs. Zero values alone do not imply missing support. Position-only users hold upright at every crosshair; without tilt measurements, that posture cannot be verified.

Tilt targets use the reported coordinates, not physical angles. The first stable pose at each grid point sets its reference; later targets spread around it. Exact perpendicularity, angular scale, and raw-zero inclusion are not release gates. Distinct measurements, usable triangulation, stable sampling, and actual spatial/tilt coverage still matter. Reduce Tilt range if targets are unreachable. No-tilt data provides spatial correction; it cannot compensate for orientation-dependent error that the tablet does not report.

## Prepare the files

From the repository root, with the pinned SDK and restored locked dependencies:

```powershell
dotnet restore CalibrationSuite.slnx --locked-mode
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Package-Release.ps1
```

The version in packaging/metadata.json determines artifacts/releases/0.1.0.0:

| File | Purpose |
| --- | --- |
| CalibrationSuite.zip | OTD Plugin Manager package; DLLs and MIT/NTS licenses at ZIP root |
| CalibrationSuite.Calibrator.Windows.zip | Windows x64 app and its complete runtime dependency/notices folder; requires .NET 8 Windows Desktop Runtime |
| CalibrationSuite.Source.zip | Actual working-tree source/build inputs, plus the exact upstream OTD source archive |
| CalibrationSuite.json | Prepared OTD registry metadata with versioned download URL and actual plugin ZIP SHA-256 |
| SHA256SUMS.txt | Checksums of all three archives |

The script does not install, publish, create a tag/release, or modify OTD settings. The prepared download URL becomes valid only after the exact ZIP is uploaded under the matching v0.1.0.0 GitHub release. Registry metadata belongs outside the ZIP to avoid a self-referential checksum. The package identity is CalibrationSuite, matching the ZIP filename so OTD's manual and registry installers use the same folder; the filter's human-readable name remains Calibration Suite.

Source recipients can build using the pinned inputs. For offline OTD source preparation, copy third-party/OpenTabletDriver-source.zip from the source archive to artifacts/otd-736003ed72c8bbb28033b039d5a0bb76c344145c.zip before building. NuGet dependencies and the SDK remain prerequisites. OTD DLLs are separate, allowing compatible modified builds to replace them. Calibration Suite is MIT; OTD-derived inputs/binaries retain LGPL, including its incorporated GPL text. Package notices retain dependency copyright, declared license, exact repository revision where supplied, and bundled NOTICE/LICENSE files.

## Verification

Automated checks cover capture/copy/runtime correction with and without tilt, offset reference/coverage that excludes raw zero, no stale-tilt reuse, report-field preservation, invalid-data identity fallback, and pipeline ordering. OTD's actual Plugin Manager is exercised for installation, replacement of obsolete files during update, and removal. Full Settings serialization covers measured calibration data, destination-profile isolation, retained data when bypassed, backup/restore, and clear-to-identity. Tests use temporary app-data directories and synthetic reports; they do not alter the installed daemon.

WPF checks cover the reported-coordinate circle against the sampling gate, reference/position-only states, all directions, normal/small/high-DPI layouts, reused views, keyboard capture and mouse/touch suppression. Dependency restore and Debug/Release builds must remain warning-free. Release candidates must contain their documented files and matching hashes.

## Final user workflow check

1. Install the candidate filter ZIP through OTD Plugin Manager and restart OTD. Extract the complete Calibrator ZIP into a fresh directory and launch the executable. Verify connection and the documented mapping.
2. Capture a small 2 × 2 position-only dataset, accept/copy it, paste into the intended profile, and Save. Compare enabled/bypassed pointer behavior at captured and intermediate positions. Reopen/restart OTD and verify the data remains present. Save a settings backup/preset, clear the data, then restore it.
3. On a tilt-capable tablet, repeat a small tilt capture using reported-coordinate targets, including a nonzero reference if that is the comfortable pose. Check that targets can be reached, bursts remain responsive during contact, and correction/bypass behaves consistently between sampled tilts and near coverage boundaries. A physical angle reference or accuracy threshold is not required.

These are functional checks of the new workflow, not physical-angle certification. Broad hardware accuracy has not been established merely by using common interfaces. Publish support claims according to actual report compatibility and observed device behavior.

After the user workflow check, commit the reviewed changes, create the versioned release with all three ZIPs and checksums, and submit its generated metadata to OpenTabletDriver/Plugin-Repository under the documented driver/owner/repository path. Publishing requires explicit user authorization. A public source repository alone is not an OTD Plugin Manager listing or binary release.

## Recorded verification — 2026-10-12

Debug and Release solution builds passed with zero warnings/errors. Both configurations passed 177 non-live tests, including actual OTD install/update/removal, Settings backup/restore contracts, and blocked tilt poses after detecting position-only reports. WPF circle/gate, reference/position-only instructions, layout, crosshair feedback, and input checks passed; renders were visually inspected. The optional guidance metadata in earlier measured data remains loadable. Locked dependency restore passed; the transitive vulnerability audit reports no known vulnerable packages after the MessagePack 2.5.301 patch.

Production C# decreased from 2,227 to 2,173 lines and from 21,015 to 20,279 syntax tokens; function/constructor declarations decreased from 159 to 149. This reduction removes the physical-angle projection, annular-sector model/rendering, and origin/upright constraints rather than compressing formatting. Runtime correction remains allocation-free in the existing performance check. Tests for superseded physical-angle behavior were replaced with reported-coordinate, nonzero-reference, no-tilt, and delivery checks.

The archives are local release candidates. The final on-device workflow check and explicit publication remain pending; no new measured hardware accuracy is claimed.
