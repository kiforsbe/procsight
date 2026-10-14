# ProcSight: an AI-assisted Windows process explorer

Date: 2026-10-13
Status: approved for implementation planning on 2026-10-13, including the requested
local-only and online full-command-line analysis options.

## Purpose and agreed scope

ProcSight is a native Windows 11 WinUI 3 application that helps a person understand
the processes and services running on their own computer. Its starting point is
Windows 11 Task Manager, enriched with meaningful ownership, purpose, explanations,
and inspectable classifications. Repeated generic names should become understandable
application groups when evidence supports that attribution.

The user selected a first release focused on understanding and investigation,
automatic sanitized AI analysis, and automatically activated reusable rules for
all classifications. Management and cleanup are explicitly later phases.

Success means that a user can answer what a process is, why it is running, which
application or service it belongs to, what resources it consumes, and what evidence
supports concern or optionality. Unknown identity and insufficient evidence are
first-class outcomes. An AI judgment is not proof that an executable is safe or malicious.

## First release

- Live process table with sorting, filtering, search, application grouping, and a
  raw process-tree view. Search covers display name, executable name, publisher,
  PID, owning application, and associated service names.
- Services view including running and stopped services, service configuration
  metadata, and links to hosting processes where available.
- Selected-process or selected-service explanation pane with evidence, confidence,
  classifications, provenance, and related entities.
- Local identification, reusable rules, cached analysis, and automatic AI fallback.
- Optional online research, provider configuration, request controls, and visible
  analysis status.
- Rule library with inspect, disable, delete, and re-analysis controls; user corrections.
- Local persistence of explanations, rules, evidence, and preferences.

First release does not end processes, change service state or startup configuration,
uninstall software, remove files, or perform remediation. It does not install a
monitoring service, implement antivirus scanning, or require an always-running agent.

## Native experience

Use a WinUI 3 shell with left navigation for Processes, Services, Knowledge, and
Settings. Processes is the landing page. Use native theme resources, accessible
labels, keyboard navigation, and text alongside classification colors.

The main page has search and filters above a virtualized, sortable table. Keep
familiar resource columns and add understandable identity, role, and classification.
Application groups expand into individual processes. Group summaries aggregate only
known measurements and indicate incomplete totals. The raw tree preserves PID and
parent relationships without implying that every child shares its parent's trust.

Selecting an entity opens an explanation pane, alongside the table on wider windows
and as a navigable detail view on narrower windows. It includes:

- Meaningful identity, executable name, PID/start time, application, and role.
- Current resource measurements and available Windows metadata.
- Separate purpose, security, and optionality assessments, each with confidence.
- Explanation of ownership and process/service relationships.
- Evidence records, source links, collection dates, and missing or conflicting facts.
- Whether the result came from a rule, cache, local metadata, or fresh AI analysis.
- Correct classification, inspect matching rule, and request re-analysis actions.

Example: multiple Node.js processes may become "Codex / tool workers" if parent,
path, and application metadata support that conclusion. Merely sharing node.exe
does not justify that group. Unsupported attribution remains unknown.

## Classification model

Avoid a single label that combines legitimacy with usefulness. Store independent axes:

| Axis | Example values |
| --- | --- |
| Purpose | OS component, application, background helper, updater, developer tool, unknown |
| Security assessment | No concerning evidence observed, suspicious, known threat evidence, unknown |
| Optionality | Essential to identified function, optional, potentially unwanted/bloat, possible leftover, unknown |

Every assessment includes a reason, evidence references, confidence (low, medium,
high), provenance, and validity conditions. "Essential" is contextual: essential
to an application does not mean essential to Windows. User preferences can affect
optionality. Bloat and leftover labels are assessments, not removal instructions.

Known-threat labeling requires independently attributable threat evidence matched
to the executable identity; model recollection or filename matching is insufficient.
All axes may have automatically generated rules, but the validation requirements
for each axis still apply. Missing metadata does not imply suspicion.

## Architecture and boundaries

Use C# and WinUI 3 with independently testable components. Keep collection and
analysis off the UI thread. Do not introduce a Node.js server solely because VidVNC
uses one. Initially components run in one application process at ordinary user
privilege. Interfaces allow a restricted collector broker in a later phase if needed.

