# Immediate settings and connection priority Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Apply valid settings immediately and let users configure, enable, order and explicitly test independent AI connections with opt-in fallback.

**Architecture:** Store profiles separately from provider kind and resolve an immutable eligible route for each policy revision. Keep draft validation and serialized persistence outside WinUI event handlers; preserve local-first analysis, privacy projections and actual-attempt accounting.

**Tech Stack:** C#/.NET 10, WinUI 3, existing SQLite ledger and Windows credentials; no new dependency.

**Spec:** [Approved design](../specs/2026-10-16-connections-tray-elevation-design.md), including the immediate-settings amendment.

**Execution record (2026-10-16):** Implemented inline and independently reviewed. Review findings
were fixed with targeted regressions; the full 330-test suite passed. See [verification](../../verification.md)
for actual checks and remaining desktop acceptance. The checklist below preserves the original plan.

## Global Constraints

- Fallback defaults off; disabled profiles never participate automatically.
- Local rules and knowledge remain first; ordering cannot relax projection/privacy rules.
- ChatGPT plan billing stays separate from API billing; credentials remain Windows-protected.
- AI activity remains memory-only and bounded to 200 actual attempts, without raw private data.
- Use this checkout/feature branch; no CI/CD, remote publication or process management.
- Settings apply without Save; incomplete text retains the last valid configuration.

## Review Focus

- Legacy provider plus local model must migrate without enabling new billing paths (Task 1).
- Rapid edits and navigation must not let an old commit restore broader sharing (Task 3).
- Authentication failure on one account must not pause unrelated profiles (Task 2).
- Invalid text or storage failure must not falsely report a setting as applied/saved (Task 3).
- Explicit testing must target the selected profile without fallback or real process metadata (Task 4).

---

### Task 1: Validated profiles and safe migration

**Files:** Modify `src/ProcSight.Core/Models/PolicyModels.cs`, `src/ProcSight.Windows/Storage/PreferencesStore.cs`, `src/ProcSight.App/MainWindow.Runtime.cs`; create `src/ProcSight.Core/Models/ConnectionProfile.cs`, `src/ProcSight.Application/Settings/ConnectionSettingsMigration.cs`; test `tests/ProcSight.Windows.Tests/SettingsStoreTests.cs`, `tests/ProcSight.Core.Tests/ConnectionProfileTests.cs`.

**Interfaces:** Produce `ConnectionProfile(string ProfileId, ProviderSettings Provider, bool Enabled, int Priority)`; append optional `ConnectionProfile[]? Connections=null`, `bool FallbackEnabled=false`, `string? ResearchProfileId=null` to `AnalysisPolicy`. `ProviderSettings.Id` remains provider kind; append `string? CredentialId=null` for protected lookup. Produce `ConnectionSettingsMigration.Migrate(AnalysisPolicy policy, ICredentialStore credentials): AnalysisPolicy`.

- [ ] Write failing tests: legacy cloud/local migrate with the same effective primary/local preference; automatic/research/sharing flags stay unchanged; fallback is false; API keys are absent from serialized JSON. Reject duplicate/invalid profile IDs, invalid endpoint/model/account combinations, negative priorities and more than 32 profiles. Equal priorities sort by original array position.
- [ ] Run `dotnet test tests/ProcSight.Core.Tests --filter ConnectionProfile` and `dotnet test tests/ProcSight.Windows.Tests --filter SettingsStore`; confirm new assertions fail before implementation.
- [ ] Implement validation, migration and atomic preference round-trip under the existing 65,536-byte read cap. Copy legacy credentials to profile-specific keys before publishing migrated configuration; on failure retain the legacy usable policy and show safe feedback. Do not delete old keys until migration is confirmed. Bind ChatGPT model choices to their account.
- [ ] Re-run focused tests; require all pass. Document profile storage/migration and credential boundaries in ARCHITECTURE, SECURITY and DEVELOPERS; add Unreleased changelog entry.
- [ ] Commit the tested profile/migration change with its documentation.

### Task 2: Routing, guarded fallback and actual provenance

**Files:** Create `src/ProcSight.Application/Analysis/ConnectionRouter.cs`; modify `src/ProcSight.Application/Analysis/AnalysisCoordinator.cs`, `src/ProcSight.Application/Analysis/AnalysisActivityLog.cs`, `src/ProcSight.App/MainWindow.Runtime.cs`; test `tests/ProcSight.Infrastructure.Tests/ConnectionRoutingTests.cs`, `tests/ProcSight.Infrastructure.Tests/ActivityCoordinatorTests.cs`.

**Interfaces:** Produce `ConnectionRouter.Resolve(AnalysisPolicy policy): IReadOnlyList<ConnectionProfile>` and `ConnectionRouter.CanFallback(ProviderError error): bool` using the existing error contract. Coordinator consumes resolved profiles, destination-specific projection and profile credential lookup from Task 1. Provider factories still consume `ProviderSettings` and destination; provenance records actual provider kind/model, not account or credential IDs.

