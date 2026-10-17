# MSIX Packaging Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. Work in the existing approved feature branch/checkout; no new worktree is requested.

**Goal:** Build verifiable native MSIX installers and Store submission artifacts without changing the unpackaged development workflow.

**Architecture:** Opt-in single-project MSIX build, shared branding and explicit Direct/Store profiles. Local PowerShell owns input/tool validation, signing and artifact inspection; application code only adapts package-aware administrator availability. Native deployment testing remains separate from artifact generation.

**Tech Stack:** C#/.NET 10, Windows App SDK 2.2, Windows SDK 26100 MSIX/SignTool tools, PowerShell, existing xUnit projects.

**Spec:** [MSIX packaging design](../specs/2026-10-17-msix-packaging-design.md)

## Global constraints

- Windows 11 x64, minimum build 26100; preserve pinned dependencies and unpackaged defaults.
- Explicit `ProcSightPackaging=true`; Direct bundles .NET/Windows App SDK, Store bundles .NET and declares Windows App SDK framework dependencies.
- Development identity `ProcSight.Development`, publisher `CN=ProcSight Development`, initial version `0.1.0.0`.
- Store identity must be supplied; Store version's fourth component must be zero.
- Stable SemVer product versions use annotated vMAJOR.MINOR.PATCH tags and derive Windows versions as MAJOR.MINOR.PATCH.0; Store major must be >= 1.
- Version.props is authoritative; release preparation synchronizes every live version field/changelog without changing dependency pins or historical examples.
- Normal activation remains asInvoker; Store `ElevationMode` is explicitly Disabled or Approved.
- No CI/CD, publication, trust import, user-data migration, installer custom actions or automatic installation.
- Do not package real settings/knowledge/credentials, test fixtures or diagnostics; retain AGPL/notices.

## Review focus

1. Existing debug app and data must survive packaging work without installation or configuration changes.
2. A certificate with a different publisher must fail before a misleading signed artifact is presented.
3. Missing runtimes, unsigned output and missing SDK tools must be distinguishable from a usable installer.
4. Upgrades must preserve synthetic consent/settings while uninstall/reinstall behavior is documented honestly.
5. Store elevation restrictions and actual packaged UAC handoff must not be hidden by build success.

## File map and public script contracts

- `src/ProcSight.App/Package.appxmanifest`: base desktop package manifest/branding; generated identity/capability changes stay in artifacts.
- `src/ProcSight.App/ProcSight.App.csproj`: conditional package settings, per-profile runtime settings and distributed notices.
- `packaging/Direct.pubxml`, `packaging/Store.pubxml`: explicit packaging profiles.
- `packaging/store-identity.example.json`: public identity schema example; production/example validation rejects placeholders.
- `scripts/packaging/Common.ps1`: `Get-ProcSightPackageInputs`, `Get-ProcSightSdkTool`, `Assert-ProcSightPackageContents`; pure validation and bounded SDK discovery.
- `Version.props`: authoritative product VersionPrefix and derived assembly/file/package version properties, imported by Directory.Build.props.
- `scripts/release/Common.ps1`: Get-ProcSightReleaseVersion, Get-ProcSightVersionTargets and Assert-ProcSightVersionConsistency; explicit live product-version inventory and derived values.
- `scripts/prepare-release.ps1`: `(-Bump Major|Minor|Patch | -Version <semver> | -Resume -Version <semver>) -Profile Direct|Store [-DryRun] [-ReleaseDate yyyy-MM-dd]` plus explicit profile/signing arguments; update/verify/commit/build/tag locally.
- `scripts/test-release.ps1`: synthetic temporary repositories and stubbed build/signing calls; no real release tags, remote operations or certificates.
- `scripts/package.ps1`: `-Profile Direct|Store [-IdentityFile <path>] [-CertificateThumbprint <thumbprint>] [-ElevationMode Disabled|Approved] [-FeedUri <https>] [-PackageUri <https>]`; derive version from Version.props, output under artifacts/packages only. No independent release-version override.
- `scripts/new-package-certificate.ps1`: explicit development certificate creation/public export; no trust installation.
- `scripts/test-packaging.ps1`: temporary synthetic fixtures and assertions; no real certificate/account changes.
- `src/ProcSight.Windows/Security/PackageContext.cs`: package identity/capability inspection with restrictive failure behavior.
- `src/ProcSight.App/MainWindow.Elevation.cs`: capability-aware restart availability and app-owned explanation.
- `scripts/generate-icons.ps1`, `src/ProcSight.App/Assets/`: additional MSIX PNG sizes from existing SVG; keep ICO frames within supported dimensions.

