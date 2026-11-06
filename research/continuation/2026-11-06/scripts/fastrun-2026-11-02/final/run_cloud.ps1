# RideBound final runs on a rented Windows machine (ADR-089 GD5/GD6, 2026-11-04).
# Run from the folder that holds ridebound-final-v2.zip and MANIFEST.tsv, in Windows PowerShell 5.1 or later:
#   powershell -ExecutionPolicy Bypass -File run_cloud.ps1 -Unpack D:\E -Workers 8
# Steps (each stops the script on failure; rerunning the script resumes: finished jobs are skipped):
#   0 prerequisites (Git, VC++ runtime)   1 unpack and map the folder to E:   2 verify every file (SHA-256)
#   3 cloud = home: 4 jobs whose frames must equal this bundle's reference evidence frame by frame
#   4 the 168 final jobs                  5 all comparisons and metrics -> E:\runs\report\FINAL-REPORT.json
param(
    [string]$Unpack = "D:\E",
    [int]$Workers = 8,
    [double]$RamFloorGiB = 6.5,
    [double]$DiskFloorGiB = 4
)
$ErrorActionPreference = "Stop"
function Step($name) { Write-Host ("[{0}] {1}" -f (Get-Date -Format s), $name) }
function Check($code, $what) { if ($code -ne 0) { throw "$what failed (exit $code)" } }

Step "0 prerequisites"
if (-not (Get-Command git -ErrorAction SilentlyContinue)) { throw "Git is required: winget install --id Git.Git -e" }
if (-not (Test-Path "$env:WINDIR\System32\msvcp140.dll")) {
    throw "The VC++ 2015-2022 x64 runtime is required: winget install --id Microsoft.VCRedist.2015+.x64 -e"
}

Step "1 unpack and map E:"
if (-not (Test-Path (Join-Path $Unpack "RideBoundData\final-v2"))) {
    New-Item -ItemType Directory -Force $Unpack | Out-Null
    # .NET, not tar.exe: Windows tar skips the one UTF-8 file name (Vietnamese letters) and fails (tested 2026-11-04).
    # Keep -Unpack short (e.g. D:\E): the longest path is 216 characters under it, and Windows stops at 260.
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [IO.Compression.ZipFile]::ExtractToDirectory((Resolve-Path "ridebound-final-v2.zip").Path, $Unpack)
}
if (-not (Test-Path "E:\RideBoundData\final-v2")) {
    if (Test-Path "E:\") { throw "E: is taken; unpack onto E:\ itself (-Unpack E:\) instead of mapping a folder" }
    subst E: $Unpack
    Check $LASTEXITCODE "subst E:"
}
$py = "E:\RideBoundData\wp7\envs\fleetpy-1.0.2\python.exe"
$final = "E:\Code\Report_INT3508\ridebound-scratchpad\fastrun-2026-11-02\final"
$jobs = "E:\RideBoundData\research\fullsys-f14-v2\jobs-bundle-v2"
$runs = "E:\runs\final"
$logs = "E:\runs\logs"
$stop = "E:\runs\STOP"
New-Item -ItemType Directory -Force $runs, $logs | Out-Null

Step "2 verify the bundle"
& $py -I -B "$final\verify_bundle.py" MANIFEST.tsv
Check $LASTEXITCODE "bundle verification"

Step "3 gates: cloud = home, main arms, attribution arms (8 jobs)"
& $py -B "$final\run_queue_final.py" --jobs "$jobs\jobs-cloud-check.json" --out $runs --logs $logs --stop $stop `
    --workers 4 --ram-floor-gib $RamFloorGiB --disk-floor-gib $DiskFloorGiB
Check $LASTEXITCODE "cloud-check jobs"
& $py -B "$final\run_queue_final.py" --jobs "$jobs\jobs-gate-attr.json" --out "E:\runs\gates" --logs $logs --stop $stop `
    --workers 4 --ram-floor-gib $RamFloorGiB --disk-floor-gib $DiskFloorGiB
Check $LASTEXITCODE "attribution gate jobs"
if (-not (Test-Path "E:\runs\gates-report\GATES-REPORT.json")) {
    & $py -I -B "$final\cloud_gates.py" --check-runs $runs --attr-runs "E:\runs\gates" --out "E:\runs\gates-report"
    Check $LASTEXITCODE "gates (see E:\runs\gates-report; a difference means STOP and report, do not run the final jobs)"
}

Step "4 final jobs (168; finished ones are skipped)"
& $py -B "$final\run_queue_final.py" --jobs "$jobs\jobs-final.json" --out $runs --logs $logs --stop $stop `
    --workers $Workers --ram-floor-gib $RamFloorGiB --disk-floor-gib $DiskFloorGiB
Check $LASTEXITCODE "final jobs"

Step "5 comparisons and metrics"
& $py -I -B "$final\final_report.py" --runs $runs --out "E:\runs\report"
Check $LASTEXITCODE "final report (see E:\runs\report\*.log)"
Step "done: copy E:\runs back (evidence, logs, report)"
