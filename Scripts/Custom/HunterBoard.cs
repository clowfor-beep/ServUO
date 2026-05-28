// ============================================================
// HunterBoard.cs
// Scripts/Custom/HunterBoard.cs
// ============================================================
//
// [hunterboard player command — opens a read-only gump showing
// the top 10 hunters by hunter points.
//
// Stateless — no Serialize/Deserialize needed.
// ============================================================

using Server;
using Server.Commands;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;
using System.Collections.Generic;

namespace Server.Custom
{
    public static class HunterBoard
    {
        public static void Initialize()
        {
            CommandSystem.Register("hunterboard", AccessLevel.Player, OnCommand);
        }

        private static void OnCommand(CommandEventArgs e)
        {
            e.Mobile.SendGump(new HunterBoardGump(e.Mobile));
        }

        // ── HunterBoardGump ─────────────────────────────────────────────────────

        private class HunterBoardGump : Gump
        {
            private const int W    = 480;
            private const int H    = 320;
            private const int PadX = 18;

            // Column x-offsets (rank#, name, points, rank title)
            private const int ColRank   = PadX;
            private const int ColName   = PadX + 30;
            private const int ColPoints = PadX + 30 + 180;
            private const int ColTitle  = PadX + 30 + 180 + 80;

            public HunterBoardGump(Mobile from) : base(60, 50)
            {
                var entries = HunterSystem.GetLeaderboard(10);

                AddBackground(0, 0, W, H, 9200);
                AddAlphaRegion(2, 2, W - 4, H - 4);

                // ── Title ────────────────────────────────────────────────────────
                AddLabel(PadX, 10, 0x53, "Hunter's Guild — Hall of Legends");

                // Close button (button ID 0)
                AddButton(W - 40, 8, 4017, 4019, 0, GumpButtonType.Reply, 0);

                // ── Column headers ───────────────────────────────────────────────
                AddLabel(ColRank,   45, 2119, "#");
                AddLabel(ColName,   45, 2119, "Name");
                AddLabel(ColPoints, 45, 2119, "Points");
                AddLabel(ColTitle,  45, 2119, "Rank");

                // ── Rows or empty state ──────────────────────────────────────────
                if (entries.Count == 0)
                {
                    AddLabel(PadX, 140, 2119, "No hunters yet — be the first!");
                }
                else
                {
                    int y = 70;
                    for (int i = 0; i < entries.Count; i++)
                    {
                        var (name, points, rankTitle) = entries[i];

                        int rowHue = i == 0 ? 0x53   // gold for #1
                                   : i == 1 ? 1153   // blue for #2
                                   : i == 2 ? 0x35   // green for #3
                                   : 2119;            // grey for the rest

                        AddLabel(ColRank,   y, rowHue, $"{i + 1}.");
                        AddLabel(ColName,   y, rowHue, name);
                        AddLabel(ColPoints, y, rowHue, points.ToString());
                        AddLabel(ColTitle,  y, 2119,   rankTitle);

                        y += 22;
                    }
                }
            }
        }
    }
}
