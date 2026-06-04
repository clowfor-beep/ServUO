// ============================================================
// SimMonitorGump.cs
// Scripts/Custom/SimMonitorGump.cs
//
// Paginated monitor showing health status for every SimPlayer.
// Open with:  [simmonitor   (or the Monitor button in [simpanel)
//
// Columns:  ●  Name  Guild  State  Phase  [Goto] [Fix]
// Health dot:  green = Healthy | yellow = Warning | red = Stuck
// Guild filter bar across the top — click any guild to filter.
// ============================================================

using System;
using System.Collections.Generic;
using Server;
using Server.Custom;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Server.Custom
{
    public class SimMonitorGump : Gump
    {
        private readonly Mobile _from;
        private readonly int    _page;
        private readonly string _guildFilter; // null = show all

        private const int ROWS_PER_PAGE = 14;

        // ── Button IDs ───────────────────────────────────────────────
        private const int BTN_CLOSE        = 0;
        private const int BTN_PREV         = 1;
        private const int BTN_NEXT         = 2;
        private const int BTN_FIX_ALL      = 3;
        private const int BTN_REFRESH      = 4;
        private const int BTN_BACK         = 5;
        private const int BTN_FILTER_ALL   = 6;
        private const int BTN_FILTER_BASE  = 100;  // 100 + FBGuilds.All index
        private const int BTN_GOTO_BASE    = 1000; // 1000 + snapshot index
        private const int BTN_FIX_BASE     = 2000; // 2000 + snapshot index

        // ── Colours ──────────────────────────────────────────────────
        private const int HUE_HEALTHY  = 0x40;   // green
        private const int HUE_WARNING  = 0x35;   // yellow/gold
        private const int HUE_STUCK    = 0x22;   // red
        private const int HUE_HEADER   = 1153;   // light blue
        private const int HUE_DIM      = 2049;   // grey
        private const int HUE_SELECTED = 0x481;  // gold — active filter

        // ── Layout ───────────────────────────────────────────────────
        private const int W       = 590;
        private const int PadX    = 10;
        private const int RowH    = 22;
        private const int HeaderH = 132; // title + summary + actions + filter bar + col headers

        // Short display names for the filter bar (must match FBGuilds.All order)
        private static readonly string[] GuildShort =
        {
            "Wanderers", "Craftsmen", "ShadowHand", "IronCo",
            "Arcane", "SilverWolves", "Paladin", "DeadWatch",
            "DreadHunt", "BloodPact", "TheVoid", "Shadowblade",
        };

        public SimMonitorGump(Mobile from, int page, string guildFilter = null) : base(30, 30)
        {
            _from        = from;
            _page        = page;
            _guildFilter = guildFilter;
            bool isGM    = from.AccessLevel >= AccessLevel.GameMaster;

            // ── Collect + filter SimPlayers ───────────────────────────
            var all = new List<SimPlayer>();
            var mgr = PlayerSimulatorManager.Instance;
            if (mgr != null)
            {
                foreach (SimPlayer sp in mgr.AllSimPlayers)
                {
                    if (sp == null || sp.Deleted) continue;
                    if (guildFilter != null && sp.GuildName != guildFilter) continue;
                    all.Add(sp);
                }
            }

            // Sort: Stuck first, then Warning, then Healthy; alpha within each group
            all.Sort((a, b) =>
            {
                int ha = (int)a.GetHealth(), hb = (int)b.GetHealth();
                if (hb != ha) return hb.CompareTo(ha);
                return string.Compare(a.MemberName, b.MemberName, StringComparison.Ordinal);
            });

            int total    = all.Count;
            int maxPage  = Math.Max(0, (total - 1) / ROWS_PER_PAGE);
            int safePage = Math.Max(0, Math.Min(page, maxPage));
            int start    = safePage * ROWS_PER_PAGE;
            int end      = Math.Min(start + ROWS_PER_PAGE, total);

            // ── Summary counts (over full filtered set) ───────────────
            int nHealthy = 0, nWarning = 0, nStuck = 0;
            foreach (SimPlayer sp in all)
            {
                switch (sp.GetHealth())
                {
                    case SimPlayer.SimHealthStatus.Healthy: nHealthy++; break;
                    case SimPlayer.SimHealthStatus.Warning: nWarning++; break;
                    case SimPlayer.SimHealthStatus.Stuck:   nStuck++;   break;
                }
            }

            int H = HeaderH + (end - start) * RowH + 28;

            // ── Background ───────────────────────────────────────────
            AddBackground(0, 0, W, H, 9200);
            AddAlphaRegion(4, 4, W - 8, H - 8);

            // ── Title row ────────────────────────────────────────────
            AddLabel(PadX, 10, HUE_HEADER, "SimPlayer Monitor");
            string filterLabel = guildFilter != null
                ? string.Format("[ {0} ]  Page {1}/{2}  ({3})", guildFilter, safePage + 1, maxPage + 1, total)
                : string.Format("Page {0}/{1}   Total: {2}", safePage + 1, maxPage + 1, total);
            AddLabel(PadX + 175, 10, guildFilter != null ? HUE_SELECTED : HUE_DIM, filterLabel);

            // ── Health summary ────────────────────────────────────────
            AddLabel(PadX,       30, HUE_HEALTHY, string.Format("● Healthy: {0}", nHealthy));
            AddLabel(PadX + 110, 30, HUE_WARNING, string.Format("● Warning: {0}", nWarning));
            AddLabel(PadX + 220, 30, HUE_STUCK,   string.Format("● Stuck: {0}",   nStuck));

            // ── Action buttons ────────────────────────────────────────
            int bx = PadX, by = 52;
            AddButton(bx, by, 4005, 4007, BTN_REFRESH, GumpButtonType.Reply, 0);
            AddLabel(bx + 22, by + 3, HUE_DIM, "Refresh");
            bx += 88;

            if (isGM)
            {
                AddButton(bx, by, 4005, 4007, BTN_FIX_ALL, GumpButtonType.Reply, 0);
                AddLabel(bx + 22, by + 3, HUE_STUCK, "Fix All Stuck");
                bx += 115;
            }

            AddButton(bx, by, 4005, 4007, BTN_BACK, GumpButtonType.Reply, 0);
            AddLabel(bx + 22, by + 3, HUE_DIM, "Back to Panel");

            // ── Guild filter bar ──────────────────────────────────────
            // Row 1: [All] + guilds 0–5
            // Row 2: guilds 6–11
            AddImageTiled(PadX, 74, W - PadX * 2, 1, 9304);

            int fx = PadX, fy = 78;
            bool allActive = (guildFilter == null);
            int allHue = allActive ? HUE_SELECTED : HUE_DIM;
            AddButton(fx, fy, 4005, 4007, BTN_FILTER_ALL, GumpButtonType.Reply, 0);
            AddLabel(fx + 22, fy + 3, allHue, allActive ? "[All]" : "All");
            fx += 46;

            for (int gi = 0; gi < FBGuilds.All.Length; gi++)
            {
                if (gi == 6) { fx = PadX; fy += 20; } // wrap to row 2

                string shortName  = gi < GuildShort.Length ? GuildShort[gi] : FBGuilds.All[gi];
                bool   isSelected = (guildFilter == FBGuilds.All[gi]);
                int    btnHue     = isSelected ? HUE_SELECTED : HUE_DIM;
                string label      = isSelected ? string.Format("[{0}]", shortName) : shortName;

                AddButton(fx, fy, 4005, 4007, BTN_FILTER_BASE + gi, GumpButtonType.Reply, 0);
                AddLabel(fx + 22, fy + 3, btnHue, label);
                fx += shortName.Length * 7 + 28; // rough proportional spacing
            }

            // ── Column headers ────────────────────────────────────────
            AddImageTiled(PadX, 116, W - PadX * 2, 2, 9304);
            int hy = 120;
            AddLabel(PadX,       hy, HUE_DIM, "●");
            AddLabel(PadX + 16,  hy, HUE_DIM, "Name");
            AddLabel(PadX + 155, hy, HUE_DIM, "Guild");
            AddLabel(PadX + 265, hy, HUE_DIM, "State");
            AddLabel(PadX + 345, hy, HUE_DIM, "Phase");
            AddLabel(W - 95,     hy, HUE_DIM, "Goto");
            AddLabel(W - 50,     hy, HUE_DIM, "Fix");
            AddImageTiled(PadX, hy + 16, W - PadX * 2, 1, 9304);

            // ── Rows ─────────────────────────────────────────────────
            int ry = HeaderH;
            for (int i = start; i < end; i++)
            {
                SimPlayer sp = all[i];
                int rowHue;
                switch (sp.GetHealth())
                {
                    case SimPlayer.SimHealthStatus.Stuck:   rowHue = HUE_STUCK;   break;
                    case SimPlayer.SimHealthStatus.Warning: rowHue = HUE_WARNING; break;
                    default:                                rowHue = HUE_HEALTHY; break;
                }

                if ((i - start) % 2 == 0)
                    AddImageTiled(PadX, ry, W - PadX * 2, RowH - 2, 9274);

                string state = sp.Map == Map.Internal ? "Inactive" : sp.State.ToString();
                string phase = sp.GetStatusDetail();
                if (phase.Length > 22) phase = phase.Substring(0, 21) + "…";

                string name  = sp.MemberName.Length > 18
                    ? sp.MemberName.Substring(0, 17) + "…" : sp.MemberName;
                string guild = sp.GuildName.Length > 13
                    ? sp.GuildName.Substring(0, 12) + "…" : sp.GuildName;

                AddLabel(PadX,       ry + 3, rowHue,      "●");
                AddLabel(PadX + 16,  ry + 3, rowHue,      name);
                AddLabel(PadX + 155, ry + 3, HUE_DIM,     guild);
                AddLabel(PadX + 265, ry + 3, HUE_DIM,     state);
                AddLabel(PadX + 345, ry + 3, HUE_WARNING, phase);

                AddButton(W - 98, ry + 2, 4005, 4007, BTN_GOTO_BASE + i, GumpButtonType.Reply, 0);
                if (isGM)
                    AddButton(W - 50, ry + 2, 4005, 4007, BTN_FIX_BASE + i, GumpButtonType.Reply, 0);

                ry += RowH;
            }

            // ── Pagination ────────────────────────────────────────────
            AddImageTiled(PadX, H - 26, W - PadX * 2, 1, 9304);
            int px = PadX, py = H - 22;

            if (safePage > 0)
            {
                AddButton(px, py, 4014, 4016, BTN_PREV, GumpButtonType.Reply, 0);
                AddLabel(px + 20, py + 2, HUE_DIM, "Prev");
                px += 65;
            }

            if (safePage < maxPage)
            {
                AddButton(px, py, 4005, 4007, BTN_NEXT, GumpButtonType.Reply, 0);
                AddLabel(px + 22, py + 2, HUE_DIM, "Next");
            }

            _snapshot = all;
        }

        private readonly List<SimPlayer> _snapshot;

        public override void OnResponse(NetState sender, RelayInfo info)
        {
            if (!(_from is Mobile from)) return;

            int btn = info.ButtonID;
            if (btn == BTN_CLOSE) return;

            bool isGM = from.AccessLevel >= AccessLevel.GameMaster;

            if (btn == BTN_REFRESH)
            {
                from.SendGump(new SimMonitorGump(from, _page, _guildFilter));
                return;
            }

            if (btn == BTN_BACK)
            {
                from.SendGump(new SimPlayerGump(from));
                return;
            }

            if (btn == BTN_FIX_ALL && isGM)
            {
                PlayerSimulatorManager.SimFixAll(from);
                from.SendGump(new SimMonitorGump(from, _page, _guildFilter));
                return;
            }

            if (btn == BTN_PREV)
            {
                from.SendGump(new SimMonitorGump(from, _page - 1, _guildFilter));
                return;
            }

            if (btn == BTN_NEXT)
            {
                from.SendGump(new SimMonitorGump(from, _page + 1, _guildFilter));
                return;
            }

            // Guild filter — All
            if (btn == BTN_FILTER_ALL)
            {
                from.SendGump(new SimMonitorGump(from, 0, null));
                return;
            }

            // Guild filter — specific guild
            if (btn >= BTN_FILTER_BASE && btn < BTN_FILTER_BASE + FBGuilds.All.Length)
            {
                string selected = FBGuilds.All[btn - BTN_FILTER_BASE];
                // Toggle off if already selected
                string next = (selected == _guildFilter) ? null : selected;
                from.SendGump(new SimMonitorGump(from, 0, next));
                return;
            }

            // Goto
            if (btn >= BTN_GOTO_BASE && btn < BTN_GOTO_BASE + _snapshot.Count)
            {
                SimPlayer sp = _snapshot[btn - BTN_GOTO_BASE];
                if (!sp.Deleted && sp.Map != null && sp.Map != Map.Internal)
                    from.MoveToWorld(sp.Location, sp.Map);
                else
                    from.SendMessage(0x22, string.Format("{0} is not in the world.", sp.MemberName));
                from.SendGump(new SimMonitorGump(from, _page, _guildFilter));
                return;
            }

            // Fix (GM only)
            if (isGM && btn >= BTN_FIX_BASE && btn < BTN_FIX_BASE + _snapshot.Count)
            {
                SimPlayer sp = _snapshot[btn - BTN_FIX_BASE];
                sp.AutoFix();
                from.SendMessage(0x35, string.Format("Fixed: {0} ({1})", sp.MemberName, sp.GuildName));
                from.SendGump(new SimMonitorGump(from, _page, _guildFilter));
                return;
            }
        }
    }
}
