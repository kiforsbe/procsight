# ChatGPT plan implementation

Approved design: [ChatGPT plan integration](../specs/2026-10-14-chatgpt-plan-design.md).
Use the approved existing feature branch and checkout. Execute with Superpowers subagent-driven development.

1. Authentication adapter: Infrastructure/Authentication owns dynamic OAuth, JWT validation,
   loopback browser callback, protected credential chunking, account catalog, refresh/revocation and
   model discovery. Synthetic HTTP/token/callback tests precede implementation. No credentials in logs.
2. Provider adapter: Infrastructure/Providers/ChatGptPlanProvider owns bounded SSE completion,
   fixed endpoints, array input, strict output validation and redacted errors. Tests cover request
   shape, completion, malformed/truncated streams, refusal/auth/rate limits and cancellation.
3. Native integration: MainWindow.ChatGpt.cs and Settings/Runtime connect sign-in/account/model actions
   to the existing coordinator. Keep online metadata consent unchanged; use a separate provider ID
   and account binding. Never fall back to API-key billing automatically.
4. Review protocol and trust boundaries, fix findings, run scripts/verify.ps1, update living architecture
   UML, security/developer/user docs, changelog and verification. Commit locally; do not publish.

Ruling: user approval of the preceding integration design authorizes implementation without another
design approval loop. Live sign-in is performed by the user; tests use synthetic fixtures only.
