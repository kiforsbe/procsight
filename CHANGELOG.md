# Changelog

Notable changes to ProcSight are recorded here. Unreleased entries describe implemented work;
plans, implementation details and validation records belong in the project documentation.

## [Unreleased]

### Added

- Drag-and-drop development-certificate trust helper with validation, administrator approval and
  signed MSIX installation instructions.
- Native Windows 11 process/service explorer with live CPU/memory sampling, ownership grouping,
  collapsible process trees, search and classification filters.
- Configurable table columns with resizing, reordering, saved layouts and additional local metadata;
  Name remains first and visible.
- Context-aware investigation menus, multi-selection and local CSV/JSON/TSV export with private-field
  opt-ins, group deduplication and spreadsheet-formula protection.
- Structured process/service details covering assessments, commands, windows, file identity,
  signatures, metadata availability and supporting evidence.
- Readable host/workload names for scripts, modules, browser workers and extension hosts, with
  local window captions and asynchronously loaded executable icons.
- Searchable Rules & knowledge library with purpose/security/optionality assessments, AI provenance,
  matching versus contributing rules, corrections and rule inspection/disable/delete.
- Bundled catalog of 373 portable process/service definitions with verified metadata gates and
  local command rules for common runtimes and hosts.
- OpenAI, compatible API and loopback-local analysis connections, protected credentials, payload
  previews and persistent request limits.
- ChatGPT-plan sign-in, account-specific model discovery and renewable protected sessions,
  separate from API-key billing.
- Optional web research within ChatGPT/OpenAI analysis, with attributable sources and public-only
  identity metadata; separate research connections remain explicitly selectable.
- Memory-only AI activity history for requests, results, retries and tests, bounded to 200 entries
  with pause, filter and clear controls.
- Immediate native Settings with independent connection profiles, enable/priority controls,
  per-connection configuration/testing/removal and explicitly enabled fallback.
- App branding and an opt-in system-tray graph for CPU, memory, disk, network or GPU load,
  including restore actions and minimize-to-tray.
- Explicit administrator restart with startup recovery and session-only sharing consent;
  ordinary startup stays unelevated and collection remains read-only.
- Direct/Store MSIX profiles with isolated packaging, content inspection, signing tools and optional
  App Installer feeds; packaged data and credentials use separate namespaces.
- Central product versioning and local SemVer release preparation that synchronizes versions,
  changelog, packages and annotated tags without pushing or publishing.
- VS Code build/debug tasks, locked Release verification, synthetic native UI fixtures and
  repeatable UI/rules benchmarks; packaging/release scripts require PowerShell 7+.
- Maintained Windows 11 design guidelines, UML architecture, developer/security documentation
  and GitHub's standard AGPL v3.0 license.

### Fixed

- Evidence-backed known-purpose analysis creates an exact-binary rule when the provider proposes none, preserving
  names across sessions; older entry-level names also survive SQLite reloads.
- Shared column transforms, cached display metadata and batched explicit sorts reduce UI stalls
  while preserving selection and incremental live updates.
- Local rule evaluation reuses one evidence index and skips unrelated-rule validation,
  reducing allocations without weakening matching-rule checks.
- Full columns follow the mouse during reordering; invisible resize targets span both sides of
  dividers, and full-height headers toggle ascending/descending sorting.
- Table headers stay aligned with rows during resizing and horizontal scrolling;
  vertical scrollbars remain accessible in narrow windows.
- Detail sections retain expansion choices across selections, while resource updates preserve
  the selected item's detail content and scroll position.
- Single-process applications no longer have redundant group headers; group transitions preserve
  stable keys, selection and sibling order, with working native expand/collapse chevrons.
- Restricted processes retain stable lifetime scopes when Windows kernel sequence numbers are
  available; unavailable metadata shows safe reasons instead of repeated access-denied failures.
- Name-only protected processes no longer consume repeated automatic analysis jobs;
  limited manual analysis remains available.
- Established application names and specific local roles take precedence over explanatory AI prose
  or generic catalog labels.
- Service icons use implementation/host sources with a native gear fallback;
  implementation replacement invalidates saved service identity.
- Command-role matching treats interpreter payloads as opaque, avoiding false worker labels
  from quoted code or arguments.
- ChatGPT inference retains valid streamed output when successful completion has an empty output
  array, while still requiring completion and validation.
- ChatGPT model discovery handles larger catalogs, dialog reattachment and stale responses;
  account/model controls adapt without clipped content.
- AI failures and rejected rules show safe reasons and recovery guidance, including explicitly
  unavailable diagnostics for older records.
- Rule validation handles certificate names, path-root comparisons and private invocation scope
  correctly while preserving conflicts and user corrections.
- Concurrent settings/key edits, permission changes and request-limit reductions preserve
  destination authorization; authentication pauses and tray recovery remain stable.
- Memory heat updates when total RAM changes, even if process memory text stays unchanged.

### Changed

- Command lines are always available locally when Windows permits; online commands and identifying
  metadata remain excluded by default, with separate sharing controls.
- Command-derived rules retain keyed invocation scopes instead of raw arguments, preventing reuse
  across unrelated scripts; legacy unscoped rules require fresh analysis.
- Ownership distinguishes equal product names at different paths, while generic Windows product
  metadata falls back to specific file descriptions or executable names.
- Expired findings trigger reevaluation, and older observations cannot restore superseded
  analysis permissions.
- ChatGPT request allowances are admission controls, not hard tool-call or monetary caps.
- Consolidated Unreleased notes and required concise, informative entries for future changelog updates.

### Known limitations

- Unreleased development preview: full provider/desktop accessibility acceptance, installation/trust
  and Store certification remain outstanding; per-process disk/network/GPU metrics are unavailable.
- No GitHub remote or private vulnerability-reporting channel is configured in this checkout;
  CI/CD workflows are intentionally excluded.
