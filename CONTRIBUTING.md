# Contributing to ShootProcs

Build something memorable, but never trade accuracy or safety for spectacle.
Arena and Process Explorer must remain two views of the same observations and
commands, not competing implementations.

## Get started

Use Windows 11 x64 and the .NET SDK selected by `global.json`. Clone the
repository and open PowerShell in its directory. The private `.tools` SDK is a
local convenience, not a prerequisite or a committed dependency.

```powershell
just dev       # build and run from source
just test      # build, behavior tests and formatting
just verify    # also exercise Windows adapters and native UI
```

`just` is optional. Without it:

```powershell
dotnet run --project .\src\ShootProcs.App
.\build.ps1 -NoPublish
.\build.ps1 -NoPublish -Verify
```

Close an app before replacing its executable. For a separate, verified package:

```powershell
.\build.ps1 -Installer -Verify -PublishDirectory artifacts\release-candidate
.\tools\Test-Installer.ps1
```

The installer test refuses to overwrite an existing installation or Start-menu
shortcut. Do not remove that guard just to make a local run pass.

## Product and engineering principles

- **Measure honestly.** Distinguish resource consumption from evidence of
  pressure. Unavailable data is not zero; private commit is not exclusive
  physical RAM; network activity alone does not prove a bottleneck. Explain
  units, smoothing, attribution limits, and changed scoring.
- **Group applications, not executable names.** Preserve owner/session,
  installation, reliable profile, ancestry, and shared-runtime job boundaries.
  Avoid double-counting measurements.
- **Keep termination bounded.** Capture PID plus creation time, recheck
  protections on the actual native handle, and report partial outcomes.
  Do not sweep descendants, chase restarts, include later instances, bypass
  essential-process/self protection, or silently widen permissions.
- **Least privilege by default.** Normal launch and per-user installation need
  no admin. Kernel tracing is an explicit UAC choice that elevates the whole
  app. Safe Mode confirms actions; it is not a permission boundary.
- **Native, accessible, and responsive.** Preserve keyboard access, meaningful
  automation names, reduced motion, stable inspection targets, bounded
  animations, virtualized rows, and low collection/rendering overhead.
- **Small, coherent changes.** Follow existing component boundaries and
  conventions. Avoid unrelated refactors, speculative abstractions, and new
  dependencies without a concrete reason.
- **Local data stays local.** No analytics, uploads, external service calls,
  automatic elevation, or automatic update behavior without an explicit
  product/security decision.

## Tests and evidence

Add focused tests for behavior introduced or fixed, preferably in existing
test files. Use the smallest relevant test set while iterating, then run the
existing build/format and affected integration checks before submitting.
Do not weaken assertions or fabricate success to get a green run.

**Termination tests may target only disposable processes created by the test.**
Never terminate the contributor's existing editor, browser, terminals, services,
or other applications. Preserve fixture identity guards and bounded cleanup.
Changes to termination, grouping, or telemetry need the appropriate behavior
and real Windows checks, not just compilation.

For elevated disk/network changes, run the dedicated fixture from an elevated
PowerShell after building:

```powershell
.\tools\ShootProcs.Validation\bin\Release\net10.0-windows\ShootProcs.Validation.exe --verify-telemetry artifacts\validation\elevated
```

This writes its own temporary file and uses localhost TCP. Report which checks
actually ran, privilege level, observed results, and anything unverified.
Local reports and screenshots can contain private process details: never
commit them or upload them unredacted. Do not use personal desktop screenshots
as public product previews.

## Documentation and architecture

Update the README and permission/install guidance when user-facing behavior
changes. Maintain `docs\components.md` and `docs\architecture.md` when contracts
or flows change; record significant decisions and tradeoffs in `docs\adrs`.

Use **Mermaid sequence diagrams** for architectural interactions and flows.
Use **Excalidraw** for component diagrams, keeping editable sources alongside
their rendered SVG previews under `docs\diagrams`. Keep previews and sources
consistent; diagrams must describe the implementation, not an aspiration.

If a new document is needed in the downloadable app, add it explicitly to
`packaging\files.json`. Never replace the payload allowlist with a broad copy
of the workspace or diagnostics directory.

## Dependencies and automation

Use existing tools and justify new packages. Commit both ordinary
`packages.lock.json` and affected `packages.publish.lock.json` changes when
dependencies or the SDK change. Verify the locked restore paths described in
[installation](docs/installation.md).

Pin workflow actions to commit SHAs, keep token permissions minimal, and never
execute untrusted PR code through privileged `pull_request_target` workflows.
Preserve the version/SHA-256 check for the installer compiler. Do not commit
tokens, environment files, certificates, signing keys, traces, or build outputs.

## Submit a pull request

Explain the problem, focused change, user-visible effects, and relevant
tradeoffs. Include commands/results, limitations, and documentation/ADR changes.
For visual changes, use sanitized fixture-only evidence. Keep changes compatible
with normal unelevated use and explicitly describe any permission implications.
Contributions are provided under this repository's [MIT license](LICENSE).

Report exploitable security issues privately using [SECURITY.md](SECURITY.md),
not a public issue with sensitive reproduction data.
