# ADR 0004: Bounded native rendering and portable delivery

Status: Accepted

The installer omission below is superseded by
[ADR 0005](0005-public-distribution.md); rendering decisions remain accepted.

## Context

Hundreds of animated process objects would undermine legibility and overhead.
Monitoring must not make its own UI sluggish. The user prefers simple local
launch, and both experiences must agree about state and commands.

## Decision

Render at most 24 application groups as native WPF button targets. Use stable
anchors, render-transform orbital motion, cached field/target textures, icon
caching, bounded histories, and a timer capped near 30 FPS. Pause that timer
when hidden, frozen, unloaded, or in reduced-motion mode. Repaint only changed
target content and ignore tiny size jitter. Apply replacement hysteresis and pin inspected/focused
targets. Freeze confirmation and support reduced motion.

Sample off the dispatcher once per approximately one second. Reuse observable
Explorer rows with live sorting/filtering and native row virtualization instead
of rebuilding the full view every tick.

Both experiences share one diagnosis and command controller. The action button
stays fixed while long explanatory content scrolls.

Publish an untrimmed, self-contained Windows x64 single-file executable and a
portable ZIP. Do not introduce a browser runtime, service, installer, signing
toolchain, or optional monitoring utility to deliver the core experiences.

## Consequences

The Arena is intentionally selective; omitted counts and searchable Explorer
preserve access to all observed groups. Group size is resource-relative within
the bounded field, not a literal pixel-to-byte conversion.

Single-file startup can extract native runtime libraries and is larger than a
framework-dependent build. No digital signature or Store identity is claimed.
The executable launches without an SDK or separate .NET runtime.

Validation must launch the actual published build, not only the development
executable. Generated JSON and native-view screenshots provide local acceptance
evidence.
