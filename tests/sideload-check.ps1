<#
.SYNOPSIS
  Checks what the garage menu answers for each way Sideload can be installed. One PASS or FAIL per case.

.DESCRIPTION
  Needs a test slot that already holds a Debug build of this mod (for the `bodyshop open` console command), for
  example:
    pwsh -File Workspace/tools/session/session.ps1 -Profile main -Build _wt-MarcosBodyshop-<topic>,Chassis -NoApp

  For every case the script stops the slot's game, puts Sideload where the case says, boots with -KeepSlot, runs
  `bodyshop open` and reads the lines it wrote. The released Sideload DLLs come from GitHub (gh).

  Cases (the player line is matched in English and German):
    clean      Sideload 1.36.3 in Mods                     -> the Sideload check passes (the answer is about the vehicle)
    missing    no Sideload anywhere                        -> "Sideload is not installed."
    subfolder  Mods/Sideload/Sideload.dll                  -> "Sideload is not loaded. Move Mods/Sideload/Sideload.dll ..."
    oldlib     1.36.3 in Mods, 1.10.0 in UserLibs          -> "Sideload is loaded from UserLibs/Sideload.dll, ..."
    plugins    Sideload 1.36.3 in Plugins                  -> "Sideload is loaded from Plugins/Sideload.dll, ..."
    old        Sideload 1.10.0 in Mods                     -> "Sideload 1.10.0 is too old. ..."

  The slot keeps the last case's layout. The next session.ps1 run without -KeepSlot rebuilds it from the stack.
#>
[CmdletBinding()]
param(
    [ValidateSet('mptest', 'mptest2', 'mptest3', 'mptest4', 'mptest5', 'mptest6')]
    [string]$Slot = 'mptest',
    [string[]]$Cases = @('clean', 'missing', 'subfolder', 'oldlib', 'plugins', 'old'),
    [string]$NewVersion = '1.36.3',
    [string]$OldVersion = '1.10.0'
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path "$PSScriptRoot\..\..").Path
$session = Join-Path $root 'Workspace\tools\session\session.ps1'
$s1 = Join-Path $root 'Workspace\tools\bridge\s1.py'
$table = (& python (Join-Path $root 'Workspace\tools\slots\slots.py') table) | ConvertFrom-Json
$slotRoot = [string]$table.$Slot.root
if (-not (Test-Path (Join-Path $slotRoot 'Schedule I.exe'))) { throw "no game at '$slotRoot' for slot $Slot" }
$log = Join-Path $slotRoot 'MelonLoader\Latest.log'

$cache = Join-Path ([IO.Path]::GetTempPath()) 'bodyshop-sideload-check'
function Get-Sideload([string]$version) {
    $dir = Join-Path $cache $version
    $dll = Join-Path $dir 'Sideload.dll'
    if (-not (Test-Path $dll)) {
        New-Item -ItemType Directory -Force $dir | Out-Null
        & gh release download "v$version" -R DooDesch-Mods/ScheduleOne-Sideload -D $dir -p 'Sideload.dll' --clobber
        if ($LASTEXITCODE -ne 0 -or -not (Test-Path $dll)) { throw "Sideload $version could not be downloaded" }
    }
    return $dll
}
$new = Get-Sideload $NewVersion
$old = Get-Sideload $OldVersion

function Stop-SlotGame {
    $mine = { Get-CimInstance Win32_Process -Filter "Name='Schedule I.exe'" | Where-Object { $_.ExecutablePath -like "$slotRoot*" } }
    & $mine | ForEach-Object { Stop-Process -Id $_.ProcessId -Force }
    $deadline = (Get-Date).AddSeconds(30)
    while ((& $mine) -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 300 }
    if (& $mine) { throw "the game in $Slot did not stop" }
}

