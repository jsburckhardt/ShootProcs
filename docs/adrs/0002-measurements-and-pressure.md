# ADR 0002: Measured resources and pressure-aware diagnosis

Status: Accepted

## Context

Resource consumption is not proof of slowdown. Adding CPU percent, RAM percent,
and byte rates produces a meaningless score. Generic per-process I/O counters
also include activity that is not physical disk traffic.

## Decision

CPU uses process CPU-time deltas divided by measured elapsed time and logical
processor count. Machine CPU uses system-time deltas excluding idle.

Application memory uses private committed bytes, avoiding duplicate ownership
of shared working-set pages. This is not a claim about exclusive physical RAM.
Machine RAM pressure comes from physical availability.

Use kernel ETW disk completions and TCP/UDP events, through TraceEvent, for
optional per-process disk/network measurements. Do not substitute generic
process I/O counters or connection counts. Start tracing only when the process
is already elevated through the user's explicit action. Missing or degraded
telemetry is nullable and clearly labeled.

The initial model forms normalized evidence:

- CPU pressure ramps from 55% to 90% machine utilization; a group's share of
  measured machine utilization estimates its contribution.
- Memory pressure ramps from 72% to 94% physical utilization; private commit
  relative to 20% of installed RAM provides an explicitly approximate ownership
  weight.
- Disk pressure ramps from 8 to 40 ms observed completion latency, weighted by
  the group's share of measured physical disk bytes.

Smooth each evidence channel with a five-second exponential time constant.
Combine channels as `1 - product(1 - evidence)`, scaled to 0-100.
These are engineering heuristics, not trained causal inference or probabilities.

The system headline detects CPU above 65%, RAM above 80%, or measured disk
latency above 15 ms. Group scores reflect sustained evidence, so a newly observed
spike can change the headline before accumulating a large offender score.

Network activity remains visible but is not scored without reliable evidence
of link contention. No bandwidth saturation is inferred from arbitrary byte
thresholds.

## Consequences and limitations

The score is explainable and bounded. Healthy systems do not manufacture
offenders. Single-thread bottlenecks, GPU pressure, thermal throttling, driver
latency, storage-specific baselines, and remote-service problems are not directly
diagnosed by this first model.

Memory attribution is approximate because commit and residency differ.
Disk latency can reflect device-specific behavior rather than universally
harmful contention; the explanation is evidence, not proof.

Kernel I/O may be charged to System. Unknown ownership, event loss, and PID
replacement can make process attribution incomplete. Lost ETW events disable
attribution until restart instead of silently underreporting exact-looking rates.

Sources:

- https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-getprocesstimes
- https://learn.microsoft.com/en-us/windows/win32/api/psapi/ns-psapi-process_memory_counters_ex
- https://learn.microsoft.com/en-us/windows/win32/etw/diskio
- https://github.com/microsoft/perfview/tree/main/src/TraceEvent
