# Milestone 1 dependency and integration decisions

## Source and API compatibility

The adjacent OTD 0.6.7 source tree targets .NET 8 and is pinned by scripts/otd-source.sha256. Desktop, core, Plugin, Configurations, and Native assemblies build from an unmodified workspace copy. No UX/Eto assembly is referenced. Initialization follows OTD's application-info/plugin-manager sequence; no settings, profile, output-mode, or installer APIs are called.

CalibrationSuite.Otd isolates raw RPC acquisition from the shared core and WPF. It reconstructs OTD reports using DebugReportData.ToObject and identifies the single connected tablet by reference contents. A synthetic RPC daemon lacking settings methods exercises the same path. The old profile snapshots, mapping preflight, output-mode construction, and settings polling were removed following the explicitly approved 2026-09-13 workflow revision.

The adjacent Tablet_Calibration sample was inspected for ideas; its old framework and implementation were not copied. No sample code is included. Deterministic triangulation-library selection remains in milestone 2.

## Display coordinates

Windows MonitorFromWindow chooses the monitor containing the largest part of the main window, or the nearest monitor if off-screen. GetMonitorInfo supplies full monitor bounds, including negative origins; SetWindowPos places the borderless preview. This replaces OTD display enumeration and the display selector.

Each target's raw X/MaxX and Y/MaxY fractions multiply the full preview's WPF ActualWidth/ActualHeight in DIPs. WPF per-monitor DPI awareness handles pixel scaling. No cursor position, applied OTD matrix, or desktop offset enters the measurement formula. Display/DPI changes end the old preview; Start selects fresh monitor bounds. The user must provide an aligned full-digitizer/full-display setup; identifying a physically attached screen from HID or monitor model names is not attempted.

## Dependencies and deployment

Versions and content hashes, including transitive dependencies, are captured in project lock files and `scripts/otd-locks`. Relevant package metadata inspected locally:

| Dependency | Version | Declared license / role |
| --- | --- | --- |
| OpenTabletDriver source assemblies | 0.6.7 | LGPL-3.0-or-later, per source build metadata and LICENSE |
| Newtonsoft.Json | 13.0.4 | MIT; OTD serialization and debug DTOs |
| StreamJsonRpc | 2.22.23 | MIT; OTD named-pipe RPC |
| JetBrains.Annotations | 2025.2.4 | MIT; OTD attributes |
| Microsoft.Extensions.DependencyInjection | 10.0.1 | MIT; OTD service manager |
| Octokit | 14.0.0 | MIT; OTD Desktop dependency |
| SharpZipLib | 1.4.2 | MIT; OTD Desktop dependency |
| System.CommandLine | 2.0.1 | MIT; OTD Desktop dependency |
| WaylandNET | 0.2.0 | ISC; upstream cross-platform dependency, unused by Windows preview |
| HidSharpCore | 1.3.0 | NuGet metadata omits a license; retain upstream attribution and resolve exact package notices before redistribution |
| xunit / VS runner | 2.9.3 / 3.1.5 | Apache-2.0; tests only |
| Microsoft.NET.Test.Sdk | 18.0.1 | MIT; tests only |

Other restored Microsoft, MessagePack, and Nerdbank runtime packages declare MIT. `xunit.abstractions` 2.0.3 uses a legacy upstream license URL in its metadata. HidSharpCore source files carry an [Apache-2.0 header](https://github.com/OpenTabletDriver/HIDSharpCore/blob/master/HidSharp/ImplementationDetail.cs), but the 1.3.0 package lacks license metadata and the exact recorded commit's license text was not retrieved. Treat package notice verification as an outstanding distribution check, not as permission inferred from a current source file.

Development deployment requires the complete Calibrator build output and the .NET 8 Windows Desktop runtime. The pass-through filter references the host's OTD Plugin assembly with `Private=false`; do not install its development output as a finished plugin. Packaging, dependency/notice distribution, replacement of host-owned assemblies, and install/update/remove checks belong to milestones 3 and 5. No release artifact has been published.

## Acquisition limits

The queue holds 512 events and the serialized consumer runs independently of WPF. Monotonic ingress timestamps detect stale queued work; the cancellation timer detects blocked processing. Ordinary pen absence never cancels preview. Latest readings expire after 500 ms to avoid presenting stale position/tilt as current.

RPC waits and ordered enable→disable cleanup are bounded to three seconds. A late enable reply still triggers its disable. Unconfirmed cleanup or a worker that fails to stop blocks another preview until restart. This is necessary because OTD exposes no debug ownership token and third-party deserializers cannot safely be aborted in-process.

The main screen retries connection/single-tablet detection automatically while idle. Debug is enabled only after Start and stopped on exit. OTD's public RpcClient has no cancellation/disposal API for pending pipe connection attempts, so a timed-out attempt is reused; closing the process releases it. No GetSettings polling or Resynchronize handling is necessary for the independent mapping.