function Set-Layout([string]$case) {
    foreach ($d in 'Mods', 'Plugins', 'UserLibs') {
        Get-ChildItem (Join-Path $slotRoot $d) -Recurse -Filter 'Sideload*.dll' -ErrorAction SilentlyContinue | Remove-Item -Force
    }
    $sub = Join-Path $slotRoot 'Mods\Sideload'
    if (Test-Path $sub) { Remove-Item $sub -Recurse -Force }
    switch ($case) {
        'clean'     { Copy-Item $new (Join-Path $slotRoot 'Mods\Sideload.dll') }
        'missing'   { }
        'subfolder' { New-Item -ItemType Directory -Force $sub | Out-Null; Copy-Item $new (Join-Path $sub 'Sideload.dll') }
        'oldlib'    { Copy-Item $new (Join-Path $slotRoot 'Mods\Sideload.dll'); Copy-Item $old (Join-Path $slotRoot 'UserLibs\Sideload.dll') }
        'plugins'   { Copy-Item $new (Join-Path $slotRoot 'Plugins\Sideload.dll') }
        'old'       { Copy-Item $old (Join-Path $slotRoot 'Mods\Sideload.dll') }
        default     { throw "unknown case '$case'" }
    }
}

# What `bodyshop open` and the request wrote, as one text.
function Invoke-Open {
    $lines = & python $s1 --slot $Slot run 'bodyshop open' 2>&1
    return ($lines | Where-Object { $_ -match "Marco's_Bodyshop" }) -join "`n"
}

$results = @()
function Report([string]$case, [bool]$ok, [string]$why) {
    $script:results += [pscustomobject]@{ Case = $case; Ok = $ok }
    Write-Host ("{0} {1}: {2}" -f ($(if ($ok) { 'PASS' } else { 'FAIL' })), $case, $why)
}

foreach ($case in $Cases) {
    Write-Host "--- $case"
    Stop-SlotGame
    Set-Layout $case
    $boot = & pwsh -File $session -Slot $Slot -KeepSlot -NoApp -Template none 2>&1
    if ($LASTEXITCODE -ne 0) { Report $case $false ("the game did not come up: " + ($boot | Select-Object -Last 1)); continue }
    $out = Invoke-Open
    Write-Host ($out -split "`n" | ForEach-Object { "    $_" } | Out-String).TrimEnd()

    switch ($case) {
        'clean' {
            $bad = $out -match 'Sideload'
            Report $case (-not $bad -and $out.Length -gt 0) ($(if ($bad) { 'the answer mentions Sideload' } elseif ($out.Length -eq 0) { 'no answer from bodyshop open' } else { 'the Sideload check passed' }))
        }
        'missing' {
            $ok = $out -match 'no Sideload file in Mods, Plugins or UserLibs' -and
                  $out -match 'not opened: (Sideload is not installed|Sideload ist nicht installiert)'
            Report $case $ok ($(if ($ok) { 'refused as not installed' } else { 'expected "Sideload is not installed"' }))
        }
        'subfolder' {
            $ok = $out -match 'not opened: (Sideload is not loaded\. Move|Sideload ist nicht geladen\. Leg) Mods/Sideload/Sideload\.dll'
            Report $case $ok ($(if ($ok) { 'refused, names Mods/Sideload/Sideload.dll' } else { 'expected "Move Mods/Sideload/Sideload.dll"' }))
        }
        'oldlib' {
            $ok = $out -match 'not opened: (Sideload is loaded from|Sideload wird aus) UserLibs/Sideload\.dll'
            Report $case $ok ($(if ($ok) { 'refused, names UserLibs/Sideload.dll' } else { 'expected "loaded from UserLibs/Sideload.dll"' }))
        }
        'plugins' {
            $ok = $out -match 'not opened: (Sideload is loaded from|Sideload wird aus) Plugins/Sideload\.dll'
            Report $case $ok ($(if ($ok) { 'refused, names Plugins/Sideload.dll' } else { 'expected "loaded from Plugins/Sideload.dll"' }))
        }
        'old' {
            $ok = $out -match "Sideload $([regex]::Escape($OldVersion)) is too old" -and
                  $out -match "not opened: Sideload $([regex]::Escape($OldVersion)) (is too old|ist zu alt)"
            Report $case $ok ($(if ($ok) { "refused as too old ($OldVersion)" } else { "expected 'Sideload $OldVersion is too old'" }))
        }
    }
}

$failed = @($results | Where-Object { -not $_.Ok }).Count
Write-Host ("{0} of {1} case(s) passed." -f ($results.Count - $failed), $results.Count)
exit ([int]($failed -gt 0))
