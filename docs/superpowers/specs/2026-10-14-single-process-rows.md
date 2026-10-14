# Single-process application rows revision

The user explicitly requests direct process rows for one-member applications. Group headers
and count/chevron controls appear only for multiple visible members. Single rows expose their
actual process name, PID, resources, details, and analysis action.

Top-level owner-slot keys remain stable across single/group transitions to retain native
viewport anchoring and sibling order. Selection maps a former child to the remaining process
using its lifetime key. Expansion preferences remain remembered while a group has one member.
Windows description metadata uses a filename fallback for null, empty, and whitespace values.

Synthetic regression tests cover the blank-name case, both membership transitions, stable
top-level ordering, and selected-child survival. Domain identity and ownership scope are unchanged.
