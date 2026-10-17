# Windows 11 and WinUI 3 design guide for ProcSight

Reviewed against Microsoft guidance on 2026-10-14. This is a practical synthesis,
not an official Microsoft specification. Source recommendations are linked below;
ProcSight choices are identified explicitly. Apply this guide to new and changed UI,
including rule management, process inspection, AI activity, and Settings.

## Performance page convention

Use a compact native ListView on the left with Overview first, followed by CPU, Memory, Disks,
Network and GPU. Each subsystem retains a small live graph and text reading while another page
is selected. Overview uses five uniform tile buttons with label, primary reading, brief context
and small graph; use three tiles above two on wide windows, two columns at medium widths and one
when necessary. Each tile opens the corresponding subsystem detail page with a larger history
graph and structured readings. Keep selected subsystem for the window session.
Keep the sidebar, its items/charts and compact selector attached when switching subsystems.
Replace only right-hand page content; replaying list entrance transitions makes navigation bounce.
Closing should hide the window promptly. Keep native monitoring shutdown and disposal off the UI
thread, while retaining the hidden window's dispatcher until cleanup completes. Window close, tray
Exit and application handoff must use the same path. Do not force process exit or fake responsiveness
by abandoning the owned system monitoring session.

On narrow windows replace the sidebar with a native ComboBox and keep the content vertically
scrollable. Use theme resources for series colors, including high-contrast resources, with text
values and native accessible names alongside graphs. Color alone cannot identify a subsystem or
state. Missing/warming observations have explicit text and break graph segments; never fabricate
an idle zero or hardware identity. Graphs show the latest 60 samples without growing visual trees.
Reuse native trace geometry and point collections during sampling; rebuild static grids only when
dimensions or the elapsed-time scale changes. Gap/restoration behavior must survive reuse.

This is the user-selected ProcSight layout; synthetic light/dark captures validate geometry.
The native Performance header offers a saved Logarithmic time axis toggle, off by default.
It expands recent elapsed time with log(1+age), preserving Now without a logarithm-of-zero error.
Use actual sample timestamps, labelled time ticks in details and unchanged vertical value scales;
overview and mini charts share the selected time mode. Toggling redraws existing charts in place.
Keyboard, Narrator, high contrast and scaling acceptance remain separate checks.

## Native controls and hierarchy

Resource-cell backgrounds are optional presentation cues. Disk, Network and GPU default to
recent displayed-value changes: brighten when the visible number changes, then fade without
refreshing on unchanged polls. Offer per-column Off, Recent changes, Current activity, Recent
spikes and Top consumers modes plus fade duration, opacity range and color in Appearance. Explain relative
scales; never imply hardware saturation or trust. Missing values and high contrast have no heat.
Use cached accent brushes, preserve text/selection contrast and retain brush bindings after drag.
CPU and Memory retain usage-based heat. Resource cells share a configurable minimum opacity,
defaulting to 5% (slightly below the former CPU/Memory 7% floor); valid zero readings retain that
floor, while missing values, Off modes and high contrast remain transparent. A 0% floor disables it.
Offer a direct maximum opacity (roof), default 50%, plus system highlight (Windows accent) or custom
RGB color using a native ColorPicker. Keep floor <= roof, use a NumberBox for the roof, and debounce
valid drafts with focus-loss/unload flush. Apply the same range/color across resource columns.

Keep live process order stable by default. Offer automatic sorting as an explicit switch in
Appearance and the process overflow menu; it follows the selected header direction. Automatic
reordering moves existing rows incrementally, preserving selection and avoiding entrance/reset
animations. Suspend it during column dragging. Header clicks still sort immediately in either mode.
Check the native vector boundary: managed collection Move can become a WinRT Reset. Use native
remove/insert notifications for logical row relocation; managed-only checks miss this flicker.

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

Process/service rows use native MenuFlyout commands for the clicked row; right-click on an existing
multi-selection preserves it. Blank areas do not act on a previously selected row. Native context
requests also support keyboard invocation. Groups expose expansion and export rather than individual
analysis. Disable commands that cannot apply, and revalidate process lifetime before executing.

Columns share one layout between headers and virtualized rows. Use native resize thumbs (keyboard
arrows also change widths), header dragging and a native chooser with show/hide, width and move controls.
Name remains first and mandatory. The chooser applies valid edits immediately; process/service layouts
are separate. Export is a focused native dialog followed by the Save picker, with explicit scope,
format and private-field choices. Do not silently reuse online-sharing choices for a local export.

### Task Manager column interaction reference

The user's Windows 11 Task Manager recording `20261017-0659-52.2612434.mp4`, reviewed on
2026-10-17, is the interaction reference for ProcSight's process/service tables. It shows Status
being moved among neighboring headers and later shows column-edge resizing. Record the behavior
here; do not check the recording or its real process inventory into the repository.

