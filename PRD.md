# ShootProcs - Product Requirements Document

Status: Arena and Process Explorer implemented and locally validated. See
[launch instructions](README.md) and [validation evidence and limitations](docs/validation.md).

This document records the build challenge, the decisions confirmed during
grilling, and the subsequently approved testing boundaries. It follows Matt
Pocock's specification structure (`to-spec`, formerly `to-prd`). It is the
local product contract. Implementation was subsequently authorized; the
requirements below preserve the agreed scope. Elevated disk/network tracing has
been validated with owned disk and localhost TCP activity; the optional
Performance Monitor is deferred.

## Problem Statement

Windows users struggle to identify which applications are actually making their
computer slow. Traditional process tables expose hundreds of technical process
names, fragment modern multiprocess applications, and emphasize consumption
without adequately explaining performance impact.

A large application is not necessarily a problem on a healthy machine. Users
need to distinguish high resource consumption from evidence of system pressure,
recognize the applications involved, understand the consequences of ending
them, and act without having to interpret individual helper processes.

## Solution

Build a polished, genuinely native Windows application combining real system
diagnostics with a memorable arcade-inspired interface: "Windows Task Manager
meets an interactive video game."

ShootProcs provides two complete experiences backed by the same live application
groups, metrics, diagnosis, protection rules, and termination behavior:

- **Arena, priority 1:** Recognizable application icons move as animated targets.
  Their sizes reflect the selected resource mode. Users inspect a target and
  shoot it to directly terminate the represented application group.
- **Process Explorer, priority 2:** A modern, practical structured view provides
  sortable resource columns, expandable groups, search, filtering, sparklines,
  usage bars, and application or individual-process actions.

Overall is the default resource mode and identifies likely contributors to
actual performance degradation. CPU, Memory, Disk, and reliably attributable
Network modes expose the underlying consumption.

The application should immediately feel substantially cooler than Task Manager,
without sacrificing accuracy, accessibility, usability, or monitoring efficiency.

### Delivery priorities

1. Complete the working Arena, real monitoring, grouping, diagnosis, inspection,
   and termination behavior.
2. Complete the polished Process Explorer using the same underlying behavior.
3. Consider the optional Performance Monitor only after both main experiences
   are complete and working well.

## User Stories

1. As a Windows user, I want to launch ShootProcs as a native desktop application,
   so that I do not need a browser dashboard to diagnose my machine.
2. As a user, I want a standalone executable or straightforward installation,
   so that launching the application does not require developer tooling.
3. As a user, I want real, live Windows process information, so that the display
   describes my actual computer rather than simulated activity.
4. As a user, I want recognizable application names and icons, so that I can
   identify programs without understanding executable names.
5. As a user, I want related processes combined into one application target,
   so that a multiprocess application does not overwhelm the display.
6. As a user, I want all windows and instances from the same application
   installation in my Windows user/session grouped together, so that I can
   understand their combined consumption.
7. As a user, I want separate installations and reliably distinguishable browser
   profiles kept separate, so that unrelated application identities are not
   accidentally combined.
8. As a user, I want unrelated programs with matching executable names kept
   separate, so that grouping does not mislead me or broaden a termination.
9. As a technical user, I want to inspect a group's individual processes,
   so that I can investigate helpers and subprocesses when necessary.
10. As a user, I want Overall mode selected initially, so that the application
    helps me find likely causes of slowdown without choosing a metric first.
11. As a user, I want CPU, Memory, Disk, and supported Network views,
    so that I can investigate a particular resource.
12. As a user, I want sustained resource pressure distinguished from short
    spikes, so that transient activity does not constantly create false offenders.
13. As a user, I want a healthy machine to say "No significant pressure,"
    so that high consumption alone is not misrepresented as harmful.
14. As a user, I want an understandable impact score, explanation, and indication
    of attribution confidence, so that I know why an application is ranked highly.
15. As a user, I want unavailable measurements clearly identified,
    so that missing data is not mistaken for zero activity.
16. As a user, I want smooth animated application targets,
    so that the Arena feels like an intentional, polished game experience.
17. As a user, I want target sizes to update with the selected resource,
    so that the largest relevant consumers are immediately apparent.
18. As a user, I want a legible Arena with at most 24 relevant groups,
    so that hundreds of small targets do not obscure the important applications.
19. As a user, I want to see how many groups are omitted and access them through
    searchable Process Explorer, so that the Arena limit does not hide information.
20. As a user, I want brief spikes not to constantly replace Arena targets,
    so that the scene remains understandable and actionable.
21. As a user, I want hovering over a target to reveal useful information
    immediately, so that inspection does not require navigating away.
22. As a user, I want a target to stop moving while hovered or keyboard-focused
    and remain stable during inspection or confirmation, so that I can act
    precisely.
