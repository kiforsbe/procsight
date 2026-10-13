# ProcSight

ProcSight is a planned native Windows 11 process explorer built with C# and WinUI 3.
It aims to explain what processes and services belong to, why they are running, and
what evidence supports their classifications.

**Status: implementation in progress.** The native shell and read-only local process/service
collector are runnable. AI analysis, reusable rules, and the complete investigation interface
are still being implemented; there is no published release yet.

On Windows 11 with .NET SDK 10.0.301 and Windows App Runtime 2.2 installed:

```powershell
./scripts/build.ps1
./scripts/start.ps1
```

## Planned experience

- A familiar Task Manager-style process table with meaningful application grouping,
  resource measurements, search, and a raw process-tree view.
- Processes and services explained through ownership, purpose, and supporting evidence.
- Separate purpose, security, and optionality assessments, with confidence and unknown
  outcomes when the evidence is insufficient.
- Local rules and cached knowledge first, with automatic AI analysis for unresolved
  cases. Validated rules are reused for matching processes, reducing repeated API calls.
- User-configured OpenAI or compatible API endpoints, optional loopback local models,
  and separately enabled online research with source links.

The complete experience above is a design commitment; the current build provides local
process/service tables, CPU/memory sampling, search, ownership candidates, and metadata details.
The Windows native host
in VidVNC informed the WinUI shell, spacing, preferences, and component boundaries.

## Privacy and command-line analysis

The design provides three explicit command-line modes:

| Mode | Planned behavior |
| --- | --- |
| Off (default) | Do not collect command lines for analysis |
| Local only | Analyze full command lines locally; keep them and derived knowledge out of cloud requests |
| Include in online analysis | Permit the full, verbatim command line at the selected online analysis provider |

Other online metadata remains sanitized. Full command lines can contain credentials or
personal paths; the online option includes a payload preview and explicit disclosure.
Raw command lines will not be stored in the knowledge database, logs, or rule literals,
and will never be used as online research queries. API access requires the selected
provider's credentials; a ChatGPT subscription is not assumed to include API access.

AI assessments are explanations with evidence and uncertainty. They are not antivirus
verdicts or authorization to change the computer.

## Roadmap

1. **Understand and investigate:** live processes/services, ownership, classifications,
   local knowledge, AI fallback, optional research, and an inspectable rule library.
2. **Process and service management:** explicitly initiated controls with identity,
   permission, and impact checks.
3. **Cleanup recommendations:** investigate startup items, optional software, and
   possible leftovers using installation and dependency evidence.

## Project documents

- [Design](docs/superpowers/specs/2026-10-13-procsight-design.md)
- [Implementation plan](docs/superpowers/plans/2026-10-13-procsight.md)
- [Architecture and UML diagrams](ARCHITECTURE.md)
- [Developer guide](DEVELOPERS.md)
- [Agent instructions](AGENTS.md)
- [Changelog](CHANGELOG.md)
- [Security policy](SECURITY.md)

See the developer guide for toolchain, verification, and contribution instructions.

## License

ProcSight uses GitHub's standard **GNU Affero General Public License v3.0** template.
See [LICENSE](LICENSE) for the complete terms. Third-party components retain their own
licenses; their notices must accompany any distribution that includes them.
