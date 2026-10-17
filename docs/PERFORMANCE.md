# Local UI and rules performance

## Batched metadata enumeration — 2026-10-17

The command-reader baseline averaged 135.25 ms and 1.666 MiB allocations over ten local samples.
An experimental forward-only, 64-item WMI enumeration averaged 120.63 ms; the production reader
after applying those options averaged 123.32 ms and 1.632 MiB. This is a modest improvement, subject
to live process/provider variation. No command cache, reduced cadence or reduced metadata coverage
was introduced. Service enumeration uses the same options, without claiming an independently
measured service-specific gain. Each item and collection is still disposed.

Microsoft documents [block operations](https://learn.microsoft.com/en-us/dotnet/api/system.management.enumerationoptions.blocksize)
and [forward-only enumeration](https://learn.microsoft.com/en-us/dotnet/api/system.management.enumerationoptions.rewindable).
Reports stay under ignored `artifacts/sampling-performance/{second-baseline,experiment-batched,second-after}-metadata.json`.

The user's separate administrator console run recorded 60 samples per mode: inventory capture
averaged 268 ms including initialization, with the last ten averaging 198 ms. Inventory peaked at
123 MiB working set/87 MiB private bytes and retained 0.36 MiB managed data after cleanup/GC.
Monitoring Stop took 174.78 ms, followed by 1.19 ms disposal. Those files do not record continuous
ETW health or full elevated UI state, so they neither reproduce nor explain the reported 500 MB.

## Native chart and projection reuse — 2026-10-17

A new 200-update native chart fixture measured the previous 60-point trace at 1.25 ms mean and
60,962 bytes allocated per update. Reusing PathGeometry, PathFigure and PolyLineSegment point
collections reduced that to 0.068 ms and 3,073 bytes, with unchanged size/time-mode grid geometry
retained. This is about 95% lower per-chart update cost in the synthetic run, not a frame-rate guarantee.
Every chart accepts at most 60 points. Gaps detach runs, restored observations reconnect them,
and empty history clears figures. Reuse is local to each native chart, with no saved history.

Stable 1,000-row projection initially allocated 2,469,576 bytes in the regression fixture. Tree
traversal rebuilt already-constructed rows and aggregation scanned members four times. A single
aggregation pass, row reuse, skipped empty child sorts and per-build display labels reduced this to
1,295,944 bytes. The broader UI harness measured table refresh allocations falling from about
5.88 MB to 4.09 MB and explicit-sort allocations from 8.33 MB to 6.06 MB per operation. Refresh
and sort p95 timings still exceeded a frame budget in this run; allocation reductions do not establish
that every interaction is now fluid. No stale cross-build name cache or lifetime-key changes are used.

Reports: `artifacts/ui-performance/baseline-chart-performance.png.chart.json`,
`reused-chart.png.chart.json`, and `second-projection-chart-*.performance.json`. Native gap/log/resize
checks and the full UI harness pass. AI and authentication are excluded from profiling.

## Sampling, memory and shutdown follow-up — 2026-10-17

This pass excludes AI transport/authentication. Measurements cover live system/GPU counters, WMI
command metadata, full inventory capture, native tables, dashboard/page navigation and synthetic
SQLite rules. Reports remain ignored under `artifacts/sampling-performance` and `artifacts/ui-performance`.
Only aggregate live counts/timing/memory leave the collector; no real inventory is exported.

| Workload | Before | After |
| --- | ---: | ---: |
| Steady full inventory allocations | about 27–28 MiB/sample | about 5 MiB/sample |
| 1,000-row CPU-change allocations | 5.17 MB/update | 1.67 MB/update |
| 1,000-row table refresh allocations | 9.38 MB/update | 5.88 MB/update |
| 1,000-entity SQLite read + local evaluation | 1,020 ms / 984 MiB | 108 ms / 34 MiB |

Image lookup previously allocated a 32,768-character StringBuilder per process. It now rents 512
characters, retries with bounded growth only on insufficient buffer, and clears arrays before return.
Rows reuse unchanged resource labels; aggregation avoids intermediate arrays. Candidate batching
deserializes shared rule/evidence records once per fresh SQLite read transaction, with separate
per-entry lists and generation-guarded invalidation. There is no long-lived stale catalog cache.

System sampling alone averaged about 10 ms after initialization; its console process ended around
19 MiB private bytes with 0.3 MiB retained managed data. GPU counter reads were about 1–2 ms.
Command metadata alone was about 119–135 ms with 1.6–1.7 MiB allocations. Cold full collection
had enrichment spikes above two seconds; pooling reduces allocations, not those native trust calls.
Do not confuse allocation totals over many iterations with simultaneously retained RAM.

The native live-collection run used approximately 508 processes/305 services over 25 samples,
without saved accounts/provider configuration. Working set peaked at 322 MiB, private bytes at
253 MiB, and managed data at 65 MiB before collection. With live rows retained after diagnostic GC,
managed data was 11.4 MiB, private bytes 248 MiB and working set 318 MiB. Forced GC is diagnostic
only; production does not force collection or trim working sets. This did not reproduce 500 MB and
does not establish a memory ceiling for the user's longer-running/elevated session.

Three 60-switch dashboard navigation passes retained about 2.4–2.6 MiB managed data. Twenty
round trips between process/dashboard pages left zero surviving retired root views in weak-reference
checks after GC. Native memory/cache retention still exceeded the managed heap. UI timing tails
remain: explicit sorts/search/details can exceed a frame budget. The first UI comparison shared some
desktop load with memory profiling, so timing differences are exploratory; allocation changes are
the stronger evidence. No sustained frame-rate or broad memory-leak absence claim is made.

Shutdown formerly ran synchronous ETW stop in the Closed callback, after native destruction began.
Native close, tray Exit and elevation handoff now cancel/hide immediately, run one cleanup operation
off-thread, retain the dispatcher until completion and then close. ETW stop retains its original source
and releases the state lock before native calls. The WM_CLOSE fixture returned in 8.18 ms while
cleanup was deliberately blocked and verified the dispatcher remained responsive and cleanup ran
once. Non-elevated live collector disposal took 1.05 ms; active administrator ETW stop latency has
not been measured in this session.

Repeat sampling with `./scripts/measure-sampling.ps1 -Samples 15 -Label comparison`. Native fixture
modes and privacy boundaries are documented in [DEVELOPERS.md](../DEVELOPERS.md). Keep privilege,
process count, viewport, build optimization and desktop load comparable between runs.

Measured on 2026-10-17 using synthetic processes, metadata and rules. No provider requests,
credentials, real inventory dumps or machine-specific rules were used. The accepted native column
interaction remains the visual requirement; these changes optimize its implementation.

## Repeat the measurements

Resource shading/sort-policy follow-up on 2026-10-17: the optimized synthetic harness completed
with 1,000-row refresh mean 7.36 ms/p95 18.00 ms, drag position mean 0.05 ms/p95 0.09 ms, and
crossing preview mean 0.22 ms/p95 0.33 ms. Explicit sort/search means were 34.66/28.88 ms; these
remain candidates for further optimization. This single run includes current presentation work,
not a controlled causal comparison or sustained live-load frame-rate claim. The ignored report is
`artifacts/ui-performance/resource-shading-20261017-125111-656.png.performance.json`.

```powershell
./scripts/measure-ui.ps1 -Label comparison
./scripts/measure-ui.ps1 -Label narrow-light -Theme Light -Width 700 -NoBuild
dotnet restore benchmarks/Rules --locked-mode
$priorTiering=$env:DOTNET_TieredCompilation
$env:DOTNET_TieredCompilation='0'
try { dotnet run --project benchmarks/Rules -c Release --no-restore }
finally {
    if ($null -eq $priorTiering) { Remove-Item Env:DOTNET_TieredCompilation }
    else { $env:DOTNET_TieredCompilation=$priorTiering }
}
```

Run comparisons without concurrent builds, profilers or benchmarks. The UI script uses optimized Debug compilation so native
fixtures are available without replacing Windows App SDK bootstrap constants. The app runs without
a debugger, using its real XAML controls. It writes numerical JSON and synthetic captures to ignored
artifacts/ui-performance. It never launches collection, loads real settings or contacts AI services.

UI samples time an operation plus explicit `WindowRoot.UpdateLayout()` unless labeled projection or
row-values. They exclude the 16ms pacing wait. These are UI-thread/layout costs, **not GPU frame rate,
end-to-end mouse latency or a guarantee of smoothness**. Twelve samples are used for refresh stages,
eight for sort/search/group changes, 40 for crossings/resizes and 100 for pointer-position updates.
JIT, garbage collection and desktop load affect tails. Repeat runs and compare allocations as well
as elapsed time; absolute thresholds are not used as machine-dependent unit-test assertions.

## Findings and changes

At 1200 effective pixels, Dark theme, with 1000 synthetic processes and seven default columns:

| UI operation | Before mean | After mean | Before allocations | After allocations |
| --- | ---: | ---: | ---: | ---: |
| Column position update | 2.23 ms | 0.06 ms | 226 KB | 126 B |
| Cross a column boundary | 47.70 ms | 0.29 ms | 3.99 MB | 9.4 KB |
| Explicit sort | 298.26 ms | 28.85 ms | 56.97 MB | 7.60 MB |
| Ordinary refresh | 16.10 ms | 9.36 ms | 20.63 MB | 5.01 MB |
| Refresh with all 17 columns | 22.63 ms | 6.28 ms | 20.63 MB | 4.98 MB |
| Search/filter | 30.66 ms | 25.89 ms | 16.56 MB | 6.91 MB |
| Change selected details | 26.46 ms | 22.53 ms | 145 KB | 134 KB |
| Group toggle | 64.94 ms | 23.66 ms | 12.45 MB | 4.81 MB |
| Resize a column | 7.74 ms | 7.69 ms | 33 KB | 30 KB |

Decimal allocation units above are rounded. Original baseline and comparison reports remain local
under artifacts/ui-performance. These are exploratory measurements, not statistically controlled
certification. The final repeat had refresh p95 42ms and sort p95 54ms, so occasional stalls remain.
Bundled-rule selection measured about 6ms and searching about 3ms; those were not dominant costs.
Realistic CPU-only row updates are also measured separately rather than treating same-object no-ops
as representative resource ticks.

- Drag crossings previously rewrote shared width slots/Grid.Column for realized cells and created
  an animation per cell. Shared transforms now move each entire column without relayout during
  preview; one animation per neighboring column replaces hundreds of independent animations.
- Projection previously scanned every process for every parent's children and reevaluated names
  while maintaining existing order. Verified child indexing and sorting only new siblings reduce
  this work. Row display caches survive resource samples and invalidate for changed metadata/analysis.
- Explicit sorting previously emitted hundreds of native collection moves. One reset for large
  explicit reorder/filter operations reuses row objects and restores selected lifetimes. Live ticks
  stay incremental. Selection/detail expansion, privacy and group identity requirements still apply.
- Native label styles are cached by app-owned style/secondary-text choice. Theme resources remain
  native; commands, paths, captions and item identities are never cache keys or diagnostic output.

## Local rules

The identical console harness evaluated 373 bundled definitions plus 200 learned rules/evidence
records for 500/1000 prebuilt synthetic entities. Tiered compilation was disabled for this comparison.

| 1000-process workload | Before | After | Before allocations | After allocations |
| --- | ---: | ---: | ---: | ---: |
| Rule engine and bundled catalog | 4307 ms | 135 ms | 8664 MiB | 29 MiB |
| SQLite candidate reads plus evaluation | 6308 ms | 1749 ms | 9620 MiB | 984 MiB |

The engine rebuilt an evidence dictionary while validating every unrelated rule. It now indexes
evidence once per evaluation, tests predicate eligibility first and retains full validation for
matching rules. Expiry, command scopes, correction precedence and applied-rule attribution remain
unchanged. Tests cover allocation growth, duplicate evidence precedence and expired supporting proof.

The original per-entity SQLite read path allocated roughly 955 MiB for 1,000 reads. The follow-up
above replaces collection observation's repeated catalog reads with fresh batched snapshots, keeping
single-process reads available. Bundled catalog matching itself was about 1 ms per 1,000 processes.

## Remaining acceptance

Synthetic fixtures exercise shared-transform attachment during row recycling, drag cancellation
cleanup, resource-background restoration, and multiple selected lifetimes through bulk sorting.
Actual mouse/frame presentation, Escape while moving, keyboard focus after large sorts, Narrator,
high contrast and scaling still need desktop acceptance. Active administrator ETW shutdown latency
and longer-running user-session memory remain unverified; provider/network performance is excluded.