23. As a keyboard user, I want to inspect and act on targets without a mouse,
    so that the primary experience is not pointer-only.
24. As a user who prefers reduced motion, I want reduced-motion interaction,
    so that animation does not prevent comfortable use.
25. As a user, I want inspection to show the application's icon, name, process
    count, CPU, memory, disk, available network activity, impact score, and
    explanation, so that I can make an informed decision.
26. As a user, I want to shoot an application target,
    so that I can directly terminate its represented process group.
27. As a user, I want Safe Mode ON by default with a prominent global toggle,
    so that I can choose between confirmed and immediate termination.
28. As a user with Safe Mode ON, I want confirmation naming the application
    and number of processes, so that I understand the scope before termination.
29. As a user with Safe Mode OFF, I want a shot to immediately attempt direct
    termination without a confirmation dialog, so that the action resembles
    Task Manager's End Task.
30. As a user, I want essential Windows processes and ShootProcs protected in
    both modes, so that switching Safe Mode off does not disable those protections.
31. As a user, I want protected groups visible but clearly non-shootable,
    so that I can diagnose system pressure even when termination is unavailable.
32. As a user, I want a shot bounded to the captured process identities,
    so that it does not kill a replacement process or silently expand to newly
    created instances.
33. As a user, I want survivors, permission errors, and partial failures reported
    clearly, so that visual effects do not falsely imply complete termination.
34. As a user, I want newly detected or restarted instances shown rather than
    repeatedly killed, so that a shot is not a persistent kill rule.
35. As a user, I want destruction feedback and refreshed resource information,
    so that I can see the outcome without being promised unmeasured savings.
36. As a user, I want ShootProcs to launch without administrator rights,
    so that ordinary monitoring does not require automatic elevation.
37. As a user, I want elevation to require an explicit action and permission
    limitations explained, so that I remain in control of privileged access.
38. As a user, I want to switch between Arena and Process Explorer intuitively,
    so that I can choose playful or structured diagnosis.
39. As a user, I want sortable resource columns and overall impact ranking,
    so that I can find applications using a familiar structured workflow.
40. As a user, I want search, filtering, expandable groups, and optional access
    to background processes, so that both everyday and technical tasks are possible.
41. As a user, I want sparklines, usage bars, and visual indicators,
    so that trends and relative consumption are easy to interpret.
42. As a technical user, I want to terminate one selected process without
    implicitly terminating its descendants, so that individual-process actions
    have a precise scope.
43. As a user, I want the same Safe Mode and protection behavior in both views,
    so that switching views does not change action semantics.
44. As a user, I want low monitoring overhead and a responsive interface,
    so that the diagnostic tool does not become a performance problem itself.
45. As a user, I want modern dark-mode visuals, clear typography, spacing, and
    feedback, so that the application feels polished rather than like a mockup.
46. As the machine owner, I want development termination tests limited to
    disposable processes launched for testing, so that my existing applications
    and work remain untouched.
47. As a user, I want documented launch steps and necessary dependencies,
    so that I can run the delivered build reliably.

## Implementation Decisions

### Engineering ownership and current state

- The coding agent owns framework, language, architecture, Windows API,
  dependency, and packaging selection. Do not ask the user to choose them.
- Investigate current native Windows technologies before selecting the stack.
  A browser-based dashboard does not satisfy the product.
- Inspect the machine and available toolchains before installation. Determine
  and install the dependencies necessary for the selected implementation.
- Prefer a standalone executable or straightforward installation.
- The workspace was empty when inspected. There is no existing implementation,
  domain glossary, architecture decision record, test suite, or project
  convention to preserve.
- No framework, internal class structure, scoring formula, collection API,
  refresh interval, or packaging technology has been selected yet. These remain
  engineering work, not decisions silently added by this PRD.

### Process discovery, identity, and grouping

- Discover actual Windows processes and collect live metrics.
- Use process relationships, executable identity, installation identity,
  application metadata, and appropriate Windows APIs to infer logical groups.
- The default group scope is one application identity within the current
  Windows user/session, including multiple windows and instances of the same
  installation.
- Keep separate installations and browser profiles separate where their
  identities are reliably distinguishable. Do not promise profile separation
  when Windows or application metadata cannot support it reliably.
- Do not merge unrelated programs solely because they share an executable name.
- Group-level CPU, memory, disk, and supported network measurements aggregate
  their included processes without double-counting overlapping measurements.
- Expose process membership for inspection and individual-process actions.
- Group termination can close every included window and instance. Make that
  scope clear before a confirmed shot.
- Show protected and non-actionable groups for diagnosis, clearly marked as
  read-only. Focus ordinary navigation on applications users recognize while
  allowing technical exploration of background processes.

