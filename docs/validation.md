# Validation

## Executed on the target Windows machine

The application was built and run, not only inspected or compiled. Validation
used the actual Windows environment and disposable processes created expressly
for these checks. No existing user application was terminated.

The end-to-end entry point is:

```powershell
.\build.ps1 -Verify
```

It builds Release with warnings as errors, runs the behavior suite, verifies
formatting, publishes the self-contained Windows x64 executable and portable ZIP,
then exercises Windows integration and native acceptance on the **published
executable**.

## Behavior tests

The no-admin setup lifecycle was also executed from an unelevated account:
install to an isolated destination, verify the binary matches the portable
release, check per-user uninstall registration and Start-menu shortcut, launch
a responsive native window, close normally, and uninstall. Registration,
shortcut, and application files were removed. The local report is
`artifacts\installer-verification.json`; run `.\tools\Test-Installer.ps1` after
building setup to reproduce it. The test refuses to overwrite an existing
installation or Start-menu shortcut.

22 passing xUnit cases cover:

- Same-installation instance/helper aggregation and duplicate identity removal.
- Distinct installations, owners, sessions, profiles, and unknown identities.
- Recycled-parent rejection and explicit conflicting profile ancestry.
- Shared WebView host association without merging into the desktop shell.
- Independent shared-runtime jobs kept separate while descendants and editor
  runtime helpers group with their observed host.
- Nullable/incomplete measurement propagation.
- Healthy-machine behavior, sustained versus transient CPU pressure, bounded
  memory/disk evidence, and avoiding network-only false accusations.
- Refresh/exit history and stable, bounded, pinned Arena selection.
- Confirmation gating, immediate execution, captured identities, independent
  protection, and partial/replaced outcomes without retries.

The test-run report is generated at `artifacts\tests\behavior.trx`.

## Windows adapter integration

21 passing checks exercise real isolated worker processes:

- Discovery, machine-normalized CPU, and comparison with independently observed
  Windows CPU-time deltas. Independent reads bracket both enumeration windows,
  avoiding an artificial exact timestamp match while preserving bounds that
  would reject incorrect CPU normalization.
- Private memory allocation, comparison with Windows process-memory values,
  machine memory sanity, same-installation grouping, and aggregation.
- No kill before confirmation; confirmed termination of captured workers;
  survival of an instance started after capture.
- Stale creation identity rejected, immediate single-process termination, and
  exited processes disappearing on refresh.
- Actual parent/child relationship discovery and proof that terminating the
  parent individually leaves the child running.
- Cleanup of the owned child; protection in both the controller and native
  self-protection boundary; essential process classification.
- Explicit unavailability of unprivileged disk/network measurements.

The integration report is generated at
`artifacts\validation\windows-integration.json`.

### Elevated disk/network regression

Run the dedicated check from an elevated PowerShell after building:

```powershell
.\tools\ShootProcs.Validation\bin\Release\net10.0-windows\ShootProcs.Validation.exe --verify-telemetry artifacts\validation\elevated
```

The check was executed elevated. It verified kernel session startup, positive
physical disk and network activity, disk completion latency, attribution to the
test process, and session restart after disposal. The workload writes and removes
its own 8 MiB temporary file and transfers 4 MiB over localhost TCP. It neither
contacts external services nor terminates applications.

The report is `artifacts\validation\elevated\elevated-telemetry.json`.

## Native application acceptance

The actual WPF application verifies live samples, real process groups,
machine/resource values, and WPF rendering capability. It exercises:

- Arena's 24-target bound and all resource modes.
- Actual rendered Explorer cell text, not just row counts.
- Search exclusion/restoration, numeric sorting, expansion with member-process
  controls, and collapse.
- Inspection through focus and the hover event route; circular hit testing;
  focused-target position stability and reduced motion.
- Safe Mode's default and immediate labels, rendered/hit-testable confirmation,
  an overlay blocking underlying controls, frozen Arena,
  cancellation, unchanged captured membership during further samples, and the
  actual confirmed termination of owned fixture processes with a success burst.
- Immediate individual-process action without a dialog, visible result feedback,
  protected action rejection, and disabled protected action UI.
- Subsequent removal of exited fixture processes.

The fixture identity guard refuses any action group containing a process the
test did not create. All leftover owned fixtures are cleaned up.

Generated evidence:

- `artifacts\ui\ui-verification.json`
- `artifacts\ui\arena.png`
- `artifacts\ui\explorer.png`
- `artifacts\ui\confirmation.png`
- `artifacts\ui\destruction.png`

These are automated native-control, event-route, rendering, and execution
checks. They are not a claim of exhaustive manual mouse, screen-reader,
multi-monitor, or every-DPI acceptance.

The corrected self-contained executable was also exercised elevated, with
36 passing boolean assertions including real rendered disk and network values.
Its report and screenshots are under `artifacts\ui-elevated`. The installed
portable executable was verified byte-identical to that tested build, and the
old application was closed normally before the corrected elevated app reopened.

## Performance evidence

Reproduce the ordinary, uninstrumented, published Arena measurement with:

```powershell
.\tools\Measure-Performance.ps1
```

It launches its own normal application instance, records six ten-second windows,
and gracefully closes that instance. It does not shoot existing processes.
The JSON report is `artifacts\ui\release-performance.json`.

An ordinary Release run on this 20-logical-processor machine, monitoring roughly
600 processes, recorded 4.07% total machine CPU in the first ten seconds, then
1.43%, 0.99%, 0.97%, 0.82%, and 1.13%. Private commit ranged from approximately
291 to 329 MiB, and the application remained responsive in every interval.
These are observations under this machine's workload, not universal guarantees.

Warm collection commonly measured roughly 50-70 ms per approximately one-second
sample. The UI shows sample duration. Short three-second measurements immediately
after UI-test interaction include startup/JIT/layout activity and are not a
reliable substitute for the longer ordinary-session result.

Rendering uses cached static field imagery, cached target textures, change-only
target repainting, a bounded animation timer, and small-size-change suppression.
Reduced-motion, frozen, unloaded, and hidden Arena states stop the animation
timer. Explorer reuses and virtualizes rows rather than rebuilding the view.

## Unverified or deliberately limited

- Elevated ETW disk/network collection and attribution were runtime-verified by
  the dedicated regression above. Unelevated unavailable behavior was also
  verified. The in-app UAC restart button is not covered by automated UI checks.
- Process attribution is limited by Windows permissions, shared runtime/profile
  ambiguity, kernel/cache ownership, and available trace events.
- Impact is a documented pressure heuristic, not proof of causation. It does not
  diagnose GPU bottlenecks, thermal throttling, all single-thread bottlenecks,
  driver latency, or remote-service problems.
- Packaging provides a self-contained portable ZIP and a verified per-user
  installer, but is not digitally signed or installed through the Store.
  Offline launch was not tested on a separate clean machine;
  launch of the actual self-contained executable on this machine was tested.
- The optional tray/floating Performance Monitor remains deferred as agreed.

## Defects caught during execution

Live acceptance exposed and corrected a deferred-collection refresh conflict, a
Windows theme background override, and a CPU progress-bar binding that attempted
to write into read-only measurement properties. Rendered-cell checks were added
so empty Explorer rows cannot pass merely because their models exist.

Elevated use subsequently exposed a kernel-session initialization-order defect:
accessing `TraceEventSession.Source` implicitly started the session before
`EnableKernelProvider`. Kernel enablement now precedes source access, and the
elevated regression exercises actual collection rather than accepting a generic
unavailable state. An elevated tracing failure no longer incorrectly asks the
user to elevate again.
