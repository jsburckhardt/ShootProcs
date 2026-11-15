$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$tool = (Get-Content (Join-Path $root 'packaging\toolchain.json') -Raw | ConvertFrom-Json).innoSetup
$directory = Join-Path $root ".tools\innosetup-$($tool.version)"
$compiler = Join-Path $directory 'ISCC.exe'
if (-not (Test-Path -LiteralPath $compiler)) {
    $downloads = Join-Path $root '.tools\downloads'
    [System.IO.Directory]::CreateDirectory($downloads) | Out-Null
    $installer = Join-Path $downloads "innosetup-$($tool.version)-x64.exe"
    if (-not (Test-Path -LiteralPath $installer)) {
        Invoke-WebRequest -Uri $tool.url -OutFile $installer -UseBasicParsing
    }
    if ((Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash -ine $tool.sha256) {
        throw "Installer compiler checksum mismatch: $installer"
    }
    $process = Start-Process -FilePath $installer -ArgumentList @(
        '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/CURRENTUSER',
        ('/DIR="' + $directory + '"')
    ) -Wait -PassThru
    if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $compiler)) {
        throw "Inno Setup installation failed with code $($process.ExitCode)."
    }
}
return $compiler
