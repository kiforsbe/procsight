# Shared tooling integration validation

Integration work began 2026-10-22; native/package checks continued 2026-10-23 on
Windows x64. This record distinguishes source/package consistency checks from
installed servicing, signing and desktop acceptance.

## Pinned source and ownership

ProcSight consumes winbuildtools commit
`9949d5cd95f426611ec137522d1ee76b4889677c` at `external/winbuildtools`.
Its implementation is commit `12c5e5f95b6135f47457e68608d8beeeab9a9872`;
the follow-up changes only the shared validation record. Shared VERSION remains
0.3.0 and the new behavior is recorded under its Unreleased changelog. The gitlink,
not that unchanged version string or a moving branch, selects the actual tools.

The [product adapter](../scripts/packaging/ProcSight.ps1) keeps ProcSight's identity
derivation, approved-file policy and release fields. Shared tools own installer
construction/inspection, signing/certificate mechanisms, SDK/process helpers and
final measured hash/signature metadata. Ordinary solution orchestration and the
isolated WinUI profile context remain consumer-owned compatibility exceptions.

The [dependency review](winbuildtools-dependency-review-2026-10-22.md) records the
live authoritative review, actual licenses, SQLite 3.54.1 update, benchmark lock
refresh and host Git/PowerShell servicing findings. Those host findings were not
silently resolved or represented as absent; no host software was changed.

## Automated and synthetic results

- Shared `verify.ps1 -Integration` passed on the committed implementation: 49
  configuration checks, 157 v1 and 151 v2 native MSI checks, common/MSIX/signing,
  managed restore/build/publish and fresh pinned recursive consumer coverage.
  Shared fresh review found no substantive remaining issues. See the pinned
  [shared validation record](../external/winbuildtools/docs/VALIDATION.md).
- ProcSight `scripts/verify.ps1` passed locked restore, Release build, all script
  suites and all four test projects: Core 74, Application 167, Infrastructure 144,
  Windows 69 (454 total, no failures). Build output included 259 xUnit analyzer
  warnings in existing test sources and zero errors; this was not a warning-free
  build. Full logs remain under ignored `.superpowers/sdd/`.
- Adapter checks passed (25), covering missing checkout, legacy GUID/folder continuity,
  exact approved payload paths including extensionless runtime licenses,
  forbidden metadata, authoritative SDK-map staging and unchanged final metadata.
  A real Git/submodule fixture proves `ignore=all` hides changed tool source from
  plain status while the explicit provenance query still reports it dirty.
- Store-free certificate-wrapper checks passed (11), including UAC-free trust
  WhatIf and a guarded development-key WhatIf. No key/trust entry was created.
  Reads without an expected thumbprint are valid; import requires the reviewed
  exact thumbprint and passes the validated object, never a reopened file.
- Native ProcSight MSI consumer checks passed (12), preserving the historical
  product/upgrade/executable component GUIDs, exact install/menu names, 24H2 floor,
  disabled Restart Manager and Just me / All users choice. Product registrations
  were compared before/after; no product was installed or removed.
- An isolated Agenda checkout passed its installer-adapter and packaging-delegation
  suites against the candidate shared implementation. Only the temporary fixture's
  old hardcoded pin assertion was changed to name the candidate. The real Agenda
  checkout, app version and recorded tool pin were untouched.
- A fresh ProcSight recursive clone passed full verification (454 project tests)
  with its recorded tool pin. After upstream publication, another fresh clone used
  the canonical HTTPS tool URL without a local override and passed all 25 adapter
  and 12 native consumer checks at exactly the published commit. This host's Git
  repeatedly reported a `.git` pointer write permission error during initial
  submodule setup; its automatic retry completed checkout and the checked pin/tests
  passed. That host behavior remains unresolved and is not hidden as a clean log.
- The [Settings footer record](settings-version-validation.md) covers four
  synthetic light/dark wide/narrow views and retains the 10.0.13 runtime floor.
- Workflow actionlint and all 23 local PowerShell parsers passed. The affected
  relative documentation targets resolved. All 36 living-architecture/current
  connection-design Mermaid diagrams parsed and rendered with the already installed
  local Mermaid/browser tooling; the new ownership sequence was visually inspected.
  One existing certificate-sequence semicolon was changed to a comma to fix parsing.
- Final independent consumer review found no remaining actionable code blockers
  after the submodule-provenance guard and certificate-disposal corrections.

## Actual package checks

Direct MSIX creation passed with the existing isolated WinUI build and the new
shared exact inspection. Expected files and the generated manifest were staged
from the SDK package map before inspection, not copied from the unpacked artifact.
The resulting unsigned development package is not directly installable without
separate appropriate signing/trust. No signing or installation was performed.

Store-profile build and shared exact inspection also passed in an isolated clone,
with synthetic identity, version 1.0.0 and elevation disabled. The first long-path
fixture failed in the WinUI compiler while writing XamlSaveStateFile.xml; an
otherwise equivalent shorter fixture succeeded. The real app version and identity
were not changed for this check. No Store API, upload, signing key or certification
was used; Store packaging remains preparation only.

The first full MSI build exposed an extensionless runtime LICENSE omitted from the
shared allowlist. The adapter now supplies every already approved relative path
explicitly and no extension-wide allowances; a synthetic regression was observed
failing before this correction and passing afterward. The corrected full MSI build
passed shared full payload/table inspection and isolated native preflight through
InstallValidate. Its runtimeconfig includes Microsoft.NETCore.App 10.0.13,
preserving the required COM-wrapper runtime floor.

Integration package builds describe a modified development checkout and correctly
record SourceDirty=true. Release preparation separately requires a clean checkout,
including submodules despite user/global ignore settings, and measures the final
release artifact after any requested signing.

## Limits

No automated check installs/removes/repairs/upgrades ProcSight, imports certificate
trust, uses a real signing key, signs into an account, requests elevation or stops
a running application. MSI installed user/machine lifecycle, actual scope dialog
interaction, standard-user versus alternate-account UAC, cross-context coexistence,
ICE/ICE105 and native ARM64 acceptance remain manual. Scope selection is not a
migration; choose All users to upgrade an existing machine installation.

Keyboard, Narrator, high contrast and display/text scaling remain separate from
the synthetic footer captures. Shared source tests and clean advisory reports do
not establish executable safety, license clearance or release readiness.
