# Security policy

## Reporting

Please report exploitable vulnerabilities privately through
[GitHub private vulnerability reporting](https://github.com/jsburckhardt/ShootProcs/security/advisories/new).
Do not post credentials, full process inventories, usernames, command lines,
or unredacted desktop screenshots in public issues.

For ordinary defects, use the issue tracker with reproduction steps, app version,
Windows version, whether it was elevated, and a redacted status message.

## Supported versions

Security fixes target the latest public release. Older bundled .NET runtimes
are not automatically serviced by ShootProcs; install a newer application
release when fixes are published.

## Security boundaries

See [permissions](docs/permissions.md). Safe Mode is confirmation, not privilege
isolation. Direct process termination can lose unsaved work. Elevation applies
to the entire app; it remains opt-in.

Current binaries are unsigned. Download only official release assets and compare
their SHA-256 checksums. Do not disable antivirus, SmartScreen, or enterprise
policy to install this app.

## Repository controls

CodeQL scans C# on main/PR changes and weekly using security-extended queries.
Dependency review checks pull requests; Dependabot tracks NuGet and pinned
GitHub Actions. Lockfiles and an explicit NuGet source constrain dependency
resolution. Workflows use read-only tokens except CodeQL result submission and
the tag-release publisher.

Release payloads are allowlisted and built in fresh staging directories.
Source control and release uploads exclude local process inventories,
unapproved screenshots, traces, SDKs, certificates/private keys, and environment
files. The README image was explicitly supplied for publication by the project
owner and reviewed for sensitive details. That permission does not extend to
other local screenshots or diagnostic reports.
Automated scanning reduces risk; it is not a guarantee that software has no
vulnerabilities.
