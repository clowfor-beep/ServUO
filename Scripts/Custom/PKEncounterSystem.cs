// ============================================================
// PKEncounterSystem.cs
// Scripts/Custom/PKEncounterSystem.cs
//
// Unified PK encounter system — replaces GraveyardPKEncounter.cs
//
// Covers all dungeon zones across all facets:
//   Felucca, Ilshenar, Malas, Tokuno, TerMur
//
// Features:
//   - Per-zone spawn chance (tunable per dungeon floor)
//   - 5-minute check interval, 30-minute cooldown per player
//   - Targets the player who has been in the dungeon the longest
//   - PK stays on its spawn floor — gives up if target changes floor
//   - 2-minute wait state before despawn on floor change
//   - Portal moongate VFX + sound on spawn
//   - Max 1 active PK per player at a time
//   - GMs and staff are never targeted
// ============================================================

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Server.Custom
{
    // -------------------------------------------------------
    // Tier enum
    // -------------------------------------------------------
    public enum PKTier { Newbie, Advanced, Expert }

    // -------------------------------------------------------
    // Zone definition
    // -------------------------------------------------------
    public class PKZone
    {
        public string        Name;
        public string        DungeonKey;   // groups floors of the same dungeon
        public Rectangle2D[] Rects;
        public Map           Map;
        public int           MinZ;
        public int           MaxZ;
        public PKTier        Tier;
        public double        SpawnChance;  // 0.0 – 1.0

        public PKZone(string name, string dungeonKey, Map map,
                      int minZ, int maxZ, PKTier tier, double chance,
                      params Rectangle2D[] rects)
        {
            Name        = name;
            DungeonKey  = dungeonKey;
            Map         = map;
            MinZ        = minZ;
            MaxZ        = maxZ;
            Tier        = tier;
            SpawnChance = chance;
            Rects       = rects;
        }

        public bool Contains(Mobile m)
        {
            if (m.Map != Map)              return false;
            if (m.Z < MinZ || m.Z > MaxZ)  return false;
            foreach (var r in Rects)
                if (r.Contains(m.Location)) return true;
            return false;
        }
    }

    // -------------------------------------------------------
    // Main system
    // -------------------------------------------------------
    public static class PKEncounterSystem
    {
        // ── Public zone list ─────────────────────────────────────────────
        public static readonly List<PKZone> AllZones = new List<PKZone>();

        // ── Per-player state ─────────────────────────────────────────────
        // Last time a PK was spawned on this player
        private static readonly Dictionary<PlayerMobile, DateTime> Cooldowns
            = new Dictionary<PlayerMobile, DateTime>();

        // Currently active PK hunting this player
        internal static readonly Dictionary<PlayerMobile, BasePKNPC> ActivePKs
            = new Dictionary<PlayerMobile, BasePKNPC>();

        // When each player first entered their current dungeon (DungeonKey)
        private static readonly Dictionary<PlayerMobile, (string Key, DateTime When)> DungeonEntry
            = new Dictionary<PlayerMobile, (string, DateTime)>();

        // Which zone a player was last seen in (for entry-time tracking)
        private static readonly Dictionary<PlayerMobile, PKZone> LastSeenZone
            = new Dictionary<PlayerMobile, PKZone>();

        // ── Timing constants ─────────────────────────────────────────────
        private static readonly TimeSpan CooldownDuration  = TimeSpan.FromMinutes(30.0);
        private static readonly TimeSpan CheckInterval     = TimeSpan.FromMinutes(5.0);
        internal static readonly TimeSpan WaitBeforeDespawn = TimeSpan.FromMinutes(2.0);

        // ── Startup ───────────────────────────────────────────────────────
        public static void Initialize()
        {
            RegisterZones();
            new PKCheckTimer().Start();

            EventSink.Logout      += e => ClearPlayer(e.Mobile as PlayerMobile);
            EventSink.PlayerDeath += e => ClearPlayer(e.Mobile as PlayerMobile);
        }

        private static void ClearPlayer(PlayerMobile pm)
        {
            if (pm == null) return;
            DungeonEntry.Remove(pm);
            LastSeenZone.Remove(pm);
        }

        // ── 5-minute zone check ───────────────────────────────────────────
        private class PKCheckTimer : Timer
        {
            public PKCheckTimer() : base(CheckInterval, CheckInterval)
            {
                Priority = TimerPriority.OneMinute;
            }

            protected override void OnTick()
            {
                // Build a map of zone → list of eligible players
                var zoneOccupants = new Dictionary<PKZone, List<PlayerMobile>>();

                foreach (NetState ns in NetState.Instances)
                {
                    if (!(ns.Mobile is PlayerMobile pm)) continue;
                    if (pm.AccessLevel > AccessLevel.Player) continue;
                    if (!pm.Alive) continue;

                    PKZone zone = AllZones.FirstOrDefault(z => z.Contains(pm));

                    if (zone == null)
                    {
                        // Player left all zones — clear dungeon entry tracking
                        if (LastSeenZone.ContainsKey(pm))
                        {
                            LastSeenZone.Remove(pm);
                            DungeonEntry.Remove(pm);
                        }
                        continue;
                    }

                    // Track dungeon entry time
                    // Reset when the player moves to a different dungeon entirely
                    if (DungeonEntry.TryGetValue(pm, out var entry))
                    {
                        if (entry.Key != zone.DungeonKey)
                        {
                            // Moved to a different dungeon — reset
                            DungeonEntry[pm]  = (zone.DungeonKey, DateTime.UtcNow);
                        }
                        // Same dungeon, different floor — keep original entry time
                    }
                    else
                    {
                        DungeonEntry[pm] = (zone.DungeonKey, DateTime.UtcNow);
                    }

                    LastSeenZone[pm] = zone;

                    if (!zoneOccupants.ContainsKey(zone))
                        zoneOccupants[zone] = new List<PlayerMobile>();
                    zoneOccupants[zone].Add(pm);
                }

                // For each occupied zone, roll for an encounter
                foreach (var kvp in zoneOccupants)
                {
                    PKZone zone    = kvp.Key;
                    var    players = kvp.Value;

                    // Roll spawn chance
                    if (Utility.RandomDouble() >= zone.SpawnChance)
                        continue;

                    // Filter: no active PK, not on cooldown
                    var eligible = players.Where(pm =>
                    {
                        if (ActivePKs.TryGetValue(pm, out var existing)
                            && existing != null && !existing.Deleted)
                            return false;

                        if (Cooldowns.TryGetValue(pm, out var last)
                            && DateTime.UtcNow < last + CooldownDuration)
                            return false;

                        return true;
                    }).ToList();

                    if (eligible.Count == 0) continue;

                    // Target the player who has been in this dungeon the longest
                    PlayerMobile target = eligible
                        .OrderBy(pm =>
                            DungeonEntry.TryGetValue(pm, out var e) ? e.When : DateTime.UtcNow)
                        .First();

                    TriggerEncounter(target, zone);
                }
            }
        }

        // ── Trigger encounter ─────────────────────────────────────────────
        private static void TriggerEncounter(PlayerMobile target, PKZone zone)
        {
            // Lock in the cooldown immediately to prevent double-triggers
            Cooldowns[target] = DateTime.UtcNow;

            Point3D spawnPoint = FindSpawnPoint(target, zone);
            if (spawnPoint == Point3D.Zero) return;

            // Portal opening VFX
            Effects.SendLocationParticles(
                EffectItem.Create(spawnPoint, zone.Map, EffectItem.DefaultDuration),
                0x3728, 10, 14, 5042);
            Effects.PlaySound(spawnPoint, zone.Map, 0x20E);

            Timer.DelayCall(TimeSpan.FromSeconds(1.2), () =>
            {
                if (target == null || target.Deleted || !target.Alive) return;

                // Second VFX burst as NPC steps through
                Effects.SendLocationParticles(
                    EffectItem.Create(spawnPoint, zone.Map, EffectItem.DefaultDuration),
                    0x3728, 10, 10, 2023);
                Effects.PlaySound(spawnPoint, zone.Map, 0x1FE);

                BasePKNPC pk = CreateForTier(zone.Tier);
                pk.MoveToWorld(spawnPoint, zone.Map);
                pk.InitEncounter(target);

                ActivePKs[target] = pk;

                target.SendMessage(0x26, "A dark figure steps through a shimmering portal...");

                // Start floor monitor for this encounter
                new FloorMonitorTimer(pk, target, zone).Start();
            });
        }

        // ── Floor change monitor ──────────────────────────────────────────
        // Fires every 3 seconds. If the target leaves the spawn zone,
        // the PK enters a wait state and despawns after 2 minutes.
        private class FloorMonitorTimer : Timer
        {
            private readonly BasePKNPC     _pk;
            private readonly PlayerMobile  _target;
            private readonly PKZone        _spawnZone;
            private bool                   _waiting;
            private DateTime               _waitSince;

            public FloorMonitorTimer(BasePKNPC pk, PlayerMobile target, PKZone spawnZone)
                : base(TimeSpan.FromSeconds(3.0), TimeSpan.FromSeconds(3.0))
            {
                _pk        = pk;
                _target    = target;
                _spawnZone = spawnZone;
                Priority   = TimerPriority.TwoFiftyMS;
            }

            protected override void OnTick()
            {
                // PK was killed or deleted — clean up and stop
                if (_pk == null || _pk.Deleted)
                {
                    ActivePKs.Remove(_target);
                    Stop();
                    return;
                }

                // Target gone — stop monitoring
                if (_target == null || _target.Deleted)
                {
                    Stop();
                    return;
                }

                if (_waiting)
                {
                    // In wait state — despawn after 2 minutes
                    if (DateTime.UtcNow >= _waitSince + WaitBeforeDespawn)
                    {
                        ActivePKs.Remove(_target);
                        _pk.Delete();
                        Stop();
                    }
                    return;
                }

                // Check if target has left the spawn zone (changed floor)
                if (!_spawnZone.Contains(_target))
                {
                    _pk.Combatant = null;  // stop chasing
                    _waiting      = true;
                    _waitSince    = DateTime.UtcNow;
                }
            }
        }

        // ── Spawn point finder ────────────────────────────────────────────
        private static Point3D FindSpawnPoint(PlayerMobile target, PKZone zone)
        {
            Map map = zone.Map;

            for (int attempt = 0; attempt < 20; attempt++)
            {
                int dx = Utility.RandomMinMax(-5, 5);
                int dy = Utility.RandomMinMax(-5, 5);

                // Keep at least 3 tiles away
                if (Math.Abs(dx) < 3 && Math.Abs(dy) < 3) continue;

                int x = target.X + dx;
                int y = target.Y + dy;

                // Must be inside one of this zone's rects
                bool inZone = false;
                foreach (var r in zone.Rects)
                    if (r.Contains(new Point2D(x, y))) { inZone = true; break; }
                if (!inZone) continue;

                int z     = map.GetAverageZ(x, y);
                Point3D p = new Point3D(x, y, z);

                if (map.CanSpawnMobile(p) && target.InLOS(p))
                    return p;
            }

            // Fallback: right next to the player — use GetAverageZ so the NPC
            // lands on the actual terrain surface rather than potentially underground.
            int fz = map.GetAverageZ(target.X + 2, target.Y);
            return new Point3D(target.X + 2, target.Y, fz != 0 ? fz : target.Z);
        }

        // ── Tier factory ──────────────────────────────────────────────────
        private static BasePKNPC CreateForTier(PKTier tier)
        {
            switch (tier)
            {
                case PKTier.Advanced: return CreateRandomAdvanced();
                case PKTier.Expert:   return CreateRandomExpert();
                default:              return CreateRandomNewbie();
            }
        }

        private static BasePKNPC CreateRandomNewbie()
        {
            switch (Utility.Random(7))
            {
                case 0:  return new ClassicDexxerNewbie();
                case 1:  return new PureMageNewbie();
                case 2:  return new NecroMageNewbie();
                case 3:  return new NinjaDexxerNewbie();
                case 4:  return new PaladinNewbie();
                case 5:  return new ArcherNewbie();
                default: return new SampireNewbie();
            }
        }

        private static BasePKNPC CreateRandomAdvanced()
        {
            switch (Utility.Random(7))
            {
                case 0:  return new ClassicDexxerAdvanced();
                case 1:  return new PureMageAdvanced();
                case 2:  return new NecroMageAdvanced();
                case 3:  return new NinjaDexxerAdvanced();
                case 4:  return new PaladinAdvanced();
                case 5:  return new ArcherAdvanced();
                default: return new SampireAdvanced();
            }
        }

        private static BasePKNPC CreateRandomExpert()
        {
            switch (Utility.Random(7))
            {
                case 0:  return new ClassicDexxerExpert();
                case 1:  return new PureMageExpert();
                case 2:  return new NecroMageExpert();
                case 3:  return new NinjaDexxerExpert();
                case 4:  return new PaladinExpert();
                case 5:  return new ArcherExpert();
                default: return new SampireExpert();
            }
        }

        // ── Public entry point for [pktest ───────────────────────────────
        public static void ForceEncounter(PlayerMobile target, PKTier? forceTier = null)
        {
            PKZone zone = AllZones.FirstOrDefault(z => z.Contains(target));

            if (zone == null)
            {
                // Not in a registered zone — build a temporary zone centred on the
                // player using their actual map and Z, so FindSpawnPoint can always
                // find a valid tile nearby.  Covers any facet / surface / dungeon.
                PKTier tier = forceTier ?? PKTier.Newbie;
                zone = new PKZone("Force Spawn", "Force", target.Map,
                    target.Z - 10, target.Z + 20, tier, 1.0,
                    new Rectangle2D(target.X - 8, target.Y - 8, 16, 16));
                TriggerEncounter(target, zone);
                return;
            }

            if (forceTier.HasValue && forceTier.Value != zone.Tier)
            {
                // Build a temporary zone with the forced tier, same geometry
                zone = new PKZone(zone.Name, zone.DungeonKey, zone.Map,
                    zone.MinZ, zone.MaxZ, forceTier.Value, zone.SpawnChance,
                    zone.Rects);
            }

            TriggerEncounter(target, zone);
        }

        // ── Zone registration ─────────────────────────────────────────────
        // Coordinates sourced from Data/Regions.xml.
        // Z splits between floors are approximate — tune with [where in-game.
        // SpawnChance: surface 8%, L1 10%, mid 15%, deep 18%, end-game 20%
        private static void RegisterZones()
        {
            // ─────────────────────────────────────────────────────────────
            // NEWBIE ZONES
            // ─────────────────────────────────────────────────────────────

            // Surface — Felucca
            AllZones.Add(new PKZone("Britain Graveyard", "Graveyard", Map.Felucca,
                -5, 25, PKTier.Newbie, 0.08,
                new Rectangle2D(1333, 1441, 84, 82)));

            AllZones.Add(new PKZone("Orc Cave", "OrcCave", Map.Felucca,
                -20, 20, PKTier.Newbie, 0.08,
                new Rectangle2D(5281, 1283, 92, 103),
                new Rectangle2D(5267, 1955, 97, 91),
                new Rectangle2D(5127, 1941, 37, 83)));

            // Classic dungeons L1 — Felucca
            AllZones.Add(new PKZone("Covetous L1", "Covetous", Map.Felucca,
                20, 128, PKTier.Newbie, 0.10,
                new Rectangle2D(5376, 1793, 201, 255)));

            AllZones.Add(new PKZone("Deceit L1", "Deceit", Map.Felucca,
                20, 128, PKTier.Newbie, 0.10,
                new Rectangle2D(5122, 518, 248, 252)));

            AllZones.Add(new PKZone("Despise L1", "Despise", Map.Felucca,
                40, 128, PKTier.Newbie, 0.10,
                new Rectangle2D(5377, 516, 254, 506)));

            AllZones.Add(new PKZone("Destard L1", "Destard", Map.Felucca,
                -5, 25, PKTier.Newbie, 0.10,
                new Rectangle2D(5120, 770, 251, 258)));

            AllZones.Add(new PKZone("Hythloth L1", "Hythloth", Map.Felucca,
                20, 128, PKTier.Newbie, 0.10,
                new Rectangle2D(5898, 2, 238, 244)));

            AllZones.Add(new PKZone("Shame L1", "Shame", Map.Felucca,
                20, 128, PKTier.Newbie, 0.10,
                new Rectangle2D(5377, 2, 257, 260),
                new Rectangle2D(5635, 2, 260, 124)));

            AllZones.Add(new PKZone("Wrong L1", "Wrong", Map.Felucca,
                -5, 25, PKTier.Newbie, 0.10,
                new Rectangle2D(5633, 511, 253, 510)));

            // Ilshenar — easier dungeons
            AllZones.Add(new PKZone("Rock Dungeon", "RockDungeon", Map.Ilshenar,
                -20, 20, PKTier.Newbie, 0.10,
                new Rectangle2D(2176, 288, 24, 40),
                new Rectangle2D(2088, 8, 160, 176)));

            AllZones.Add(new PKZone("Spectre Dungeon", "SpectreDungeon", Map.Ilshenar,
                -30, 20, PKTier.Newbie, 0.10,
                new Rectangle2D(1936, 1000, 96, 120)));

            AllZones.Add(new PKZone("Ankh Dungeon L1", "AnkhDungeon", Map.Ilshenar,
                -5, 20, PKTier.Newbie, 0.10,
                new Rectangle2D(0, 1248, 176, 344)));

            // ─────────────────────────────────────────────────────────────
            // ADVANCED ZONES
            // ─────────────────────────────────────────────────────────────

            // Classic dungeons L2-3 — Felucca
            AllZones.Add(new PKZone("Covetous L2-3", "Covetous", Map.Felucca,
                -40, 20, PKTier.Advanced, 0.15,
                new Rectangle2D(5376, 1793, 201, 255)));

            AllZones.Add(new PKZone("Deceit L2-3", "Deceit", Map.Felucca,
                -40, 20, PKTier.Advanced, 0.15,
                new Rectangle2D(5122, 518, 248, 252)));

            AllZones.Add(new PKZone("Despise L2-3", "Despise", Map.Felucca,
                -20, 40, PKTier.Advanced, 0.15,
                new Rectangle2D(5377, 516, 254, 506)));

            AllZones.Add(new PKZone("Destard L2", "Destard", Map.Felucca,
                -40, -5, PKTier.Advanced, 0.15,
                new Rectangle2D(5120, 770, 251, 258)));

            AllZones.Add(new PKZone("Hythloth L2-3", "Hythloth", Map.Felucca,
                -40, 20, PKTier.Advanced, 0.15,
                new Rectangle2D(5898, 2, 238, 244)));

            AllZones.Add(new PKZone("Shame L2-3", "Shame", Map.Felucca,
                -40, 20, PKTier.Advanced, 0.15,
                new Rectangle2D(5377, 2, 257, 260),
                new Rectangle2D(5635, 2, 260, 124)));

            AllZones.Add(new PKZone("Wrong L2-3", "Wrong", Map.Felucca,
                -40, -5, PKTier.Advanced, 0.15,
                new Rectangle2D(5633, 511, 253, 510)));

            // Standalone mid-tier — Felucca
            AllZones.Add(new PKZone("Khaldun", "Khaldun", Map.Felucca,
                -128, 128, PKTier.Advanced, 0.15,
                new Rectangle2D(5381, 1284, 247, 225)));

            AllZones.Add(new PKZone("Fire Dungeon", "Fire", Map.Felucca,
                -128, 128, PKTier.Advanced, 0.15,
                new Rectangle2D(5635, 1285, 245, 235)));

            AllZones.Add(new PKZone("Ice Dungeon", "Ice", Map.Felucca,
                -128, 128, PKTier.Advanced, 0.15,
                new Rectangle2D(5668, 130, 220, 138),
                new Rectangle2D(5800, 319, 63, 65),
                new Rectangle2D(5654, 300, 54, 40)));

            AllZones.Add(new PKZone("Terathan Keep", "TerathanKeep", Map.Felucca,
                -128, 128, PKTier.Advanced, 0.15,
                new Rectangle2D(5404, 3099, 77, 68),
                new Rectangle2D(5120, 1530, 254, 258)));

            AllZones.Add(new PKZone("Blackthorn Dungeon", "Blackthorn", Map.Felucca,
                -128, 128, PKTier.Advanced, 0.15,
                new Rectangle2D(6151, 2301, 413, 539)));

            AllZones.Add(new PKZone("Misc Dungeons", "MiscDungeons", Map.Felucca,
                -128, 128, PKTier.Advanced, 0.15,
                new Rectangle2D(5886, 1281, 257, 254)));

            // Ilshenar — mid-tier
            AllZones.Add(new PKZone("Spider Cave", "SpiderCave", Map.Ilshenar,
                -40, 20, PKTier.Advanced, 0.15,
                new Rectangle2D(1752, 952, 112, 48),
                new Rectangle2D(1480, 864, 48, 32)));

            AllZones.Add(new PKZone("Blood Dungeon", "BloodDungeon", Map.Ilshenar,
                -40, 20, PKTier.Advanced, 0.15,
                new Rectangle2D(2032, 808, 168, 256)));

            AllZones.Add(new PKZone("Wisp Dungeon", "WispDungeon", Map.Ilshenar,
                -40, 20, PKTier.Advanced, 0.15,
                new Rectangle2D(616,  1480, 88,  96),
                new Rectangle2D(816,  1448, 96, 136),
                new Rectangle2D(912,  1456, 112, 128)));

            AllZones.Add(new PKZone("Ankh Dungeon L2", "AnkhDungeon", Map.Ilshenar,
                -40, -5, PKTier.Advanced, 0.15,
                new Rectangle2D(0, 1248, 176, 344)));

            AllZones.Add(new PKZone("Exodus Entry", "ExodusDungeon", Map.Ilshenar,
                -10, 20, PKTier.Advanced, 0.15,
                new Rectangle2D(1832, 16, 248, 200)));

            // Malas — mid-tier
            AllZones.Add(new PKZone("Bedlam", "Bedlam", Map.Malas,
                -128, 128, PKTier.Advanced, 0.15,
                new Rectangle2D(80, 1590, 130, 100)));

            AllZones.Add(new PKZone("Labyrinth", "Labyrinth", Map.Malas,
                -128, 128, PKTier.Advanced, 0.15,
                new Rectangle2D(255, 1791, 256, 256)));

            // Tokuno
            AllZones.Add(new PKZone("Yomotsu Mines", "YomotsuMines", Map.Tokuno,
                -128, 128, PKTier.Advanced, 0.15,
                new Rectangle2D(0, 0, 129, 129)));

            AllZones.Add(new PKZone("Fan Dancer's Dojo", "FanDancerDojo", Map.Tokuno,
                -128, 128, PKTier.Advanced, 0.15,
                new Rectangle2D(40, 320, 170, 400)));

            // ─────────────────────────────────────────────────────────────
            // EXPERT ZONES
            // ─────────────────────────────────────────────────────────────

            // Classic dungeons L4+ — Felucca
            AllZones.Add(new PKZone("Covetous L4+", "Covetous", Map.Felucca,
                -128, -40, PKTier.Expert, 0.18,
                new Rectangle2D(5376, 1793, 201, 255)));

            AllZones.Add(new PKZone("Deceit L4+", "Deceit", Map.Felucca,
                -128, -40, PKTier.Expert, 0.18,
                new Rectangle2D(5122, 518, 248, 252)));

            AllZones.Add(new PKZone("Despise L4+", "Despise", Map.Felucca,
                -128, -20, PKTier.Expert, 0.18,
                new Rectangle2D(5377, 516, 254, 506)));

            AllZones.Add(new PKZone("Destard L3+", "Destard", Map.Felucca,
                -128, -40, PKTier.Expert, 0.18,
                new Rectangle2D(5120, 770, 251, 258)));

            AllZones.Add(new PKZone("Hythloth L4+", "Hythloth", Map.Felucca,
                -128, -40, PKTier.Expert, 0.18,
                new Rectangle2D(5898, 2, 238, 244)));

            AllZones.Add(new PKZone("Shame L4+", "Shame", Map.Felucca,
                -128, -40, PKTier.Expert, 0.18,
                new Rectangle2D(5377, 2, 257, 260),
                new Rectangle2D(5635, 2, 260, 124)));

            AllZones.Add(new PKZone("Wrong L4+", "Wrong", Map.Felucca,
                -128, -40, PKTier.Expert, 0.18,
                new Rectangle2D(5633, 511, 253, 510)));

            // Malas — end-game
            AllZones.Add(new PKZone("Doom", "Doom", Map.Malas,
                -128, 128, PKTier.Expert, 0.18,
                new Rectangle2D(256, 0, 256, 304)));

            AllZones.Add(new PKZone("Doom Gauntlet", "Doom", Map.Malas,
                -128, 128, PKTier.Expert, 0.20,
                new Rectangle2D(256, 304, 256, 256)));

            AllZones.Add(new PKZone("The Citadel", "Citadel", Map.Malas,
                -128, 128, PKTier.Expert, 0.20,
                new Rectangle2D(65, 1865, 130, 125)));

            // Ilshenar — end-game
            AllZones.Add(new PKZone("Exodus Deep", "ExodusDungeon", Map.Ilshenar,
                -128, -10, PKTier.Expert, 0.18,
                new Rectangle2D(1832, 16, 248, 200)));

            // TerMur — Stygian Abyss
            AllZones.Add(new PKZone("Stygian Abyss", "StygianAbyss", Map.TerMur,
                -128, 128, PKTier.Expert, 0.20,
                new Rectangle2D(301,  328, 775, 474),
                new Rectangle2D(780,   32, 296, 296),
                new Rectangle2D(436,  802, 304, 178),
                new Rectangle2D(754,  881, 108,  93),
                new Rectangle2D(  0,  684, 134, 176)));
        }
    }
}
