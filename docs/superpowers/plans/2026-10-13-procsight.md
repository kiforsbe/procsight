# ProcSight Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development
> (recommended) or superpowers:executing-plans to implement this plan task-by-task.
> Steps use checkbox (`- [ ]`) syntax for tracking. Implementation awaits plan review
> and the user's execution-method selection.

**Goal:** Build a native Windows 11 explorer that explains processes and services using
local evidence and reusable classification rules, with automatic AI fallback.

**Architecture:** An ordinary-privilege WinUI 3 application hosts independently testable
collection, identity, rules, privacy, persistence, and provider components. The coordinator
applies local knowledge before invoking providers; the UI consumes immutable snapshots
and analysis updates. Command-line policy and provenance govern every analysis boundary.

**Tech Stack:** C#, .NET 10, WinUI 3, Windows APIs, Microsoft.Data.Sqlite, System.Text.Json,
HttpClient, xUnit, and PowerShell developer scripts. No Node server or monitoring service.

**Spec:** [Approved design](../specs/2026-10-13-procsight-design.md).

## Global constraints

- Ordinary user privilege; Windows 11; unpackaged developer build; no system-changing actions.
- Automatically activate validated rules for all classification axes.
- CPU and working-set memory are first-class; unavailable metrics are never fabricated zero.
- Command-line modes: Off (default), Local only, Include in online analysis.
- Online full command lines are verbatim and opt-in; other online metadata remains sanitized.
- Local-only command lines and their derived knowledge cannot enter online payloads.
- Raw command lines never become persisted snapshots, logs, or rule literals.
- Research never receives a full command line; command-bearing local-model requests use
  loopback endpoints with redirects disabled and no remote fallback.
- Default review windows: 30 days for identity/purpose; 7 days for security/optionality.
- Default one in-flight AI request, 20 automatic analysis jobs/day, 60 billable calls/day
  including retries; at most two retries per transient failure.
- Prune unreferenced records/history older than 90 days; preserve active corrections and
  evidence required to explain active rules. Do not persist continuous resource history.
- API credentials use Windows-protected storage; no API keys in JSON, SQLite, or logs.
- Source/model output is untrusted data; no generated executable code or arbitrary tools.
- Management and cleanup remain later phases, documented but not implemented here.

## Review focus

1. Mode changes during queued work: no previously prepared command line leaves after
   online permission is revoked. Pin in Tasks 4 and 9.
2. Paths containing personal data or lookalike publishers: normalization must not equate
   a metadata company name with a verified signer. Pin in Tasks 3 and 4.
3. Local-model endpoints that redirect remotely: local-only data must never follow them.
   Pin in Task 7.
4. Multiple services sharing one host or a reused PID: attribution must remain accurate
   and incomplete relationships must be visible. Pin in Tasks 2, 3, and 10.
5. Database corruption or interrupted policy writes: preserve corrections, budgets, and
   privacy settings rather than silently resetting them. Pin in Task 5.

## Delivery sequence and working milestones

This is one product plan because identity, privacy, rules, and UI share contracts; they
are not independent products. Each task has an isolated verification boundary.

- After Tasks 1-3: native local explorer with real processes, services, and ownership evidence.
- After Tasks 4-6: private local analysis, persistence, reusable rules, and corrections.
- After Tasks 7-9: provider/research adapters and bounded automatic fallback.
- After Tasks 10-11: complete investigation UI and verified developer release.

## File structure

Use namespace roots matching project names. Core and Application target net10.0;
Windows and App target net10.0-windows10.0.26100.0, x64. Test Windows functionality on
Windows, keeping domain tests independent of WinUI initialization.

| Path | Ownership |
| --- | --- |
| `ProcSight.slnx`, `global.json`, `Directory.Build.props`, `Directory.Packages.props` | SDK, reproducible build, central pinned package versions |
| `src/ProcSight.Core/Models/` | Immutable identities, snapshots, classifications, provenance, rule contracts |
| `src/ProcSight.Core/Identity/` | Parent validation, ownership grouping, analysis identities |
| `src/ProcSight.Core/Rules/` | Predicate validation, matching, precedence, freshness |
| `src/ProcSight.Core/Privacy/` | Command-line policy, projection, redaction, derived-data restrictions |
| `src/ProcSight.Application/` | Coordinator, knowledge/credential interfaces, budgets, update stream |
| `src/ProcSight.Windows/Collection/` | Process/service enumeration and metadata enrichment |
| `src/ProcSight.Windows/Storage/` | Atomic preferences and Windows credential implementation |
| `src/ProcSight.Infrastructure/Knowledge/` | SQLite migrations, transactions, retention, recovery |
| `src/ProcSight.Infrastructure/Providers/` | OpenAI, compatible, loopback adapters and response validation |
| `src/ProcSight.Infrastructure/Research/` | Sanitized research adapter and source validation |
| `src/ProcSight.App/` | Composition root, XAML shell, views, view models, theme tokens |
| `tests/ProcSight.Core.Tests/`, `tests/ProcSight.Application.Tests/` | Fixture-based domain/orchestration tests |
| `tests/ProcSight.Infrastructure.Tests/`, `tests/ProcSight.Windows.Tests/` | Fake HTTP, temporary database, Windows integration checks |
| `tests/Fixtures/` | Synthetic metadata, churn, provider and research fixtures without personal data |
| `scripts/build.ps1`, `scripts/start.ps1`, `scripts/verify.ps1` | Native build, launch, repeatable checks |
| `README.md`, `docs/architecture.md`, `docs/verification.md`, `CHANGELOG.md` | Living project/build documentation |

