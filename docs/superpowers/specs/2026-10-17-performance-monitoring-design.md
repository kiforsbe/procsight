# Live resource monitoring

Requested scope: process-list disk/network throughput plus a native Performance page with
an overview and subsystem details for CPU, Memory, Disks, Network and GPU. Implement in this
checkout under the user's standing instruction to proceed without approval rounds.

Use an owned, real-time Windows kernel ETW session for disk read/write and TCP/UDP IPv4/IPv6
send/receive bytes. Never substitute all process I/O counters for disk I/O or system adapter
traffic for per-process network rates. No ETL files, packet contents, paths, addresses or ports
are retained. Administrator mode is explicit; failure stays unavailable with an app-owned reason.
No SeDebugPrivilege, process modification, automatic elevation or shared logger takeover.

Aggregate events off-thread into bounded, lifetime-aware process buckets. Snapshot confirmation
remains authoritative. Unobserved/reused PIDs, startup and event loss require warm-up; missing
data never becomes zero. Dispose only the session owned by this app. Event delivery is buffered,
so rates are interval estimates, not exact wire bandwidth or storage attribution for cached I/O.

System monitoring runs independently of process collection and AI at one-second cadence, using
system CPU/memory APIs, physical-disk PDH counters, adapter byte deltas and the busiest GPU engine.
Use English PDH paths, preserve warm-up/missing states and dispose all query handles. GPU engine
utilization is not a claim of whole-device utilization. Keep a 60-sample in-memory history with
native charts, accessible text values, adaptive overview cards and subsystem detail navigation.

Approved visual direction: compact live subsystem list on the left, Overview as the first page
with five tiles (three above two when wide), then CPU, Memory, Disks, Network and GPU details.
Narrow layouts replace the left list with a native selector and reflow tiles. This supersedes
the earlier oversized card implementation. Use measured metadata only; sample hardware captions
in the visualization do not authorize invented live specifications.

Implementation order: (1) synthetic lifetime/rate tests and ETW collector, (2) process columns
and grouped values, (3) independent system sampler and native overview/details, (4) documentation,
dependency notices, build/tests and synthetic visual checks. No automated sign-in is involved.
