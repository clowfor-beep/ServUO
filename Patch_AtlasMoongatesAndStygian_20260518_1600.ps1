# Patch_AtlasMoongatesAndStygian_20260518_1600.ps1
# Fixes remaining coordinate errors verified against Regions.xml:
#   Moongates: Ilshenar Gate and Malas Gate corrected
#   Dungeons:  Stygian Abyss corrected
#
# All other moongates and dungeons verified correct in Regions.xml.
# Run from VS Code terminal: .\Patch_AtlasMoongatesAndStygian_20260518_1600.ps1

$file = 'C:\UO\SERVUO\Scripts\Custom\WorldAtlas.cs'

Write-Host '=== Atlas Moongate + Stygian Abyss Fix ===' -ForegroundColor Cyan

if (-not (Test-Path $file)) {
    Write-Host 'ERROR: WorldAtlas.cs not found' -ForegroundColor Red
    exit 1
}

Copy-Item $file ($file + '.bak') -Force
Write-Host 'Backup created.' -ForegroundColor Gray

$lines = Get-Content $file -Encoding UTF8
$changes = 0

$newLines = $lines | ForEach-Object {
    $line = $_

    if ($line -match '"Ilshenar Gate"') {
        # Shrine of Compassion go coord from Regions.xml: 1223, 475, z=-16
        $line = '            new AtlasLocation("Ilshenar Gate",    1223,  475,-16, Map.Ilshenar),'
        $changes++
        Write-Host '  Ilshenar Gate: 1223, 475, z=-16 (Shrine of Compassion)' -ForegroundColor Green
    }
    elseif ($line -match '"Malas Gate"') {
        # Luna go coord from Regions.xml: 989, 520, z=-50
        $line = '            new AtlasLocation("Malas Gate",        989,  520,-50, Map.Malas),'
        $changes++
        Write-Host '  Malas Gate: 989, 520, z=-50 (Luna arrival)' -ForegroundColor Green
    }
    elseif ($line -match '"Tokuno Gate"') {
        # Already correct from previous patch: 1169, 998, z=0
        # Rewrite cleanly anyway
        $line = '            new AtlasLocation("Tokuno Gate",      1169,  998,  0, Map.Tokuno),'
        $changes++
        Write-Host '  Tokuno Gate: 1169, 998, z=0 (confirmed)' -ForegroundColor Green
    }
    elseif ($line -match '"Stygian Abyss"') {
        # StygianAbyss go coord from Regions.xml: 985, 366, z=-11
        $line = '            new AtlasLocation("Stygian Abyss",     985,  366,-11, Map.TerMur),'
        $changes++
        Write-Host '  Stygian Abyss: 985, 366, z=-11' -ForegroundColor Green
    }

    $line
}

if ($changes -eq 0) {
    Write-Host 'No changes needed - already up to date.' -ForegroundColor Yellow
    exit 0
}

$newLines | Set-Content $file -Encoding UTF8

Write-Host ''
Write-Host "SUCCESS - $changes coordinate(s) corrected." -ForegroundColor Green
Write-Host 'Now run Compile.WIN - Debug.bat' -ForegroundColor Cyan