| Component | Responsibility and interface |
| --- | --- |
| Windows collector | Produces process/service snapshots, capabilities, relationships, and resource samples; performs no AI calls |
| Identity resolver | Derives executable identities, runtime context, ownership candidates, and evidence-backed groups |
| Local analysis engine | Evaluates validated declarative rules and reusable analysis against current evidence |
| Analysis coordinator | Deduplicates unresolved work, sanitizes payloads, enforces budgets, and coordinates AI/research |
| Provider adapters | Accept sanitized analysis requests and return structured results or typed errors |
| Research adapter | Returns attributable source excerpts and links for sanitized identity queries |
| Knowledge store | Persists versioned rules, analyses, evidence, corrections, and invalidations transactionally |
| Presentation layer | Renders snapshot updates and analysis state; issues investigation commands through application interfaces |

Use a local SQLite knowledge database under the user's application-data directory.
Keep small presentation/provider preferences separately with atomic replacement.
Secrets belong in Windows-protected credential storage, never the knowledge database,
source tree, preferences JSON, or diagnostic output.

## Collection and identity

Collect PID, creation time, executable location, executable version/product/company
metadata, parent identity where verifiable, signature verification status and signer,
service associations, and available package/application ownership. Enrich executable
identity with a content hash when the file is readable; cache hashing using file
identity/change information. Signature data describes verified observations, not
an assertion inferred from the company-name field.

Runtime identity is PID plus creation time. If creation time cannot be read, use a
snapshot-scoped identity and do not carry PID-only conclusions across snapshots.
Parent PIDs require lifetime checks because Windows can reuse PIDs. Distinguish
an executable's identity from its invocation context and owning application.

Collect CPU and working-set memory first-class. Show disk and network values only
when the chosen Windows collection mechanism supplies attributable measurements;
otherwise show unavailable, never fabricated zero. CPU and rate measurements require
sampling intervals; initial samples remain pending. GPU measurement is deferred.

Command-line analysis has three explicit modes: Off (default), Local only, and
Include in online analysis. Local only enables full-command-line collection and
analysis by local rules and, if configured, a loopback local model endpoint. Include
in online analysis additionally permits sending the full, verbatim command line to
the selected online analysis provider. It is an explicit exception to normal payload
redaction, with a payload preview and clear disclosure that arguments can contain
credentials and personal paths. The app does not silently truncate or redact a line
while calling it full; a collection or provider-size limit is shown as unavailable
or omitted, with a reason.

Command-line collection is best-effort without elevation. Raw command lines remain
in memory and are excluded from persisted snapshots, logs, and rule literals. Store
only bounded normalized role/path predicates and provenance needed for reuse. A mode
change clears pending payloads and rebuilds them under the new policy; moving to Off
also releases collected raw lines. Requests already sent cannot be recalled.
Identity and grouping must work with partial access. Ordinary privilege is the
default; protected or inaccessible processes retain visible partial records.

Poll process resources approximately once per second while the live view is active;
collect expensive identity metadata asynchronously and reuse it until invalidated.
Avoid rebuilding the entire visual tree per sample. Services refresh more slowly and
on explicit refresh. Stop collection and cancel queued analysis on application exit.

## Analysis flow

1. Gather an entity snapshot and available local evidence.
2. Apply user corrections, local identity evidence, and eligible rules.
3. Reuse cached analysis only when its executable identity, relevant context,
   evidence requirements, and validity still match.
4. Queue unresolved dimensions or conflicts for AI. Do not re-send unchanged
   unresolved entities continuously after failure or an inconclusive response.
5. Send sanitized metadata through the configured provider. Optional research
   supplies attributable evidence when enabled and useful.
6. Validate the structured explanation and proposed rules. Store valid results,
   evidence, and lineage, then publish updates to the UI.
7. Re-evaluate when relevant evidence changes or the user requests re-analysis.

Resource fluctuations alone do not trigger cloud analysis. Multiple matching
instances share analysis work. One pending request per analysis identity prevents
duplicate calls. Results for exited processes may enrich executable knowledge but
must not attach to a newly reused PID.

## Reusable analytical rules

A cached explanation applies to an established identity and context. A rule can
generalize across related identities only through explicit, validated predicates.
Similarity finds candidate rules; it does not bypass required matches.

A rule contains an ID, schema version, rule version, active/disabled state, origin,
creation/review/expiry timestamps, evidence references, predicates, per-axis outputs,
confidence, specificity, and supersession lineage. Rules are declarative data with
an allowlisted predicate set; they cannot execute scripts, commands, arbitrary regex,
or network operations.

Supported predicate families include content hash, verified signer, product metadata,
executable name, normalized location pattern, package/application identity, verified
parent context, service association, and normalized command-role context when command-line
analysis is enabled. Command-role evidence carries its command-line origin and cannot
embed raw arguments or secrets. Normalize local paths into symbolic roots
where possible. A process name alone cannot establish identity, security, ownership,
or unwanted status. Publisher metadata is not equivalent to a verified signer.

