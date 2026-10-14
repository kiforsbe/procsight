# Security policy

## Supported versions

ProcSight has no executable release or supported application version yet. The repository
currently contains an in-progress native application, design documents, and project policy. Supported releases and
security-update expectations will be listed here when releases exist.

## Reporting a vulnerability

Do not put credentials, personal process metadata, full command lines, or exploit details
in a public issue or pull request.

After this repository is published on GitHub, use **Security > Report a vulnerability**
if private vulnerability reporting is enabled. Its availability is not currently confirmed.
If it is unavailable, use a maintainer's explicitly published private security contact.
No private reporting address has been configured yet; a public issue may request a private
contact route without disclosing vulnerability details or sensitive information.

Before distributing executable releases, maintainers must enable private reporting or add
a real private contact route here. Do not invent an email address or promise a response time.

A private report should include the affected revision/version, Windows version, expected
and observed behavior, impact, and minimal reproduction steps with synthetic data.

## Implemented security boundaries and validation limits

The following boundaries exist in code and have focused automated checks. They do not
constitute an independent security audit or verified behavior of a remote/local provider:

- Read-only observation with ordinary user privilege in the first release.
- Sanitized metadata for online analysis, with explicit opt-in for a verbatim full
  command line. Command lines may contain passwords, tokens, and personal paths.
- Local-only command lines and derived findings remain out of cloud requests, including
  later re-analysis. Local model routing is loopback-only with redirects disabled.
- No raw command lines in stored snapshots, logs, rule literals, or research queries.
- API credentials protected by Windows credential storage.
- Evidence and scope validation before generated rules become active; external/model
  text cannot execute commands or grant local capabilities.
- Known limitations, missing data, stale findings, and conflicting evidence remain visible.

Automatic AI and research are disabled by default. Requests are bounded and daily limits
persist separately from generated knowledge. Cloud metadata-only requests in Local only
mode use a separate shareable identity. A policy revision change cancels pending work and
is checked again before analysis dispatch. Already transmitted requests cannot be recalled.
Command-bearing model explanations are replaced with safe summaries before persistence;
rules cannot retain arbitrary echoed arguments. Full lines exceeding 32,768 characters
are omitted, never silently truncated. Configured local endpoints must use loopback;
ProcSight cannot guarantee the behavior of a separately operated server.

See [verification](docs/verification.md) for evidence and outstanding checks. Disk/network
and GPU measurements are unavailable/deferred. Protected-process access denial is normal
partial collection; launching with administrator privilege is not required.

AI classifications are not antivirus verdicts. A legitimate signer or cached explanation
does not alone establish that a running process is safe.

## Handling sensitive data during development

Use synthetic fixtures and redact diagnostic material before sharing. Ignore local secrets,
databases, and dumps, and review staged changes independently of ignore rules. If a credential
is committed, revoke/rotate it promptly; deleting the working file does not erase Git history.

Update this policy alongside changes to analysis payloads, command-line routing, credentials,
privileges, external endpoints, supported releases, or the reporting route.
