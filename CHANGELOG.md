# Changelog

Notable changes to ProcSight are recorded here. Entries under Unreleased describe work
in this repository; planned product features are documented in the design and roadmap.

## [Unreleased]

### Added

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
  are not collected; native process icons currently use a generic symbol.
- No GitHub remote or private vulnerability-reporting channel is configured in this
  checkout. CI/CD workflows are intentionally excluded.
