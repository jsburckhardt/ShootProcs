$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$executable = Join-Path $root 'artifacts\ShootProcs\ShootProcs.exe'
if (-not (Test-Path $executable)) { throw 'Build the portable application first with .\build.ps1.' }
$app = Start-Process $executable -PassThru
$measurements = @()
try {
    $cpu = [TimeSpan]::Zero
    $watch = [Diagnostics.Stopwatch]::StartNew()
    $last = 0.0
    foreach ($bucket in 1..6) {
        Start-Sleep -Seconds 10
        $app.Refresh()
        $elapsed = $watch.Elapsed.TotalSeconds
        $next = $app.TotalProcessorTime
        $entry = [pscustomobject]@{
            Seconds = [math]::Round($elapsed, 1)
            MachineCpuPercent = [math]::Round(($next - $cpu).TotalSeconds / ($elapsed - $last) / [Environment]::ProcessorCount * 100, 2)
            PrivateMiB = [math]::Round($app.PrivateMemorySize64 / 1MB, 1)
            Responsive = $app.Responding
        }
        $measurements += $entry
        $entry | ConvertTo-Json -Compress
        $cpu = $next
        $last = $elapsed
    }
    $directory = Join-Path $root 'artifacts\ui'
    [System.IO.Directory]::CreateDirectory($directory) | Out-Null
    $measurements | ConvertTo-Json | Set-Content (Join-Path $directory 'release-performance.json')
}
finally {
    $app.Refresh()
    if (-not $app.HasExited) {
        $null = $app.CloseMainWindow()
        if (-not $app.WaitForExit(10000)) { throw "Owned performance-check application did not close; inspect PID $($app.Id)." }
    }
    $app.Dispose()
}
