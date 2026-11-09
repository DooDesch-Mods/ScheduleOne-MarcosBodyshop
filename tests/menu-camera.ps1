<#
.SYNOPSIS
  Opens every top-level category of Marco's menu on one vehicle in the hall and checks where the camera went.
  One PASS or FAIL per category.

.DESCRIPTION
  Needs a running test slot with a Debug build of this mod and a loaded save, for example:
    pwsh -File Workspace/tools/session/session.ps1 -Build _wt-MarcosBodyshop-<topic>

  The script spawns the vehicle in Marco's hall, opens the menu and walks the categories with the page's keys.
  A category FAILs when the log says its shot stayed behind a wall ("blocked on both sides ... the shot stays").
  It PASSes when the view was free, mirrored to the other side, moved in before the wall, or handed to the hall
  camera. A screenshot per category is listed for a look.

  Run it on a fresh save per vehicle: a vehicle already standing in the hall would be the one the menu opens.
#>
[CmdletBinding()]
param(
    [ValidateSet('mptest', 'mptest2', 'mptest3', 'mptest4', 'mptest5', 'mptest6')]
    [string]$Slot = 'mptest',
    [string]$Vehicle = 'veeper',
    [int]$Categories = 12
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path "$PSScriptRoot\..\..").Path
$s1 = Join-Path $root 'Workspace\tools\bridge\s1.py'
$table = (& python (Join-Path $root 'Workspace\tools\slots\slots.py') table) | ConvertFrom-Json
$log = Join-Path ([string]$table.$Slot.root) 'MelonLoader\Latest.log'

function S1 { & python $s1 --slot $Slot @args }
function LogSince([int]$from) { $all = Get-Content $log; if ($all.Count -le $from) { return @() }; return $all[$from..($all.Count - 1)] }
function Lines { return (Get-Content $log).Count }

# The hall's vehicle detector stands at (5.0, 1.55, -12.5); spawn there, then step out of the way.
S1 tp 5 1.2 -12.5 | Out-Null
S1 run "spawnvehicle $Vehicle" | Out-Null
S1 tp 3 1.2 -18 | Out-Null
$start = Lines
S1 run 'bodyshop open' | Out-Null
Start-Sleep -Milliseconds 1500
$open = @(LogSince $start | Where-Object { $_ -match '\[Menu\] open:' })
if (-not $open) { Write-Host "FAIL open: no '[Menu] open:' line"; exit 1 }
Write-Host "info $($open[-1] -replace '^.*\[Menu\] ', '')"
if ($open[-1] -notmatch "bay 'hall'") { Write-Host "FAIL open: the vehicle is not in the hall bay"; exit 1 }

# The page keeps its cursor between openings: back to the first category.
for ($i = 0; $i -lt $Categories; $i++) { S1 run 'bodyshop key up' | Out-Null }
$fails = 0
for ($i = 0; $i -lt $Categories; $i++) {
    $at = Lines
    $shot = (S1 frame 'bodyshop key enter' --wait 1.5 | Where-Object { $_ -match '\.png\s*$' } | Select-Object -Last 1)
    $lines = @(LogSince $at | Where-Object { $_ -match '\[Menu\] camera view' })
    $stays = $lines | Where-Object { $_ -match 'the shot stays' }
    $what = if ($lines) { ($lines | Where-Object { $_ -notmatch 'blocker\(s\) faded' } | Select-Object -Last 1) -replace '^.*\[Menu\] ', '' } else { 'view free' }
    if (-not $what) { $what = 'view free' }
    if ($stays) { $fails++; Write-Host "FAIL category $($i + 1): $what  ($shot)" }
    else { Write-Host "PASS category $($i + 1): $what  ($shot)" }
    S1 run 'bodyshop key back' | Out-Null
    S1 run 'bodyshop key down' | Out-Null
}
S1 run 'bodyshop close' | Out-Null
exit ([int]($fails -gt 0))
