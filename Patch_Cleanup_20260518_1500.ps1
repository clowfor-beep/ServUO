# Patch_Cleanup_20260518_1500.ps1
# Removes all old patch scripts from C:\UO\SERVUO\ leaving only timestamped ones.
# Run once from VS Code terminal: .\Patch_Cleanup_20260518_1500.ps1

$folder = 'C:\UO\SERVUO'

$oldScripts = @(
    'PatchAtlasMoongates.ps1',
    'PatchSkillsCooldown.ps1',
    'PatchSkillsDelegate.ps1',
    'PatchSkillsFresh.ps1',
    'PatchFixDuplicateDelegate.ps1',
    'PatchBeggingAndItemID.ps1',
    'PatchBaseWeapon.ps1'
)

Write-Host '=== Patch Script Cleanup ===' -ForegroundColor Cyan

$removed = 0
foreach ($name in $oldScripts) {
    $path = Join-Path $folder $name
    if (Test-Path $path) {
        Remove-Item $path -Force
        Write-Host "  Removed: $name" -ForegroundColor Gray
        $removed++
    }
}

if ($removed -eq 0) {
    Write-Host 'No old scripts found - already clean.' -ForegroundColor Yellow
} else {
    Write-Host ''
    Write-Host "Removed $removed old script(s)." -ForegroundColor Green
}

Write-Host ''
Write-Host 'Remaining patch scripts:' -ForegroundColor Cyan
Get-ChildItem $folder -Filter 'Patch_*.ps1' | Select-Object Name | Format-Table -HideTableHeaders
