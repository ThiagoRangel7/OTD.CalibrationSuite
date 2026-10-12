# Dependency and integration decisions

## Source and API compatibility

OTD 0.6.7 source is pinned by commit and archive SHA-256 in dependencies/otd/source.json. Desktop, core, Plugin, Configurations, and Native assemblies build from automatically downloaded, unmodified source files. Their upstream project definitions are committed under dependencies/otd so the solution can load before downloading; Directory.Build.targets attaches source files and resources and fixes the build date. The filter instead references the official OpenTabletDriver.Plugin 0.6.7 NuGet package, excluding its runtime assets from the plugin package. No UX/Eto assembly is referenced. Initialization follows OTD's application-info/plugin-manager sequence; no settings, profile, output-mode, or installer APIs are called.

CalibrationSuite.Otd isolates raw RPC acquisition from the shared core and WPF. It reconstructs OTD reports using DebugReportData.ToObject and identifies the single connected tablet by reference contents. A synthetic RPC daemon lacking settings methods exercises the same path. The old profile snapshots, mapping preflight, output-mode construction, and settings polling were removed following the explicitly approved 2026-09-13 workflow revision.

The adjacent Tablet_Calibration and Tilt-Calibration samples were inspected for ideas; their mapping and fitted-coefficient implementations do not implement this spec's measured error field. No sample code is included.

## Correction geometry

Milestone 2 uses **NetTopologySuite 2.6.0**, pinned in the core project and all affected package locks. Its .NET Standard 2.1 asset is compatible with our .NET 8 projects and adds no transitive dependencies. Package metadata declares BSD-3-Clause and source commit `97d26b92586448b52d5a4f0c060217f94f882c75`; include its notice in delivery. The core uses built-in System.Text.Json and has no OTD/WPF reference.

`DelaunayTriangulationBuilder` receives lexicographically sorted measured coordinates with zero snapping tolerance. Mesh preparation checks that every distinct sample survives and that triangle arithmetic is usable. The library owns triangulation; there is no second in-circle audit of every triangle against every sample. `CGAlgorithmsDD.OrientationIndex` provides robust orientation for strict hull coverage and point location. No jitter, vertex merging, or custom triangulation engine is introduced. A numerically unusable mesh is rejected with a node diagnostic, rather than silently altering measurements.

