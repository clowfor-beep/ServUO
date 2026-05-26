// ============================================================
// FBZones.cs
// Scripts/Custom/FBZones.cs
//
// Single source of truth for all world coordinates used by
// Forsaken Britannia systems.
//
// Usage:
//   bool inZone = FBZones.IsInZone(mobile, SpawnZone.Destard_L1);
//   Map    map  = FBZones.GetMap(SpawnZone.Destard_L1);
//   var rects   = FBZones.GetRects(SpawnZone.Destard_L1);
//
// NOTE: Z-range filtering is each caller's responsibility.
//       FBZones stores only Rectangle2D footprints + map.
//
// Guild home and town board coords are approximate.
// Pin with [where in-game before going live.
// ============================================================

using System.Collections.Generic;
using Server;
using Server.Mobiles;

namespace Server.Custom
{
    // ── Named zone identifiers ────────────────────────────────────────────
    public enum SpawnZone
    {
        // ── Felucca — Newbie ──────────────────────────
        Britain_Graveyard,
        OrcCave,
        Covetous_L1,
        Deceit_L1,
        Despise_L1,
        Destard_L1,
        Hythloth_L1,
        Shame_L1,
        Wrong_L1,

        // ── Ilshenar — Newbie ─────────────────────────
        Ilshenar_Rock,
        Ilshenar_Spectre,
        Ilshenar_Ankh_L1,

        // ── Felucca — Advanced ────────────────────────
        Covetous_L2,
        Deceit_L2,
        Despise_L2,
        Destard_L2,
        Hythloth_L2,
        Shame_L2,
        Wrong_L2,
        Khaldun,
        Fire,
        Ice,
        TerathanKeep,
        Blackthorn,
        MiscDungeons,

        // ── Ilshenar — Advanced ───────────────────────
        Ilshenar_SpiderCave,
        Ilshenar_Blood,
        Ilshenar_Wisp,
        Ilshenar_Ankh_L2,
        Ilshenar_Exodus_L1,

        // ── Malas — Advanced ──────────────────────────
        Malas_Bedlam,
        Malas_Labyrinth,

        // ── Tokuno — Advanced ─────────────────────────
        Tokuno_Yomotsu,
        Tokuno_FanDancer,

        // ── Felucca — Expert ──────────────────────────
        Covetous_L3,
        Deceit_L3,
        Despise_L3,
        Destard_L3,
        Hythloth_L3,
        Shame_L3,
        Wrong_L3,

        // ── Malas — Expert ────────────────────────────
        Malas_Doom,
        Malas_DoomGauntlet,
        Malas_Citadel,

        // ── Ilshenar — Expert ─────────────────────────
        Ilshenar_Exodus_L2,

        // ── TerMur — Expert ───────────────────────────
        TerMur_Stygian,
        TerMur_Underworld,   // TODO: add rects from Regions.xml — coords unverified

        // ── FBPKSpawner — Tier 1 Road Zones ──────────────────────────────────
        // Overworld roads near towns. Weak pool PKs, 5-min respawn.
        Britain_Roads,
        Trinsic_Outskirts,
        Yew_Roads,

        // ── FBPKSpawner — Tier 2 Entrance Zones ──────────────────────────────
        // Dungeon entrance corridors. Moderate pool PKs, 10-min respawn.
        Despise_Entrance,
        Covetous_Entrance,
        Wrong_Entrance,
        Shame_Entrance,

        // ── FBPKSpawner — Tier 3 Deep Zones ──────────────────────────────────
        // Deep dungeon patrols. Veteran killers, 15-min respawn.
        Despise_Level3,
        Hythloth_Deep,
        Deceit_Level3,

        // ── FBEncounterSystem — Ambush Trigger Zones ──────────────────────────
        // Specific corridors used as encounter trigger rectangles.
        Covetous_DarkCorridor,
        Wrong_DeepHall,
        Despise_BottomLevel,
        Hythloth_Abyss,
        Deceit_Level3Entry,

        // ── Hunter Wanted NPC surface zones ────────────────────────────────────
        // Overworld Felucca rects where Wanted NPCs spawn near dungeon entrances.
        WantedZone_NearDespise,
        WantedZone_NearCovetous,
        WantedZone_NearShame,
        WantedZone_NearWrong,
        WantedZone_NearDestard,
        WantedZone_NearDeceit,
        WantedZone_NearHythloth,
    }

