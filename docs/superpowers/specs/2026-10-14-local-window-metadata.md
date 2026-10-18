# Local window metadata revision

The user requests additional metadata, including window titles, to explain process names.
The collector reads visible non-tool top-level windows, caption/class strings, and foreground/
minimized state. It confirms PID ownership and known process creation times, uses bounded
enumeration/text limits, and bounds same-process caption waits.

Names retain host/script/service roles and append a useful caption, preferring foreground then
non-minimized windows with stable lexical fallback. Known app suffixes can be removed from
captions to reduce repetition. Group ownership remains unchanged. Details shows all collected
window metadata, with missing/untitled observations explicit.

Window metadata stays transient and local even when online command/identifier sharing is enabled.
It cannot enter JSON, knowledge, provider/research requests, identity, or rule evidence. Caption
changes update presentation without additional provider work. Tests use synthetic captions and
native windows owned by the test process, including an unresponsive window.