Baseline locally confirmed: .NET SDK 10.0.301, Windows SDK include 10.0.26100.0,
VidVNC/cached WinUI package 2.2.1, Runtime 2.2.0, BuildTools 10.0.26100.4654.
Use those WinUI versions initially, not an assumed latest version. Pin SDK 10.0.301
with latestPatch roll-forward. Resolve stable compatible Microsoft.Data.Sqlite and
xUnit/test-runner and System.Management packages in Task 1, centralize exact resolved
versions, commit lock
files, and verify restore/build before relying on them. No dependency installation
or product scaffolding was performed while writing this plan.

## Shared contracts

Task 1 defines these records/enums; subsequent tasks implement the listed interfaces.
Nullable observations use `Observation<T>(T? Value, ObservationState State, string? Reason)`
where states are Available, Pending, Unavailable. Unknown is an assessment, not zero.

- `ProcessKey(int Pid, DateTimeOffset? StartedAt, Guid? SnapshotScope)`; absent start
  time requires a non-null snapshot scope.
- `ProcessSnapshot(ProcessKey Key, int? ParentPid, ExecutableMetadata Executable,
  ResourceSample Resources, IReadOnlyList<string> ServiceNames, CommandLineValue? CommandLine)`.
- `ServiceSnapshot(string Name, string DisplayName, string State, string StartMode,
  int? HostPid, string? BinaryPath, ObservationState AccessState)`.
- `MachineSnapshot(Guid Id, DateTimeOffset CapturedAt, IReadOnlyList<ProcessSnapshot>
  Processes, IReadOnlyList<ServiceSnapshot> Services)`.
- `ExecutableMetadata`: observed full path, name, hash, version, product, metadata
  company, signature status, verified signer, file identity, and optional package identity.
- `ResourceSample`: observed CPU percent, working-set bytes, disk bytes/sec, network
  bytes/sec, plus timestamp and sample interval. CPU percent is machine-total normalized.
- `CommandLineMode`: Off, LocalOnly, OnlineFull; `DataOrigin`: ShareableMetadata,
  LocalCommandLine, OnlineCommandLine, Research, UserCorrection.
- `CommandLineValue(string Raw, DateTimeOffset CollectedAt)` is memory-only.
- `AnalysisIdentity(string Value)`, `ResolvedEntity` (process or service, identity,
  ownership candidates, typed facts, evidence IDs, and origin dependencies).
- `EvidenceRecord`: ID, origin, observed time, optional expiry, kind, bounded normalized
  facts, and optional source URL; never a raw command-line field.
- `Assessment`: axis, typed value, reason, confidence, evidence IDs, expiry, origins.
  Axes/values/confidence follow the spec's classification table.
- `RuleDefinition`: versioned ID, state, origin, review/expiry, predicates, per-axis
  assessments, evidence IDs, specificity, and optional superseded ID.
- `Predicate`: allowlisted kind, field, bounded value, required-evidence IDs.
- `LocalAnalysis`: assessments, ownership, matching rule IDs, missing/conflicting facts,
  and unresolved axes; `AnalysisUpdate`: entity identity, state, local result, optional error.
- `AnalysisPolicy`: automatic enabled, research enabled, command-line mode, provider
  settings, local-model settings, caps, response limits, revision number.
- `AnalysisPayload`: independently sanitized metadata, permitted evidence, optional
  command line, policy revision, destination; `ResearchRequest` has sanitized identity
  terms only. Provider results use `AnalysisProposal` with assessments and proposed rules.
- `ProviderCapabilities`: structured-output support and research availability; no
  universal detection endpoint is assumed. `ProviderError`: Auth, RateLimited,
  Transient, Unsupported, Malformed, Refused, Cancelled.