    // ── Hunter spawn entry ────────────────────────────────────────────────
    // References the zone where this target spawns and the display name used
    // in shard-wide broadcast messages.  Actual Point3D is resolved at spawn
    // time by FBZones.GetRandomSpawnPoint so creatures never land in walls.
    public struct HunterSpawnEntry
    {
        public SpawnZone Zone;
        public string    DungeonName;

        public HunterSpawnEntry(SpawnZone zone, string dungeon)
        {
            Zone        = zone;
            DungeonName = dungeon;
        }
    }

    // ── Zone coordinate store ─────────────────────────────────────────────
    public static class FBZones
    {
        // Internal record: map + rectangles for one zone
        private struct ZoneData
        {
            public readonly Map           Map;
            public readonly Rectangle2D[] Rects;

            public ZoneData(Map map, params Rectangle2D[] rects)
            {
                Map   = map;
                Rects = rects;
            }
        }

        private static readonly Dictionary<SpawnZone, ZoneData> _zones;

        // Static constructor — populates the dictionary once at class init
        static FBZones()
        {
            _zones = new Dictionary<SpawnZone, ZoneData>
            {
                // ── Felucca — Newbie ──────────────────────────────────────────────

                [SpawnZone.Britain_Graveyard] = new ZoneData(Map.Felucca,
                    new Rectangle2D(1333, 1441, 84, 82)),

                [SpawnZone.OrcCave] = new ZoneData(Map.Felucca,
                    new Rectangle2D(5281, 1283,  92, 103),
                    new Rectangle2D(5267, 1955,  97,  91),
                    new Rectangle2D(5127, 1941,  37,  83)),

                [SpawnZone.Covetous_L1] = new ZoneData(Map.Felucca,
                    new Rectangle2D(5376, 1793, 201, 255)),

                [SpawnZone.Deceit_L1] = new ZoneData(Map.Felucca,
                    new Rectangle2D(5122,  518, 248, 252)),

                [SpawnZone.Despise_L1] = new ZoneData(Map.Felucca,
                    new Rectangle2D(5377,  516, 254, 506)),

                [SpawnZone.Destard_L1] = new ZoneData(Map.Felucca,
                    new Rectangle2D(5120,  770, 251, 258)),

                [SpawnZone.Hythloth_L1] = new ZoneData(Map.Felucca,
                    new Rectangle2D(5898,    2, 238, 244)),

                [SpawnZone.Shame_L1] = new ZoneData(Map.Felucca,
                    new Rectangle2D(5377,    2, 257, 260),
                    new Rectangle2D(5635,    2, 260, 124)),

                [SpawnZone.Wrong_L1] = new ZoneData(Map.Felucca,
                    new Rectangle2D(5633,  511, 253, 510)),

                // ── Ilshenar — Newbie ─────────────────────────────────────────────

                [SpawnZone.Ilshenar_Rock] = new ZoneData(Map.Ilshenar,
                    new Rectangle2D(2176,  288,  24,  40),
                    new Rectangle2D(2088,    8, 160, 176)),

                [SpawnZone.Ilshenar_Spectre] = new ZoneData(Map.Ilshenar,
                    new Rectangle2D(1936, 1000,  96, 120)),

                [SpawnZone.Ilshenar_Ankh_L1] = new ZoneData(Map.Ilshenar,
                    new Rectangle2D(   0, 1248, 176, 344)),

                // ── Felucca — Advanced ────────────────────────────────────────────
                // Note: L2 floors share the same rectangle footprint as L1 but differ
                // in Z range — PKEncounterSystem applies Z filtering, not FBZones.

                [SpawnZone.Covetous_L2] = new ZoneData(Map.Felucca,
                    new Rectangle2D(5376, 1793, 201, 255)),

                [SpawnZone.Deceit_L2] = new ZoneData(Map.Felucca,
                    new Rectangle2D(5122,  518, 248, 252)),

                [SpawnZone.Despise_L2] = new ZoneData(Map.Felucca,
                    new Rectangle2D(5377,  516, 254, 506)),

                [SpawnZone.Destard_L2] = new ZoneData(Map.Felucca,
                    new Rectangle2D(5120,  770, 251, 258)),

                [SpawnZone.Hythloth_L2] = new ZoneData(Map.Felucca,
                    new Rectangle2D(5898,    2, 238, 244)),

                [SpawnZone.Shame_L2] = new ZoneData(Map.Felucca,
                    new Rectangle2D(5377,    2, 257, 260),
                    new Rectangle2D(5635,    2, 260, 124)),

                [SpawnZone.Wrong_L2] = new ZoneData(Map.Felucca,
                    new Rectangle2D(5633,  511, 253, 510)),

                [SpawnZone.Khaldun] = new ZoneData(Map.Felucca,
                    new Rectangle2D(5381, 1284, 247, 225)),

                [SpawnZone.Fire] = new ZoneData(Map.Felucca,
                    new Rectangle2D(5635, 1285, 245, 235)),

                [SpawnZone.Ice] = new ZoneData(Map.Felucca,
                    new Rectangle2D(5668,  130, 220, 138),
                    new Rectangle2D(5800,  319,  63,  65),
                    new Rectangle2D(5654,  300,  54,  40)),

                [SpawnZone.TerathanKeep] = new ZoneData(Map.Felucca,
                    new Rectangle2D(5404, 3099,  77,  68),
                    new Rectangle2D(5120, 1530, 254, 258)),

                [SpawnZone.Blackthorn] = new ZoneData(Map.Felucca,
                    new Rectangle2D(6151, 2301, 413, 539)),

                [SpawnZone.MiscDungeons] = new ZoneData(Map.Felucca,
                    new Rectangle2D(5886, 1281, 257, 254)),

                // ── Ilshenar — Advanced ───────────────────────────────────────────

                [SpawnZone.Ilshenar_SpiderCave] = new ZoneData(Map.Ilshenar,
                    new Rectangle2D(1752,  952, 112,  48),
                    new Rectangle2D(1480,  864,  48,  32)),

                [SpawnZone.Ilshenar_Blood] = new ZoneData(Map.Ilshenar,
                    new Rectangle2D(2032,  808, 168, 256)),

                [SpawnZone.Ilshenar_Wisp] = new ZoneData(Map.Ilshenar,
                    new Rectangle2D( 616, 1480,  88,  96),
                    new Rectangle2D( 816, 1448,  96, 136),
                    new Rectangle2D( 912, 1456, 112, 128)),

                [SpawnZone.Ilshenar_Ankh_L2] = new ZoneData(Map.Ilshenar,
                    new Rectangle2D(   0, 1248, 176, 344)),

                [SpawnZone.Ilshenar_Exodus_L1] = new ZoneData(Map.Ilshenar,
                    new Rectangle2D(1832,   16, 248, 200)),

                // ── Malas — Advanced ──────────────────────────────────────────────

                [SpawnZone.Malas_Bedlam] = new ZoneData(Map.Malas,
                    new Rectangle2D(  80, 1590, 130, 100)),

                [SpawnZone.Malas_Labyrinth] = new ZoneData(Map.Malas,
                    new Rectangle2D( 255, 1791, 256, 256)),

                // ── Tokuno — Advanced ─────────────────────────────────────────────

                [SpawnZone.Tokuno_Yomotsu] = new ZoneData(Map.Tokuno,
                    new Rectangle2D(   0,    0, 129, 129)),

                [SpawnZone.Tokuno_FanDancer] = new ZoneData(Map.Tokuno,
                    new Rectangle2D(  40,  320, 170, 400)),

                // ── Felucca — Expert ──────────────────────────────────────────────

                [SpawnZone.Covetous_L3] = new ZoneData(Map.Felucca,
                    new Rectangle2D(5376, 1793, 201, 255)),

                [SpawnZone.Deceit_L3] = new ZoneData(Map.Felucca,
                    new Rectangle2D(5122,  518, 248, 252)),

                [SpawnZone.Despise_L3] = new ZoneData(Map.Felucca,
                    new Rectangle2D(5377,  516, 254, 506)),

                [SpawnZone.Destard_L3] = new ZoneData(Map.Felucca,
                    new Rectangle2D(5120,  770, 251, 258)),

                [SpawnZone.Hythloth_L3] = new ZoneData(Map.Felucca,
                    new Rectangle2D(5898,    2, 238, 244)),

                [SpawnZone.Shame_L3] = new ZoneData(Map.Felucca,
                    new Rectangle2D(5377,    2, 257, 260),
                    new Rectangle2D(5635,    2, 260, 124)),

                [SpawnZone.Wrong_L3] = new ZoneData(Map.Felucca,
                    new Rectangle2D(5633,  511, 253, 510)),

                // ── Malas — Expert ────────────────────────────────────────────────

                [SpawnZone.Malas_Doom] = new ZoneData(Map.Malas,
                    new Rectangle2D( 256,    0, 256, 304)),

                [SpawnZone.Malas_DoomGauntlet] = new ZoneData(Map.Malas,
                    new Rectangle2D( 256,  304, 256, 256)),

                [SpawnZone.Malas_Citadel] = new ZoneData(Map.Malas,
                    new Rectangle2D(  65, 1865, 130, 125)),

                // ── Ilshenar — Expert ─────────────────────────────────────────────

                [SpawnZone.Ilshenar_Exodus_L2] = new ZoneData(Map.Ilshenar,
                    new Rectangle2D(1832,   16, 248, 200)),

                // ── TerMur — Expert ──────────────────────────────────────────────

                [SpawnZone.TerMur_Stygian] = new ZoneData(Map.TerMur,
                    new Rectangle2D( 301,  328, 775, 474),
                    new Rectangle2D( 780,   32, 296, 296),
                    new Rectangle2D( 436,  802, 304, 178),
                    new Rectangle2D( 754,  881, 108,  93),
                    new Rectangle2D(   0,  684, 134, 176)),

                // ── FBPKSpawner — Tier 1 Road Zones ──────────────────────────────
                // Overworld roads on Map.Felucca. Coords are approximate —
                // verify with [where and tune widths to match actual road tiles.

                [SpawnZone.Britain_Roads] = new ZoneData(Map.Felucca,
                    new Rectangle2D(1380, 1750,  80, 300),   // south road to Trinsic
                    new Rectangle2D(1700, 1610, 300,  80),   // east road toward Vesper
                    new Rectangle2D(1270, 1600, 120, 200)),  // west road toward Yew

                [SpawnZone.Trinsic_Outskirts] = new ZoneData(Map.Felucca,
                    new Rectangle2D(1800, 2620, 200, 150),   // north gate approach
                    new Rectangle2D(1870, 2800, 150, 120)),  // south road approach

                [SpawnZone.Yew_Roads] = new ZoneData(Map.Felucca,
                    new Rectangle2D( 470, 1050, 250, 150),   // main road through Yew
                    new Rectangle2D( 600,  980, 150, 120)),  // road east of Yew

                // ── FBPKSpawner — Tier 2 Entrance Zones ──────────────────────────
                // Dungeon entrance corridors (interior map, 5000+ X range).
                // These are sub-rects of the L1 zones, covering only the entry rooms.

                [SpawnZone.Despise_Entrance] = new ZoneData(Map.Felucca,
                    new Rectangle2D(5390,  520, 120,  80)),

                [SpawnZone.Covetous_Entrance] = new ZoneData(Map.Felucca,
                    new Rectangle2D(5385, 1800, 120,  80)),

                [SpawnZone.Wrong_Entrance] = new ZoneData(Map.Felucca,
                    new Rectangle2D(5645,  520, 100,  80)),

                [SpawnZone.Shame_Entrance] = new ZoneData(Map.Felucca,
                    new Rectangle2D(5385,    8, 120,  80)),

                // ── FBPKSpawner — Tier 3 Deep Zones ──────────────────────────────
                // Named deep-zone rects for pool PK patrol assignment.
                // Share the same footprint as L2/L3 PKEncounterSystem zones;
                // callers apply Z filtering to distinguish depth.

                [SpawnZone.Despise_Level3] = new ZoneData(Map.Felucca,
                    new Rectangle2D(5377,  516, 254, 506)),

                [SpawnZone.Hythloth_Deep] = new ZoneData(Map.Felucca,
                    new Rectangle2D(5898,    2, 238, 244)),

                [SpawnZone.Deceit_Level3] = new ZoneData(Map.Felucca,
                    new Rectangle2D(5122,  518, 248, 252)),

                // ── FBEncounterSystem — Ambush Trigger Zones ──────────────────────
                // Small corridor rects used as encounter trigger areas.
                // FBEncounterSystem checks IsInZone; FBPKSpawner does NOT use these.

                [SpawnZone.Covetous_DarkCorridor] = new ZoneData(Map.Felucca,
                    new Rectangle2D(5430, 1860,  60,  40)),

                [SpawnZone.Wrong_DeepHall] = new ZoneData(Map.Felucca,
                    new Rectangle2D(5690,  720,  60,  40)),

                [SpawnZone.Despise_BottomLevel] = new ZoneData(Map.Felucca,
                    new Rectangle2D(5430,  850,  80,  60)),

                [SpawnZone.Hythloth_Abyss] = new ZoneData(Map.Felucca,
                    new Rectangle2D(5940,  150,  60,  40)),

                [SpawnZone.Deceit_Level3Entry] = new ZoneData(Map.Felucca,
                    new Rectangle2D(5180,  660,  60,  40)),

                // ── Hunter Wanted NPC surface zones ───────────────────────────────
                // Overworld Felucca rects near dungeon entrances where Wanted NPCs
                // lurk. GetRandomSpawnPoint samples within these for valid floor tiles.

                [SpawnZone.WantedZone_NearDespise] = new ZoneData(Map.Felucca,
                    new Rectangle2D(1260, 1040, 80, 80)),  // near Despise entrance (1296,1082)

                [SpawnZone.WantedZone_NearCovetous] = new ZoneData(Map.Felucca,
                    new Rectangle2D(2455,  880, 50, 50)),

                [SpawnZone.WantedZone_NearShame] = new ZoneData(Map.Felucca,
                    new Rectangle2D( 490, 1455, 50, 50)),

                [SpawnZone.WantedZone_NearWrong] = new ZoneData(Map.Felucca,
                    new Rectangle2D(2025,  155, 50, 50)),

                [SpawnZone.WantedZone_NearDestard] = new ZoneData(Map.Felucca,
                    new Rectangle2D( 965, 3055, 50, 50)),

                [SpawnZone.WantedZone_NearDeceit] = new ZoneData(Map.Felucca,
                    new Rectangle2D(1950,  125, 50, 50)),

                [SpawnZone.WantedZone_NearHythloth] = new ZoneData(Map.Felucca,
                    new Rectangle2D(4685, 3785, 50, 50)),
            };
        }

