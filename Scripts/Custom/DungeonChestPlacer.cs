// ============================================================
// DungeonChestPlacer.cs
// Scripts/Custom/DungeonChestPlacer.cs
//
// Places two TreasureLevel5 spawners in each of four deep
// dungeon locations. Each spawner holds 1 chest and resets
// 5-15 minutes after the chest is opened and auto-deletes.
//
// Runs once at startup. Uses a marker item to prevent
// duplicate placement across server restarts.
//
// To force a re-run (e.g. after moving spawners): delete all
// items named "DungeonChestPlacerMarker" in the world, then restart.
// ============================================================

using System;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Server.Custom
{
    public static class DungeonChestPlacer
    {
        private const string MarkerName = "DungeonChestPlacerMarker";

        // Each tuple: two chest locations + map + a label for logging
        // !! Z values are approximate from ArchWizard teleport data.
        //    Verify each in-game with [go <x> <y> <z> and [where.
        //    Adjust z if the chest spawns inside a wall or floor.
        private static readonly (Point3D a, Point3D b, Map map, string label)[] Locations =
        {
            // Destard L3 — deep dragon lair (zone: x5120-5371, y770-1028)
            (
                new Point3D(5175, 855, 5),
                new Point3D(5280, 960, 5),
                Map.Felucca,
                "Destard L3"
            ),
            // Deceit L4 — deepest undead floor (zone: x5122-5370, y518-770)
            (
                new Point3D(5200, 580, 2),
                new Point3D(5320, 690, 2),
                Map.Felucca,
                "Deceit L4"
            ),
            // Hythloth L4 — deepest daemon floor (zone: x5898-6136, y2-246)
            (
                new Point3D(5960, 60, 24),
                new Point3D(6080, 175, 24),
                Map.Felucca,
                "Hythloth L4"
            ),
            // Doom Gauntlet — Malas end-game area (zone: x256-512, y304-560)
            (
                new Point3D(335, 375, -1),
                new Point3D(430, 470, -1),
                Map.Malas,
                "Doom Gauntlet"
            ),
        };

        public static void Initialize()
        {
            Timer.DelayCall(TimeSpan.FromSeconds(2), TryPlace);
        }

        private static void TryPlace()
        {
            // Check for the marker — if it exists we've already run
            foreach (Item item in World.Items.Values)
            {
                if (item.Name == MarkerName)
                    return;
            }

            Console.WriteLine("[DungeonChestPlacer] Placing TreasureLevel5 spawners...");

            foreach (var (a, b, map, label) in Locations)
            {
                PlaceSpawner(a, map, label + " A");
                PlaceSpawner(b, map, label + " B");
            }

            // Leave a marker so we don't double-place on next restart
            var marker = new Static(0x1);  // invisible static tile
            marker.Name    = MarkerName;
            marker.Movable = false;
            marker.Visible = false;
            marker.MoveToWorld(new Point3D(0, 0, 0), Map.Internal);

            Console.WriteLine("[DungeonChestPlacer] Done. 8 spawners placed across 4 dungeons.");
        }

        private static void PlaceSpawner(Point3D loc, Map map, string label)
        {
            var spawner = new Spawner(
                amount:     1,
                minDelay:   TimeSpan.FromMinutes(5),
                maxDelay:   TimeSpan.FromMinutes(15),
                team:       0,
                spawnRange: 0,
                spawnName:  "TreasureLevel5"
            );

            spawner.MoveToWorld(loc, map);

            Console.WriteLine($"[DungeonChestPlacer]   {label} spawner at {loc} ({map})");
        }
    }
}
