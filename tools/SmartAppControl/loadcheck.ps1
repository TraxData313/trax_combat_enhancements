# The game's runtime (.NET Framework) maps the DLL from its file - LoadFile reproduces Code Integrity's verdict for the installed pair.
$bin = 'C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TraxCombatEnhancements.Dev\bin\Win64_Shipping_Client'
$ok = $true
foreach ($f in @("TraxCombat.Core.dll", "TraxCombatEnhancements.dll")) {
    try { $a = [Reflection.Assembly]::LoadFile((Join-Path $bin $f)); Write-Host "LOADED $f - $($a.GetName().Version) $(($a.GetCustomAttributes([Reflection.AssemblyInformationalVersionAttribute], $false) | Select-Object -First 1).InformationalVersion)" }
    catch { $ok = $false; Write-Host "REFUSED $f - $($_.Exception.InnerException.Message) $($_.Exception.Message)" }
}
if ($ok) { exit 0 } else { exit 1 }
