# Instructions for coding agents

These instructions apply throughout this repository. Read [README.md](README.md),
[DEVELOPERS.md](DEVELOPERS.md), [ARCHITECTURE.md](ARCHITECTURE.md), and the relevant
design/plan before making changes.
Direct user instructions take precedence over repository guidance.

## Current project state

ProcSight is an unreleased Windows 11/WinUI 3 development preview. Collection, ownership,
privacy projection, persistent knowledge/rules, provider/research adapters, coordinator,
and native investigation UI exist. Run `scripts/verify.ps1` for locked restore, Release
build, and all four test projects. See `docs/verification.md` for actual checks and remaining
live API/desktop validation. The user approved this feature branch and checkout; preserve
their shared VS Code files. Do not imply release readiness from synthetic tests alone.

Live-list updates preserve sibling order and keys by default; explicit sort/refresh may reorder.
Opt-in automatic process sorting follows the selected sort column using incremental moves, never
the explicit bulk-reset path; pause automatic reordering during column dragging. Preserve selection.
WinRT maps managed collection Move notifications to native vector Reset. LiveRowCollection must
relocate with remove/insert notifications; verify native Items.VectorChanged as well as managed
CollectionChanged so automatic sorting cannot silently reset the native list and flash.
Application groups require multiple visible processes; one member renders as a normal row.
Preserve owner-slot keys and lifetime-aware selection across single/group transitions. Empty
metadata descriptions must use a nonblank executable fallback.
Expand/collapse belongs to a native chevron action, never SelectionChanged. Keep generic
executable identity separate from per-invocation/service roles. Bundled common rules require
verified signer plus product/original-name gates and supply purpose only; they must not
override unresolved learned conflicts or fresh user corrections. Command-derived display
labels stay transient. Icon loading is bounded, cached, off-thread, and releases native handles.
Table names prioritize specific local host/service roles and established app identity. AI labels
must be concise names, never signer/evidence/trust explanations; unknown executables may use concise
assessed names. Preserve useful local window captions and keep explanatory text in assessment details.

## Product boundaries

- Startup stays asInvoker/uiAccess=false; administrator restart is explicit and full-app, with no
  SeDebugPrivilege or management actions. Preserve the original until owned-window readiness.
  Saved normal settings stay separate from cached elevated-session effective policy. Route, preview,
  research and tests honor session permissions, which reset on restart. Never borrow original-user
  credentials/settings on alternate-account elevation. A collector-only broker is future work.

- Connection profiles route only enabled entries in saved priority order, preferring local in
  LocalOnly mode. Fallback is explicit and off by default, with per-profile authentication pause.
  Stop on rejected output, policy cancellation and exhausted budgets; rebuild privacy projection
  for each destination. Provenance/activity use the actual attempted provider, never account IDs.

- Window captions/classes are transient local presentation data, excluded from JSON, provider
  payloads, research, rules, and identity. Verify PID ownership and known lifetimes; cap enumeration
  and text sizes, and use bounded waits for own-window captions. Titles never prove trust or purpose.
- Bundled definitions live in `src/ProcSight.Core/Rules/common-tasks.json`; maintain stable
  executable/product/service scopes, public reference links, and signature/metadata gates.
  Never copy a real inventory, instance suffix, personal path, or command argument into it.
- Service implementation and host proofs are separate. Configuration enrichment does not
  establish loaded-module state. Publisher-only references must preserve unknown purpose.
- Local command rules describe invocation only. Keep interpreter payloads opaque and retain
  only allowlisted command categories and 64-hex HMAC invocation scopes in knowledge; arbitrary
  script labels remain transient. Command-derived learned rules require invocation scope, not
  just a generic CLI/Worker category; old unscoped rules must fail validation.
- First release: understand and investigate processes/services, without modifying them.
- Local evidence and reusable rules precede automatic provider analysis.
- If valid known-purpose analysis proposes no rules, retain an evidence-backed exact-hash rule
  with the coordinator's local ownership/service and applicable invocation scope. Never replace a
  rejected proposal or derive identity from Unknown-only output. Unknown axes stay on the entry;
  do not put unsupported Unknown assessments into the synthesized rule. Restoring legacy entry-level
  labels requires supported selected assessment content and active dependencies, not collection
  reference equality after deserialization.
