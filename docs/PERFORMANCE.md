# Local UI and rules performance

Measured on 2026-10-17 using synthetic processes, metadata and rules. No provider requests,
credentials, real inventory dumps or machine-specific rules were used. The accepted native column
interaction remains the visual requirement; these changes optimize its implementation.

## Repeat the measurements

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

SQLite still reads/deserializes the stored candidate catalog per entity: roughly 1.45 seconds and
955 MiB for 1000 reads in this workload. This remains the clearest local-rules follow-up. A future
indexed/cached candidate store needs explicit invalidation and privacy/scope design; no unsafe shared
mutable cache or weakened validation was introduced. Bundled catalog matching itself was about 1ms
per 1000 processes and did not warrant optimization.

## Remaining acceptance

Synthetic fixtures exercise shared-transform attachment during row recycling, drag cancellation
cleanup, resource-background restoration, and multiple selected lifetimes through bulk sorting.
Actual mouse/frame presentation, Escape while moving, keyboard focus after large sorts, Narrator,
high contrast and scaling still need desktop acceptance. Collection API timings, SQLite redesign and
provider/network performance are outside these measured UI changes.
