# PatchAtlasMoongates.ps1
# Fixes moongate coordinates in WorldAtlas.cs using line-by-line replacement.
# Run from VS Code terminal: .\PatchAtlasMoongates.ps1

$file = 'C:\UO\SERVUO\Scripts\Custom\WorldAtlas.cs'

Write-Host '=== Atlas Moongate Coordinate Fix ===' -ForegroundColor Cyan

if (-not (Test-Path $file)) {
    Write-Host 'ERROR: WorldAtlas.cs not found' -ForegroundColor Red
    exit 1
}

Copy-Item $file ($file + '.bak') -Force
Write-Host 'Backup created.' -ForegroundColor Gray

# Read line by line
$lines = Get-Content $file -Encoding UTF8
$changes = 0

$newLines = $lines | ForEach-Object {
    $line = $_

    if ($line -match '"Ilshenar Gate"') {
        $line = '            new AtlasLocation("Ilshenar Gate",    1223,  475,-16, Map.Ilshenar),'
        $changes++
        Write-Host '  Ilshenar Gate fixed: 1223, 475, z=-16' -ForegroundColor Green
    }
    elseif ($line -match '"Malas Gate"') {
        $line = '            new AtlasLocation("Malas Gate",       1306, 1072,  0, Map.Malas),'
        $changes++
        Write-Host '  Malas Gate fixed: 1306, 1072' -ForegroundColor Green
    }
    elseif ($line -match '"Tokuno Gate"') {
        $line = '            new AtlasLocation("Tokuno Gate",      1169,  998,  0, Map.Tokuno),'
        $changes++
        Write-Host '  Tokuno Gate fixed: 1169, 998' -ForegroundColor Green
    }

    $line
}

if ($changes -eq 0) {
    Write-Host 'WARNING: No matching lines found.' -ForegroundColor Yellow
    exit 1
}

$newLines | Set-Content $file -Encoding UTF8

Write-Host ''
Write-Host "SUCCESS - $changes moongate(s) corrected." -ForegroundColor Green
Write-Host 'Now run Compile.WIN - Debug.bat' -ForegroundColor Cyan
