using System;
using System.Collections.Generic;
using Server;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;
using Server.Engines.CannedEvil;

namespace Server.Custom.ArchWizard
{
    // ============================================================
    // Page state enum — controls what is shown in the gump
    // ============================================================
    public enum ArchWizardPage
    {
        Main,
        Dungeons,
        ChampionSpawns
    }

    // ============================================================
    // Dungeon destination data
    // ============================================================
    public class DungeonDestination
    {
        public string Name;
        public Point3D Location;
        public Map     Map;

        public DungeonDestination(string name, Point3D loc, Map map)
        {
            Name     = name;
            Location = loc;
            Map      = map;
        }
    }

    // ============================================================
    // Champion spawn info (built at runtime)
    // ============================================================
    public class ChampionDestination
    {
        public string   Name;
        public Point3D  Location;
        public Map      Map;
        public ChampionSpawn Spawn;

        public ChampionDestination(string name, Point3D loc, Map map, ChampionSpawn spawn)
        {
            Name     = name;
            Location = loc;
            Map      = map;
            Spawn    = spawn;
        }
    }

    // ============================================================
    // GUMP
    // ============================================================
    public class ArchWizardGump : Gump
    {
        // ── Layout constants ─────────────────────────────────────
        private const int GumpX     = 100;
        private const int GumpY     = 100;
        private const int GumpW     = 440;
        private const int GumpH     = 520;
        private const int BtnW      = 200;
        private const int BtnH      = 30;
        private const int ColLeft   = 30;
        private const int ColRight  = 240;
        private const int RowStart  = 120;
        private const int RowStep   = 36;

        // ── State ────────────────────────────────────────────────
        private readonly PlayerMobile  _player;
        private readonly ArchWizardNPC _npc;
        private readonly ArchWizardPage _page;

        // Destination lists — populated per page
        private List<DungeonDestination>   _dungeons;
        private List<ChampionDestination>  _champions;

        // Button IDs
        private const int BtnClose       = 0;
        private const int BtnDungeons    = 1;
        private const int BtnChampions   = 2;
        private const int BtnBack        = 3;
        private const int BtnDestBase    = 100;   // BtnDestBase + index = destination button


        // ============================================================
        // DUNGEON DATA
        // All locations are Felucca entrance coordinates for each level.
        // Adjust Point3D values if your server map differs.
        // ============================================================
        private static readonly DungeonDestination[] AllDungeons =
        {
            // Covetous
            new DungeonDestination("Covetous – Level 1",  new Point3D(2500,  975, 0),   Map.Felucca),
            new DungeonDestination("Covetous – Level 2",  new Point3D(2479, 1016, -20), Map.Felucca),
            new DungeonDestination("Covetous – Level 3",  new Point3D(2358,  974, -40), Map.Felucca),

            // Deceit
            new DungeonDestination("Deceit – Level 1",    new Point3D(1300,  170,   0), Map.Felucca),
            new DungeonDestination("Deceit – Level 2",    new Point3D(1350,  400, -20), Map.Felucca),
            new DungeonDestination("Deceit – Level 3",    new Point3D(1450,  175, -40), Map.Felucca),
            new DungeonDestination("Deceit – Level 4",    new Point3D(1475,  480, -60), Map.Felucca),

            // Despise
            new DungeonDestination("Despise – Level 1",   new Point3D(1298, 1081,   0), Map.Felucca),
            new DungeonDestination("Despise – Level 2",   new Point3D(1297, 1172,  20), Map.Felucca),
            new DungeonDestination("Despise – Level 3",   new Point3D(1302, 1338,   0), Map.Felucca),

            // Destard
            new DungeonDestination("Destard – Level 1",   new Point3D(1176, 2640,   0), Map.Felucca),
            new DungeonDestination("Destard – Level 2",   new Point3D(1176, 2800, -20), Map.Felucca),

            // Hythloth
            new DungeonDestination("Hythloth – Level 1",  new Point3D( 722, 3814,   0), Map.Felucca),
            new DungeonDestination("Hythloth – Level 2",  new Point3D( 722, 3900, -20), Map.Felucca),
            new DungeonDestination("Hythloth – Level 3",  new Point3D( 722, 4000, -40), Map.Felucca),
            new DungeonDestination("Hythloth – Level 4",  new Point3D( 722, 4100, -60), Map.Felucca),

            // Shame
            new DungeonDestination("Shame – Level 1",     new Point3D( 512, 1559,   0), Map.Felucca),
            new DungeonDestination("Shame – Level 2",     new Point3D( 512, 1700, -20), Map.Felucca),
            new DungeonDestination("Shame – Level 3",     new Point3D( 512, 1850, -40), Map.Felucca),
            new DungeonDestination("Shame – Level 4",     new Point3D( 512, 2000, -60), Map.Felucca),
            new DungeonDestination("Shame – Level 5",     new Point3D( 512, 2150, -80), Map.Felucca),

            // Wrong
            new DungeonDestination("Wrong – Level 1",     new Point3D(2040,  213,   0), Map.Felucca),
            new DungeonDestination("Wrong – Level 2",     new Point3D(2040,  310, -20), Map.Felucca),
            new DungeonDestination("Wrong – Level 3",     new Point3D(2040,  440, -40), Map.Felucca),

            // Khaldun
            new DungeonDestination("Khaldun – Level 1",   new Point3D(5765, 2098,   0), Map.Felucca),
            new DungeonDestination("Khaldun – Level 2",   new Point3D(5850, 2098, -20), Map.Felucca),

            // Orc Caves
            new DungeonDestination("Orc Caves – Level 1", new Point3D(1036, 1649,   0), Map.Felucca),
            new DungeonDestination("Orc Caves – Level 2", new Point3D(1036, 1750, -20), Map.Felucca),

            // Terathan Keep
            new DungeonDestination("Terathan Keep – L1",  new Point3D(2571,  772,   0), Map.Felucca),
            new DungeonDestination("Terathan Keep – L2",  new Point3D(2571,  860, -20), Map.Felucca),

            // Ice Dungeon
            new DungeonDestination("Ice – Level 1",       new Point3D(1999,   81,   0), Map.Felucca),
            new DungeonDestination("Ice – Level 2",       new Point3D(1999,  200, -20), Map.Felucca),

            // Fire Dungeon
            new DungeonDestination("Fire – Level 1",      new Point3D(2923, 3438,   0), Map.Felucca),
            new DungeonDestination("Fire – Level 2",      new Point3D(2923, 3550, -20), Map.Felucca),
        };

