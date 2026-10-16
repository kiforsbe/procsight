# Explicit administrator mode Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Support explicit administrator restart with honest privilege feedback and restrictive elevated-session sharing defaults.

**Architecture:** Detect the current token/context, keep saved preferences separate from effective session policy, and launch only the actual application executable through UAC. Preserve the original instance until the replacement is confirmed ready; retain all read-only collection and untrusted-input boundaries.

**Tech Stack:** C#/.NET 10, WinUI 3, Windows token/ShellExecuteEx APIs; asInvoker manifest and uiAccess=false.

**Spec:** [Approved design and security assessment](../specs/2026-10-16-connections-tray-elevation-design.md).

## Global Constraints

- Normal launch stays unelevated; never elevate automatically after denied metadata access.
- No process/service management, privilege adjustment or SeDebugPrivilege enablement.
- Each elevated session starts with automatic external analysis off and online command/identifying-data sharing excluded.
- Use only the current Windows security context's settings/credentials; never copy tokens across accounts.
- Full-app elevation has residual risk; collector-only broker is future hardening, not an implemented boundary.
- No UIPI weakening, arbitrary privileged IPC or executable paths from editable settings/model/process data.

## Review Focus

- UAC cancellation/bootstrap failure must leave the original instance usable (Task 2).
- Alternate-user elevation must never borrow the original account's configuration (Tasks 1–2).
- Saved broad sharing must not become effective automatically in an elevated session (Task 1).
- Fallback, research and explicit tests must honor the same session restrictions (Task 3).
- Restricted/PPL metadata must remain partial/unavailable even as administrator (Task 3).

---

### Task 1: Privilege context and ephemeral privacy policy

**Files:** Create `src/ProcSight.Windows/Security/PrivilegeContext.cs`, `src/ProcSight.Application/Settings/SessionPolicy.cs`; modify `src/ProcSight.App/MainWindow.Runtime.cs`; test `tests/ProcSight.Application.Tests/SessionPolicyTests.cs`, `tests/ProcSight.Windows.Tests/PrivilegeContextTests.cs`.

**Interfaces:** Produce `PrivilegeContext.IsElevated: bool` from the active token and current-user context, not launch arguments. Produce `SessionConsent(bool ExternalAnalysis=false, bool CommandLine=false, bool IdentifyingMetadata=false)` and `SessionPolicy.Effective(AnalysisPolicy saved, bool elevated, SessionConsent consent): AnalysisPolicy`. Keep saved policy and session consent separate; session consent is never persisted.

- [ ] Write failing tests: normal policy unchanged; elevated startup blocks external automatic dispatch and strips online command/identifying sharing despite broad saved defaults; local processing remains available; fresh explicit session consent permits only saved-and-consented permissions. Restart discards consent and saving settings never accidentally persists the restrictive session overlay as normal preferences.
- [ ] Add synthetic context tests ensuring no passed-in original-user path or credential object is consulted. Test explicit online attempts without session consent are blocked as well as automatic attempts; policy revisions invalidate old queued work.
- [ ] Run focused SessionPolicy/PrivilegeContext tests; confirm failures.
- [ ] Implement effective-policy overlay plus routing eligibility checks for external automatic work while preserving local-only automatic work. Read current-context standard settings/credential locations only. Validate untrusted preferences/profiles and existing rule scopes before elevated use; retain existing input caps.
- [ ] Re-run tests; require pass. Update SECURITY with full-app risk, cross-account isolation and unsigned-checkout trust; update ARCHITECTURE saved/effective-policy UML and AGENTS constraints.
- [ ] Commit context/session guards and documentation.

### Task 2: Safe explicit restart and confirmed handoff

**Files:** Create `src/ProcSight.Windows/Security/ElevationLauncher.cs`, `src/ProcSight.App/MainWindow.Elevation.cs`; modify `src/ProcSight.App/App.xaml.cs`, `src/ProcSight.App/MainWindow.xaml.cs`; test `tests/ProcSight.Windows.Tests/ElevationLauncherTests.cs`.