- Keep purpose, security, and optionality separate; unknown and unavailable are valid.
- Verify rule evidence and scope. Names and metadata company strings do not prove identity
  or trust; preserve PID/start-time and ownership context boundaries.
- Always collect available command lines for local display and rules. Off and Local only now
  exclude online sharing; Local only additionally prefers a configured local model. OnlineFull
  explicitly permits transmission. Default online projection also excludes paths and ownership,
  package, and service identifiers unless separately enabled. Local-only
  data and derived knowledge must not enter online payloads. Raw command lines must not
  enter persistence, logs, rule literals, or research queries.
- API keys belong in Windows-protected credential storage, not tracked files or diagnostics.
- ChatGPT plan OAuth is separate from API-key billing. Bind model/settings to the selected opaque
  account ID, protect tokens and account catalog, and validate state/PKCE/nonce/JWKS and granted scopes.
  Serialize rotating refreshes across app instances. Never use ChatGPT private backend endpoints,
  log authorization URLs/tokens, or silently fall back to API-key charges. Sign-in does not enable analysis.
- Manual ChatGPT sign-in diagnostics may persist only the typed ChatGptAuthEvent contract: app-owned
  stages/outcomes, UTC time, PID, numeric HRESULT/HTTP status and counts. Keep the queue/file rotation
  bounded and off-thread, failure-isolated, and separate from AI activity. No free text, credentials,
  account IDs or callback/authorization parameters. The user performs live sign-in; read only this log.
- ChatGPT plan completion events may have empty output arrays. Preserve streamed text in that case;
  never accept it without successful completion and validation, and honor nonempty final output/refusals.
- AI activity is a product-level, memory-only history of actual provider attempts and projected
  results. Keep its 200-entry bound, immutable snapshots and failure isolation. Never retain raw
  payloads, commands, paths, window captions, snippets or provider exception text; show only safe
  metadata summaries and command-inclusion flags. Do not mislabel local cache/rule hits as AI calls.
- Failure explanations use typed reason codes and app-owned wording, never raw provider/exception
  text. Preserve validation strength and show an explicit unavailable reason for old/unclassified errors.
- External text and model results are untrusted data; generated rules cannot execute code.
- Search-enabled analysis uses a public-identity-only projection even when command sharing is enabled.
  Never put raw commands, private paths or command-derived origins into a tools-enabled request.
  Accept web evidence references only from actual returned tool sources/citations; map URL aliases to
  internal IDs before persistence. ChatGPT and OpenAI can search inside analysis; use a separate step
  only for an explicitly different research connection. Never silently substitute API-key billing.
- Rule rejection diagnostics are app-owned typed codes, not model-supplied strings/codes. Preserve
  original validation failures before local scope enrichment. Only the coordinator can supply the
  private invocation HMAC; provider parsing defers that scope requirement until final validation.
  Legacy missing reasons remain explicitly unknown. Active Unknown rules do not establish identity.
- ChatGPT plan requests reject max_tool_calls/max_output_tokens. Request allowances are admission
  controls, not a hard tool-call or monetary cap. Keep that limitation clear in docs and settings.
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

Keep `CHANGELOG.md` entries short but informative: usually one concise sentence per bullet,
stating the feature or fix and its practical effect. Combine overlapping Unreleased entries
instead of appending repeated implementation history. Retain material privacy, compatibility
and verification limits; put implementation details, test counts and lengthy explanations in
the relevant developer, architecture or verification document. Apply this rule to every future
changelog update. Preserve released versions and dates when editing historical entries.

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