        // ============================================================
        // Pagination
        // ============================================================
        private const int ItemsPerPage = 10;
        private readonly int _pageIndex;   // which page of results we are on (0-based)


        // ============================================================
        // CONSTRUCTOR
        // ============================================================
        public ArchWizardGump(PlayerMobile player, ArchWizardNPC npc,
                              ArchWizardPage page = ArchWizardPage.Main,
                              int pageIndex = 0)
            : base(GumpX, GumpY)
        {
            _player    = player;
            _npc       = npc;
            _page      = page;
            _pageIndex = pageIndex;

            if (_page == ArchWizardPage.ChampionSpawns)
                _champions = GetActiveChampionSpawns();

            Closable   = true;
            Disposable = true;
            Dragable   = true;
            Resizable  = false;

            BuildGump();
        }

        // ============================================================
        // CHAMPION SPAWN SCANNER
        // Iterates World.Items for active ChampionSpawn objects on Felucca.
        // ============================================================
        private static List<ChampionDestination> GetActiveChampionSpawns()
        {
            var list = new List<ChampionDestination>();

            foreach (Item item in World.Items.Values)
            {
                var cs = item as ChampionSpawn;
                if (cs == null)          continue;
                if (!cs.Active)          continue;
                if (cs.Map != Map.Felucca) continue;
                if (cs.Deleted)          continue;

                // Build a readable name from the ChampionSpawn type
                string name = GetChampionName(cs);
                list.Add(new ChampionDestination(name, cs.Location, cs.Map, cs));
            }

            // Sort alphabetically so the list is predictable
            list.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            return list;
        }

        private static string GetChampionName(ChampionSpawn cs)
        {
            // ChampionSpawn stores its type in the SpawnType property
            string typeName = cs.Type.ToString();

            switch (typeName)
            {
                case "Abyss":          return "The Abyss (Daemon Spawn)";
                case "Arachnid":       return "Arachnid (Spider Spawn)";
                case "ColdBlood":      return "Cold Blood (Reptile Spawn)";
                case "ForestLord":     return "Forest Lord (Beast Spawn)";
                case "Unholy Terror":
                case "UnholyTerror":   return "Unholy Terror (Undead Spawn)";
                case "VerminHorde":    return "Vermin Horde (Rat Spawn)";
                case "Sleeping Dragon":
                case "SleepingDragon": return "Sleeping Dragon";
                case "Terathan":       return "Terathan Matriarch";
                case "Orc":            return "Orc Spawn";
                default:               return typeName + " Spawn";
            }
        }