**Interfaces:** Produce `ElevationLauncher.RestartAsync(CancellationToken ct): Task<ElevationResult>` and `ElevationResult { Ready, Cancelled, Failed, TimedOut }`. Use an injected shell/process-readiness adapter for tests. Resolve `Environment.ProcessPath` to the actual absolute app executable; ShellExecuteEx uses verb `runas`, with a retained process handle, no user/model-provided arguments. Readiness waits at most 20 seconds for an explicitly ready main window belonging to the returned process.

- [ ] Write failing tests: UAC error 1223 maps to Cancelled, shell launch failure maps to Failed, bootstrap exit/timeout preserves original, success closes original only after readiness, unexpected PID/window cannot confirm readiness, handles always close. No arbitrary input can alter executable, verb or arguments.
- [ ] Run `dotnet test tests/ProcSight.Windows.Tests --filter ElevationLauncher`; confirm failures.
- [ ] Implement bounded handoff using process-handle/PID-owned-window checks; mark the launched main window ready only after runtime initialization. Do not use a lower-trust acknowledgement file or accept arbitrary privileged window commands. Keep original usable on cancellation/failure; show app-owned recovery text.
- [ ] Add a native shield action “Restart as administrator” and persistent Administrator indicator. When already elevated, explain reopening normally through the unelevated Windows shell; do not claim launching an ordinary child removes elevation. Keep manifest asInvoker/uiAccess=false.
- [ ] Re-run tests; require pass. Update ARCHITECTURE handoff sequence/state diagrams, SECURITY restart behavior, README and changelog.
- [ ] Commit launch/UI integration and documentation.

### Task 3: Consent UI, cross-feature safeguards and acceptance

**Files:** Modify `src/ProcSight.App/MainWindow.Settings.cs`, `src/ProcSight.App/MainWindow.Settings.Connections.cs`, `src/ProcSight.App/MainWindow.VisualFixture.cs`, `src/ProcSight.Application/Analysis/AnalysisCoordinator.cs`, `src/ProcSight.Application/Analysis/ConnectionTester.cs`; test `tests/ProcSight.Infrastructure.Tests/ElevatedAnalysisTests.cs`, `tests/ProcSight.Infrastructure.Tests/RestrictedProcessAnalysisTests.cs`.

**Interfaces:** Coordinator, tester, research and preview consume Task 1 effective policy and session eligibility. Settings display saved choices and explicit elevated-session consent separately; consent updates are immediate and invalidate old work. No administrator-only process action is added to tray commands.

- [ ] Add failing fake-provider tests covering manual Analyze/test consent, automatic external suppression, permitted local processing, fallback/research projection, consent revocation during a pending call and stale result rejection. Assert no raw metadata/commands leak into activity and no original-user credentials are requested in alternate context.
- [ ] Add regression tests that elevated mode does not reinterpret denied/PPL reads as empty verified metadata or repeatedly reset restricted lifetime selection.
- [ ] Run `dotnet test tests/ProcSight.Infrastructure.Tests --filter 'ElevatedAnalysis|RestrictedProcessAnalysis'`; confirm failures.
- [ ] Implement native explanatory consent controls with per-session scope and app-owned unavailable messages. Audit endpoint/rule/input bounds and all profile/research/test call paths. Record security findings and actual mitigation in SECURITY; do not represent full-app elevation as a broker boundary.
- [ ] Re-run focused tests and `scripts/verify.ps1`. Capture synthetic light/dark wide/narrow elevated settings/indicator. Exercise UAC cancellation, successful launch, WinAppRuntime bootstrap and alternate account live only through normal Windows UI; never collect a UAC password. Record unperformed acceptance explicitly.
- [ ] Update README, DEVELOPERS, ARCHITECTURE/UML, AGENTS, WINUI3-DESIGN, SECURITY, CHANGELOG and verification. Verify Mermaid/links and `git diff --check`; commit verified changes. No publication, installation hardening or CI/CD.
