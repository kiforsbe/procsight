# Developer guide

## Current state

Resource shading now persists `MaximumOpacityPercent`, system/custom color selection and a validated
RGB hex value. The legacy `IntensityPercent` field remains readable but the direct roof replaces its
relative multiplier. Native fixtures check custom color on all resource columns, a shared CPU/Memory
roof, stable-palette notification isolation, debounced saves and rejection of partial opacity drafts.

`PerformanceTimeAxisTests` cover linear/logarithmic elapsed-time mapping, endpoints, monotonicity
and invalid times. The native `performance-log-time` fixture toggles the saved presentation mode,
checks logarithmic tick labels and verifies that the existing subsystem list stays attached.

The native `row-performance-checks` fixture also verifies the default 5% CPU/Memory tint, a disabled
floor and transparent unavailable readings, alongside notification and metadata-cache checks.

Resource monitoring uses TraceEvent 4.0.0 for one owned real-time kernel session in explicit
administrator mode. No ETL files are created. `ProcessIoAccumulator` tests cover elapsed rates,
PID reuse, lifecycle invalidation, event-loss restart and pruning; `NetworkRatesTests` cover
direction totals, adapter replacement, reset and invalid intervals. `SystemPerformanceTests`
samples local system counters without process inventory or AI. No credential access is needed.
`ProcessGpuTests` cover engine aggregation, missing samples and reused lifetimes. GPU uses a separate
off-thread PDH query; `SortTests` cover numeric GPU order and invariant export. The `process-io`
fixture also checks GPU bindings, new-layout columns and the process-page permission notice.
`ResourceHighlightTests` verify recency without poll refresh, missing samples, peak/spike/relative
modes and idle readings. Presentation preference tests cover shading round-trip and older files.
Native `resource-shading`, `resource-shading-settings` and `live-sort` fixtures use synthetic values
only. They verify shading/drag binding restoration, mode controls and an 83-row manual/automatic
sort transition with retained selection and no collection Reset on automatic reordering.
That fixture also checks native `Items.VectorChanged` during repeated bidirectional reordering:
managed Move can become native Reset even when `CollectionChanged` reports no Reset. Logical
relocations use remove/insert notifications to keep the rest of the list realized.

Native `--visual-fixture <absolute.png> Dark|Light 1200|660 <fixture>` modes include
`performance-overview`, `performance-navigation`, `performance-cpu`, `performance-memory`,
`performance-disks`, `performance-network`, `performance-gpu`, `performance-empty`,
`performance-unavailable` and `process-io`. These use synthetic data only. They exercise native
navigation, responsive tiles, graphs, details and process rate availability. `performance-navigation`
also checks that every subsystem switch retains the same sidebar items and compact selector and
synchronizes both navigation controls. Keep captures under
ignored `artifacts/performance-ui`. Elevated event delivery, end-to-end rate attribution, shutdown
session removal, Narrator, high contrast and scaling still require manual desktop acceptance.

SQLite restart regressions in `AnalysisIdentityPersistenceTests` cover rule-free provider responses,
metadata-only reuse across changed invocation/PID, private command scopes, partial Unknown axes,
changed binaries/paths, rejected proposals and older entry-only identity labels. Existing rule-free
entries are not globally migrated: Analyze again to generate an exact rule when their identity no
longer matches. Supported older entry-level names are restored for their original validated scope.

For local UI profiling, run `./scripts/measure-ui.ps1 -Label comparison`. It builds an optimized
Debug fixture app (retaining SDK bootstrap constants) and reports synthetic 500/1000-row projection,
updates, sorting, search, details, resizing, grouping, column motion and bundled-rule browsing.
Reports/captures go under ignored artifacts/ui-performance. `-Theme Light -Width 700` exercises a
different viewport; compare like-for-like runs without other builds. No live inventory, provider,
credential, clipboard or real preference access occurs. See [performance measurements](docs/PERFORMANCE.md).
The local rules harness is `dotnet run --project benchmarks/Rules -c Release`; it uses synthetic
SQLite data only. Allocation-regression tests complement timings; desktop accessibility and actual
pointer/frame-rate acceptance remain separate.

