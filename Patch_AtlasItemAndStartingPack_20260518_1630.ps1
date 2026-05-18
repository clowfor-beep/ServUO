# Patch_AtlasItemAndStartingPack_20260518_1630.ps1
#
# Does two things:
# 1. Fixes WorldAtlas item graphic from 0x1B2 (white square container) to
#    0x14ED (flat world map) with a gold hue - looks like a proper atlas
#
# 2. Adds the WorldAtlas to every new character's starting backpack
#    by patching CharacterCreation.cs AddBackpack method
#
# Run from VS Code terminal: .\Patch_AtlasItemAndStartingPack_20260518_1630.ps1

$atlasFile = 'C:\UO\SERVUO\Scripts\Custom\WorldAtlas.cs'
$charFile  = 'C:\UO\SERVUO\Scripts\Misc\CharacterCreation.cs'

Write-Host '=== Atlas Item Graphic + Starting Pack Patch ===' -ForegroundColor Cyan

# -------------------------------------------------------
# 1. Fix WorldAtlas item graphic
# -------------------------------------------------------
Write-Host ''
Write-Host '-- WorldAtlas.cs --' -ForegroundColor Yellow

if (-not (Test-Path $atlasFile)) {
    Write-Host 'ERROR: WorldAtlas.cs not found' -ForegroundColor Red
} else {
    Copy-Item $atlasFile ($atlasFile + '.bak') -Force
    Write-Host 'Backup created.' -ForegroundColor Gray

    $lines = Get-Content $atlasFile -Encoding UTF8
    $changed = $false

    $newLines = $lines | ForEach-Object {
        $line = $_
        # Fix item ID from 0x1B2 (container/bag graphic) to 0x14ED (flat world map)
        if ($line -match 'public WorldAtlas\(\) : base\(0x1B2\)') {
            $line = $line -replace 'base\(0x1B2\)', 'base(0x14ED)'
            $changed = $true
        }
        # Fix hue to gold (1154 -> 2213 = antique gold, looks like an old map)
        if ($line -match 'Hue\s*=\s*1154') {
            $line = $line -replace 'Hue\s*=\s*1154', 'Hue       = 2213'
            $changed = $true
        }
        $line
    }

    if ($changed) {
        $newLines | Set-Content $atlasFile -Encoding UTF8
        Write-Host 'WorldAtlas graphic fixed: 0x14ED (world map), hue 2213 (antique gold)' -ForegroundColor Green
    } else {
        Write-Host 'WARNING: Graphic pattern not found - may already be patched.' -ForegroundColor Yellow
    }
}

# -------------------------------------------------------
# 2. Add WorldAtlas to new character starting backpack
# -------------------------------------------------------
Write-Host ''
Write-Host '-- CharacterCreation.cs --' -ForegroundColor Yellow

if (-not (Test-Path $charFile)) {
    Write-Host 'ERROR: CharacterCreation.cs not found' -ForegroundColor Red
} else {
    $content = Get-Content $charFile -Raw -Encoding UTF8

    if ($content -match 'WorldAtlas') {
        Write-Host 'Already patched - WorldAtlas already in starting pack.' -ForegroundColor Yellow
    } else {
        Copy-Item $charFile ($charFile + '.bak') -Force
        Write-Host 'Backup created.' -ForegroundColor Gray

        # Add WorldAtlas after the gold line in AddBackpack
        $lines = Get-Content $charFile -Encoding UTF8
        $changed = $false

        $newLines = $lines | ForEach-Object {
            $line = $_
            if ($line -match 'PackItem\(new Gold\(1000\)\)') {
                $line
                '            PackItem(new Server.Items.WorldAtlas()); // World Atlas - blessed travel guide'
                $changed = $true
            } else {
                $line
            }
        }

        if ($changed) {
            $newLines | Set-Content $charFile -Encoding UTF8
            Write-Host 'WorldAtlas added to starting backpack.' -ForegroundColor Green
        } else {
            Write-Host 'ERROR: Could not find Gold(1000) line in AddBackpack.' -ForegroundColor Red
        }
    }
}

Write-Host ''
Write-Host 'Done. Run Compile.WIN - Debug.bat then create a new character to test.' -ForegroundColor Cyan