        // ── Patrol waypoints ─────────────────────────────────────────────────
        // Used by FBPKSpawner to assign PoolPK patrol routes within a zone.
        // Each zone has one or more routes (outer array); each route is an
        // ordered list of waypoints the NPC walks in sequence (inner array).
        // All coords are approximate — tune with [where in-game.

        public static readonly Dictionary<SpawnZone, Point2D[][]> Waypoints =
            new Dictionary<SpawnZone, Point2D[][]>
        {
            // ── Tier 1 — Road zones ────────────────────────────────────────
            [SpawnZone.Britain_Roads] = new Point2D[][]
            {
                new Point2D[] { new Point2D(1400, 1780), new Point2D(1400, 1880), new Point2D(1400, 1980), new Point2D(1400, 1880) },
                new Point2D[] { new Point2D(1720, 1630), new Point2D(1820, 1630), new Point2D(1920, 1630), new Point2D(1820, 1630) },
                new Point2D[] { new Point2D(1290, 1640), new Point2D(1290, 1720), new Point2D(1290, 1780), new Point2D(1290, 1720) },
            },

            [SpawnZone.Trinsic_Outskirts] = new Point2D[][]
            {
                new Point2D[] { new Point2D(1840, 2640), new Point2D(1900, 2680), new Point2D(1940, 2660), new Point2D(1900, 2630) },
                new Point2D[] { new Point2D(1880, 2810), new Point2D(1930, 2840), new Point2D(1970, 2820), new Point2D(1930, 2800) },
            },

            [SpawnZone.Yew_Roads] = new Point2D[][]
            {
                new Point2D[] { new Point2D(500, 1070), new Point2D(580, 1110), new Point2D(660, 1080), new Point2D(580, 1060) },
                new Point2D[] { new Point2D(630, 990),  new Point2D(680, 1020), new Point2D(730, 1000), new Point2D(680,  990) },
            },

            // ── Tier 2 — Entrance zones ────────────────────────────────────
            [SpawnZone.Despise_Entrance] = new Point2D[][]
            {
                new Point2D[] { new Point2D(5400, 530), new Point2D(5450, 555), new Point2D(5500, 575), new Point2D(5450, 555) },
            },

            [SpawnZone.Covetous_Entrance] = new Point2D[][]
            {
                new Point2D[] { new Point2D(5395, 1810), new Point2D(5445, 1830), new Point2D(5495, 1855), new Point2D(5445, 1830) },
            },

            [SpawnZone.Wrong_Entrance] = new Point2D[][]
            {
                new Point2D[] { new Point2D(5655, 530), new Point2D(5705, 550), new Point2D(5735, 575), new Point2D(5705, 550) },
            },

            [SpawnZone.Shame_Entrance] = new Point2D[][]
            {
                new Point2D[] { new Point2D(5393, 15), new Point2D(5440, 30), new Point2D(5490, 50), new Point2D(5440, 30) },
            },

            // ── Tier 3 — Deep zones ────────────────────────────────────────
            [SpawnZone.Despise_Level3] = new Point2D[][]
            {
                new Point2D[] { new Point2D(5430, 700), new Point2D(5500, 750), new Point2D(5570, 720), new Point2D(5500, 680) },
                new Point2D[] { new Point2D(5460, 850), new Point2D(5530, 900), new Point2D(5600, 870), new Point2D(5530, 840) },
            },

            [SpawnZone.Hythloth_Deep] = new Point2D[][]
            {
                new Point2D[] { new Point2D(5940, 100), new Point2D(5990, 140), new Point2D(6060, 120), new Point2D(5990, 100) },
                new Point2D[] { new Point2D(5960, 170), new Point2D(6020, 200), new Point2D(6080, 180), new Point2D(6020, 160) },
            },

            [SpawnZone.Deceit_Level3] = new Point2D[][]
            {
                new Point2D[] { new Point2D(5180, 680), new Point2D(5240, 720), new Point2D(5300, 700), new Point2D(5240, 670) },
                new Point2D[] { new Point2D(5200, 620), new Point2D(5260, 650), new Point2D(5330, 630), new Point2D(5260, 610) },
            },
        };

