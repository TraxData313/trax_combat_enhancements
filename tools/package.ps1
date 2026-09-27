# Builds the RELEASE of the mod and lays it out, clean and from scratch, for the Steam Workshop (and
# a manual install) under dist\TraxCombatEnhancements - the folder the Workshop uploader uploads.
# Plus a versioned zip beside it (dist\TraxCombatEnhancements_vX.Y.Z.zip) for a manual install.
# Usage: powershell -ExecutionPolicy Bypass -File tools\package.ps1 [-Force]
# The whole release loop: tools\WORKSHOP-UPLOAD.md.
#
# Gates, in order - any failure stops it before dist\ is touched:
#   the manifest   module\SubModule.xml carries the RELEASE identity (Id TraxCombatEnhancements, name
#                  "Trax Combat Enhancements" - deploy.ps1's .Dev rename never ships) and a vX.Y.Z version;
#   build          the solution in Release (the DLLs take their version from the manifest -
#                  Directory.Build.props);
#   unit tests     dotnet test;
#   AssemblyGuard  MCM, Harmony, ButterLib, UIExtenderEx, War Sails, CustomBattle in ANY type surface
#                  of either DLL = HARD FAIL (the mod would not load for a player without them);
#   OfflineSmoke   the real DLL on .NET Framework with the game's own DLLs, no game launched.
# Then the layout is checked against an explicit list: our two DLLs (+ their .pdb files - line
# numbers in the [error] stacks players send), SubModule.xml, GUI\Prefabs\*.xml - and nothing else
# (never MCM, Newtonsoft.Json or a game DLL: the game ships its own Newtonsoft 13.0.0.0).
#
# Release rhythm (CLAUDE.md "Release"): the version is stamped ONCE, on release day, in
# module\SubModule.xml. A zip for the current version already in dist\ is the released one, so it is
# never overwritten silently - bump the version, or pass -Force (e.g. for test packages made before
# the first release).
#
# (ASCII only in this file: Windows PowerShell 5.1 reads a BOM-less script as ANSI.)
param(
    [string]$Configuration = "Release",
    [string]$GameFolder = "C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord",
    [string]$McmBinFolder = "C:\Program Files (x86)\Steam\steamapps\workshop\content\261550\2859238197\bin\Win64_Shipping_Client",
    [switch]$Force
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$moduleId = "TraxCombatEnhancements"
$moduleName = "Trax Combat Enhancements"
$distRoot = Join-Path $repoRoot "dist"
$moduleDir = Join-Path $distRoot $moduleId
$binDir = Join-Path $moduleDir "bin\Win64_Shipping_Client"
$outDir = Join-Path $repoRoot "src\TraxCombat.Module\bin\$Configuration"
$shippedBin = @("TraxCombatEnhancements.dll", "TraxCombatEnhancements.pdb", "TraxCombat.Core.dll", "TraxCombat.Core.pdb")

# 1. The manifest: release identity + version (read as UTF-8, like deploy.ps1).
$manifestPath = Join-Path $repoRoot "module\SubModule.xml"
[xml]$manifest = [System.IO.File]::ReadAllText($manifestPath, [System.Text.Encoding]::UTF8)
$id = $manifest.Module.Id.value
$name = $manifest.Module.Name.value
$version = $manifest.Module.Version.value
if ($id -ne $moduleId) { throw "module\SubModule.xml has Id '$id' - the release must be '$moduleId' (deploy.ps1 makes the .Dev identity on the fly; it never belongs in the repo)." }
if ($name -ne $moduleName) { throw "module\SubModule.xml has Name '$name' - the release must be '$moduleName' (the Workshop title is read from it)." }
if (-not ($version -match '^v(\d+\.\d+\.\d+)$')) { throw "module\SubModule.xml has Version '$version' - expected vX.Y.Z." }
$numericVersion = $Matches[1]

$zipPath = Join-Path $distRoot ("{0}_{1}.zip" -f $moduleId, $version)
if ((Test-Path $zipPath) -and -not $Force) {
    throw "dist\$(Split-Path -Leaf $zipPath) already exists - the release rhythm stamps a version once. Bump <Version> in module\SubModule.xml for a new release, or pass -Force to overwrite this one."
}

# 2. Build + unit tests.
dotnet build (Join-Path $repoRoot "TraxCombatEnhancements.sln") -c $Configuration
if ($LASTEXITCODE -ne 0) { throw "Build failed. Nothing packaged." }
dotnet test (Join-Path $repoRoot "TraxCombatEnhancements.sln") -c $Configuration --no-build
if ($LASTEXITCODE -ne 0) { throw "Unit tests failed. Nothing packaged." }

# 3. The soft-dependency guard on BOTH shipped DLLs - a HARD gate (see tools\AssemblyGuard\Program.cs).
$forbidden = @(
    "MCMv5", "MCM.UI", "Bannerlord.MBOptionScreen",
    "0Harmony", "Bannerlord.ButterLib", "Bannerlord.UIExtenderEx",
    "NavalDLC", "NavalDLC.View", "TaleWorlds.MountAndBlade.CustomBattle"
)
foreach ($dll in @("TraxCombatEnhancements.dll", "TraxCombat.Core.dll")) {
    dotnet run --project (Join-Path $repoRoot "tools\AssemblyGuard") -c Release -- (Join-Path $outDir $dll) @forbidden
    if ($LASTEXITCODE -ne 0) { throw "AssemblyGuard failed on $dll - the mod would not load without an optional module. Nothing packaged." }
}

# 4. The offline smoke test (no -SkipSmoke here: a release is never packaged untested).
dotnet run --project (Join-Path $repoRoot "tools\OfflineSmoke") -c $Configuration -- $GameFolder $McmBinFolder
if ($LASTEXITCODE -ne 0) { throw "Offline smoke test failed - see above. Nothing packaged." }

# 5. The built DLLs must carry the manifest's version (Directory.Build.props reads it from there).
foreach ($dll in @("TraxCombatEnhancements.dll", "TraxCombat.Core.dll")) {
    $asmVersion = [System.Reflection.AssemblyName]::GetAssemblyName((Join-Path $outDir $dll)).Version
    $asmNumeric = "{0}.{1}.{2}" -f $asmVersion.Major, $asmVersion.Minor, $asmVersion.Build
    if ($asmNumeric -ne $numericVersion) { throw "$dll is version $asmNumeric but module\SubModule.xml says $version - rebuild (the DLL version comes from the manifest)." }
}

# 6. The layout, from scratch.
if (Test-Path $moduleDir) { Remove-Item $moduleDir -Recurse -Force }
New-Item -ItemType Directory -Force $binDir | Out-Null
Copy-Item $manifestPath (Join-Path $moduleDir "SubModule.xml")
foreach ($f in $shippedBin) { Copy-Item (Join-Path $outDir $f) $binDir }
$guiSource = Join-Path $repoRoot "module\GUI"
if (Test-Path $guiSource) { Copy-Item $guiSource (Join-Path $moduleDir "GUI") -Recurse }

# 7. Nothing but the listed files may ride along.
$moduleRoot = (Resolve-Path $moduleDir).Path
$files = Get-ChildItem $moduleDir -Recurse -File | Sort-Object FullName
$unexpected = @()
foreach ($file in $files) {
    $rel = $file.FullName.Substring($moduleRoot.Length + 1)
    $allowed = ($rel -eq "SubModule.xml") `
        -or ($shippedBin | Where-Object { $rel -eq ("bin\Win64_Shipping_Client\" + $_) }) `
        -or ($rel -match '^GUI\\Prefabs\\[^\\]+\.xml$')
    if (-not $allowed) { $unexpected += $rel }
}
if ($unexpected.Count -gt 0) { throw ("Unexpected files in the package: " + ($unexpected -join ", ")) }
foreach ($f in $shippedBin) {
    if (-not (Test-Path (Join-Path $binDir $f))) { throw "Missing from the package: bin\Win64_Shipping_Client\$f" }
}
if (-not (Get-ChildItem (Join-Path $moduleDir "GUI\Prefabs") -Filter *.xml -ErrorAction SilentlyContinue)) { throw "Missing from the package: GUI\Prefabs (the HUD movies)." }

# 8. The zip: the module folder at its root, forward-slash entry names (Windows PowerShell 5.1's
#    Compress-Archive writes backslashes, which some unzip tools turn into file names).
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
$zip = [System.IO.Compression.ZipFile]::Open($zipPath, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($file in $files) {
        $entry = $moduleId + "/" + $file.FullName.Substring($moduleRoot.Length + 1).Replace("\", "/")
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $file.FullName, $entry, [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
}
finally {
    $zip.Dispose()
}

# 9. What was packaged.
Write-Host ""
Write-Host "Packaged $moduleName $version (Id $moduleId) to $moduleDir"
$total = 0
foreach ($file in $files) {
    $total += $file.Length
    Write-Host ("  {0,10:N0} bytes  {1}" -f $file.Length, $file.FullName.Substring($moduleRoot.Length + 1))
}
Write-Host ("  {0,10:N0} bytes  total, {1} files" -f $total, $files.Count)
Write-Host ("Zip: {0} ({1:N0} bytes)" -f $zipPath, (Get-Item $zipPath).Length)
Write-Host "Workshop upload: tools\WORKSHOP-UPLOAD.md (the uploader points at dist\$moduleId)."
