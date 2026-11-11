<#
.SYNOPSIS
  Another mod's trunk size stays: Bodyshop no longer resizes it back every two seconds. Buying or removing a trunk
  tier still sizes the trunk. One PASS or FAIL per check.

.DESCRIPTION
  Needs a running test slot with a Debug build of this mod and of Chassis and a loaded save 5, for example:
    pwsh -File Workspace/tools/session/session.ps1 -Build _wt-MarcosBodyshop-<topic>
  'bodyshop trunk foreign N' sizes the hall car's trunk the way another mod would. On the old build the size goes
  back within two seconds with a "(check)" line, so this script FAILs there.
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
function Cmd([string]$c, [int]$ms = 600) {
    $at = Lines
    S1 run $c | Out-Null
    Start-Sleep -Milliseconds $ms
    return (@(LogSince $at) -join "`n")
}
function TrunkLine {
    $t = Cmd 'bodyshop trunk'
    return ([regex]::Match($t, "trunk hall: (.*)")).Groups[1].Value
}
function Buy([string]$entry) { return Cmd "bodyshop buy trunk/$entry" 2500 }

$start = Lines
S1 tp 5 1.2 -12.5 | Out-Null
S1 run "spawnvehicle $Vehicle" | Out-Null
S1 tp 3 1.2 -18 | Out-Null
S1 run 'bodyshop open' | Out-Null
Start-Sleep -Milliseconds 1500

$t = TrunkLine
Check 'stock' ($t -match 'tier 0, 5 slot') $t

# A car without a Bodyshop tier: another mod's size stays.
Cmd 'bodyshop trunk foreign 12' | Out-Null
Start-Sleep -Seconds 5
$t = TrunkLine
Check 'no tier: foreign size stays' ($t -match 'tier 0, 12 slot.*left to another mod') $t

# Buying a tier is the player's choice and sizes the trunk again.
Buy 'bodyshop_trunk_1' | Out-Null
$t = TrunkLine
Check 'tier bought: Bodyshop sizes it' (($t -match 'tier 1, 10 slot') -and -not ($t -match 'left to another mod')) $t

# A car with a tier: another mod's later size stays too.
Cmd 'bodyshop trunk foreign 18' | Out-Null
Start-Sleep -Seconds 5
$t = TrunkLine
Check 'with tier: foreign size stays' ($t -match 'tier 1, 18 slot.*left to another mod') $t

# Back to stock is the player's choice again.
Buy 'stock' | Out-Null
$t = TrunkLine
Check 'stock bought: Bodyshop sizes it' (($t -match 'tier 0, 5 slot') -and -not ($t -match 'left to another mod')) $t
S1 run 'bodyshop close' | Out-Null

$after = @(LogSince $start)
$checks = @($after | Where-Object { $_ -match '\[Trunk\].*slot\(s\) \(check\)' })
Check 'no resize from the periodic check' ($checks.Count -eq 0) ("$($checks.Count) line(s)" + $(if ($checks.Count) { ': ' + $checks[0] } else { '' }))
$leaves = @($after | Where-Object { $_ -match '\[Trunk\].*Bodyshop leaves it' })
Check 'each foreign size logged once' ($leaves.Count -eq 2) "$($leaves.Count) line(s)"
$errors = @($after | Where-Object { $_ -match '\[Marco.*\[Trunk\].*(failed|Exception)' })
Check 'no trunk error' ($errors.Count -eq 0) $(if ($errors.Count) { $errors[0] } else { '' })

exit ([int]($fails -gt 0))
