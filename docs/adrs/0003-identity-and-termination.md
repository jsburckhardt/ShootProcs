# ADR 0003: Application identity and bounded direct termination

Status: Accepted

## Context

Modern applications spawn helpers and multiple independent windows. Executable
names alone merge unrelated installations; process trees alone split independent
instances of the same installed application. PIDs can be reused between
inspection, confirmation, and execution.

## Decision

Group by owner, session, executable/installation identity, observable ancestry,
and reliable explicit browser profile boundaries. Keep unknown identities
separate. Fold helpers into an observed same-installation root; handle the shared
WebView runtime through observed host ancestry, not global executable merging.
For generic interpreter and service hosts, use the observed job root as an
additional identity boundary. Sharing one Node/Python/.NET/PowerShell/runtime
installation is not sufficient to merge unrelated applications. Observed
windowed/editor hosts can own runtime children; launcher shells cannot.

Merge independent instances of the same identity as the user agreed. Keep
separate installations and distinguishable profiles separate. Do not imply that
every browser profile can be recovered when processes share one ambiguous root.

Capture `(PID, creation FILETIME)` identities for each action. Safe Mode ON
requires explicit confirmation of that snapshot; OFF skips confirmation but
changes no protection rule. Group shots target only captured members. Individual
actions target exactly one process, not its descendants.

Recheck identity and protections at the Windows boundary, then call
`TerminateProcess` on the same validated handle. Protect the current application,
critical/essential Windows processes, other owners/sessions, and unknown or
unverifiable identities. Fail closed if owner or critical status cannot be
established.

Never expand pending membership or chase restarting instances. Confirm exit
with a bounded wait and return per-process outcomes.

## Consequences

A group shot may close multiple windows and discard unsaved work. The UI names
this scope explicitly. PID reuse cannot redirect the native handle to a
replacement. Some permission-limited processes are deliberately read-only,
including when Safe Mode is OFF.

Unknown or shared helper ownership can leave separate diagnostic groups rather
than risking an overly broad kill. This conservatism is preferable to a
misleadingly confident merge.

Sources:

- https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-isprocesscritical
- https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-terminateprocess