See the pinned [builder source](https://github.com/NetTopologySuite/NetTopologySuite/blob/v2.6.0/src/NetTopologySuite/Triangulate/DelaunayTriangulationBuilder.cs), [predicate source](https://github.com/NetTopologySuite/NetTopologySuite/blob/v2.6.0/src/NetTopologySuite/Triangulate/QuadEdge/TrianglePredicate.cs), and [license](https://github.com/NetTopologySuite/NetTopologySuite/blob/v2.6.0/License.md). Triangle.NET was considered but not selected because its maintainer documents [unresolved licensing](https://github.com/wo80/Triangle.NET#license).

Single-sample nodes bypass triangulation and return their one measured error for any usable tilt; spatial blending remains unchanged. Prepared nodes retain only private value arrays and indices. Evaluating four small meshes uses bounded linear triangle/edge scans and binary grid lookup, without per-report allocations, construction, I/O, or locks. Synthetic tests cover exact and near degeneracies and sample permutations; they do not prove arbitrary extreme floating-point geometry is usable. No physical tilt-axis convention is inferred: guidance accepts already verified screen tangents, while interpolation always uses unchanged measured raw tilt.

## Display coordinates

Windows MonitorFromWindow chooses the monitor containing the largest part of the main window, or the nearest monitor if off-screen. GetMonitorInfo supplies full monitor bounds, including negative origins; SetWindowPos places the borderless preview. This replaces OTD display enumeration and the display selector.

Each target's raw X/MaxX and Y/MaxY fractions multiply the full preview's WPF ActualWidth/ActualHeight in DIPs. WPF per-monitor DPI awareness handles pixel scaling. No cursor position, applied OTD matrix, or desktop offset enters the measurement formula. Display/DPI changes end the old preview; Start selects fresh monitor bounds. The user must provide an aligned full-digitizer/full-display setup; identifying a physically attached screen from HID or monitor model names is not attempted.

## Dependencies and deployment

Versions and content hashes, including transitive dependencies, are captured in project lock files, including those under `dependencies/otd`. Relevant package metadata inspected locally:

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

Development deployment requires the complete Calibrator build output and the .NET 8 Windows Desktop runtime. The correction filter uses the host's OTD Plugin assembly at runtime. Milestone 3 packages only CalibrationSuite.Filter, CalibrationSuite.Core, and NetTopologySuite 2.6.0, with metadata and the complete pinned upstream NetTopologySuite license notice. No OTD, RPC, WPF, or HidSharpCore binaries are included in the filter ZIP. The package script uses an explicit file list and fixed ZIP timestamps. The build-workflow revision adds the official Plugin package; existing transitive dependency versions remain pinned. No release artifact has been published. Calibrator distribution/license checks and live install/update/removal verification remain delivery work.

## Visual guidance

Milestone 5 uses the existing WPF Windows Desktop runtime's vector drawing and geometry support. The 2026-10-05 review revision replaced the separate Viewport3D scene with thin guidance drawn at the physical crosshair. No rendering package or redistributable was added. Live updates redraw an arrow and reuse the target geometry between requested-pose/tolerance changes; the existing Huion/Giano adapter still owns raw-axis conversion and burst gates. The WPF-free top-view/tolerance and overlay placement helpers are in the shared core, while all WPF rendering remains in the Calibrator. `scripts/VisualChecks` references the Calibrator and pins the same existing dependency graph for offscreen layout/input verification.

## Acquisition limits

The queue holds 512 events and a background consumer awaits reports directly, without polling. Monotonic ingress timestamps detect stale queued work; a two-second cancellation timer runs during report processing to detect a blocked parser, independently of RPC timeouts. Cleanup runs independently, even when a report was buffered before the consumer started. Waiting for reports leaves the timer disarmed, so ordinary pen absence never cancels preview. Latest readings expire after 500 ms to avoid presenting stale position/tilt as current.

RPC waits and ordered enable→disable cleanup are bounded to three seconds. A late enable reply still triggers its disable. Unconfirmed cleanup or a worker that fails to stop blocks another preview until restart. This is necessary because OTD exposes no debug ownership token and third-party deserializers cannot safely be aborted in-process.

The main screen retries connection/single-tablet detection automatically while idle. Debug is enabled only after Start and stopped on exit. OTD's public RpcClient has no cancellation/disposal API for pending pipe connection attempts, so a timed-out attempt is reused; closing the process releases it. No GetSettings polling or Resynchronize handling is necessary for the independent mapping.


## Updating the dependency

Use one reviewed OTD release for both the Plugin NuGet version and source commit. Never float to latest. Update source.json with the immutable commit and verified ZIP checksum, replace the five upstream project definitions and Upstream.props verbatim, review their changes, and refresh package locks deliberately with RestoreLockedMode=false. Normal builds must use locked restoration. Delete only the generated artifacts/otd-source folder to force verification of the new definitions. Do not patch downloaded source.

Run Debug and Release builds, both test configurations, package loading, and live daemon checks before claiming support for a new release. A compatible version number alone does not establish parser/plugin compatibility. This build change does not replace OTD report reconstruction or introduce a custom protocol.

Git preserves the upstream project-definition bytes for checksum comparison. The verified source archive is cached, extraction is serialized across parallel build processes, and a completion marker is written only after project definitions match. No source download runs during editor design-time evaluation. Build once to populate source navigation.

The standard Windows OTD release contains single-file executables rather than standalone library DLLs; this is why Desktop remains source-built. Dependency preparation is build infrastructure, not an OTD requirement. The original neighboring checkout is no longer needed.