AI-generated rules activate automatically for all classification axes after schema
and semantic validation. Semantic validation checks evidence references, allowed
predicates, scope, and that each output's required evidence is present. Invalid rules
are retained as rejected proposals for inspection and cannot affect classification.

Exact executable findings use a content hash where available. Family rules require
multiple independent identifying predicates and evidence for their intended scope.
An exact finding cannot silently become a family-wide threat or safety assessment.
Runtime-role rules include contextual predicates where one binary has multiple uses.

User corrections take precedence within their explicitly selected scope: executable,
application family, or invocation context. They do not suppress independently observed
conflicting security evidence; show that conflict separately. Prefer more specific
eligible rules. Conflicting eligible rules yield an unresolved assessment and queue
re-analysis; a favorable conclusion must not hide a conflicting concern.

Default review windows are 30 days for identity/purpose and 7 days for security and
optionality assessments. These are freshness limits, not security guarantees.
Source-specific shorter validity wins. Expired assessments remain visible as history
but are not presented as current conclusions. AI responses cannot extend these limits.

Executable changes invalidate hash-specific results immediately. Changed signatures,
unexpected locations, different ownership context, withdrawn evidence, and rule-version
changes trigger dependency-based re-evaluation. Missing required evidence means a
rule cannot match. Evidence refresh may require renewed research rather than another
model opinion alone. Disabling a rule invalidates its dependent current assessments.

## Providers, research, and privacy

The first provider adapter targets the OpenAI API using a user's API key and chosen
model. A ChatGPT subscription is not assumed to supply API access. Add a configurable
OpenAI-compatible endpoint adapter with capability detection; API compatibility does
not imply universal support for structured outputs, research, or every model feature.
Other providers fit the same interface. Unsupported capabilities are shown explicitly.
Verify supported endpoint/model details during implementation rather than copying
unverified package or API versions into the specification.

Automatic cloud analysis starts only after provider setup and the user enables it.
Before activation, show the categories of data sent and a representative sanitized
payload. Redact usernames, machine identifiers, personal path segments, and secrets.
Exclude full command lines unless Include in online analysis is explicitly enabled.
Even in that mode, other metadata remains sanitized; raw environment variables,
file contents, and unrelated personal data remain excluded. Keep local evidence
identifiers stable without revealing original paths.

Local only prohibits raw command lines and command-line-derived analysis text,
evidence excerpts, or predicates from entering online payloads. Track provenance
through stored knowledge so a local result cannot leak indirectly during later
cloud re-analysis. Build any parallel cloud analysis from independently shareable
metadata. Local model requests use explicitly configured loopback endpoints with
redirects disabled; no remote fallback is allowed for command-line-bearing requests
in Local only mode. The app controls its own routing, not a local server's behavior.

Online research has a separate enable control and adapter capability. Use sanitized
queries built from executable/product/signer identity, excluding personal paths.
Full command lines are never search queries, regardless of command-line mode; online
research receives only the independently sanitized research request.
Never upload binaries or other local files. Preserve source URLs, retrieval timestamps,
and what each source actually supports. If research is unavailable, identify that
limitation and allow metadata-only analysis.

Treat filenames, version strings, command arguments, AI responses, and web text as
untrusted data. They cannot alter system instructions, grant capabilities, or cause
computer actions. Research access is bounded by the coordinator; the model cannot
request arbitrary local reads. Open evidence links only through an explicit user action.

Offer pause/resume automatic analysis, separate research control, provider/model choice,
concurrency and budget controls, and clear/delete knowledge controls. Default to one
in-flight AI request, at most 20 automatic analysis jobs per day, and bounded response
size. Jobs include their research work; any additional billable adapter calls count
against a separately visible request counter capped at 60 billable calls per day by
default, including retries. These caps are not
an exact monetary estimate. Manual analysis observes the same budget unless the user
explicitly raises it. A reset does not bypass an exhausted daily request cap.

Use cancellable timeouts and at most two retries for transient failures, honoring
provider rate-limit guidance. Authentication failures pause provider work until corrected.
Do not retry malformed output or unchanged unknown findings in a loop. Diagnostics
contain operational status without API keys or raw personal metadata.

## Persistence and failure behavior

Keep the latest analysis and its superseded lineage, referenced evidence, and rules.
Avoid storing continuous resource history in this release. Prune unreferenced records
and analysis history older than 90 days; preserve active user corrections and evidence
needed to explain active rules. Provide explicit clear controls for generated knowledge
and for user corrections. API credentials have a separate remove control.

