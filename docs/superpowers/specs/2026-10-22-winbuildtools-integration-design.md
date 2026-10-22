# Shared Windows tooling integration and Settings version

Date: 2026-10-22. Status: proposed for written-spec review; not implemented.

## Intent and scope

Replace ProcSight's reusable local Windows packaging mechanisms with a pinned
checkout of <https://github.com/kiforsbe/winbuildtools.git>, following Agenda's
integration. Adapt and test winbuildtools where ProcSight exposes missing shared
capabilities, then consume the exact updated commit. After integration, display
the running application version at the bottom of Settings and prepare ProcSight
0.3.0 through its existing release workflow. The user requested the integration,
the Settings addition and a new release in Git.

The implementation spans the existing ProcSight and winbuildtools checkouts.
Agenda is a compatibility consumer: exercise its integration against the updated
tools without silently changing its committed tool pin or application version.
Preserve the shared VS Code files and unrelated edits in every repository.

## Current evidence

- ProcSight is on `main`, with central version 0.2.0 and an `origin` at
  <https://github.com/kiforsbe/procsight.git>.
- Published winbuildtools v0.3.0 resolves to
  `9a3bf8cf1d79d05946abbd020fe57ccec3d0ceb5`, also the inspected local HEAD.
  The tools are MIT licensed and do not add runtime package dependencies.
- Agenda consumes `external/winbuildtools` through a Git submodule and retains a
  product adapter, manifest/configuration, publishing and dependency policy.
- ProcSight's MSI GUIDs use SHA-256 over `ProcSight.Msi/` plus a lowercased scope,
  taking the first sixteen bytes directly as a GUID. Shared tools use another
  namespace/derivation, including UUID bit changes. Keeping only UpgradeCode is
  insufficient to preserve existing component identities.
- Shared authoring appends an architecture suffix to install/menu directories;
  ProcSight currently uses `Program Files/ProcSight` and a `ProcSight` menu folder.
- Shared v0.3.0 enables Restart Manager shutdown. ProcSight currently disables
  it and has no explicit Windows session-ending message handler.
- Shared certificate trust validates an object but imports by reopening its file.
  ProcSight's helper instead imports the already validated certificate object.

## Ownership and alternatives

Use Agenda's adapter boundary for all applicable mechanisms. An MSI-only switch
would leave duplicate signing, certificate, SDK and hash implementations. Replacing
all .NET orchestration at once would introduce unrelated multi-project, profile and
WinUI build-context risk. Neither alternative meets this migration's scope as well.

ProcSight keeps product/version/identity data, privilege and privacy policy,
profile-specific build inputs, WinUI resource preparation, approved payload and
license checks, dependency audit gates, appinstaller feed policy, release ordering,
artifact names and backward-compatible consumer metadata/result fields.

winbuildtools owns MSI/MSIX construction and inspection, certificate validation,
explicit key/trust operations, signing and signature verification, installed SDK
discovery, process diagnostics and final artifact hash/signature metadata.

Ordinary development build/test commands can remain solution-based. Packaging
must use shared process utilities where the generic single-project .NET helper
does not preserve ProcSight's proven isolated restore/build/publish context.
Document this compatibility exception instead of retaining generic helper copies.

## Shared compatibility additions

The user clarified the ownership boundary: all application identities and policy
must come from the integrating repository. Do not implement a ProcSight-specific
compatibility mode, recognize a product name, embed a product namespace or branch
on the consumer repository. The current shared engine does not contain ProcSight
product GUIDs: its incompatibility is the imposed derivation and folder-suffix
convention, despite taking the namespace from caller configuration.

Introduce a versioned, strict configuration contract for fully resolved,
consumer-owned installer inputs. The consuming adapter supplies ProductCode,
UpgradeCode and a complete mapping of normalized relative payload paths to
component GUIDs, as well as exact install/menu directory names and an explicit
Restart Manager policy. Shared authoring and inspection use those values directly;
neither may derive a different identity or append a folder suffix. Fresh PackageCode
and isolated-preflight GUIDs remain build/validation instance identities owned by
the mechanism, rather than persistent product/component identities.

The consumer owns any deterministic derivation needed to produce that resolved
configuration. ProcSight's adapter uses its existing derivation to preserve
product, upgrade and per-file component GUIDs for x64. Its checked-in inputs
declare its namespace and paths; build-specific resolved configuration lives
outside the payload. This is product compatibility data, not a local copy of MSI
authoring, signing or inspection. Shared deterministic primitives may be exposed
as utilities, but selection of their algorithm, scopes and inputs belongs to the
consumer and is never inferred from its product name.

Reject missing or extra component mappings, duplicate/colliding GUIDs, malformed
paths and incompatible architecture/product data before writing an artifact.
Internal MSI table identifiers can remain generic implementation details; they
must consistently refer to the supplied component GUIDs and approved paths.
Retain the published v1 contract as an explicitly documented compatibility entry
point for existing consumers until deliberately migrated; do not reinterpret its
identities or change Agenda's current installer ownership during this work. The
new contract has no hidden product-identity or directory-naming defaults.

