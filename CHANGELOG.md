# Changelog

Notable changes to ProcSight are recorded here. Entries under Unreleased describe work
in this repository; planned product features are documented in the design and roadmap.

## [Unreleased]

### Added

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

- The application is under implementation and unreleased. AI/rules and the complete
  investigation UI are not finished; disk/network/GPU measurements are not collected.
- No GitHub remote or private vulnerability-reporting channel is configured in this
  checkout. CI/CD workflows are intentionally excluded.