Database updates are transactional and schema migrations are versioned. Detect a
corrupt store, preserve the original for local recovery, and offer a fresh store.
Do not silently erase corrections. Preference writes use temporary files and atomic
replacement. UI preferences cannot silently change analysis policy.

The live table remains useful without credentials, offline, with partial Windows
access, or during provider failures. Show distinct states for locally explained,
queued, analyzing, unknown, stale, budget-paused, and failed. Previously valid history
remains inspectable without being mislabeled as current. Resource collection continues
when analysis is paused. Cancellation and process exit do not crash collection.

## Lessons grounded in VidVNC

Reference repository: `../vidvnc`.

- `apps/windows-host/VidVnc.Host.csproj`: native C#/WinUI 3 shell, explicit runtime
  prerequisites, resource-index publication, and selective dependencies. Reuse build
  lessons; validate ProcSight's selected SDK/runtime versions during planning.
- `apps/windows-host/HostWindow.Layout.cs`: native NavigationView, theme resources,
  DPI-aware dimensions, and accessible labels. Adapt to a dense virtualized process
  table rather than copying VidVNC's card-heavy presentation.
- `docs/design/windows-host-spacing.md`: shared spacing tokens (24 page gutters,
  16 section/card spacing, 8 related controls, 4 metadata gaps) and avoidance of
  duplicated padding. Dense table rows get their own consistent sizing.
- `apps/windows-host/HostPreferencesStore.cs`: bounded reads and atomic preference
  replacement, with presentation preferences separated from operational policy.
- VidVNC's separation of host presentation and runtime ownership informs explicit
  ProcSight boundaries and shutdown cancellation. Its streaming server, child-process
  lifetime ownership, and network listener are not needed for observation of unrelated
  processes.

Start with an unpackaged developer build and a documented launch path. Installer,
signing, and production distribution are a later release task, not prerequisites for
demonstrating the approved first-release experience.

## Later phases

### Phase 2: process and service management

Add explicitly initiated end-process, start/stop-service, and relevant management
actions. Introduce a separate command boundary, fresh target-identity checks,
permission handling, elevation only where required, and clear impact information.
Analysis and rules can recommend investigation but cannot execute management actions.
The first release's collector and evidence models do not depend on these commands.

### Phase 3: cleanup recommendations

Add installation inventory, startup registrations, service dependencies, and evidence
of orphaned or unnecessary components. Explain why something may be removable,
what depends on it, and how to undo supported changes. Distinguish preference-based
bloat assessments from threats and actual orphaned components. A missing process
or old executable timestamp alone cannot establish leftover software. Cleanup changes
require explicit user actions through the management boundary.

These phases are roadmap commitments in the design, not first-release functionality.
Each receives its own detailed design before implementation.

## Verification and acceptance

Use fixture-driven tests for the domain and adapters plus Windows integration checks:

- PID reuse and unverifiable parent relationships do not transfer identity or trust.
- Identical executable names under different applications stay distinct until evidence
  supports grouping; family rules do not overgeneralize across contexts.
- Rule precedence, conflicts, expiry, corrections, invalidation, and disabled-rule
  dependencies behave as described, including all classification axes.
- Redaction prevents usernames, personal paths, secrets, and full command lines from
  entering default analysis and research payloads. Local-only command lines and
  derived knowledge cannot reach cloud adapters. Online mode includes the full
  command line only at the configured analysis provider, never research queries,
  logs, or persistence. Test mode changes while requests are queued.
- Malformed provider results, prompt-injection text, invented source references,
  unavailable research, timeouts, rate limits, and budget exhaustion degrade visibly.
- Analysis deduplication avoids repeated calls for equivalent instances, unchanged
  inconclusive results, and process churn.
- Resource samples handle initial intervals, missing access, and exited processes;
  unavailable metrics are never shown as measured zero.
- Knowledge survives restart; migrations, atomic preference writes, and corrupt-store
  recovery preserve or explicitly account for user corrections.
- On a representative Windows 11 machine, a table of at least 500 processes remains
  interactive while analysis is running. Verify sorting, filtering, selection stability,
  keyboard access, light/dark theme, and multiple display scaling settings.
- A configured provider yields an attributable explanation for an unresolved process,
  produces a validated active rule, and a later eligible process uses that rule without
  another provider call. A conflicting context causes re-analysis instead.
- The complete investigation flow works without performing any system-changing action.

Implementation planning must turn these requirements into concrete tasks and checks.
Written-spec approval precedes that plan; plan review and execution-method selection
precede scaffolding and implementation.
