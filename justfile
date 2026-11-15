set windows-shell := ["powershell.exe", "-NoLogo", "-NoProfile", "-Command"]

# Show available commands.
default:
    @just --list

# Launch the portable build without administrator privileges.
run directory='artifacts\ShootProcs':
    $exe = Join-Path '{{directory}}' 'ShootProcs.exe'; if (-not (Test-Path -LiteralPath $exe)) { throw 'No portable build found. Run just build first, or select the folder passed to -PublishDirectory.' }; Start-Process -FilePath $exe

# Build and run from source, using the private SDK when available.
dev:
    $sdk = if (Test-Path '.\.tools\dotnet\dotnet.exe') { '.\.tools\dotnet\dotnet.exe' } else { 'dotnet' }; & $sdk run --project .\src\ShootProcs.App -c Release; exit $LASTEXITCODE

# Build, test, check formatting, and publish the portable app and ZIP.
build:
    & .\build.ps1

# Run behavior tests and formatting checks without packaging.
test:
    & .\build.ps1 -NoPublish

# Also exercise real Windows adapters and native UI with disposable workers.
verify:
    & .\build.ps1 -NoPublish -Verify

# Build the installer, portable ZIP, checksums, and verify the published app.
package:
    & .\build.ps1 -Installer -Verify

# Verify installation, launch, and uninstall without replacing an installed app.
test-installer:
    & .\tools\Test-Installer.ps1
