# deploy.ps1's step 4 exactly (the guard and the smoke ran in memory - Smart App Control refuses the freshly built tool exes)
$ErrorActionPreference = 'Stop'
$repoRoot = 'C:\Users\Asus ROG\Documents\BannerlordMods\trax_combat_enhancements'
$GameFolder = 'C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord'
$moduleDir = Join-Path $GameFolder "Modules\TraxCombatEnhancements.Dev"
$binDir = Join-Path $moduleDir "bin\Win64_Shipping_Client"
$outDir = Join-Path $repoRoot "src\TraxCombat.Module\bin\Release"
New-Item -ItemType Directory -Force $binDir | Out-Null
$manifestPath = Join-Path $repoRoot "module\SubModule.xml"
$manifest = [System.IO.File]::ReadAllText($manifestPath, [System.Text.Encoding]::UTF8)
$manifest = $manifest -replace '<Id value="TraxCombatEnhancements" />', '<Id value="TraxCombatEnhancements.Dev" />'
$manifest = $manifest -replace '<Name value="Trax Combat Enhancements" />', '<Name value="Trax Combat Enhancements (dev)" />'
[System.IO.File]::WriteAllText((Join-Path $moduleDir "SubModule.xml"), $manifest, (New-Object System.Text.UTF8Encoding($false)))
foreach ($f in @("TraxCombatEnhancements.dll", "TraxCombatEnhancements.pdb", "TraxCombat.Core.dll", "TraxCombat.Core.pdb")) { Copy-Item (Join-Path $outDir $f) $binDir -Force }
$guiSource = Join-Path $repoRoot "module\GUI"
$guiDest = Join-Path $moduleDir "GUI"
if (Test-Path $guiDest) { Remove-Item $guiDest -Recurse -Force }
if (Test-Path $guiSource) { Copy-Item $guiSource $guiDest -Recurse }
Write-Host "Deployed to $moduleDir"
foreach ($f in @("TraxCombatEnhancements.dll", "TraxCombat.Core.dll")) { $h = (Get-FileHash (Join-Path $binDir $f) -Algorithm SHA256).Hash; Write-Host "$f $h" }
