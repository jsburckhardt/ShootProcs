# Permissions and feature availability

## Normal launch: no administrator rights

ShootProcs requests `asInvoker` and runs with your existing Windows token.
Installing it per-user also requires no administrator rights.

You can use Arena and Process Explorer, live CPU/private-memory metrics,
application grouping, performance-impact explanations, filtering, and
inspection. You can terminate applications that Windows permits your account
to terminate, subject to ShootProcs' additional protection rules.

Windows process-query APIs expose executable identity, process times, memory,
parent relationships, owner/session, and recognizable window/application
metadata. Some processes are inaccessible; their information can be incomplete.
System-wide CPU and physical memory pressure do not require elevation.

**Without admin, the app is not full-featured.** Physical disk activity,
completion latency, and process-attributed TCP/UDP byte rates are unavailable.
Some protected/elevated process details and termination handles may also be
denied. Missing metrics say Unavailable rather than a fabricated zero. Overall
scoring uses only the evidence actually available.

## Optional elevation: disk and network

Choose **Enable tracing** and approve Windows UAC. Windows starts a new elevated
ShootProcs instance and closes the original one.

Administrative access is needed to enable the Windows kernel ETW providers used
for physical disk I/O, disk completion latency, TCP/UDP activity, and
process/thread attribution. ShootProcs owns a uniquely named tracing session,
does not intentionally stop other collectors, and disposes its session on normal
exit. No driver, service, or permanent privilege is installed.

Elevation applies to the **whole application**, including its process
termination code. It is not a separate restricted collector. The header says
Running elevated; that is the privilege state, not a guarantee tracing started.
If tracing fails or loses events, disk/network data becomes unavailable and
the status bar explains the failure.

To return to normal permissions, close ShootProcs and reopen it normally from
Start or Explorer. Do not enable "Run this program as administrator" permanently
unless that is your deliberate choice.

If UAC requires credentials for a *different* administrator account, the new
process runs as that identity. ShootProcs' same-owner/session protections can
then make your original account's applications non-actionable.

## Termination permissions and protections

ShootProcs asks Windows for a termination handle only when executing a shot.
It checks the captured PID and creation time, actual owner/session, critical
status, essential-process rules, and self protection again on that handle.
Access denied, exited, replaced, and partial outcomes are reported.

Both privilege modes protect essential Windows processes and ShootProcs itself.
Neither mode deliberately expands an individual-process shot to descendants,
chases restarted instances, or includes processes started after confirmation.
Windows protections may still deny an operation even when elevated.

Safe Mode ON asks for confirmation; OFF attempts immediate termination.
**Safe Mode does not remove privileges, create a sandbox, or make force
termination harmless.** Both modes use direct termination and can discard
unsaved work in the selected applications.

## Privacy

The application monitors locally and has no analytics, login, telemetry upload,
remote backend, or automatic update request. Network monitoring observes byte
counts, not packet contents. Process metadata can include application names,
paths, owners, and profile arguments necessary for grouping.

Ordinary monitoring keeps that data in memory. Explicit developer verification
can write local screenshots, process identities, and reports under `artifacts`.
Those files are excluded from Git and public release payloads. Redact personal
paths and process information before attaching diagnostic material to an issue.
