# PatchFixDuplicateDelegate.ps1
# Removes both merged OnSkillUsed declarations and puts one clean copy back.
# Run from VS Code terminal: .\PatchFixDuplicateDelegate.ps1

$file = 'C:\UO\SERVUO\Server\Skills.cs'

Write-Host '=== Fix Duplicate OnSkillUsed ===' -ForegroundColor Cyan

if (-not (Test-Path $file)) {
    Write-Host 'ERROR: Skills.cs not found' -ForegroundColor Red
    exit 1
}

$content = Get-Content $file -Raw -Encoding UTF8

Copy-Item $file ($file + '.bak3') -Force
Write-Host 'Backup created.' -ForegroundColor Gray

$decl = 'public static Action<Mobile, string, double> OnSkillUsed;'

# Step 1: Strip ALL occurrences of the declaration (however many, wherever they are)
$stripped = $content.Replace($decl, '')

# Step 2: Insert exactly one clean declaration on its own line before UseSkill(SkillName)
$target = 'public static bool UseSkill(Mobile from, SkillName name)'
$replacement = $decl + "`r`n`t`t" + $target

$patched = $stripped.Replace($target, $replacement)

if ($patched -eq $stripped) {
    Write-Host 'ERROR: Could not find UseSkill(Mobile from, SkillName name) to re-insert declaration.' -ForegroundColor Red
    exit 1
}

Set-Content $file -Value $patched -Encoding UTF8 -NoNewline

$count = ([regex]::Matches($patched, [regex]::Escape($decl))).Count
Write-Host "OnSkillUsed declarations remaining: $count" -ForegroundColor Gray

if ($count -eq 1) {
    Write-Host 'SUCCESS - One clean declaration in place.' -ForegroundColor Green
    Write-Host 'Now run Compile.WIN - Debug.bat' -ForegroundColor Cyan
} else {
    Write-Host "WARNING: Expected 1, found $count" -ForegroundColor Yellow
}
