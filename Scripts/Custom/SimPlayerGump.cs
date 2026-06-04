// ============================================================
// SimPlayerGump.cs
// Scripts/Custom/SimPlayerGump.cs
//
// GM control panel for the SimPlayer system.
// Open with:  [simpanel
//
// Displays:
//   - Global actions: Status (chat), Champ Run, Reset Roster
//   - Per-guild rows: active/total count + Goto / Trigger / Info buttons
// ============================================================

using System.Collections.Generic;
using Server;
using Server.Custom;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Server.Custom
{
    public class SimPlayerGump : Gump
    {
        private readonly Mobile _from;

        // ── Button IDs ───────────────────────────────────────────────
        private const int BTN_CLOSE        = 0;
        private const int BTN_STATUS       = 1;
        private const int BTN_CHAMP        = 2;
        private const int BTN_RESET        = 3;
        private const int BTN_MONITOR      = 4;
        private const int BTN_GOTO_BASE    = 100;   // 100 + guild index
        private const int BTN_TRIGGER_BASE = 200;   // 200 + guild index
        private const int BTN_INFO_BASE    = 300;   // 300 + guild index

        // ── Layout ───────────────────────────────────────────────────
        private const int W        = 460;
        private const int PadX     = 12;
        private const int RowH     = 26;
        private const int HeaderH  = 110;  // space above guild table

        // Guild hues — colour by rough alignment, all bright enough for dark background
        private static readonly Dictionary<string, int> GuildHue = new Dictionary<string, int>
        {
            { FBGuilds.Wanderers,         0x481 },  // gold   — neutral travellers
            { FBGuilds.CraftsmenLeague,   0x481 },  // gold   — craftsmen
            { FBGuilds.ShadowHand,        1150  },  // silver — grey thieves
            { FBGuilds.IronCompany,       1153  },  // light blue
            { FBGuilds.ArcaneBrotherhood, 1152  },  // blue   — mages
            { FBGuilds.SilverWolves,      0x40  },  // green  — law/good
            { FBGuilds.PaladinOrder,      1153  },  // light blue — good
            { FBGuilds.DeadWatchers,      1150  },  // silver — dark neutral
            { FBGuilds.DreadHunters,      1153  },  // light blue — elite hunters
            { FBGuilds.BloodPact,         0x22  },  // red    — murderers
            { FBGuilds.TheVoid,           0x22  },  // red    — murderers
            { FBGuilds.Shadowblade,       1150  },  // silver — assassins
        };

        public SimPlayerGump(Mobile from) : base(40, 40)
        {
            _from = from;

            // ── Gather live stats ─────────────────────────────────────
            int totalRoster = 0, totalActive = 0;
            var guildTotal  = new Dictionary<string, int>();
            var guildActive = new Dictionary<string, int>();

            foreach (string g in FBGuilds.All) { guildTotal[g] = 0; guildActive[g] = 0; }

            var mgr = PlayerSimulatorManager.Instance;
            if (mgr != null)
            {
                foreach (SimPlayer sp in mgr.AllSimPlayers)
                {
                    if (sp == null || sp.Deleted) continue;
                    totalRoster++;
                    if (!guildTotal.ContainsKey(sp.GuildName))  guildTotal[sp.GuildName]  = 0;
                    if (!guildActive.ContainsKey(sp.GuildName)) guildActive[sp.GuildName] = 0;
                    guildTotal[sp.GuildName]++;
                    if (sp.Map != Map.Internal)
                    {
                        totalActive++;
                        guildActive[sp.GuildName]++;
                    }
                }
            }

            int H = HeaderH + FBGuilds.All.Length * RowH + 18;

            // ── Background ───────────────────────────────────────────
            AddBackground(0, 0, W, H, 9200);
            AddAlphaRegion(4, 4, W - 8, H - 8);

            // ── Title ────────────────────────────────────────────────
            AddLabel(PadX, 10, 1153, "SimPlayer Control Panel");
            AddLabel(PadX, 30, 2049,
                $"Roster: {totalRoster}   Active: {totalActive} / {PlayerSimulatorManager.MaxActiveSimultaneous}");

            // ── Divider ──────────────────────────────────────────────
            AddImageTiled(PadX, 52, W - PadX * 2, 2, 9304);

            // ── Global action buttons ─────────────────────────────────
            //   [Status]   [Champ Run]   [Reset Roster]
            int bx = PadX, by = 60;

            AddButton(bx, by, 4005, 4007, BTN_STATUS, GumpButtonType.Reply, 0);
            AddLabel(bx + 22, by + 3, 0x35, "Status (chat)");
            bx += 130;

            AddButton(bx, by, 4005, 4007, BTN_CHAMP, GumpButtonType.Reply, 0);
            AddLabel(bx + 22, by + 3, 1153, "Champ Run");
            bx += 120;

            AddButton(bx, by, 4005, 4007, BTN_RESET, GumpButtonType.Reply, 0);
            AddLabel(bx + 22, by + 3, 0x22, "Reset Roster");
            bx += 120;

            AddButton(bx, by, 4005, 4007, BTN_MONITOR, GumpButtonType.Reply, 0);
            AddLabel(bx + 22, by + 3, 0x35, "Monitor");

            // ── Divider ──────────────────────────────────────────────
            AddImageTiled(PadX, 88, W - PadX * 2, 2, 9304);

            // ── Column headers ────────────────────────────────────────
            int hy = 92;
            AddLabel(PadX,       hy, 2049, "Guild");
            AddLabel(W - 195,    hy, 2049, "In World");
            AddLabel(W - 145,    hy, 2049, "Goto");
            AddLabel(W - 105,    hy, 2049, "Trigger");
            AddLabel(W - 58,     hy, 2049, "Info");

            AddImageTiled(PadX, hy + 18, W - PadX * 2, 1, 9304);

            // ── Guild rows ────────────────────────────────────────────
            int gy = HeaderH;
            for (int i = 0; i < FBGuilds.All.Length; i++)
            {
                string guild  = FBGuilds.All[i];
                int    active = guildActive.ContainsKey(guild) ? guildActive[guild] : 0;
                int    total  = guildTotal.ContainsKey(guild)  ? guildTotal[guild]  : 0;

                // Alternating row tint
                if (i % 2 == 0)
                    AddImageTiled(PadX, gy, W - PadX * 2, RowH - 2, 9274);

                // Guild name
                int nameHue = GuildHue.ContainsKey(guild) ? GuildHue[guild] : 1153;
                AddLabel(PadX, gy + 4, nameHue, guild);

                // Active / total
                int countHue = active > 0 ? 0x40 : 2049;
                AddLabel(W - 188, gy + 4, countHue, $"{active}/{total}");

                // Goto
                AddButton(W - 148, gy + 3, 4005, 4007, BTN_GOTO_BASE + i, GumpButtonType.Reply, 0);

                // Trigger
                AddButton(W - 104, gy + 3, 4005, 4007, BTN_TRIGGER_BASE + i, GumpButtonType.Reply, 0);

                // Info
                AddButton(W - 58, gy + 3, 4005, 4007, BTN_INFO_BASE + i, GumpButtonType.Reply, 0);

                gy += RowH;
            }
        }

        public override void OnResponse(NetState sender, RelayInfo info)
        {
            if (!(_from is Mobile from)) return;

            int btn = info.ButtonID;
            if (btn == BTN_CLOSE) return;

            // ── Global actions ────────────────────────────────────────
            if (btn == BTN_STATUS)
            {
                PlayerSimulatorManager.SimStatus(from);
                from.SendGump(new SimPlayerGump(from));
                return;
            }

            if (btn == BTN_CHAMP)
            {
                PlayerSimulatorManager.SimChamp(from);
                from.SendGump(new SimPlayerGump(from));
                return;
            }

            if (btn == BTN_RESET)
            {
                PlayerSimulatorManager.SimReset(from);
                from.SendGump(new SimPlayerGump(from));
                return;
            }

            if (btn == BTN_MONITOR)
            {
                from.SendGump(new SimMonitorGump(from, 0));
                return;
            }

            // ── Per-guild Goto ────────────────────────────────────────
            if (btn >= BTN_GOTO_BASE && btn < BTN_GOTO_BASE + FBGuilds.All.Length)
            {
                PlayerSimulatorManager.SimGoto(from, FBGuilds.All[btn - BTN_GOTO_BASE]);
                from.SendGump(new SimPlayerGump(from));
                return;
            }

            // ── Per-guild Trigger ─────────────────────────────────────
            if (btn >= BTN_TRIGGER_BASE && btn < BTN_TRIGGER_BASE + FBGuilds.All.Length)
            {
                PlayerSimulatorManager.SimTrigger(from, FBGuilds.All[btn - BTN_TRIGGER_BASE]);
                from.SendGump(new SimPlayerGump(from));
                return;
            }

            // ── Per-guild Info ────────────────────────────────────────
            if (btn >= BTN_INFO_BASE && btn < BTN_INFO_BASE + FBGuilds.All.Length)
            {
                PlayerSimulatorManager.SimInfo(from, FBGuilds.All[btn - BTN_INFO_BASE]);
                from.SendGump(new SimPlayerGump(from));
                return;
            }
        }
    }
}
