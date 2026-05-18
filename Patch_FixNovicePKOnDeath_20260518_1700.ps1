# Patch_FixNovicePKOnDeath_20260518_1700.ps1
# Fixes: CS0507 - OnDeath access modifier mismatch
# Changes 'protected override void OnDeath' to 'public override void OnDeath'
# Run from VS Code terminal: .\Patch_FixNovicePKOnDeath_20260518_1700.ps1

$file = 'C:\UO\SERVUO\Scripts\Custom\NovicePlayerKiller.cs'

Write-Host '=== Fix NovicePlayerKiller OnDeath ===' -ForegroundColor Cyan

if (-not (Test-Path $file)) {
    Write-Host 'ERROR: NovicePlayerKiller.cs not found' -ForegroundColor Red
    exit 1
}

Copy-Item $file ($file + '.bak') -Force
Write-Host 'Backup created.' -ForegroundColor Gray

$lines = Get-Content $file -Encoding UTF8

$newLines = $lines | ForEach-Object {
    if ($_ -match 'protected override void OnDeath') {
        $_ -replace 'protected override void OnDeath', 'public override void OnDeath'
    } else {
        $_
    }
}

$newLines | Set-Content $file -Encoding UTF8

Write-Host 'SUCCESS - OnDeath fixed to public.' -ForegroundColor Green
Write-Host 'Now run Compile.WIN - Debug.bat' -ForegroundColor Cyan
