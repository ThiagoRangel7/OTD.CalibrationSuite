# Dependencies and integration

## Pinned inputs

OTD **0.6.7** source commit and archive SHA-256 are in `dependencies/otd/source.json`. Five unmodified upstream project definitions, build properties, locks, and the license are committed so editors can load the solution before source download. Shared targets attach downloaded source/resources and fix the build date. Desktop remains source-built because the Windows OTD release ships single-file executables rather than library DLLs.

The filter uses the official **OpenTabletDriver.Plugin 0.6.7** NuGet package with runtime assets excluded. It ships only Calibration Suite assemblies and NetTopologySuite, using the host's OTD interfaces. The Calibrator uses source-built OTD Desktop report reconstruction and plugin loading; no UX/Eto assembly or replacement protocol is added.

Package locks record exact versions and hashes. Normal restores are locked and audit all dependencies.

| Dependency | Version | License / role |
| --- | --- | --- |
| OTD source assemblies | 0.6.7 | LGPL-3.0-or-later |
| NetTopologySuite | 2.6.0 | BSD-3-Clause; triangulation/predicates |
| Newtonsoft.Json | 13.0.4 | MIT; OTD serialization/reference matching |
| StreamJsonRpc | 2.22.23 | MIT; named-pipe RPC |
| MessagePack / Annotations | 2.5.301 | MIT; patched RPC dependency |
| JetBrains.Annotations | 2025.2.4 | MIT; OTD attributes |
| Microsoft.Extensions.DependencyInjection | 10.0.1 | MIT; OTD services |
| Octokit / SharpZipLib / System.CommandLine | 14.0.0 / 1.4.2 / 2.0.1 | MIT; OTD Desktop dependencies |
| WaylandNET | 0.2.0 | ISC; upstream cross-platform dependency |
| HidSharpCore | 1.3.0 | Apache-2.0; retained LICENSE/NOTICE |
| xunit / Visual Studio runner | 2.9.3 / 3.1.5 | Apache-2.0; tests |
| Microsoft.NET.Test.Sdk | 18.0.1 | MIT; tests |

Release packaging generates notices from restored package metadata and retains bundled LICENSE/NOTICE files. HidSharpCore's license was verified at its recorded commit `bca4aee1c349985b0119eed24aba83e2dd63492d`; WaylandNET's ISC license is also retained. GPL-3.0 text accompanies LGPL-3.0. The Calibrator ZIP includes runtime notices; the source ZIP includes build inputs and exact OTD source. See [release preparation](RELEASE.md).

## Geometry and presentation

NetTopologySuite receives sorted measured tilts with zero snapping tolerance. Preparation checks sample retention and usable triangle/boundary arithmetic; robust orientation predicates decide point inclusion. Single-sample nodes return constant error. Private prepared arrays support allocation-free correction with small triangle/edge scans and binary grid lookup.

The library chooses triangulation; a second in-circle audit is unnecessary. No jitter or sample merging is used. See the pinned [builder](https://github.com/NetTopologySuite/NetTopologySuite/blob/v2.6.0/src/NetTopologySuite/Triangulate/DelaunayTriangulationBuilder.cs) and [license](https://github.com/NetTopologySuite/NetTopologySuite/blob/v2.6.0/License.md). Synthetic degeneracy tests do not promise usable arithmetic for arbitrary extreme inputs.

Windows monitor APIs place the full-screen window. Raw target fractions map into WPF DIPs independently of OTD cursor mapping. Guidance plots reported tilt around each measured reference using native WPF vector drawing and cached target geometry. It introduces no rendering dependency or physical-angle conversion.

## Acquisition limits

- Queue: 512 events, one background consumer, monotonic ingress timestamps.
- Processing: two-second stale-work/stall limit, disarmed while awaiting a report.
- Live readings: expire after 500 ms.
- RPC and ordered enable-then-disable cleanup: three-second waits. Late enable completion still triggers disable.
- Consumer shutdown: allow 200 ms; if still blocked, require a Calibrator restart. Unconfirmed debug cleanup requires restarting both OTD and Calibrator.

OTD provides no debug ownership token or public cancellation/disposal of pending connection attempts. Reuse a pending connection rather than accumulating attempts. Closing the process releases it. Ordinary pen absence preserves progress; only a gap within a burst rejects that attempt.

## Updating

1. Choose one reviewed OTD release for both Plugin NuGet and source inputs. Update the immutable commit/checksum in `source.json`.
2. Replace upstream project definitions and `Upstream.props` verbatim. Review dependency changes, deliberately refresh locks with `RestoreLockedMode=false`, then return to locked restores.
3. Remove only generated `artifacts/otd-source` to force preparation. Never patch downloaded source. Git preserves upstream project-definition bytes for checksum comparison.
4. Re-run Debug/Release builds and tests, package/resource checks, editor loading, and compatible-daemon checks before claiming support.

Source extraction is serialized across build workers, validates paths/checksums, and writes its completion marker only after matching upstream definitions. It does not run during editor design-time evaluation.

## Recorded dependency audit — 2026-10-12

MessagePack 2.5.192 was replaced with **2.5.301** in the build overlay, preserving upstream source/projects. The [June 2026 advisory](https://github.com/advisories/GHSA-hv8m-jj95-wg3x) and [recursive-skip advisory](https://github.com/advisories/GHSA-vh6j-jc39-fggf) identify the patched branch. Locked restore, reconstruction, RPC, and packaging checks passed; the subsequent transitive NuGet audit reported no known vulnerabilities. This records that audit, not a guarantee about future advisories or the separately installed daemon.
