# App icon and tray monitoring Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give ProcSight a recognizable native icon and a selectable live system-load tray indicator.

**Architecture:** Keep metric samples and rendering independent of process analysis. A disposable Windows adapter owns notification registration and native handles; WinUI owns settings/menu actions and window visibility.

**Tech Stack:** C#/.NET 10, WinUI 3, Windows notification APIs, PDH, adapter statistics, editable SVG and generated ICO/PNG.

**Spec:** [Approved design](../specs/2026-10-16-connections-tray-elevation-design.md).

**Execution record (2026-10-16):** Implemented inline and independently reviewed. Native icon/metric
and registration tests pass; light/dark wide/narrow Settings captures exist. Actual shell/DPI/menu,
Explorer restart and metric accuracy remain desktop acceptance checks. See [verification](../../verification.md).
The checklist below preserves the original plan.

## Global Constraints

- Default metric is CPU; tray visibility and minimize-to-tray are explicit preferences.
- Closing still exits by default; Exit always exits.
- Unavailable/warm-up data is explicit and never rendered as zero.
- No providers, credentials, command lines or persistent histories in tray collection/rendering.
- Collection/rendering is bounded and kept off the UI thread as appropriate.

## Review Focus

- Missing counters and first samples must show unavailable, not fabricated zero (Task 2).
- Explorer recreation must restore exactly one notification icon (Task 3).
- Metric switching must dispose prior counters and reject late samples (Tasks 2–3).
- High DPI/light/dark tray assets must remain recognizable at small sizes (Tasks 1, 3).
- Hidden-window exit and failure recovery must never strand an invisible app (Task 4).

---

### Task 1: App identity assets

**Files:** Create `src/ProcSight.App/Assets/ProcSight.svg`, `src/ProcSight.App/Assets/ProcSight.ico`, PNG variants and `scripts/generate-icons.ps1`; modify `src/ProcSight.App/ProcSight.App.csproj`, `src/ProcSight.App/MainWindow.xaml.cs`, `src/ProcSight.App/MainWindow.xaml`.

**Interfaces:** Generated `Assets/ProcSight.ico` supplies executable ApplicationIcon and `AppWindow.SetIcon`; SVG remains the editable source. Generator emits ICO frames at 16, 20, 24, 32, 40, 48, 64 and 256 pixels without network downloads.

- [ ] Create the process-chip/magnifying-lens SVG and reproducible local asset generator with deliberate small-size strokes. Use existing installed tooling; if a new library is necessary, pin it and include notices/lock files.
- [ ] Generate assets, inspect all small frames on light/dark backgrounds and verify ICO dimensions/alpha. Apply actual executable/window/title-bar identity; confirm the built executable contains the icon resource.
- [ ] Build Release app, capture synthetic native wide/narrow light/dark views, record actual asset/build checks in verification and document generation in DEVELOPERS; add changelog entry. No redundant implementation-mirroring unit test for static art.
- [ ] Commit assets, integration and documentation.

### Task 2: Bounded system metrics

**Files:** Create `src/ProcSight.Core/Models/TrayModels.cs`, `src/ProcSight.Windows/Collection/TrayMetricSampler.cs`, `src/ProcSight.Windows/Collection/PerformanceCounterReader.cs`; test `tests/ProcSight.Windows.Tests/TrayMetricTests.cs`.

**Interfaces:** Produce `TrayMetric { Cpu, Memory, Disk, Network, Gpu }`, `TraySample(TrayMetric Metric, double? Value, string Unit, TraySampleState State)` and `TraySampleState { Available, WarmingUp, Unavailable }`. Produce disposable `TrayMetricSampler.SampleAsync(TrayMetric metric, SystemResourceSample? system, CancellationToken ct): Task<TraySample>`. Native counter access is injectable for synthetic tests.

