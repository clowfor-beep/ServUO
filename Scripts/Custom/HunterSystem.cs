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
using Server.Commands;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Server.Custom
{
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

        // ---- Active hunt records (up to 3 concurrent) ----

        private sealed class HuntRecord
        {
            public readonly Serial   Serial;
            public readonly string   Name;
            public readonly string   Location;
            public readonly DateTime SpawnTime;

            public HuntRecord(Serial serial, string name, string location)
            {
                Serial    = serial;
                Name      = name;
                Location  = location;
                SpawnTime = DateTime.UtcNow;
            }
        }

        private static readonly List<HuntRecord> _activeHunts  = new List<HuntRecord>();
        private static readonly List<HuntRecord> _activeWanted = new List<HuntRecord>();

        private const int MaxConcurrentHunts  = 5;  // hunt monster targets
        private const int MaxConcurrentWanted = 5;  // wanted PK NPCs

        // Dungeons that are guarded or peaceful — wanted NPCs would be killed
        // by guards the moment they spawn there.
        private static readonly System.Collections.Generic.HashSet<string> _guardedDungeons =
            new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Prism of Light",  // guarded zone
                "Wind",            // mage city — guarded
                "Sanctuary",       // peaceful haven — no aggression
            };

        private static readonly TimeSpan WantedTTL = TimeSpan.FromMinutes(10);

        private static void PruneHunts()  =>
            _activeHunts.RemoveAll(r => { Mobile m = World.FindMobile(r.Serial); return m == null || m.Deleted; });

        private static void PruneWanted()
        {
            _activeWanted.RemoveAll(r =>
            {
                Mobile m = World.FindMobile(r.Serial);
                if (m == null || m.Deleted)
                    return true; // already dead / gone

                // Expire stale wanted NPCs that haven't been killed within the TTL
                if (DateTime.UtcNow - r.SpawnTime >= WantedTTL)
                {
                    m.Delete(); // despawn the old one
                    return true;
                }

                return false;
            });
        }

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

            // Fill all slots immediately on server start
            for (int i = 0; i < MaxConcurrentHunts;  i++) SpawnHunterTarget();
            for (int i = 0; i < MaxConcurrentWanted; i++) SpawnWantedTarget();
        }

        // --------------------------------------------------------
        // PERSISTENCE — save / load
        // --------------------------------------------------------

        private static void OnWorldSave(WorldSaveEventArgs e)
        {
            Persistence.Serialize(SavePath, writer =>
            {
                writer.Write(2); // version

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

                // Active hunts list
                PruneHunts();
                writer.Write(_activeHunts.Count);
                foreach (HuntRecord r in _activeHunts)
                {
                    writer.Write(r.Serial);
                    writer.Write(r.SpawnTime);
                    writer.Write(r.Name);
                    writer.Write(r.Location);
                }

                // Active wanted list
                PruneWanted();
                writer.Write(_activeWanted.Count);
                foreach (HuntRecord r in _activeWanted)
                {
                    writer.Write(r.Serial);
                    writer.Write(r.SpawnTime);
                    writer.Write(r.Name);
                    writer.Write(r.Location);
                }
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

                if (version >= 2)
                {
                    int huntCount = reader.ReadInt();
                    for (int i = 0; i < huntCount; i++)
                    {
                        Serial   serial    = reader.ReadInt();
                        DateTime spawnTime = reader.ReadDateTime();
                        string   name      = reader.ReadString();
                        string   location  = reader.ReadString();
                        _activeHunts.Add(new HuntRecord(serial, name, location));
                    }

                    int wantedCount = reader.ReadInt();
                    for (int i = 0; i < wantedCount; i++)
                    {
                        Serial   serial    = reader.ReadInt();
                        DateTime spawnTime = reader.ReadDateTime();
                        string   name      = reader.ReadString();
                        string   location  = reader.ReadString();
                        _activeWanted.Add(new HuntRecord(serial, name, location));
                    }
                }
                else
                {
                    // v0 / v1 — single-entry format; migrate into lists
                    Serial huntSerial = reader.ReadInt();
                    /*spawnTime*/      reader.ReadDateTime();
                    string huntName   = reader.ReadString();
                    string huntLoc    = version >= 1 ? reader.ReadString() : string.Empty;
                    if (huntSerial != Serial.Zero)
                        _activeHunts.Add(new HuntRecord(huntSerial, huntName, huntLoc));

                    Serial wantedSerial = reader.ReadInt();
                    string wantedName   = reader.ReadString();
                    string wantedLoc    = version >= 1 ? reader.ReadString() : string.Empty;
                    if (wantedSerial != Serial.Zero)
                        _activeWanted.Add(new HuntRecord(wantedSerial, wantedName, wantedLoc));
                }
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

        public static bool HasActiveHunt   { get { PruneHunts();  return _activeHunts .Count > 0; } }
        public static bool HasActiveWanted { get { PruneWanted(); return _activeWanted.Count > 0; } }

        /// <summary>
        /// Returns the top maxCount players by hunter points, sorted descending.
        /// Skips serials that no longer resolve to a mobile.
        /// </summary>
        public static List<(string name, int points, string rank)> GetLeaderboard(int maxCount = 10)
        {
            var result = new List<(string, int, string)>();

            foreach (var kvp in _points)
            {
                if (kvp.Value <= 0) continue;
                Mobile m    = World.FindMobile(kvp.Key);
                string name = m != null ? m.Name : $"<Unknown #{(int)kvp.Key}>";
                result.Add((name, kvp.Value, GetRankTitle(kvp.Value)));
            }

            result.Sort((a, b) => b.Item2.CompareTo(a.Item2));

            if (result.Count > maxCount)
                result.RemoveRange(maxCount, result.Count - maxCount);

            return result;
        }

        /// <summary>Returns a snapshot of active hunter creature targets.</summary>
        public static List<(string name, string location)> GetActiveHunts()
        {
            PruneHunts();
            var list = new List<(string, string)>();
            foreach (HuntRecord r in _activeHunts)
                list.Add((r.Name, r.Location));
            return list;
        }

        /// <summary>Returns a snapshot of active wanted NPC targets.</summary>
        public static List<(string name, string location)> GetActiveWanted()
        {
            PruneWanted();
            var list = new List<(string, string)>();
            foreach (HuntRecord r in _activeWanted)
                list.Add((r.Name, r.Location));
            return list;
        }

        public static void SpawnHunterTarget()
        {
            PruneHunts();
            if (_activeHunts.Count >= MaxConcurrentHunts)  return;

            int tier = PickTier();
            HunterSpawnEntry entry   = PickSpawnEntry(tier);
            HunterCreature creature  = CreateCreature(tier);
            if (creature == null) return;

            Point3D loc = FBZones.GetRandomSpawnPoint(entry.Zone);
            Map     map = FBZones.GetMap(entry.Zone);
            if (loc == Point3D.Zero) { creature.Delete(); return; }
            creature.MoveToWorld(loc, map);

            var record = new HuntRecord(creature.Serial,
                creature.Name.Replace("[Hunted] ", ""), entry.DungeonName);
            _activeHunts.Add(record);

            BroadcastHuntSpawn(record.Name, entry.DungeonName);
            FBEventBus.Fire_HunterTargetSpawned(record.Name, entry.DungeonName);

            // Capture locals for timer closures
            Serial capturedSerial   = creature.Serial;
            string capturedName     = record.Name;
            string capturedLocation = entry.DungeonName;

            Timer.DelayCall(TimeSpan.FromMinutes(10), () =>
            {
                if (HuntRecordExists(_activeHunts, capturedSerial))
                    BroadcastHuntReminder(capturedName, capturedLocation);
            });

            Timer.DelayCall(TimeSpan.FromMinutes(20), () =>
            {
                if (RemoveHuntRecord(_activeHunts, capturedSerial, out Mobile target))
                {
                    target?.Delete();
                    World.Broadcast(0x22, false,
                        $"[World Hunt] {capturedName} has fled into the darkness. The hunt is over.");
                }
            });
        }

        public static void SpawnWantedTarget()
        {
            PruneWanted();
            if (_activeWanted.Count >= MaxConcurrentWanted) return;

            int wantedTier = PickWantedTier();
            BaseWantedNPC npc = CreateWantedNPC(wantedTier);
            if (npc == null) return;

            // Pick a random dungeon — skip guarded / peaceful zones where guards
            // would kill the wanted NPC immediately on spawn.
            var dungeons = Server.Items.AtlasGump.Dungeons;
            AtlasLocation dungeon;
            int pickAttempts = 0;
            do { dungeon = dungeons[Utility.Random(dungeons.Count)]; }
            while (_guardedDungeons.Contains(dungeon.Name) && ++pickAttempts < 50);

            // Find a valid tile near the dungeon entrance with a small random jitter
            Point3D loc = Point3D.Zero;
            for (int attempt = 0; attempt < 10; attempt++)
            {
                int x = dungeon.X + Utility.RandomMinMax(-6, 6);
                int y = dungeon.Y + Utility.RandomMinMax(-6, 6);
                int z = dungeon.Map.GetAverageZ(x, y);
                if (dungeon.Map.CanSpawnMobile(x, y, z))
                {
                    loc = new Point3D(x, y, z);
                    break;
                }
            }
            if (loc == Point3D.Zero) loc = new Point3D(dungeon.X, dungeon.Y, dungeon.Z);

            npc.MoveToWorld(loc, dungeon.Map);

            var record = new HuntRecord(npc.Serial,
                npc.Name.Replace("[Wanted] ", ""), dungeon.Name);
            _activeWanted.Add(record);

            BroadcastWantedSpawn(record.Name, dungeon.Name);
            FBEventBus.Fire_WantedNPCSpawned(record.Name, dungeon.Name);

            Serial capturedSerial = npc.Serial;
            string capturedName   = record.Name;

            // 10-minute expiry — wanted NPCs are fast-moving targets outside dungeons
            Timer.DelayCall(TimeSpan.FromMinutes(10), () =>
            {
                if (RemoveHuntRecord(_activeWanted, capturedSerial, out Mobile target))
                {
                    target?.Delete();
                    World.Broadcast(0x22, false,
                        $"[Wanted] {capturedName} has slipped away. The bounty has expired.");
                }
            });
        }

        public static void OnHunterKilled(HunterCreature creature, Mobile killer)
        {
            RemoveHuntRecord(_activeHunts, creature.Serial, out _);
            FBEventBus.Fire_HunterTargetKilled(creature, killer);
        }

        public static void OnWantedKilled(BaseWantedNPC npc, Mobile killer)
        {
            RemoveHuntRecord(_activeWanted, npc.Serial, out _);
            FBEventBus.Fire_WantedNPCKilled(npc, killer);
        }

        /// <summary>
        /// Deletes every active hunt and wanted NPC mob, clears both tracking lists.
        /// Used by [resethunts to unstick the system when mobs get spawned inside walls
        /// or otherwise end up in a bad state.
        /// Returns the number of mobs that were actually deleted.
        /// </summary>
        public static int ResetAllHunts()
        {
            int deleted = 0;

            foreach (HuntRecord r in _activeHunts)
            {
                Mobile m = World.FindMobile(r.Serial);
                if (m != null && !m.Deleted) { m.Delete(); deleted++; }
            }
            _activeHunts.Clear();

            foreach (HuntRecord r in _activeWanted)
            {
                Mobile m = World.FindMobile(r.Serial);
                if (m != null && !m.Deleted) { m.Delete(); deleted++; }
            }
            _activeWanted.Clear();

            return deleted;
        }

        // Returns first active hunt info string (used by old single-line gump callers)
        public static string GetActiveHuntInfo()
        {
            PruneHunts();
            if (_activeHunts.Count == 0) return string.Empty;
            HuntRecord r = _activeHunts[0];
            return r.Location.Length > 0 ? $"{r.Name} — {r.Location}" : r.Name;
        }

        public static string GetActiveWantedInfo()
        {
            PruneWanted();
            if (_activeWanted.Count == 0) return string.Empty;
            HuntRecord r = _activeWanted[0];
            return r.Location.Length > 0 ? $"{r.Name} — {r.Location}" : r.Name;
        }

        // Returns one info string per active entry — for gump and compass
        public static List<string> GetActiveHuntInfoList()
        {
            PruneHunts();
            var list = new List<string>(_activeHunts.Count);
            foreach (HuntRecord r in _activeHunts)
                list.Add(r.Location.Length > 0 ? $"{r.Name} — {r.Location}" : r.Name);
            return list;
        }

        public static List<string> GetActiveWantedInfoList()
        {
            PruneWanted();
            var list = new List<string>(_activeWanted.Count);
            foreach (HuntRecord r in _activeWanted)
                list.Add(r.Location.Length > 0 ? $"{r.Name} — {r.Location}" : r.Name);
            return list;
        }

        // ---- Compass / GM-command support ----

        public struct ActiveTargetInfo
        {
            public string   Name;
            public string   Location;
            public Point3D  Position;
            public Map      Map;
        }

        public static List<ActiveTargetInfo> GetAllActiveHunts()
        {
            PruneHunts();
            var result = new List<ActiveTargetInfo>(_activeHunts.Count);
            foreach (HuntRecord r in _activeHunts)
            {
                Mobile m = World.FindMobile(r.Serial);
                if (m != null)
                    result.Add(new ActiveTargetInfo
                        { Name = r.Name, Location = r.Location, Position = m.Location, Map = m.Map });
            }
            return result;
        }

        public static List<ActiveTargetInfo> GetAllActiveWanted()
        {
            PruneWanted();
            var result = new List<ActiveTargetInfo>(_activeWanted.Count);
            foreach (HuntRecord r in _activeWanted)
            {
                Mobile m = World.FindMobile(r.Serial);
                if (m != null)
                    result.Add(new ActiveTargetInfo
                        { Name = r.Name, Location = r.Location, Position = m.Location, Map = m.Map });
            }
            return result;
        }

        // Kept for backward-compat (compass / old callers) — returns first entry
        public static string   GetActiveHuntTargetName()     { PruneHunts();  return _activeHunts .Count > 0 ? _activeHunts [0].Name     : string.Empty; }
        public static string   GetActiveHuntTargetLocation() { PruneHunts();  return _activeHunts .Count > 0 ? _activeHunts [0].Location  : string.Empty; }
        public static string   GetActiveWantedTargetName()   { PruneWanted(); return _activeWanted.Count > 0 ? _activeWanted[0].Name     : string.Empty; }
        public static string   GetActiveWantedTargetLocation(){ PruneWanted(); return _activeWanted.Count > 0 ? _activeWanted[0].Location  : string.Empty; }

        public static Point3D GetActiveHuntPosition()
        {
            PruneHunts();
            if (_activeHunts.Count == 0) return Point3D.Zero;
            Mobile m = World.FindMobile(_activeHunts[0].Serial);
            return m != null ? m.Location : Point3D.Zero;
        }

        public static Map GetActiveHuntMap()
        {
            PruneHunts();
            if (_activeHunts.Count == 0) return null;
            Mobile m = World.FindMobile(_activeHunts[0].Serial);
            return m?.Map;
        }

        public static Point3D GetActiveWantedPosition()
        {
            PruneWanted();
            if (_activeWanted.Count == 0) return Point3D.Zero;
            Mobile m = World.FindMobile(_activeWanted[0].Serial);
            return m != null ? m.Location : Point3D.Zero;
        }

        public static Map GetActiveWantedMap()
        {
            PruneWanted();
            if (_activeWanted.Count == 0) return null;
            Mobile m = World.FindMobile(_activeWanted[0].Serial);
            return m?.Map;
        }

        // ---- List helpers ----

        private static bool HuntRecordExists(List<HuntRecord> list, Serial serial)
        {
            foreach (HuntRecord r in list)
                if (r.Serial == serial) return true;
            return false;
        }

        // Removes the record and outputs the live mobile (if still alive). Returns true if found.
        private static bool RemoveHuntRecord(List<HuntRecord> list, Serial serial, out Mobile mobile)
        {
            mobile = null;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Serial == serial)
                {
                    Mobile m = World.FindMobile(serial);
                    if (m != null && !m.Deleted && m.Alive)
                        mobile = m;
                    list.RemoveAt(i);
                    return true;
                }
            }
            return false;
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
                case 1: return 1;
                case 2: return 2;
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
        // All coordinates live in FBZones.cs — single source of truth.
        // --------------------------------------------------------

        private static HunterSpawnEntry PickSpawnEntry(int tier)
        {
            HunterSpawnEntry[] pool;
            switch (tier)
            {
                case 1:  pool = FBZones.HunterTier1Spawns; break;
                case 2:  pool = FBZones.HunterTier2Spawns; break;
                case 3:  pool = FBZones.HunterTier3Spawns; break;
                default: pool = FBZones.HunterTier4Spawns; break;
            }
            return pool[Utility.Random(pool.Length)];
        }

        private static HunterSpawnEntry PickWantedEntry(int tier)
        {
            HunterSpawnEntry[] pool;
            switch (tier)
            {
                case 10: pool = FBZones.WantedCutthroatSpawns; break;
                case 11: pool = FBZones.WantedMurdererSpawns;  break;
                default: pool = FBZones.WantedDreadLordSpawns; break;
            }
            return pool[Utility.Random(pool.Length)];
        }
    }

    // ============================================================
    // GM COMMANDS
    // ============================================================

    public static class HunterCommands
    {
        public static void Initialize()
        {
            CommandSystem.Register("gothunt",    AccessLevel.GameMaster, OnGoHunt);
            CommandSystem.Register("gowanted",   AccessLevel.GameMaster, OnGoWanted);
            CommandSystem.Register("resethunts", AccessLevel.GameMaster, OnResetHunts);
        }

        private static void OnGoHunt(CommandEventArgs e)
        {
            Mobile gm = e.Mobile;
            var hunts = HunterSystem.GetAllActiveHunts();

            if (hunts.Count == 0)
            {
                gm.SendMessage(0x22, "There are no active hunt targets right now.");
                return;
            }

            // If an index argument is given (gothunt 2), go to that slot; else go to #1
            int idx = 0;
            if (e.Arguments.Length > 0 && int.TryParse(e.Arguments[0], out int arg))
                idx = Math.Max(0, Math.Min(arg - 1, hunts.Count - 1));

            var target = hunts[idx];
            gm.MoveToWorld(target.Position, target.Map);
            gm.SendMessage(0x35, $"Teleported to {target.Name} in {target.Location}. " +
                $"({hunts.Count} hunt{(hunts.Count > 1 ? "s" : "")} active — use [gothunt 1/2/3 to pick)");
        }

        private static void OnGoWanted(CommandEventArgs e)
        {
            Mobile gm = e.Mobile;
            var wanted = HunterSystem.GetAllActiveWanted();

            if (wanted.Count == 0)
            {
                gm.SendMessage(0x22, "There are no active Wanted NPCs right now.");
                return;
            }

            int idx = 0;
            if (e.Arguments.Length > 0 && int.TryParse(e.Arguments[0], out int arg))
                idx = Math.Max(0, Math.Min(arg - 1, wanted.Count - 1));

            var target = wanted[idx];
            gm.MoveToWorld(target.Position, target.Map);
            gm.SendMessage(0x35, $"Teleported to {target.Name} in {target.Location}. " +
                $"({wanted.Count} wanted NPC{(wanted.Count > 1 ? "s" : "")} active — use [gowanted 1/2/3 to pick)");
        }

        private static void OnResetHunts(CommandEventArgs e)
        {
            int deleted = HunterSystem.ResetAllHunts();
            e.Mobile.SendMessage(0x35, $"Hunt reset complete — {deleted} mob(s) deleted, both lists cleared.");
        }
    }

    // ============================================================
    // HUNTER SPAWN TIMER — fires every 10-15 minutes
    // ============================================================

    public class HunterSpawnTimer : Timer
    {
        public HunterSpawnTimer()
            : base(TimeSpan.FromMinutes(Utility.RandomMinMax(10, 15)),
                   TimeSpan.FromMinutes(Utility.RandomMinMax(10, 15)))
        {
            Priority = TimerPriority.OneMinute;
        }

        protected override void OnTick()
        {
            HunterSystem.SpawnHunterTarget();
            // Randomise next interval
            Delay    = TimeSpan.FromMinutes(Utility.RandomMinMax(10, 15));
            Interval = Delay;
        }
    }

    // ============================================================
    // WANTED SPAWN TIMER — fires every 20-30 minutes
    // ============================================================

    public class WantedSpawnTimer : Timer
    {
        public WantedSpawnTimer()
            : base(TimeSpan.FromMinutes(Utility.RandomMinMax(10, 13)),
                   TimeSpan.FromMinutes(Utility.RandomMinMax(10, 13)))
        {
            Priority = TimerPriority.OneMinute;
        }

        protected override void OnTick()
        {
            HunterSystem.SpawnWantedTarget();
            Delay    = TimeSpan.FromMinutes(Utility.RandomMinMax(10, 13));
            Interval = Delay;
        }
    }
}
