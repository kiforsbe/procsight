# Security policy

## Reporting a vulnerability

Do not put credentials, personal process metadata, full command lines, or exploit details
in a public issue or pull request.

On GitHub, use **Security > Report a vulnerability** if private vulnerability reporting
is enabled. Its availability is not currently confirmed.
If it is unavailable, use a maintainer's explicitly published private security contact.
No private reporting address has been configured yet; a public issue may request a private
contact route without disclosing vulnerability details or sensitive information.

Before distributing executable releases, maintainers must enable private reporting or add
a real private contact route here. Do not invent an email address or promise a response time.

A private report should include the affected revision/version, Windows version, expected
and observed behavior, impact, and minimal reproduction steps with synthetic data.

Do not send credentials or real inventory dumps even in a private report. Supply only
the minimum sanitized evidence needed. There is no promised response or remediation
time. Ordinary bugs and support questions belong in the issue templates and
[SUPPORT.md](SUPPORT.md), not a vulnerability report.

## Supported versions

ProcSight is an unreleased development preview. Source builds and locally prepared
development MSIX packages are for evaluation; no stable release or supported-version security
update policy has been established. See [verification notes](docs/verification.md)
for actual checks and remaining acceptance work.

## Implemented security boundaries and validation limits

Connection profiles keep API keys in independent Windows-protected credential slots. Plain settings
contain only opaque references. Migration copies a legacy key before publishing the new profile;
failure must not publish a partly configured replacement. Profile endpoints reject embedded
credentials, query strings and fragments, and require HTTPS except loopback HTTP. Local profiles
are restricted to loopback addresses. Profiles are bounded to 32; fallback remains off by default.

New profiles cannot borrow a legacy API key when no profile credential is bound. Key commits retain
the explicitly authorized endpoint/profile across asynchronous persistence and discard stale edits.
Protected key records also bind provider kind and endpoint, so editing a plain preference reference
cannot redirect a stored key to another destination or select an unrelated credential namespace.
Desired settings are separate from applied settings; permission and budget reductions revoke work
before disk writes. Synthetic connection tests cancel on policy/session changes and recheck revision
after asynchronous budget reservation and before accepting a response.

Tray monitoring uses system counters only, without provider calls, credentials, process commands
or persistent sample logs. It owns and releases native icon/menu/query resources. Tray actions
are a fixed typed set: open, settings, metric selection and exit. They do not modify processes.

Local inventory export is a separate explicit disclosure boundary. Command lines/derived names,
paths/ownership and window captions/derived names are excluded until independently selected in the
export dialog, regardless of online-sharing settings. The UI freezes a collector/assessment snapshot;
export does not access credential stores or provider/activity transport records. Full command lines
may contain secrets supplied to other applications, so opt-in exports can be sensitive. Nothing is
uploaded automatically. Delimited text is quoted and formula-like strings are prefixed for spreadsheet
safety; JSON retains original projected strings and nulls. Writes use a temporary file in the chosen
folder and replace the destination only after successful completion. The temporary file is deleted
on ordinary failure/cancellation; interruption of the whole process can leave it behind. In an elevated
session the Save picker and write use that session's privileges, so choose the destination deliberately.

Local MSIX tooling validates explicit package identity, capabilities and contents before labeling
an artifact inspected. Public Direct distribution requires a selected trusted code-signing certificate
matching the publisher and HTTPS timestamping; development certificate creation never installs trust.
Unsigned development artifacts are not installation-ready. Store preparation does not establish
Microsoft approval for restricted capabilities or certification. Signing and installed lifecycle
acceptance remain outstanding; no certificate or package is automatically trusted or installed.

Release preparation requires PowerShell 7+, a clean named branch and valid local Git identity.
It commits only synchronized version/changelog files, builds from that exact clean commit and checks
artifact version/commit/hash before an annotated local tag. Tags are never moved and scripts do not
publish. Review the selected source/dependencies before distributing a release: these consistency
checks establish provenance, not executable safety or Store acceptance.

Search-enabled analysis projects public identity metadata only, stripping raw commands, private
paths and command-derived evidence even when ordinary analysis command sharing is permitted.
ChatGPT-plan requests use the selected account's OAuth token at the fixed public Responses endpoint;
web search never silently substitutes an API key. Only sources actually returned in tool output or
provider citation annotations can back web evidence aliases. Web claims remain untrusted and do not
prove local executable identity or meet the separate exact-hash threat-intelligence requirement.
Plan requests have no supported server-side tool-count cap. The local request allowance admits work
and reserves an additional research unit; it does not enforce a monetary or remote web-tool budget.
Persisted rejection reasons are fixed typed codes derived by the app; raw provider rejection text,
command arguments and exceptions are excluded. Missing legacy causes must never be invented.

### Explicit administrator mode

