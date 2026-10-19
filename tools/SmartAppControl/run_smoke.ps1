# The offline smoke run IN MEMORY under Windows PowerShell 5.1 (.NET Framework 4.8): Smart App Control blocks the freshly built,
# unsigned OfflineSmoke.exe / TraxCombatEnhancements.dll / TraxCombat.Core.dll when they are mapped from a file; loaded from bytes
# they are not mapped. The smoke's own resolver loads the game's DLLs (signed / reputable) from the game folder.
param([string]$Bin, [string]$Game, [string]$Mcm, [string]$Repo)
$ErrorActionPreference = 'Stop'
# AppContext.BaseDirectory is the smoke's way to the repo (Program.Hud.cs): point the domain's APPBASE at its bin folder
try { [AppDomain]::CurrentDomain.SetData('APPBASE', $Bin + '\') } catch { Write-Host "APPBASE not set: $_" }
$ours = @{}
foreach ($n in 'TraxCombat.Core', 'TraxCombatEnhancements') {
    $ours[$n] = [Reflection.Assembly]::Load([IO.File]::ReadAllBytes((Join-Path $Bin "$n.dll")))
}
$smoke = [Reflection.Assembly]::Load([IO.File]::ReadAllBytes((Join-Path $Bin 'OfflineSmoke.exe')))
$ours['OfflineSmoke'] = $smoke
$handler = [ResolveEventHandler] {
    param($s, $e)
    $name = (New-Object Reflection.AssemblyName($e.Name)).Name
    if ($ours.ContainsKey($name)) { return $ours[$name] }
    $p = Join-Path $Bin "$name.dll"
    if (Test-Path $p) { return [Reflection.Assembly]::LoadFrom($p) }
    return $null
}
[AppDomain]::CurrentDomain.add_AssemblyResolve($handler)
$main = $smoke.EntryPoint
$code = $main.Invoke($null, @(, [string[]]@($Game, $Mcm)))
Write-Host "smoke exit code $code"
exit $code
