// ============================================================
// PlayerSimulatorManager.cs
// Scripts/Custom/PlayerSimulatorManager.cs
//
// Singleton Item that owns all SimPlayers.
// - Creates the roster (36 members across 12 guilds) on first load.
// - Activates eligible SimPlayers up to MaxActiveSimultaneous.
// - Ticks every minute to top up the active pool.
//
// Persists as a world Item on Map.Internal (invisible).
// Pattern: modelled after BountyQuestSpawner.cs singleton.
// ============================================================

using System;
using System.Collections.Generic;
using Server;
using Server.Commands;
using Server.Custom;
using Server.Mobiles;

namespace Server.Custom
{
    public class PlayerSimulatorManager : Item
    {
        // -- Singleton ------------------------------------------------
        private static PlayerSimulatorManager _instance;
        public  static PlayerSimulatorManager Instance => _instance;

        // Fixed location for the manager item (invisible, internal map)
        private static readonly Point3D ManagerLocation = new Point3D(0, 0, 0);

        // -- Config ---------------------------------------------------
        public const int MaxActiveSimultaneous = 30;

        // -- Roster ---------------------------------------------------
        // All SimPlayers. Persisted via item serialization.
        private List<SimPlayer> _allSimPlayers = new List<SimPlayer>();

        // -- Initialize() -- auto-called at server startup ------------
        public static void Initialize()
        {
            // Register staff commands
            CommandSystem.Register("simreset",   AccessLevel.GameMaster, e => SimReset(e.Mobile));
            CommandSystem.Register("simstatus",  AccessLevel.GameMaster, e => SimStatus(e.Mobile));
            CommandSystem.Register("simgoto",    AccessLevel.GameMaster, e => SimGoto(e.Mobile, e.ArgString?.Trim()));
            CommandSystem.Register("simtrigger", AccessLevel.GameMaster, e => SimTrigger(e.Mobile, e.ArgString?.Trim()));
            CommandSystem.Register("siminfo",    AccessLevel.GameMaster, e => SimInfo(e.Mobile, e.ArgString?.Trim()));

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

        // -- Constructors ---------------------------------------------
        [Constructable]
        public PlayerSimulatorManager() : base(0x1)
        {
            Name    = "PlayerSimulatorManager";
            Visible = false;
            Movable = false;
        }

        public PlayerSimulatorManager(Serial serial) : base(serial) { }

        // -- Roster creation -- runs once on first world load ---------
        private void CreateRosterIfEmpty()
        {
            if (_allSimPlayers.Count > 0) return;

            Console.WriteLine("[SimPlayer] Creating roster...");

            // THE WANDERERS -- 4 members
            // Home: Britain bank area (FBZones.Wanderers_Home)
            Point3D wandHome = FBZones.Wanderers_Home;

            CreateSimPlayer(FBGuilds.Wanderers, "Erik the Wanderer", wandHome, SpawnZone.Britain_Roads,  0);
            CreateSimPlayer(FBGuilds.Wanderers, "Mira of the Road",  wandHome, SpawnZone.Britain_Roads, 15);
            CreateSimPlayer(FBGuilds.Wanderers, "Old Thomas",        wandHome, SpawnZone.Britain_Roads, -20);
            CreateSimPlayer(FBGuilds.Wanderers, "Lena Farwalker",    wandHome, SpawnZone.Britain_Roads, 10);

            // THE CRAFTSMEN'S LEAGUE -- 3 members
            Point3D craftHome = FBZones.CraftsmensLeague_Home;
            _allSimPlayers.Add(new CraftsmensLeagueSimPlayer("Garrett the Smith",
                craftHome, SpawnZone.Britain_Roads, ScheduleProfile.CraftsmensLeague(0)));
            _allSimPlayers.Add(new CraftsmensLeagueSimPlayer("Fisherman Pete",
                craftHome, SpawnZone.Britain_Roads, ScheduleProfile.CraftsmensLeague(20)));
            _allSimPlayers.Add(new CraftsmensLeagueSimPlayer("Woodcutter Bram",
                craftHome, SpawnZone.Britain_Roads, ScheduleProfile.CraftsmensLeague(-15)));

            // IRON COMPANY -- 3 members
            Point3D ironHome = FBZones.IronCompany_Home;
            _allSimPlayers.Add(new IronCompanySimPlayer("Sergeant Vale",
                ironHome, SpawnZone.Britain_Roads, ScheduleProfile.IronCompany(0)));
            _allSimPlayers.Add(new IronCompanySimPlayer("Brother Kael",
                ironHome, SpawnZone.Britain_Roads, ScheduleProfile.IronCompany(10)));
            _allSimPlayers.Add(new IronCompanySimPlayer("Ironhide",
                ironHome, SpawnZone.Britain_Roads, ScheduleProfile.IronCompany(-10)));

            // ARCANE BROTHERHOOD -- 3 members
            Point3D arcaneHome = FBZones.ArcaneBrotherhood_Home;
            _allSimPlayers.Add(new ArcaneBrotherhoodSimPlayer("Scholar Aldric",
                arcaneHome, SpawnZone.Britain_Roads, ScheduleProfile.ArcaneBrotherhood(0)));
            _allSimPlayers.Add(new ArcaneBrotherhoodSimPlayer("Mistress Verna",
                arcaneHome, SpawnZone.Britain_Roads, ScheduleProfile.ArcaneBrotherhood(25)));
            _allSimPlayers.Add(new ArcaneBrotherhoodSimPlayer("The Recluse",
                arcaneHome, SpawnZone.Britain_Roads, ScheduleProfile.ArcaneBrotherhood(-30)));

            // SILVER WOLVES -- 2 members
            Point3D wolvesHome = FBZones.SilverWolves_Home;
            _allSimPlayers.Add(new SilverWolvesSimPlayer("Captain Rowena",
                wolvesHome, SpawnZone.Britain_Roads, ScheduleProfile.SilverWolves(0)));
            _allSimPlayers.Add(new SilverWolvesSimPlayer("Scout Finn",
                wolvesHome, SpawnZone.Britain_Roads, ScheduleProfile.SilverWolves(15)));

            // THE SHADOW HAND -- 3 members
            Point3D shadowHome = FBZones.ShadowHand_Home;
            _allSimPlayers.Add(new ShadowHandSimPlayer("Fingers Malory",
                shadowHome, SpawnZone.Britain_Roads, ScheduleProfile.ShadowHand(0)));
            _allSimPlayers.Add(new ShadowHandSimPlayer("The Whisper",
                shadowHome, SpawnZone.Britain_Roads, ScheduleProfile.ShadowHand(25)));
            _allSimPlayers.Add(new ShadowHandSimPlayer("Slick Fen",
                shadowHome, SpawnZone.Britain_Roads, ScheduleProfile.ShadowHand(-20)));

            // PALADIN ORDER -- 3 members
            Point3D paladinHome = FBZones.PaladinOrder_Home;
            _allSimPlayers.Add(new PaladinOrderSimPlayer("Commander Aldric",
                paladinHome, SpawnZone.Britain_Roads, ScheduleProfile.PaladinOrder(0)));
            _allSimPlayers.Add(new PaladinOrderSimPlayer("Sister Isolde",
                paladinHome, SpawnZone.Britain_Roads, ScheduleProfile.PaladinOrder(20)));
            _allSimPlayers.Add(new PaladinOrderSimPlayer("Sir Edwyn the Pure",
                paladinHome, SpawnZone.Britain_Roads, ScheduleProfile.PaladinOrder(-15)));

            // DEAD WATCHERS -- 3 members
            Point3D deadHome = FBZones.DeadWatchers_Home;
            _allSimPlayers.Add(new DeadWatchersSimPlayer("The Pale Warden",
                deadHome, SpawnZone.Britain_Roads, ScheduleProfile.DeadWatchers(0)));
            _allSimPlayers.Add(new DeadWatchersSimPlayer("Morwen of the Veil",
                deadHome, SpawnZone.Britain_Roads, ScheduleProfile.DeadWatchers(30)));
            _allSimPlayers.Add(new DeadWatchersSimPlayer("Ashborn",
                deadHome, SpawnZone.Britain_Roads, ScheduleProfile.DeadWatchers(-20)));

            // DREAD HUNTERS -- 3 members
            Point3D dreadHome = FBZones.DreadHunters_Home;
            _allSimPlayers.Add(new DreadHuntersSimPlayer("Huntmaster Caelyn",
                dreadHome, SpawnZone.Britain_Roads, ScheduleProfile.DreadHunters(0)));
            _allSimPlayers.Add(new DreadHuntersSimPlayer("Voryn the Relentless",
                dreadHome, SpawnZone.Britain_Roads, ScheduleProfile.DreadHunters(15)));
            _allSimPlayers.Add(new DreadHuntersSimPlayer("Iron Nessa",
                dreadHome, SpawnZone.Britain_Roads, ScheduleProfile.DreadHunters(-25)));

            // BLOOD PACT -- 3 members
            // Home: Destard outskirts (NOT a city zone — guards = instant death)
            Point3D bloodHome = FBZones.BloodPact_Home;
            _allSimPlayers.Add(new BloodPactSimPlayer("Magister Vael",
                bloodHome, SpawnZone.Destard_L1, ScheduleProfile.BloodPact(0)));
            _allSimPlayers.Add(new BloodPactSimPlayer("Sorrow",
                bloodHome, SpawnZone.Destard_L1, ScheduleProfile.BloodPact(20)));
            _allSimPlayers.Add(new BloodPactSimPlayer("The Hollow Priest",
                bloodHome, SpawnZone.Destard_L1, ScheduleProfile.BloodPact(-15)));

            // THE VOID -- 3 members
            // Home: Deceit outskirts (NOT a city zone)
            Point3D voidHome = FBZones.TheVoid_Home;
            _allSimPlayers.Add(new TheVoidSimPlayer("The Nameless",
                voidHome, SpawnZone.Deceit_L1, ScheduleProfile.TheVoid(0)));
            _allSimPlayers.Add(new TheVoidSimPlayer("Unraveller",
                voidHome, SpawnZone.Deceit_L1, ScheduleProfile.TheVoid(35)));
            _allSimPlayers.Add(new TheVoidSimPlayer("Fracture",
                voidHome, SpawnZone.Deceit_L1, ScheduleProfile.TheVoid(-30)));

            // SHADOWBLADE -- 3 members
            // Home: Wrong outskirts (NOT a city zone)
            Point3D bladeHome = FBZones.Shadowblade_Home;
            _allSimPlayers.Add(new ShadowbladeSimPlayer("Silentmark",
                bladeHome, SpawnZone.WantedZone_NearWrong, ScheduleProfile.Shadowblade(0)));
            _allSimPlayers.Add(new ShadowbladeSimPlayer("Cinder",
                bladeHome, SpawnZone.WantedZone_NearWrong, ScheduleProfile.Shadowblade(10)));
            _allSimPlayers.Add(new ShadowbladeSimPlayer("The Ledger",
                bladeHome, SpawnZone.WantedZone_NearWrong, ScheduleProfile.Shadowblade(-20)));

            Console.WriteLine($"[SimPlayer] Roster created: {_allSimPlayers.Count} SimPlayers (36 across 12 guilds).");
        }

        private void CreateSimPlayer(string guild, string memberName, Point3D home,
                                     SpawnZone zone, int scheduleDriftMinutes)
        {
            var sp = new SimPlayer(guild, memberName, home, zone,
                                   ScheduleProfile.Wanderers(scheduleDriftMinutes));
            _allSimPlayers.Add(sp);
        }

        // -- Active pool management -----------------------------------
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

        // -- Staff commands -------------------------------------------

        /// <summary>
        /// [simreset -- wipe all SimPlayers and rebuild the full roster from scratch.
        /// Use after adding new guild members to the roster definition.
        /// </summary>
        public static void SimReset(Mobile from)
        {
            if (_instance == null)
            {
                from.SendMessage(0x22, "[SimPlayer] No manager instance found.");
                return;
            }

            int deleted = 0;
            foreach (SimPlayer sp in _instance._allSimPlayers)
            {
                if (!sp.Deleted)
                {
                    sp.Delete();
                    deleted++;
                }
            }
            _instance._allSimPlayers.Clear();

            from.SendMessage(0x35, $"[SimPlayer] Deleted {deleted} SimPlayers. Rebuilding roster...");
            Console.WriteLine($"[SimPlayer] simreset by {from.Name} -- deleted {deleted} SimPlayers.");

            _instance.CreateRosterIfEmpty();
            _instance.ActivateEligibleSimPlayers();

            from.SendMessage(0x35, $"[SimPlayer] Roster rebuilt: {_instance._allSimPlayers.Count} SimPlayers, {_instance.CountActive()} active.");
        }

        /// <summary>
        /// [simstatus -- roster overview: location, state, and one-line guild detail for every SimPlayer.
        /// </summary>
        public static void SimStatus(Mobile from)
        {
            if (_instance == null)
            {
                from.SendMessage(0x22, "[SimPlayer] No manager instance found.");
                return;
            }

            from.SendMessage(0x4AA, $"=== SimPlayer Status ({_instance._allSimPlayers.Count} roster / {_instance.CountActive()} active) ===");
            foreach (SimPlayer sp in _instance._allSimPlayers)
            {
                string location = sp.Deleted
                    ? "DELETED"
                    : sp.Map == Map.Internal
                        ? $"Internal  cooldown={sp.IsOnCooldown}  schedule={sp.Schedule_ShouldBeActive()}"
                        : $"{sp.Map.Name} ({sp.X},{sp.Y})  state={sp.State}";

                from.SendMessage(1153, $"  {sp.MemberName} [{sp.GuildName}] -- {location}");

                string detail = sp.GetStatusDetail();
                if (!string.IsNullOrEmpty(detail))
                    from.SendMessage(1153, $"      {detail}");
            }
        }

        /// <summary>
        /// [siminfo [name] -- detailed state dump for a specific SimPlayer (partial name match).
        /// If no name is given, shows all SimPlayers with guild detail.
        /// </summary>
        public static void SimInfo(Mobile from, string filter)
        {
            if (_instance == null)
            {
                from.SendMessage(0x22, "[SimPlayer] No manager instance found.");
                return;
            }

            var matches = FindSimPlayers(filter);
            if (matches.Count == 0)
            {
                from.SendMessage(0x22, $"[SimPlayer] No SimPlayer matching '{filter}'.");
                return;
            }

            from.SendMessage(0x4AA, $"=== SimInfo: {matches.Count} match(es) for '{filter}' ===");
            foreach (SimPlayer sp in matches)
            {
                from.SendMessage(0x4AA, $"  {sp.MemberName} [{sp.GuildName}]");

                string loc = sp.Deleted
                    ? "DELETED"
                    : sp.Map == Map.Internal
                        ? $"Map.Internal  cooldown={sp.IsOnCooldown}"
                        : $"{sp.Map.Name} ({sp.X},{sp.Y},{sp.Z})";

                from.SendMessage(1153, $"    Location : {loc}");
                from.SendMessage(1153, $"    State    : {sp.State}");
                from.SendMessage(1153, $"    Schedule : {sp.Schedule_ShouldBeActive()}");
                from.SendMessage(1153, $"    Combatant: {(sp.Combatant != null ? sp.Combatant.Name : "none")}");

                string detail = sp.GetStatusDetail();
                if (!string.IsNullOrEmpty(detail))
                    from.SendMessage(1153, $"    {detail}");
            }
        }

        /// <summary>
        /// [simgoto [name] -- teleport the GM to the first SimPlayer whose name/guild matches.
        /// Usage: [simgoto Sergeant  or  [simgoto Iron Company
        /// </summary>
        public static void SimGoto(Mobile from, string filter)
        {
            if (string.IsNullOrEmpty(filter))
            {
                from.SendMessage(0x22, "Usage: [simgoto <partial name or guild>");
                return;
            }

            SimPlayer match = FindSimPlayer(filter);
            if (match == null)
            {
                from.SendMessage(0x22, $"[SimPlayer] No SimPlayer matching '{filter}'.");
                return;
            }

            if (match.Map == Map.Internal)
            {
                from.SendMessage(0x22, $"[SimPlayer] {match.MemberName} is currently inactive (internal map).");
                return;
            }

            from.MoveToWorld(match.Location, match.Map);
            from.SendMessage(0x35, $"[SimPlayer] Jumped to {match.MemberName} [{match.GuildName}]  state={match.State}");

            string detail = match.GetStatusDetail();
            if (!string.IsNullOrEmpty(detail))
                from.SendMessage(0x35, $"  {detail}");
        }

        /// <summary>
        /// [simtrigger [filter] -- immediately trigger the next scheduled event on ALL matching
        /// SimPlayers.  Use a guild name to trigger all members at once.
        /// Usage: [simtrigger iron          (triggers all Iron Company members)
        ///        [simtrigger Fingers        (triggers that specific SimPlayer)
        ///        [simtrigger shadow hand    (triggers all Shadow Hand members)
        /// </summary>
        public static void SimTrigger(Mobile from, string filter)
        {
            if (string.IsNullOrEmpty(filter))
            {
                from.SendMessage(0x22, "Usage: [simtrigger <partial name or guild>");
                return;
            }

            var matches = FindSimPlayers(filter);
            if (matches.Count == 0)
            {
                from.SendMessage(0x22, $"[SimPlayer] No SimPlayer matching '{filter}'.");
                return;
            }

            from.SendMessage(0x4AA, $"[SimPlayer] Triggering {matches.Count} SimPlayer(s) matching '{filter}':");
            foreach (SimPlayer sp in matches)
            {
                string result = sp.TriggerNextEvent();
                from.SendMessage(0x35, $"  {result}");
            }
        }

        // -- Lookup helpers -------------------------------------------

        private static SimPlayer FindSimPlayer(string filter)
        {
            if (_instance == null || string.IsNullOrEmpty(filter)) return null;
            string f = filter.ToLowerInvariant();
            foreach (SimPlayer sp in _instance._allSimPlayers)
            {
                if (!sp.Deleted
                    && (sp.MemberName.ToLowerInvariant().Contains(f)
                        || sp.GuildName.ToLowerInvariant().Contains(f)))
                    return sp;
            }
            return null;
        }

        private static List<SimPlayer> FindSimPlayers(string filter)
        {
            var result = new List<SimPlayer>();
            if (_instance == null) return result;

            // Empty filter = return all
            if (string.IsNullOrEmpty(filter))
            {
                result.AddRange(_instance._allSimPlayers);
                return result;
            }

            string f = filter.ToLowerInvariant();
            foreach (SimPlayer sp in _instance._allSimPlayers)
            {
                if (!sp.Deleted
                    && (sp.MemberName.ToLowerInvariant().Contains(f)
                        || sp.GuildName.ToLowerInvariant().Contains(f)))
                    result.Add(sp);
            }
            return result;
        }

        // -- Serialization --------------------------------------------
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