- `AnalysisDestination`: LocalRules, LocalModel, CloudModel; `RuleState`: Active,
  Disabled, Rejected; `RuleValidationResult`: accepted flag and rejection reasons.
- `KnowledgeEntry`: identity, current/historical analyses, evidence, candidate rules,
  and scoped corrections. `ClearScope`: GeneratedKnowledge or UserCorrections.
- `EvidenceChange`: changed/withdrawn evidence IDs and old/new executable/context facts.
- `UsageKind`: AutomaticJob or BillableCall; `UsageLimits`: maximum jobs and calls.
  The ledger uses the client's local calendar date and survives generated-knowledge resets.
- `ProviderResult`: proposal or error, billable call count, tool usage, optional retry-after;
  `ResearchResult`: evidence or error with the same usage fields.
- `ProviderSettings`: ID, endpoint URI, chosen model, configured capability expectations;
  `PresentationPreferences`: theme, columns, grouping, sort, and page selection.
- `PageId`: Processes, Services, Knowledge, Settings; `NavigationState`: current page
  and Select operation. Define presentation-only state in Application/Presentation.

Plan-level bounds: 60-second HTTP timeout, 256 KiB provider response limit, 8 KiB
per research excerpt, 10 evidence sources per research job, 64 predicates per rule,
and 512 characters per predicate value. Bound failures are explicit typed errors.
Full-command-line input is never silently shortened to fit these bounds.

Collection DTOs are never serialized to persistence or HTTP. Only explicit projections
cross those boundaries. Runtime raw strings and provenance stay within their policy scope.

## Task 1: Native shell and testable contracts

**Files:** Create the root build files, four library projects (Core, Application,
Windows, Infrastructure), App project, test projects, Core Models `IdentityModels.cs`,
`SnapshotModels.cs`, `AnalysisModels.cs`, `RuleModels.cs`, `PolicyModels.cs`,
`KnowledgeModels.cs`, App `App.xaml(.cs)` and
`MainWindow.xaml(.cs)`, Application `Presentation/NavigationState.cs`,
`tests/ProcSight.Core.Tests/ContractTests.cs`,
`tests/ProcSight.Application.Tests/NavigationTests.cs`, `README.md`, `.gitignore`,
`scripts/build.ps1`, `scripts/start.ps1`.

**Interfaces:** Produces all shared contracts above and a shell navigation state
`NavigationState.Select(PageId page)` with Processes, Services, Knowledge, Settings.
Consumes no existing product code. App references all libraries; Application references
Core; Windows references Core/Application; Infrastructure references Core/Application.

- [ ] Create build/test projects and synthetic fixtures as the prerequisites for the
  contract tests; resolve and pin package versions and SDK. Configure unpackaged WinUI,
  nullable references, resource-index publication, and x64; exclude test files from App.
  Core/Application/Infrastructure test projects target net10.0; Windows tests target
  net10.0-windows10.0.26100.0. Fixture files copy to test output via explicit project items.
- [ ] Write tests `MissingStartTimeRequiresSnapshotScope` and
  `SelectingServicesChangesCurrentPage`: assert invalid identity throws and Services
  becomes the selected page. Run `dotnet test --filter "FullyQualifiedName~ContractTests|FullyQualifiedName~NavigationTests"`
  and confirm behavioral failure before implementing validation/navigation.
- [ ] Implement records, identity validation, and navigation state; build the XAML shell
  with native theme resources and 24/16/8/4 spacing tokens. No process mutation commands.
- [ ] Re-run contract tests; run `./scripts/build.ps1 -Configuration Debug`, then
  `./scripts/start.ps1`. Confirm native window opens and all four pages are reachable;
  missing runtime/build prerequisites produce actionable launch documentation.
- [ ] Commit the deliverable and README with `feat: establish native ProcSight shell`.

## Task 2: Live process and service collector

**Files:** Create Windows `Collection/IWindowsSnapshotSource.cs`,
`WindowsSnapshotSource.cs`, `ProcessSampler.cs`, `ServiceEnumerator.cs`,
`ProcessMetadataReader.cs`, `WindowsNativeMethods.cs`; tests `CollectorTests.cs`
and `CollectorIntegrationTests.cs`; App `ViewModels/ProcessesViewModel.cs`,
`ViewModels/ServicesViewModel.cs`, `Views/ProcessesPage.xaml(.cs)`,
`Views/ServicesPage.xaml(.cs)`; update composition root and README.

**Interfaces:** `IWindowsSnapshotSource.CaptureAsync(CommandLineMode mode,
CancellationToken ct) -> Task<MachineSnapshot>` and
`ProcessSampler.Sample(ProcessKey key, TimeSpan? totalCpu, long? workingSet,
DateTimeOffset now, int logicalProcessors) -> ResourceSample`.