- [ ] Add fake-provider tests asserting ordered enabled selection, all-disabled guidance, LocalOnly local preference, default zero fallback calls, and explicit fallback on availability/rate/auth failures after bounded retries. Verify independent auth pause for two ChatGPT profiles.
- [ ] Add tests asserting no fallback on cancellation, stale revision, invalid configuration, exhausted budget, malformed/rejected output or a valid Unknown answer. Every online attempt reserves budget; each destination rebuilds its projection; research uses only the explicit eligible capable research profile. Activity and accepted rule provenance identify the actual successful provider/model.
- [ ] Run `dotnet test tests/ProcSight.Infrastructure.Tests --filter 'ConnectionRouting|ActivityCoordinator'`; confirm the new cases fail.
- [ ] Implement route snapshots, per-profile authentication pause, existing bounded retry policy, stop conditions and safe fallback reason codes. Policy updates cancel old work; do not weaken output validation. Update payload preview to use the same route resolver.
- [ ] Re-run focused tests, require pass; update ARCHITECTURE sequence/state diagrams and AGENTS routing constraints, SECURITY and changelog.
- [ ] Commit routing and corresponding documentation.

### Task 3: Serialized immediate settings

**Files:** Create `src/ProcSight.Application/Settings/ImmediateSettingsController.cs`; modify `src/ProcSight.App/MainWindow.Settings.cs`, `src/ProcSight.App/MainWindow.ChatGpt.cs`, `src/ProcSight.App/MainWindow.Runtime.cs`; test `tests/ProcSight.Application.Tests/ImmediateSettingsTests.cs`.

**Interfaces:** Produce `ImmediateSettingsController.ApplyAsync(AnalysisPolicy candidate, PresentationPreferences presentation, CancellationToken ct): Task<SettingsApplyResult>` with `SettingsApplyResult(bool Applied, bool Persisted, long Revision, SettingsFailure Failure)` and an app-owned `SettingsFailure` enum. Constructor receives `IPreferencesStore`, coordinator policy-change delegate and current snapshots. Produce `FlushAsync(CancellationToken ct): Task` for pending debounced edits. Never include credential text in result objects.

- [ ] Write deterministic fake-clock/store/coordinator tests: toggle immediate, text applies after 400 ms, incomplete URI/model never replaces valid settings, focus-loss flush, unchanged values do not bump revision, rapid A/B completion cannot restore A, and navigation flushes only valid edits. Initialization and model discovery must not enable analysis.
- [ ] Add failure tests: restrictive changes remain effective despite disk failure, failed saves show unsaved feedback, no stale completion overwrites newer status, API-key edits only commit nonblank on Enter/focus loss, blank does not delete stored credentials. Restrictive policy cancellation must precede slow persistence; an enabling change cannot dispatch before successful validation/persistence.
- [ ] Run `dotnet test tests/ProcSight.Application.Tests --filter ImmediateSettings`; confirm failure.
- [ ] Implement serialized updates, draft generation guards, inline validation and immediate privacy restrictions. Replace Save handlers with control events, separate independent presentation/privacy fields from invalid connection drafts, and remove all “then save” messages. Never rebuild the entire Settings page on each edit or log draft values.
- [ ] Re-run focused tests; require pass. Update WINUI3-DESIGN immediate-feedback convention, ARCHITECTURE settings lifecycle UML, README, DEVELOPERS, AGENTS and changelog.
- [ ] Commit immediate-settings behavior and documentation.

### Task 4: Native priority/configuration UI and selected connection test

**Files:** Create `src/ProcSight.App/MainWindow.Settings.Connections.cs`; modify `src/ProcSight.App/MainWindow.Settings.cs`, `src/ProcSight.App/MainWindow.ChatGpt.cs`, `src/ProcSight.App/MainWindow.VisualFixture.cs`; create `src/ProcSight.Application/Analysis/ConnectionTester.cs`; test `tests/ProcSight.Infrastructure.Tests/ConnectionTesterTests.cs`.

**Interfaces:** Produce `ConnectionTester.TestAsync(ConnectionProfile profile, AnalysisPolicy policy, CancellationToken ct): Task<ProviderResult>` using injected existing provider factory, ledger and activity log. The tester makes one explicit synthetic test operation on that profile, with no routing/fallback. UI consumes Task 3 controller for configuration/order/enable updates.

- [ ] Write tests asserting selected disabled profile can be explicitly tested, another profile is never contacted, synthetic name is `synthetic-connection-test.exe`, command/ownership/private metadata are absent, budget is honored, test does not enable analysis, and incomplete edits prevent stale-profile testing. Preserve safe activity outcomes and cancellation.
- [ ] Run `dotnet test tests/ProcSight.Infrastructure.Tests --filter ConnectionTester`; confirm new cases fail.
- [ ] Implement compact native selectable profile rows with enable toggles, accessible Move up/down, adaptive configuration, and Test connection beside selected-profile controls in the priority section. Clearly label billing destination and explicit fallback disclosure; disabled configuration remains editable. Remove the old page-bottom test button and Save button. Profile-specific API-key removal and ChatGPT sign-out must not affect unrelated accounts/profiles. Do not transmit credentials to a changed endpoint until explicit credential entry/authorization for that destination.
- [ ] Re-run tester tests and capture synthetic 1100/600-width light/dark settings with configured, disabled, invalid and empty states. Assert header/control bounds and test-target labels in `MainWindow.VisualFixture.cs`; manually check keyboard order and record remaining accessibility checks.
- [ ] Run `scripts/verify.ps1`, verify documentation links/Mermaid and `git diff --check`. Update verification evidence and README screenshot/features; commit only sanitized source/assets/docs. If the debug executable is locked, preserve the session and use a scratch Debug output for fixtures; Release verification remains required.

## Handoff

Implement this plan first, then tray monitoring, then administrator mode. Review this plan with the user before implementation; preserve the selected execution approach for all three plans.
