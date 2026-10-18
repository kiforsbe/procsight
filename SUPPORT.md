# Getting help

ProcSight is a development preview with no guaranteed support response time. Start with
the [README](README.md) for setup and features, the [developer guide](DEVELOPERS.md) for
build/install troubleshooting, and [verification notes](docs/verification.md) for known
validation gaps. Performance measurements and their limits are in
[PERFORMANCE.md](docs/PERFORMANCE.md).

Search existing GitHub issues before opening a new one. Use **Bug report** for unexpected
behavior and **Feature request** for proposed improvements. For usage questions not
covered by the docs, an issue with a clear question and relevant version is sufficient.
Do not assume GitHub Discussions or a private support channel is enabled.

## Useful troubleshooting information

- ProcSight package version or Git revision, Windows version/build, and installation type.
- Whether the app is elevated, and whether it is a Debug or Release build with a debugger.
- Minimal reproduction steps, expected behavior and actual behavior.
- For UI issues: theme, display scaling and approximate window size.
- For performance issues: relevant settings, approximate process count and the metric
  measured; distinguish total working set, private memory and managed heap size.

Unavailable metadata on protected processes can be normal, including in administrator
mode. Report incorrect handling or unstable UI with a synthetic example when possible;
do not attach a machine inventory to demonstrate missing data.

## Keep reports safe

Review text and screenshots before posting. Remove credentials, account identifiers,
email addresses, personal paths, full command lines and window titles. Do not attach
knowledge databases, inventory exports, memory dumps, browser authorization URLs or
raw provider responses. Share only the minimal sanitized app-owned reason/status codes
needed to describe a failure. Never provide an API key or sign-in token to reproduce a bug.

For suspected vulnerabilities, follow [SECURITY.md](SECURITY.md) instead of posting
details in a public issue.
