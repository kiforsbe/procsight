# Analysis research and decision diagnostics revision

User-directed revision, 2026-10-17. Supersedes the earlier API-key-only research route and
separate preliminary search for same-connection ChatGPT/OpenAI analysis. Implementation was
explicitly requested in the current checkout; no remote publication or CI/CD is included.

When research is enabled, the primary ChatGPT or OpenAI connection searches in the analysis
request. An explicit different research profile retains the separate step. A disabled explicit
profile does not silently select another account or billing route. Local analysis remains local.
Tools-enabled analysis receives public identity metadata only: raw commands/private fields and
command-derived origins are excluded even when ordinary online command sharing is permitted.

Accept findings only after successful completion and strict structured validation. Web evidence
references must correspond to actual returned tool sources or citations. Map validated source URLs
to internal IDs; retain at most ten records, prioritizing referenced sources. Claims remain untrusted
and cannot establish local authenticity or replace exact-hash threat intelligence. Plan requests omit
unsupported max_tool_calls/max_output_tokens; request allowances do not promise a remote spending cap.

Persist typed app-owned rejection causes from original and final validation, never model-supplied
diagnostics. Explain legacy missing causes honestly. Only the coordinator can append private invocation
scope; defer that single validation requirement until then. Keep matching and privacy predicate semantics
consistent. Active Unknown is eligible knowledge, not successful identification.

Rule details put rejection/recovery and research outcome before classifications. Account configuration
fits the dialog viewport without a nested Expander. Model loading has a bounded timeout, adjacent status,
stale-result checks and reload-safe lifecycle. Synthetic native fixtures reproduce panel reattachment.

Editable UML sequence/state diagrams and code-owner links are maintained in
[ARCHITECTURE.md](../../../ARCHITECTURE.md). Verification and live synthetic smoke results belong in
[verification.md](../../verification.md); historical screenshot causes cannot be reconstructed if absent.

References checked: [plan request limitations](https://developers.openai.com/siwc/token-sharing-open-source/preview-limitations),
[plan models/inference](https://developers.openai.com/siwc/token-sharing-open-source/models-and-inference),
[Responses web search](https://developers.openai.com/api/docs/guides/tools-web-search).