        // ── Patrol waypoint helper ────────────────────────────────────────────

        /// <summary>
        /// Returns a random patrol route for the given zone, or null if the
        /// zone has no defined waypoints.
        /// </summary>
        public static Point2D[] GetRandomRoute(SpawnZone zone)
        {
            if (!Waypoints.TryGetValue(zone, out Point2D[][] routes) || routes.Length == 0)
                return null;
            return routes[Utility.Random(routes.Length)];
        }

        // ── Guild home locations ──────────────────────────────────────────────
        // All clustered near the Britain bank steps — confirmed open road area.
        // Spread south along Y axis (not east, which hits buildings at X>1450).
        // ±20-tile random wander means each guild naturally covers a 40x40 area.

        // Tier 1 — Blue / Grey
        public static readonly Point3D Wanderers_Home         = new Point3D(1440, 1697, 5);
        public static readonly Point3D CraftsmensLeague_Home  = new Point3D(1444, 1697, 5);
        public static readonly Point3D ShadowHand_Home        = new Point3D(1440, 1701, 5);

        // Tier 2 — Blue
        public static readonly Point3D IronCompany_Home       = new Point3D(1444, 1701, 5);
        public static readonly Point3D ArcaneBrotherhood_Home = new Point3D(1440, 1705, 5);
        public static readonly Point3D SilverWolves_Home      = new Point3D(1444, 1705, 5);

