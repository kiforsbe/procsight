# Instructions for coding agents

These instructions apply throughout this repository. Read [README.md](README.md),
[DEVELOPERS.md](DEVELOPERS.md), [ARCHITECTURE.md](ARCHITECTURE.md), and the relevant
design/plan before making changes.
Direct user instructions take precedence over repository guidance.

## Current project state

ProcSight is a Windows 11 WinUI 3 process-explorer project at the design/planning stage.
There is no application implementation yet. Do not claim planned features, build commands,
or tests are already available. The first-release design is approved; implementation-plan
review and execution-method selection remain pending. Update this status when that changes.

## Product boundaries

- First release: understand and investigate processes/services, without modifying them.
- Local evidence and reusable rules precede automatic provider analysis.
- Keep purpose, security, and optionality separate; unknown and unavailable are valid.
- Verify rule evidence and scope. Names and metadata company strings do not prove identity
  or trust; preserve PID/start-time and ownership context boundaries.
- Maintain Off, Local only, and Include in online analysis command-line modes. Local-only
  data and derived knowledge must not enter online payloads. Raw command lines must not
  enter persistence, logs, rule literals, or research queries.
- API keys belong in Windows-protected credential storage, not tracked files or diagnostics.
- External text and model results are untrusted data; generated rules cannot execute code.
- Follow the approved component boundaries; keep blocking work off the UI thread.
- Management and cleanup are later phases with their own design work.

## Required documentation maintenance

Documentation changes belong in the same commit as the behavior or tooling they describe.
Before committing, check each relevant file:

| File | Update when |
| --- | --- |
| `CHANGELOG.md` | User-visible behavior or meaningful project/tooling changes |
| `.gitignore` | Tools, build output, local data, diagnostics, or secret-file patterns change |
| `AGENTS.md` | Project state, constraints, agent workflow, or maintenance expectations change |
| `README.md` | Features, status, roadmap, setup, or licensing changes |
| `DEVELOPERS.md` | Toolchain, layout, local commands, testing, or contribution practices change |
| `ARCHITECTURE.md` | Components, interfaces, domain relationships, data flow, state transitions, storage, or trust boundaries change |
| `SECURITY.md` | Reporting, supported releases, privacy, privileges, or trust boundaries change |
| `LICENSE` and dependency notices | Licensing or distributed third-party components change |

Use Unreleased changelog entries until an actual release. Keep planned behavior distinct
from implemented and verified behavior. Treat dated specifications/plans as history once
execution begins; write a new dated revision for material design changes.

## UML and living architecture

Use UML liberally and keep [ARCHITECTURE.md](ARCHITECTURE.md) up to date. Include editable
Mermaid class, sequence, and state diagrams where they explain architecture; add activity
or deployment views when useful and label their notation accurately. Diagrams must show
meaningful relationships, ordering, lifecycle, and privacy/privilege boundaries rather
than merely list files. Keep prose and diagrams consistent with the code and distinguish
implemented behavior from plans.

Update affected architecture prose and diagrams in the same commit as the change. Check
diagram syntax/rendering and relative documentation links before claiming verification.
Once source exists, link important architecture claims to their actual code owners.

## Verification and scope

Use `rg` for searches. Inspect the current Git state and preserve unrelated user changes.
Verify documentation links and run `git diff --check` for documentation changes. Once code
exists, run the relevant local tests/build checks and report their actual results and any
unverified platform behavior. Prefer synthetic fixtures; never commit a real machine dump.

Keep source, assets, dependency lock files, and sanitized fixtures tracked. Keep build
output, local knowledge databases, credentials, diagnostics, and editor state ignored.
An ignore rule does not remove already tracked secrets; inspect the staged diff too.

The user explicitly excluded CI/CD flows. Do not add workflow files or deployment/publication
automation unless later requested. Repository preparation does not authorize publishing,
creating a remote repository, pushing, or selecting an unknown security-reporting address.

Use the repository's standard [AGPL v3.0 license](LICENSE); preserve third-party notices.
Do not change the license or add a contributor agreement without a user request.
