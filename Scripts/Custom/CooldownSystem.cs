// ============================================================
// CooldownSystem.cs
// Scripts/Custom/CooldownSystem.cs
// ============================================================

using System;
using System.Collections.Generic;
using Server;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Server.Custom
{
    public static class CooldownConfig
    {
        public static double UpdateInterval = 0.5;
        public static int GumpX    = 250;
        public static int GumpY    = 600;
        public static int GumpWidth = 200;
    }

    public class CooldownEntry
    {
        public string   SkillName;
        public DateTime Expires;
        public double   TotalDuration;

        public double Remaining    => Math.Max(0.0, (Expires - DateTime.UtcNow).TotalSeconds);
        public bool   IsExpired    => DateTime.UtcNow >= Expires;
        public double FractionLeft => TotalDuration > 0 ? Math.Min(1.0, Remaining / TotalDuration) : 0.0;

        public CooldownEntry(string skillName, double seconds)
        {
            SkillName     = skillName;
            TotalDuration = seconds;
            Expires       = DateTime.UtcNow.AddSeconds(seconds);
        }
    }

    public static class CooldownSystem
    {
        private static readonly Dictionary<Mobile, List<CooldownEntry>> Active =
            new Dictionary<Mobile, List<CooldownEntry>>();

        // -------------------------------------------------------
        // Hook into Skills.cs delegate on startup
        // -------------------------------------------------------
        // Skills whose cooldown is managed externally (e.g. server resets NextSkillTime
        // long before the OnUse return value expires, so we track them ourselves).
        private static readonly HashSet<string> ExternallyManaged = new HashSet<string>
        {
            "Animal Taming",
        };

        public static void Initialize()
        {
            // Skills.OnSkillUsed fires for every player skill activation.
            // We only show the HUD for cooldowns >= 1 second.
            // Externally-managed skills supply their own Start/Clear calls.
            Skills.OnSkillUsed = (m, name, seconds) =>
            {
                if (seconds >= 1.0 && !ExternallyManaged.Contains(name))
                    Start(m, name, seconds);
            };
        }

        public static void Start(Mobile m, string skillName, double seconds)
        {
            if (!(m is PlayerMobile pm)) return;

            if (!Active.TryGetValue(m, out var list))
            {
                list = new List<CooldownEntry>();
                Active[m] = list;
            }

            list.RemoveAll(e => e.SkillName == skillName);
            list.Add(new CooldownEntry(skillName, seconds));
            RefreshGump(pm);
        }

        public static void Clear(Mobile m, string skillName)
        {
            if (!Active.TryGetValue(m, out var list)) return;
            list.RemoveAll(e => e.SkillName == skillName);
            PruneAndRefresh(m as PlayerMobile);
        }

        public static double GetRemaining(Mobile m, string skillName)
        {
            if (!Active.TryGetValue(m, out var list)) return 0.0;
            return list.Find(e => e.SkillName == skillName)?.Remaining ?? 0.0;
        }

        public static bool IsOnCooldown(Mobile m, string skillName)
            => GetRemaining(m, skillName) > 0.0;

        internal static void PruneAndRefresh(PlayerMobile pm)
        {
            if (pm == null) return;

            if (Active.TryGetValue(pm, out var list))
            {
                list.RemoveAll(e => e.IsExpired);

                if (list.Count == 0)
                {
                    Active.Remove(pm);
                    pm.CloseGump(typeof(CooldownGump));
                    return;
                }
            }

            RefreshGump(pm);
        }

        private static void RefreshGump(PlayerMobile pm)
        {
            if (pm?.NetState == null) return;
            pm.CloseGump(typeof(CooldownGump));

            if (Active.TryGetValue(pm, out var list) && list.Count > 0)
                pm.SendGump(new CooldownGump(pm, list));
        }
    }

    // ============================================================
    // COOLDOWN GUMP
    // ============================================================

    public class CooldownGump : Gump
    {
        private readonly PlayerMobile _player;
        private Timer _updateTimer;

        private const int BarBg   = 9264;
        private const int BarFill = 9266;

        public CooldownGump(PlayerMobile player, List<CooldownEntry> entries)
            : base(CooldownConfig.GumpX, CooldownConfig.GumpY)
        {
            _player    = player;
            Closable   = false;
            Disposable = false;
            Dragable   = true;
            Resizable  = false;

            BuildLayout(entries);
            StartTimer();
        }

        private void BuildLayout(List<CooldownEntry> entries)
        {
            int w    = CooldownConfig.GumpWidth;
            int rowH = 26;
            int padX = 8;
            int h    = 8 + entries.Count * rowH + 4;

            AddBackground(0, 0, w, h, 9270);
            AddAlphaRegion(2, 2, w - 4, h - 4);

            int y = 6;

            foreach (var entry in entries)
            {
                double rem  = entry.Remaining;
                double frac = entry.FractionLeft;

                string hexColor = rem > 3.0 ? "#00CC44" : rem > 1.0 ? "#FF8800" : "#FF2222";

                string label   = String.Format("<BASEFONT COLOR={0}>{1}</BASEFONT>", hexColor, entry.SkillName);
                string timeStr = String.Format("<BASEFONT COLOR={0}>{1:F1}s</BASEFONT>", hexColor, rem);

                AddHtml(padX,   y, w - 60, 18, label,   false, false);
                AddHtml(w - 52, y, 48,     18, timeStr, false, false);

                int barY  = y + 18;
                int barW  = w - padX * 2;
                int fillW = Math.Max(2, (int)(barW * frac));

                AddImageTiled(padX, barY, barW,  4, BarBg);
                AddImageTiled(padX, barY, fillW, 4, BarFill);

                y += rowH;
            }
        }

        private void StartTimer()
        {
            _updateTimer = Timer.DelayCall(
                TimeSpan.FromSeconds(CooldownConfig.UpdateInterval),
                TimeSpan.FromSeconds(CooldownConfig.UpdateInterval),
                OnTick);
        }

        private void OnTick()
        {
            _updateTimer?.Stop();
            CooldownSystem.PruneAndRefresh(_player);
        }

        public override void OnServerClose(NetState owner)
        {
            _updateTimer?.Stop();
            base.OnServerClose(owner);
        }
    }
}
