# Patch_AtlasTowns_20260518_1530.ps1
# Replaces all town coordinates in WorldAtlas.cs with verified
# values from Regions.xml <go> tags and moongate region centres.
# Run from VS Code terminal: .\Patch_AtlasTowns_20260518_1530.ps1

$file = 'C:\UO\SERVUO\Scripts\Custom\WorldAtlas.cs'

Write-Host '=== Atlas Town Coordinate Fix ===' -ForegroundColor Cyan

if (-not (Test-Path $file)) {
    Write-Host 'ERROR: WorldAtlas.cs not found' -ForegroundColor Red
    exit 1
}

Copy-Item $file ($file + '.bak') -Force
Write-Host 'Backup created.' -ForegroundColor Gray

$lines = Get-Content $file -Encoding UTF8
$changes = 0

# Lookup table: name -> corrected line (all coords from Regions.xml go tags)
$fixes = @{
    '"Britain"'         = '            new AtlasLocation("Britain",         1495, 1629, 10, Map.Felucca),'
    '"Trinsic"'         = '            new AtlasLocation("Trinsic",         1867, 2780,  0, Map.Felucca),'
    '"Minoc"'           = '            new AtlasLocation("Minoc",           2466,  544,  0, Map.Felucca),'
    '"Vesper"'          = '            new AtlasLocation("Vesper",          2899,  676,  0, Map.Felucca),'
    '"Cove"'            = '            new AtlasLocation("Cove",            2275, 1210,  0, Map.Felucca),'
    '"Skara Brae"'      = '            new AtlasLocation("Skara Brae",       632, 2233,  0, Map.Felucca),'
    '"Yew"'             = '            new AtlasLocation("Yew",              546,  992,  0, Map.Felucca),'
    '"Jhelom"'          = '            new AtlasLocation("Jhelom",          1383, 3815,  0, Map.Felucca),'
    '"Moonglow"'        = '            new AtlasLocation("Moonglow",        4467, 1284,  0, Map.Felucca),'
    '"Magincia"'        = '            new AtlasLocation("Magincia",        3714, 2220, 20, Map.Felucca),'
    '"Nujel''m"'        = '            new AtlasLocation("Nujel''m",        3732, 1279,  0, Map.Felucca),'
    '"Ocllo"'           = '            new AtlasLocation("Ocllo",           3650, 2519,  0, Map.Felucca),'
    '"Serpent''s Hold"' = '            new AtlasLocation("Serpent''s Hold", 3010, 3371, 15, Map.Felucca),'
    '"Papua"'           = '            new AtlasLocation("Papua",           5769, 3176,  0, Map.Felucca),'
    '"Delucia"'         = '            new AtlasLocation("Delucia",         5228, 3978, 37, Map.Felucca),'
    '"New Haven"'       = '            new AtlasLocation("New Haven",       3503, 2574, 14, Map.Trammel),'
    '"Luna"'            = '            new AtlasLocation("Luna",             989,  520,-50, Map.Malas),'
    '"Umbra"'           = '            new AtlasLocation("Umbra",           2049, 1344,-85, Map.Malas),'
    '"Zento"'           = '            new AtlasLocation("Zento",            736, 1256, 30, Map.Tokuno),'
    '"Mistas"'          = '            new AtlasLocation("Mistas",           818, 1073,-30, Map.Ilshenar),'
}

$newLines = $lines | ForEach-Object {
    $line = $_
    $matched = $false

    foreach ($key in $fixes.Keys) {
        if ($line -match [regex]::Escape($key) -and $line -match 'AtlasLocation') {
            $newLine = $fixes[$key]
            if ($line -ne $newLine) {
                Write-Host "  Fixed: $($key.Trim('`"'))" -ForegroundColor Green
                $line = $newLine
                $changes++
            }
            $matched = $true
            break
        }
    }

    $line
}

if ($changes -eq 0) {
    Write-Host 'No changes needed - already up to date.' -ForegroundColor Yellow
    exit 0
}

$newLines | Set-Content $file -Encoding UTF8

Write-Host ''
Write-Host "SUCCESS - $changes town coordinate(s) corrected." -ForegroundColor Green
Write-Host 'Now run Compile.WIN - Debug.bat' -ForegroundColor Cyan
