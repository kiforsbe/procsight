# Shared Windows tooling integration implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans for native execution in the current session. Implementation is pending user review of this plan.

**Goal:** Make winbuildtools configurable by consuming projects, integrate it into
ProcSight, add a Settings version footer and prepare ProcSight 0.3.0 in Git.

**Architecture:** A strict v2 MSI contract accepts resolved product identities,
exact directories, scope and servicing policy. A product adapter supplies
ProcSight compatibility inputs and delegates mechanisms to the pinned tools.
The existing v1 contract remains compatible with Agenda.

**Tech stack:** PowerShell 7.4+, Windows Installer 5.0 COM/native tables,
MakeCab/Expand, Windows SDK MakeAppx/SignTool, .NET 10 and native WinUI 3.

**Spec:** [Integration design](../specs/2026-10-22-winbuildtools-integration-design.md).

## Global constraints

- Work in the user-approved ProcSight checkout and inspected winbuildtools checkout;
  preserve unrelated changes and shared VS Code files. Do not reset Agenda's checkout.
- No product-specific branches, identities or naming policy inside shared tools.
- Preserve .NET Runtime 10.0.13+ within 10.0 and debugger support.
- Preserve forced audited profile restore and locked dependencies; do not suppress audit failures.
- Ordinary tests never install products, import trust, create persistent keys or terminate apps.
- Keep isolated-identity read-only MSI preflight through InstallValidate.
- Just me / All users is an installation-time choice; no automatic cross-scope migration.
- ProcSight 0.3.0 uses its existing release script; no automatic GitHub Actions dispatch
  or public GitHub Release publication. Never publish an unreachable submodule commit.

## Review focus

- Existing ProcSight machine identities and directories remain exact after migration (Task 4).
- Scope controls run before costing and do not appear during maintenance (Task 2).
- A changed certificate file cannot substitute the imported object (Task 3).
- Changed MSI destination/upgrade semantics fail inspection despite correct payload bytes (Task 2).
- A fresh recursive consumer clone uses the exact shared commit without sibling fallback (Tasks 4/6).

## Task 1: Review dependencies and lock the configuration contract

**Files:** winbuildtools `msi/config-v2.schema.json` (new), `msi/Configuration.ps1`,
`msi/tests/test-config.ps1`, `msi/README.md`, `docs/DEPENDENCIES.md`;
ProcSight `docs/DEPENDENCIES.md` and `docs/verification.md`.

**Interfaces:** `Read-MsiConfiguration(Path)` continues returning a validated
hashtable and dispatches schema validation by schemaVersion. V2 retains product
id/name/manufacturer, application, shortcuts and payload approval fields; adds
`product.productCode`, uses `product.upgradeCode` literally, and requires top-level
`architecture`, `components` (relative-path to GUID map), `installation.menuDirectoryName`,
`installation.scope` (`user`, `machine`, `choice`) and
`installation.restartManager` (`disabled`, `graceful`). `installation.directoryName`
is literal. Choice additionally requires `installation.defaultScope` (`user`, `machine`).

- [ ] Check live upstream release/commit, advisories and actual MIT/NOTICE terms;
  record the shared tools' complete external dependency surface and review limits.
- [ ] Run ProcSight's direct/transitive outdated, vulnerable and deprecated JSON
  reports with `dotnet list ProcSight.slnx package --outdated --include-transitive --format json`
  and equivalent `--vulnerable` / `--deprecated` commands. Review affected package
  license/notices and record retained-version reasons; resolve flagged findings explicitly.
- [ ] Add failing configuration tests for a valid explicit v2 product and rejection
  of missing identities, unknown fields, duplicate keys/paths/GUIDs, zero GUIDs,
  invalid architecture and scope/default combinations. Run `./msi/tests/test-config.ps1`.
- [ ] Implement v2 parsing/validation without changing the published v1 schema.
  Validate complete component-map coverage during payload enumeration.
- [ ] Rerun configuration checks and document the v1/v2 ownership contract.

## Task 2: Author and inspect configured identities, paths, policy and scope UI