        // Tier 3 — Blue / Perm Grey / Mixed
        public static readonly Point3D PaladinOrder_Home      = new Point3D(1442, 1713, 5);
        public static readonly Point3D DeadWatchers_Home      = new Point3D(1460, 1713, 5);
        public static readonly Point3D DreadHunters_Home      = new Point3D(1478, 1713, 5);

        // Tier 3 — Red / Grey (not placed in Felucca cities — guards)
        public static readonly Point3D BloodPact_Home         = new Point3D(5456,  724, 0); // Destard outskirts
        public static readonly Point3D TheVoid_Home           = new Point3D(5168,  520, 0); // Deceit outskirts
        public static readonly Point3D Shadowblade_Home       = new Point3D(5664,  516, 0); // Wrong outskirts

        // ── Hunter System spawn tables ────────────────────────────────────────
        // Actual Point3D is resolved at spawn time by GetRandomSpawnPoint so
        // creatures never land inside wall geometry.  Dungeon zones resolve
        // entirely within the dungeon interior map; Wanted zones are overworld
        // Felucca rects near dungeon entrances.

        public static readonly HunterSpawnEntry[] HunterTier1Spawns =
        {
            new HunterSpawnEntry(SpawnZone.Despise_L1, "Despise"),
            new HunterSpawnEntry(SpawnZone.Despise_L1, "Despise"),
            new HunterSpawnEntry(SpawnZone.Despise_L1, "Despise"),
            new HunterSpawnEntry(SpawnZone.Shame_L1,   "Shame"),
            new HunterSpawnEntry(SpawnZone.Shame_L1,   "Shame"),
        };

