# ChatGPT plan integration

The user approved the open-source direct OAuth integration described in chat and requested implementation.
Add a separate ChatGPT plan profile; retain explicit API-key and local profiles without automatic billing fallback.

Use system-browser authorization with a short-lived IPv4 loopback callback, state, nonce and PKCE S256.
Persist one opaque host UUID; dynamically register with `dynamic_agent_client` and `agent_name_hint=ProcSight`.
Keep issued client IDs bound to validated issuer/subject, verify ID-token signatures using OpenAI JWKS,
and require granted direct-plan scopes. Keep account registrations separate; refresh tokens serially and
support revocation on sign-out. Credentials belong in Windows-protected storage, never ordinary preferences.

Discover account-specific models and use fixed public OpenAI endpoints. Stream Responses requests with
array input, store=false and stream=true, omitting unsupported fields. Require completed validated output
before accepting explanations or rules. Bound responses, timeouts, cancellation and diagnostics.
Reuse existing cloud privacy projection and rule validation; captions remain local. Research initially
remains API-key-only rather than silently using a paid API credential from the ChatGPT plan profile.

Settings provides Continue with ChatGPT, saved-account selection, model selection, sign-out and usage link.
Sign-in must not itself enable automatic process transmission. Live user consent and account inference
remain explicit manual verification; synthetic tests verify protocol/security/error cases.

Sources: [sign-in](https://developers.openai.com/siwc/token-sharing-open-source/sign-in),
[sessions](https://developers.openai.com/siwc/token-sharing-open-source/profiles-and-sessions),
[inference](https://developers.openai.com/siwc/token-sharing-open-source/models-and-inference),
[limitations](https://developers.openai.com/siwc/token-sharing-open-source/preview-limitations).