**Files:** winbuildtools `msi/Identity.ps1`, `Payload.ps1`, `Database.ps1`,
`Authoring.ps1`, `Inspection.ps1`, `Maintenance.ps1`, new `Scope.ps1`,
`msi/package.ps1`, `msi/tests/test-msi.ps1`, `verify.ps1`, README/developer/
architecture/security/validation/changelog documents and `docs/provenance.json`.

**Interfaces:** V2 `Get-MsiPackageGuid` returns supplied product/upgrade/component
GUIDs for their supported scopes, with no application-specific derivation.
`Add-MsiScopeUi(Installer,Database,Configuration)` and
`Assert-MsiScopePolicy(Path,Snapshot,Configuration)` own native scope authoring and
inspection. Existing public package command/result fields remain available.

- [ ] Add failing synthetic authoring tests for two configured products with the
  same payload paths but different explicit identities/directories; exact GUIDs
  must survive unchanged. Include unchanged v1 golden compatibility inputs.
- [ ] Add failing native-table tests for machine, user and choice modes, both
  defaults, scope UI Continue/Cancel mappings, unattended properties and maintenance
  conditions. Choice initializes ALLUSERS=2 and sets/clears MSIINSTALLPERUSER.
- [ ] Implement explicit identity selection, literal install/menu names, generic
  scope controls before related-product discovery/costing, summary privilege bits
  and configurable graceful/disabled Restart Manager policy. Retain v1 behavior.
- [ ] Add real copied-MSI mutation cases for wrong directory names/parents, product
  metadata, launch conditions, upgrade ranges/action properties, component GUIDs,
  file versions, scope/summary flags and control ordering. Observe rejection tests
  fail against the old inspection before completing the comparisons.
- [ ] Extend read-only snapshots and inspection to compare the complete configured
  semantics before preflight/extraction. Preserve no-custom-action safeguards and
  bound all expected tables. Scope-specific preflight must use isolated identities,
  temporary targets and the explicit action allowlist, with RM disabled in copies.
- [ ] Run `./msi/tests/test-config.ps1`, `./msi/tests/test-msi.ps1` and shared
  verification; inspect x64 and ARM64 tables without claiming ARM64 execution.
  Update current provenance hashes while preserving original extraction hashes.
- [ ] Document unattended scope choices, existing-machine upgrade instructions,
  no scope migration, architecture collision responsibilities and manual ICE105/
  rendered-dialog/installed-lifecycle acceptance. Commit the coherent shared change.

## Task 3: Share bounded certificate validation and remove trust file reopening

**Files:** winbuildtools new `signing/Certificates.ps1`,
`signing/trust-dev-certificate.ps1`, `tests/test-signing.ps1`, and related
security/developer/architecture/validation/changelog documentation.

**Interfaces:** `Read-WbtPublicSigningCertificate(Path,ExpectedThumbprint)` returns
an owned X509Certificate2; caller disposes it. A separate store-import operation
accepts that object and never rereads its source path. The administrator-only
public trust command continues requiring an explicit expected thumbprint.

- [ ] Write failing store-free tests using in-memory generated certificates for
  oversized/invalid input, PFX disguised as CER, missing EKU, invalid dates and
  thumbprint mismatch. Test a replaced source file after validation: the import
  boundary must receive the original certificate object, with no real store write.
- [ ] Implement a bounded 1 MiB read, actual Cert content-type validation, dates,
  EKU and thumbprint checks. Import the validated object into TrustedPeople using
  explicit ShouldProcess; preserve administrator and store-target restrictions.
- [ ] Run `./tests/test-signing.ps1` and `./verify.ps1 -Integration`; use no real
  private key, signing service or trust mutation. Update documentation and commit.

## Task 4: Integrate the exact tools through a ProcSight product adapter

**Files:** ProcSight `.gitmodules`, `external/winbuildtools`, new
`scripts/packaging/ProcSight.ps1` and `packaging/msi.json`, existing package/common/
MSI/certificate/release scripts, affected script tests, `scripts/verify.ps1`,
`.github/workflows/windows-build.yml`, and required living documentation/notices.

**Interfaces:** The adapter resolves only `external/winbuildtools`, emits resolved
v2 configuration under the owned build tree, and preserves package arguments and
`Artifact`, `Manifest`, `Version`, `Commit` result fields. Product derivation stays
in the adapter, preserving `ProcSight.Msi/` legacy hashes and exact paths.

