# Development verification

Recorded 2026-10-14. ProcSight is an unreleased development preview, not a completed
release certification. No real API credentials, process dumps, or paid provider requests
were used for automated verification.

## Local checks

On Windows 11 x64 with .NET SDK 10.0.301 and installed Windows App Runtime 2.2:

```powershell
./scripts/verify.ps1 -Configuration Release
```

Locked restore and native Release build passed with zero warnings/errors. Test results:

| Project | Passed | Focus |
| --- | ---: | --- |
| Core | 52 | Identity/privacy/rules and persisted opaque ChatGPT profile binding |
| Application | 66 | Ordering/Details/captions and bounded privacy-safe activity history |
| Infrastructure | 89 | Storage, OAuth/rotation/models/SSE and actual activity/retry/cancellation instrumentation |
| Windows | 20 | Native reads/icons/signatures and caption ownership/lifetime/timeout checks |
| Total | 227 | No failed or skipped tests |

Key behaviors covered include same-name owners at different paths, PID reuse, all three
classification axes, rejected name-only rules, expired referenced evidence, changed-owner
reanalysis, zero provider calls after learned-rule restart, disabled-rule cache invalidation,
unchanged failure suppression, successful knowledge expiry during a live session, daily
caps/retries, authentication pause, older-policy rejection, queued-work cancellation, and
local-only cloud projection using a shareable identity.

Privacy checks use synthetic secrets and quoted commands. They cover full-line preservation
when allowed, cloud omission in Off/Local only, exclusion from research, safe command-derived
outputs, arbitrary private rule/evidence rejection, and origin persistence after restart.
Storage checks include atomic corrections, separate clear scopes/usage accounting, retention,
and rejection of a newer schema. These focused tests are not exhaustive corruption/fault injection.

Windows integration queries the current process and available services without elevation.
A first-chance exception observer verifies that protected-process counter reads do not throw
managed Win32Exception probes. Task cancellation exceptions may still appear in a debugger
when cooperative shutdown cancels awaited operations; a normal exit code 0 is expected.

## Native UI observations

The Debug-only `--visual-fixture` mode renders the actual WinUI XAML tree with synthetic
processes and assessments, then exits. It does not collect machine metadata, load preferences,
access credentials, or send provider requests. Dark wide-table and light narrow-detail
captures were inspected. Header/row alignment and the light detail background were corrected
from those captures. The renderer omits native caption buttons and Mica composition.
The expanded-group fixture exercises expansion/collapse/re-expansion with the same selected
group through the actual window handler. Its rendering shows distinct extension-host,
renderer, and GPU-worker labels and a Shell-loaded Notepad icon. This verifies fixture state
and rendering, not physical mouse delivery. Metric-order and single-member-group regressions
are covered by portable tests; a real signed Windows service host validates catalog matching.

![Dark process table](images/processes-dark.png)

![Light narrow details](images/details-light.png)

![Expanded group with specific local roles](images/tree-groups.png)

A live credential-free launch exposed the native title bar, navigation, processes, group
counts, CPU/memory totals, and search through accessibility inspection. The computer-use
capture returned the background window, and input failed with unavailable coordinate
geometry. Therefore desktop clicks, keyboard/screen-reader behavior, and live caption/Mica
appearance are not claimed verified by that tool.

Twelve Mermaid class, sequence, and state diagrams in [ARCHITECTURE.md](../ARCHITECTURE.md) were
parsed and rendered locally. Local documentation targets and whitespace were checked.

## Outstanding acceptance work

- Run the live provider smoke test with a user-selected model and synthetic metadata;
  check structured-output and web-search support on that account/model. Fake HTTP coverage
  does not establish current hosted API compatibility.
- Check full-command-line routing with a synthetic, non-secret process against an explicit
  local/online endpoint, including live policy changes and provider-side retention behavior.
- Complete keyboard, narrator, narrow/wide, and 100/150/200% desktop scaling checks.
  The 500-row test checks projection identity/churn, not a timed native UI performance claim.
- Exercise recovery and storage fault injection while collection runs, and audit the complete
  package redistribution/license inventory before distributing executable releases.
- Finish the baseline independent whole-branch review. A fresh reviewer reported ownership-identity
  and referenced-evidence/cache freshness findings, reproduced and fixed, then stopped because
  the account usage limit prevented review completion. No independent security audit is claimed.

A focused independent review of this live-list/catalog change completed. Real certificate
subject matching, conflict/correction precedence, and preservation of specific role labels
were corrected and verified. A reported selection-action concern was checked against the
existing post-reconciliation action refresh; collapse now selects the parent explicitly.