Packaged instances inspect the current Windows package's installed manifest before enabling restart.
Only the restricted allowElevation capability permits packaged restart; failed inspection disables it.
This is a feature gate, not proof of Store approval. The launcher also enforces the gate. Packaged
data uses its package family's LocalState folder and Credential Manager targets include that family,
so installation does not implicitly import unpackaged settings or OAuth sessions. Unknown package
inspection uses isolated session data and refuses credential access. Unpackaged paths/credential
targets remain compatible. Windows uninstall is not assumed to erase Credential Manager entries.

The manifest remains `asInvoker` with `uiAccess=false`. Explicit `runas` launches the actual
absolute app executable, never a path from preferences, process arguments or model output.
The original stays available until the returned process owns a visible ready-marked window;
cancellation, early exit and a 20-second timeout preserve it. No privilege adjustment,
SeDebugPrivilege, process/service modification or arbitrary privileged IPC is introduced.
Protected/PPL metadata may remain unavailable.

The entire UI, native parsing, SQLite and provider code run elevated in this mode, increasing the
consequence of a vulnerability. Only elevate a trusted app/runtime/dependency tree; an unsigned
build in a user-writable checkout is not a hardened privileged distribution. A collector-only broker
is future hardening; signed protected installation is future release work.

Each elevated session blocks online analysis, tests and research until fresh consent. Commands
and identifying metadata require separate session consent as well as saved sharing settings.
Local analysis stays available. Consent is not persisted; saved policy and effective overlay stay
separate. Revocation invalidates queued work; already transmitted data cannot be recalled.

Settings/credentials resolve under the current Windows context. Alternate-account UAC and
Administrator Protection must never borrow original-user credentials or settings. ProcSight never
collects UAC credentials. To return to standard privileges, close and reopen through the unelevated
shell; a child of an elevated app is not presumed unelevated. Live UAC/alternate-account/bootstrap
and protected-process acceptance remain outstanding.

The following boundaries exist in code and have focused automated checks. They do not
constitute an independent security audit or verified behavior of a remote/local provider:

- Read-only observation with ordinary user privilege by default and explicit administrator restart.
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
  When a provider proposes no rules, known-purpose assessments may produce a coordinator-scoped
  exact-hash rule with submitted evidence. It passes the same validator and local ownership/service
  and invocation-scope gates. Rejected proposals and Unknown-only results never trigger this fallback.
- Known limitations, missing data, stale findings, and conflicting evidence remain visible.

Automatic AI and research are disabled by default. Requests are bounded and daily limits
persist separately from generated knowledge. Cloud metadata-only requests in Local only
mode use a separate shareable identity. A policy revision change cancels pending work and
is checked again before analysis dispatch. Already transmitted requests cannot be recalled.
Command-bearing model explanations are replaced with safe summaries before persistence;
rules cannot retain arbitrary echoed arguments. Full lines exceeding 32,768 characters
are omitted, never silently truncated. Configured local endpoints must use loopback;
ProcSight cannot guarantee the behavior of a separately operated server.

See [verification](docs/verification.md) for evidence and outstanding checks. System resource
monitoring uses local CPU/memory APIs, PDH and adapter byte totals, independent of AI. Its 60-sample
history stays in memory. Explicit administrator mode additionally owns a uniquely named real-time
kernel ETW session for process disk/network byte attribution. It never takes over a shared logger,
writes an ETL file, requests SeDebugPrivilege or retains file paths, endpoints or packet contents.
Parser tracking is limited to thread/PID attribution. Loss invalidates the sampling baseline;
startup and unavailable counters remain explicit. Window shutdown stops the session synchronously
before asynchronous cleanup. Unexpected process termination may still require OS session cleanup;
normal/elevated live acceptance remains documented separately. Protected-process access denial is
normal partial collection; administrator mode is optional for the rest of ProcSight.
Per-process GPU uses local PDH engine counters without requesting elevation. Instance names are
parsed transiently for PID/physical-engine attribution; only lifetime-scoped utilization observations
reach snapshots. Driver/counter failures preserve the rest of collection. The process-page restart
button shares Settings' package checks, Windows consent and owned-window readiness behavior.

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

Explicitly requested ChatGPT sign-in troubleshooting uses a bounded local operational log containing
only app-owned stage/outcome enums, UTC time, process ID, numeric HRESULT/HTTP status and counts.
No tokens, account IDs, emails, authorization URLs, callback parameters, HTTP bodies or exception text
are accepted by the log contract. A bounded background queue and one rotated file limit retention;
logging failures never affect authentication. This does not persist AI activity or provider payloads.

The explicit `scripts/trust-package-certificate.cmd` drop target delegates to its PowerShell 7
companion. It accepts only public, currently valid code-signing certificates, displays their identity,
and requests UAC to add machine-wide LocalMachine/TrustedPeople trust. It never uses Trusted Root
or installs a package. Only drop a certificate whose source and package signer you have verified;
this trust extends to other packages signed by that key. Elevated validation pins the original
thumbprint and imports the validated object rather than reopening the file. Packaging does not
invoke this helper. Synthetic tests and WhatIf do not import trust.

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
