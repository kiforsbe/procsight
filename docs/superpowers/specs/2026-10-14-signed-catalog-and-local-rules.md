# Signed catalog and local invocation rules revision

This user-requested extension continues the approved read-only, local-first implementation:
package portable knowledge for processes/services observed locally, apply authenticated
signature evidence, and derive more invocation roles locally before provider fallback.

Shared definitions contain public executable/product/original-file/service names, authenticated
publisher labels, and references. They omit all machine paths, PIDs, hashes, installed/running
state, arguments, and per-user service instance suffixes. Product/service scopes distinguish
variants. Missing product/original-file evidence permits publisher-only references that keep
purpose unknown. Safety and optionality always remain separate.

Service configuration identifies its own executable or ServiceDll, including root/Parameters
registry values and verified per-user templates. Host proof is separate. Hash/signature changes
invalidate implementation knowledge; configuration does not prove actual loaded-module state.
Bounded cached enrichment runs off-thread and rejects ambiguous/network executable paths.

Fixed local rules use bounded Windows argument tokenization. Known switches, entry points,
Python modules, .NET commands, and PowerShell modes yield provisional roles. Inline/encoded
payloads remain opaque. Raw arguments never enter storage; display-only entry-point names
remain transient. Existing Off/Local only/Online full routing and provider fallback apply.

Verification covers identity replacement, signed-host/unsigned-module separation, variant IDs,
actual .NET metadata, quoted/ambiguous paths, opaque code, and local invocation roles. The
living architecture documents implemented components; full desktop/provider checks remain open.