Disk/network counters remain unavailable; GPU collection and management/cleanup actions are
deferred. Missing/inaccessible executable artwork uses a fallback native symbol.

## Signed catalog and invocation rules extension

The same Release verification covers 373 bundled references (96 process/277 service), scoped
unique IDs, actual .NET signer/product metadata, publisher-only unknown purpose, separate service
host/implementation proofs, module replacement, private service-argument exclusion, per-user
suffix constraints, and Windows servicing/Defender location gates. Runtime tests cover Python,
.NET and PowerShell roles, opaque inline code, option arity, and script-argument boundaries.
Independent focused review found two coverage/parsing defects; both have regression tests.

The catalog was curated from read-only local observations into public identifiers and references.
The real inventory remains ignored scratch data and is not distributed. Windows enrichment does
not verify actual loaded service modules or online certificate revocation. Searchable catalog
UI builds successfully; its physical desktop input remains part of outstanding UI validation.

## Host names and native table interaction revision

Available local command lines are always retained for display/rules; online sharing controls
command transmission and identifying metadata separately. Tests cover host — hosted names for
scripts/modules/MCP launchers/extensions, signed PowerToys components, retained owner grouping,
verbatim local Details, opposite sort directions, and name sorting by displayed labels.
A two-script local-model regression verifies invocation-specific learned rules cannot transfer
between unrelated Python scripts; no raw command or argument is persisted.

Single-member applications now render directly. Three additional portable regressions cover
blank Windows descriptions, stable single/group ordering, and selected-child survival. The native
groups fixture also removes siblings and verifies that the selected remaining row exposes its
PID with no group header or expander, then restores the original group.

The actual Debug XAML fixture at 660px overflows both axes, checks that the vertical scrollbar
stays inside the window and stationary during horizontal scrolling, checks full-height header
button bounds, and exercises sort direction toggling. Header and body scrolling synchronize in
both directions. These are layout/handler checks, not physical pointer delivery or full narrator
acceptance. Normal Debug and Release builds both passed with zero warnings/errors after the
user stopped the running Debug session that initially held build outputs open.

![Narrow native host/workload table with viewport scrollbars](images/host-workloads-narrow.png)

Read-only live investigation retrieved 392 available command lines from 547 observed processes.
Only executable/pattern counts and option names were displayed; raw lines and private argument
values were held in memory and were not written to the repository or diagnostics.

## Local window metadata

Synthetic caption tests verify local names, foreground selection, complete local Details,
stable analysis identity, and exclusion from serialized snapshots and every provider destination.
Native tests create/destroy off-screen windows owned by the test process, verify caption/class
collection, requested-PID filtering, changed-lifetime rejection, and bounded reads from an
unresponsive own-window thread. Window classes/captions remain transient and local.
Focused independent review caught the synchronous own-window caption risk; the timeout fix and
native regression now pass. Full desktop acceptance remains separate from these API checks.

## ChatGPT plan, Settings and AI activity

Synthetic OAuth checks cover callback state/PKCE, nonce, signed issuer/audience/expiration,
missing grants, returning client/subject mismatch, protected Unicode token chunking, rotated-token
reuse across service restart, serial renewal, mutex cancellation and revocation retry. Temporary
renewal response-body/network failures remain transient instead of permanently pausing authentication.
SSE tests require completed validated output and cover partial/refused/malformed/error streams,
exact response/output bounds, cancellation, account errors and fixed public endpoints.

The user completed browser sign-in in the Debug app. A read-only model-discovery check using
ProcSight's protected session received HTTP 200 with 10 model records and 7 visible choices.
The approximately 600 KiB catalog exposed the old 256 KiB limit; the discovery-only 2 MiB bound
now returns all 7 selectable models. Synthetic large-catalog/over-limit tests cover the regression.
Only status, counts, JSON field names and size were displayed; credentials, account identity,
model instruction content and process metadata were not printed or committed. Live inference,
live switching/revocation, and provider-side retention behavior remain outstanding.

Native Debug fixtures rendered the rebuilt Settings at wide/narrow sizes and inspected an
expanded light-theme connection panel. AI activity fixtures render success/failure/running rows
and selected returned explanations. They use synthetic entries and memory-only credentials;
no real account or machine collection is used for screenshots. Normal Debug and Release builds
passed with zero warnings/errors. These are renderer checks, not complete pointer/accessibility acceptance.

Activity tests cover the 200-entry bound, immutable safe snapshots, pause/clear/in-flight behavior,
command-bearing output projection, location/secret omission, observer failure isolation and actual
provider/research attempts/retries/cancellation. Local cache hits do not produce AI requests.

![Native Settings cards](images/settings-dark.png)

![Native AI activity with selected results](images/ai-activity.png)
