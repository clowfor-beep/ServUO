// ============================================================
// SimMonitorGump.cs
// Scripts/Custom/SimMonitorGump.cs
//
// Paginated monitor showing health status for every SimPlayer.
// Open with:  [simmonitor   (or the Monitor button in [simpanel)
//
// Columns:  ●  Name  Guild  State  Phase  [Goto] [Fix]
// Health dot:  green = Healthy | yellow = Warning | red = Stuck
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

        private const int ROWS_PER_PAGE = 16;

        // ── Button IDs ───────────────────────────────────────────────
        private const int BTN_CLOSE     = 0;
        private const int BTN_PREV      = 1;
        private const int BTN_NEXT      = 2;
        private const int BTN_FIX_ALL   = 3;
        private const int BTN_REFRESH   = 4;
        private const int BTN_BACK      = 5;
        private const int BTN_GOTO_BASE = 1000;   // 1000 + absolute index
        private const int BTN_FIX_BASE  = 2000;   // 2000 + absolute index

        // ── Colours ──────────────────────────────────────────────────
        private const int HUE_HEALTHY = 0x40;   // green
        private const int HUE_WARNING = 0x35;   // yellow/gold
        private const int HUE_STUCK   = 0x22;   // red
        private const int HUE_HEADER  = 1153;   // light blue
        private const int HUE_DIM     = 2049;   // grey

        // ── Layout ───────────────────────────────────────────────────
        private const int W      = 590;
        private const int PadX   = 10;
        private const int RowH   = 22;
        private const int HeaderH = 96;

        public SimMonitorGump(Mobile from, int page) : base(30, 30)
        {
            _from = from;
            _page = page;

            // ── Collect all SimPlayers ────────────────────────────────
            var all = new List<SimPlayer>();
            var mgr = PlayerSimulatorManager.Instance;
            if (mgr != null)
            {
                foreach (SimPlayer sp in mgr.AllSimPlayers)
                {
                    if (sp != null && !sp.Deleted)
                        all.Add(sp);
                }
            }

            // Sort: Stuck first, then Warning, then Healthy; alpha within each group
            all.Sort((a, b) =>
            {
                int ha = (int)a.GetHealth(), hb = (int)b.GetHealth();
                if (hb != ha) return hb.CompareTo(ha); // higher enum value = worse
                return string.Compare(a.MemberName, b.MemberName, System.StringComparison.Ordinal);
            });

            int total    = all.Count;
            int maxPage  = Math.Max(0, (total - 1) / ROWS_PER_PAGE);
            int safePage = Math.Max(0, Math.Min(page, maxPage));
            int start    = safePage * ROWS_PER_PAGE;
            int end      = Math.Min(start + ROWS_PER_PAGE, total);

            // ── Summary counts ────────────────────────────────────────
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

            int H = HeaderH + (end - start) * RowH + 30;

            // ── Background ───────────────────────────────────────────
            AddBackground(0, 0, W, H, 9200);
            AddAlphaRegion(4, 4, W - 8, H - 8);

            // ── Title ────────────────────────────────────────────────
            AddLabel(PadX, 10, HUE_HEADER, "SimPlayer Monitor");
            AddLabel(PadX + 180, 10, HUE_DIM,
                string.Format("Page {0}/{1}   Total: {2}", safePage + 1, maxPage + 1, total));

            // ── Health summary ────────────────────────────────────────
            AddLabel(PadX,       30, HUE_HEALTHY, string.Format("● Healthy: {0}", nHealthy));
            AddLabel(PadX + 110, 30, HUE_WARNING, string.Format("● Warning: {0}", nWarning));
            AddLabel(PadX + 220, 30, HUE_STUCK,   string.Format("● Stuck: {0}",   nStuck));

            // ── Action buttons ────────────────────────────────────────
            int bx = PadX, by = 54;

            AddButton(bx, by, 4005, 4007, BTN_REFRESH, GumpButtonType.Reply, 0);
            AddLabel(bx + 22, by + 3, HUE_DIM, "Refresh");
            bx += 90;

            AddButton(bx, by, 4005, 4007, BTN_FIX_ALL, GumpButtonType.Reply, 0);
            AddLabel(bx + 22, by + 3, HUE_STUCK, "Fix All Stuck");
            bx += 120;

            AddButton(bx, by, 4005, 4007, BTN_BACK, GumpButtonType.Reply, 0);
            AddLabel(bx + 22, by + 3, HUE_DIM, "Back to Panel");

            // ── Divider + column headers ──────────────────────────────
            AddImageTiled(PadX, 78, W - PadX * 2, 2, 9304);
            int hy = 82;
            AddLabel(PadX,       hy, HUE_DIM, "●");
            AddLabel(PadX + 16,  hy, HUE_DIM, "Name");
            AddLabel(PadX + 155, hy, HUE_DIM, "Guild");
            AddLabel(PadX + 270, hy, HUE_DIM, "State");
            AddLabel(PadX + 350, hy, HUE_DIM, "Phase");
            AddLabel(W - 95,     hy, HUE_DIM, "Goto");
            AddLabel(W - 50,     hy, HUE_DIM, "Fix");
            AddImageTiled(PadX, hy + 16, W - PadX * 2, 1, 9304);

            // ── Rows ─────────────────────────────────────────────────
            int ry = HeaderH;
            for (int i = start; i < end; i++)
            {
                SimPlayer sp    = all[i];
                int       rowHue;
                string    dot;

                switch (sp.GetHealth())
                {
                    case SimPlayer.SimHealthStatus.Stuck:
                        rowHue = HUE_STUCK;   dot = "●"; break;
                    case SimPlayer.SimHealthStatus.Warning:
                        rowHue = HUE_WARNING; dot = "●"; break;
                    default:
                        rowHue = HUE_HEALTHY; dot = "●"; break;
                }

                // Alternating row tint
                if ((i - start) % 2 == 0)
                    AddImageTiled(PadX, ry, W - PadX * 2, RowH - 2, 9274);

                string state  = sp.Map == Map.Internal ? "Inactive" : sp.State.ToString();
                string phase  = sp.GetStatusDetail();
                string name   = sp.MemberName.Length > 18
                    ? sp.MemberName.Substring(0, 17) + "…"
                    : sp.MemberName;
                string guild  = sp.GuildName.Length > 14
                    ? sp.GuildName.Substring(0, 13) + "…"
                    : sp.GuildName;

                AddLabel(PadX,       ry + 3, rowHue,   dot);
                AddLabel(PadX + 16,  ry + 3, rowHue,   name);
                AddLabel(PadX + 155, ry + 3, HUE_DIM,  guild);
                AddLabel(PadX + 270, ry + 3, HUE_DIM,  state);
                AddLabel(PadX + 350, ry + 3, HUE_WARNING, phase);

                // Goto button
                AddButton(W - 98, ry + 2, 4005, 4007, BTN_GOTO_BASE + i, GumpButtonType.Reply, 0);

                // Fix button (dimmed if healthy, red if stuck/warning)
                int fixHue = rowHue == HUE_HEALTHY ? HUE_DIM : rowHue;
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

            // Store list for OnResponse (pass via static snapshot keyed by page)
            _snapshot = all;
        }

        // We keep a local snapshot so button handlers can look up players by index
        private readonly List<SimPlayer> _snapshot;

        public override void OnResponse(NetState sender, RelayInfo info)
        {
            if (!(_from is Mobile from)) return;

            int btn = info.ButtonID;
            if (btn == BTN_CLOSE) return;

            if (btn == BTN_REFRESH)
            {
                from.SendGump(new SimMonitorGump(from, _page));
                return;
            }

            if (btn == BTN_BACK)
            {
                from.SendGump(new SimPlayerGump(from));
                return;
            }

            if (btn == BTN_FIX_ALL)
            {
                PlayerSimulatorManager.SimFixAll(from);
                from.SendGump(new SimMonitorGump(from, _page));
                return;
            }

            if (btn == BTN_PREV)
            {
                from.SendGump(new SimMonitorGump(from, _page - 1));
                return;
            }

            if (btn == BTN_NEXT)
            {
                from.SendGump(new SimMonitorGump(from, _page + 1));
                return;
            }

            // Goto
            if (btn >= BTN_GOTO_BASE && btn < BTN_GOTO_BASE + _snapshot.Count)
            {
                int idx = btn - BTN_GOTO_BASE;
                if (idx < _snapshot.Count)
                {
                    SimPlayer sp = _snapshot[idx];
                    if (!sp.Deleted && sp.Map != null && sp.Map != Map.Internal)
                        from.MoveToWorld(sp.Location, sp.Map);
                    else
                        from.SendMessage(0x22, string.Format("{0} is not in the world.", sp.MemberName));
                }
                from.SendGump(new SimMonitorGump(from, _page));
                return;
            }

            // Fix
            if (btn >= BTN_FIX_BASE && btn < BTN_FIX_BASE + _snapshot.Count)
            {
                int idx = btn - BTN_FIX_BASE;
                if (idx < _snapshot.Count)
                {
                    SimPlayer sp = _snapshot[idx];
                    sp.AutoFix();
                    from.SendMessage(0x35,
                        string.Format("Fixed: {0} ({1})", sp.MemberName, sp.GuildName));
                }
                from.SendGump(new SimMonitorGump(from, _page));
                return;
            }
        }
    }
}
