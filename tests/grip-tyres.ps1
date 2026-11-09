<#
.SYNOPSIS
  Buys Drift Tyres and then Grip Tyres on one vehicle in Marco's hall and checks that the second replaces the first.
  One PASS or FAIL per check.

.DESCRIPTION
  Needs a running test slot with a Debug build of this mod and of Chassis and a loaded save, for example:
    pwsh -File Workspace/tools/session/session.ps1 -Build _wt-MarcosBodyshop-<topic>

  Checks:
    1. The Tyres category lists both tyres at the same price.
    2. After buying Drift Tyres the car carries tyres_drift and its grip drops below 1.
    3. After buying Grip Tyres the car carries bodyshop_tyres_grip, grip x1.3, and the drift tyres are gone.
  Run it on a fresh save: a vehicle already standing in the hall would be the one the menu opens.
#>
[CmdletBinding()]
param(
    [ValidateSet('mptest', 'mptest2', 'mptest3', 'mptest4', 'mptest5', 'mptest6')]
    [string]$Slot = 'mptest',
    [string]$Vehicle = 'shitbox'
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path "$PSScriptRoot\..\..").Path
$s1 = Join-Path $root 'Workspace\tools\bridge\s1.py'
$table = (& python (Join-Path $root 'Workspace\tools\slots\slots.py') table) | ConvertFrom-Json
$log = Join-Path ([string]$table.$Slot.root) 'MelonLoader\Latest.log'

function S1 { & python $s1 --slot $Slot @args }
function Lines { return (Get-Content $log).Count }
function LogSince([int]$from) { $all = Get-Content $log; if ($all.Count -le $from) { return @() }; return $all[$from..($all.Count - 1)] }
$fails = 0
function Check([string]$name, [bool]$ok, [string]$detail) {
    if ($ok) { Write-Host "PASS $name  $detail" } else { $script:fails++; Write-Host "FAIL $name  $detail" }
}
function Tune {
    $at = Lines
    S1 run 'chassis tune' | Out-Null
    Start-Sleep -Milliseconds 500
    return (@(LogSince $at) -join "`n")
}

S1 tp 5 1.2 -12.5 | Out-Null
S1 run "spawnvehicle $Vehicle" | Out-Null
S1 tp 3 1.2 -18 | Out-Null
S1 run 'bodyshop open' | Out-Null
Start-Sleep -Milliseconds 1500

$at = Lines
S1 run 'bodyshop list tyres' | Out-Null
Start-Sleep -Milliseconds 500
$list = (@(LogSince $at | Where-Object { $_ -match 'list:' }) -join "`n")
Check 'list' (($list -match 'bodyshop_tyres_grip') -and ($list -match 'tyres_drift')) 'both tyres in the Tyres category'
$gripPrice = [regex]::Match($list, '"id":"bodyshop_tyres_grip"[^}]*?"price":(\d+)').Groups[1].Value
$driftPrice = [regex]::Match($list, '"id":"tyres_drift"[^}]*?"price":(\d+)').Groups[1].Value
Check 'price' (($gripPrice -ne '') -and ($gripPrice -eq $driftPrice)) "grip $gripPrice, drift $driftPrice"

S1 run 'bodyshop buy tyres/tyres_drift' | Out-Null
Start-Sleep -Seconds 2
$t = Tune
Check 'drift fitted' (($t -match 'part\.tyres\W+tyres_drift') -and ($t -match 'grip x0\.')) (([regex]::Match($t, 'grip x[\d.]+')).Value)

S1 run 'bodyshop buy tyres/bodyshop_tyres_grip' | Out-Null
Start-Sleep -Seconds 2
$t = Tune
Check 'grip fitted' (($t -match 'part\.tyres\W+bodyshop_tyres_grip') -and ($t -match 'grip x1\.3')) (([regex]::Match($t, 'grip x[\d.]+')).Value)
Check 'drift gone' (-not ($t -match 'tyres_drift')) 'one part per slot'

S1 run 'bodyshop close' | Out-Null
exit ([int]($fails -gt 0))