`PackageContext.Inspect()` returns `PackagedExecutionContext(bool HasPackageIdentity, bool InspectionSucceeded,
bool AllowsElevation)`. `CanRestartElevated` is true only for a successfully identified unpackaged context
or a successfully inspected package declaring allowElevation. Package observation is injectable in
tests; production reads the current package's installed manifest, never a user-selected manifest.

### Task 0: Central product version and synchronization

**Consumes:** existing app.manifest version 0.1.0.0 and build defaults. **Produces:** Version.props,
explicit live-version inventory, shared validation and synthetic script tests for later release tasks.

- [ ] Write failing `scripts/test-release.ps1` cases for stable SemVer parsing/bumping, v-tag derivation, initial explicit release, component overflow, leading zeroes, unsupported prerelease/build suffixes and Store major zero.
- [ ] Add Version.props with VersionPrefix 0.1.0, import it in Directory.Build.props and derive AssemblyVersion/FileVersion/ProcSightPackageVersion as prefix plus .0. InformationalVersion includes the actual source commit; packages must never embed a pre-release-commit SHA.
- [ ] Implement Get-ProcSightVersionTargets as the inventory of live checked-in version fields: Version.props, app.manifest and the package manifest once added. Add future live README badges/download references only if they exist. Get-ProcSightReleaseVersion validates/computes values; Assert-ProcSightVersionConsistency compares evaluated MSBuild properties and manifest identities.
- [ ] Add failing drift tests for each target and tests proving historical docs, dependency pins and sample version strings remain unchanged. Implement scoped XML updates without broad replacement; use synthetic manifest fixtures until Task 1 adds the real package manifest.
- [ ] Run script tests plus normal Release verification, update developer/version maintenance guidance and commit without a release bump/tag.

### Task 1: Opt-in package builds and branding

**Consumes:** Task 0 central version, existing pinned application project, SVG and native asInvoker manifest.
**Produces:** Direct and Store package outputs with explicit identity/version/dependency declarations.

- [ ] Add synthetic checks to `scripts/test-packaging.ps1` asserting normal build defaults to WindowsPackageType None, explicit profiles select correct runtime modes, minimum OS 26100/x64 and no unexpected capabilities; verify failure before implementation.
- [ ] Add manifest and conditional project/profile configuration deriving version from Task 0; add the manifest to its synchronization inventory and reject drift. Generate StoreLogo (50), Square44x44Logo and Square150x150Logo from existing SVG, including scale variants needed for package validation; do not put >256px frames into the ICO.
- [ ] Include LICENSE and existing notices in the package. Keep normal scripts/build.ps1 and F5 commands unchanged.
- [ ] Run normal Release and Direct builds using the development identity; inspect output with SDK MakeAppx unpack to verify executable, assets, version, architecture and dependencies. Exercise Store inputs/dependency configuration with synthetic tests; build an actual Store artifact only when a reserved identity and valid Store release version are supplied, otherwise record that external prerequisite explicitly.
- [ ] Update developer packaging prerequisites and commit this deliverable.

### Task 2: Input validation, artifact inspection and optional feed

**Consumes:** Task 1 manifests/profiles. **Produces:** `scripts/package.ps1`, shared validators, deterministic artifact paths and optional `.appinstaller` file.

- [ ] Write failing script tests for malformed/overflow version, Store revision nonzero, example production identity, absent Store identity, paths with spaces, missing SDK tool, nonzero SDK exit and unexpected packaged files.
- [ ] Implement profile/identity/version validation, locked restore and isolated intermediate/output directories under artifacts/packages; do not reuse or remove active debug outputs.
- [ ] Validate actual generated manifest and archive contents. Reject secrets/settings/databases/diagnostics and accidental repository/test content; signed/unsigned artifacts have distinct names and output labels.
- [ ] Add feed tests for HTTPS/userinfo validation and XML escaping; generate feed only when both explicit FeedUri and PackageUri are supplied for production Direct output. Use nonblocking on-launch update checks without forced updates/background scheduling.
- [ ] Run `./scripts/test-packaging.ps1` and `./scripts/verify.ps1`; document exact invocation/output and commit.

### Task 3: Explicit development/production signing

**Consumes:** validated package inputs and artifact from Task 2. **Produces:** signed, verified Direct artifact or clearly labeled unsigned/Store-preparation artifact.

- [ ] Add failing tests using synthetic certificate metadata and a stub SDK invocation for publisher mismatch, expired/non-code-signing certificate, missing selected thumbprint and signing/verification failure.
- [ ] Implement explicit development certificate command using CurrentUser\\My, nonexportable key and public-only export to artifacts; never import trust automatically.
- [ ] Implement thumbprint-based SHA-256 SignTool invocation, publisher eligibility checks and signature verification. Require timestamping and trusted certificate checks for production Direct output. Do not pass private-key passwords in command arguments.
- [ ] Document deliberate manual development trust and installation steps; `.gitignore` must cover certificate exports/local overrides/uploads as well as existing private-key/package patterns.
- [ ] Run synthetic signing tests; manually verify signing on a disposable test environment only with approval. Commit source/docs without generated packages or certificates.