        public static readonly HunterSpawnEntry[] HunterTier2Spawns =
        {
            new HunterSpawnEntry(SpawnZone.Covetous_L1, "Covetous"),
            new HunterSpawnEntry(SpawnZone.Covetous_L1, "Covetous"),
            new HunterSpawnEntry(SpawnZone.Wrong_L1,    "Wrong"),
            new HunterSpawnEntry(SpawnZone.Shame_L1,    "Shame"),
            new HunterSpawnEntry(SpawnZone.Deceit_L1,   "Deceit"),
        };

        public static readonly HunterSpawnEntry[] HunterTier3Spawns =
        {
            new HunterSpawnEntry(SpawnZone.Deceit_L2,  "Deceit"),
            new HunterSpawnEntry(SpawnZone.Deceit_L2,  "Deceit"),
            new HunterSpawnEntry(SpawnZone.Destard_L2, "Destard"),
            new HunterSpawnEntry(SpawnZone.Destard_L2, "Destard"),
            new HunterSpawnEntry(SpawnZone.Destard_L2, "Destard"),
        };

        public static readonly HunterSpawnEntry[] HunterTier4Spawns =
        {
            new HunterSpawnEntry(SpawnZone.Hythloth_L3, "Hythloth"),
            new HunterSpawnEntry(SpawnZone.Hythloth_L3, "Hythloth"),
            new HunterSpawnEntry(SpawnZone.Hythloth_L3, "Hythloth"),
            new HunterSpawnEntry(SpawnZone.Hythloth_L3, "Hythloth"),
        };