Table UI fixtures `columns`, `columns-services`, `row-menu`, `column-chooser` and `inventory-export`
exercise dynamic native templates, shared row/header widths, optional-column sorting, selection,
menus and native configuration dialogs. Column checks also cover invisible centered resize targets,
continuous column translation and restoration of resource-background bindings. `column-drag` captures
the actual full-column mid-drag rendering and checks header/body geometry without injecting pointer
input. Fixtures never launch the actual Save picker or modify the
real clipboard. The opt-in `columns-pointer` probe waits for real pointer reordering before a
`.finish` marker; `export-picker` waits for a synthetic CSV written through the real Save picker.
Keep those opt-in probes distinct from unattended fixtures. Export uses the [Windows App SDK Save picker](https://learn.microsoft.com/en-us/windows/apps/develop/files/pickers-save-file)
owned by AppWindow.Id; elevated picker acceptance still requires manual desktop testing.
CSV/TSV resource values are invariant percent/bytes/bytes-per-second; JSON retains text/null fields.
Application tests verify layout normalization, formula escaping and excluded context in derived names.

Native Debug visual fixtures `details-selection`, `details-service-selection` and `rules-selection`
exercise real ListView selection changes and expanded/collapsed detail choices, using synthetic
metadata only. The entity fixtures also cover empty selection, page recreation and updated item
identity. Run with `--visual-fixture <absolute.png> Dark|Light 1200|700 <fixture>`; check `.error`
and `.checks` beside the PNG. Existing `details-stability` and `details-evidence` cover refreshes.

`Version.props` is the authoritative product version. MSBuild derives assembly/file and MSIX versions
as `X.Y.Z.0`; dependencies remain pinned independently. Run `scripts/test-release.ps1` for synthetic
version/manifest/changelog checks. Local release orchestration and packaging are implemented; do not
manually bump individual product version fields or infer Store readiness from these tests.

## Local release preparation

Packaging, verification and release scripts require **PowerShell 7+ (`pwsh`)**. Run the commands
below inside pwsh; Windows PowerShell 5.1 is rejected before any script actions occur.

Start from a clean branch, with populated Unreleased notes and configured Git author/committer.
Use stable SemVer `MAJOR.MINOR.PATCH` and annotated `vMAJOR.MINOR.PATCH` tags. Components are capped
at 65534 for assembly compatibility; prereleases are currently rejected. Store requires major >=1.

```powershell
./scripts/prepare-release.ps1 -Bump Patch -Profile Direct -DryRun
./scripts/prepare-release.ps1 -Bump Patch -Profile Direct
# After a packaging failure, retain the prepared commit and reuse its exact version:
./scripts/prepare-release.ps1 -Resume -Version X.Y.Z -Profile Direct
```

Alternatively supply `-Version X.Y.Z`. The first release may use the current initial version.
The script synchronizes Version.props and both source manifests, dates the changelog, runs verify.ps1,
commits only that inventory, builds/inspects packages from the clean release commit, then creates the
annotated local tag. It validates artifact version, commit and SHA-256 before tagging. Release notes
and artifact metadata go into ignored artifacts/releases. Nothing is pushed or published.

Precommit failure restores unchanged script-owned edits while preserving intervening edits. A package
failure retains the prepared release commit without tagging; Resume requires that commit at HEAD.
Existing tags are never moved. Supply the same identity/signing/feed options when resuming. Public
Direct releases require explicit production identity, trusted signing and timestamping; unsigned
development output is only a build artifact. DryRun performs no edits, commits, tags or signing.
Synthetic workflow checks run only in disposable temporary repositories through
`scripts/test-release-workflow.ps1`; they do not create a release in this repository.

## MSIX packaging

Windows SDK 10.0.26100.0+ x64 MakeAppx/SignTool are discovered explicitly. Packaging uses the pinned
Microsoft.Windows.SDK.BuildTools.MSIX package and opt-in single-project profiles in packaging/.
Ordinary builds remain unpackaged; isolated packaging intermediates/output live in artifacts/packages.

```powershell
./scripts/test-packaging.ps1
./scripts/package.ps1 -Profile Direct
```

Unsigned development output is not directly installable. No packaging script imports trust or
installs/elevates an application.

### Install a signed development package

Create a signing certificate **once**, in ordinary PowerShell 7 as your development user:

```powershell
./scripts/new-package-certificate.ps1
```

The command creates a one-year, nonexportable code-signing key in `Cert:\CurrentUser\My`, with
subject `CN=ProcSight Development`, and exports its public certificate to the ignored
`artifacts/certificates/<thumbprint>.cer`. Record the printed thumbprint and reuse this certificate
for subsequent builds until it expires. Creation alone does not establish installation trust.

Sign a package as the same development user, replacing the placeholder with that thumbprint:

```powershell
$devThumbprint = 'YOUR_40_HEX_THUMBPRINT'
./scripts/package.ps1 -Profile Direct -CertificateThumbprint $devThumbprint
```

Use the final `Package inspected:` path ending in `-signed.msix`; each build has a separate output
directory under `artifacts/packages/Direct/<version>/<build-id>`. Before trusting a certificate,
check that its subject and thumbprint match the package signer:

```powershell
Get-PfxCertificate -FilePath 'C:\path\to\artifacts\certificates\YOUR_40_HEX_THUMBPRINT.cer' |
    Select-Object Subject, Thumbprint, NotAfter
(Get-AuthenticodeSignature -LiteralPath 'C:\path\to\ProcSight-VERSION-x64-signed.msix').SignerCertificate |
    Select-Object Subject, Thumbprint, NotAfter
```

For drag-and-drop setup, drop the matching `.cer` onto
[`scripts/trust-package-certificate.cmd`](scripts/trust-package-certificate.cmd) in File Explorer,
then approve the administrator prompt. Keep it next to `trust-package-certificate.ps1`; PowerShell 7
must be installed. The console stays open to show success or errors. This helper trusts the
certificate only; reopen the signed MSIX afterward to install the app.

The PowerShell helper also supports a command-line call and a store-free preview:

```powershell
./scripts/trust-package-certificate.ps1 -CertificatePath 'C:\path\to\development.cer' -WhatIf
./scripts/test-package-certificate.ps1
```

It accepts only a currently valid public `.cer` with an explicit code-signing EKU, rejecting private-key
containers and files over 1 MB. It displays subject, thumbprint and expiry, requests elevation only
for the explicit trust operation, then revalidates the certificate thumbprint across elevation.
Import uses the validated in-memory certificate, preventing a file replacement between validation
and import. Existing trust can be imported again safely. Validation does not prove who supplied
the certificate: verify the signer match and trust its source before dropping it.
Synthetic helper tests create certificates in memory, not certificate stores; `-WhatIf` never elevates
or imports. Actual Explorer drag/drop, UAC cancellation and import still require desktop acceptance.

For manual setup, open **PowerShell as Administrator** and import only the matching public `.cer`:

```powershell
Import-Certificate -FilePath 'C:\path\to\artifacts\certificates\YOUR_40_HEX_THUMBPRINT.cer' -CertStoreLocation 'Cert:\LocalMachine\TrustedPeople'
```

This is machine-wide trust; use only a verified certificate you intend to trust for development.
Use **Trusted People**, not Trusted Root Certification Authorities. The GUI equivalent is opening the
`.cer`, choosing **Install Certificate > Local Machine > Place all certificates in the following
store > Trusted People**, then completing the wizard. Test machines need only the public `.cer`,
never your private signing key. See [Microsoft's MSIX signing guide](https://learn.microsoft.com/en-us/windows/msix/package/sign-msix-package-guide).

Return to ordinary PowerShell as the intended app user and install the signed package:

```powershell
Add-AppxPackage -Path 'C:\path\to\ProcSight-VERSION-x64-signed.msix'
```

Alternatively double-click that package in Windows App Installer. If elevation used another account,
keep signing and app installation in the original development user's session: signing uses their
Personal/My store, and installation targets the invoking user.

Error `0x800B0109`, possibly wrapped in `0x87e00034`, indicates missing certificate trust. Confirm the
exact package signer's certificate is present in `Cert:\LocalMachine\TrustedPeople`, is unexpired,
and that you selected the signed artifact. Importing into Current User or Personal/My alone is not
this trust step; Developer Mode does not replace it. If you generate a new certificate, trust its
new public `.cer` before installing packages signed with it. A successful PowerShell signature check
alone does not establish MSIX deployment trust.

After development testing, remove only this certificate's trust entry in elevated PowerShell:

```powershell
Remove-Item -LiteralPath 'Cert:\LocalMachine\TrustedPeople\YOUR_40_HEX_THUMBPRINT'
```

Development certificates are for local testing, not public distribution. Certificate trust is an
explicit operation through the helper or manual import; packaging never invokes it or installs the app.

### Store and public distribution

For Store preparation, copy packaging/store-identity.example.json to an ignored *.local.json and
replace every placeholder with exact Partner Center identity values. Store major versions must be >=1;
use the release workflow for version changes. Choose -ElevationMode Disabled unless the publisher has
received approval for allowElevation. Microsoft signs accepted Store submissions; this command does
not publish or certify a package.

Public Direct packages require a production IdentityFile, a trusted CA-issued code-signing certificate
and explicit HTTPS TimestampUri. Optional FeedUri and PackageUri must both be supplied; generated
.appinstaller files check on launch without blocking activation. No feed is hosted automatically.
Each output includes release-artifacts.json with source commit, version, primary package hash and
signing state. Inspect tool-output.log for local build diagnostics. Source/binary packaging preserves
ProcSight's AGPL and collects available dependency/runtime licence and notice files under Licenses.

Packaging tests use synthetic identities/certificate metadata and do not change real certificate
stores. Actual install/update/uninstall, signed-package trust, OAuth return and Store certification
need separate disposable-system acceptance. A missing optional PDB conversion tool can cause an SDK
warning about symbol packages; debug symbols are excluded and no symbols package is distributed.

Installed packages use `%LOCALAPPDATA%\Packages\<family>\LocalState\ProcSight` and family-scoped
Credential Manager targets. They do not import the unpackaged `%LOCALAPPDATA%\ProcSight` state.
Same-family updates keep that namespace; uninstall/reinstall and credential retention require actual
Windows lifecycle testing. Restart as administrator is disabled if package permission cannot be
verified or allowElevation is absent. This does not certify Store eligibility.

Run `scripts/generate-icons.ps1` on Windows to regenerate the tracked multi-resolution ICO and PNG
assets from `Assets/ProcSight.svg` using built-in GDI+. SVG rect/line/circle primitives are supported;
no network download or image dependency is required. Tray counters use English PDH paths, network
adapter deltas and existing CPU/memory samples. Do not infer per-process disk/GPU data from these
system-only tray samples.

Connection profiles separate provider kind from protected credential-slot IDs. Legacy preference
migration is in `ConnectionSettingsMigration`; it must preserve permissions and keep fallback off.
Profile key slots hold a protected JSON envelope binding provider kind and endpoint to the secret.
Do not accept raw/unbound profile key records or arbitrary credential namespace references.
Use synthetic credentials in migration tests; never commit exported settings or credential data.

The [ChatGPT integration design](docs/superpowers/specs/2026-10-14-chatgpt-plan-design.md) and
[plan](docs/superpowers/plans/2026-10-14-chatgpt-plan.md) document the direct open-source OAuth adapter.
Authentication owns a temporary IPv4 loopback callback, JWKS validation, protected account/token records,
and renewable sessions; `ChatGptPlanProvider` uses the public Responses SSE API. No client secret,
API key, Node helper, hosted login backend, or additional dependency is needed for this profile.
Tests use synthetic signed tokens, fake HTTP, in-memory protected storage, and real loopback callbacks.
Never inspect or copy another application's credentials to validate this integration.
The Debug `--visual-fixture <absolute.png> Dark 1000 settings` option renders actual Settings using
in-memory credentials, without real account reads or network traffic.
`settings-connection-chatgpt` uses an in-memory account and delayed fake model catalog, detaches and
reattaches the account panel, and asserts a populated enabled picker with the configured model selected.
Connection fixtures also assert that controls fit their actual dialog viewport. `rules-rejected` and
`rules-unknown` exercise prominent decision diagnostics and research outcomes with synthetic records.
ChatGPT research uses the public Responses web-search tool in the analysis request, with validated
source-URL aliases mapped to internal evidence IDs. Keep `max_tool_calls` and `max_output_tokens` out
of plan requests; the documented plan route rejects them. API-key requests may set these fields.
The local allowance controls admission, not a provider-side spending cap; do not imply otherwise.
Use `settings-admin` for simulated elevated-session UI or `settings-tray` for tray preferences;
these fixtures do not elevate, invoke UAC, create a tray icon or read real accounts.
`settings-connection` and `settings-connection-api` invoke the real Configure action through UI
Automation and capture the native local/API configuration dialog. The settings fixture asserts that
provider fields are absent from the main page and that Configure/Test/Remove actions exist.
Use `activity` or `activity-details` as the fixture's final argument to render the AI activity page
with synthetic requests/results and an optional selected result. The coordinator's optional
`AnalysisActivityLog` records actual research/provider attempts; keep it isolated from analysis failures.
Provider failures carry typed rejection reasons into app-owned cause/recovery text. Never display or
retain arbitrary exception messages, rejected output, or evidence ID values to explain a failure.

ProcSight is an unreleased development preview. The native collector/UI, identity resolution,
privacy projection, rules, SQLite knowledge, provider/research adapters, and coordinator exist.
No CI/CD workflows exist. [Verification](docs/verification.md) separates automated coverage
from remaining live-provider and desktop checks.

```powershell
./scripts/build.ps1
./scripts/start.ps1
./scripts/verify.ps1
dotnet test tests/ProcSight.Core.Tests
dotnet test tests/ProcSight.Application.Tests
dotnet test tests/ProcSight.Infrastructure.Tests
dotnet test tests/ProcSight.Windows.Tests
```

Read the [design](docs/superpowers/specs/2026-10-13-procsight-design.md) and
[implementation plan](docs/superpowers/plans/2026-10-13-procsight.md) before implementing.
The plan describes future files and commands; their presence in the plan does not mean
they already exist.

## Intended toolchain

The plan targets Windows 11 x64, C#/.NET 10, WinUI 3, and Windows SDK 10.0.26100.0.
The locally inspected baseline is .NET SDK 10.0.301, WinUI package 2.2.1, Windows App SDK
Runtime package 2.2.0, and Windows SDK BuildTools 10.0.26100.4654.

Implementation must confirm the actual Visual Studio/MSBuild and runtime prerequisites,
pin dependencies and SDK configuration, and document a working unpackaged build/launch
path. The current build uses dotnet/MSBuild and the unpackaged Windows App Runtime bootstrapper.
Keep the build/launch commands above verified as the project evolves.

## Run and debug in VS Code

Open the repository root folder in VS Code on Windows 11 x64, with the toolchain
above installed. Install the recommended **C#** extension (`ms-dotnettools.csharp`)
from Microsoft; accept workspace trust for this checkout to enable tasks and debugging.

- Select **ProcSight (Debug)** in Run and Debug (`Ctrl+Shift+D`). **F5** builds the
  solution in Debug configuration, then launches the native app with the managed C#
  debugger. Set breakpoints in C# source before launching; **Shift+F5** stops debugging.
- **Ctrl+F5** uses the same configuration to build and run without the debugger.
- **Ctrl+Shift+B** runs the default **ProcSight: build** task.
- **Terminal > Run Task > ProcSight: run** builds and launches through the existing
  scripts without requiring a debug session.

[launch.json](.vscode/launch.json) launches the unpackaged `.exe` directly so the
Windows App Runtime bootstrapper runs. [tasks.json](.vscode/tasks.json) reuses
[build.ps1](scripts/build.ps1) and [start.ps1](scripts/start.ps1). Tasks use Windows
PowerShell with a process-scoped execution-policy override; no machine policy is changed.
See the [official C# debugger settings](https://code.visualstudio.com/docs/csharp/debugger-settings)
for launch configuration options.

If a build fails, inspect the task output and fix the reported errors before launching.
Close any running ProcSight instance before rebuilding if its executable is locked.
If VS Code reports that `coreclr` is unsupported, install or enable Microsoft's C#
extension and reload the window. A missing Windows App Runtime must be installed
before the unpackaged app can start. This configuration provides managed C# debugging;
native/XAML tooling still requires the appropriate Visual Studio tools.

The shared launch, task, and extension recommendation files are tracked; personal
VS Code settings and other editor state remain ignored. Update the launch executable
path if the app's target framework or runtime identifier changes.

## Intended boundaries

The living [architecture reference](ARCHITECTURE.md) explains the intended components,
identity/evidence relationships, analysis interactions, rule lifecycle, and privacy routing
with UML diagrams. Use UML liberally: maintain editable Mermaid class, sequence, and state
views, and add activity/deployment views when they clarify new boundaries. Update diagrams
and architecture prose in the same commit as relevant code changes; verify syntax/rendering
and label planned versus implemented behavior accurately.

The plan separates Core domain models/rules/privacy, Application orchestration, Windows
collection/credential storage, Infrastructure persistence/provider adapters, and the
WinUI App. Domain tests must not require WinUI initialization. Collection is read-only
and ordinary-privilege by default, with explicit full-app administrator mode; no process/service
management actions belong in the first release. The manifest stays asInvoker/uiAccess=false.
Elevated sessions use separate privacy consent and current-context credentials only. See SECURITY
before testing elevation from an unsigned user-writable development checkout.

Run collection and analysis away from the UI thread. Keep unavailable facts explicit.
Do not derive identity from a name alone, trust metadata company names as verified
signers, or attach old findings to a reused PID. Generated rules are validated declarative
data, never executable code.

## Contributions and verification

The embedded [common catalog](src/ProcSight.Core/Rules/common-tasks.json) contains shared
process/service definitions, not a machine inventory. Extend it with public file/product/service
identifiers and reference links; never retain machine paths, per-user instance suffixes, command
arguments, hashes, state, or PIDs. Product variants and service scopes need distinct IDs.

[WindowMetadataReader](src/ProcSight.Windows/Collection/WindowMetadataReader.cs) enumerates visible
non-tool desktop windows off-thread, caps captions/classes and per-process counts, and checks
known creation times before attaching observations. Tests create/destroy their own off-screen
synthetic native window; an unresponsive own-window test verifies bounded caption reads.
Window titles/classes must never become provider or knowledge fields.
Missing product/original-file evidence permits only an explicit publisher reference with unknown
purpose. Test wrong signers, unsigned files, and service host/module separation when adding gates.
The [local command matcher](src/ProcSight.Core/Rules/LocalCommandRules.cs) uses bounded tokenization
and fixed declarative tables. It never interprets script contents or decodes PowerShell payloads.

Keep changes focused and describe their purpose, validation, and limitations. Use synthetic
process/service fixtures with no personal paths, credentials, or real command-line dumps.
Real API keys and machine metadata must remain outside the repository.

Run `scripts/verify.ps1 -Configuration Release` for locked restore, native build, all four
test projects (including Windows integration), and `git diff --check`. Record exact commands
and results, and maintain documented local build/test scripts. Automated checks use fake
providers; real provider tests are explicit opt-in actions with synthetic data.

The Debug executable accepts `--visual-fixture <absolute-png-path> [Dark|Light] [width] [details|groups]`.
It renders the actual XAML tree using synthetic data, without collection, preferences,
credential access, or provider calls, then exits. It may read a standard Windows icon asset.

The additional `scroll` fixture mode creates enough synthetic workloads to overflow both axes.
The `groups` fixture also removes siblings and verifies that the selected child becomes a
standalone process row with its PID, no group header, and no expander before restoring the group.
At a 660px width it checks vertical scrollbar bounds/stationary position, header button height,
and ascending/descending selection. For example, pass `Dark 660 scroll` after the output path.
Local Details/workload labels always use available command lines; policy controls transmission.
The serialized Off/LocalOnly modes exclude online commands, and ShareIdentifyingMetadata separately
controls path/package/ownership/service fields. Maintain local rule scope when withholding fields.
The `groups` fixture selects a group and exercises expansion/collapse/re-expansion without
reselection, writing an ignored `.checks` companion. This mode is absent from Release builds.
It captures content; native caption buttons and Mica composition are outside that bitmap.
Use an ignored scratch path for temporary captures and deliberately review images before
tracking them. See [Microsoft's rendering API](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.media.imaging.rendertargetbitmap.renderasync)
for capture limitations.

The UI follows Task Manager's compact table and navigation patterns. Details adapt to the
window width. Keep slow collection, hashing, database work, and HTTP off the dispatcher;
presentation projections are portable and tested without WinUI initialization.

`KnownTaskCatalog` contains shared, versioned-in-source purpose references. Add exact publisher
and product/original-file identity gates plus a primary source for new entries. Test real
certificate-subject formats and localized Windows metadata. Never infer security or optionality
from a recognized executable name. Keep rule conflicts and fresh corrections authoritative.
`ProcessRoleLabels` only creates local display labels; do not serialize them into shared facts.

CI/CD workflows are intentionally excluded from this repository setup. Do not add GitHub
Actions, deployment pipelines, or automatic publication without a subsequent user request.

## Keep the repository current

- Update [CHANGELOG.md](CHANGELOG.md) under Unreleased for user-visible changes and
  meaningful project/tooling changes. Do not invent release versions or dates.
- Update [README.md](README.md) when status, features, setup, roadmap, or licensing changes.
- Update this guide when prerequisites, layout, commands, verification, or contribution
  practices change.
- Update [AGENTS.md](AGENTS.md) when architecture constraints, agent responsibilities,
  maintenance rules, or verification expectations change.
- Update [ARCHITECTURE.md](ARCHITECTURE.md) and its UML diagrams when components, interfaces,
  relationships, flows, lifecycle, storage, or trust boundaries change.
- Update [.gitignore](.gitignore) when new tooling creates local/generated files; keep
  sources, assets, package lock files, and sanitized fixtures trackable.
- Update [SECURITY.md](SECURITY.md) when supported releases, reporting routes, or security
  boundaries change. Document third-party notices when dependencies are introduced.

Dated design/plan documents are historical records. Material design revisions after
implementation begins should use a new dated document; living guides reflect current code.

## GitHub publication checklist

Before the initial push, review staged files and commit history for credentials and personal
metadata, and confirm the license and links. Configure the intended GitHub remote only when
the repository URL is known. Set up private vulnerability reporting in the published
repository's Security settings if available, or document a real maintainer contact in
SECURITY.md before distributing executable releases. No reporting address is invented here.

This preparation does not create a GitHub repository, configure a remote, or publish it.

## Licensing

Project contributions are made under the [GNU AGPL v3.0](LICENSE) used by this repository.
Preserve applicable copyright and license notices. Review compatibility and distribution
notice requirements before adding third-party code or dependencies.

## Rule and restricted-metadata inspection

`RuleInspection` owns readable rule/evidence presentation; `MainWindow.Rules.cs` owns the native
library and process dialogs. `LocalAnalysis.AppliedRuleIds` records contributing assessments,
while `MatchingRuleIds` also includes eligible matches that did not contribute. Legacy applied
IDs and provider provenance can be absent. New provenance comes from coordinator settings,
never provider-supplied identity. SQLite's existing JSON payload stores the optional fields.

Debug visual fixtures accept `rules` and `rules-process` for synthetic library and process views.
`RuleContributionTests`, `RuleInspectionTests`, and `RuleProvenanceTests` cover attribution and
legacy persistence. `MetadataAccessTests` and `MetadataAvailabilityTests` cover restricted reads,
partial records, safe reasons, and local-only collection diagnostics. Do not use machine dumps.

## Windows 11 UI conventions

[WINUI3-DESIGN.md](docs/WINUI3-DESIGN.md) synthesizes Microsoft's native guidance and documents
ProcSight choices. Apply it when changing the UI. Rules use two-line selectable rows and adaptive
list/details panes, with maintenance in CommandBar overflow. `MainWindow.RulePresentation.cs`
builds structured shared content for both the library and process inspection dialogs. Native
Debug `rules-detail` fixtures select a synthetic learned rule; `rules` shows the unselected list.
Metadata-derived names are descriptive; hash predicates still govern a file-specific rule.

`rules-actions` is a synthetic native automation-peer fixture covering selection refresh,
state changes, delete/cancel confirmation, source filtering and search/reset without provider calls.

## ChatGPT sign-in diagnostics

When diagnosing a manual sign-in, inspect `diagnostics/chatgpt-auth.log` under ProcSight's data
folder: `%LOCALAPPDATA%\ProcSight` for unpackaged runs or
`%LOCALAPPDATA%\Packages\<family>\LocalState\ProcSight` for installed MSIX runs. Only read this
diagnostic file, not the account catalog, Credential Manager records or browser authorization URLs.
The user performs real sign-in; do not automate authentication or inspect credentials to reproduce it.

Records contain UTC time, process ID, app-owned stage/outcome enums, optional HTTP status, account/model
counts and numeric HRESULTs. Follow browser launch, callback, token exchange, identity validation,
session/catalog writes and dialog/model updates to locate the last completed stage. The logger never
accepts free text, tokens, codes, state/nonce/PKCE values, account IDs, emails, paths or HTTP bodies.
It writes off-thread through a bounded 256-event queue and rotates at 128 KiB to one `.previous`
file. Logging failures do not change authentication. A fatal UI exception synchronously records
only its numeric HRESULT without suppressing the crash. Abrupt termination or a full queue can lose
pending entries. Debug visual fixtures redirect these logs alongside their synthetic captures.
This is operational authentication diagnostics, separate from memory-only AI activity history.

For manual debugging against an installed package's identity and settings, Windows supports
`Invoke-CommandInDesktopPackage -PackageFamilyName '<installed-family>' -AppId 'App' -Command '<absolute-built-ProcSight.App.exe-path>'`.
This launches a diagnostic executable without reinstalling or clearing the package. Its process token
is similar to, but not identical to, normal activation; it does not establish full installed-package
acceptance. See [Microsoft's debugging command](https://learn.microsoft.com/en-us/powershell/module/appx/invoke-commandindesktoppackage).

Connection editors belong to one dialog instance. Closing deferral flushes valid settings, then
explicit owner cleanup cancels account/model work and detaches the editor. Transient WinUI Unloaded
notifications do not end the editor lifetime or disable still-visible account controls.

## Structured process details


`ProcessDetails.Sections` produces transient, locally displayed field groups. Field values are
JSON-ignored and diagnostic strings omit them; this is not a persistence or provider DTO.
`MainWindow.ProcessDetails.cs` owns the native pane, compares display inputs without resource
samples, and preserves top-level expansion/scroll state for the same selection/lifetime. Evidence
loads run off-thread and require matching selection plus pane generation before applying results.
The legacy text formatter remains for existing callers and tests and shares safe unavailable reasons.
Debug fixtures `details`, `details-command`, and `details-stability` capture the native pane;
`details-stability` asserts preservation across resource and actual metadata updates.
`details-evidence` additionally checks nested evidence expansion/scroll and reopening. Presentation
state follows ProcessKey or service name; analysis identity changes invalidate evidence scope while
retaining the lifetime's presentation state. Scroll restoration is bounded by available content.

The Debug `layout` fixture starts with an empty table, then populates a long synthetic list and
checks every header boundary/width against realized row cells. It also checks title/action vertical
centers, narrow resize, horizontal scrolling and wide restore. Use it with light and dark themes;
`scroll` separately verifies vertical scrollbar position, header hit height and sort direction.

## Restricted process lifetime and service icons

`KernelProcessLifetimeReader` dynamically resolves NtQuerySystemInformation and optionally reads
SystemBasicProcessInformation (252 in the installed Windows SDK). It bounds native allocations,
entry counts, offsets and string pointers. `ProcessLifetimeTracker` maps session/PID/kernel sequence
to a stable opaque ProcessKey scope and retains previously observed creation time only for the
same confirmed sequence. Failed final confirmation excludes rows; unsupported enumeration falls
back to Toolhelp and existing snapshot scopes. The process-name-only automatic-analysis gate remains;
manual analysis can finish against limited stable evidence without creating name-only reusable rules.

`ProcessIconSource` selects process executable or configured service implementation/host metadata;
QueueIcon and row invalidation share its key. Tests and native `services` fixtures cover real icon and
16px gear fallback presentation. `details-restricted` asserts a denied process stays selected and keeps
its details controls across collection. Windows lifetime tests also perform bounded current-kernel and
live collector checks; do not commit a machine dump or raw inventory to demonstrate those checks.
