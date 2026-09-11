param(
    [Parameter(Mandatory=$true)][int]$GameProcessId,
    [string]$OutputPath = "artifacts/visual-refresh/gpu-memory.csv",
    [int]$MaxSeconds = 150
)
$ErrorActionPreference = "Stop"
$samples = [System.Collections.Generic.List[object]]::new()
$clock = [System.Diagnostics.Stopwatch]::StartNew()
$directory = Split-Path -Parent $OutputPath
if ($directory) { New-Item -ItemType Directory -Path $directory -Force | Out-Null }
while ($clock.Elapsed.TotalSeconds -lt $MaxSeconds) {
    $game = Get-Process -Id $GameProcessId -ErrorAction SilentlyContinue
    if ($null -eq $game) { break }
    $gpu = @(Get-CimInstance Win32_PerfFormattedData_GPUPerformanceCounters_GPUProcessMemory |
        Where-Object { $_.Name -like "pid_${GameProcessId}_*" })
    $samples.Add([pscustomobject]@{
        ProcessAgeSeconds = [Math]::Round(([DateTime]::Now - $game.StartTime).TotalSeconds, 2)
        Available = $gpu.Count -gt 0
        DedicatedBytes = if ($gpu.Count) { ($gpu | Measure-Object DedicatedUsage -Sum).Sum } else { $null }
        SharedBytes = if ($gpu.Count) { ($gpu | Measure-Object SharedUsage -Sum).Sum } else { $null }
        CommittedBytes = if ($gpu.Count) { ($gpu | Measure-Object TotalCommitted -Sum).Sum } else { $null }
        WorkingSetBytes = $game.WorkingSet64
        PrivateBytes = $game.PrivateMemorySize64
    })
    $samples | Export-Csv -LiteralPath $OutputPath -NoTypeInformation -Encoding utf8
    Start-Sleep -Seconds 10
}
Write-Output "GPU samples: $($samples.Count); output: $OutputPath"
