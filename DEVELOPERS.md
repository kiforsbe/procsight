# Developer guide

## Current state

ProcSight is at the design/planning stage. Only documentation and repository policy files
exist. There are no solution files, application sources, build scripts, tests, or CI/CD
workflows yet. The implementation plan is awaiting review and execution-method selection.

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
path. Add exact commands here when they are available and verified. There is no runnable
setup command at the current stage.

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
and ordinary-privilege; no process/service management actions belong in the first release.

Run collection and analysis away from the UI thread. Keep unavailable facts explicit.
Do not derive identity from a name alone, trust metadata company names as verified
signers, or attach old findings to a reused PID. Generated rules are validated declarative
data, never executable code.

## Contributions and verification

Keep changes focused and describe their purpose, validation, and limitations. Use synthetic
process/service fixtures with no personal paths, credentials, or real command-line dumps.
Real API keys and machine metadata must remain outside the repository.

For now, verify documentation links, factual status, and whitespace with `git diff --check`.
After implementation begins, run checks appropriate to each change, record exact commands
and results, and maintain documented local build/test scripts. Automated checks use fake
providers; real provider tests are explicit opt-in actions with synthetic data.

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
