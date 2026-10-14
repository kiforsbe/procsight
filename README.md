# ProcSight

ProcSight is a native Windows 11 process explorer built with C# and WinUI 3.
It explains what processes and services belong to, why they are running, and
what evidence supports their classifications.

**Status: development preview; no published release.** Live collection, local rules and
knowledge, provider adapters, optional research, and the investigation interface are
implemented. Automated checks use synthetic metadata and fake providers; live API behavior
and the complete desktop accessibility/scaling matrix remain unverified.

On Windows 11 with .NET SDK 10.0.301 and Windows App Runtime 2.2 installed:

```powershell
./scripts/build.ps1
./scripts/start.ps1
./scripts/verify.ps1
```

In VS Code, open this repository folder and install the recommended Microsoft C#
extension. Select **ProcSight (Debug)** in Run and Debug, then press **F5** to build
and debug or **Ctrl+F5** to build and run without debugging. See the
[VS Code setup](DEVELOPERS.md#run-and-debug-in-vs-code) for tasks and troubleshooting.

## Current experience

![Native WinUI process table with synthetic data](docs/images/processes-dark.png)

This is the actual XAML rendering, using synthetic processes and a synthetic analysis label.

- A familiar Task Manager-style process table with meaningful application grouping,
  resource measurements, search, and a raw process-tree view.
- Processes and services explained through ownership, purpose, and supporting evidence.
- Separate purpose, security, and optionality assessments, with confidence and unknown
  outcomes when the evidence is insufficient.
- Local rules and cached knowledge first, with automatic AI analysis for unresolved
  cases. Validated rules are reused for matching processes, reducing repeated API calls.
- User-configured OpenAI or compatible API endpoints, optional loopback local models,
  and separately enabled online research with source links.

The table uses compact rows, resource totals, classification filters, and optional details.
Disk/network are explicitly unavailable; GPU collection is deferred. Process icons currently
use a generic native symbol. Collection remains useful without an API key or network connection.
The Windows native host
in VidVNC informed the WinUI shell, spacing, preferences, and component boundaries.

## Privacy and command-line analysis

Settings provides three explicit command-line modes:

| Mode | Behavior |
| --- | --- |
| Off (default) | Do not collect command lines for analysis |
| Local only | Analyze full command lines locally; keep them and derived knowledge out of cloud requests |
| Include in online analysis | Permit the full, verbatim command line at the selected online analysis provider |

Other online metadata remains sanitized. Full command lines can contain credentials or
personal paths; the online option includes a payload preview and explicit disclosure.
Raw command lines are excluded from the knowledge database, logs, rule literals,
and online research queries. API access requires the selected
provider's credentials; a ChatGPT subscription is not assumed to include API access.

AI assessments are explanations with evidence and uncertainty. They are not antivirus
verdicts or authorization to change the computer.

Automatic analysis and research start disabled. Configure a model and endpoint in Settings,
save the API key in Windows Credential Manager, and use the synthetic connection test before
enabling automatic analysis. Default daily limits are 20 automatic jobs and 60 billable
calls, including retries and reserved research calls. OpenAI research requires a model that
supports web search; compatible/local providers currently supply analysis only.

Preferences and SQLite knowledge live under `%LOCALAPPDATA%\ProcSight`. Usage accounting
uses a separate database and survives knowledge resets. Local models must use loopback
HTTP/HTTPS endpoints, with redirects disabled. Command lines above 32,768 characters are
omitted rather than truncated. See [verification](docs/verification.md) for checked behavior
and remaining desktop/live-provider validation.

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