- [ ] Write `FirstSampleIsPending`, `PidReuseResetsCpuDelta`,
  `ProtectedProcessKeepsPartialRow`, `TwoServicesShareOneHost`, and
  `UnavailableNetworkIsNotZero`. Assert no stale delta and both service names preserved.
  Run `dotnet test --filter "FullyQualifiedName~CollectorTests"`; confirm failures.
- [ ] Implement enumeration with Windows process handles and service-control manager
  APIs, safe handles, background metadata reads, CPU interval sampling, and one-second
  active-page refresh. Use QueryFullProcessImageName/GetProcessTimes where appropriate;
  service enumeration is read-only. Disk/network initially remain explicitly unavailable.
- [ ] Use bounded best-effort Windows command-line retrieval when mode permits (WMI
  Win32_Process CommandLine via System.Management); Off does not request that field.
  Return full available strings; do not manually split Windows quoting into argv.
- [ ] Bind immutable snapshots into the virtualized local tables; services include stopped
  entries and host links. Keep selection by ProcessKey, not row index or bare PID.
- [ ] Run collector tests and Windows integration checks: current process exists, a stopped
  service can appear, access denial is recoverable, cancellation ends polling. Launch App
  and inspect live CPU/memory and services. Commit `feat: collect live processes and services`.

## Task 3: Executable identity and application grouping

**Files:** Create Core `Identity/IdentityResolver.cs`, `ParentLinkValidator.cs`,
`OwnershipResolver.cs`; Windows `Collection/ExecutableEnricher.cs`,
`SignatureVerifier.cs`, `ExecutableHashCache.cs`, `PackageIdentityReader.cs`;
tests `IdentityTests.cs`, `ExecutableEnrichmentTests.cs`; update ProcessesViewModel.

**Interfaces:** `IdentityResolver.Resolve(MachineSnapshot snapshot,
IReadOnlyList<EvidenceRecord> evidence) -> IReadOnlyList<ResolvedEntity>`;
`ExecutableEnricher.EnrichAsync(ExecutableMetadata metadata, CancellationToken ct)
-> Task<ExecutableMetadata>`. AnalysisIdentity includes executable identity and
relevant owner/service context, independent of resource utilization and PID.

- [ ] Write fixtures/tests `SameNodeDifferentParentsStaySeparate`,
  `ParentStartedAfterChildIsUnverified`, `CompanyNameDoesNotVerifySigner`,
  `SharedHostRetainsSeparateServices`, `ChangedFileInvalidatesHash`, and
  `AccessDeniedLeavesOwnershipUnknown`. Run the IdentityTests filter; confirm failures.
- [ ] Implement verified parent lifetime links, package/version evidence, file hashing,
  and Windows signature verification using WinVerifyTrust with no UI. Distinguish unsigned,
  invalid, unavailable, and verified; never turn company metadata into signer evidence.
- [ ] Implement ownership groups from verified relationships and metadata; show unverified
  candidates as candidates. Cache expensive enrichment using file identity/change facts;
  handle replacement during hashing by rejecting the inconsistent result.
- [ ] Run identity/enrichment tests and inspect grouped/raw views with two synthetic Node
  applications and live processes. Commit `feat: explain local executable ownership`.

## Task 4: Command-line modes and privacy projections

**Files:** Create Core `Privacy/AnalysisProjection.cs`, `CommandLinePolicy.cs`,
`MetadataSanitizer.cs`, `OutputPrivacyFilter.cs`, `OriginPolicy.cs`;
tests `PrivacyTests.cs`; App `ViewModels/SettingsViewModel.cs`,
`Views/SettingsPage.xaml(.cs)` and `Views/PayloadPreviewDialog.xaml(.cs)`.

**Interfaces:** `AnalysisProjection.Build(ResolvedEntity entity, AnalysisPolicy policy,
AnalysisDestination destination) -> AnalysisPayload`;
`BuildResearchRequest(ResolvedEntity entity) -> ResearchRequest`;
`OutputPrivacyFilter.Project(AnalysisProposal proposal, AnalysisPayload request)
-> AnalysisProposal` rejects unsafe persisted text/predicates;
`OriginPolicy.CanShare(IReadOnlySet<DataOrigin> origins, AnalysisPolicy policy) -> bool`.

- [ ] Write `OffOmitsCommandLine`, `LocalOnlyCloudCannotSeeRawOrDerivedText`,
  `OnlineFullPreservesQuotedCommandVerbatim`, `ResearchNeverSeesCommandLine`,
  `UserPathsNormalizeWithoutChangingSigner`, `EchoedSecretIsNotPersistable`, and
  `PolicyRevisionChangeRejectsPreparedPayload`. Use a synthetic quoted path and token
  sentinel; assert exact verbatim equality online and sentinel absence elsewhere.
  Run PrivacyTests and confirm failures.