### Resource measurement and impact model

- Support Overall, CPU, Memory, Disk, and Network modes. Overall is the default.
- Measure CPU utilization and sustained processor pressure; memory consumption
  relative to installed RAM and memory pressure; disk reads/writes, activity,
  and contention; and relevant network activity where reliable process-level
  attribution is available.
- The Overall score must model likely contribution to actual system pressure,
  not simply add unrelated percentages or equate consumption with harm.
- Distinguish sustained load from short spikes. Explain why an application is
  considered expensive, expose attribution confidence, and acknowledge incomplete
  evidence.
- A healthy machine can report "No significant pressure" even when one
  application leads consumption rankings or uses substantial RAM.
- Unavailable metrics must be visibly unavailable, not fabricated or reported
  as measured zero. Network support is conditional on reliable attribution.
- Resource-specific modes still communicate consumption when Overall finds no
  significant pressure.
- Do not claim to have measured released resources solely because a termination
  command succeeded. Update measurements and report observed outcomes.

### Arena

- Render recognizable icons, smooth movement, dynamically resource-scaled
  targets, and clear inspection and destruction feedback.
- Show at most 24 relevant application groups. Clearly indicate omitted groups
  and provide access to them through searchable Process Explorer.
- Avoid constant target replacement caused by brief spikes.
- Stop motion for hovered or keyboard-focused targets. Maintain stable
  interaction during inspection and confirmation.
- Support keyboard interaction and reduced motion.
- Inspection immediately exposes application identity, process count, resource
  measurements, impact score, explanatory evidence, and available process detail.
- Preserve usability and accuracy when resource usage and group membership
  change while the scene is active.

### Process Explorer

- Use the same groups, measurements, scoring, protection rules, and action
  semantics as Arena.
- Provide recognizable names/icons, sortable resource columns, live metrics,
  impact ranking, visual usage indicators, sparklines, search, filtering, and
  expandable process groups.
- Offer both application-group and individual-process termination.
- An individual-process action targets only the selected process, not an
  implicit descendant tree.
- Provide intuitive navigation between the two main experiences.

### Direct termination and Safe Mode

- Safe Mode is a prominent global setting, ON by default.
- With Safe Mode ON, shooting requests confirmation identifying the application
  and process count before direct termination.
- With Safe Mode OFF, shooting immediately attempts direct termination without
  a confirmation dialog.
- Both modes use direct process termination, not a graceful-close request.
- Essential Windows processes and ShootProcs itself remain protected in both
  modes, with termination disabled in every relevant UI and command path.
- Capture exact process identities for an action. Validate identity before
  termination so PID reuse cannot redirect the action to a replacement process.
- A confirmed shot is bounded to the identities presented for confirmation.
  Skip exited or replaced processes; do not silently include new instances that
  appear while the dialog is open.
- An immediate shot is likewise a bounded attempt against its captured
  identities. Neither mode repeatedly chases restarted applications.
- Report access denied, partial termination, surviving processes, and newly
  observed instances honestly. Destruction effects must not imply a failed
  termination succeeded.
- Launch without administrator rights. Elevation is available only through an
  explicit user action, never automatically.
- Explain permission-dependent monitoring limitations and termination failures
  clearly.

### Quality, packaging, and operational behavior

- Deliver a real working application, not a static mockup or architecture-only
  deliverable.
- Prioritize modern Windows-native dark aesthetics, typography, spacing,
  smooth animations, meaningful visualization, and responsive interaction.
- Keep monitoring overhead low; visual polish must not compromise accuracy
  or usability.
- Build and package the application for easy launch on the user's machine.
- Document launch instructions, required dependencies, validated behavior, and
  anything that could not be validated.
- No numeric overhead budget or performance threshold was agreed. Select
  defensible engineering targets and measure them rather than claiming that
  "low overhead" has been proven without evidence.

## Testing Decisions

The user explicitly approved the following testing boundaries before this
document was written.

### Primary behavior boundary

Test observable behavior through the application's monitoring-and-command
interface. Prefer this high-level boundary over tests coupled to internal
classes, rendering implementation details, or a particular scoring formula.

Supply controlled process snapshots and resource samples, then verify:

- Logical grouping, multiple instances, distinct identities, and ambiguous
  executable names.
- Aggregation and units without double-counting resource measurements.
- Sustained versus transient pressure, healthy-machine diagnosis, understandable
  explanations, and unavailable-metric handling.
- Refresh behavior when applications appear, exit, change usage, or change group
  membership.
- Global Safe Mode command behavior and protected-process rejection.
- The exact captured process identities selected for termination.
- Individual-process scope, PID reuse, exits during confirmation, newly created
  instances, restarts, partial failures, and permission errors.