ProcSight explicitly configures its existing disabled Restart Manager policy;
changing application session-ending behavior is a separate future change. Document
the retained close-before-servicing requirement. This avoids silently introducing
shutdown requests into a collector with asynchronous ETW cleanup requirements.

Extract shared public-certificate validation usable without elevation or store
mutation. Enforce a public-only `.cer`, bounded size (1 MiB), actual certificate
content type, current validity, explicit code-signing EKU and expected thumbprint
when provided. Trust imports that validated object into LocalMachine/TrustedPeople;
it must not reopen the input after validation. Keep Root and automatic trust outside
builds. The administrator-only shared command remains explicitly invoked.

ProcSight retains its Explorer drop target and UAC orchestration as a thin wrapper.
It calls shared validation before elevation and forwards the reviewed thumbprint;
the elevated invocation revalidates and delegates import to the shared command.
`WhatIf` and synthetic tests never elevate or modify certificate stores.

## ProcSight adapter and checkout

Add `external/winbuildtools` as an exact Git submodule with the public upstream URL.
Packaging must fail early with initialization instructions when it is absent or
incomplete. Never fall back to a sibling checkout, download tools during builds or
follow a moving branch. CI initializes the recorded submodule recursively with
credential persistence disabled; the separate write-enabled release job continues
to avoid checkout and execution of application/tool/artifact contents.

Move MSI product inputs into app-owned JSON and retain the public packaging profiles
`Msi`, `Direct` and `Store`. Preserve their existing arguments and final artifact
names. Remove copied generic implementations after all callers/tests delegate.
MSIX may continue to be emitted through the proven WinUI profile initially, with
shared exact inspection of the produced artifact; do not remove required resource
generation merely to route through a different packing entry point.

Shared final metadata is authoritative for SHA256, actual signature state and
signer. Compose the consumer's `Profile`, `PackageVersion` and legacy `Commit`
alias additively without overriding shared facts. Preserve the structured consumer
return fields consumed by release preparation and GitHub staging.

Preserve .NET Runtime 10.0.13+ within 10.0 and debugger support. Preserve forced,
profile-specific audited restore, all-severity vulnerability gates, lock files,
24H2/x64 requirements, privilege checks and bundled runtime/license materials.
Packaging checks remain non-installing, with isolated-identity MSI preflight through
InstallValidate, fixed binary version reading and exact payload inspection.
Shared commands require PowerShell 7.4+; align affected entry-point checks and docs.

## Settings version

Append a modest selectable TextBlock after the last Settings card, using native
secondary text/theme resources and an accessible label. Display
`ProcSight <major.minor.patch>` from the running application assembly's product
version, excluding its source-commit suffix. Do not hardcode 0.3.0 in UI code,
read the source tree or assume installed MSIX identity. Packaged and unpackaged
builds must show their own built version. Existing Settings behavior is unchanged.

## Validation and dependencies

Before accepting the tool dependency, check live upstream revisions/releases,
advisories, MIT license and notices, and its complete dependency surface. Run the
required outdated/vulnerable/deprecated direct/transitive NuGet reports and review
actual distribution licenses for the affected graph. Record dated results and
compatibility/security exceptions; do not treat an advisory scan as proof of safety
or silently broaden this migration into unrelated dependency upgrades.

Use synthetic fixtures for consumer-supplied GUID and directory continuity,
two differently configured products sharing identical payload paths, and changed
component mappings being rejected during inspection. Also test unchanged v1
Agenda identities, rejected configuration, certificate format/thumbprint/file
replacement behavior, missing submodules and metadata compatibility. Run shared
`verify.ps1 -Integration`, ProcSight `scripts/verify.ps1`, real unsigned MSI and
Direct/Store packaging where prerequisites permit, and Agenda adapter regressions
against the candidate shared commit. Never install products, mutate trust or use
real credentials as an automated check.

Capture synthetic Settings wide/narrow, light/dark views and verify the footer's
version against build metadata. Screenshots do not establish Narrator, high-contrast,
scaling or installed servicing acceptance. Check affected documentation links,
Mermaid syntax/rendering and `git diff --check` before claiming verification.

## Documentation and release

Update both repositories' changelogs and living architecture/developer/dependency/
verification documentation with implemented behavior and actual results. Preserve
ProcSight AGPL and shared MIT notices; distinguish tooling source terms from the
redistribution terms of payloads and Windows components.

Commit and validate shared changes before pinning their exact commit in ProcSight.
Keep the shared version decision separate from the application version. A consumer
commit must not be published with an unreachable tool gitlink. Publish upstream
tool commits before the ProcSight consumer commit if remote publication is requested.

Prepare ProcSight 0.3.0 using `scripts/prepare-release.ps1 -Version 0.3.0 -Profile Msi`
after implementation checks and a clean committed checkout. The script owns central
version synchronization, changelog dating, release verification, exact-commit MSI
creation and the annotated `v0.3.0` tag. Verify the resulting MSI/hash/source metadata
and Settings version. Do not independently bump manifests or rewrite existing tags.
Release preparation remains separate from GitHub Actions dispatch and public GitHub
Release publication; those require explicit requests. Record local versus pushed
commits/tags accurately in the final handoff.