- [ ] Implement explicit outbound and persistence projections. Local-only origin propagates
  through dependent assessments/evidence/rules; cloud payloads are rebuilt from shareable
  facts rather than reusing local explanations. No raw command-line persistence serializer
  exists. LocalCommandLine-origin knowledge remains non-shareable even after switching
  to OnlineFull; new online analysis uses a newly authorized projection.
- [ ] Add Off/Local only/Include in online analysis settings and payload preview. Explain
  that online full is verbatim and may contain secrets. Mode changes increment revision,
  release raw strings on Off, and invalidate prepared work; show omitted/oversize reasons.
- [ ] Reject proposed rule literals or generated persisted text that echo raw lines or
  private argument values. Permit only bounded structural command-role/path predicates;
  retain origin tags, never arbitrary argument strings. Raw detailed model output may be
  viewed only in the transient detail session if it cannot pass persistence projection.
- [ ] Run privacy tests and check all three modes with fake providers and settings UI.
  Commit `feat: control local and online command-line analysis`.

## Task 5: Knowledge, preferences, credentials, and budgets

**Files:** Create Application `Knowledge/IKnowledgeStore.cs`,
`Settings/IPreferencesStore.cs`, `Settings/ICredentialStore.cs`, `Budget/IUsageLedger.cs`;
Infrastructure `Knowledge/SqliteKnowledgeStore.cs`, `KnowledgeSchema.cs`,
`KnowledgeRetention.cs`, `KnowledgeRecovery.cs`; Windows `Storage/PreferencesStore.cs`,
`CredentialStore.cs`; tests `KnowledgeStoreTests.cs`, `SettingsStoreTests.cs`.

**Interfaces:** Store `LoadAsync(AnalysisIdentity id, CancellationToken ct)` returns
`Task<KnowledgeEntry>`; `ReadCandidatesAsync(ResolvedEntity entity, CancellationToken ct)
-> Task<KnowledgeEntry>` adds relevant family rules and corrections across identities;
`SaveAsync(KnowledgeEntry entry,
CancellationToken ct)`; `SetRuleStateAsync(Guid id, RuleState state, CancellationToken ct)`;
`ClearAsync(ClearScope scope, CancellationToken ct)`; `PruneAsync(DateTimeOffset now,
CancellationToken ct)`. Preferences Load/Save AnalysisPolicy plus PresentationPreferences.
Credentials Get/Set/Remove by provider ID. Ledger
`TryReserveAsync(UsageKind kind, DateOnly localDate, UsageLimits limits, CancellationToken ct)
-> Task<bool>` atomically counts automatic jobs and billable calls, including retries.

- [ ] Write `RestartPreservesCorrections`, `LocalOriginsSurviveRestart`,
  `RawCommandNeverStored`, `NinetyDayPruneKeepsActiveEvidence`,
  `InterruptedMigrationRollsBack`, `CorruptionPreservesOriginal`,
  `InterruptedPreferenceWriteRetainsPolicy`, `ClearKnowledgeDoesNotResetUsage`, and
  `ConcurrentSixtyFirstCallDenied`. Run KnowledgeStoreTests/SettingsStoreTests; confirm failures.
- [ ] Implement schema-version migrations, SQLite transactions, indexed identity/rule/evidence
  dependencies, preferences atomic replacement, and credential protection using Windows
  Credential Manager. Persist ledger separately from clearable generated knowledge.
- [ ] Store under `%LOCALAPPDATA%/ProcSight`; reject raw collection DTOs and unsafe output
  at the persistence boundary. Preserve corrupt database and offer explicit recovery;
  invalid privacy settings disable online analysis until repaired rather than resetting it.
- [ ] Run temporary-database restart/fault tests and Windows credential set/get/remove using
  synthetic values without printing them. Update architecture/storage documentation.
  Commit `feat: persist private analysis knowledge and usage limits`.

## Task 6: Declarative rules and corrections

**Files:** Create Core `Rules/RuleValidator.cs`, `PredicateMatcher.cs`,
`LocalAnalysisEngine.cs`, `RuleInvalidation.cs`, `CorrectionResolver.cs`;
tests `RuleEngineTests.cs`, fixtures `rules.json`.