        // ============================================================
        // GUMP BUILDER
        // ============================================================
        private void BuildGump()
        {
            AddBackground(0, 0, GumpW, GumpH, 9200);
            AddAlphaRegion(10, 10, GumpW - 20, GumpH - 20);

            // Title bar
            AddImageTiled(10, 10, GumpW - 20, 40, 9304);
            AddLabel(GumpW / 2 - 80, 20, 1153, "The Arch Wizard");

            switch (_page)
            {
                case ArchWizardPage.Main:          BuildMainPage();     break;
                case ArchWizardPage.Dungeons:      BuildDungeonPage();  break;
                case ArchWizardPage.ChampionSpawns: BuildChampionPage(); break;
            }
        }

        // ── MAIN PAGE ────────────────────────────────────────────
        private void BuildMainPage()
        {
            AddLabel(ColLeft, 65, 1152,
                "Where would you like to travel today?");

            AddLabel(ColLeft, RowStart - 20, 1153, "Dungeon Levels");
            AddLabel(ColLeft, RowStart - 5, 0,
                "(" + ArchWizardNPC.CostDungeon + " gold from bank)");

            AddButton(ColLeft, RowStart + 20, 4005, 4007, BtnDungeons, GumpButtonType.Reply, 0);
            AddLabel(ColLeft + 35, RowStart + 22, 1152, "Felucca Dungeons");

            AddLabel(ColLeft, RowStart + 80, 1153, "Champion Spawns");
            AddLabel(ColLeft, RowStart + 95, 0,
                "(" + ArchWizardNPC.CostChampion + " gold from bank)");

            AddButton(ColLeft, RowStart + 115, 4005, 4007, BtnChampions, GumpButtonType.Reply, 0);
            AddLabel(ColLeft + 35, RowStart + 117, 1152, "Active Felucca Spawns");

            // Close
            AddButton(GumpW / 2 - 40, GumpH - 50, 4017, 4019, BtnClose, GumpButtonType.Reply, 0);
            AddLabel(GumpW / 2 - 15, GumpH - 48, 33, "Close");
        }

        // ── DUNGEON PAGE ─────────────────────────────────────────
        private void BuildDungeonPage()
        {
            int totalItems = AllDungeons.Length;
            int totalPages = (int)Math.Ceiling(totalItems / (double)ItemsPerPage);
            int startIdx   = _pageIndex * ItemsPerPage;
            int endIdx     = Math.Min(startIdx + ItemsPerPage, totalItems);

            AddLabel(ColLeft, 60, 1152, "Felucca Dungeons  —  " + ArchWizardNPC.CostDungeon + "gp each");
            AddLabel(ColLeft, 75, 0,   "Gold is deducted from your bank.");

            int btnId = BtnDestBase;
            int y     = RowStart - 20;

            for (int i = startIdx; i < endIdx; i++)
            {
                var dest  = AllDungeons[i];
                bool canAfford = ArchWizardNPC.HasBankGold(_player, ArchWizardNPC.CostDungeon);

                AddButton(ColLeft, y, 4005, 4007, btnId, GumpButtonType.Reply, 0);
                AddLabel(ColLeft + 35, y + 2, canAfford ? 1152 : 33, dest.Name);
                btnId++;
                y += RowStep;
            }

            // Pagination
            if (_pageIndex > 0)
            {
                AddButton(ColLeft, GumpH - 55, 4014, 4016, 50, GumpButtonType.Reply, 0);
                AddLabel(ColLeft + 35, GumpH - 53, 1152, "Previous");
            }
            if (_pageIndex < totalPages - 1)
            {
                AddButton(ColLeft + 130, GumpH - 55, 4005, 4007, 51, GumpButtonType.Reply, 0);
                AddLabel(ColLeft + 165, GumpH - 53, 1152, "Next");
            }

            // Back
            AddButton(GumpW - 100, GumpH - 55, 4017, 4019, BtnBack, GumpButtonType.Reply, 0);
            AddLabel(GumpW - 65, GumpH - 53, 33, "Back");
        }

