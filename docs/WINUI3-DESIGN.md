# Windows 11 and WinUI 3 design guide for ProcSight

Reviewed against Microsoft guidance on 2026-10-14. This is a practical synthesis,
not an official Microsoft specification. Source recommendations are linked below;
ProcSight choices are identified explicitly. Apply this guide to new and changed UI,
including rule management, process inspection, AI activity, and Settings.

## Native controls and hierarchy

Use WinUI controls and their theme resources, templates, focus visuals, and interaction
states. Prefer a native ListView, CommandBar, AutoSuggestBox, ComboBox, Expander, InfoBar,
and ContentDialog to imitations made from arbitrary buttons and text. Use Segoe UI Variable
through the XAML defaults, the native type ramp, sentence case, and left alignment. Primary
labels use Body/Body Strong; supporting information uses Caption. Do not make every row a
page heading, and do not encode state with color alone. Text must remain readable when
scaled or translated. [Microsoft typography guidance](https://learn.microsoft.com/en-us/windows/apps/design/signature-experiences/typography).

## Layout and density

Group related controls spatially. Microsoft illustrates 8 effective pixels between buttons,
12 between content areas, and 16 between surface edges and text. Use consistent spacing
and deliberate hierarchy rather than distributing related controls across an entire window.
Multi-line rows should distinguish primary content from supporting text.
[Microsoft content layout and spacing](https://learn.microsoft.com/en-us/windows/apps/design/basics/content-basics).

ProcSight choices: use 24 effective pixels for page gutters, 8 between toolbar controls,
12 between detail sections, and 16 inside cards. Rule rows are compact, selectable items
with two lines and a source icon; an Inspect button on every row wastes space and repeats
the same action. Leave meaningful room for the list rather than a large permanent
maintenance panel. These dimensions are project defaults, not universal Microsoft mandates.

## Lists and details

The list/details pattern supports browsing collections and comparing items without losing
context. Selecting an item updates its details. Use side-by-side panes when space permits;
at narrower widths, show one pane at a time and provide Back navigation. Each pane owns
its scrolling. Preserve selection and scroll position when filters or actions allow it.
[Microsoft list/details guidance](https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/list-details).

ProcSight choices: the rules library uses a native selectable ListView and a structured
detail pane. Its breakpoint is based on the actual content width required by both panes,
not copied blindly from an example. Rule names prioritize executable/product/service
conditions or supporting metadata. A metadata-derived name is descriptive context; it
must not imply a broader matching scope. IDs and matching hashes belong in details.

## Commands and discovery

Use CommandBar for page and selection commands. Put frequent actions first; place secondary
actions in overflow. Native AppBarButton controls supply labels, icons and responsive
overflow behavior. Search and filters should have accessible names and form a coherent group.
[Microsoft command bar guidance](https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/command-bar).

ProcSight choices: Refresh is a page command. Enable/disable belongs to the selected learned
rule. Delete and clear scopes are secondary commands with explicit confirmation. Bundled
definitions are read-only and should say so. Do not show enabled actions that cannot apply.

## Surfaces and themes

Use theme-aware text, divider, card and layer resources. Mica is a window material, not a
reason to make every row translucent. Native surfaces must remain legible in light, dark,
inactive and high-contrast states; avoid hard-coded gray fills and reduced-opacity text
as a substitute for semantic secondary text resources.
[Microsoft materials guidance](https://learn.microsoft.com/en-us/windows/apps/design/signature-experiences/materials).

## Progressive disclosure and feedback

Show identity, source and state first, followed by classifications and matching conditions.
Put long evidence, history and identifiers in labeled expanders. Use contextual InfoBar
feedback for loading, empty results, validation and operation failures. Reserve modal dialogs
for decisions or focused inspection; provide a safe cancel/close action. Only one ContentDialog
can be open per window. Use specific action labels and do not use a modal error for ordinary
field validation. [Microsoft dialog guidance](https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/dialogs-and-flyouts/dialogs).

ProcSight choices: library browsing is nonmodal. Per-process rule inspection shares the same
structured content. Deletion requires an explicit confirmation; feedback must never report
success before storage succeeds. Provenance is an audit aid, not a trust or safety indicator.

## Accessibility and verification

Native controls provide keyboard and automation behavior; retain it. Label search, filters,
icon-only actions and selectable rows for UI Automation. Keep Tab order logical, support
arrow-key list selection and keyboard activation, and return focus after Back or confirmation.
Test contrast, high contrast, Narrator, keyboard access, text scaling, and window resizing.
[Microsoft accessibility overview](https://learn.microsoft.com/en-us/windows/apps/design/accessibility/accessibility-overview).

For each UI change, capture actual XAML with synthetic data at wide and narrow widths in
light and dark themes. Check that captions, long conditions, command overflow and scrollbars
remain usable. A screenshot validates appearance, not accessibility; record untested checks
in [verification.md](verification.md). Keep this guide current when shared UI conventions change.