**Interfaces:** `RuleValidator.Validate(RuleDefinition rule, IReadOnlyList<EvidenceRecord>
evidence, DateTimeOffset now) -> RuleValidationResult`;
`LocalAnalysisEngine.Evaluate(ResolvedEntity entity, KnowledgeEntry knowledge,
DateTimeOffset now) -> LocalAnalysis`;
`RuleInvalidation.FindAffected(EvidenceChange change, IReadOnlyList<RuleDefinition> rules)
-> IReadOnlySet<Guid>`.

- [ ] Write `NameOnlyRuleRejected`, `MissingSignerDoesNotMatch`,
  `AllAxesActivateAfterValidation`, `HashThreatDoesNotGeneralizeToFamily`,
  `ConflictingRulesQueueUnresolvedAxis`, `CorrectionDoesNotHideIndependentThreat`,
  `PurposeExpiresAfterThirtyDays`, `SecurityAndOptionalityExpireAfterSevenDays`,
  `DisabledRuleInvalidatesDependents`, `CommandRoleRuleRetainsLocalOrigin`.
  Run RuleEngineTests and confirm failures.
- [ ] Implement bounded allowlisted exact/prefix/path-root predicates; no scripts,
  arbitrary regex, or model-supplied operations. Family matches require independent
  evidence families, not duplicate comparisons of the same field. Exact hash specificity
  outranks family candidates, while incompatible eligible findings remain visible.
- [ ] Apply corrections in explicit scopes, freshness caps and source shorter expiries,
  lineage, and change invalidation. Known-threat output requires matched attributable
  evidence. Keep invalid generated proposals rejected and inspectable.
- [ ] Run fixtures across same/similar/different binaries, changed owners, unavailable
  signatures, and altered command-role contexts. Commit `feat: apply reusable evidence-based rules`.

## Task 7: Analysis provider adapters, including local models

**Files:** Create Application `Providers/IAnalysisProvider.cs`; Infrastructure
`Providers/OpenAiResponsesProvider.cs`, `CompatibleChatProvider.cs`,
`LocalEndpointPolicy.cs`, `AnalysisResponseSchema.cs`, `ProviderResponseValidator.cs`,
`ProviderCapabilitiesProbe.cs`; tests `ProviderTests.cs` and fake HTTP fixtures.

**Interfaces:** `IAnalysisProvider.GetCapabilitiesAsync(CancellationToken ct)
-> Task<ProviderCapabilities>`;
`AnalyzeAsync(AnalysisPayload payload, CancellationToken ct) -> Task<ProviderResult>`.
ProviderResult contains AnalysisProposal or typed ProviderError, request usage, and
retry-after when supplied. Providers receive projected payloads, never ResolvedEntity.

- [ ] Write `ResponsesRequestUsesStrictSchema`, `RefusalIsNotMalformedSuccess`,
  `UnknownEvidenceReferenceRejected`, `UnsupportedSchemaFallsBackToValidatedJson`,
  `LocalOnlyRejectsRemoteEndpoint`, `LoopbackRedirectNeverFollowed`,
  `OversizeCommandIsExplicitlyOmitted`, `ApiKeyNeverLogged`. Run ProviderTests; confirm failures.
- [ ] Implement OpenAI Responses HTTP adapter with JSON-schema output and explicit refusal,
  truncation, rate-limit, and authentication handling. Use selected model configuration,
  no hardcoded latest model, and no network/model call during ordinary app startup.
- [ ] Implement compatible chat-completions adapter and loopback variant with redirects
  disabled, bounded response reads, timeouts, and explicit capability probes on setup.
  Do not infer support from an endpoint name. Lack of structured-output support falls
  back to ordinary JSON plus the same strict local validation, not a trusted free-form rule.
- [ ] Keep research tools out of command-line-bearing analysis requests. Validate all
  returned evidence IDs against submitted or fetched evidence and project generated output
  through Task 4 before persistence. Provider failure never changes machine state.
- [ ] Run fake HTTP success/error/redirect tests. Offer an explicit manual connection test
  without requiring live credentials in automated checks. Commit `feat: add bounded analysis providers`.

## Task 8: Sanitized online research and evidence provenance

**Files:** Create Application `Research/IResearchProvider.cs`; Infrastructure
`Research/OpenAiWebResearchProvider.cs`, `ResearchSourceValidator.cs`;
tests `ResearchTests.cs`; update provider settings capabilities.

**Interfaces:** `IResearchProvider.ResearchAsync(ResearchRequest request,
CancellationToken ct) -> Task<ResearchResult>` returns bounded EvidenceRecords and
provider-call/tool usage or typed errors. Inputs cannot contain AnalysisPayload or raw lines.

- [ ] Write `ResearchQueryHasIdentityOnly`, `CitationMustComeFromToolResult`,
  `UnknownSourceIsNotEvidence`, `UnsupportedResearchLeavesAnalysisAvailable`,
  `WithdrawnEvidenceInvalidatesAssessment`, `WebInstructionCannotRequestLocalFiles`.
  Run ResearchTests; confirm failures.
