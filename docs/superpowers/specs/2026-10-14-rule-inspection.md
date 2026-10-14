# Inspectable rules and applied provenance

The user requests a readable rule library and a way to see which rules were applied to a specific
process, especially AI-derived rules. Extend the existing Knowledge/Details flow rather than create
a separate subsystem. Browse bundled and saved definitions together, with search/source/state filters.
Readable inspection shows source/provider/model when recorded, dates/state, conditions, classifications,
evidence and public references. Keep raw commands and credentials excluded. Existing enable/disable/
delete/clear actions retain their current safeguards.

New learned rules retain app-stamped provider/model/destination provenance, never model-supplied
credentials or account identifiers. Legacy rules explicitly show unavailable provider history.
Distinguish matched rules from rules whose assessment actually contributed to the current result;
conflicts and user corrections must not be described as applied. Preserve stable rule IDs and old JSON.
Process Details links matched/applied rules to the same inspector; show local command-rule hints and
user corrections separately. Do not imply a rule was applied merely because a file predicate matched.

Verify source/provenance persistence, contributing IDs during conflicts/corrections, readable
presentation and native synthetic UI. Update living architecture/UML and project documentation.