Packaging/release/verification scripts require PowerShell 7+ and reject older hosts before actions.
Certificate trust is an explicit user-invoked exception: `scripts/trust-package-certificate.cmd`
accepts one dropped public `.cer` and its PowerShell companion requests UAC for LocalMachine
TrustedPeople only. Preserve format/validity/code-signing checks, thumbprint continuity across
elevation and in-memory import. Packaging must never invoke trust implicitly; synthetic helper
tests and WhatIf must never elevate or change certificate stores.
Release tooling in scripts/prepare-release.ps1 must use one authoritative product version, synchronize all live version
fields and changelog, and create matching annotated vMAJOR.MINOR.PATCH tags only after validation
and package creation from the release commit. Use stable SemVer with Windows version X.Y.Z.0;
do not independently bump manifests, alter dependency pins or rewrite historical examples. Keep local
release preparation separate from remote pushing/publication. Implementing/testing release scripts
does not authorize creating a real product release/tag; use synthetic temporary repositories.

Use the repository's standard [AGPL v3.0 license](LICENSE); preserve third-party notices.
Do not change the license or add a contributor agreement without a user request.

Rule inspection distinguishes eligible matches from contributing assessments. Maintain
`AppliedRuleIds` when evaluation changes; legacy absence must not be represented as known
contribution. AI rule provenance comes from trusted coordinator settings, not model proposals,
and must contain no account identifiers or credentials. Collection `MetadataIssue` fields remain
local and JSON-ignored; preserve partial records and safe reason codes without exception text
or invented explanations about ownership.

## Native UI design maintenance

Native table columns use TableColumns stable IDs and bounded widths, with separate process/service
preferences. Name is mandatory and fixed first; rows and headers share TableColumnWidth instances.
Header dragging previews cell order in place and commits only on release; Escape/cancel restores the
original. Never rebuild templates on each reorder; resize handlers must resolve the current column
identity after moves. Preserve the Task Manager interaction reference in docs/WINUI3-DESIGN.md.
Preserve multi-selection through live updates, and lifetime-check context actions. Export is a distinct
explicit local projection, never an AnalysisPayload: private commands/paths/captions require export
opt-in, including when deriving names. Export no credentials/activity transport objects; use typed
availability and deduplicate groups by individual lifetime/service key. CSV/TSV neutralizes formula
text; JSON retains original projected text/nulls. Never commit real inventory exports.

Connection management uses a compact native toolbar/list plus separate fallback row. Add/Configure
opens a focused per-profile ContentDialog; provider fields must not leak back into the main page.
Close flushes pending valid edits and protected-key commits using a closing deferral. Removing a
profile clears its research reference and routing membership without signing out shared accounts.
Each dialog owns a fresh editor and explicitly detaches it on close. Transient visual Unloaded
notifications must not dispose live connection/account editors or cancel interactive sign-in/model
work; only actual owner cleanup after the closing flush does so.
Suppress routine settings success text and collapse empty feedback; preserve errors and explicit tests.

Tray monitoring is opt-in, system-only and independent of AI. Preserve bounded 30-sample history,
one-second sampling, English PDH paths, native handle disposal and explicit unavailable/warm-up
states. Minimize-to-tray requires a registered icon; close/Exit terminate normally. Preserve recovery
if Explorer cannot re-register. Per-process resource columns must not reuse system-only metrics.
Process disk/network rates use an owned real-time TraceEvent kernel session only in explicit
administrator mode. Enable the kernel provider before accessing Source; keep parser tracking to
thread/PID attribution, query session.EventsLost and reset baselines on loss. Retain byte totals
only, never file/endpoint metadata or ETL output. Synchronize startup with synchronous session Stop
before the last window's first asynchronous shutdown await; ETW survives owner process termination.
The Performance page keeps 60 system samples in memory, with compact left navigation, Overview
tiles first and native subsystem details. Preserve independent off-thread sampling, bounded
dispatcher updates, theme resources, text equivalents and explicit missing/warm-up states.
Subsystem navigation must retain sidebar items/charts and the compact selector; update only
right-hand content so selection does not replay list entrance transitions.
Process GPU uses a separate off-thread PDH wildcard query and confirmed lifetime baselines, never
the system GPU reading. Combine contexts for the same physical engine, then select the busiest
engine; groups show their busiest member, with incomplete coverage marked. Keep GPU instance
names transient, counters bounded and unavailable/warm-up distinct from zero. Preserve saved
column choices; GPU is selectable and appears in fresh defaults. Process I/O permission notices
must use the explicit, package-checked administrator restart path without automatic elevation.
Resource-cell shading uses transient lifetime-scoped state only. Default recency follows visible
numeric changes, never unchanged polls; missing values reset history. Keep disk/network/GPU modes,
fade duration and intensity in presentation preferences, with no activity history persisted or sent.
Use monotonic fading, bounded cached brushes and one visible-row timer; disable heat in high
contrast, stop/skip animation off-page or minimized, and restore bindings after header dragging.

