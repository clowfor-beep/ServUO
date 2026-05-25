// ============================================================
// FBPKSpawner.cs
// Scripts/Custom/FBPKSpawner.cs
//
// Manages pools of PoolPK (ambient road/dungeon PKs) per zone.
// Fires FBEventBus.PoolPKSpawned / PoolPKKilled events.
//
// Singleton manager Item — place once via [add FBPKSpawner
// then verify with [fbpkstatus.  Force a fill with [fbpkspawnall.
//
// Zone list is in ZoneConfigs below — comment/uncomment to
// enable zones incrementally as each tier is tested.
//
// Modelled from BountyQuestSpawner.cs.
// Design doc: Design/WorldSpawn_DesignDoc.txt  (Layer 2A)
// ============================================================

using System;
using System.Collections.Generic;
using Server;
using Server.Commands;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;

namespace Server.Custom
{
    // ============================================================
    // POOL PK — ambient road/dungeon PK grunt
    // ============================================================

    public class PoolPK : BaseFBCombatNPC
    {
        // ── Name tables per tier ──────────────────────────────────────────
        private static readonly string[] _tier1Names =
            { "a ruffian", "a thug", "a cutpurse", "a brigand" };
        private static readonly string[] _tier2Names =
            { "a brigand", "a murderer", "a sellsword", "a deserter" };
        private static readonly string[] _tier3Names =
            { "a murderer", "a veteran killer", "a blood knight", "a death knight" };

        // ── Fields ────────────────────────────────────────────────────────
        private SpawnZone _zone;
        private int       _tier;

        [CommandProperty(AccessLevel.GameMaster)]
        public SpawnZone Zone { get => _zone; set => _zone = value; }

        [CommandProperty(AccessLevel.GameMaster)]
        public int Tier { get => _tier; set => _tier = value; }

        // ── Speech ────────────────────────────────────────────────────────
        protected override string[] AggroLines => new[]
        {
            "Stand and deliver!",
            "Your coin or your life!",
            "This road has a toll.",
            "Bad time to be travelling alone.",
            "You picked the wrong path.",
        };

        protected override string[] KillLines => new[]
        {
            "Easy pickings.",
            "Should've stayed home.",
            "*rifles through pockets*",
            "Next.",
            "Didn't even break a sweat.",
        };

        // ── Constructors ──────────────────────────────────────────────────

        // Staff-spawnable default (Britain_Roads, Tier 1)
        [Constructable]
        public PoolPK() : this(SpawnZone.Britain_Roads, 1) { }

        public PoolPK(SpawnZone zone, int tier)
            : base(AIType.AI_Melee, FightMode.Closest, 8)
        {
            _zone = zone;
            _tier = tier;

            // Name by tier
            switch (tier)
            {
                case 2:  Name = _tier2Names[Utility.Random(_tier2Names.Length)]; break;
                case 3:  Name = _tier3Names[Utility.Random(_tier3Names.Length)]; break;
                default: Name = _tier1Names[Utility.Random(_tier1Names.Length)]; break;
            }

            // Archetype: Tier 1 roads are mostly melee fighters; higher tiers
            // get more variety.  Weights are intentionally simple — tune later.
            int archetype = PickArchetype(tier);
            ApplyArchetype(archetype, tier);
        }

        private static int PickArchetype(int tier)
        {
            if (tier == 1)
            {
                // Road PKs: 50 % Dexxer, 20 % Ninja, 15 % Archer, 15 % other
                double r = Utility.RandomDouble();
                if (r < 0.50) return 1; // Dexxer
                if (r < 0.70) return 4; // NinjaDexxer
                if (r < 0.85) return 6; // Archer
                return Utility.RandomMinMax(2, 7);
            }
            // Tiers 2/3 — full spread
            return Utility.RandomMinMax(1, 7);
        }

        private void ApplyArchetype(int archetype, int tier)
        {
            switch (archetype)
            {
                case 1: ApplyDexxerTemplate(tier);      break;
                case 2: ApplyMageTemplate(tier);        break;
                case 3: ApplyNecroMageTemplate(tier);   break;
                case 4: ApplyNinjaDexxerTemplate(tier); break;
                case 5: ApplyPaladinTemplate(tier);     break;
                case 6: ApplyArcherTemplate(tier);      break;
                case 7: ApplySampireTemplate(tier);     break;
                default: ApplyDexxerTemplate(tier);     break;
            }
        }

