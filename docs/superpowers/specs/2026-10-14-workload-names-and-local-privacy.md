# Workload names, table interaction, and local privacy revision

The user requests host entries that identify their actual workload, matching service-host
explanations: Python scripts/modules, Command Prompt batch/npx launchers, VS Code extension
scripts and Electron worker roles. Ownership groups remain unchanged; child names use host — hosted, matching service-host labels. Derived names are transient, never online payloads.

Local command collection and Details are always available when Windows permits access.
Details includes raw command and file/signature/hash/version/parent/lifetime information.
The existing Off/LocalOnly enum values now govern online exclusion, while LocalOnly also
prefers a configured loopback model. OnlineFull explicitly permits full online transmission.
A separate default-off setting governs identifying path/package/owner/service metadata and
evidence; full command sharing independently permits identifiers embedded in that line.
Provider-returned rules receive withheld local ownership scopes after dispatch, preserving
application boundaries without sending private context. Raw lines remain transient.

The table uses the native ListView scrolling viewport for both axes. Its vertical scrollbar
stays inside the window, while a separate header scroll viewport synchronizes in both directions.
Every header cell fills its height, toggles sort direction, and exposes direction visually and
to automation. Name sorting uses visible workload labels. Missing metrics remain last, stable
sampling preserves order, and explicit sorting/refresh reorders hierarchy siblings.

Regression checks cover literal invocation/quoting/argument boundaries, persistent owner scopes,
online projection, local Details, opposite sort orders, and native narrow scrolling/header bounds.
Physical pointer delivery and full accessibility/provider acceptance remain separate checks.
