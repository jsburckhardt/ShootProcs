# ADR 0005: Public repository and per-user distribution

Status: Accepted

## Context

The first local delivery was portable. Public users need an install path that
does not require a developer SDK, administrator access, or manually managing
shortcuts. The owner approved `jsburckhardt/ShootProcs` as a public MIT project.

## Decision

Retain the self-contained Windows x64 executable/ZIP and add an Inno Setup
per-user installer. Use `PrivilegesRequired=lowest`, a local-application-data
destination, Start-menu integration, an optional desktop shortcut, and standard
Windows uninstall registration. Do not install services, drivers, startup
entries, or runtime prerequisites.

Separate installation from privilege: normal launch is `asInvoker`; kernel
disk/network collection requires explicit runtime UAC elevation of the entire
app. Document limitations, direct-termination consequences, and same-identity
constraints without presenting Safe Mode as a security boundary.

Build release payloads in new staging directories and enforce a file allowlist.
Pin the installer compiler by version and SHA-256. Commit NuGet lockfiles and
pin GitHub Actions to commit SHAs. Build/test release tags from source, then
publish only setup, portable ZIP, and SHA-256 checksums.

Use CodeQL security-extended queries, dependency review, Dependabot, and GitHub
secret scanning/push protection. Keep workflow tokens read-only by default;
grant write access only to analysis submission and release publication.

## Consequences

Users need neither administrator rights nor .NET installation for ordinary
features. Updates remain manual and closing elevated instances may require
user action. Bundled runtime security fixes require a new app release.

Windows signing is not supplied by GitHub code scans or checksums. Unsigned
build warnings remain possible; Authenticode signing and Store/winget
distribution are future work. Local diagnostics and machine inventories are
not public assets. Inno Setup retains its own license; its compiler is a build
dependency, not an installed application component.

This supersedes only ADR 0004's decision to omit an installer; its rendering
and self-contained runtime decisions remain unchanged.
