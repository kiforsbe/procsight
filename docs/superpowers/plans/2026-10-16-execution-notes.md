# Execution notes: connections, tray and administrator mode

Implemented on the user-approved existing feature branch, with no merge, push or CI/CD changes.
The full Release verification passed 330 tests. Independent review identified seven material
issues; all were fixed with targeted regressions and a passing full suite. No minor findings
were deferred. See [verification](../../verification.md) for evidence and live acceptance gaps.

Implementation rulings, in execution order:

1. Use PowerShell bookkeeping instead of unavailable Bash helpers. Plan-scoped ledgers retained
   task/test history; the cost is losing automatic helper-generated briefs, not product behavior.
2. Preserve the destination of a legacy LocalModel independently of a synthetic provider-kind
   string. This retains existing adapter/test compatibility; getting it wrong would project local
   requests as cloud requests. The provenance regression passes.
3. Integrate immediate-settings and connection UI tasks in one commit because they share draft
   state. The cost is coarser commit-level review; independent whole-change review still ran.
4. Integrate related tray assets, sampler and native lifecycle in one working feature commit.
   The cost is coarser commit granularity; focused tests cover metric and registration contracts.
5. Cache effective elevated policy separately from saved/desired settings to prevent per-tick
   policy churn. Getting it wrong could suppress analysis or restore old sharing; session/race
   tests now cover the boundary.
6. Returning to normal privilege uses explicit unelevated-shell reopening guidance. No elevated
   child is represented as unelevated; the tradeoff is a manual reopen instead of one-click restart.
7. Leave live UAC/alternate-account/bootstrap, Explorer restart, native menu/DPI/accessibility and
   metric-accuracy acceptance explicitly unverified. Synthetic checks cannot establish those OS
   behaviors; release readiness must wait for the documented desktop acceptance.

Protected API-key records additionally bind endpoint/provider kind, including during migration.
Plain preference edits cannot redirect a key to another destination or credential namespace.
