// ============================================================
// QuestTrackerHUD.cs
// Scripts/Custom/QuestTrackerHUD.cs
//
// Always-visible HUD showing the player's active quest and
// objective progress counters.
//
// Architecture: Same pattern as CooldownSystem.cs
//   - Static manager holds per-player quest state
//   - QuestTrackerGump is non-closable, auto-refreshes
//   - Any system hooks in via the static API
//
// API (call from any quest system):
//   QuestTrackerHUD.SetQuest(mobile, title, objectives)
//   QuestTrackerHUD.UpdateObjective(mobile, label, current, max)
//   QuestTrackerHUD.ClearQuest(mobile)
//
// HUD is hidden when no quest is active — zero footprint.
// ============================================================

using System;
using System.Collections.Generic;
using Server;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Server.Custom
{
    // ============================================================
    // DATA TYPES
    // ============================================================

    public class QuestObjective
    {
        public string Label;
        public int    Current;
        public int    Max;

        public bool   IsComplete => Current >= Max;
        public double Fraction   => Max > 0 ? Math.Min(1.0, (double)Current / Max) : 0.0;

        public QuestObjective(string label, int current, int max)
        {
            Label   = label;
            Current = current;
            Max     = max;
        }
    }

    public class ActiveQuest
    {
        public string Title;
        public List<QuestObjective> Objectives;

        public ActiveQuest(string title, List<QuestObjective> objectives)
        {
            Title      = title;
            Objectives = objectives ?? new List<QuestObjective>();
        }
    }

    // ============================================================
    // STATIC MANAGER
    // ============================================================

    public static class QuestTrackerHUD
    {
        private static readonly Dictionary<Mobile, ActiveQuest> _quests =
            new Dictionary<Mobile, ActiveQuest>();

        // ── API ──────────────────────────────────────────────────

        /// <summary>
        /// Set or replace the player's active quest.
        /// objectives: list of (label, current, max) tuples — pass null for no objectives yet.
        /// </summary>
        public static void SetQuest(Mobile m, string title, List<QuestObjective> objectives = null)
        {
            if (!(m is PlayerMobile pm)) return;

            _quests[pm] = new ActiveQuest(title, objectives ?? new List<QuestObjective>());
            Refresh(pm);
        }

        /// <summary>
        /// Update a single objective counter by label. Creates it if it doesn't exist.
        /// </summary>
        public static void UpdateObjective(Mobile m, string label, int current, int max)
        {
            if (!(m is PlayerMobile pm)) return;
            if (!_quests.TryGetValue(pm, out var quest)) return;

            var obj = quest.Objectives.Find(o => o.Label == label);
            if (obj != null)
            {
                obj.Current = current;
                obj.Max     = max;
            }
            else
            {
                quest.Objectives.Add(new QuestObjective(label, current, max));
            }

            Refresh(pm);
        }

        /// <summary>
        /// Remove the player's active quest and hide the HUD.
        /// </summary>
        public static void ClearQuest(Mobile m)
        {
            if (!(m is PlayerMobile pm)) return;
            _quests.Remove(pm);
            pm.CloseGump(typeof(QuestTrackerGump));
        }

        /// <summary>
        /// Returns the active quest for the player, or null if none.
        /// </summary>
        public static ActiveQuest GetQuest(Mobile m) =>
            _quests.TryGetValue(m, out var q) ? q : null;

        // ── Internal ──────────────────────────────────────────────

        internal static void Refresh(PlayerMobile pm)
        {
            if (pm?.NetState == null) return;
            pm.CloseGump(typeof(QuestTrackerGump));

            if (_quests.TryGetValue(pm, out var quest))
                pm.SendGump(new QuestTrackerGump(pm, quest));
        }
    }

    // ============================================================
    // GUMP
    // ============================================================

    public class QuestTrackerGump : Gump
    {
        private const int W      = 220;
        private const int PadX   = 8;
        private const int RowH   = 28;
        private const int BarH   = 4;
        private const int BarBg  = 9264;
        private const int BarFill= 9266;

        // Position: top-right of screen — doesn't overlap CooldownHUD
        private const int GumpX  = 600;
        private const int GumpY  = 100;

        private readonly PlayerMobile _player;
        private Timer _updateTimer;

        public QuestTrackerGump(PlayerMobile player, ActiveQuest quest)
            : base(GumpX, GumpY)
        {
            _player    = player;
            Closable   = false;
            Disposable = false;
            Dragable   = true;
            Resizable  = false;

            Build(quest);
            StartTimer();
        }

        private void Build(ActiveQuest quest)
        {
            int objCount = quest.Objectives.Count;
            int h = 30 + (objCount > 0 ? objCount * RowH + 4 : 0);

            AddBackground(0, 0, W, h, 9270);
            AddAlphaRegion(2, 2, W - 4, h - 4);

            // Quest title
            AddHtml(PadX, 6, W - PadX * 2, 18,
                $"<BASEFONT COLOR=#C8A428>{quest.Title}</BASEFONT>",
                false, false);

            if (objCount == 0) return;

            // Divider
            AddImageTiled(PadX, 24, W - PadX * 2, 1, BarBg);

            int y = 28;
            foreach (var obj in quest.Objectives)
            {
                string col   = obj.IsComplete ? "#44FF44" : "#DDCCAA";
                string label = obj.Max > 1
                    ? $"{obj.Label}: {obj.Current}/{obj.Max}"
                    : obj.Label;

                AddHtml(PadX, y, W - PadX * 2, 18,
                    $"<BASEFONT COLOR={col}>{label}</BASEFONT>",
                    false, false);

                int barY  = y + 18;
                int barW  = W - PadX * 2;
                int fillW = obj.IsComplete ? barW : Math.Max(2, (int)(barW * obj.Fraction));

                AddImageTiled(PadX, barY, barW,  BarH, BarBg);
                AddImageTiled(PadX, barY, fillW, BarH, obj.IsComplete ? 9268 : BarFill);

                y += RowH;
            }
        }

        private void StartTimer()
        {
            // Refresh every 2 seconds — objectives don't need sub-second precision
            _updateTimer = Timer.DelayCall(
                TimeSpan.FromSeconds(2.0),
                TimeSpan.FromSeconds(2.0),
                OnTick);
        }

        private void OnTick()
        {
            _updateTimer?.Stop();

            var quest = QuestTrackerHUD.GetQuest(_player);
            if (quest == null)
            {
                _player?.CloseGump(typeof(QuestTrackerGump));
                return;
            }

            QuestTrackerHUD.Refresh(_player);
        }

        public override void OnServerClose(NetState owner)
        {
            _updateTimer?.Stop();
            base.OnServerClose(owner);
        }

        public override void OnResponse(NetState sender, RelayInfo info) { }
    }
}