**Approved interaction baseline:** on 2026-10-17 the user accepted the implementation in
commit `e438eb4`. Preserve this behavior for process/service tables and reuse it for future native
tables: the full column follows the mouse fluidly, neighbors slide aside, and resizing uses an
invisible target spanning both sides of the thin divider. Do not reintroduce visible grips or
replace full-column movement with a floating header label. Treat these conventions as established
product requirements when changing shared table controls.

- **Header body:** clicking sorts; pressing and moving horizontally beyond a small drag threshold
  begins reordering. The drag must not also change the sort. The entire column, including the full
  header, resource summary, sort indicator and visible body cells, follows the pointer continuously
  from its original grab point. Use the actual cells, not a floating label or badge. Neighboring
  columns slide into their preview positions. Releasing commits the arrangement. ProcSight's cancel convention is
  Escape or a cancelled capture restoring the original order without saving a partial preview.
- **Header edge:** use an invisible 20px hit area centered on the divider (10px on each side).
  Draw no grip, handle, extra bar or hover/pressed indicator: the existing thin column separator
  and horizontal double-arrow mouse cursor are the only pointer cues. Dragging
  changes that column's width continuously; neighboring columns shift accordingly. Do not turn
  an edge drag into a reorder or sort, and do not redistribute all column widths automatically.
- **Stable list:** keep row identities, selection, group expansion, icons and resource data intact.
  Reordering must move existing cells, not recreate the row templates on every pointer move.
  Keep boundaries aligned through horizontal scrolling and virtualization, without binding errors,
  flicker, blank cells or a jump in vertical scroll position.
- **ProcSight constraints:** Name stays visible and first and cannot be a reorder destination.
  Width bounds and keyboard resize/move controls remain available. Save a completed layout separately
  for Processes and Services. Keep native theme/focus visuals and resource-summary header content;
  do not copy a recording's incidental colors, process names or private data into UI assets.

Verify both real pointer gestures and synthetic regression scenarios. Calling the reorder helper
alone does not prove that dragging works. Preserve the current sort during reorder/resize; the
recording's resource-header summaries and sorted Memory indicator illustrate that distinction.

Performance convention: share one rendering transform across a column's header and realized body
cells, animate neighbors per column, and defer grid relayout until drop. Recycled rows must adopt
the active motion. Do not recreate per-cell animations, rewrite width bindings or relayout the list
for every pointer crossing. Preserve the approved full-column appearance while optimizing it.
Use the [synthetic performance measurements](PERFORMANCE.md) to separate handler/layout cost from
actual desktop frame rate; timing a helper alone is not proof of pointer responsiveness.

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

Rejected rules put their cause and recovery guidance immediately below state, before classifications.
Keep original decisions distinct from current validation. Active Unknown rules explicitly explain
that eligibility to match does not establish identity. Research status distinguishes disabled,
unavailable, failed and completed-without-sources outcomes; do not imply a search happened from a toggle.
Configuration dialogs use their actual content viewport rather than a width derived from the main
window. Avoid nesting a settings Expander inside a focused account dialog. Async pickers expose
loading/retry status beside the field, recover after reattachment, and discard stale completions.

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

Process details use the same typography, theme resources, padded expandable sections and command
bar as rules inspection. Keep summary identity/PID visible, group assessments separately from raw
metadata, and retain local command/window context under labeled sections. Resource ticks must not
rebuild the pane; preserve expansion and scroll state through real metadata changes for the same
selection. Keep top-level section expansion choices when browsing other process/service or rule
entries, including temporarily missing sections and page navigation. Remember choices by stable
section kind for the window session, independently of item data; scroll resets for a different item.
Service host signatures remain separate from implementation signatures and module proof.

Process/service table headings share the row grid's actual width and horizontal offset, including
when the initial collection is empty. Align CommandBar content vertically with its native actions;
verify geometry through resizing rather than checking only a static screenshot.

Service rows use a 16px Fluent gear fallback when their configured implementation/host file icon
is unavailable. Keep the icon slot fixed and the service name in a constrained star column so long
names trim before neighboring metadata. File icons are cosmetic, not authentication evidence.

Settings account/model selectors share a row when there is room and stack at narrow widths.
Keep Refresh models beside the model selector, group account actions compactly, and preserve
native keyboard focus order when controls move between rows.

Settings apply immediately: native toggles/selections apply directly, valid text edits debounce
400 ms and flush on focus loss. Show incomplete values inline and retain the last valid setting.
API keys commit only on Enter/focus loss, never per keystroke. Use the compact selectable connection
list for enabling, ordering and explicit testing; Test connection targets the selected row, not a
hidden global provider. No Save button or stale “then save” guidance belongs on the settings page.

The connection page uses one native CommandBar for Add, Configure, Test, priority and Remove actions,
with native overflow at narrow widths. Provider fields belong in a focused per-connection ContentDialog,
not below the list. Add offers provider choices in a menu and opens that dialog; unfinished new profiles
are discarded when closed. Valid changes remain immediate, and Close flushes pending valid edits/key
commits before dismissal. Keep fallback as a separate compact row. Routine success feedback is silent;
collapse empty feedback entirely and reserve visible messages for validation, errors and explicit tests.
