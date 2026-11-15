# Components and contracts

## Core models

`ProcessIdentity` binds a PID to its creation FILETIME. A PID alone is never a
termination authority.

`ProcessSample` contains one process's identity, observed parent, friendly name,
path, owner, session, optional profile, window status, nullable resource
measurements, and protection reason. `CanTerminate` derives from that reason.

`MachineSample` contains timestamped machine CPU, physical memory, optional
kernel disk and network data, disk latency, and collection status.

`MonitorSnapshot` is one observation boundary. `ApplicationGroup` aggregates
member observations and adds identity, explanation, score, actionability, and
recent impact history. `Diagnosis` combines all groups with machine context
and an understandable headline.

Resource units are machine-normalized CPU percent, bytes, bytes/second, and
milliseconds. Null means unavailable, including incomplete group totals.

## DiagnosticEngine

**Input:** Successive `MonitorSnapshot` values.

**Output:** A `Diagnosis` with application groups, aggregate metrics, bounded
smoothed impact scores, reasons, confidence descriptions, and history.

**State:** Evidence smoothing and at most 40 impact samples per live group.
Exited group state is removed.

**Invariants:** Unique process identities are counted once. Unknown identities
are not merged. Cross-owner/session and conflicting-profile ancestry is
rejected. Consumption alone does not produce an offender score on a healthy
machine. Network traffic alone does not imply network contention.

Independent shared-runtime roots receive distinct job identities; descendants
can join their observed job or application host without merging unrelated scripts
using the same installed interpreter. Runtime-job labels include a root PID.

## ArenaSelection

Selects at most 24 resource-relevant groups. Existing targets are retained until
a challenger exceeds the weakest replaceable target by a 30% plus small absolute
margin. A pinned target is not displaced by another candidate. Exited groups
are removed. Resource-specific weights are used for geometry/selection, not as
a substitute for Overall diagnosis.

## TerminationController

**Input:** A captured `TerminationRequest`, including its Safe Mode state, and
explicit confirmation when required.

**Output:** `TerminationResult`, including confirmation-required status and a
`ProcessTermination` outcome for each distinct captured identity.

With Safe Mode ON and no confirmation, the Windows adapter is never called.
Protected members are rejected independently of UI state. There is no
descendant traversal or restart loop. The adapter interface is the test seam
for deterministic permission and replacement outcomes.

## WindowsMonitor

Owns process collection and metadata/CPU caches. Uses Tool Help, process query
handles, process times, process memory counters, token ownership, system times,
global memory status, and top-level-window enumeration.

Executable metadata supplies friendly names. Browser command-line arguments
are read only for recognized browser executables and only to identify explicit
profile or user-data-directory boundaries; they are not persisted as command
history. Metadata cache keys include creation time.

Processes outside the current owner/session, unknown identities, critical and
essential Windows processes, and the current application are read-only.
Permissions are not treated as evidence of zero consumption.

## TraceTelemetry

Owns a uniquely named TraceEvent kernel session only when already elevated.
Collects physical disk read/write completions and TCP/UDP send/receive events
for IPv4 and IPv6, with process/thread events supporting attribution.

Events are accumulated behind a short lock, then drained at the monitoring
boundary. Disk throughput includes physical I/O, not generic process file/device
I/O counters. Average observed completion latency is a contention proxy, not a
disk active-time percentage.

Missing permissions, startup failure, and event loss produce explicit
unavailability. Event batches predating a process's creation time are not
assigned to its replacement. Kernel/cache ownership and unrecoverable
attribution can make system totals differ from visible application totals.

## WindowsTerminator

Implements `IProcessTerminator`. Revalidates creation time, critical status,
owner/session, essential names, and self protection. Direct termination uses
the same process handle whose identity was checked; exit is confirmed with a
bounded wait.

Outcomes distinguish terminated, already exited, replaced, protected, access
denied, and failed. A stale PID is skipped, never retargeted.

## MainWindow

Coordinates one background sampling loop, shared diagnosis, stable row
collection, inspection, mode changes, Safe Mode, explicit elevation, and
notifications. It marshals view changes onto the WPF dispatcher.

The custom confirmation overlay captures membership once, explains the scope,
freezes the Arena, supports cancellation, and defaults focus to keeping the
application alive. The same action controller serves target clicks, the fixed
inspector button, and individual Explorer actions.

Normal close cancels and awaits collection. A monitoring failure is visible.
Unexpected UI errors are logged locally under the user's local application-data
ShootProcs directory and reported.

## ArenaControl and ArenaTarget

`ArenaControl` manages bounded target selection, stable cell anchors, a
cached low-contrast radar/dot field, orbital render transforms, and transient success
effects. Inspected/focused/hovered targets are pinned; modal confirmation freezes
movement. Its bounded animation timer stops when hidden, unloaded, frozen, or
in reduced-motion mode. Target textures repaint only when displayed data or
meaningful geometry changes.

`ArenaTarget` is a real native WPF button with an accessible application name,
resource summary, keyboard action, circular hit test, shell icon, resource label,
and distinct protected/high-impact coloring. Protected targets are inspectable
but do not dispatch a shot.

## GroupRow, Sparkline, and IconCache

`GroupRow` adapts immutable group snapshots to stable observable Explorer rows,
with numeric sort values and formatted display properties.

`Sparkline` renders a lightweight bounded history. `IconCache` retrieves and
freezes Windows shell icon images, releasing native icon handles after copying.

## Validation components

`ShootProcs.Tests` exercises the agreed high-level diagnostic and command
boundaries without Windows dependencies or production-process termination.

`ShootProcs.Validation` launches isolated copies of its own worker executable.
Each worker holds private memory and a modest periodic CPU load. The harness
checks actual Windows observations and bounded termination, then cleans up
only those worker processes it created.

The app's `--verify-ui` path exercises real WPF controls, rendered cell contents,
both views, modes, sorting, filtering, focus/reduced-motion behavior, protection,
and confirmation/immediate termination against explicitly supplied disposable
fixtures. Reports and screenshots are local generated artifacts, not runtime
telemetry sent elsewhere.