- [ ] Commit shared changes before pinning them. Add the public-URL submodule and
  select the reviewed full commit; no build-time fetch or sibling fallback.
- [ ] Add failing adapter tests for missing checkout, exact existing product/
  upgrade/component GUIDs, literal ProcSight folders, choice/user default,
  disabled RM, payload rejection and additive metadata/result compatibility.
- [ ] Delegate MSI authoring/inspection, MSIX inspection, SDK/process utilities,
  signing/verification and final metadata. Retain proven isolated WinUI publishing,
  product privilege/resource/license checks, CA trust policy and audited restores.
- [ ] Replace local certificate mechanics with shared calls while retaining the
  drop target, hidden UAC relaunch, thumbprint continuity and non-elevating WhatIf.
  Adapt synthetic release fixtures to provide the expected checkout/configuration.
- [ ] Remove copied generic helpers only after all callers/tests use the adapter.
  Set affected script requirements to PowerShell 7.4+ and initialize CI submodules
  recursively without broadening triggers or the write-enabled release job.
- [ ] Run all affected script suites and `scripts/verify.ps1`. Build/inspect unsigned
  MSI and Direct MSIX. Store packaging requires a valid synthetic separate fixture
  with major >=1; never change the real app version or use real Store credentials
  merely to satisfy that profile's preflight.
- [ ] Exercise Agenda's adapter and existing packaging regressions against the
  candidate shared commit in a temporary consumer checkout; do not change its pin.
  Record the precise checks and remaining lifecycle/signing/platform limits.
- [ ] Update Unreleased notes, guides, architecture Mermaid, dependency review,
  AGENTS state and MIT notices in the same implementation commit.

## Task 5: Add the running application version to Settings

**Files:** ProcSight `src/ProcSight.App/MainWindow.Settings.cs`,
`MainWindow.VisualFixture.cs`, `docs/WINUI3-DESIGN.md`, `CHANGELOG.md` and
`docs/verification.md`.

**Interface:** Footer text is `ProcSight <major.minor.patch>` from the running
ProcSight.App assembly informational version, excluding the commit suffix.

- [ ] Append native selectable secondary text after the final Settings card,
  using theme resources and an accessible name. No source-tree or MSIX-only lookup.
- [ ] Extend the existing synthetic Settings fixture to scroll to and verify the
  footer against build metadata; do not add a separate test that mirrors the text.
- [ ] Build and capture 1200/660 widths in Light/Dark with synthetic data only.
  Record that keyboard, Narrator, high contrast and scaling remain manual checks.
- [ ] Update the short Unreleased note and UI/verification guidance; commit.

## Task 6: Verify, review and prepare the requested Git release

**Files:** Both repositories' validation records; ProcSight release-owned version
inventory and changelog. Shared VERSION changes, if needed, must have their own
version decision and notes rather than copying the app's 0.3.0 version.

- [ ] Run shared `./verify.ps1 -Integration` after commits so its pinned clone
  exercises the new committed code. Run ProcSight `./scripts/verify.ps1`, native
  Settings fixtures and package inspections; resolve failures without weakening gates.
- [ ] Review the complete diffs, staged inventory, secrets/output exclusions,
  source provenance, documentation links and Mermaid rendering. Run unstaged/staged
  `git diff --check` in both repos. Obtain the whole-change review required by the
  execution workflow before calling the integration complete.
- [ ] Record exact verification, explicit dependency exceptions and manual scope/
  lifecycle/ICE/signing limits. Ensure both checkouts are clean and commits refer
  only to intended inputs. Test a fresh recursive ProcSight consumer checkout.
- [ ] Run `./scripts/prepare-release.ps1 -Version 0.3.0 -Profile Msi -DryRun`, then
  the real command. Verify synchronized versions, release commit, annotated tag,
  source commit and final MSI hash. Recheck the footer from a 0.3.0 build.
- [ ] Publish shared commits before consumer commits only within the user's Git
  publication authorization; never move an existing tag or push unrelated work.
  Clarify any unresolved remote-publication scope using concrete commits/artifacts.
  Do not dispatch Actions or publish a GitHub Release implicitly.
- [ ] Report local/pushed commits and tags, package path, test results and remaining
  manual acceptance accurately, without implying release readiness from fixtures.
