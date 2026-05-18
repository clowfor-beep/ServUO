# PatchSkillsFresh.ps1
# Restores Skills.cs from original backup then applies ONE clean patch.
# Run from VS Code terminal: .\PatchSkillsFresh.ps1

$file   = 'C:\UO\SERVUO\Server\Skills.cs'
$backup = 'C:\UO\SERVUO\Server\Skills.cs.bak'

Write-Host '=== Skills.cs Fresh Patch ===' -ForegroundColor Cyan

# -------------------------------------------------------
# Step 1: Restore from original backup
# -------------------------------------------------------
if (-not (Test-Path $backup)) {
    Write-Host 'ERROR: Skills.cs.bak not found - cannot restore.' -ForegroundColor Red
    Write-Host 'Manually revert Skills.cs from git or re-download the original.' -ForegroundColor Yellow
    exit 1
}

Copy-Item $backup $file -Force
Write-Host 'Restored Skills.cs from .bak' -ForegroundColor Gray

$content = Get-Content $file -Raw -Encoding UTF8

# Verify this is the unpatched original
if ($content -match 'OnSkillUsed') {
    Write-Host 'ERROR: Backup already contains OnSkillUsed - not a clean original.' -ForegroundColor Red
    exit 1
}

# -------------------------------------------------------
# Step 2: One clean patch - split the callback line,
#         add delegate field, invoke it
# -------------------------------------------------------

# 2a. Add delegate declaration before UseSkill(SkillName) 
$content = $content -replace `
    'public static bool UseSkill\(Mobile from, SkillName name\)', `
    "public static Action<Mobile, string, double> OnSkillUsed;`r`n`t`tpublic static bool UseSkill(Mobile from, SkillName name)"

# 2b. Split the Callback line and invoke the delegate
$old = 'from\.NextSkillTime\s*=\s*Core\.TickCount\s*\+\s*\(int\)info\.Callback\(from\)\.TotalMilliseconds;'
$new = 'var skillDelay = info.Callback(from);' + "`r`n" +
       '								from.NextSkillTime = Core.TickCount + (int)skillDelay.TotalMilliseconds;' + "`r`n`r`n" +
       '								OnSkillUsed?.Invoke(from, info.Name, skillDelay.TotalSeconds);'

$content = [regex]::Replace($content, $old, $new)

# -------------------------------------------------------
# Step 3: Verify and save
# -------------------------------------------------------
$declCount   = ([regex]::Matches($content, [regex]::Escape('public static Action<Mobile, string, double> OnSkillUsed;'))).Count
$invokeCount = ([regex]::Matches($content, [regex]::Escape('OnSkillUsed?.Invoke'))).Count

if ($declCount -ne 1 -or $invokeCount -ne 1) {
    Write-Host "ERROR: Expected 1 declaration and 1 invoke, got $declCount / $invokeCount" -ForegroundColor Red
    exit 1
}

Set-Content $file -Value $content -Encoding UTF8 -NoNewline

Write-Host ''
Write-Host 'SUCCESS - Skills.cs patched cleanly.' -ForegroundColor Green
Write-Host "  OnSkillUsed declarations : $declCount" -ForegroundColor Gray
Write-Host "  OnSkillUsed?.Invoke calls: $invokeCount" -ForegroundColor Gray
Write-Host ''
Write-Host 'Now run Compile.WIN - Debug.bat' -ForegroundColor Cyan
