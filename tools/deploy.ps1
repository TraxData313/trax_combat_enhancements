# Builds the mod, checks it still loads without its optional dependencies, and installs it into
# the Bannerlord Modules folder as the DEV module.
# Usage: powershell -ExecutionPolicy Bypass -File tools\deploy.ps1 [-Configuration Release]
#
# The local deploy wears its own identity - Id "TraxCombatEnhancements.Dev", shown as
# "Trax Combat Enhancements (dev)" - so it can one day sit beside the Steam Workshop copy in the
# launcher and be picked deliberately. Enable only ONE of the two at a time.
#
# (ASCII only in this file: Windows PowerShell 5.1 reads a BOM-less script as ANSI.)
param(
    [string]$Configuration = "Release",
    [string]$GameFolder = "D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord",
    [string]$McmBinFolder = "D:\SteamLibrary\steamapps\workshop\content\261550\2859238197\bin\Win64_Shipping_Client",
    [switch]$SkipSmoke
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$moduleDir = Join-Path $GameFolder "Modules\TraxCombatEnhancements.Dev"
$binDir = Join-Path $moduleDir "bin\Win64_Shipping_Client"
$outDir = Join-Path $repoRoot "src\TraxCombat.Module\bin\$Configuration"
$moduleDll = Join-Path $outDir "TraxCombatEnhancements.dll"

# 1. Build (the module project pulls Core in).
dotnet build (Join-Path $repoRoot "src\TraxCombat.Module\TraxCombat.Module.csproj") -c $Configuration
if ($LASTEXITCODE -ne 0) { throw "Build failed." }

# 2. The soft-dependency guard: HARD fail if any type's base type, interface or field (compiler-
#    generated lambda caches included) comes from an assembly a player may not have. MCM and its
#    stack, War Sails, the CustomBattle module - all optional. See tools\AssemblyGuard\Program.cs.
$forbidden = @(
    "MCMv5", "MCM.UI", "Bannerlord.MBOptionScreen",
    "0Harmony", "Bannerlord.ButterLib", "Bannerlord.UIExtenderEx",
    "NavalDLC", "NavalDLC.View", "TaleWorlds.MountAndBlade.CustomBattle"
)
dotnet run --project (Join-Path $repoRoot "tools\AssemblyGuard") -c Release -- $moduleDll @forbidden
if ($LASTEXITCODE -ne 0) { throw "AssemblyGuard failed - the mod would not load without an optional module. Not deployed." }

# 3. The offline smoke test: the real DLL on .NET Framework with the game's own DLLs, no game
#    launched - types load without MCM, config file flows, then the MCM page built by MCM's real
#    fluent builder. See tools\OfflineSmoke. -SkipSmoke only if it cannot run on this machine.
if (-not $SkipSmoke) {
    dotnet run --project (Join-Path $repoRoot "tools\OfflineSmoke") -c $Configuration -- $GameFolder $McmBinFolder
    if ($LASTEXITCODE -ne 0) { throw "Offline smoke test failed - see above. Not deployed." }
}

# 4. Install. The manifest gets the dev identity; the DLLs and their .pdb files (line numbers in
#    the [error] stacks of trax_combat.log) go to bin. Newtonsoft.Json is NOT shipped - the game's
#    own copy (same assembly version 13.0.0.0) is used.
New-Item -ItemType Directory -Force $binDir | Out-Null
$manifestPath = Join-Path $repoRoot "module\SubModule.xml"
$manifest = [System.IO.File]::ReadAllText($manifestPath, [System.Text.Encoding]::UTF8)
$manifest = $manifest -replace '<Id value="TraxCombatEnhancements" />', '<Id value="TraxCombatEnhancements.Dev" />'
$manifest = $manifest -replace '<Name value="Trax Combat Enhancements" />', '<Name value="Trax Combat Enhancements (dev)" />'
[System.IO.File]::WriteAllText((Join-Path $moduleDir "SubModule.xml"), $manifest, (New-Object System.Text.UTF8Encoding($false)))

$files = @("TraxCombatEnhancements.dll", "TraxCombatEnhancements.pdb", "TraxCombat.Core.dll", "TraxCombat.Core.pdb")
try {
    foreach ($f in $files) {
        Copy-Item (Join-Path $outDir $f) $binDir -Force
    }
}
catch {
    Write-Host ""
    Write-Host "Could not copy $f into $binDir - is Bannerlord running? (the game locks the DLL)"
    Write-Host "Close the game and run again:  powershell -ExecutionPolicy Bypass -File tools\deploy.ps1"
    throw
}

# GUI assets (the HUD prefabs, from step 6 on). Remove-then-copy: Copy-Item with a folder source
# and an existing folder destination would nest a GUI\GUI inside it instead.
$guiSource = Join-Path $repoRoot "module\GUI"
$guiDest = Join-Path $moduleDir "GUI"
if (Test-Path $guiDest) { Remove-Item $guiDest -Recurse -Force }
if (Test-Path $guiSource) { Copy-Item $guiSource $guiDest -Recurse }

Write-Host "Deployed to $moduleDir as 'Trax Combat Enhancements (dev)' - enable it in the launcher."
