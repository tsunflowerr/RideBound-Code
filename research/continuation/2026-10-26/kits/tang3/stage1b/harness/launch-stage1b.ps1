# Stage 1b launcher (coordinator, 2026-10-14). Not sealed; it defines the settled launch environment of plan §11.
# Every freeze, run and verdict command goes through this script, so the environment is identical whoever starts it
# and however the parent process was configured. Usage (from any PowerShell):
#   powershell.exe -NoProfile -ExecutionPolicy Bypass -File launch-stage1b.ps1 -Tool driver     -Mode freeze -Phase gate0 -Log <new log>
#   powershell.exe -NoProfile -ExecutionPolicy Bypass -File launch-stage1b.ps1 -Tool driver     -Mode run    -Phase gate0 -Log <new log>
#   powershell.exe -NoProfile -ExecutionPolicy Bypass -File launch-stage1b.ps1 -Tool comparator -Mode gate0  -Phase gate0-compare-<tag>.txt -Log <new log>
param(
    [Parameter(Mandatory = $true)][ValidateSet("driver", "comparator")][string]$Tool,
    [Parameter(Mandatory = $true)][string]$Mode,
    [Parameter(Mandatory = $true)][string]$Phase,
    [Parameter(Mandatory = $true)][string]$Log,
    [string]$Version = "v7"
)
$ErrorActionPreference = "Stop"
$harness = "E:\Code\Report_INT3508\ridebound-scratchpad\tang3\stage1b\harness"
$python = "E:\RideBoundData\wp7\envs\fleetpy-1.0.2\python.exe"

# 1. Remove every steering variable family, then set the fixed values.
$prefixes = @("GIT_", "PYTHON", "DOTNET_", "COREHOST_", "COMPLUS_", "OMP_", "MKL_", "OPENBLAS_", "RB_")
foreach ($item in @(Get-ChildItem Env:)) {
    foreach ($prefix in $prefixes) {
        if ($item.Name.ToUpperInvariant().StartsWith($prefix)) { Remove-Item -LiteralPath "Env:$($item.Name)"; break }
    }
}
foreach ($name in @("NoDefaultCurrentDirectoryInExePath", "SETUPTOOLS_USE_DISTUTILS")) {
    if (Test-Path -LiteralPath "Env:$name") { Remove-Item -LiteralPath "Env:$name" }
}
$env:PATH = "C:\Windows\system32;C:\Windows;C:\Windows\System32\Wbem;C:\Windows\System32\WindowsPowerShell\v1.0\;C:\Program Files\dotnet\;C:\Program Files\Git\cmd"
$env:PATHEXT = ".COM;.EXE;.BAT;.CMD;.VBS;.VBE;.JS;.JSE;.WSF;.WSH;.MSC;.CPL"

# 2. Run the requested harness file from the harness folder; the log must be new.
Set-Location -LiteralPath $harness
if (Test-Path -LiteralPath $Log) { throw "log exists: $Log" }
$script = if ($Tool -eq "driver") { "run_stage1b-$Version.py" } else { "compare_equivalence_stage1b-$Version.py" }
"launch $(Get-Date -Format o) tool=$Tool script=$script mode=$Mode phase=$Phase" | Out-File -LiteralPath $Log -Encoding utf8
& $python -B $script $Mode $Phase *>> $Log
$code = $LASTEXITCODE
"exit=$code at $(Get-Date -Format o)" | Out-File -LiteralPath $Log -Encoding utf8 -Append
exit $code
