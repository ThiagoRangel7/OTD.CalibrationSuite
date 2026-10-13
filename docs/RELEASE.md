# Release preparation

The project is unreleased. Local candidates are ready for final device workflow review; publication still requires explicit authorization.

## Scope

Windows x64 Calibrator, OTD 0.6.7, and one display tablet with aligned full-digitizer/full-screen mapping. Absolute pen position is required; paired tilt is optional. Guidance uses reported coordinates, and position-only capture asks the user to hold upright without verifying posture. [README](../README.md) covers setup and use.

Common OTD interfaces do not establish broad hardware accuracy. Check reachable targets, stable sampling, usable coverage, and actual correction on each supported setup. Physical-angle certification is not a release gate.

## Prepare archives

```powershell
dotnet restore CalibrationSuite.slnx --locked-mode
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Package-Release.ps1
```

`packaging/metadata.json` sets the version and output directory, currently `artifacts/releases/0.1.0.0`.

| Output | Contents |
| --- | --- |
| `CalibrationSuite.zip` | Filter/Core/NTS DLLs, metadata, and licenses at ZIP root |
| `CalibrationSuite.Calibrator.Windows.zip` | Complete Windows x64 app and notices; requires .NET 8 Windows Desktop runtime |
| `CalibrationSuite.Source.zip` | Working-tree source/build inputs and exact upstream source ZIP |
| `CalibrationSuite.json` | Registry metadata with versioned download URL and actual filter ZIP hash |
| `SHA256SUMS.txt` | Checksums of all three archives |

Packaging does not install, publish, create a tag, or change OTD settings. The generated download URL works only after uploading the exact ZIP to the matching versioned release. Registry metadata stays outside the ZIP to avoid a self-referential hash. Package identity and stable ZIP filename are `CalibrationSuite`; the displayed filter name is **Calibration Suite**.

For offline source preparation, copy `third-party/OpenTabletDriver-source.zip` from the source archive to `artifacts/otd-<commit>.zip`, using the commit in `dependencies/otd/source.json`. SDK and NuGet dependencies are still required. OTD DLLs remain separate so compatible modified builds can replace them. Retain MIT, LGPL/GPL, and all dependency notices/source inputs.

## Automated checks

Require clean locked restores, warning-free Debug/Release builds, and passing synthetic tests. Tests cover capture/copy/correction with and without tilt, nonzero references, invalid-data identity fallback, report preservation/order, and actual OTD Plugin Manager install/update/removal in temporary data directories. Settings serialization covers destination-profile isolation, Save/backup/restore, bypass retention, and clear-to-identity.

Run the [visual checker](../README.md#development-checks) for normal/small/high-DPI layouts, all directions/cues, tolerance-circle gates, reused views, and keyboard/mouse/touch handling. Inspect the renders. Confirm archive contents and checksums. These checks do not mutate the installed daemon or establish physical accuracy.

## Final device check

1. Install the candidate filter through Plugin Manager and restart OTD. Extract the complete Calibrator ZIP into a fresh directory, launch it, and verify connection/full-screen placement.
2. Capture a 2 × 2 position-only dataset. Exercise retry, Backspace, restart, Esc/resume, review, pose/node recapture, discard, accept, and copy. Confirm earlier accepted data survives unfinished or discarded work.
3. Paste into the intended profile and Save. Compare enabled/bypassed correction at captured and intermediate positions. Restart OTD, confirm persistence, then test settings/preset backup, clear, and restore. Check the Calibrator close reminder before and after copying.
4. On a tilt-capable tablet, repeat with reported-coordinate targets, including a comfortable nonzero reference. Check reachable targets, contact responsiveness, and correction between sampled tilts and near coverage boundaries. Verify pen/mouse/touch cannot activate capture controls.
5. Check disconnect and display/DPI-change recovery. Review broader monitor configurations where available; changed digitizer geometry requires new capture.

Record observed behavior and limitations; no invented accuracy threshold is required. Calibrator updates use a fresh extraction directory; removal deletes that directory. Filter update/removal stays in OTD Plugin Manager, and saved data stays in OTD.

After device review and publication authorization, commit the reviewed source, create the versioned release with all archives/checksums, and submit generated metadata to `OpenTabletDriver/Plugin-Repository`. A public repository alone is not a binary release or registry listing.

## Verification record

Before this simplification, the 2026-10-12 Debug/Release builds passed without warnings/errors and each passed 177 non-live tests. WPF/input checks, locked restore, and the transitive dependency audit also passed. This pass retains those behavioral checks; current results are recorded in [optimization notes](OPTIMIZATION.md#readability-pass).

Final device review and publication remain pending.
