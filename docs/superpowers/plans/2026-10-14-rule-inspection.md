# Rule inspection implementation

Approved scope: [rule inspection](../specs/2026-10-14-rule-inspection.md).
Use this checkout and feature branch; execute with Superpowers subagent-driven development.

1. Core/coordination: add optional rule provenance and contributing applied-rule IDs; stamp provider
   metadata after validated response, preserve JSON backward compatibility, test persistence and
   correction/conflict/supplement behavior.
2. Presentation/native UI: shared readable rule inspection, combined bundled/learned searchable
   library, source/state filters, process matching/applied indicators and evidence navigation.
   Keep local command hints/corrections separate and preserve existing mutation controls.
3. Run focused and full verification, review trust/state claims, update docs/UML and commit locally.
   Do not publish. Legacy rules are not retroactively attributed to a particular provider/model.
