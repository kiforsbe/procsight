# Changelog

Notable changes to ProcSight are recorded here. Entries under Unreleased describe work
in this repository; planned product features are documented in the design and roadmap.

## [Unreleased]

### Added

- Host — hosted child names for Python/PowerShell/Plex/Windows scripts, Python modules, Command Prompt
  batch/npx launchers, and VS Code extension workers, while retaining application grouping.
- Local Details includes the full available command line and identifying file/lifetime metadata.
- Local command collection is always enabled; separate online controls govern command sharing
  and identifying metadata. Learned rules retain local ownership scopes withheld from providers.
- Command-derived learned rules require keyed invocation scopes, preventing reuse across
  unrelated scripts sharing the same runtime and generic role. Legacy unscoped rules re-analyze.
- Expanded bundled knowledge to 373 portable process/service definitions with scoped IDs,
  searchable inspection, and publisher-only references that preserve unknown purpose.
- Signature/hash enrichment of configured service executables and DLLs, separate from host
  signatures; module replacement invalidates the service's knowledge identity.
- Reusable bounded local command rules for Python, .NET, PowerShell, browser workers, and
  language-server entry points, respecting the selected command-line privacy mode.
- Bundled machine-independent common-task catalog with source references and verified
  metadata gates, plus provisional local renderer/worker/service role labels.
- Windows Shell executable icons loaded asynchronously with a bounded in-memory cache.

### Fixed

- Native table scrollbars remain within the narrow-window viewport; horizontal scrolling
  keeps headers and rows aligned in both directions.
- Full-height column headers toggle ascending/descending sorting, display direction arrows,
  and persist preferences. Name sorting follows visible workload labels.
- Inline interpreter code is opaque to role matching; quoted command text is no longer
  mistaken for browser worker switches. .NET certificate/product metadata is recognized.
- Service arguments are excluded from serialized snapshots and shareable service identities.
- Live metric samples no longer continuously reorder rows or notify every cell. Sibling
  order and viewport anchors persist until explicit sorting/refresh; single-member groups
  retain their group identity as processes enter and exit.
- Native chevron buttons expand/collapse selected application groups repeatedly, independently
  of selection changes. Raw parent/child trees can also be collapsed.
- Generic catalog identities preserve service-specific and invocation-specific names.
- Common-rule matching handles actual certificate distinguished names and Windows `.mui`
  original-file metadata; conflicts and fresh corrections retain precedence.
- Name-only protected snapshots do not repeatedly consume automatic AI jobs as their
  unavailable-lifetime identities change; manual investigation remains available.

### Analysis foundation

- Persistent local knowledge and validated declarative rules for purpose, security, and
  optionality; scoped user corrections, rule inspection/disable/delete, and retention.
- OpenAI Responses and compatible/local analysis adapters, separately enabled web research,
  payload preview, protected API credentials, and persistent request limits.
- Command-line modes Off, Local only, and Include in online analysis, with origin filtering,
  bounded full-line payloads, and safe command-derived persistence.
- Local Release verification script and synthetic WinUI rendering fixtures.

### Changed

- Task Manager-style native table with compact single-line rows, title-bar search, resource
  totals, classification filters, and an optional adaptive details pane.
- Protected-process reads use nonthrowing native queries; unavailable metadata remains
  visible without repeated managed Access Denied probes.
- Ownership identities distinguish equal product names at different executable paths;
  generic Windows product metadata uses file descriptions when available.
- Older observations cannot restore superseded analysis permissions; expired local findings
  trigger reevaluation even while a process remains unchanged.

### Repository foundation

- Shared VS Code build/run tasks and a C# launch configuration for F5 debugging and
  Ctrl+F5 running, with extension recommendations and setup instructions.
- Native WinUI 3 shell, live read-only process/service collection, CPU and memory sampling,
  search, metadata details, initial ownership resolution, and local build/launch scripts.
- Initial tests for identity contracts, navigation, sampling, ownership, and Windows collection.
- Living root architecture reference with UML component/domain, sequence, and state views;
  developer and agent requirements to use UML liberally and maintain diagrams with changes.
- Design for a native Windows 11 process/service explorer with evidence-backed
  classifications, local reusable rules, automatic AI fallback, and optional online research.
- Implementation plan with local-only and online full-command-line analysis options.
- Roadmap for later process/service management and cleanup recommendations.
- GitHub publication preparation: README, developer guide, agent instructions, security
  policy, project ignore rules, and GitHub's standard GNU AGPL v3.0 license template.

### Known limitations

- The application is an unreleased development preview. Live provider integration and the
  full desktop accessibility/scaling matrix remain unverified. Disk/network/GPU measurements
  are not collected; inaccessible icon assets use a fallback symbol.
- No GitHub remote or private vulnerability-reporting channel is configured in this
  checkout. CI/CD workflows are intentionally excluded.
