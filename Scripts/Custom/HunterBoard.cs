// ============================================================
// HunterBoard.cs
// Scripts/Custom/HunterBoard.cs
//
// [hunterboard command — opens a read-only gump showing the
// top 10 hunters on the shard by hunter points.
// ============================================================

using System;
using System.Collections.Generic;
using Server;
using Server.Commands;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;

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

        // ──────────────────────────────────────────────────────────────────────
        // Gump
        // ──────────────────────────────────────────────────────────────────────

        private class HunterBoardGump : Gump
        {
            public HunterBoardGump(Mobile from) : base(60, 60)
            {
                const int W = 480;
                const int H = 320;

                AddBackground(0, 0, W, H, 9200);
                AddAlphaRegion(2, 2, W - 4, H - 4);

                AddLabel(20, 10, 0x53, "Hunter's Guild — Hall of Legends");
                AddImageTiled(10, 30, W - 20, 1, 9264);

                // Column headers
                AddLabel(20,  45, 2119, "#");
                AddLabel(55,  45, 2119, "Name");
                AddLabel(240, 45, 2119, "Points");
                AddLabel(320, 45, 2119, "Rank");
                AddImageTiled(10, 63, W - 20, 1, 9264);

                var leaderboard = HunterSystem.GetLeaderboard(10);

                if (leaderboard.Count == 0)
                {
                    AddLabel(100, 140, 2119, "No hunters yet — be the first!");
                }
                else
                {
                    int y = 70;
                    for (int i = 0; i < leaderboard.Count; i++)
                    {
                        var entry = leaderboard[i];
                        // Gold for 1st, silver-blue for 2nd, green for 3rd, white the rest
                        int hue = i == 0 ? 0x53 : (i == 1 ? 0x3B2 : (i == 2 ? 0x35 : 1153));

                        AddLabel(20,  y, hue,  (i + 1).ToString());
                        AddLabel(55,  y, hue,  entry.name);
                        AddLabel(240, y, hue,  entry.points.ToString());
                        AddLabel(320, y, 2119, entry.rank);
                        y += 22;
                    }
                }

                AddButton(W / 2 - 30, H - 28, 4005, 4007, 0, GumpButtonType.Reply, 0);
                AddLabel(W / 2 + 6,   H - 28, 2119, "Close");
            }
        }
    }
}
