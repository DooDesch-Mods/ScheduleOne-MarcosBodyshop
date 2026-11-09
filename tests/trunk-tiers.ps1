<#
.SYNOPSIS
  Buys the three trunk tiers on one car in Marco's hall, checks the slot counts, the refusal to shrink over items, and
  that a full trunk survives save and load. One PASS or FAIL per check.

.DESCRIPTION
  Needs a running test slot with a Debug build of this mod and of Chassis and a loaded save 5, for example:
    pwsh -File Workspace/tools/session/session.ps1 -Build _wt-MarcosBodyshop-<topic>
  Run it on a fresh save: a vehicle already standing in the hall would be the one the menu opens.
  The default car is the Shitbox (5 slots): the tiers take it to 10, 15 and 20.
#>
[CmdletBinding()]
param(
    [ValidateSet('mptest', 'mptest2', 'mptest3', 'mptest4', 'mptest5', 'mptest6')]
    [string]$Slot = 'mptest',
    [string]$Vehicle = 'shitbox',
    [switch]$NoReload
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
# Runs a console command and returns the log lines it wrote, joined.
function Cmd([string]$c, [int]$ms = 600) {
    $at = Lines
    S1 run $c | Out-Null
    Start-Sleep -Milliseconds $ms
    return (@(LogSince $at) -join "`n")
}
# The trunk line of the car in the hall, from 'bodyshop trunk'. After a load the Chassis showroom adds a second car of
# the same code, so the hall line names the one car.
function TrunkLine {
    $t = Cmd 'bodyshop trunk'
    return ([regex]::Match($t, "trunk hall: (.*)")).Groups[1].Value
}
function Buy([string]$entry) { return Cmd "bodyshop buy trunk/$entry" 2500 }

S1 tp 5 1.2 -12.5 | Out-Null
S1 run "spawnvehicle $Vehicle" | Out-Null
S1 tp 3 1.2 -18 | Out-Null
S1 run 'bodyshop open' | Out-Null
Start-Sleep -Milliseconds 1500

$t = TrunkLine
Check 'stock' ($t -match 'tier 0, 5 slot') $t
$props = Cmd 'bodyshop trunk'
Check 'props' (($props -match 'trunk props:') -and -not ($props -match 'missing')) (([regex]::Match($props, 'trunk props: .*')).Value)

$list = (Cmd 'bodyshop list trunk')
foreach ($e in @(@('bodyshop_trunk_1', 1250), @('bodyshop_trunk_2', 2500), @('bodyshop_trunk_3', 4375))) {
    $price = [regex]::Match($list, '"id":"' + $e[0] + '"[^}]*?"price":(\d+)').Groups[1].Value
    Check "list $($e[0])" ($price -eq [string]$e[1]) "price $price"
}

foreach ($tier in 1..3) {
    $want = [Math]::Min(20, 5 + 5 * $tier)
    $rows = [Math]::Max(1, [Math]::Ceiling($want / 10))
    Buy "bodyshop_trunk_$tier" | Out-Null
    $t = TrunkLine
    Check "tier $tier" ($t -match "tier $tier, $want slot\(s\), wants $want, rows $rows") $t
}

# An item in slot 18 blocks going back to tier 1 (10 slots).
Cmd 'bodyshop trunk put 18' | Out-Null
$r = Buy 'bodyshop_trunk_1'
$t = TrunkLine
Check 'shrink refused' (($r -match 'trunk space|Kofferraum') -and ($t -match 'tier 3, 20 slot')) (([regex]::Match($r, 'buy: .*')).Value + ' / ' + $t)
Cmd 'bodyshop trunk take 18' | Out-Null
Cmd 'bodyshop trunk put 7' | Out-Null
Buy 'bodyshop_trunk_1' | Out-Null
$t = TrunkLine
Check 'shrink when empty' ($t -match 'tier 1, 10 slot.*filled \[7\]') $t

Buy 'bodyshop_trunk_3' | Out-Null
Cmd 'bodyshop trunk put 19' | Out-Null
$t = TrunkLine
Check 'full trunk' ($t -match 'tier 3, 20 slot.*filled \[7,19\]') $t
S1 run 'bodyshop close' | Out-Null

if (-not $NoReload) {
    & python (Join-Path $PSScriptRoot 'reload-save.py') --slot $Slot --save 5 | Select-Object -Last 1
    Start-Sleep -Seconds 4
    $t = TrunkLine
    Check 'after load' ($t -match 'tier 3, 20 slot.*filled \[7,19\]') $t
}

exit ([int]($fails -gt 0))
