# Developer guide

## Current state

Run `scripts/generate-icons.ps1` on Windows to regenerate the tracked multi-resolution ICO and PNG
assets from `Assets/ProcSight.svg` using built-in GDI+. SVG rect/line/circle primitives are supported;
no network download or image dependency is required. Tray counters use English PDH paths, network
adapter deltas and existing CPU/memory samples. Do not infer per-process disk/GPU data from these
system-only tray samples.

Connection profiles separate provider kind from protected credential-slot IDs. Legacy preference
migration is in `ConnectionSettingsMigration`; it must preserve permissions and keep fallback off.
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
