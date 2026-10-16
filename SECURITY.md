# Security policy

Connection profiles keep API keys in independent Windows-protected credential slots. Plain settings
contain only opaque references. Migration copies a legacy key before publishing the new profile;
failure must not publish a partly configured replacement. Profile endpoints reject embedded
credentials, query strings and fragments, and require HTTPS except loopback HTTP. Local profiles
are restricted to loopback addresses. Profiles are bounded to 32; fallback remains off by default.

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
- ChatGPT plan sessions and their account catalog use protected generation-addressed credential chunks.
  Browser authorization uses PKCE, state and nonce with a temporary IPv4 loopback callback. ID tokens
  require OpenAI JWKS signature/issuer/audience/expiry validation and direct plan-use scope checks.
  Model and inference requests use fixed public OpenAI HTTPS endpoints. Tokens and authorization URLs
  must never enter logs or diagnostics. Sign-in alone does not permit automatic process transmission.
- The AI activity history holds at most 200 request summaries and privacy-projected assessments
  in memory. It excludes full commands, paths, captions, credentials, transport bodies and exception
  text, and provides no export/file logging. Pause prevents new entries; Clear removes current history.
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

Bundled common-task definitions identify known executable purpose using verified signer and
product/original-name metadata. They do not establish safety, necessity, or the exact behavior
of the current invocation. Local role labels derived from permitted command context are
transient display data. Shell icon reads reject network paths, run off-thread, and release
native handles; an icon is cosmetic and never identity or security evidence.

AI classifications are not antivirus verdicts. A legitimate signer or cached explanation
does not alone establish that a running process is safe.

Available command lines are always collected for local display and rules, regardless of online
sharing settings. Serialized Off/LocalOnly exclude commands online; LocalOnly also prefers a
configured loopback model. File paths and ownership/package/service identifiers are excluded
from online facts/evidence by default through a separate setting. Full command sharing sends
verbatim arguments independently of that setting. Local ownership scope is attached to returned
rules after dispatch, and command-derived rules require keyed HMAC invocation scopes; different
scripts cannot reuse each other's invocation-specific assessments. Raw lines remain transient.

Services use configuration evidence from WMI and read-only registry values. Their configured
executable or DLL is hashed and verified separately from the host. A verified host signature
does not authenticate a hosted DLL; actual loaded-module state is not established. Inaccessible,
network, and ambiguous unquoted executable paths remain unavailable. Verification uses offline
WinVerifyTrust, so it does not establish current online revocation status. Publisher-only
references identify a publisher family while leaving purpose unresolved.
Local command rules never execute code, decode encoded commands, or inspect interpreter payloads.

## Handling sensitive data during development

Visible-window captions and classes can reveal document names or browsing activity. They remain
local transient observations and are excluded from serialization, provider/research payloads,
rule literals, and identity. Foreground/minimized state is observational, not trust evidence.
The collector reads top-level captions only, with bounded own-window waits, and never queries
control contents, sends input, or automates another application.

Use synthetic fixtures and redact diagnostic material before sharing. Ignore local secrets,
databases, and dumps, and review staged changes independently of ignore rules. If a credential
is committed, revoke/rotate it promptly; deleting the working file does not erase Git history.

Update this policy alongside changes to analysis payloads, command-line routing, credentials,
privileges, external endpoints, supported releases, or the reporting route.

## Local collection failures and rule provenance

Restricted reads preserve partial process/service records. Only bounded field identifiers and
reason enums are retained; exception text, ownership guesses, and protected paths are not used
as error explanations. Collection issues are excluded from JSON serialization and online payloads.
The application does not elevate to acquire missing metadata. A verified signature remains
separate from a failed signature read; unavailable does not mean unsigned or malicious.

AI rule provenance records configured provider, model, destination and generation time. It is
an audit aid, not proof that an assessment is correct or a process is safe. Existing evidence,
scope, privacy and lifecycle validation still apply before enabling or applying a learned rule.

The native process details model contains transient local fields, including commands and captions.
Their values are excluded from normal JSON serialization and diagnostic record text. UI comparison
strings also stay in memory only. Neither presentation shape is an online-analysis or storage DTO;
provider payloads continue to require the explicit privacy projection.

Kernel process sequence numbers may establish lifetime continuity for restricted processes without
opening them. Their scope is tied to a collector session; it is not binary identity, signature proof
or verified parent ownership. Final sequence confirmation rejects reused or unconfirmed rows. Optional
API fallback retains snapshot scoping rather than guessing a lifetime from the PID. Creation times
are retained only under a matching sequence and are never invented from first observation time.
Service implementation/host icons are cosmetic configured-file presentation, not trust evidence.
