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
        ChampionSpawns,
        SelectService       // choose ticket type for a specific destination
    }

    // ============================================================
    // Portal / ticket type
    // ============================================================
    public enum PortalType
    {
        OneWay,         // instant one-way teleport
        TwoWayShort,    // two-way portal, 30 seconds
        TwoWayLong      // two-way portal, 10 minutes
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
        private const int GumpX    = 100;
        private const int GumpY    = 100;
        private const int GumpW    = 440;
        private const int GumpH    = 520;
        private const int ColLeft  = 30;
        private const int RowStart = 120;
        private const int RowStep  = 36;

        // ── State ────────────────────────────────────────────────
        private readonly PlayerMobile   _player;
        private readonly ArchWizardNPC  _npc;
        private readonly ArchWizardPage _page;
        private readonly int            _pageIndex;     // pagination index
        private readonly int            _selectedIdx;   // dest index for SelectService page
        private readonly bool           _isDungeon;     // true = dungeon dest, false = champ dest

        private List<ChampionDestination> _champions;

        // ── Button IDs ───────────────────────────────────────────
        private const int BtnClose        = 0;
        private const int BtnDungeons     = 1;
        private const int BtnChampions    = 2;
        private const int BtnBack         = 3;
        private const int BtnPrevPage     = 50;
        private const int BtnNextPage     = 51;
        private const int BtnOneWay       = 60;
        private const int BtnTwoWayShort  = 61;
        private const int BtnTwoWayLong   = 62;
        private const int BtnDestBase     = 100;   // + index = destination button

        // ============================================================
        // DUNGEON DATA
        // ============================================================
        private static readonly DungeonDestination[] AllDungeons =
        {
            new DungeonDestination("Covetous – Level 1",  new Point3D(2500,  975,   0), Map.Felucca),
            new DungeonDestination("Covetous – Level 2",  new Point3D(2479, 1016, -20), Map.Felucca),
            new DungeonDestination("Covetous – Level 3",  new Point3D(2358,  974, -40), Map.Felucca),

            new DungeonDestination("Deceit – Level 1",    new Point3D(1300,  170,   0), Map.Felucca),
            new DungeonDestination("Deceit – Level 2",    new Point3D(1350,  400, -20), Map.Felucca),
            new DungeonDestination("Deceit – Level 3",    new Point3D(1450,  175, -40), Map.Felucca),
            new DungeonDestination("Deceit – Level 4",    new Point3D(1475,  480, -60), Map.Felucca),

            new DungeonDestination("Despise – Level 1",   new Point3D(1298, 1081,   0), Map.Felucca),
            new DungeonDestination("Despise – Level 2",   new Point3D(1297, 1172,  20), Map.Felucca),
            new DungeonDestination("Despise – Level 3",   new Point3D(1302, 1338,   0), Map.Felucca),

            new DungeonDestination("Destard – Level 1",   new Point3D(1176, 2640,   0), Map.Felucca),
            new DungeonDestination("Destard – Level 2",   new Point3D(1176, 2800, -20), Map.Felucca),

            new DungeonDestination("Hythloth – Level 1",  new Point3D( 722, 3814,   0), Map.Felucca),
            new DungeonDestination("Hythloth – Level 2",  new Point3D( 722, 3900, -20), Map.Felucca),
            new DungeonDestination("Hythloth – Level 3",  new Point3D( 722, 4000, -40), Map.Felucca),
            new DungeonDestination("Hythloth – Level 4",  new Point3D( 722, 4100, -60), Map.Felucca),

            new DungeonDestination("Shame – Level 1",     new Point3D( 512, 1559,   0), Map.Felucca),
            new DungeonDestination("Shame – Level 2",     new Point3D( 512, 1700, -20), Map.Felucca),
            new DungeonDestination("Shame – Level 3",     new Point3D( 512, 1850, -40), Map.Felucca),
            new DungeonDestination("Shame – Level 4",     new Point3D( 512, 2000, -60), Map.Felucca),
            new DungeonDestination("Shame – Level 5",     new Point3D( 512, 2150, -80), Map.Felucca),

            new DungeonDestination("Wrong – Level 1",     new Point3D(2040,  213,   0), Map.Felucca),
            new DungeonDestination("Wrong – Level 2",     new Point3D(2040,  310, -20), Map.Felucca),
            new DungeonDestination("Wrong – Level 3",     new Point3D(2040,  440, -40), Map.Felucca),

            new DungeonDestination("Khaldun – Level 1",   new Point3D(5765, 2098,   0), Map.Felucca),
            new DungeonDestination("Khaldun – Level 2",   new Point3D(5850, 2098, -20), Map.Felucca),

            new DungeonDestination("Orc Caves – Level 1", new Point3D(1036, 1649,   0), Map.Felucca),
            new DungeonDestination("Orc Caves – Level 2", new Point3D(1036, 1750, -20), Map.Felucca),

            new DungeonDestination("Terathan Keep – L1",  new Point3D(2571,  772,   0), Map.Felucca),
            new DungeonDestination("Terathan Keep – L2",  new Point3D(2571,  860, -20), Map.Felucca),

            new DungeonDestination("Ice – Level 1",       new Point3D(1999,   81,   0), Map.Felucca),
            new DungeonDestination("Ice – Level 2",       new Point3D(1999,  200, -20), Map.Felucca),

            new DungeonDestination("Fire – Level 1",      new Point3D(2923, 3438,   0), Map.Felucca),
            new DungeonDestination("Fire – Level 2",      new Point3D(2923, 3550, -20), Map.Felucca),
        };

        private const int ItemsPerPage = 10;

        // ============================================================
        // CONSTRUCTOR
        // ============================================================
        public ArchWizardGump(PlayerMobile player, ArchWizardNPC npc,
                              ArchWizardPage page        = ArchWizardPage.Main,
                              int            pageIndex   = 0,
                              int            selectedIdx = -1,
                              bool           isDungeon   = true)
            : base(GumpX, GumpY)
        {
            _player      = player;
            _npc         = npc;
            _page        = page;
            _pageIndex   = pageIndex;
            _selectedIdx = selectedIdx;
            _isDungeon   = isDungeon;

            if (_page == ArchWizardPage.ChampionSpawns || (_page == ArchWizardPage.SelectService && !_isDungeon))
                _champions = GetActiveChampionSpawns();

            Closable   = true;
            Disposable = true;
            Dragable   = true;
            Resizable  = false;

            BuildGump();
        }

        // ============================================================
        // CHAMPION SPAWN SCANNER
        // ============================================================
        private static List<ChampionDestination> GetActiveChampionSpawns()
        {
            var list = new List<ChampionDestination>();
            foreach (Item item in World.Items.Values)
            {
                var cs = item as ChampionSpawn;
                if (cs == null || !cs.Active || cs.Deleted) continue;
                string name = GetChampionName(cs) + " (" + cs.Map.Name + ")";
                list.Add(new ChampionDestination(name, cs.Location, cs.Map, cs));
            }
            list.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            return list;
        }

        private static string GetChampionName(ChampionSpawn cs)
        {
            string typeName = cs.Type.ToString();
            switch (typeName)
            {
                case "Abyss":                             return "The Abyss (Daemon Spawn)";
                case "Arachnid":                          return "Arachnid (Spider Spawn)";
                case "ColdBlood":                         return "Cold Blood (Reptile Spawn)";
                case "ForestLord":                        return "Forest Lord (Beast Spawn)";
                case "Unholy Terror": case "UnholyTerror": return "Unholy Terror (Undead Spawn)";
                case "VerminHorde":                       return "Vermin Horde (Rat Spawn)";
                case "Sleeping Dragon": case "SleepingDragon": return "Sleeping Dragon";
                case "Terathan":                          return "Terathan Matriarch";
                case "Orc":                               return "Orc Spawn";
                default:                                  return typeName + " Spawn";
            }
        }

        // ============================================================
        // GUMP BUILDER
        // ============================================================
        private void BuildGump()
        {
            AddBackground(0, 0, GumpW, GumpH, 9200);
            AddAlphaRegion(10, 10, GumpW - 20, GumpH - 20);
            AddImageTiled(10, 10, GumpW - 20, 40, 9304);
            AddLabel(GumpW / 2 - 80, 20, 1153, "The Arch Wizard");

            switch (_page)
            {
                case ArchWizardPage.Main:           BuildMainPage();          break;
                case ArchWizardPage.Dungeons:       BuildDungeonPage();       break;
                case ArchWizardPage.ChampionSpawns: BuildChampionPage();      break;
                case ArchWizardPage.SelectService:  BuildSelectServicePage(); break;
            }
        }

        // ── MAIN PAGE ────────────────────────────────────────────
        private void BuildMainPage()
        {
            AddLabel(ColLeft, 65, 1152, "Where would you like to travel today?");

            int y = RowStart - 20;

            AddLabel(ColLeft, y,      1153, "Dungeon Levels");
            AddLabel(ColLeft, y + 15, 1152, "From " + ArchWizardNPC.CostDungeon + "gp");
            AddButton(ColLeft, y + 35, 4005, 4007, BtnDungeons, GumpButtonType.Reply, 0);
            AddLabel(ColLeft + 35, y + 37, 1152, "Felucca Dungeons");

            y += 90;

            AddLabel(ColLeft, y,      1153, "Champion Spawns");
            AddLabel(ColLeft, y + 15, 1152, "From " + ArchWizardNPC.CostChampion + "gp");
            AddButton(ColLeft, y + 35, 4005, 4007, BtnChampions, GumpButtonType.Reply, 0);
            AddLabel(ColLeft + 35, y + 37, 1152, "Active Champion Spawns");

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

            AddLabel(ColLeft, 60, 1152, "Felucca Dungeons — choose a destination");
            AddLabel(ColLeft, 75, 1152, "You will select a ticket type on the next screen.");

            int btnId = BtnDestBase;
            int y     = RowStart - 20;

            for (int i = startIdx; i < endIdx; i++)
            {
                var dest = AllDungeons[i];
                int baseLabel = ArchWizardNPC.HasGold(_player, ArchWizardNPC.CostDungeon) ? 1152 : 33;
                AddButton(ColLeft, y, 4005, 4007, btnId, GumpButtonType.Reply, 0);
                AddLabel(ColLeft + 35, y + 2, baseLabel, dest.Name);
                btnId++;
                y += RowStep;
            }

            AddPaginationAndBack(totalPages);
        }

        // ── CHAMPION PAGE ─────────────────────────────────────────
        private void BuildChampionPage()
        {
            AddLabel(ColLeft, 60, 1152, "Active Champion Spawns — choose a destination");
            AddLabel(ColLeft, 75, 1152, "You will select a ticket type on the next screen.");

            if (_champions.Count == 0)
            {
                AddLabel(ColLeft, RowStart, 33,
                    "No champion spawns are currently active.");
                AddLabel(ColLeft, RowStart + 20, 0, "Check back when a spawn has been activated.");
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
                    int baseLabel  = ArchWizardNPC.HasGold(_player, ArchWizardNPC.CostChampion) ? 1152 : 33;
                    int pct        = dest.Spawn.Active ? Math.Min(100, dest.Spawn.Level * 25) : 0;
                    string label   = dest.Name + "  [" + pct + "% spawned]";

                    AddButton(ColLeft, y, 4005, 4007, btnId, GumpButtonType.Reply, 0);
                    AddLabel(ColLeft + 35, y + 2, baseLabel, label);
                    btnId++;
                    y += RowStep;
                }

                AddPaginationAndBack(totalPages);
                return;
            }

            AddBackButton();
        }

        // ── SELECT SERVICE PAGE ───────────────────────────────────
        private void BuildSelectServicePage()
        {
            int basePrice = _isDungeon ? ArchWizardNPC.CostDungeon : ArchWizardNPC.CostChampion;
            int priceOneWay      = basePrice;
            int priceTwoWayShort = basePrice * 2;
            int priceTwoWayLong  = basePrice * 5;

            // Destination name
            string destName = GetSelectedDestName();
            AddLabel(ColLeft, 60, 1153, destName);
            AddLabel(ColLeft, 78, 1152, "Choose your travel option:");

            int y = RowStart;

            // Option 1 — One-way ticket
            bool canOneWay = ArchWizardNPC.HasGold(_player, priceOneWay);
            AddButton(ColLeft, y, 4005, 4007, BtnOneWay, GumpButtonType.Reply, 0);
            AddLabel(ColLeft + 35, y + 2,  canOneWay ? 1152 : 33, "One-Way Ticket");
            AddLabel(ColLeft + 35, y + 18, canOneWay ? 1152 : 33, priceOneWay + " gold — teleports you instantly, no return.");
            y += 55;

            // Option 2 — Two-way portal, 30 seconds
            bool canShort = ArchWizardNPC.HasGold(_player, priceTwoWayShort);
            AddButton(ColLeft, y, 4005, 4007, BtnTwoWayShort, GumpButtonType.Reply, 0);
            AddLabel(ColLeft + 35, y + 2,  canShort ? 1152 : 33, "Express Portal  (30 seconds)");
            AddLabel(ColLeft + 35, y + 18, canShort ? 1152 : 33, priceTwoWayShort + " gold — two-way portal, open for 30 seconds.");
            y += 55;

            // Option 3 — Two-way portal, 10 minutes
            bool canLong = ArchWizardNPC.HasGold(_player, priceTwoWayLong);
            AddButton(ColLeft, y, 4005, 4007, BtnTwoWayLong, GumpButtonType.Reply, 0);
            AddLabel(ColLeft + 35, y + 2,  canLong ? 1152 : 33, "Sustained Portal  (10 minutes)");
            AddLabel(ColLeft + 35, y + 18, canLong ? 1152 : 33, priceTwoWayLong + " gold — two-way portal, open for 10 minutes.");

            AddBackButton();
        }

        private string GetSelectedDestName()
        {
            if (_isDungeon)
            {
                if (_selectedIdx >= 0 && _selectedIdx < AllDungeons.Length)
                    return AllDungeons[_selectedIdx].Name;
            }
            else
            {
                if (_champions != null && _selectedIdx >= 0 && _selectedIdx < _champions.Count)
                    return _champions[_selectedIdx].Name;
            }
            return "Unknown Destination";
        }

        // ── Shared pagination helpers ─────────────────────────────
        private void AddPaginationAndBack(int totalPages)
        {
            if (_pageIndex > 0)
            {
                AddButton(ColLeft, GumpH - 55, 4014, 4016, BtnPrevPage, GumpButtonType.Reply, 0);
                AddLabel(ColLeft + 35, GumpH - 53, 1152, "Previous");
            }
            if (_pageIndex < totalPages - 1)
            {
                AddButton(ColLeft + 130, GumpH - 55, 4005, 4007, BtnNextPage, GumpButtonType.Reply, 0);
                AddLabel(ColLeft + 165, GumpH - 53, 1152, "Next");
            }
            AddBackButton();
        }

        private void AddBackButton()
        {
            AddButton(GumpW - 100, GumpH - 55, 4017, 4019, BtnBack, GumpButtonType.Reply, 0);
            AddLabel(GumpW - 65,  GumpH - 53, 33, "Back");
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
                case BtnClose: return;

                case BtnBack:
                    if (_page == ArchWizardPage.SelectService)
                    {
                        // Go back to the appropriate dest list
                        var prevPage = _isDungeon ? ArchWizardPage.Dungeons : ArchWizardPage.ChampionSpawns;
                        _player.SendGump(new ArchWizardGump(_player, _npc, prevPage, _pageIndex));
                    }
                    else
                    {
                        _player.SendGump(new ArchWizardGump(_player, _npc, ArchWizardPage.Main));
                    }
                    return;

                case BtnDungeons:
                    _player.SendGump(new ArchWizardGump(_player, _npc, ArchWizardPage.Dungeons));
                    return;

                case BtnChampions:
                    _player.SendGump(new ArchWizardGump(_player, _npc, ArchWizardPage.ChampionSpawns));
                    return;

                case BtnPrevPage:
                    _player.SendGump(new ArchWizardGump(_player, _npc, _page, _pageIndex - 1));
                    return;

                case BtnNextPage:
                    _player.SendGump(new ArchWizardGump(_player, _npc, _page, _pageIndex + 1));
                    return;

                // ── Service selection (on SelectService page) ─────
                case BtnOneWay:
                case BtnTwoWayShort:
                case BtnTwoWayLong:
                    HandleServiceSelected(buttonId);
                    return;
            }

            // ── Destination selected — go to service selection page ──
            if (buttonId >= BtnDestBase)
            {
                int localIdx = buttonId - BtnDestBase;
                int realIdx  = _pageIndex * ItemsPerPage + localIdx;

                if (_page == ArchWizardPage.Dungeons)
                {
                    if (realIdx < 0 || realIdx >= AllDungeons.Length) return;
                    _player.SendGump(new ArchWizardGump(
                        _player, _npc, ArchWizardPage.SelectService,
                        _pageIndex, realIdx, isDungeon: true));
                }
                else if (_page == ArchWizardPage.ChampionSpawns)
                {
                    var champs = GetActiveChampionSpawns();
                    if (realIdx < 0 || realIdx >= champs.Count)
                    {
                        _player.SendMessage("That spawn is no longer active.");
                        return;
                    }
                    _player.SendGump(new ArchWizardGump(
                        _player, _npc, ArchWizardPage.SelectService,
                        _pageIndex, realIdx, isDungeon: false));
                }
            }
        }

        private void HandleServiceSelected(int buttonId)
        {
            // Resolve destination
            Point3D destLoc;
            Map     destMap;
            int     basePrice;

            if (_isDungeon)
            {
                if (_selectedIdx < 0 || _selectedIdx >= AllDungeons.Length) return;
                var d   = AllDungeons[_selectedIdx];
                destLoc   = d.Location;
                destMap   = d.Map;
                basePrice = ArchWizardNPC.CostDungeon;
            }
            else
            {
                var champs = GetActiveChampionSpawns();
                if (_selectedIdx < 0 || _selectedIdx >= champs.Count)
                {
                    _player.SendMessage("That spawn is no longer active.");
                    _player.SendGump(new ArchWizardGump(_player, _npc, ArchWizardPage.ChampionSpawns));
                    return;
                }
                var c = champs[_selectedIdx];
                if (!c.Spawn.Active || c.Spawn.Deleted)
                {
                    _player.SendMessage("That spawn is no longer active.");
                    _player.SendGump(new ArchWizardGump(_player, _npc, ArchWizardPage.ChampionSpawns));
                    return;
                }
                destLoc   = new Point3D(c.Location.X + 5, c.Location.Y + 5, c.Location.Z);
                destMap   = c.Map;
                basePrice = ArchWizardNPC.CostChampion;
            }

            PortalType type;
            int cost;

            switch (buttonId)
            {
                case BtnTwoWayShort:
                    type = PortalType.TwoWayShort;
                    cost = basePrice * 2;
                    break;
                case BtnTwoWayLong:
                    type = PortalType.TwoWayLong;
                    cost = basePrice * 5;
                    break;
                default:
                    type = PortalType.OneWay;
                    cost = basePrice;
                    break;
            }

            _npc.TeleportPlayer(_player, destLoc, destMap, cost, type);
        }
    }
}
