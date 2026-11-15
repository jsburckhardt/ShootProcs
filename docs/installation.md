# Install, update, and remove ShootProcs

## Recommended: per-user setup

Requirements: Windows 11 x64, a normal Windows user account, and enough free
space for the bundled application/runtime. No SDK or separate .NET runtime is
needed. Administrator rights are not required to install or use CPU/memory
monitoring.

1. Open the [official releases](https://github.com/jsburckhardt/ShootProcs/releases/latest).
2. Download `ShootProcs-Setup.exe`.
3. Run setup and choose Install. The default destination is
   `%LOCALAPPDATA%\Programs\ShootProcs`.
4. Open ShootProcs from Start. Optionally select a desktop shortcut during setup.

Setup registers only a per-user uninstall entry and shortcuts. It does not
install a service, driver, scheduled task, startup entry, or machine-wide
prerequisite. Administrator access is a separate, explicit runtime choice for
[kernel disk/network tracing](permissions.md).

Current builds are unsigned. SmartScreen, antivirus, or organizational policy
can warn or refuse to run them. Do not disable those protections. Only obtain
builds from the official repository and follow your organization's software
policy. Authenticode signing is not yet available.

## Portable alternative

Download `ShootProcs-win-x64.zip`, choose Extract All, and open `ShootProcs.exe`
from the extracted directory. Do not run it inside the ZIP. Keep the included
documentation and third-party notices when redistributing it.

## Verify the download

Download `SHA256SUMS.txt` from the same release. In the download directory:

```powershell
Get-FileHash .\ShootProcs-Setup.exe -Algorithm SHA256
# Or:
Get-FileHash .\ShootProcs-win-x64.zip -Algorithm SHA256
```

Compare the complete hash to the matching filename in `SHA256SUMS.txt`,
ignoring letter case. A mismatch means do not run that file; download again.
Hashes detect file changes, but a checksum hosted beside a download is not an
independent publisher signature.

## Update

Close ShootProcs, especially if it is elevated. Download and run the new setup
from the official release. It reuses the same per-user application identity and
replaces installed files. Safe Mode starts ON when the updated application opens.
Windows may require you to close an elevated window yourself.

For portable use, close the app and extract the new ZIP into a new directory.
There is no automatic updater. The bundled .NET runtime is updated by installing
a newer ShootProcs release, not by installing an SDK.

## Uninstall

For setup installations, use Windows **Settings > Apps > Installed apps >
ShootProcs > Uninstall**. Close an elevated ShootProcs window first.

For portable use, close the app and remove its extracted directory and any
shortcut you created. No service or background task remains.

## Build your own

Install the SDK selected by `global.json`, open PowerShell in a source checkout:

```powershell
.\build.ps1 -Verify -Installer
```

Outputs:

- `artifacts\ShootProcs-Setup.exe`
- `artifacts\ShootProcs-win-x64.zip`
- `artifacts\SHA256SUMS.txt`
- `artifacts\ShootProcs\ShootProcs.exe`

The compiler used for setup is pinned in `packaging\toolchain.json`; its
download is checked against SHA-256 before execution. Packaging starts in a
new staging directory and checks every payload file against
`packaging\files.json`, preventing local diagnostics or stale files from entering
the distribution.

Source dependencies use `packages.lock.json`; the single-file Windows publish
graph uses separate `packages.publish.lock.json` files because it additionally
restores SDK linker/runtime assets. CI enforces locked mode for both. When
intentionally changing dependencies or the SDK, run the build locally to
regenerate both graphs and commit the updated lockfiles.