### Task 4: Packaged administrator compatibility

**Consumes:** explicit profile/elevation choice and existing ElevationLauncher/session-consent boundaries.
**Produces:** package-aware administrator availability without changing unpackaged behavior.

- [ ] Add failing Windows tests for unpackaged availability, packaged declared elevation, packaged Store-disabled elevation and failed capability inspection. Existing elevation cancellation/readiness tests remain required.
- [ ] Implement PackageContext using native package/capability APIs; unavailable inspection must not claim elevation support. Generate Direct allowElevation and Store capabilities only from explicit mode; Store mode missing elevation choice fails validation.
- [ ] Update administrator Settings to explain unavailable packaged elevation; do not invoke ShellExecute when capability is absent. Preserve asInvoker and all session sharing restrictions.
- [ ] Run Windows tests and native synthetic Settings fixtures; perform actual packaged restart/cancel/ready-handoff checks on a disposable test system when authorized. Record external Store approval as unverified unless provided by publisher.
- [ ] Update security/architecture UML and commit.

### Task 5: Installation lifecycle acceptance and documentation

**Consumes:** inspected/signed packages and package-aware app. **Produces:** reproducible acceptance checklist and accurate supported distribution status.

- [ ] Add synthetic update fixtures for settings, disabled sharing, rules and account references; inspect virtualization/active-user Credential Manager behavior without exporting real credentials.
- [ ] On an explicitly authorized disposable account/VM, install version 0.1.0.0, test native activation/collection/icons/tray, OAuth loopback return and local endpoint access; update to 0.1.1.0 and verify synthetic consent/data; uninstall/reinstall and record actual data/credential retention.
- [ ] Run Windows App Certification Kit on Store-preparation output when available, recording capability/runtime results. Build/certification success does not authorize submission.
- [ ] Run locked Release verification, packaging script tests, package/signature inspection, documentation link checks, Mermaid rendering and git diff --check.
- [ ] Update README, DEVELOPERS, ARCHITECTURE, SECURITY, AGENTS, CHANGELOG, docs/verification.md and ignores in the same commits as behavior. Mark implementation versus live verification precisely.
- [ ] Obtain independent code review, address material findings and leave artifacts local. No remote publication or installer launch against the user's active setup.

### Task 6: Local release preparation, changelog and annotated tags

**Consumes:** Task 0 version helpers, Task 2 packaging command, Task 3 signing and verified profiles.
**Produces:** scripts/prepare-release.ps1, resumable release preparation and matching local annotated tag.

- [ ] Add failing temporary-repository tests for dirty checkout/index, detached HEAD, missing Git identity, duplicate/mismatched tag, no Unreleased notes, lower/equal subsequent version and -DryRun with zero mutations.
- [ ] Implement preflight and in-memory edit preparation. Move Unreleased content verbatim to the chosen version/date, retain fresh Unreleased, synchronize all inventory fields and prepare versioned release notes under ignored artifacts.
- [ ] Add failing tests for each pre-commit validation failure restoring only owned files and not moving HEAD. Run version consistency and scripts/verify.ps1 before committing the explicit release inventory.
- [ ] Create the local `chore: release vX.Y.Z` commit, then invoke inspected/signed packaging from that exact clean commit. Verify informational commit ID, MSIX/bundle/feed identities, filenames, release notes and checksum manifest agree. Only then create annotated tag vX.Y.Z at that commit.
- [ ] Add failure-injection tests: package/signing failure leaves a prepared commit without a tag; -Resume uses the same version/commit; successful rerun is idempotent; existing tags never move. Refuse unrelated changes during resume and report recovery without destructive history edits.
- [ ] Assert no command executes git push/fetch, gh release, remote upload, Store submission or CI/CD configuration. Run the whole workflow against temporary repositories with fake packaging, not this product branch's real release history.
- [ ] Update README/DEVELOPERS/AGENTS/ARCHITECTURE/SECURITY/CHANGELOG and verification notes with commands, version inventory, lifecycle and limitations. Independently review the workflow and commit tooling only; do not create an actual product release/tag during implementation.

## Execution handoff

Written design/plan await review. Recommended execution is inline in the approved checkout, followed
by independent review, consistent with the user's existing preference. Store publisher identity,
production signing and disposable installation environment are supplied only when those stages need
them; missing external prerequisites do not block preparing source or unsigned/test artifacts.