- [ ] Implement separate OpenAI Responses web-search request using sanitized identity terms,
  explicit enabled capability, bounded results, and citation/tool-result parsing. Preserve
  URL, retrieval time, and supported claims; do not manufacture source links from model prose.
- [ ] Keep excerpts as untrusted evidence with applicability/confidence; source text cannot
  grant local tools or expand predicates. Compatible/local providers without a configured
  research adapter remain metadata-only and disclose this limitation.
- [ ] Run research tests; update architecture docs with the two separate HTTP flows. Record
  web-tool usage as well as HTTP calls without claiming the call cap is a monetary limit.
  Commit `feat: research process identities with attributable evidence`.

## Task 9: Local-first automatic analysis coordinator

**Files:** Create Application `Analysis/AnalysisCoordinator.cs`, `AnalysisQueue.cs`,
`AnalysisState.cs`, `AnalysisFailurePolicy.cs`; tests `CoordinatorTests.cs`.

**Interfaces:** `AnalysisCoordinator.ObserveAsync(MachineSnapshot snapshot,
AnalysisPolicy policy, CancellationToken ct) -> Task`;
`RequestReanalysisAsync(AnalysisIdentity identity, CancellationToken ct) -> Task`;
`Updates(CancellationToken ct) -> IAsyncEnumerable<AnalysisUpdate>`;
`ChangePolicyAsync(AnalysisPolicy policy, CancellationToken ct) -> Task`.
Consumes Tasks 2-8 interfaces; uses TimeProvider for deterministic intervals/expiry/retries.

- [ ] Write `MatchingRuleMakesZeroProviderCalls`, `EquivalentInstancesShareOneJob`,
  `DifferentContextNeedsAnalysis`, `ResourceChangeDoesNotReanalyze`,
  `UnknownUnchangedDoesNotLoop`, `TwentiethJobAllowedTwentyFirstPaused`,
  `SixtiethBillableCallAllowedSixtyFirstPaused`, `TransientRetriesAtMostTwice`,
  `AuthFailurePauses`, `RevokeOnlineBeforeDispatchRebuildsPayload`,
  `ExitedProcessResultNeverAttachesToReusedPid`, `ShutdownCancelsQueuedWork`.
  Run CoordinatorTests; confirm failures.
- [ ] Implement local evaluation/cache eligibility, one-flight queue, identity deduplication,
  per-job/per-call atomic reservations, cancellation, and structured state updates.
  Unchanged unknown/failure results are suppressed until metadata/policy/evidence changes
  or explicit re-analysis; explicit requests do not bypass budgets.
- [ ] Dispatch only after a final policy-revision check. Local-only command-bearing model
  requests use the loopback provider; unresolved cloud dimensions receive independently
  sanitized metadata. Cancel/rebuild pending work on mode changes and release raw data.
  Command-role changes affect contextual analysis identities through a local keyed digest
  (key in Task 5 credential storage), never raw arguments. Tag these identities with their
  origin; use separate shareable cloud identities when command-line data is local-only.
- [ ] On success validate proposals, normalize private output, save one transaction with
  provenance/lineage, then re-evaluate locally. Retry only transient/rate-limit failures
  within cap, at most twice, honoring retry-after; authentication pauses until setup changes.
- [ ] Run coordinator tests with fake clock/providers/ledger, including offline and conflicting
  rule paths. Commit `feat: coordinate local-first automatic analysis`.

## Task 10: Complete investigation and knowledge UI

**Files:** Update App composition, existing page/view-model files; create
`Views/EntityDetailView.xaml(.cs)`, `Views/KnowledgePage.xaml(.cs)`,
`Views/CorrectionDialog.xaml(.cs)`, `ViewModels/EntityDetailViewModel.cs`,
`KnowledgeViewModel.cs`; Application `Presentation/ProcessListProjection.cs`,
`Presentation/ViewOptions.cs`, `Presentation/ProcessListState.cs`;
tests `PresentationTests.cs`. These projections do not depend on WinUI.

**Interfaces:** `ProcessListProjection.Build(MachineSnapshot snapshot,
IReadOnlyList<ResolvedEntity> entities, IReadOnlyList<AnalysisUpdate> analysis,
ViewOptions options) -> ProcessListState` (rows, groups, selection keys, incomplete totals).
ViewOptions includes grouped/raw mode, search, sort, classification filters.

