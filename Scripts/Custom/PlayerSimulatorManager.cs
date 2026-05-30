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
using Server.Engines.CannedEvil;
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
        public const int MaxActiveSimultaneous = 100;

        // -- Roster ---------------------------------------------------
        // All SimPlayers. Persisted via item serialization.
        private List<SimPlayer> _allSimPlayers = new List<SimPlayer>();

        /// <summary>Read-only roster access for the SimPlayerGump.</summary>
        public IEnumerable<SimPlayer> AllSimPlayers => _allSimPlayers;

        // -- Initialize() -- auto-called at server startup ------------
        public static void Initialize()
        {
            // Register staff commands
            CommandSystem.Register("simreset",   AccessLevel.GameMaster, e => SimReset(e.Mobile));
            CommandSystem.Register("simstatus",  AccessLevel.GameMaster, e => SimStatus(e.Mobile));
            CommandSystem.Register("simgoto",    AccessLevel.GameMaster, e => SimGoto(e.Mobile, e.ArgString?.Trim()));
            CommandSystem.Register("simtrigger", AccessLevel.GameMaster, e => SimTrigger(e.Mobile, e.ArgString?.Trim()));
            CommandSystem.Register("siminfo",    AccessLevel.GameMaster, e => SimInfo(e.Mobile, e.ArgString?.Trim()));
            CommandSystem.Register("simchamp",   AccessLevel.GameMaster, e => SimChamp(e.Mobile));
            CommandSystem.Register("simpanel",   AccessLevel.GameMaster, e => e.Mobile.SendGump(new SimPlayerGump(e.Mobile)));

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

            // IRON COMPANY -- 6 members (Britain chapter)
            Point3D ironHome = FBZones.IronCompany_Home;
            _allSimPlayers.Add(new IronCompanySimPlayer("Sergeant Vale",
                ironHome, SpawnZone.Britain_Roads, ScheduleProfile.IronCompany(0)));
            _allSimPlayers.Add(new IronCompanySimPlayer("Brother Kael",
                ironHome, SpawnZone.Britain_Roads, ScheduleProfile.IronCompany(10)));
            _allSimPlayers.Add(new IronCompanySimPlayer("Ironhide",
                ironHome, SpawnZone.Britain_Roads, ScheduleProfile.IronCompany(-10)));
            _allSimPlayers.Add(new IronCompanySimPlayer("Stonewall Brec",
                ironHome, SpawnZone.Britain_Roads, ScheduleProfile.IronCompany(5)));
            _allSimPlayers.Add(new IronCompanySimPlayer("Shield Maiden Rova",
                ironHome, SpawnZone.Britain_Roads, ScheduleProfile.IronCompany(-5)));
            _allSimPlayers.Add(new IronCompanySimPlayer("Grim the Veteran",
                ironHome, SpawnZone.Britain_Roads, ScheduleProfile.IronCompany(20)));

            // ARCANE BROTHERHOOD -- 3 members
            Point3D arcaneHome = FBZones.ArcaneBrotherhood_Home;
            _allSimPlayers.Add(new ArcaneBrotherhoodSimPlayer("Scholar Aldric",
                arcaneHome, SpawnZone.Britain_Roads, ScheduleProfile.ArcaneBrotherhood(0)));
            _allSimPlayers.Add(new ArcaneBrotherhoodSimPlayer("Mistress Verna",
                arcaneHome, SpawnZone.Britain_Roads, ScheduleProfile.ArcaneBrotherhood(25)));
            _allSimPlayers.Add(new ArcaneBrotherhoodSimPlayer("The Recluse",
                arcaneHome, SpawnZone.Britain_Roads, ScheduleProfile.ArcaneBrotherhood(-30)));

            // SILVER WOLVES -- 4 members
            Point3D wolvesHome = FBZones.SilverWolves_Home;
            _allSimPlayers.Add(new SilverWolvesSimPlayer("Captain Rowena",
                wolvesHome, SpawnZone.Britain_Roads, ScheduleProfile.SilverWolves(0)));
            _allSimPlayers.Add(new SilverWolvesSimPlayer("Scout Finn",
                wolvesHome, SpawnZone.Britain_Roads, ScheduleProfile.SilverWolves(15)));
            _allSimPlayers.Add(new SilverWolvesSimPlayer("Patrol Ryen",
                wolvesHome, SpawnZone.Britain_Roads, ScheduleProfile.SilverWolves(-20)));
            _allSimPlayers.Add(new SilverWolvesSimPlayer("Sentinel Kade",
                wolvesHome, SpawnZone.Britain_Roads, ScheduleProfile.SilverWolves(30)));

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

            // ════════════════════════════════════════════════════════════════════
            // MULTI-CITY EXPANSION
            // Each SimPlayer automatically banks at whichever city bank is nearest
            // its home — no extra wiring needed.  All coords are approximate; verify
            // with [where in-game and adjust FBZones constants if needed.
            // ════════════════════════════════════════════════════════════════════

            // ── TRINSIC (16 new members across 5 guilds) ─────────────────────────

            Point3D wandTrinsic   = FBZones.Wanderers_Home_Trinsic;
            Point3D ironTrinsic   = FBZones.IronCompany_Home_Trinsic;
            Point3D wolvTrinsic   = FBZones.SilverWolves_Home_Trinsic;
            Point3D palTrinsic    = FBZones.PaladinOrder_Home_Trinsic;
            Point3D shadTrinsic   = FBZones.ShadowHand_Home_Trinsic;

            // Wanderers (4)
            CreateSimPlayer(FBGuilds.Wanderers, "Alec Southford",   wandTrinsic, SpawnZone.Trinsic_City,   0);
            CreateSimPlayer(FBGuilds.Wanderers, "Brin of Trinsic",  wandTrinsic, SpawnZone.Trinsic_City,  20);
            CreateSimPlayer(FBGuilds.Wanderers, "Old Saul",         wandTrinsic, SpawnZone.Trinsic_City, -15);
            CreateSimPlayer(FBGuilds.Wanderers, "Traveller Wyn",    wandTrinsic, SpawnZone.Trinsic_City,  10);

            // Iron Company (6) -- Trinsic chapter, still runs champ spawns
            _allSimPlayers.Add(new IronCompanySimPlayer("Vanguard Petra",
                ironTrinsic, SpawnZone.Trinsic_City, ScheduleProfile.IronCompany(0)));
            _allSimPlayers.Add(new IronCompanySimPlayer("Shield Wall Dorn",
                ironTrinsic, SpawnZone.Trinsic_City, ScheduleProfile.IronCompany(15)));
            _allSimPlayers.Add(new IronCompanySimPlayer("Tactician Yeln",
                ironTrinsic, SpawnZone.Trinsic_City, ScheduleProfile.IronCompany(-10)));
            _allSimPlayers.Add(new IronCompanySimPlayer("Bladewarden Cass",
                ironTrinsic, SpawnZone.Trinsic_City, ScheduleProfile.IronCompany(5)));
            _allSimPlayers.Add(new IronCompanySimPlayer("Hardened Oryn",
                ironTrinsic, SpawnZone.Trinsic_City, ScheduleProfile.IronCompany(-20)));
            _allSimPlayers.Add(new IronCompanySimPlayer("Forgeborn Thane",
                ironTrinsic, SpawnZone.Trinsic_City, ScheduleProfile.IronCompany(25)));

            // Silver Wolves (3) -- patrols the Trinsic streets
            _allSimPlayers.Add(new SilverWolvesSimPlayer("Constable Daven",
                wolvTrinsic, SpawnZone.Trinsic_City, ScheduleProfile.SilverWolves(0)));
            _allSimPlayers.Add(new SilverWolvesSimPlayer("Warden Tess",
                wolvTrinsic, SpawnZone.Trinsic_City, ScheduleProfile.SilverWolves(20)));
            _allSimPlayers.Add(new SilverWolvesSimPlayer("Watchman Ardor",
                wolvTrinsic, SpawnZone.Trinsic_City, ScheduleProfile.SilverWolves(-15)));

            // Paladin Order (3) -- Trinsic is the paladin city
            _allSimPlayers.Add(new PaladinOrderSimPlayer("Knight-Captain Lira",
                palTrinsic, SpawnZone.Trinsic_City, ScheduleProfile.PaladinOrder(0)));
            _allSimPlayers.Add(new PaladinOrderSimPlayer("Squire Petyr",
                palTrinsic, SpawnZone.Trinsic_City, ScheduleProfile.PaladinOrder(25)));
            _allSimPlayers.Add(new PaladinOrderSimPlayer("High Justiciar Wren",
                palTrinsic, SpawnZone.Trinsic_City, ScheduleProfile.PaladinOrder(-20)));

            // Shadow Hand (3) -- thieves work every city
            _allSimPlayers.Add(new ShadowHandSimPlayer("Dockside Sam",
                shadTrinsic, SpawnZone.Trinsic_City, ScheduleProfile.ShadowHand(0)));
            _allSimPlayers.Add(new ShadowHandSimPlayer("The Hook",
                shadTrinsic, SpawnZone.Trinsic_City, ScheduleProfile.ShadowHand(30)));
            _allSimPlayers.Add(new ShadowHandSimPlayer("Pale Fen",
                shadTrinsic, SpawnZone.Trinsic_City, ScheduleProfile.ShadowHand(-25)));

            // ── YEW (10 new members across 3 guilds) ─────────────────────────────

            Point3D wandYew   = FBZones.Wanderers_Home_Yew;
            Point3D arcYew    = FBZones.ArcaneBrotherhood_Home_Yew;
            Point3D ironYew   = FBZones.IronCompany_Home_Yew;

            // Wanderers (4)
            CreateSimPlayer(FBGuilds.Wanderers, "Forest Quinn",     wandYew, SpawnZone.Yew_City,   0);
            CreateSimPlayer(FBGuilds.Wanderers, "Dara of Yew",      wandYew, SpawnZone.Yew_City,  15);
            CreateSimPlayer(FBGuilds.Wanderers, "Rowan Fartrail",   wandYew, SpawnZone.Yew_City, -20);
            CreateSimPlayer(FBGuilds.Wanderers, "Nils the Pilgrim", wandYew, SpawnZone.Yew_City,  10);

            // Arcane Brotherhood (3) -- court mages at Yew courthouse
            _allSimPlayers.Add(new ArcaneBrotherhoodSimPlayer("Court Mage Orin",
                arcYew, SpawnZone.Yew_City, ScheduleProfile.ArcaneBrotherhood(0)));
            _allSimPlayers.Add(new ArcaneBrotherhoodSimPlayer("Druid Sylva",
                arcYew, SpawnZone.Yew_City, ScheduleProfile.ArcaneBrotherhood(20)));
            _allSimPlayers.Add(new ArcaneBrotherhoodSimPlayer("Warden Theron",
                arcYew, SpawnZone.Yew_City, ScheduleProfile.ArcaneBrotherhood(-15)));

            // Iron Company (3) -- Yew chapter, patrols the forest roads
            _allSimPlayers.Add(new IronCompanySimPlayer("Forester Dunric",
                ironYew, SpawnZone.Yew_City, ScheduleProfile.IronCompany(0)));
            _allSimPlayers.Add(new IronCompanySimPlayer("Ashveil",
                ironYew, SpawnZone.Yew_City, ScheduleProfile.IronCompany(20)));
            _allSimPlayers.Add(new IronCompanySimPlayer("Steelback Orvyn",
                ironYew, SpawnZone.Yew_City, ScheduleProfile.IronCompany(-15)));

            // ── MINOC (11 new members across 4 guilds) ───────────────────────────

            Point3D wandMinoc  = FBZones.Wanderers_Home_Minoc;
            Point3D craftMinoc = FBZones.CraftsmensLeague_Home_Minoc;
            Point3D dreadMinoc = FBZones.DreadHunters_Home_Minoc;
            Point3D shadMinoc  = FBZones.ShadowHand_Home_Minoc;

            // Wanderers (2)
            CreateSimPlayer(FBGuilds.Wanderers, "Crossroads Fen",    wandMinoc, SpawnZone.Minoc_City,   0);
            CreateSimPlayer(FBGuilds.Wanderers, "Mountain Pass Dar", wandMinoc, SpawnZone.Minoc_City,  20);

            // Craftsmen's League (3) -- miners and smelters
            _allSimPlayers.Add(new CraftsmensLeagueSimPlayer("Miner Keth",
                craftMinoc, SpawnZone.Minoc_City, ScheduleProfile.CraftsmensLeague(0)));
            _allSimPlayers.Add(new CraftsmensLeagueSimPlayer("Stonecutter Riva",
                craftMinoc, SpawnZone.Minoc_City, ScheduleProfile.CraftsmensLeague(20)));
            _allSimPlayers.Add(new CraftsmensLeagueSimPlayer("Smelter Bors",
                craftMinoc, SpawnZone.Minoc_City, ScheduleProfile.CraftsmensLeague(-15)));

            // Dread Hunters (3) -- hunt near the mines
            _allSimPlayers.Add(new DreadHuntersSimPlayer("Mountaineer Cask",
                dreadMinoc, SpawnZone.Minoc_City, ScheduleProfile.DreadHunters(0)));
            _allSimPlayers.Add(new DreadHuntersSimPlayer("Tracker Bolt",
                dreadMinoc, SpawnZone.Minoc_City, ScheduleProfile.DreadHunters(15)));
            _allSimPlayers.Add(new DreadHuntersSimPlayer("Ridge Walker",
                dreadMinoc, SpawnZone.Minoc_City, ScheduleProfile.DreadHunters(-25)));

            // Shadow Hand (3) -- ore thieves and fence operators
            _allSimPlayers.Add(new ShadowHandSimPlayer("Tunnels",
                shadMinoc, SpawnZone.Minoc_City, ScheduleProfile.ShadowHand(0)));
            _allSimPlayers.Add(new ShadowHandSimPlayer("Ore Fingers",
                shadMinoc, SpawnZone.Minoc_City, ScheduleProfile.ShadowHand(30)));
            _allSimPlayers.Add(new ShadowHandSimPlayer("Rook",
                shadMinoc, SpawnZone.Minoc_City, ScheduleProfile.ShadowHand(-20)));

            // ── VESPER (10 new members across 3 guilds) ──────────────────────────

            Point3D wandVesper  = FBZones.Wanderers_Home_Vesper;
            Point3D craftVesper = FBZones.CraftsmensLeague_Home_Vesper;
            Point3D shadVesper  = FBZones.ShadowHand_Home_Vesper;

            // Wanderers (4)
            CreateSimPlayer(FBGuilds.Wanderers, "Merchant Devin",   wandVesper, SpawnZone.Vesper_City,   0);
            CreateSimPlayer(FBGuilds.Wanderers, "Tomas of Vesper",  wandVesper, SpawnZone.Vesper_City,  20);
            CreateSimPlayer(FBGuilds.Wanderers, "Lorn the Drifter", wandVesper, SpawnZone.Vesper_City, -15);
            CreateSimPlayer(FBGuilds.Wanderers, "Harbour Wick",     wandVesper, SpawnZone.Vesper_City,  10);

            // Craftsmen's League (3) -- shipwrights and rope-makers
            _allSimPlayers.Add(new CraftsmensLeagueSimPlayer("Shipwright Colm",
                craftVesper, SpawnZone.Vesper_City, ScheduleProfile.CraftsmensLeague(0)));
            _allSimPlayers.Add(new CraftsmensLeagueSimPlayer("Sailmaker Darra",
                craftVesper, SpawnZone.Vesper_City, ScheduleProfile.CraftsmensLeague(25)));
            _allSimPlayers.Add(new CraftsmensLeagueSimPlayer("Cooper Wex",
                craftVesper, SpawnZone.Vesper_City, ScheduleProfile.CraftsmensLeague(-20)));

            // Shadow Hand (3) -- smugglers and dock runners
            _allSimPlayers.Add(new ShadowHandSimPlayer("River Kyn",
                shadVesper, SpawnZone.Vesper_City, ScheduleProfile.ShadowHand(0)));
            _allSimPlayers.Add(new ShadowHandSimPlayer("The Smuggler",
                shadVesper, SpawnZone.Vesper_City, ScheduleProfile.ShadowHand(20)));
            _allSimPlayers.Add(new ShadowHandSimPlayer("Nocturne",
                shadVesper, SpawnZone.Vesper_City, ScheduleProfile.ShadowHand(-10)));

            // ── MOONGLOW (3 new members, 1 guild) ────────────────────────────────

            Point3D arcMoonglow = FBZones.ArcaneBrotherhood_Home_Moonglow;

            // Arcane Brotherhood (3) -- mage island chapter
            _allSimPlayers.Add(new ArcaneBrotherhoodSimPlayer("Inscriber Lorn",
                arcMoonglow, SpawnZone.Moonglow_City, ScheduleProfile.ArcaneBrotherhood(0)));
            _allSimPlayers.Add(new ArcaneBrotherhoodSimPlayer("Reagent Keeper Voss",
                arcMoonglow, SpawnZone.Moonglow_City, ScheduleProfile.ArcaneBrotherhood(30)));
            _allSimPlayers.Add(new ArcaneBrotherhoodSimPlayer("Astrologer Mira",
                arcMoonglow, SpawnZone.Moonglow_City, ScheduleProfile.ArcaneBrotherhood(-20)));

            // ── SKARA BRAE (12 new members across 4 guilds) ──────────────────────

            Point3D wandSkara  = FBZones.Wanderers_Home_SkaraBrae;
            Point3D craftSkara = FBZones.CraftsmensLeague_Home_SkaraBrae;
            Point3D deadSkara  = FBZones.DeadWatchers_Home_SkaraBrae;
            Point3D ironSkara  = FBZones.IronCompany_Home_SkaraBrae;

            // Wanderers (3) -- island travellers
            CreateSimPlayer(FBGuilds.Wanderers, "Saltwind",   wandSkara, SpawnZone.SkaraBrae_City,   0);
            CreateSimPlayer(FBGuilds.Wanderers, "Brine Yael", wandSkara, SpawnZone.SkaraBrae_City,  25);
            CreateSimPlayer(FBGuilds.Wanderers, "Tidewalker", wandSkara, SpawnZone.SkaraBrae_City, -20);

            // Craftsmen's League (3) -- fishermen, netmakers, boatwrights
            _allSimPlayers.Add(new CraftsmensLeagueSimPlayer("Fisherman Gareth",
                craftSkara, SpawnZone.SkaraBrae_City, ScheduleProfile.CraftsmensLeague(0)));
            _allSimPlayers.Add(new CraftsmensLeagueSimPlayer("Netmaker Sian",
                craftSkara, SpawnZone.SkaraBrae_City, ScheduleProfile.CraftsmensLeague(15)));
            _allSimPlayers.Add(new CraftsmensLeagueSimPlayer("Boatwright Edda",
                craftSkara, SpawnZone.SkaraBrae_City, ScheduleProfile.CraftsmensLeague(-25)));

            // Dead Watchers (3) -- the island's graveyard has its appeal
            _allSimPlayers.Add(new DeadWatchersSimPlayer("The Ferryman",
                deadSkara, SpawnZone.SkaraBrae_City, ScheduleProfile.DeadWatchers(0)));
            _allSimPlayers.Add(new DeadWatchersSimPlayer("Isle Shade",
                deadSkara, SpawnZone.SkaraBrae_City, ScheduleProfile.DeadWatchers(20)));
            _allSimPlayers.Add(new DeadWatchersSimPlayer("Ashrift",
                deadSkara, SpawnZone.SkaraBrae_City, ScheduleProfile.DeadWatchers(-15)));

            // Iron Company (3) -- Skara Brae chapter, guards the island docks
            _allSimPlayers.Add(new IronCompanySimPlayer("Harborguard Mads",
                ironSkara, SpawnZone.SkaraBrae_City, ScheduleProfile.IronCompany(0)));
            _allSimPlayers.Add(new IronCompanySimPlayer("Tide Warden Brek",
                ironSkara, SpawnZone.SkaraBrae_City, ScheduleProfile.IronCompany(25)));
            _allSimPlayers.Add(new IronCompanySimPlayer("Ironclad Sael",
                ironSkara, SpawnZone.SkaraBrae_City, ScheduleProfile.IronCompany(-20)));

            // ── DUNGEON EXPANSION (6 new red/grey members) ───────────────────────

            // Blood Pact near Shame entrance (3) -- surface ambushers
            Point3D bloodShame = FBZones.BloodPact_Home_NearShame;
            _allSimPlayers.Add(new BloodPactSimPlayer("Shade Covenant",
                bloodShame, SpawnZone.WantedZone_NearShame, ScheduleProfile.BloodPact(0)));
            _allSimPlayers.Add(new BloodPactSimPlayer("The Rite Keeper",
                bloodShame, SpawnZone.WantedZone_NearShame, ScheduleProfile.BloodPact(25)));
            _allSimPlayers.Add(new BloodPactSimPlayer("Pale Magister",
                bloodShame, SpawnZone.WantedZone_NearShame, ScheduleProfile.BloodPact(-20)));

            // The Void near Hythloth entrance (3) -- surface ambushers
            Point3D voidHythloth = FBZones.TheVoid_Home_NearHythloth;
            _allSimPlayers.Add(new TheVoidSimPlayer("Null",
                voidHythloth, SpawnZone.WantedZone_NearHythloth, ScheduleProfile.TheVoid(0)));
            _allSimPlayers.Add(new TheVoidSimPlayer("The Abyss Watcher",
                voidHythloth, SpawnZone.WantedZone_NearHythloth, ScheduleProfile.TheVoid(30)));
            _allSimPlayers.Add(new TheVoidSimPlayer("Void Remnant",
                voidHythloth, SpawnZone.WantedZone_NearHythloth, ScheduleProfile.TheVoid(-20)));

            Console.WriteLine($"[SimPlayer] Roster created: {_allSimPlayers.Count} SimPlayers across 12 guilds, 7 cities.");
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

        /// <summary>
        /// [simchamp -- picks a random surface Felucca ChampionSpawn, activates it if needed,
        /// then force-marches ALL Iron Company SimPlayers (active or not) to that spawn.
        /// Usage: [simchamp
        /// </summary>
        public static void SimChamp(Mobile from)
        {
            if (_instance == null)
            {
                from.SendMessage(0x22, "[SimPlayer] No manager instance found.");
                return;
            }

            // Collect eligible surface Felucca champion spawns
            var candidates = new List<ChampionSpawn>();
            foreach (ChampionSpawn cs in ChampionSystem.AllSpawns)
            {
                if (cs == null || cs.Deleted) continue;
                if (cs.Map != Map.Felucca)    continue;
                if (cs.Location.Z < -5)       continue; // skip underground altars
                candidates.Add(cs);
            }

            if (candidates.Count == 0)
            {
                from.SendMessage(0x22, "[SimPlayer] No surface Felucca ChampionSpawns found. Check spawn files.");
                return;
            }

            // Pick one at random
            ChampionSpawn target = candidates[Utility.Random(candidates.Count)];

            // Activate the spawn if it isn't already running
            bool wasActive = target.Active;
            if (!target.Active)
                target.Active = true;

            string spawnName = target.SpawnName ?? $"({target.X},{target.Y})";
            from.SendMessage(0x4AA,
                $"[SimPlayer] Spawn selected: {spawnName} at ({target.X},{target.Y}) " +
                $"— was {(wasActive ? "already active" : "activated now")}.");

            // Mobilize every Iron Company SimPlayer
            int sent = 0;
            foreach (SimPlayer sp in _instance._allSimPlayers)
            {
                if (sp.Deleted) continue;
                if (sp.GuildName != FBGuilds.IronCompany) continue;
                if (sp is IronCompanySimPlayer ic)
                {
                    string result = ic.ForceChampRunAt(target);
                    from.SendMessage(0x35, $"  {result}");
                    sent++;
                }
            }

            from.SendMessage(0x35, $"[SimPlayer] {sent} Iron Company member(s) mobilized.");
            from.SendMessage(0x35,  "[SimPlayer] Use [simgoto iron to follow them.");
        }

        // -- Iron Company coordination --------------------------------

        /// <summary>
        /// Called by any IronCompanySimPlayer when they start a champ run.
        /// Mobilises every other Iron Company member to the same spawn so
        /// all 18 participate together.
        /// </summary>
        public static void BroadcastChampRun(ChampionSpawn spawn, IronCompanySimPlayer initiator)
        {
            if (_instance == null || spawn == null || spawn.Deleted) return;

            foreach (SimPlayer sp in _instance._allSimPlayers)
            {
                if (sp.Deleted || sp == initiator) continue;
                if (sp.GuildName != FBGuilds.IronCompany) continue;
                if (sp is IronCompanySimPlayer ic)
                    ic.ForceChampRunAt(spawn);
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
