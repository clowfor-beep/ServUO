# PatchFixDuplicateDelegate.ps1
# Removes the duplicate OnSkillUsed declaration in Skills.cs
# Run from VS Code terminal: .\PatchFixDuplicateDelegate.ps1

$file = 'C:\UO\SERVUO\Server\Skills.cs'

Write-Host '=== Fix Duplicate OnSkillUsed ===' -ForegroundColor Cyan

if (-not (Test-Path $file)) {
    Write-Host 'ERROR: Skills.cs not found' -ForegroundColor Red
    exit 1
}

$content = Get-Content $file -Raw -Encoding UTF8

# Count occurrences
$count = ([regex]::Matches($content, [regex]::Escape('public static Action<Mobile, string, double> OnSkillUsed;'))).Count

Write-Host "Found $count declaration(s) of OnSkillUsed." -ForegroundColor Gray

if ($count -eq 0) {
    Write-Host 'ERROR: OnSkillUsed not found at all - check Skills.cs manually.' -ForegroundColor Red
    exit 1
} elseif ($count -eq 1) {
    Write-Host 'Only one declaration found - nothing to fix.' -ForegroundColor Yellow
    exit 0
}

Copy-Item $file ($file + '.bak3') -Force
Write-Host 'Backup created.' -ForegroundColor Gray

# Replace two consecutive declarations (with any whitespace between) with just one
$pattern = '(' + [regex]::Escape('public static Action<Mobile, string, double> OnSkillUsed;') + '\s*)+'
$replacement = 'public static Action<Mobile, string, double> OnSkillUsed;'

$patched = [regex]::Replace($content, $pattern, $replacement)

Set-Content $file -Value $patched -Encoding UTF8 -NoNewline

$remaining = ([regex]::Matches($patched, [regex]::Escape('public static Action<Mobile, string, double> OnSkillUsed;'))).Count
Write-Host "Declarations remaining: $remaining" -ForegroundColor Gray

if ($remaining -eq 1) {
    Write-Host 'SUCCESS - Duplicate removed.' -ForegroundColor Green
    Write-Host ''
    Write-Host 'Now run Compile.WIN - Debug.bat' -ForegroundColor Cyan
} else {
    Write-Host 'ERROR: Unexpected result - check Skills.cs manually.' -ForegroundColor Red
}
