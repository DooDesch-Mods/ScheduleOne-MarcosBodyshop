<#
.SYNOPSIS
  Takes the sign-off pictures of the roof loads: every tier on the Shitbox, tier 1 on the Veeper and tier 3 on the
  Dinkler, two views each, in Marco's hall. One PASS or FAIL per picture (FAIL = no file).

.DESCRIPTION
  Needs a running test slot with a Debug build of this mod and of Chassis and a loaded save 5 with an empty hall:
    pwsh -File Workspace/tools/session/session.ps1 -Build _wt-MarcosBodyshop-<topic>
  Each car is moved out of the hall (bodyshop trunk move) before the next one comes in.
#>
[CmdletBinding()]
param(
    [ValidateSet('mptest', 'mptest2', 'mptest3', 'mptest4', 'mptest5', 'mptest6')]
    [string]$Slot = 'mptest',
    [string]$Out = "$PSScriptRoot\..\..\_drafts\trunk46"
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path "$PSScriptRoot\..\..").Path
$s1 = Join-Path $root 'Workspace\tools\bridge\s1.py'
New-Item -ItemType Directory -Force $Out | Out-Null
$Out = (Resolve-Path $Out).Path
function S1 { & python $s1 --slot $Slot @args }
$fails = 0

# The car stands at about (4.3, 0.7, -12.5) in the hall; these views clear its walls.
$views = @(
    @{ Name = 'side'; Eye = '8.0 3.0 -8.0'; Target = '4.3 1.4 -12.5' },
    @{ Name = 'high'; Eye = '9.1 4.8 -10.4'; Target = '4.3 1.2 -12.5' }
)

function Shoot([string]$tag) {
    S1 tp 3 1.2 -25 | Out-Null
    foreach ($v in $views) {
        $eye = $v.Eye -split ' '
        $target = $v.Target -split ' '
        $json = (S1 cam shot --eye @eye --target @target --fov 50 --hud hide) -join "`n"
        $m = [regex]::Match($json, '"path":\s*"([^"]+)"')
        $file = Join-Path $Out "$tag-$($v.Name).png"
        if ($m.Success) {
            Copy-Item ($m.Groups[1].Value -replace '\\\\', '\') $file -Force
            Write-Host "PASS $tag-$($v.Name)  $file"
        } else { $script:fails++; Write-Host "FAIL $tag-$($v.Name)  $json" }
    }
    S1 cam release | Out-Null
}

function Car([string]$code, [int[]]$tiers) {
    S1 tp 5 1.2 -12.5 | Out-Null
    S1 run "spawnvehicle $code" | Out-Null
    S1 tp 3 1.2 -18 | Out-Null
    S1 run 'bodyshop open' | Out-Null
    Start-Sleep -Milliseconds 1500
    foreach ($t in $tiers) {
        S1 tp 3 1.2 -18 | Out-Null
        S1 run 'bodyshop open' | Out-Null
        Start-Sleep -Milliseconds 1000
        S1 run "bodyshop buy trunk/bodyshop_trunk_$t" | Out-Null
        Start-Sleep -Milliseconds 2500
        S1 run 'bodyshop close' | Out-Null
        Start-Sleep -Milliseconds 800
        Shoot "$code-tier$t"
    }
    S1 run 'bodyshop trunk move 40 300 40' | Out-Null
    Start-Sleep -Milliseconds 1500
}

S1 run 'bodyshop cash 50000' | Out-Null
Car 'shitbox' @(1, 2, 3)
Car 'veeper' @(1)
Car 'dinkler' @(3)
exit ([int]($fails -gt 0))
