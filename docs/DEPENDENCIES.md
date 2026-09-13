# Dependency and integration decisions

## Source and API compatibility

The adjacent OTD 0.6.7 source tree targets .NET 8 and is pinned by scripts/otd-source.sha256. Desktop, core, Plugin, Configurations, and Native assemblies build from an unmodified workspace copy. No UX/Eto assembly is referenced. Initialization follows OTD's application-info/plugin-manager sequence; no settings, profile, output-mode, or installer APIs are called.

CalibrationSuite.Otd isolates raw RPC acquisition from the shared core and WPF. It reconstructs OTD reports using DebugReportData.ToObject and identifies the single connected tablet by reference contents. A synthetic RPC daemon lacking settings methods exercises the same path. The old profile snapshots, mapping preflight, output-mode construction, and settings polling were removed following the explicitly approved 2026-09-13 workflow revision.

The adjacent Tablet_Calibration and Tilt-Calibration samples were inspected for ideas; their mapping and fitted-coefficient implementations do not implement this spec's measured error field. No sample code is included.

## Correction geometry

Milestone 2 uses **NetTopologySuite 2.6.0**, pinned in the core project and all affected package locks. Its .NET Standard 2.1 asset is compatible with our .NET 8 projects and adds no transitive dependencies. Package metadata declares BSD-3-Clause and source commit `97d26b92586448b52d5a4f0c060217f94f882c75`; include its notice in delivery. The core uses built-in System.Text.Json and has no OTD/WPF reference.

`DelaunayTriangulationBuilder` receives lexicographically sorted measured coordinates with zero snapping tolerance. Mesh preparation checks that every distinct sample survives and that triangle arithmetic is usable. NTS builds using normalized double in-circle tests; we check each resulting triangle with its extended-precision `TrianglePredicate.IsInCircleDDNormalized` before accepting the mesh. `CGAlgorithmsDD.OrientationIndex` provides robust orientation for strict hull coverage and point location. No jitter, vertex merging, or custom triangulation engine is introduced. A numerically unusable mesh is rejected with a node diagnostic, rather than silently altering measurements.

See the pinned [builder source](https://github.com/NetTopologySuite/NetTopologySuite/blob/v2.6.0/src/NetTopologySuite/Triangulate/DelaunayTriangulationBuilder.cs), [predicate source](https://github.com/NetTopologySuite/NetTopologySuite/blob/v2.6.0/src/NetTopologySuite/Triangulate/QuadEdge/TrianglePredicate.cs), and [license](https://github.com/NetTopologySuite/NetTopologySuite/blob/v2.6.0/License.md). Triangle.NET was considered but not selected because its maintainer documents [unresolved licensing](https://github.com/wo80/Triangle.NET#license).

Single-sample nodes bypass triangulation and return their one measured error for any usable tilt; spatial blending remains unchanged. Prepared nodes retain only private value arrays and indices. Evaluating four small meshes uses bounded linear triangle/edge scans and binary grid lookup, without per-report allocations, construction, I/O, or locks. Synthetic tests cover exact and near degeneracies and sample permutations; they do not prove arbitrary extreme floating-point geometry is usable. No physical tilt-axis convention is inferred: guidance accepts already verified screen tangents, while interpolation always uses unchanged measured raw tilt.

## Display coordinates

Windows MonitorFromWindow chooses the monitor containing the largest part of the main window, or the nearest monitor if off-screen. GetMonitorInfo supplies full monitor bounds, including negative origins; SetWindowPos places the borderless preview. This replaces OTD display enumeration and the display selector.

Each target's raw X/MaxX and Y/MaxY fractions multiply the full preview's WPF ActualWidth/ActualHeight in DIPs. WPF per-monitor DPI awareness handles pixel scaling. No cursor position, applied OTD matrix, or desktop offset enters the measurement formula. Display/DPI changes end the old preview; Start selects fresh monitor bounds. The user must provide an aligned full-digitizer/full-display setup; identifying a physically attached screen from HID or monitor model names is not attempted.

## Dependencies and deployment

Versions and content hashes, including transitive dependencies, are captured in project lock files and `scripts/otd-locks`. Relevant package metadata inspected locally:

| Dependency | Version | Declared license / role |
| --- | --- | --- |
| OpenTabletDriver source assemblies | 0.6.7 | LGPL-3.0-or-later, per source build metadata and LICENSE |
| NetTopologySuite | 2.6.0 | BSD-3-Clause; core triangulation and robust predicates |
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
