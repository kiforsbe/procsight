# AI activity log revision

The user requests product-level activity information: which processes/services are queried and
what explanations are returned, rather than raw transport/debug dumps. Add a native AI Activity
page with bounded in-memory history, live statuses, provider/model, process PID or service identity,
safe metadata summary, timing, retry attempts, returned assessment axes/reasons/confidence,
proposed-rule counts, and failures/cancellation. Include explicit synthetic connection tests and research.

Activity must correspond to actual provider calls, never imply a local cache hit was sent online.
Show the submitted metadata categories and whether command context was included, but never retain
raw command lines, tokens, authorization URLs, paths or window captions in the log. Returned
explanations use the existing output privacy projection. Retain at most 200 entries; support
pause/resume recording, Clear, readable selection details and status filtering. No file logs/export.
Diagnostics must never cause provider work to fail. Policy cancellation and retries remain visible.
Use synthetic tests and native fixture rendering; maintain architecture/UML and privacy documentation.