        // Wanted NPCs lurk on the overworld surface near dungeon entrances.
        public static readonly HunterSpawnEntry[] WantedCutthroatSpawns =
        {
            new HunterSpawnEntry(SpawnZone.WantedZone_NearDespise,  "near Despise"),
            new HunterSpawnEntry(SpawnZone.WantedZone_NearCovetous, "near Covetous"),
            new HunterSpawnEntry(SpawnZone.WantedZone_NearShame,    "near Shame"),
        };

        public static readonly HunterSpawnEntry[] WantedMurdererSpawns =
        {
            new HunterSpawnEntry(SpawnZone.WantedZone_NearWrong,   "near Wrong"),
            new HunterSpawnEntry(SpawnZone.WantedZone_NearDestard, "near Destard"),
            new HunterSpawnEntry(SpawnZone.WantedZone_NearDeceit,  "near Deceit"),
        };

        public static readonly HunterSpawnEntry[] WantedDreadLordSpawns =
        {
            new HunterSpawnEntry(SpawnZone.WantedZone_NearHythloth, "near Hythloth"),
            new HunterSpawnEntry(SpawnZone.WantedZone_NearDeceit,   "near Deceit"),
            new HunterSpawnEntry(SpawnZone.WantedZone_NearDestard,  "near Destard"),
        };

        // ── Per-SimPlayer home locations ──────────────────────────────────────
        // TODO: 144 individual Point3D entries (GuildName_MemberName_Home) will
        // be added here once GuildSystem_FullDesignDoc.txt member names are
        // finalised and SimPlayer.cs is being built (Step 5 in build order).

        // ── Static object placement coords ───────────────────────────────────
        // Named Point3D for every bulletin board, quest NPC, and reputation
        // contact placed via ForsakenBritannia.xml or initialization code.
        // Naming: [System/Guild]_[ObjectType]_[Location]
        // All coords approximate — verify with [where before placing in XML.

        // Britain bank area (Phase 1 — start here)
        public static readonly Point3D BountyBoard_Britain_Bank   = new Point3D(1439, 1695, 5);
        public static readonly Point3D QuestNPC_Wanderers_Britain = new Point3D(1445, 1698, 5);

        // Despise entrance area (overworld cave mouth, Map.Felucca)
        public static readonly Point3D IronCompany_Contact_Despise  = new Point3D(3445,  988, 0);
        public static readonly Point3D SilverWolves_Board_Despise   = new Point3D(3449,  986, 0);

        // Trinsic
        public static readonly Point3D ReputationNPC_PaladinOrder_Trinsic = new Point3D(1852, 2754, 0);
        public static readonly Point3D QuestNPC_Gather_Trinsic            = new Point3D(1858, 2756, 0);

        // Minoc
        public static readonly Point3D CraftsmensLeague_Board_Minoc = new Point3D(2527, 543, 0);
        public static readonly Point3D QuestNPC_Mining_Minoc         = new Point3D(2533, 543, 0);