Use focused cases that would fail if the required behavior were missing or
incorrect. Do not test private implementation details merely to increase coverage.

### Real Windows integration

Exercise the real Windows adapter using disposable processes launched expressly
for testing. Validate discovery, sampling, grouping where controlled, refresh,
and bounded termination against those processes.

- Never terminate the user's existing applications as part of development or
  validation.
- Exercise actual termination in both Safe Mode settings against disposable
  targets.
- Validate failure reporting through controlled scenarios where feasible,
  supplemented by deterministic boundary tests when live reproduction is not
  appropriate.
- Use available Windows observations or tools to check resource calculations
  and attribution; account for sampling-window differences.

### Native UI and packaged-launch acceptance

A separate acceptance pass covers behavior the primary interface cannot prove:

- Arena renders live icons and targets, updates sizes, animates responsively,
  respects its 24-group limit, and exposes omitted groups.
- Hover, focus, inspection, and confirmation remain stable and actionable.
- Keyboard and reduced-motion behavior work.
- Inspection content reflects live measurements and explains missing data.
- Process Explorer sorting, filtering, expansion, visual indicators, and actions
  behave correctly.
- Both views consistently honor Safe Mode, protection, and precise action scope.
- Partial failure is not presented as complete destruction.
- The packaged application launches on the actual Windows machine using the
  documented instructions.

There is no existing test suite or prior test pattern in the empty workspace.
Choose test tooling after the stack is selected. Validate through actual
execution where possible, record meaningful evidence, and never describe
unexecuted functionality as verified.

## Out of Scope

- A browser-based dashboard, ordinary process-table clone, static demo, or
  mock-data-only implementation as the delivered product.
- A separate Arena target for every helper process.
- Grouping based solely on matching executable names.
- Treating high consumption as proof of slowdown, artificially inflating scores,
  or fabricating unavailable resource measurements.
- Automatic elevation.
- Termination of essential Windows processes or ShootProcs, even with Safe
  Mode OFF.
- Graceful-close requests as a substitute for the required direct termination.
- Persistent kill rules, repeatedly chasing restarts, or expanding a confirmed
  shot to newly discovered process identities.
- Implicit descendant-tree termination from an individual-process action.
- Closing any of the user's existing applications during development tests.
- Requiring the user to select the programming language, framework, or
  application architecture.
- Compromising Arena or Process Explorer to implement optional monitoring.

The optional Performance Monitor is not required for completion. Only after
the main experiences work well may it add system-tray integration, compact
CPU/memory/disk/network indicators, small real-time graphs, an always-on-top
widget, configurable thresholds, or quick access to ShootProcs.

## Further Notes

### Inspected environment

Development and native validation used Windows 11 x64, .NET desktop runtimes,
hardware-accelerated graphics, and a virtual-display configuration. The initial
machine lacked a .NET SDK, so a private development SDK was installed.

The private machine inventory is intentionally omitted from the public product
contract. These observations are not minimum hardware requirements; recheck
development prerequisites before setup and validate rendering on other display
configurations.

### Definition of done

1. ShootProcs runs as a native Windows application and launches using documented
   instructions.
2. It displays real Windows process data, intelligently grouped and aggregated
   without misleading identity merges or double-counting.
3. Overall identifies likely contributors to real pressure, explains its
   reasoning and confidence, and represents healthy conditions honestly.
4. Arena provides live resource modes, responsive animation, recognizable icons,
   stable inspection, bounded target density, and working shooting interactions.
5. Safe Mode behaves consistently across both views; essential processes and
   ShootProcs remain protected.
6. Group and individual-process termination honor the agreed scopes, validate
   identities, and report actual outcomes and failures clearly.
7. Process Explorer delivers polished grouping, ranking, sorting, filtering,
   expansion, live metrics, and actions.
8. Appropriate behavior tests, real disposable-process integration, native UI
   acceptance, and packaged-launch checks have been executed as far as the
   environment permits; remaining gaps are explicitly documented.
9. Necessary dependencies, build/packaging steps, and launch instructions are
   documented.

### Workflow and handoff

This is the persistent decision record for work that may span multiple coding
sessions. Pocock's current flow is specification, then implementation tickets,
then implementation and review. Tickets can later slice this scope into
end-to-end deliverables; writing this PRD does not create tickets, install
dependencies, or start building.

No Git repository or project issue tracker is configured in this workspace,
so this PRD is stored locally rather than published as an issue. No tracker
labels or remote work items have been created.

Workflow references:

- https://github.com/mattpocock/skills/blob/main/skills/engineering/to-spec/SKILL.md
- https://github.com/mattpocock/skills/blob/main/docs/engineering/to-spec.md
