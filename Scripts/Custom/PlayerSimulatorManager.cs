// ============================================================
// PlayerSimulatorManager.cs
// Scripts/Custom/PlayerSimulatorManager.cs
//
// Singleton Item that owns all SimPlayers.
// - Creates the Phase 1 roster (4 Wanderers) on first load.
// - Activates eligible SimPlayers up to MaxActiveSimultaneous.
// - Ticks every minute to top up the active pool.
//
// Persists as a world Item on Map.Internal (invisible).
// Pattern: modelled after BountyQuestSpawner.cs singleton.
// ============================================================

using System;
using System.Collections.Generic;
using Server;
using Server.Custom;
using Server.Mobiles;

namespace Server.Custom
{
    public class PlayerSimulatorManager : Item
    {
        // ── Singleton ─────────────────────────────────────────────────────
        private static PlayerSimulatorManager _instance;
        public  static PlayerSimulatorManager Instance => _instance;

        // Fixed location for the manager item (invisible, internal map)
        private static readonly Point3D ManagerLocation = new Point3D(0, 0, 0);

        // ── Config ────────────────────────────────────────────────────────
        public const int MaxActiveSimultaneous = 30;

        // ── Roster ────────────────────────────────────────────────────────
        // All SimPlayers (Phase 1: 4). Persisted via item serialization.
        private List<SimPlayer> _allSimPlayers = new List<SimPlayer>();

        // ── Initialize() — auto-called at server startup ──────────────────
        public static void Initialize()
        {
            // Find existing singleton in world
            if (_instance == null)
            {
                foreach (Item item in World.Items.Values)
                {
                    if (item is PlayerSimulatorManager mgr)
                    {
                        _instance = mgr;
                        break;
                    }
                }
            }

            // Create if not found
            if (_instance == null)
            {
                _instance = new PlayerSimulatorManager();
                _instance.MoveToWorld(ManagerLocation, Map.Internal);
                Console.WriteLine("[SimPlayer] PlayerSimulatorManager created.");
            }

            _instance.CreateRosterIfEmpty();
            _instance.ActivateEligibleSimPlayers();

            // Start management tick (every minute)
            Timer.DelayCall(TimeSpan.FromMinutes(1.0), _instance.OnManageTick);
        }

        // ── Constructors ──────────────────────────────────────────────────
        [Constructable]
        public PlayerSimulatorManager() : base(0x1)
        {
            Name    = "PlayerSimulatorManager";
            Visible = false;
            Movable = false;
        }

        public PlayerSimulatorManager(Serial serial) : base(serial) { }

        // ── Roster creation — runs once on first world load ───────────────
        private void CreateRosterIfEmpty()
        {
            if (_allSimPlayers.Count > 0) return;

            Console.WriteLine("[SimPlayer] Creating Phase 1 roster...");

            // THE WANDERERS — 4 members
            // Home: Britain bank area (FBZones.Wanderers_Home)
            Point3D wandHome = FBZones.Wanderers_Home;

            CreateSimPlayer(FBGuilds.Wanderers, "Erik the Wanderer", wandHome, SpawnZone.Britain_Roads,  0);
            CreateSimPlayer(FBGuilds.Wanderers, "Mira of the Road",  wandHome, SpawnZone.Britain_Roads, 15);
            CreateSimPlayer(FBGuilds.Wanderers, "Old Thomas",        wandHome, SpawnZone.Britain_Roads, -20);
            CreateSimPlayer(FBGuilds.Wanderers, "Lena Farwalker",    wandHome, SpawnZone.Britain_Roads, 10);

            Console.WriteLine($"[SimPlayer] Roster created: {_allSimPlayers.Count} SimPlayers.");
        }

        private void CreateSimPlayer(string guild, string memberName, Point3D home,
                                     SpawnZone zone, int scheduleDriftMinutes)
        {
            var sp = new SimPlayer(guild, memberName, home, zone,
                                   ScheduleProfile.Wanderers(scheduleDriftMinutes));
            _allSimPlayers.Add(sp);
        }

        // ── Active pool management ────────────────────────────────────────
        private void ActivateEligibleSimPlayers()
        {
            int active = CountActive();
            foreach (SimPlayer sp in _allSimPlayers)
            {
                if (active >= MaxActiveSimultaneous) break;
                if (sp.Deleted) continue;
                if (sp.Map != Map.Internal) continue;       // already in world
                if (sp.IsOnCooldown) continue;
                if (!sp.Schedule_ShouldBeActive()) continue;

                sp.Activate();
                active++;
            }
        }

        private int CountActive()
        {
            int count = 0;
            foreach (SimPlayer sp in _allSimPlayers)
            {
                if (!sp.Deleted && sp.Map != Map.Internal)
                    count++;
            }
            return count;
        }

        /// <summary>Runs every minute to top up the active pool.</summary>
        private void OnManageTick()
        {
            if (Deleted) return;
            ActivateEligibleSimPlayers();
            Timer.DelayCall(TimeSpan.FromMinutes(1.0), OnManageTick);
        }

        // ── Serialization ─────────────────────────────────────────────────
        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write(0); // version

            writer.Write(_allSimPlayers.Count);
            foreach (SimPlayer sp in _allSimPlayers)
                writer.Write(sp); // writes Mobile Serial reference
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            int version = reader.ReadInt();

            int count = reader.ReadInt();
            for (int i = 0; i < count; i++)
            {
                SimPlayer sp = reader.ReadMobile() as SimPlayer;
                if (sp != null)
                    _allSimPlayers.Add(sp);
            }

            _instance = this;
        }
    }
}
