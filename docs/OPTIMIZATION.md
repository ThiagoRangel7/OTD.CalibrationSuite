# Optimization and simplification

Runtime work is prepared outside report processing. Readability changes remove repeated work and state while retaining measured-data rules; shorter names or compressed formatting are not goals.

## Retained decisions

- `NodeMesh` caches triangle bounds, edge vectors, inverse areas, and boundary arithmetic. Bounds skip irrelevant triangles; robust predicates still decide inclusion. Correction allocates no managed memory in the existing checks.
- `CaptureBurst` reuses one sorting buffer for four medians and two percentiles. Every original report still contributes; smaller later bursts do not reuse stale values.
- `CaptureDraft` keeps a captured count and contiguous recapture range. Completed nodes are validated once; export groups already-checked samples without rebuilding meshes.
- Acceptance serializes a snapshot once, so repeated Copy does not rebuild JSON or meshes.
- WPF uses automatic card heights, viewport-driven sizing, and cached guidance hit-test geometry.
- Incremental filter packaging tracks assemblies, symbols, metadata, notices, and scripts. Unchanged builds reuse the ZIP.
- Tablet matching uses Newtonsoft's structural JSON comparison instead of maintaining a manual mirror of OTD fields. The expected tree is prepared once; dictionary order is ignored and list order retained. This costs acquisition allocations but reduces maintenance; drawing correction is unaffected.

## Recorded measurements

The synthetic runner warms operations and reports the median of five passes plus managed bytes per operation. The original baseline used commit `1da0d31`; the later snapshot includes the readable comparer and burst changes. Both used Release, SDK 10.0.401, and .NET 8 on this Windows host.

| Operation | Original baseline | Later snapshot | Allocation before → after |
| --- | ---: | ---: | ---: |
| Prepared tablet-reference comparison | 60.31 µs | 30.59 µs | 44,480 → 22,240 bytes |
| Captured-count read, 10,000 slots | 17.73 µs | Constant-time field read | 32 → 0 bytes |
| Correction inside tilt hull | 0.264 µs | 0.123 µs | 0 → 0 bytes |
| Correction outside tilt hull | 1.377 µs | 0.956 µs | 0 → 0 bytes |
| Capture burst, 64 reports | 29.60 µs | 20.43 µs | 14,912 → 80 bytes |

These are local historical microbenchmarks, not hardware accuracy or latency claims. An intermediate manual comparer was faster and allocation-free; the structural comparer trades that gain for less code. At 200 references/second, the recorded comparer costs about 6 ms and 4.4 MB per second, excluding RPC reconstruction.

```powershell
dotnet run --project scripts/PerformanceChecks -c Release
```

The runner uses synthetic data and opens no daemon connection.

## Readability pass

The 2026-10-12 cleanup removes duplicated capture activity state, recapture handoff fields, a forwarding text helper, repeated controller cleanup, duplicate pen-report fixtures, and manual hashing. WPF layout uses native collection initializers to show child hierarchy directly. All controls, sampling thresholds, correction equations, dependency pins, and measured JSON remain.

Docs now have one usage guide, one specification, focused maintenance/release instructions, and short milestone evidence notes. Repeated checklists, obsolete physical-angle instructions, and contradictory milestone status were removed. License texts are retained.

Production C# decreased from **20,279 to 19,921 syntax tokens**, excluding whitespace/comments. Documentation has about **71% fewer words** than the starting revision. Statements were expanded for readability; the reduction comes from removed state, duplication, and obsolete content.

Debug and Release builds pass with zero warnings/errors and **177 non-live tests each**. All **69 WPF renders are byte-identical** to the baseline and representative layouts were inspected. The verifier also exercises both review recapture buttons and confirms that accepted data survives. Local Markdown links/anchors resolve, and the specification's JSON example loads and produces the expected correction.

Windows PowerShell 5.1 cold source extraction/hash checks and release packaging pass. Archive contents/checksums and formatting/whitespace are checked. The performance runner still reports zero allocation for count reads and correction, 80 bytes per accepted burst, and 22,240 bytes per reference comparison; WPF update/layout measured 0.55 ms median / 0.66 ms p95 locally.

Device workflow review and publication remain pending; no new hardware evidence is claimed.
