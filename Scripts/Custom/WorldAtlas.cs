// ============================================================
// WorldAtlas.cs  (v4 — full dungeon list from Regions.xml)
// Scripts/Custom/WorldAtlas.cs
//
// All dungeon entrance coordinates sourced directly from
// <entrance> and <go> tags in Regions.xml, with correct
// map facets verified from <Facet name="..."> blocks.
// ============================================================

using System;
using System.Collections.Generic;
using Server;
using Server.Custom;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Server.Items
{
    public class AtlasLocation
    {
        public readonly string Name;
        public readonly int    X, Y, Z;
        public readonly Map    Map;

        public AtlasLocation(string name, int x, int y, int z, Map map)
        {
            Name = name; X = x; Y = y; Z = z; Map = map;
        }
    }

    public enum AtlasCategory { Towns = 0, Dungeons = 1, Moongates = 2 }

    // ============================================================
    // ITEM
    // ============================================================

    public class WorldAtlas : Item
    {
        [Constructable]
        public WorldAtlas() : base(0x14ED)
        {
            Name     = "World Atlas";
            Hue       = 2213;
            LootType = LootType.Blessed;
            Weight   = 1.0;
        }

        public WorldAtlas(Serial serial) : base(serial) { }

        public override void OnDoubleClick(Mobile from)
        {
            if (!IsChildOf(from.Backpack))
            {
                from.SendLocalizedMessage(1042001);
                return;
            }

            if (from is PlayerMobile pm)
            {
                pm.CloseGump(typeof(AtlasGump));
                pm.SendGump(new AtlasGump(pm, AtlasCategory.Towns, 0));
            }
        }

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write(0);
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            reader.ReadInt();
        }
    }

    // ============================================================
    // GUMP
    // ============================================================

    public class AtlasGump : Gump
    {
        private const int W           = 540;
        private const int H           = 420;
        private const int LeftX       = 25;
        private const int RightX      = 278;
        private const int RightW      = 238;
        private const int RowH        = 22;
        private const int LocsPerPage = 14;

        private const int Btn_Towns    = 1;
        private const int Btn_Dungeons = 2;
        private const int Btn_Gates    = 3;
        private const int Btn_Prev     = 4;
        private const int Btn_Next     = 5;
        private const int Btn_Loc      = 100;

        // -------------------------------------------------------
        // TOWNS
        // -------------------------------------------------------
        private static readonly List<AtlasLocation> Towns = new List<AtlasLocation>
        {
            new AtlasLocation("Britain",         1495, 1629, 10, Map.Felucca),
            new AtlasLocation("Trinsic",         1867, 2780,  0, Map.Felucca),
            new AtlasLocation("Minoc",           2466,  544,  0, Map.Felucca),
            new AtlasLocation("Vesper",          2899,  676,  0, Map.Felucca),
            new AtlasLocation("Cove",            2275, 1210,  0, Map.Felucca),
            new AtlasLocation("Skara Brae",       632, 2233,  0, Map.Felucca),
            new AtlasLocation("Yew",              546,  992,  0, Map.Felucca),
            new AtlasLocation("Jhelom",          1383, 3815,  0, Map.Felucca),
            new AtlasLocation("Moonglow",        4467, 1284,  0, Map.Felucca),
            new AtlasLocation("Magincia",        3714, 2220, 20, Map.Felucca),
            new AtlasLocation("Nujel'm",        3732, 1279,  0, Map.Felucca),
            new AtlasLocation("Ocllo",           3650, 2519,  0, Map.Felucca),
            new AtlasLocation("Serpent's Hold", 3010, 3371, 15, Map.Felucca),
            new AtlasLocation("Papua",           5769, 3176,  0, Map.Felucca),
            new AtlasLocation("Delucia",         5228, 3978, 37, Map.Felucca),
            new AtlasLocation("New Haven",       3503, 2574, 14, Map.Trammel),
            new AtlasLocation("Luna",             989,  520,-50, Map.Malas),
            new AtlasLocation("Umbra",           2049, 1344,-85, Map.Malas),
            new AtlasLocation("Zento",            736, 1256, 30, Map.Tokuno),
            new AtlasLocation("Makoto-Jima",      802, 1204, 25, Map.Tokuno),
            new AtlasLocation("Mistas",           818, 1073,-30, Map.Ilshenar),
        };

        // -------------------------------------------------------
        // DUNGEONS — all entrance coordinates from Regions.xml
        // Organised by facet: Felucca → Malas → Ilshenar → Tokuno → TerMur
        // -------------------------------------------------------
        private static readonly List<AtlasLocation> Dungeons = new List<AtlasLocation>
        {
            // ---- Felucca (classic + T2A + ML) ----
            new AtlasLocation("Deceit",           4111,  429,  0, Map.Felucca),
            new AtlasLocation("Despise",          1296, 1082,  0, Map.Felucca),
            new AtlasLocation("Destard",          1176, 2635,  0, Map.Felucca),
            new AtlasLocation("Shame",             512, 1559,  0, Map.Felucca),
            new AtlasLocation("Wrong",            2042,  226,  0, Map.Felucca),
            new AtlasLocation("Covetous",         2499,  916,  0, Map.Felucca),
            new AtlasLocation("Hythloth",         4722, 3814,  0, Map.Felucca),
            new AtlasLocation("Ice Dungeon",      1996,   80,  0, Map.Felucca),
            new AtlasLocation("Fire Dungeon",     2922, 3402,  0, Map.Felucca),
            new AtlasLocation("Wind",             1997,   81,  0, Map.Felucca),
            new AtlasLocation("Khaldun",          5882, 3819,  0, Map.Felucca),
            new AtlasLocation("Orc Cave",         1014, 1434,  0, Map.Felucca),
            new AtlasLocation("Blighted Grove",    587, 1641,  0, Map.Felucca),
            new AtlasLocation("Sanctuary",         764, 1646,  0, Map.Felucca),
            new AtlasLocation("Prism of Light",   3784, 1097,  0, Map.Felucca),
            new AtlasLocation("Solen Hives",      5774, 1896, 20, Map.Felucca),
            new AtlasLocation("Painted Caves",    1716,  892,  0, Map.Felucca),
            new AtlasLocation("Terathan Keep",    5426, 3120,  0, Map.Felucca),

            // ---- Malas ----
            new AtlasLocation("Doom",             2357, 1268,  0, Map.Malas),
            new AtlasLocation("Bedlam",           2068, 1372,  0, Map.Malas),

            // ---- Ilshenar ----
            new AtlasLocation("Exodus Dungeon",    827,  777,  0, Map.Ilshenar),
            new AtlasLocation("Spectre Dungeon",  1362, 1031,  0, Map.Ilshenar),
            new AtlasLocation("Spider Cave",      1420,  910,  0, Map.Ilshenar),
            new AtlasLocation("Rock Dungeon",     1788,  571,  0, Map.Ilshenar),

            // ---- Tokuno ----
            new AtlasLocation("Yomotsu Mines",     259,  783,  0, Map.Tokuno),
            new AtlasLocation("Fan Dancer's Dojo", 977,  223,  0, Map.Tokuno),

            // ---- Ter Mur ----
            new AtlasLocation("Stygian Abyss",     985,  366,-11, Map.TerMur),
            new AtlasLocation("Underworld",       1128, 1207, -2, Map.TerMur),
        };

        // -------------------------------------------------------
        // MOONGATES
        // -------------------------------------------------------
        private static readonly List<AtlasLocation> Moongates = new List<AtlasLocation>
        {
            new AtlasLocation("Moonglow",        4467, 1284,  0, Map.Felucca),
            new AtlasLocation("Britain",         1495, 1629, 10, Map.Felucca),
            new AtlasLocation("Jhelom",          1383, 3815,  0, Map.Felucca),
            new AtlasLocation("Yew",              546,  992,  0, Map.Felucca),
            new AtlasLocation("Minoc",           2466,  544,  0, Map.Felucca),
            new AtlasLocation("Trinsic",         1867, 2780,  0, Map.Felucca),
            new AtlasLocation("Skara Brae",       632, 2233,  0, Map.Felucca),
            new AtlasLocation("Magincia",        3714, 2220, 20, Map.Felucca),
            new AtlasLocation("Ilshenar Gate",    1223,  475,-16, Map.Ilshenar),
            new AtlasLocation("Malas Gate",        989,  520,-50, Map.Malas),
            new AtlasLocation("Tokuno Gate",      1169,  998,  0, Map.Tokuno),
        };

        // -------------------------------------------------------
        // Instance
        // -------------------------------------------------------
        private readonly PlayerMobile  _player;
        private readonly AtlasCategory _cat;
        private readonly int           _page;

        public AtlasGump(PlayerMobile player, AtlasCategory cat, int page)
            : base(60, 60)
        {
            _player   = player;
            _cat      = cat;
            _page     = page;
            Closable  = true;
            Dragable  = true;
            Resizable = false;
            Build();
        }

        private List<AtlasLocation> Locs =>
            _cat == AtlasCategory.Towns    ? Towns    :
            _cat == AtlasCategory.Dungeons ? Dungeons : Moongates;

        private void Build()
        {
            AddBackground(0, 0, W, H, 2200);
            AddAlphaRegion(8, 8, W - 16, H - 16);
            AddImageTiled(260, 15, 3, H - 30, 9264);

            AddHtml(0, 12, W, 22,
                "<CENTER><BASEFONT COLOR=#C8A428><BIG>World Atlas</BIG></BASEFONT></CENTER>",
                false, false);

            // LEFT PAGE — categories
            AddHtml(LeftX, 48, 220, 18,
                "<BASEFONT COLOR=#888866>— Travel —</BASEFONT>", false, false);

            DrawCatButton(Btn_Towns,    80,  "Towns",     AtlasCategory.Towns);
            DrawCatButton(Btn_Dungeons, 115, "Dungeons",  AtlasCategory.Dungeons);
            DrawCatButton(Btn_Gates,    150, "Moongates", AtlasCategory.Moongates);

            AddHtml(LeftX, 210, 220, 160,
                "<BASEFONT COLOR=#776655>" +
                "Touch the gem beside a location to begin travel.<BR><BR>" +
                "You will be held in place for 3 seconds before departing.<BR><BR>" +
                "Being killed during the delay will cancel travel." +
                "</BASEFONT>",
                false, false);

            // RIGHT PAGE — locations
            var locs       = Locs;
            int totalPages = Math.Max(1, (locs.Count + LocsPerPage - 1) / LocsPerPage);
            int start      = _page * LocsPerPage;
            int end        = Math.Min(start + LocsPerPage, locs.Count);

            AddHtml(RightX, 48, RightW, 18,
                String.Format("<BASEFONT COLOR=#C8A428>{0}</BASEFONT>", _cat),
                false, false);

            AddHtml(RightX,       68, 150, 16, "<BASEFONT COLOR=#555544>Location</BASEFONT>", false, false);
            AddHtml(RightX + 165, 68,  44, 16, "<BASEFONT COLOR=#555544>Map</BASEFONT>",      false, false);

            int y = 88;
            for (int i = start; i < end; i++)
            {
                var loc = locs[i];

                if ((i - start) % 2 == 0)
                    AddImageTiled(RightX - 2, y - 1, RightW - 10, RowH - 2, 9264);

                AddHtml(RightX, y, 155, RowH,
                    String.Format("<BASEFONT COLOR=#DDCCAA>{0}</BASEFONT>", loc.Name),
                    false, false);

                AddHtml(RightX + 158, y, 44, RowH,
                    String.Format("<BASEFONT COLOR=#777766>{0}</BASEFONT>", MapAbbr(loc.Map)),
                    false, false);

                AddItem  (RightX + 198, y,     0x4BF);
                AddButton(RightX + 197, y - 1, 4005, 4007, Btn_Loc + i, GumpButtonType.Reply, 0);

                y += RowH;
            }

            // Pagination
            if (totalPages > 1)
            {
                int midX = RightX + RightW / 2;

                if (_page > 0)
                    AddButton(midX - 60, H - 38, 9909, 9911, Btn_Prev, GumpButtonType.Reply, 0);

                AddHtml(midX - 20, H - 36, 60, 18,
                    String.Format("<CENTER><BASEFONT COLOR=#666655>{0}/{1}</BASEFONT></CENTER>",
                        _page + 1, totalPages),
                    false, false);

                if (_page < totalPages - 1)
                    AddButton(midX + 40, H - 38, 9903, 9905, Btn_Next, GumpButtonType.Reply, 0);
            }
        }

        private void DrawCatButton(int btnId, int y, string label, AtlasCategory cat)
        {
            bool   active = _cat == cat;
            string color  = active ? "#C8A428" : "#886644";
            int    icon   = active ? 4006 : 4005;

            AddButton(LeftX,      y,     icon, 4007, btnId, GumpButtonType.Reply, 0);
            AddHtml  (LeftX + 30, y + 2, 180, 18,
                String.Format("<BASEFONT COLOR={0}>{1}</BASEFONT>", color, label),
                false, false);
        }

        private static string MapAbbr(Map m)
        {
            if (m == Map.Felucca)  return "Fel";
            if (m == Map.Trammel)  return "Tra";
            if (m == Map.Ilshenar) return "Ils";
            if (m == Map.Malas)    return "Mal";
            if (m == Map.Tokuno)   return "Tok";
            if (m == Map.TerMur)   return "Ter";
            return "?";
        }

        public override void OnResponse(NetState sender, RelayInfo info)
        {
            switch (info.ButtonID)
            {
                case 0: return;

                case Btn_Towns:
                    _player.CloseGump(typeof(AtlasGump));
                    _player.SendGump(new AtlasGump(_player, AtlasCategory.Towns, 0));
                    return;

                case Btn_Dungeons:
                    _player.CloseGump(typeof(AtlasGump));
                    _player.SendGump(new AtlasGump(_player, AtlasCategory.Dungeons, 0));
                    return;

                case Btn_Gates:
                    _player.CloseGump(typeof(AtlasGump));
                    _player.SendGump(new AtlasGump(_player, AtlasCategory.Moongates, 0));
                    return;

                case Btn_Prev:
                    _player.CloseGump(typeof(AtlasGump));
                    _player.SendGump(new AtlasGump(_player, _cat, _page - 1));
                    return;

                case Btn_Next:
                    _player.CloseGump(typeof(AtlasGump));
                    _player.SendGump(new AtlasGump(_player, _cat, _page + 1));
                    return;
            }

            int idx  = info.ButtonID - Btn_Loc;
            var locs = Locs;

            if (idx >= 0 && idx < locs.Count)
                BeginTravel(_player, locs[idx]);
        }

        private static void BeginTravel(PlayerMobile pm, AtlasLocation dest)
        {
            if (pm == null || pm.Deleted || !pm.Alive)
            {
                pm?.SendMessage(0x26, "You cannot travel in your current state.");
                return;
            }

            if (pm.Frozen)
            {
                pm.SendMessage(0x26, "You are already preparing to travel.");
                return;
            }

            pm.SendMessage(0x35, "The atlas draws you toward {0}. Hold still...", dest.Name);
            pm.Frozen = true;

            CooldownSystem.Start(pm, "Travelling", 3.0);

            Timer.DelayCall(TimeSpan.FromSeconds(3.0), () =>
            {
                if (pm == null || pm.Deleted)
                    return;

                pm.Frozen = false;

                if (!pm.Alive)
                {
                    pm.SendMessage(0x26, "The journey was interrupted.");
                    return;
                }

                Effects.SendLocationParticles(
                    EffectItem.Create(pm.Location, pm.Map, EffectItem.DefaultDuration),
                    0x3728, 10, 10, 2023);
                Effects.PlaySound(pm.Location, pm.Map, 0x1FE);

                pm.MoveToWorld(new Point3D(dest.X, dest.Y, dest.Z), dest.Map);

                Effects.SendLocationParticles(
                    EffectItem.Create(pm.Location, pm.Map, EffectItem.DefaultDuration),
                    0x3728, 10, 10, 2023);
                Effects.PlaySound(pm.Location, pm.Map, 0x1FE);

                pm.SendMessage(0x35, "You arrive at {0}.", dest.Name);
            });
        }
    }
}
