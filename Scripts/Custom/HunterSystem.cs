// ============================================================
// HunterSystem.cs
// Scripts/Custom/HunterSystem.cs
//
// Core of the Hunter System:
//   - HunterSystem        static class — spawn logic, broadcasts,
//                         player data API, rank management
//   - HunterSpawnTimer    repeating timer (90-180 min)
//   - WantedSpawnTimer    repeating timer (4-6 hours)
//
// Data is persisted to Saves/Misc/HunterSystem.bin via
// Persistence.Serialize / Persistence.Deserialize.
//
// Design doc: Design/HunterSystemDesignDoc.txt
// ============================================================

using System;
using System.Collections.Generic;
using System.IO;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Server.Custom
{
    // ============================================================
    // SPAWN ENTRY
    // ============================================================

    public struct HunterSpawnEntry
    {
        public Point3D Location;
        public Map     Map;
        public string  DungeonName;

        public HunterSpawnEntry(int x, int y, int z, Map map, string dungeon)
        {
            Location    = new Point3D(x, y, z);
            Map         = map;
            DungeonName = dungeon;
        }
    }

    // ============================================================
    // HUNTER SYSTEM
    // ============================================================

    public static class HunterSystem
    {
        // --------------------------------------------------------
        // PERSISTENCE
        // --------------------------------------------------------

        private static readonly string SavePath = Path.Combine("Saves/Misc", "HunterSystem.bin");

        // Hunter Points per player serial
        private static Dictionary<Serial, int> _points = new Dictionary<Serial, int>();

        // Title deeds granted via token shop
        private static HashSet<Serial> _titleDeedOwners = new HashSet<Serial>();

        // Active hunt tracking
        private static Serial   _activeHuntSerial  = Serial.Zero;
        private static DateTime _huntSpawnTime      = DateTime.MinValue;
        private static string   _activeHuntName     = string.Empty;

        private static Serial   _activeWantedSerial = Serial.Zero;
        private static string   _activeWantedName   = string.Empty;

        // --------------------------------------------------------
        // STARTUP
        // --------------------------------------------------------

        public static void Configure()
        {
            EventSink.WorldSave += OnWorldSave;
            EventSink.WorldLoad += OnWorldLoad;
        }

        public static void Initialize()
        {
            // Start spawn timers after world is loaded
            Timer.DelayCall(TimeSpan.FromSeconds(10), StartTimers);
        }

        private static void StartTimers()
        {
            new HunterSpawnTimer().Start();
            new WantedSpawnTimer().Start();
        }

        // --------------------------------------------------------
        // PERSISTENCE — save / load
        // --------------------------------------------------------

        private static void OnWorldSave(WorldSaveEventArgs e)
        {
            Persistence.Serialize(SavePath, writer =>
            {
                writer.Write(0); // version

                // Player points
                writer.Write(_points.Count);
                foreach (var kvp in _points)
                {
                    writer.Write(kvp.Key);
                    writer.Write(kvp.Value);
                }

                // Title deed owners
                writer.Write(_titleDeedOwners.Count);
                foreach (Serial s in _titleDeedOwners)
                    writer.Write(s);

                // Active hunt target
                writer.Write(_activeHuntSerial);
                writer.Write(_huntSpawnTime);
                writer.Write(_activeHuntName);

                // Active wanted target
                writer.Write(_activeWantedSerial);
                writer.Write(_activeWantedName);
            });
        }

        private static void OnWorldLoad()
        {
            if (!File.Exists(SavePath)) return;

            Persistence.Deserialize(SavePath, reader =>
            {
                int version = reader.ReadInt();

                int ptCount = reader.ReadInt();
                for (int i = 0; i < ptCount; i++)
                {
                    Serial s = reader.ReadInt();
                    int    p = reader.ReadInt();
                    _points[s] = p;
                }

                int deedCount = reader.ReadInt();
                for (int i = 0; i < deedCount; i++)
                    _titleDeedOwners.Add(reader.ReadInt());

                _activeHuntSerial  = reader.ReadInt();
                _huntSpawnTime     = reader.ReadDateTime();
                _activeHuntName    = reader.ReadString();

                _activeWantedSerial = reader.ReadInt();
                _activeWantedName   = reader.ReadString();
            });
        }

        // --------------------------------------------------------
        // PLAYER DATA API
        // --------------------------------------------------------

        public static int GetPoints(Mobile m)
        {
            if (m == null) return 0;
            return _points.TryGetValue(m.Serial, out int p) ? p : 0;
        }

        public static void AddPoints(Mobile m, int amount)
        {
            if (m == null || amount <= 0) return;
            _points[m.Serial] = GetPoints(m) + amount;
        }

        public static void GrantTitleDeed(Mobile m, string title)
        {
            if (m == null) return;
            _titleDeedOwners.Add(m.Serial);
            // Store title separately if needed; for now just confirm
        }

        // --------------------------------------------------------
        // RANK SYSTEM
        // --------------------------------------------------------

        private static readonly (int pts, string title)[] Ranks =
        {
            (100, "the Eternal Hunter"),
            (50,  "the Apex Predator"),
            (30,  "the Legendary Hunter"),
            (15,  "the Monster Hunter"),
            (5,   "the Hunter"),
            (1,   "the Tracker"),
        };

        public static string GetRankTitle(int points)
        {
            foreach (var (pts, title) in Ranks)
                if (points >= pts)
                    return title;
            return string.Empty;
        }

        public static void CheckRankUp(Mobile m)
        {
            if (!(m is PlayerMobile pm)) return;

            int pts = GetPoints(m);
            string newTitle = GetRankTitle(pts);
            if (newTitle.Length == 0) return;

            // If they currently have a hunter title, update it automatically
            bool hasTitleSet = false;
            foreach (var (_, t) in Ranks)
                if (pm.Title == t) { hasTitleSet = true; break; }

            if (hasTitleSet)
                pm.Title = newTitle;

            // Shard-wide broadcast for Eternal Hunter (100 pts)
            if (pts >= 100 && !_titleDeedOwners.Contains(pm.Serial))
            {
                _titleDeedOwners.Add(pm.Serial);
                World.Broadcast(0x44, false,
                    $"[Hunter's Guild] {pm.Name} has achieved the rank of {newTitle}! All hail the Eternal Hunter!");
            }
        }

        // --------------------------------------------------------
        // SPAWN HELPERS — called by timers
        // --------------------------------------------------------

        public static bool HasActiveHunt =>
            _activeHuntSerial != Serial.Zero &&
            World.FindMobile(_activeHuntSerial) != null;

        public static bool HasActiveWanted =>
            _activeWantedSerial != Serial.Zero &&
            World.FindMobile(_activeWantedSerial) != null;

        public static void SpawnHunterTarget()
        {
            if (HasActiveHunt) return;

            // Pick a random tier (weighted: Tier 1 most common, Tier 4 rarest)
            int tier = PickTier();

            // Pick a spawn location for this tier
            HunterSpawnEntry entry = PickSpawnEntry(tier);

            // Pick a creature class
            HunterCreature creature = CreateCreature(tier);
            if (creature == null) return;

            creature.MoveToWorld(entry.Location, entry.Map);

            _activeHuntSerial = creature.Serial;
            _huntSpawnTime    = DateTime.UtcNow;
            _activeHuntName   = creature.Name.Replace("[Hunted] ", "");

            // World spawn broadcast
            BroadcastHuntSpawn(_activeHuntName, entry.DungeonName);

            // Schedule 30-minute reminder
            Timer.DelayCall(TimeSpan.FromMinutes(30), () =>
            {
                if (HasActiveHunt)
                    BroadcastHuntReminder(_activeHuntName, entry.DungeonName);
            });

            // 4-hour auto-despawn
            Timer.DelayCall(TimeSpan.FromHours(4), () =>
            {
                if (_activeHuntSerial != Serial.Zero)
                {
                    Mobile m = World.FindMobile(_activeHuntSerial);
                    if (m != null && !m.Deleted && m.Alive)
                    {
                        m.Delete();
                        _activeHuntSerial = Serial.Zero;
                        _activeHuntName   = string.Empty;
                    }
                }
            });
        }

        public static void SpawnWantedTarget()
        {
            if (HasActiveWanted) return;

            int wantedTier = PickWantedTier();
            HunterSpawnEntry entry = PickWantedEntry(wantedTier);

            BaseWantedNPC npc = CreateWantedNPC(wantedTier);
            if (npc == null) return;

            npc.MoveToWorld(entry.Location, entry.Map);

            _activeWantedSerial = npc.Serial;
            _activeWantedName   = npc.Name.Replace("[Wanted] ", "");

            BroadcastWantedSpawn(_activeWantedName, entry.DungeonName);

            // 4-hour auto-despawn
            Timer.DelayCall(TimeSpan.FromHours(4), () =>
            {
                if (_activeWantedSerial != Serial.Zero)
                {
                    Mobile m = World.FindMobile(_activeWantedSerial);
                    if (m != null && !m.Deleted && m.Alive)
                    {
                        m.Delete();
                        _activeWantedSerial = Serial.Zero;
                        _activeWantedName   = string.Empty;
                    }
                }
            });
        }

        public static void OnHunterKilled(HunterCreature creature)
        {
            if (creature.Serial == _activeHuntSerial)
            {
                _activeHuntSerial = Serial.Zero;
                _activeHuntName   = string.Empty;
            }
        }

        public static void OnWantedKilled(BaseWantedNPC npc)
        {
            if (npc.Serial == _activeWantedSerial)
            {
                _activeWantedSerial = Serial.Zero;
                _activeWantedName   = string.Empty;
            }
        }

        public static string GetActiveHuntInfo()
        {
            if (!HasActiveHunt) return string.Empty;
            return _activeHuntName;
        }

        // --------------------------------------------------------
        // BROADCAST HELPERS
        // --------------------------------------------------------

        public static void BroadcastHuntSpawn(string name, string dungeon)
        {
            World.Broadcast(0x44, false,
                $"[World Hunt] {name} has been sighted deep within {dungeon}! " +
                $"Seek it out for glory and reward!");
        }

        public static void BroadcastHuntReminder(string name, string dungeon)
        {
            World.Broadcast(0x44, false,
                $"[World Hunt] {name} still prowls {dungeon}. It has not yet been slain!");
        }

        public static void BroadcastHuntKill(string name, string killerName)
        {
            World.Broadcast(0x44, false,
                $"[World Hunt] {name} has been slain by {killerName}! The hunt is over.");
        }

        public static void BroadcastWantedSpawn(string name, string location)
        {
            World.Broadcast(0x44, false,
                $"[Wanted] {name} has been sighted near {location}. " +
                $"The Hunter's Guild offers a bounty. Bring the head.");
        }

        public static void BroadcastWantedKill(string name, string killerName)
        {
            World.Broadcast(0x44, false,
                $"[Wanted] {name} has been brought to justice by {killerName}!");
        }

        // --------------------------------------------------------
        // TIER REWARD TABLES
        // --------------------------------------------------------

        public static int TierPoints(int tier)
        {
            switch (tier)
            {
                case 1: return 1;
                case 2: return 2;
                case 3: return 4;
                case 4: return 8;
                default: return 1;
            }
        }

        public static int TierTokens(int tier)
        {
            switch (tier)
            {
                case 2: return 1;
                case 3: return 3;
                case 4: return 6;
                default: return 0;
            }
        }

        public static double TierNamedWeaponChance(int tier)
        {
            switch (tier)
            {
                case 1: return 0.05;
                case 2: return 0.12;
                case 3: return 0.20;
                case 4: return 0.30;
                default: return 0.0;
            }
        }

        public static double TierOrbChance(int tier)
        {
            switch (tier)
            {
                case 2: return 0.10;
                case 3: return 0.25;
                case 4: return 0.50;
                default: return 0.0;
            }
        }

        // --------------------------------------------------------
        // CREATURE / NPC FACTORIES
        // --------------------------------------------------------

        private static int PickTier()
        {
            // Weights: T1=40%, T2=30%, T3=20%, T4=10%
            int r = Utility.Random(10);
            if (r < 4) return 1;
            if (r < 7) return 2;
            if (r < 9) return 3;
            return 4;
        }

        private static int PickWantedTier()
        {
            // Cutthroat=50%, Murderer=35%, DreadLord=15%
            int r = Utility.Random(20);
            if (r < 10) return 10;
            if (r < 17) return 11;
            return 12;
        }

        private static HunterCreature CreateCreature(int tier)
        {
            switch (tier)
            {
                case 1:
                    switch (Utility.Random(5))
                    {
                        case 0: return new HunterOgreLord();
                        case 1: return new HunterTrollChief();
                        case 2: return new HunterHarpy();
                        case 3: return new HunterEttinWarlord();
                        default: return new HunterLizardShaman();
                    }
                case 2:
                    switch (Utility.Random(4))
                    {
                        case 0: return new HunterDragon();
                        case 1: return new HunterBalron();
                        case 2: return new HunterLichLord();
                        default: return new HunterDaemon();
                    }
                case 3:
                    switch (Utility.Random(5))
                    {
                        case 0: return new HunterAncientWyrm();
                        case 1: return new HunterPrimevalLich();
                        case 2: return new HunterUndeadLord();
                        case 3: return new HunterBloodDragon();
                        default: return new HunterAbyssalDaemon();
                    }
                case 4:
                    switch (Utility.Random(6))
                    {
                        case 0: return new HunterRikktor();
                        case 1: return new HunterBarracoon();
                        case 2: return new HunterNeira();
                        case 3: return new HunterMephitis();
                        case 4: return new HunterSemidar();
                        default: return new HunterLordOaks();
                    }
                default:
                    return new HunterOgreLord();
            }
        }

        private static BaseWantedNPC CreateWantedNPC(int tier)
        {
            switch (tier)
            {
                case 10: return new WantedCutthroat();
                case 11: return new WantedMurderer();
                default: return new WantedDreadLord();
            }
        }

        // --------------------------------------------------------
        // SPAWN LOCATION TABLES
        // (Approximate Felucca dungeon coordinates)
        // --------------------------------------------------------

        private static readonly HunterSpawnEntry[] Tier1Spawns =
        {
            new HunterSpawnEntry(2497, 921,  0, Map.Felucca, "Covetous"),
            new HunterSpawnEntry(2497, 965,  0, Map.Felucca, "Covetous"),
            new HunterSpawnEntry(1298, 1599, 0, Map.Felucca, "Despise"),
            new HunterSpawnEntry(1350, 1650, 0, Map.Felucca, "Despise"),
            new HunterSpawnEntry(532,  1501, 0, Map.Felucca, "Shame"),
        };

        private static readonly HunterSpawnEntry[] Tier2Spawns =
        {
            new HunterSpawnEntry(2040, 195,  0, Map.Felucca, "Wrong"),
            new HunterSpawnEntry(2100, 250,  0, Map.Felucca, "Wrong"),
            new HunterSpawnEntry(600,  1550, 0, Map.Felucca, "Shame"),
            new HunterSpawnEntry(960,  3155, 0, Map.Felucca, "Destard"),
            new HunterSpawnEntry(2550, 1000, 0, Map.Felucca, "Covetous"),
        };

        private static readonly HunterSpawnEntry[] Tier3Spawns =
        {
            new HunterSpawnEntry(1010, 3200, 0, Map.Felucca, "Destard"),
            new HunterSpawnEntry(1060, 3250, 0, Map.Felucca, "Destard"),
            new HunterSpawnEntry(2600, 1050, 0, Map.Felucca, "Covetous"),
            new HunterSpawnEntry(680,  1600, 0, Map.Felucca, "Shame"),
            new HunterSpawnEntry(1965, 135,  0, Map.Felucca, "Deceit"),
        };

        private static readonly HunterSpawnEntry[] Tier4Spawns =
        {
            new HunterSpawnEntry(4725, 3823, 0, Map.Felucca, "Hythloth"),
            new HunterSpawnEntry(4780, 3870, 0, Map.Felucca, "Hythloth"),
            new HunterSpawnEntry(2010, 170,  0, Map.Felucca, "Deceit"),
            new HunterSpawnEntry(1090, 3300, 0, Map.Felucca, "Destard"),
        };

        private static readonly HunterSpawnEntry[] WantedCutthroatSpawns =
        {
            new HunterSpawnEntry(1368, 1545, 0, Map.Felucca, "the road near Despise"),
            new HunterSpawnEntry(2457, 875,  0, Map.Felucca, "the road near Covetous"),
            new HunterSpawnEntry(505,  1445, 0, Map.Felucca, "the road near Shame"),
        };

        private static readonly HunterSpawnEntry[] WantedMurdererSpawns =
        {
            new HunterSpawnEntry(2005, 148,  0, Map.Felucca, "the road near Wrong"),
            new HunterSpawnEntry(930,  3100, 0, Map.Felucca, "the road near Destard"),
            new HunterSpawnEntry(1950, 110,  0, Map.Felucca, "the road near Deceit"),
        };

        private static readonly HunterSpawnEntry[] WantedDreadLordSpawns =
        {
            new HunterSpawnEntry(4690, 3790, 0, Map.Felucca, "the entrance to Hythloth"),
            new HunterSpawnEntry(1920, 80,   0, Map.Felucca, "the depths near Deceit"),
            new HunterSpawnEntry(1050, 3280, 0, Map.Felucca, "the wilderness near Destard"),
        };

        private static HunterSpawnEntry PickSpawnEntry(int tier)
        {
            HunterSpawnEntry[] pool;
            switch (tier)
            {
                case 1:  pool = Tier1Spawns; break;
                case 2:  pool = Tier2Spawns; break;
                case 3:  pool = Tier3Spawns; break;
                default: pool = Tier4Spawns; break;
            }
            return pool[Utility.Random(pool.Length)];
        }

        private static HunterSpawnEntry PickWantedEntry(int tier)
        {
            HunterSpawnEntry[] pool;
            switch (tier)
            {
                case 10: pool = WantedCutthroatSpawns; break;
                case 11: pool = WantedMurdererSpawns;  break;
                default: pool = WantedDreadLordSpawns; break;
            }
            return pool[Utility.Random(pool.Length)];
        }
    }

    // ============================================================
    // HUNTER SPAWN TIMER — fires every 90-180 minutes
    // ============================================================

    public class HunterSpawnTimer : Timer
    {
        public HunterSpawnTimer()
            : base(TimeSpan.FromMinutes(Utility.RandomMinMax(90, 180)),
                   TimeSpan.FromMinutes(Utility.RandomMinMax(90, 180)))
        {
            Priority = TimerPriority.OneMinute;
        }

        protected override void OnTick()
        {
            HunterSystem.SpawnHunterTarget();
            // Randomise next interval
            Delay    = TimeSpan.FromMinutes(Utility.RandomMinMax(90, 180));
            Interval = Delay;
        }
    }

    // ============================================================
    // WANTED SPAWN TIMER — fires every 4-6 hours
    // ============================================================

    public class WantedSpawnTimer : Timer
    {
        public WantedSpawnTimer()
            : base(TimeSpan.FromMinutes(Utility.RandomMinMax(240, 360)),
                   TimeSpan.FromMinutes(Utility.RandomMinMax(240, 360)))
        {
            Priority = TimerPriority.OneMinute;
        }

        protected override void OnTick()
        {
            HunterSystem.SpawnWantedTarget();
            Delay    = TimeSpan.FromMinutes(Utility.RandomMinMax(240, 360));
            Interval = Delay;
        }
    }
}
