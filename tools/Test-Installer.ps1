param(
    [string]$Installer = (Join-Path (Split-Path $PSScriptRoot -Parent) 'artifacts\ShootProcs-Setup.exe')
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$key = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{9325CD84-7B69-4583-9542-FD32A0823521}_is1'
$shortcut = Join-Path ([Environment]::GetFolderPath('Programs')) 'ShootProcs.lnk'
if ((Test-Path -LiteralPath $key) -or (Test-Path -LiteralPath $shortcut)) {
    throw 'An installed ShootProcs or Start-menu shortcut already exists; refusing to overwrite it for a test.'
}
$artifacts = Join-Path $root 'artifacts'
$destination = Join-Path $artifacts ("install-test-" + [Guid]::NewGuid().ToString('N'))
$checks = @{}
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
try {
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    $checks['ranWithoutAdmin'] = -not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}
finally { $identity.Dispose() }
if (-not $checks.ranWithoutAdmin) {
    Write-Warning 'This runner is elevated; per-user paths are tested, but no-admin execution requires an unelevated run.'
}
$app = $null
try {
    $setup = Start-Process -FilePath $Installer -ArgumentList @(
        '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/TASKS=',
        ('/DIR="' + $destination + '"'),
        ('/LOG="' + (Join-Path $artifacts 'installer-test.log') + '"')
    ) -PassThru
    if (-not $setup.WaitForExit(60000) -or $setup.ExitCode -ne 0) {
        throw "Installation failed or timed out: PID $($setup.Id)."
    }
    $installed = Join-Path $destination 'ShootProcs.exe'
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead((Join-Path $artifacts 'ShootProcs-win-x64.zip'))
    $stream = $archive.GetEntry('ShootProcs.exe').Open()
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        $expectedHash = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '')
    }
    finally { $sha.Dispose(); $stream.Dispose(); $archive.Dispose() }
    $checks['installedExactPublishedBinary'] = (Get-FileHash $installed).Hash -eq $expectedHash
    $entry = Get-ItemProperty -LiteralPath $key
    $checks['registeredPerUserUninstall'] = $entry.InstallLocation.TrimEnd('\') -eq $destination
    $checks['startMenuShortcutCreated'] = Test-Path -LiteralPath $shortcut
    $app = Start-Process -FilePath $installed -PassThru
    $checks['installedAppResponsive'] = $false
    for ($i = 0; $i -lt 30; $i++) {
        Start-Sleep -Milliseconds 500
        $app.Refresh()
        if ($app.HasExited) { throw "Installed app exited: $($app.ExitCode)." }
        if ($app.MainWindowHandle -ne 0 -and $app.Responding) {
            $checks['installedAppResponsive'] = $true
            break
        }
    }
    if (-not $checks.installedAppResponsive) { throw 'Installed app did not open a responsive window.' }
}
finally {
    if ($app -and -not $app.HasExited) {
        if (-not $app.CloseMainWindow() -or -not $app.WaitForExit(15000)) {
            throw "Test app did not close normally: PID $($app.Id). No force termination attempted."
        }
    }
    if (Test-Path -LiteralPath $key) {
        $entry = Get-ItemProperty -LiteralPath $key
        if ($entry.InstallLocation.TrimEnd('\') -ne $destination) {
            throw 'Uninstall registration changed; refusing to remove another installation.'
        }
        $uninstaller = Start-Process -FilePath (Join-Path $destination 'unins000.exe') `
            -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART' -PassThru
        if (-not $uninstaller.WaitForExit(60000) -or $uninstaller.ExitCode -ne 0) {
            throw "Uninstall failed or timed out: PID $($uninstaller.Id)."
        }
    }
}
$checks['uninstallRemovedApp'] = -not (Test-Path -LiteralPath (Join-Path $destination 'ShootProcs.exe'))
$checks['uninstallRemovedRegistration'] = -not (Test-Path -LiteralPath $key)
$checks['uninstallRemovedShortcut'] = -not (Test-Path -LiteralPath $shortcut)
$checks['passed'] = -not ($checks.GetEnumerator() |
    Where-Object { $_.Key -ne 'ranWithoutAdmin' -and $_.Value -eq $false })
$checks | ConvertTo-Json | Set-Content (Join-Path $artifacts 'installer-verification.json')
if (-not $checks.passed) { throw 'Installer lifecycle verification failed.' }
$checks | ConvertTo-Json
