# ADR 0001: Native WPF on .NET 10

Status: Accepted

## Context

The product must be genuinely native to Windows, visually ambitious, accessible,
and straightforward to launch. The inspected machine has Windows 11 and .NET
desktop runtimes but initially lacked a build SDK.

## Decision

Use .NET 10 and WPF for the desktop application, with a UI-independent Core and
a Windows interop layer. Install the missing SDK locally for development and
publish a self-contained Windows x64 executable.

WPF provides native desktop controls, vector rendering, hardware acceleration,
keyboard/accessibility infrastructure, mature process interoperability, and
single-file deployment without requiring a browser runtime.

## Alternatives

- **WinUI / Windows App SDK:** Modern first-party Windows UI, but adds SDK,
  tooling, and deployment integration beyond the installed environment. The
  current command-line workflow also introduces additional tooling; it is not
  necessary to achieve the required native rendering.
- **Electron or browser-hosted UI:** Familiar web tooling, but conflicts with
  the native application requirement and adds an unnecessary runtime.
- **Native C++ with Direct2D/Direct3D:** Excellent control and performance, but
  requires the missing compiler/Windows SDK toolchain and substantially more
  bespoke accessibility and structured-control infrastructure.

## Consequences

The app is Windows-only, intentionally. Custom-drawn targets remain WPF buttons
so keyboard and automation semantics survive the visual treatment. The UI can
be polished without implementing a general-purpose game engine.

Sources consulted:

- https://learn.microsoft.com/en-us/dotnet/desktop/wpf/overview/
- https://learn.microsoft.com/en-us/windows/apps/get-started/start-here