- [ ] Write `SearchMatchesPublisherPidAndService`, `IncompleteGroupDoesNotClaimFullTotal`,
  `SelectionSurvivesSortAndExit`, `SharedHostLinksAllServices`,
  `StaleAssessmentIsLabeledHistory`, `LocalOnlyProvenanceVisible`,
  `DisableRuleRefreshesDependentRows`, `CorrectionScopeExplicit`.
  Run PresentationTests; confirm failures.
- [ ] Implement virtualized table, expandable ownership groups/raw tree, sortable resource
  columns, classification/confidence text, services page, and adaptive detail pane.
  Observe snapshot/update streams through the dispatcher without full-table rebuilds.
- [ ] Add knowledge inspect/disable/delete/re-analyze, rejected proposals, correction scopes,
  separate clear-generated/corrections/credentials controls, provider/local-model setup,
  command-line preview, research toggle, budget indicators, and all analysis states.
- [ ] Show evidence/URLs/dates and available/missing facts; links open only on user action.
  Show raw command lines only when mode permits and only in the transient detail session.
  Every assessment carries purpose/security/optionality separately; color is supplementary.
- [ ] Run projection tests; inspect keyboard navigation, narration labels, light/dark,
  narrow/wide layouts, and 100/150/200% scaling. Verify no management commands appear.
  Commit `feat: present explainable process investigations`.

## Task 11: Developer release verification and documentation

**Files:** Create `scripts/verify.ps1`, `tests/Fixtures/churn-500.json`,
`tests/ProcSight.Application.Tests/EndToEndTests.cs`, `docs/verification.md`,
`docs/architecture.md`, `CHANGELOG.md`; update README/build/start scripts.

**Interfaces:** `verify.ps1 -Configuration Release` runs restore/build/tests and reports
failures with nonzero exit; optional `-WindowsIntegration` runs host checks. UI inspection
and opt-in live provider smoke tests are documented separately from fake HTTP checks.

- [ ] Write `FirstUnknownThenStoredRuleThenZeroCallsAfterRestart`,
  `ChangedOwnerForcesFreshAnalysis`, `LocalOnlyKnowledgeNeverLeaksAfterRestart`,
  `OfflineLiveExplorerStillWorks`, `Churn500KeepsIdentityAndSelectionStable` using
  fixture snapshots, temporary SQLite, and fake providers. Run EndToEndTests; confirm failures.
- [ ] Connect remaining composition/lifecycle boundaries until those tests pass. Keep
  shutdown cooperative and error UI actionable; add no unrelated features.
- [ ] Run locked restore, Release build, all fixture/domain/adapter tests, and Windows
  integration checks through verify.ps1. Record commands, counts, and any unavailable
  Windows observations rather than claiming full metric coverage.
- [ ] Launch the native Release app on Windows 11; check 500-row fixture responsiveness
  during analysis, live process churn, service host links, command modes, credential-free
  behavior, budgets, and corruption recovery. Record responsiveness observations and
  machine conditions in verification.md. Test actual full-command-line routing only
  with a synthetic non-secret process and user-configured endpoint.
- [ ] Update README prerequisites, exact build/launch commands, data locations/privacy,
  provider/research capabilities, unavailable disk/network and deferred GPU, plus phases
  2/3 roadmap. Add current architecture/component and privacy-boundary diagrams.
- [ ] Review spec coverage and final diff. Commit `docs: verify and document ProcSight v1`.

## Official implementation references

Checked during planning on 2026-10-13; re-check request shapes when implementing adapters.
No real API calls or credentials were used for planning.

- [Microsoft unpackaged sample](https://learn.microsoft.com/en-us/samples/microsoft/windowsappsdk-samples/unpackaged/)
  supports WindowsPackageType=None and unpackaged runtime initialization.
- [OpenAI structured outputs](https://developers.openai.com/api/docs/guides/structured-outputs)
  describes schema-constrained output and refusal handling; use strict local validation too.
- [OpenAI web search](https://developers.openai.com/api/docs/guides/tools-web-search)
  describes Responses web-search tools and attributable citations. It is a separate
  sanitized research request in ProcSight, not a tool enabled on full-command-line analysis.

## Coverage and execution handoff

Collection/identity: Tasks 1-3. Command-line modes/privacy: Tasks 4, 7, 9.
Knowledge/preferences/credentials/budgets: Task 5. Rules/classifications: Task 6.
Providers/research: Tasks 7-8. Local-first orchestration/failure behavior: Task 9.
Full native investigation experience: Task 10. Windows acceptance/developer release: Task 11.
Phases 2/3: roadmap in spec/README only.

Recommended execution is native: the tasks share evolving identity/provenance contracts,
so one implementer can maintain consistency with less repeated context. Subagent-driven
execution is also available if the user prefers independent per-task review. Neither
method starts before the user reviews this plan and chooses the method.
