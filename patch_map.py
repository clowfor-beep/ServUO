import re

with open('/home/servuo/Server/Map.cs', 'r') as f:
    content = f.read()

# Find and replace the iterator method that crashes mcs
old = 'yield break;'
if old not in content:
    print("No yield break found - already patched?")
    exit()

# Rewrite EnumerateSectors without yield return
import re
pattern = r'(public static IEnumerable<Sector> EnumerateSectors\(Map map, Rectangle2D bounds\)\s*\{)[^}]*?(\bNextSector\b[^}]*?\})\s*\}'

content2 = re.sub(
    r'public static IEnumerable<Sector> EnumerateSectors\(Map map, Rectangle2D bounds\)\s*\{.*?\}(?=\s*public)',
    '''public static IEnumerable<Sector> EnumerateSectors(Map map, Rectangle2D bounds)
\t\t{
\t\t\tvar sectors = new System.Collections.Generic.List<Sector>();

\t\t\tif (map == null || map == Map.Internal)
\t\t\t\treturn sectors;

\t\t\tint x1 = bounds.Start.X, y1 = bounds.Start.Y, x2 = bounds.End.X, y2 = bounds.End.Y;
\t\t\tint xSector, ySector;

\t\t\tif (!Bound(map, ref x1, ref y1, ref x2, ref y2, out xSector, out ySector))
\t\t\t\treturn sectors;

\t\t\tvar index = 0;
\t\t\tSector s;

\t\t\twhile (NextSector(map, x1, y1, x2, y2, ref index, ref xSector, ref ySector, out s))
\t\t\t{
\t\t\t\tsectors.Add(s);
\t\t\t}

\t\t\treturn sectors;
\t\t}''',
    content,
    flags=re.DOTALL
)

if content2 != content:
    with open('/home/servuo/Server/Map.cs', 'w') as f:
        f.write(content2)
    print("Patched successfully")
else:
    print("Pattern not found - file may already be patched or structure differs")
