# PatchFixDuplicateDelegate.ps1
# Removes the duplicate OnSkillUsed declaration that got merged onto one line.
# Run from VS Code terminal: .\PatchFixDuplicateDelegate.ps1

$file = 'C:\UO\SERVUO\Server\Skills.cs'

Write-Host '=== Fix Duplicate OnSkillUsed ===' -ForegroundColor Cyan

if (-not (Test-Path $file)) {
    Write-Host 'ERROR: Skills.cs not found' -ForegroundColor Red
    exit 1
}

$content = Get-Content $file -Raw -Encoding UTF8

# The duplicate got merged onto the same line as UseSkill with no newline.
# Pattern: second declaration followed immediately by UseSkill(int) method.
$old = 'public static Action<Mobile, string, double> OnSkillUsed;public static bool UseSkill\(Mobile from, int skillID\)'
$new = 'public static bool UseSkill(Mobile from, int skillID)'

$patched = [regex]::Replace($content, $old, $new)

if ($patched -eq $content) {
    Write-Host 'Pattern not found - trying alternate (with newline)...' -ForegroundColor Yellow

    # Try with CRLF between them
    $old2 = 'public static Action<Mobile, string, double> OnSkillUsed;\r\npublic static bool UseSkill\(Mobile from, int skillID\)'
    $patched = [regex]::Replace($content, $old2, $new)
}

if ($patched -eq $content) {
    Write-Host 'ERROR: Could not find the duplicate pattern.' -ForegroundColor Red
    Write-Host 'Open Skills.cs and search for OnSkillUsed - delete the second one manually.' -ForegroundColor Yellow
    exit 1
}

Copy-Item $file ($file + '.bak3') -Force
Write-Host 'Backup created.' -ForegroundColor Gray

Set-Content $file -Value $patched -Encoding UTF8 -NoNewline

$remaining = ([regex]::Matches($patched, [regex]::Escape('public static Action<Mobile, string, double> OnSkillUsed;'))).Count
Write-Host "OnSkillUsed declarations remaining: $remaining" -ForegroundColor Gray

if ($remaining -eq 1) {
    Write-Host 'SUCCESS - Duplicate removed.' -ForegroundColor Green
    Write-Host 'Now run Compile.WIN - Debug.bat' -ForegroundColor Cyan
} else {
    Write-Host "WARNING: Expected 1, found $remaining - check Skills.cs manually." -ForegroundColor Yellow
}
