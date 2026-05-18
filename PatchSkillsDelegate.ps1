# PatchSkillsDelegate.ps1
# Fixes compile error in Skills.cs caused by referencing Server.Custom
# (Scripts assembly) from the Server core assembly.
#
# Solution: replace the direct CooldownSystem call with a static delegate
# that CooldownSystem hooks into at startup from the Scripts side.
#
# Run from VS Code terminal: .\PatchSkillsDelegate.ps1

$file = 'C:\UO\SERVUO\Server\Skills.cs'

Write-Host '=== Skills.cs Delegate Patch ===' -ForegroundColor Cyan

if (-not (Test-Path $file)) {
    Write-Host 'ERROR: Skills.cs not found' -ForegroundColor Red
    exit 1
}

$content = Get-Content $file -Raw -Encoding UTF8

# -------------------------------------------------------
# Step 1: Remove the direct CooldownSystem call we added
#         and replace it with a delegate invoke
# -------------------------------------------------------
$oldCall = [regex]::Escape('if (skillDelay.TotalSeconds >= 1.0)') + '\s*' +
           [regex]::Escape('Server.Custom.CooldownSystem.Start(from, info.Name, skillDelay.TotalSeconds);')

$newCall = 'OnSkillUsed?.Invoke(from, info.Name, skillDelay.TotalSeconds);'

$patched = [regex]::Replace($content, $oldCall, $newCall)

if ($patched -eq $content) {
    Write-Host 'ERROR: Could not find the CooldownSystem.Start call in Skills.cs' -ForegroundColor Red
    Write-Host 'Has this patch already been applied, or did PatchSkillsCooldown.ps1 not run?' -ForegroundColor Yellow
    exit 1
}

# -------------------------------------------------------
# Step 2: Add the static delegate field to the Skills class
#         (insert after the class opening or near other statics)
# -------------------------------------------------------
$delegateDecl = 'public static Action<Mobile, string, double> OnSkillUsed;'

if ($patched -notmatch [regex]::Escape($delegateDecl)) {
    # Insert just before the UseSkill method
    $patched = $patched -replace '(public static bool UseSkill\b)', ($delegateDecl + "`r`n`t`t" + '$1')
}

# -------------------------------------------------------
# Step 3: Add using System; if not present (for Action<>)
# -------------------------------------------------------
if ($patched -notmatch 'using System;') {
    $patched = 'using System;' + "`r`n" + $patched
}

Copy-Item $file ($file + '.bak2') -Force
Write-Host 'Backup created.' -ForegroundColor Gray

Set-Content $file -Value $patched -Encoding UTF8 -NoNewline

Write-Host 'SUCCESS - Skills.cs updated to use delegate.' -ForegroundColor Green
Write-Host ''
Write-Host 'Now run PatchCooldownSystemHook.ps1 to update CooldownSystem.cs' -ForegroundColor Cyan