        // ── CHAMPION PAGE ─────────────────────────────────────────
        private void BuildChampionPage()
        {
            AddLabel(ColLeft, 60, 1152, "Active Champion Spawns  —  " + ArchWizardNPC.CostChampion + "gp each");
            AddLabel(ColLeft, 75, 0,   "Gold is deducted from your bank.");

            if (_champions.Count == 0)
            {
                AddLabel(ColLeft, RowStart, 33,
                    "No champion spawns are currently active in Felucca.");
                AddLabel(ColLeft, RowStart + 20, 0,
                    "Check back when a spawn has been activated.");
            }
            else
            {
                int totalPages = (int)Math.Ceiling(_champions.Count / (double)ItemsPerPage);
                int startIdx   = _pageIndex * ItemsPerPage;
                int endIdx     = Math.Min(startIdx + ItemsPerPage, _champions.Count);

                int btnId = BtnDestBase;
                int y     = RowStart - 20;

                for (int i = startIdx; i < endIdx; i++)
                {
                    var dest       = _champions[i];
                    bool canAfford = ArchWizardNPC.HasBankGold(_player, ArchWizardNPC.CostChampion);

                    // Show spawn progress if available
                    int creatureCount = dest.Spawn.Creatures != null ? dest.Spawn.Creatures.Count : 0;
                    int pct = dest.Spawn.Active && creatureCount > 0 ? Math.Min(100, dest.Spawn.Level * 25) : 0;

                    string label = dest.Name + "  [" + pct + "% spawned]";

                    AddButton(ColLeft, y, 4005, 4007, btnId, GumpButtonType.Reply, 0);
                    AddLabel(ColLeft + 35, y + 2, canAfford ? 1152 : 33, label);
                    btnId++;
                    y += RowStep;
                }

                // Pagination
                if (_pageIndex > 0)
                {
                    AddButton(ColLeft, GumpH - 55, 4014, 4016, 50, GumpButtonType.Reply, 0);
                    AddLabel(ColLeft + 35, GumpH - 53, 1152, "Previous");
                }
                if (_champions.Count > (_pageIndex + 1) * ItemsPerPage)
                {
                    AddButton(ColLeft + 130, GumpH - 55, 4005, 4007, 51, GumpButtonType.Reply, 0);
                    AddLabel(ColLeft + 165, GumpH - 53, 1152, "Next");
                }
            }

            // Back
            AddButton(GumpW - 100, GumpH - 55, 4017, 4019, BtnBack, GumpButtonType.Reply, 0);
            AddLabel(GumpW - 65, GumpH - 53, 33, "Back");
        }

        // ============================================================
        // BUTTON HANDLER
        // ============================================================
        public override void OnResponse(NetState sender, RelayInfo info)
        {
            if (_player == null || _player.Deleted) return;
            if (_npc    == null || _npc.Deleted)    return;

            int buttonId = info.ButtonID;

            switch (buttonId)
            {
                case BtnClose:
                    // Gump closes — do nothing
                    return;

                case BtnBack:
                    _player.SendGump(new ArchWizardGump(_player, _npc, ArchWizardPage.Main));
                    return;

                case BtnDungeons:
                    _player.SendGump(new ArchWizardGump(_player, _npc, ArchWizardPage.Dungeons, 0));
                    return;

                case BtnChampions:
                    _player.SendGump(new ArchWizardGump(_player, _npc, ArchWizardPage.ChampionSpawns, 0));
                    return;

                case 50:   // Previous page
                    _player.SendGump(new ArchWizardGump(_player, _npc, _page, _pageIndex - 1));
                    return;

                case 51:   // Next page
                    _player.SendGump(new ArchWizardGump(_player, _npc, _page, _pageIndex + 1));
                    return;
            }

            // Destination buttons — index = buttonId - BtnDestBase
            if (buttonId >= BtnDestBase)
            {
                int idx = buttonId - BtnDestBase;

                if (_page == ArchWizardPage.Dungeons)
                {
                    int realIdx = _pageIndex * ItemsPerPage + idx;
                    if (realIdx < 0 || realIdx >= AllDungeons.Length) return;

                    var dest = AllDungeons[realIdx];
                    _npc.TeleportPlayer(_player, dest.Location, dest.Map, ArchWizardNPC.CostDungeon);
                }
                else if (_page == ArchWizardPage.ChampionSpawns)
                {
                    // Rescan so we have fresh data
                    var champions = GetActiveChampionSpawns();
                    int realIdx   = _pageIndex * ItemsPerPage + idx;

                    if (realIdx < 0 || realIdx >= champions.Count)
                    {
                        _player.SendMessage("That spawn is no longer active.");
                        return;
                    }

                    var dest = champions[realIdx];

                    if (!dest.Spawn.Active || dest.Spawn.Deleted)
                    {
                        _player.SendMessage("That spawn is no longer active.");
                        _player.SendGump(new ArchWizardGump(_player, _npc, ArchWizardPage.ChampionSpawns, 0));
                        return;
                    }

                    // Teleport to a safe spot just outside the spawn radius
                    Point3D safeLoc = new Point3D(
                        dest.Location.X + 5,
                        dest.Location.Y + 5,
                        dest.Location.Z);

                    _npc.TeleportPlayer(_player, safeLoc, dest.Map, ArchWizardNPC.CostChampion);
                }
            }
        }
    }
}
