# Changelog

Notable changes to ProcSight are recorded here. Entries under Unreleased describe work
in this repository; planned product features are documented in the design and roadmap.

## [Unreleased]

### Added

- Local SemVer release preparation synchronizes live versions and dated changelog, verifies source,
  commits release files, builds inspected artifacts from that commit and creates an annotated local
  vMAJOR.MINOR.PATCH tag. Includes dry-run, failure rollback and resume; remote publication is separate.

- Package-aware administrator restart gating and separate package-family data/credential namespaces,
  preventing implicit reuse of unpackaged settings or OAuth sessions. Unknown package inspection
  disables restart/credential access; ordinary unpackaged behavior is preserved.

- Opt-in Direct/Store MSIX profiles, native package branding, isolated local packaging, package-content
  inspection, dependency/runtime notices, explicit certificate/signing commands and optional HTTPS
  App Installer update feeds. Direct self-contained unsigned package generation has been checked;
  installation/trust and Store acceptance remain separate unverified steps.

- Central product version properties and scoped version/changelog validation helpers, separate from
  dependency pins and historical documentation.

- Web search inside ChatGPT-plan/OpenAI analysis requests, with attributable source evidence,
  explicit research outcomes and no silent change of billing connection. Search-enabled requests
  exclude private command/path context. A different explicitly chosen research connection still
  uses a separate research step. Request allowances are not monetary or strict ChatGPT tool caps.
- Prominent recorded rejection causes and recovery guidance in rule details; legacy rules explicitly
  distinguish missing historical diagnostics from current evidence validation. Fixed false rejection
  from case/path-root comparison and validation before private invocation scope was attached.
- Responsive ChatGPT configuration without clipped nested content. Model discovery recovers after
  dialog reattachment, rejects stale results and exposes loading, timeout and retry feedback.

- Restructured connection management into a compact native toolbar, selectable priority list and
  separate fallback row. Add/Configure opens a per-connection dialog; Remove deletes profiles from
  routing without signing out shared accounts. Routine success messages no longer occupy settings rows.

- Hardened overlapping settings/key edits, session-consent cancellation of connection tests,
  immediate request-limit reductions, persistent per-connection authentication pauses and tray
  recovery after failed shell updates, following independent code review.

- Explicit administrator restart with UAC cancellation/startup recovery, an Administrator title,
  and separate session-only online-analysis/command/identifying consent. Default startup remains
  unelevated and collection stays read-only; documented full-app elevation security risks.

- Reproducible process-chip/magnifier app icon and optional live system-tray graph with CPU, memory,
  disk busy, network throughput or busiest GPU engine selection, explicit unavailable states,
  restoration/menu actions and opt-in minimize-to-tray behavior.

- Immediate settings with inline validation, independent selectable connection configuration,
  enable/disable and priority controls, and a selected-connection synthetic test action. Removed
  the Save button; incomplete edits retain the last valid configuration. Sharing restrictions take
  effect before storage writes, and endpoint changes discard previous API-key bindings.

- Enabled connection priority routing with opt-in availability/authentication fallback, independent
  authentication pauses, and per-attempt provider activity/provenance. Settings integration follows.

- Validated independent connection profile storage and legacy credential migration, preserving
  existing analysis/privacy permissions and keeping provider fallback disabled by default.

- ChatGPT account settings use adaptive account/model selectors, adjacent model refresh,
  and compact account actions instead of a tall stack of full-width controls.

- Restricted process lifetime scopes use optional Windows kernel sequence numbers, preventing
  repeated identity/details resets and allowing limited manual analysis to finish across collection.
  PID reuse and failed confirmation invalidate the scope; name-only automatic analysis stays Unknown.
- Services now show implementation/host file icons where available, with a properly sized native
  gear fallback and names constrained to the Name column.

- Fixed process/service toolbar title alignment and table headers that could be narrower than
  their rows. Header alignment now follows viewport/extent changes and horizontal scrolling.

- Native structured process/service details with assessment, executable, signature, command-line,
  window and evidence sections; compact commands and stable expansion/scroll across live updates.

- Microsoft-sourced Windows 11 / WinUI 3 design guide and native rules redesign: compact selectable
  rows, meaningful metadata names, adaptive details, structured evidence/provenance, theme-aware
  typography, command overflow and explicit delete confirmation.

- Searchable Rules & knowledge library, per-process/service rule inspection, contributing versus
  matching rule status, supporting evidence and recorded AI provider/model provenance.
- Restricted metadata reads preserve partial processes/services and display safe unavailability
  reasons, including access denied, without exposing exception text or requiring elevation.

- Process names preserve established application identities and specific local host/invocation roles.
  Explanatory AI identity prose no longer replaces table labels, including previously saved assessments.

- Fixed ChatGPT plan inference discarding valid streamed explanations when the successful completion
  event contains an empty output array. Streamed output still requires completion and full validation.

- AI activity failures now explain the specific safe rejection reason and what to do next, including
  missing/unknown evidence, invalid classifications, incomplete streams and output limits.
  Rejected model text and exception messages remain excluded from activity history.

- Direct open-source ChatGPT plan sign-in, protected account/session storage, model discovery,
  renewal/revocation, and bounded streamed analysis without API-key billing fallback.
- Native Windows 11 Settings cards and expanders, responsive controls, and explicit account/model
  selection. Live ChatGPT inference validation remains outstanding.
- Readable AI activity history for process/service queries, retries, returned assessments, research
  counts, and connection tests. History is bounded to 200 memory-only entries, with pause/filter/clear.
- Fixed ChatGPT model discovery rejecting the live model catalog: discovery accepts up to 2 MiB;
  token and identity responses retain their smaller 256 KiB bound.

- Local visible-window captions in process names, plus window classes and foreground/minimized
  state in Details. Caption changes do not alter identity or trigger provider re-analysis.
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

- Single-process applications render directly, without redundant headers or `(1)` counts.
  Group transitions preserve row order and selection. Blank Windows descriptions fall back
  to executable names instead of creating unnamed groups.
- Native table scrollbars remain within the narrow-window viewport; horizontal scrolling
  keeps headers and rows aligned in both directions.
- Full-height column headers toggle ascending/descending sorting, display direction arrows,
  and persist preferences. Name sorting follows visible workload labels.
- Inline interpreter code is opaque to role matching; quoted command text is no longer
  mistaken for browser worker switches. .NET certificate/product metadata is recognized.
- Service arguments are excluded from serialized snapshots and shareable service identities.
- Live metric samples no longer continuously reorder rows or notify every cell. Sibling
  order and viewport anchors persist until explicit sorting/refresh; owner slots retain
  their keys as rows switch between individual processes and multi-process groups.
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