        // ── Loot ──────────────────────────────────────────────────────────
        public override void GenerateLoot()
        {
            switch (_tier)
            {
                case 2: AddLoot(LootPack.Meager);  PackGold(75,  200); break;
                case 3: AddLoot(LootPack.Average); PackGold(150, 400); break;
                default: AddLoot(LootPack.Poor);   PackGold(25,  100); break;
            }
        }

        // ── Death ─────────────────────────────────────────────────────────
        public override void OnDeath(Container c)
        {
            base.OnDeath(c);
            FBPKSpawner.Instance?.OnPoolPKKilled(this);
        }

        // ── Persistence ───────────────────────────────────────────────────
        public PoolPK(Serial serial) : base(serial) { }

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write(0); // version
            writer.Write((int)_zone);
            writer.Write(_tier);
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            int version = reader.ReadInt();
            _zone = (SpawnZone)reader.ReadInt();
            _tier = reader.ReadInt();
        }
    }

    // ============================================================
    // ZONE CONFIG — pool size and respawn timing per zone
    // ============================================================

    public class PKZoneConfig
    {
        public SpawnZone Zone         { get; }
        public int       Tier         { get; }
        public int       PoolSize     { get; } // max active at once
        public TimeSpan  RespawnDelay { get; }

        public PKZoneConfig(SpawnZone zone, int tier, int poolSize, TimeSpan respawnDelay)
        {
            Zone         = zone;
            Tier         = tier;
            PoolSize     = poolSize;
            RespawnDelay = respawnDelay;
        }
    }

    // ============================================================
    // FBPKSPAWNER — manager singleton item
    // ============================================================

    public class FBPKSpawner : Item
    {
        // ── Singleton ─────────────────────────────────────────────────────
        private static FBPKSpawner _instance;
        public  static FBPKSpawner  Instance => _instance;

        // ── Zone config table ──────────────────────────────────────────────
        // Enable zones incrementally — test each tier before adding the next.
        // Tier 1 respawn:  5 min   |  pool 2-3
        // Tier 2 respawn: 10 min   |  pool 2-4
        // Tier 3 respawn: 15 min   |  pool 1-3
        private static readonly PKZoneConfig[] ZoneConfigs =
        {
            // ── Tier 1 — roads (active) ────────────────────────────────────
            new PKZoneConfig(SpawnZone.Britain_Roads,     1, 3, TimeSpan.FromMinutes(5)),

            // ── Tier 1 expansion — enable after Britain_Roads verified ──────
            // new PKZoneConfig(SpawnZone.Trinsic_Outskirts, 1, 2, TimeSpan.FromMinutes(5)),
            // new PKZoneConfig(SpawnZone.Yew_Roads,         1, 2, TimeSpan.FromMinutes(5)),

            // ── Tier 2 — dungeon entrances — enable after Tier 1 verified ───
            // new PKZoneConfig(SpawnZone.Despise_Entrance,  2, 3, TimeSpan.FromMinutes(10)),
            // new PKZoneConfig(SpawnZone.Covetous_Entrance, 2, 2, TimeSpan.FromMinutes(10)),
            // new PKZoneConfig(SpawnZone.Wrong_Entrance,    2, 2, TimeSpan.FromMinutes(10)),
            // new PKZoneConfig(SpawnZone.Shame_Entrance,    2, 2, TimeSpan.FromMinutes(10)),

            // ── Tier 3 — deep dungeons — enable after Tier 2 verified ───────
            // new PKZoneConfig(SpawnZone.Despise_Level3, 3, 2, TimeSpan.FromMinutes(15)),
            // new PKZoneConfig(SpawnZone.Hythloth_Deep,  3, 2, TimeSpan.FromMinutes(15)),
            // new PKZoneConfig(SpawnZone.Deceit_Level3,  3, 2, TimeSpan.FromMinutes(15)),
        };

        // ── State ─────────────────────────────────────────────────────────
        private readonly Dictionary<SpawnZone, List<PoolPK>> _pools =
            new Dictionary<SpawnZone, List<PoolPK>>();

        private Timer    _timer;
        private bool     _active;
        private TimeSpan _tickInterval = TimeSpan.FromMinutes(1);

        // ── Props ──────────────────────────────────────────────────────────

        [CommandProperty(AccessLevel.GameMaster)]
        public bool Active
        {
            get => _active;
            set
            {
                if (_active == value) return;
                _active = value;
                if (_active) StartTimer(); else StopTimer();
            }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public TimeSpan TickInterval
        {
            get => _tickInterval;
            set { _tickInterval = value; if (_active) { StopTimer(); StartTimer(); } }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public int TotalActive
        {
            get
            {
                int n = 0;
                foreach (var list in _pools.Values) n += list.Count;
                return n;
            }
        }

        // ── Constructor ───────────────────────────────────────────────────

        [Constructable]
        public FBPKSpawner() : base(0xED4)
        {
            if (_instance != null && !_instance.Deleted)
            {
                Delete();
                return;
            }

            Name    = "FB Pool PK Spawner";
            Visible = false;
            Movable = false;

            _instance = this;
            InitPools();
            _active = true;
            StartTimer();
        }

        private void InitPools()
        {
            _pools.Clear();
            foreach (PKZoneConfig cfg in ZoneConfigs)
                _pools[cfg.Zone] = new List<PoolPK>();
        }

        // ── Timer ──────────────────────────────────────────────────────────

        private void StartTimer()
        {
            _timer?.Stop();
            _timer = Timer.DelayCall(_tickInterval, _tickInterval, OnTick);
        }

        private void StopTimer()
        {
            _timer?.Stop();
            _timer = null;
        }

        private void OnTick()
        {
            if (!_active) return;
            foreach (PKZoneConfig cfg in ZoneConfigs)
                TopUpZone(cfg);
        }

        // ── Spawn ──────────────────────────────────────────────────────────

        private void TopUpZone(PKZoneConfig cfg)
        {
            if (!_pools.TryGetValue(cfg.Zone, out List<PoolPK> pool))
                return;

            // Prune dead / deleted entries first
            pool.RemoveAll(pk => pk == null || pk.Deleted || !pk.Alive);

            while (pool.Count < cfg.PoolSize)
            {
                Point3D loc = FBZones.GetRandomSpawnPoint(cfg.Zone);
                Map     map = FBZones.GetMap(cfg.Zone);

                if (loc == Point3D.Zero || map == null)
                    break; // zone returned no valid tile — skip this tick

                PoolPK pk = new PoolPK(cfg.Zone, cfg.Tier);
                pk.MoveToWorld(loc, map);

                // Home keeps them from chasing players across the entire map
                pk.Home      = loc;
                pk.RangeHome = 20;

                pool.Add(pk);
                FBEventBus.Fire_PoolPKSpawned(pk, cfg.Zone);
            }
        }

        // ── Death callback ────────────────────────────────────────────────
        // Called by PoolPK.OnDeath

        public void OnPoolPKKilled(PoolPK pk)
        {
            SpawnZone zone   = pk.Zone;
            Mobile    killer = pk.LastKiller;

            // Credit pet kills to the owner
            if (killer is BaseCreature bc && bc.Controlled && bc.ControlMaster is Mobile m)
                killer = m;

            if (_pools.TryGetValue(zone, out List<PoolPK> pool))
                pool.Remove(pk);

            FBEventBus.Fire_PoolPKKilled(pk, killer, zone);

            // Schedule respawn after zone delay
            PKZoneConfig cfg = GetConfig(zone);
            if (cfg != null)
            {
                Timer.DelayCall(cfg.RespawnDelay, () =>
                {
                    if (!Deleted && _active)
                        TopUpZone(cfg);
                });
            }
        }

        private PKZoneConfig GetConfig(SpawnZone zone)
        {
            foreach (PKZoneConfig cfg in ZoneConfigs)
                if (cfg.Zone == zone) return cfg;
            return null;
        }

        // ── Cleanup ───────────────────────────────────────────────────────

        public override void Delete()
        {
            StopTimer();

            foreach (var pool in _pools.Values)
            {
                foreach (PoolPK pk in pool)
                    if (pk != null && !pk.Deleted) pk.Delete();
                pool.Clear();
            }

            if (_instance == this)
                _instance = null;

            base.Delete();
        }

        // ── GM commands ───────────────────────────────────────────────────

        public static void Initialize()
        {
            CommandSystem.Register("fbpkstatus",   AccessLevel.GameMaster, OnStatus);
            CommandSystem.Register("fbpkspawnall", AccessLevel.GameMaster, OnSpawnAll);
            CommandSystem.Register("fbpkreset",    AccessLevel.GameMaster, OnReset);
            CommandSystem.Register("fbpkfind",     AccessLevel.GameMaster, OnFind);
        }

        private static void OnStatus(CommandEventArgs e)
        {
            if (_instance == null || _instance.Deleted)
            {
                e.Mobile.SendMessage(0x22, "No FBPKSpawner in world. Use [add FBPKSpawner to place one.");
                return;
            }

            e.Mobile.SendMessage(0x35, $"FBPKSpawner — Active: {_instance._active}, Total: {_instance.TotalActive}");
            foreach (PKZoneConfig cfg in ZoneConfigs)
            {
                if (_instance._pools.TryGetValue(cfg.Zone, out List<PoolPK> pool))
                    e.Mobile.SendMessage(0x35, $"  {cfg.Zone} (T{cfg.Tier}): {pool.Count}/{cfg.PoolSize}");
            }
        }

        private static void OnSpawnAll(CommandEventArgs e)
        {
            if (_instance == null || _instance.Deleted)
            {
                e.Mobile.SendMessage(0x22, "No FBPKSpawner in world.");
                return;
            }

            foreach (PKZoneConfig cfg in ZoneConfigs)
                _instance.TopUpZone(cfg);

            e.Mobile.SendMessage(0x35, $"Pools topped up. Total active: {_instance.TotalActive}");
        }

        private static void OnFind(CommandEventArgs e)
        {
            if (_instance == null || _instance.Deleted)
            {
                e.Mobile.SendMessage(0x22, "No FBPKSpawner in world.");
                return;
            }

            // Collect all alive PKs across all zones
            var all = new List<PoolPK>();
            foreach (var pool in _instance._pools.Values)
                foreach (PoolPK pk in pool)
                    if (pk != null && !pk.Deleted && pk.Alive)
                        all.Add(pk);

            if (all.Count == 0)
            {
                e.Mobile.SendMessage(0x22, "No active PoolPKs in world right now.");
                return;
            }

            // Optional index arg: [fbpkfind 2  jumps to #2
            int idx = 0;
            if (e.Arguments.Length > 0 && int.TryParse(e.Arguments[0], out int arg))
                idx = Math.Max(0, Math.Min(arg - 1, all.Count - 1));

            PoolPK target = all[idx];
            e.Mobile.MoveToWorld(target.Location, target.Map);
            e.Mobile.SendMessage(0x35,
                $"Teleported to {target.Name} ({target.Zone}, T{target.Tier}) at {target.Location}. " +
                $"({all.Count} active — use [fbpkfind 1/2/3... to pick)");
        }

        private static void OnReset(CommandEventArgs e)
        {
            if (_instance == null || _instance.Deleted)
            {
                e.Mobile.SendMessage(0x22, "No FBPKSpawner in world.");
                return;
            }

            int deleted = 0;
            foreach (var pool in _instance._pools.Values)
            {
                foreach (PoolPK pk in pool)
                    if (pk != null && !pk.Deleted) { pk.Delete(); deleted++; }
                pool.Clear();
            }

            e.Mobile.SendMessage(0x35, $"Reset complete — {deleted} PoolPK(s) deleted. Pools will refill on next tick.");
        }

        // ── Persistence ───────────────────────────────────────────────────

        public FBPKSpawner(Serial serial) : base(serial) { }

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write(0); // version

            writer.Write(_active);
            writer.Write(_tickInterval);

            // Persist active pool serials so we re-adopt existing PKs on reload
            // rather than spawning duplicates.
            writer.Write(_pools.Count);
            foreach (var kvp in _pools)
            {
                writer.Write((int)kvp.Key);
                var alive = kvp.Value.FindAll(pk => pk != null && !pk.Deleted && pk.Alive);
                writer.Write(alive.Count);
                foreach (PoolPK pk in alive)
                    writer.Write(pk);
            }
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            int version = reader.ReadInt();

            _active       = reader.ReadBool();
            _tickInterval = reader.ReadTimeSpan();

            InitPools();
            _instance = this;

            int zoneCount = reader.ReadInt();
            for (int i = 0; i < zoneCount; i++)
            {
                SpawnZone zone    = (SpawnZone)reader.ReadInt();
                int       pkCount = reader.ReadInt();

                for (int j = 0; j < pkCount; j++)
                {
                    PoolPK pk = reader.ReadMobile() as PoolPK;
                    if (pk != null && !pk.Deleted && pk.Alive)
                    {
                        if (!_pools.ContainsKey(zone))
                            _pools[zone] = new List<PoolPK>();
                        _pools[zone].Add(pk);
                    }
                }
            }

            if (_active)
                StartTimer();
        }

        public override void OnDoubleClick(Mobile from)
        {
            if (from.AccessLevel >= AccessLevel.GameMaster)
                from.SendGump(new PropertiesGump(from, this));
        }
    }
}