- [ ] Write failing tests: CPU/RAM reuse snapshot values, first delta sample warms up, counter absence returns Unavailable, reset/wrapped network counters do not spike, memory divide-by-zero is unavailable, selection changes dispose old queries. Cap counter enumeration to 4096 instances and sample at most once per second.
- [ ] Run `dotnet test tests/ProcSight.Windows.Tests --filter TrayMetric`; require new-case failures.
- [ ] Implement disk busy via total physical-disk counters, network bytes/sec via active non-loopback adapter deltas, GPU via maximum valid engine utilization with tooltip “Busiest GPU engine”. Use English PDH counter APIs to avoid localized-path assumptions, finite-value validation and SafeHandle disposal. Never label network throughput as a percent; disk/GPU percentages clamp valid computed values to 0–100, not missing values.
- [ ] Re-run focused tests; require pass. Compare live selected metrics to Windows tools without collecting process inventory; document differences in aggregation and unavailable environments. Update ARCHITECTURE collection boundary/UML, README, AGENTS, SECURITY and changelog.
- [ ] Commit sampler and documentation.

### Task 3: Tray lifecycle and dynamic icon

**Files:** Create `src/ProcSight.Windows/Tray/NotificationIcon.cs`, `src/ProcSight.Windows/Tray/TrayIconRenderer.cs`; test `tests/ProcSight.Windows.Tests/NotificationIconTests.cs`.

**Interfaces:** Produce disposable `NotificationIcon.Update(TraySample sample)`, `Show()`, `Hide()` and typed activation/menu events. `TrayIconRenderer.Render(TraySample sample, int pixelSize): SafeHandle` owns the created icon until handed to the notification adapter. Inject shell calls and renderer for lifecycle tests; no arbitrary command strings in callbacks.

- [ ] Add failing tests for add/update/delete order, Explorer TaskbarCreated re-add, failed registration recovery, late sample suppression, deterministic HICON/GDI cleanup and repeated Hide/Dispose. History holds at most 30 samples; tooltip stays within the native 127-character bound and contains no private process data.
- [ ] Run `dotnet test tests/ProcSight.Windows.Tests --filter NotificationIcon`; confirm failures.
- [ ] Implement native version-4 notification behavior, one icon, bounded history/load rendering, unavailable glyph and unit-aware tooltip. Register the Explorer recreation message; own and remove only the app's handlers/icon. Keep rendering off UI thread and dispatch typed activation back safely.
- [ ] Re-run tests; require pass. Capture synthetic CPU/memory/network/unavailable renderings at 16/20/24/32 px against light/dark backgrounds; document actual checks and unresolved shell accessibility testing.
- [ ] Commit lifecycle/rendering plus architecture lifecycle state diagram and changelog.

### Task 4: Native preferences, menu and window lifecycle

**Files:** Modify `src/ProcSight.Core/Models/PolicyModels.cs`, `src/ProcSight.App/MainWindow.Settings.cs`, `src/ProcSight.App/MainWindow.xaml.cs`, `src/ProcSight.App/MainWindow.VisualFixture.cs`; create `src/ProcSight.App/MainWindow.Tray.cs`; test `tests/ProcSight.Windows.Tests/SettingsStoreTests.cs`, `tests/ProcSight.Application.Tests/TrayPreferencesTests.cs`.

**Interfaces:** Append `bool ShowTrayIcon=false`, `TrayMetric TrayMetric=TrayMetric.Cpu`, `bool MinimizeToTray=false` to `PresentationPreferences`. Integrate Task 2 sampler and Task 3 adapter without coupling them to AI routing. Settings use the immediate apply flow from the connections plan.

- [ ] Write failing preference/lifecycle tests: old JSON defaults remain valid, unknown metric is rejected, selected metric persists, minimize-to-tray requires successful icon registration, close exits, Exit disposes and exits even when hidden.
- [ ] Run focused SettingsStore/TrayPreferences tests and confirm new failures.
- [ ] Implement accessible menu Open ProcSight, metric selection, Settings, Exit. Restore/activate the window on icon activation; collect metrics while minimized. If shell icon registration fails, retain a recoverable visible window. Metric choice updates immediately and cancels obsolete sampling. App closure disposes tray/counter resources before shutdown.
- [ ] Re-run tests and `scripts/verify.ps1`. Capture native settings/menu and exercise Explorer restart, DPI changes, minimize/restore and Exit live; list anything not exercised as unverified. Update README, DEVELOPERS, ARCHITECTURE/UML, AGENTS, WINUI3-DESIGN, SECURITY and verification; check docs links and `git diff --check`.
- [ ] Commit verified integration and documentation.