        // Legacy aliases — kept for any existing code referencing old names
        public static readonly Point3D BritainBoard = BountyBoard_Britain_Bank;
        public static readonly Point3D TrinsicBoard = ReputationNPC_PaladinOrder_Trinsic;
        public static readonly Point3D MinocBoard   = CraftsmensLeague_Board_Minoc;

        // ── Public helpers ────────────────────────────────────────────────────

        /// <summary>
        /// Finds a random valid spawn point within the named zone.
        ///
        /// For each of 50 random X,Y candidates:
        ///   1. Try GetAverageZ (reliable for overworld surface tiles).
        ///   2. If that fails, probe Z from -128 to 128 in steps of 5.
        ///      Dungeon interiors have no real land layer — GetAverageZ returns
        ///      the ocean tile Z (0), not the actual dungeon floor Z (20-128 for L1,
        ///      lower for deeper levels). Probing finds the real floor tile.
        ///
        /// Uses static tile data to locate the highest walkable surface at (x,y).
        /// Dungeon floors are static geometry — CanFit alone can't find the right Z
        /// because it returns true at any high-altitude Z where nothing is blocking.
        ///
        /// Returns Point3D.Zero if no valid tile is found (caller must guard).
        /// </summary>
        public static Point3D GetRandomSpawnPoint(SpawnZone zone)
        {
            if (!_zones.TryGetValue(zone, out ZoneData data))
                return Point3D.Zero;

            for (int attempt = 0; attempt < 50; attempt++)
            {
                Rectangle2D rect = data.Rects[Utility.Random(data.Rects.Length)];
                int x = rect.X + Utility.Random(rect.Width);
                int y = rect.Y + Utility.Random(rect.Height);

                int z = FindSurfaceZ(data.Map, x, y);
                if (z != int.MinValue && data.Map.CanFit(x, y, z, 16, false, false))
                    return new Point3D(x, y, z);
            }

            return Point3D.Zero;
        }

        /// <summary>
        /// Finds the Z coordinate of the highest walkable surface at (x, y).
        ///
        /// For dungeon zones, the floor is made of static tiles — there is no real
        /// land layer (the land tile is ocean at Z=0).  We read static tile data
        /// directly and pick the highest tile that is Surface, not Impassable, and
        /// not Wet.  Falls back to GetAverageZ for overworld / surface zones.
        ///
        /// Returns int.MinValue when no walkable surface exists at this column.
        /// </summary>
        private static int FindSurfaceZ(Map map, int x, int y)
        {
            int best = int.MinValue;

            // Read static tiles at this column — dungeon floors are static geometry
            StaticTile[] statics = map.Tiles.GetStaticTiles(x, y, true);
            foreach (StaticTile tile in statics)
            {
                ItemData id = TileData.ItemTable[tile.ID & TileData.MaxItemValue];
                if ((id.Flags & TileFlag.Surface)    != 0 &&
                    (id.Flags & TileFlag.Impassable)  == 0 &&
                    (id.Flags & TileFlag.Wet)         == 0)
                {
                    int top = tile.Z + id.CalcHeight;
                    if (top > best)
                        best = top;
                }
            }

            // Fall back to land tile for overworld / surface zones
            if (best == int.MinValue)
            {
                LandTile lt = map.Tiles.GetLandTile(x, y);
                TileFlag  lf = TileData.LandTable[lt.ID & TileData.MaxLandValue].Flags;
                if ((lf & TileFlag.Impassable) == 0 && (lf & TileFlag.Wet) == 0)
                    best = map.GetAverageZ(x, y);
            }

            return best;
        }

        /// <summary>
        /// Returns true if the mobile is on the correct map and within any
        /// rectangle of the named zone. Does NOT check Z — callers apply Z
        /// filtering as needed.
        /// </summary>
        public static bool IsInZone(Mobile m, SpawnZone zone)
        {
            if (!_zones.TryGetValue(zone, out ZoneData data))
                return false;

            if (m.Map != data.Map)
                return false;

            foreach (Rectangle2D r in data.Rects)
                if (r.Contains(m.Location))
                    return true;

            return false;
        }

        /// <summary>Returns the Map associated with the given zone.</summary>
        public static Map GetMap(SpawnZone zone)
        {
            return _zones.TryGetValue(zone, out ZoneData data) ? data.Map : Map.Internal;
        }

        /// <summary>Returns the rectangle array for the given zone.</summary>
        public static Rectangle2D[] GetRects(SpawnZone zone)
        {
            return _zones.TryGetValue(zone, out ZoneData data)
                ? data.Rects
                : new Rectangle2D[0];
        }
    }
}