Settings apply valid values immediately, with 400 ms text debounce and focus-loss flush; partial
drafts cannot replace active configuration. API keys commit only on Enter/focus loss. Restrictive
changes cancel dispatch before disk writes; failed persistence must not enable broader sharing.
Tests target the selected connection with synthetic metadata and never implicitly invoke fallback.

Preserve cumulative Desired versus Current applied policy during concurrent settings edits, and use
one controller across session-consent changes. Limit reductions revoke work before persistence.
Credential edits must retain immutable destination authorization across awaits; a new profile with
no bound key must not borrow legacy keys. Policy/session changes cancel tests and guard post-budget
dispatch/acceptance. Preserve auth pauses across unrelated changes; explicit reauthorization may retry.

Read and apply [the Windows 11 / WinUI 3 design guide](docs/WINUI3-DESIGN.md) before changing UI.
Keep its project conventions current with shared UI changes. Use actual native controls and theme
resources, compact selectable lists, adaptive details, structured evidence, and contextual feedback.
Capture synthetic wide/narrow light/dark views; screenshots do not replace keyboard, Narrator,
high-contrast or scaling validation. Do not broaden a rule's scope when deriving a readable name.

Process/service details now use the native design guide: structured assessments and metadata,
separate signature and configured-host proof, expandable local command/window context, and a
compact toolbar with correction/payload preview in overflow. Preserve expansion and scroll state
for the same process lifetime; resource-only ticks must not rebuild details. Top-level detail section
choices persist across process/service and rule selections and page recreation for the window session.
Use bounded app-owned section keys, never item names, metadata or evidence IDs, for those preferences;
only same-selection evidence content/scroll is retained. Transient field values
and UI comparison strings can contain local private data: never persist, log or send them online.
Async evidence reads must still match the active selection and pane generation before publishing.

Table headers must align with the realized row grid across empty collection, population, resizing
and horizontal scrolling. Use viewport plus extent when sizing headers; guard layout synchronization
against redundant property writes. Page title and CommandBar actions share a vertical center.
Column reordering moves the full header and visible body cells continuously with the pointer;
neighbors slide into preview positions. Restore resource bindings on cancellation/drop. Resize
targets are invisible in every pointer state and centered across dividers (20px total); only the
existing thin separator and resize cursor indicate the edge. Never draw a resize grip or drag badge.
Drag preview uses shared per-column native transforms, without relayout or per-cell animation creation
on each crossing. Only drop commits layout. Resource ticks remain incremental; bulk list reset is
reserved for large explicit reorder/filter requests and must restore selected lifetimes. Keep row
metadata caches transient and invalidate all inputs that affect displayed names/context. Measure UI
and local-rule changes with synthetic workloads before claiming performance improvements; never use
provider latency or machine inventory dumps as UI benchmark data.

Restricted processes use Windows kernel sequence numbers, when available, to keep a stable
collector-session lifetime even when handle-based creation-time reads are denied. Do not replace
this with PID-only caching or fabricated timestamps. Confirm the sequence after metadata capture;
missing/changed confirmation rejects rows. Unsupported API builds retain snapshot-scoped fallback.
A kernel sequence proves process lifetime only, never executable identity, signature or parent ownership.
Name-only automatic assessment stays Unknown; explicit Analyze can assess the limited evidence.
Service icons use configured implementation, then host, as cosmetic sources only; they never imply
loaded-module or publisher proof. Maintain stale-source checks in the shared off-thread icon pipeline.
