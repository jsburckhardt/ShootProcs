param(
    [switch]$Verify,
    [switch]$NoPublish,
    [switch]$Installer,
    [string]$PublishDirectory = 'artifacts\ShootProcs',
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version = '1.0.0'
)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$dotnet = Join-Path $PSScriptRoot '.tools\dotnet\dotnet.exe'
if (-not (Test-Path $dotnet)) { $dotnet = (Get-Command dotnet -ErrorAction Stop).Source }
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$published = if ([System.IO.Path]::IsPathRooted($PublishDirectory)) {
    [System.IO.Path]::GetFullPath($PublishDirectory)
} else { Join-Path $PSScriptRoot $PublishDirectory }
if ($NoPublish -and $Installer) { throw '-Installer cannot be combined with -NoPublish.' }
function Invoke-DotNet {
    & $dotnet @args
    if ($LASTEXITCODE -ne 0) { throw "dotnet failed: $args" }
}
Invoke-DotNet build ShootProcs.sln -c Release "-p:Version=$Version" --nologo -v minimal
Invoke-DotNet test tests\ShootProcs.Tests -c Release --no-build --nologo -v minimal `
    --logger 'trx;LogFileName=behavior.trx' --results-directory artifacts\tests
Invoke-DotNet format ShootProcs.sln --verify-no-changes --no-restore --verbosity quiet
if (-not $NoPublish) {
    $artifacts = Join-Path $PSScriptRoot 'artifacts'
    $stage = New-Item -ItemType Directory -Path (Join-Path $artifacts ("package-" + [Guid]::NewGuid().ToString('N')))
    try {
        Invoke-DotNet publish src\ShootProcs.App -c Release -r win-x64 --self-contained true `
            "-p:Version=$Version" '-p:PublishSingleFile=true' '-p:IncludeNativeLibrariesForSelfExtract=true' `
            '-p:EnableCompressionInSingleFile=true' '-p:PublishTrimmed=false' `
            '-p:DebugType=None' '-p:DebugSymbols=false' -o $stage.FullName --nologo -v minimal
        $files = Get-Content packaging\files.json -Raw | ConvertFrom-Json
        foreach ($relative in $files) {
            if ($relative -eq 'ShootProcs.exe') { continue }
            $source = if ($relative -in @('LICENSE.txt', 'ThirdPartyNotices.txt')) {
                Join-Path (Split-Path $dotnet -Parent) $relative
            } else { Join-Path $PSScriptRoot $relative }
            $destination = Join-Path $stage.FullName $relative
            [System.IO.Directory]::CreateDirectory((Split-Path $destination)) | Out-Null
            Copy-Item -LiteralPath $source -Destination $destination -Force
        }
        $actual = @(Get-ChildItem -LiteralPath $stage.FullName -Recurse -File |
            ForEach-Object { $_.FullName.Substring($stage.FullName.Length + 1) })
        if (Compare-Object @($files | Sort-Object) @($actual | Sort-Object)) {
            throw 'Package contents do not match packaging\files.json.'
        }
        Compress-Archive -Path (Join-Path $stage.FullName '*') `
            -DestinationPath artifacts\ShootProcs-win-x64.zip -Force
        if ($Installer) {
            $compiler = & .\tools\Get-InnoSetup.ps1
            & $compiler /Q "/DAppVersion=$Version" "/DPublishDir=$($stage.FullName)" `
                "/O$artifacts" packaging\ShootProcs.iss
            if ($LASTEXITCODE -ne 0) { throw "Installer compilation failed: $LASTEXITCODE" }
        }
        [System.IO.Directory]::CreateDirectory($published) | Out-Null
        Copy-Item (Join-Path $stage.FullName '*') -Destination $published -Recurse -Force
        $assets = @('ShootProcs-win-x64.zip')
        if ($Installer) { $assets += 'ShootProcs-Setup.exe' }
        $checksums = foreach ($asset in $assets) {
            $hash = (Get-FileHash -LiteralPath (Join-Path $artifacts $asset) -Algorithm SHA256).Hash.ToLowerInvariant()
            "$hash  $asset"
        }
        [System.IO.File]::WriteAllLines((Join-Path $artifacts 'SHA256SUMS.txt'), [string[]]$checksums)
    }
    finally {
        Remove-Item -LiteralPath $stage.FullName -Recurse -Force
    }
}
if ($Verify) {
    Invoke-DotNet run --project tools\ShootProcs.Validation -c Release --no-build -- artifacts\validation
    $env:SHOOTPROCS_VERIFY_DIR = Join-Path $PSScriptRoot 'artifacts\ui'
    $env:SHOOTPROCS_FIXTURE_EXE = Join-Path $PSScriptRoot 'artifacts\validation\fixture\ShootProcs.Validation.exe'
    [System.IO.Directory]::CreateDirectory($env:SHOOTPROCS_VERIFY_DIR) | Out-Null
    $report = Join-Path $env:SHOOTPROCS_VERIFY_DIR 'ui-verification.json'
    if (Test-Path $report) { Remove-Item -LiteralPath $report }
    $executable = if ($NoPublish) {
        Join-Path $PSScriptRoot 'src\ShootProcs.App\bin\Release\net10.0-windows\ShootProcs.exe'
    } else { Join-Path $published 'ShootProcs.exe' }
    $process = Start-Process $executable '--verify-ui' -PassThru
    if (-not $process.WaitForExit(90000)) { throw "UI verification timed out; inspect PID $($process.Id)." }
    if ($process.ExitCode -ne 0) { throw "UI verification exited with code $($process.ExitCode)." }
    $result = Get-Content $report -Raw | ConvertFrom-Json
    if (-not $result.passed) { throw 'Native UI verification failed. See artifacts\ui\ui-verification.json.' }
    $result | ConvertTo-Json -Depth 6
}
