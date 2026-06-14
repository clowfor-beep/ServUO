// ============================================================
// ResurrectGuideSystem.cs
// Scripts/Custom/ResurrectGuideSystem.cs
//
// On player death a quest arrow appears pointing toward the
// nearest resurrection point on the same facet:
//   - Any BaseHealer NPC (Healer, WanderingHealer, ShrineHealer, etc.)
//   - Any AnkhWest or AnkhNorth item
//
// The arrow updates every 5 seconds as the ghost moves,
// switching to a closer target if one becomes available.
// Right-click the arrow to dismiss it.
// Arrow stops automatically on resurrection.
// ============================================================

using System;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Server.Custom
{
    public static class ResurrectGuideSystem
    {
        public static void Initialize()
        {
            EventSink.PlayerDeath += OnPlayerDeath;
        }

        private static void OnPlayerDeath(PlayerDeathEventArgs e)
        {
            Mobile m = e.Mobile;

            if (m == null || !(m is PlayerMobile) || m.Map == null || m.Map == Map.Internal)
                return;

            // Small delay so the death animation and corpse creation finish first
            Timer.DelayCall(TimeSpan.FromSeconds(1.5), () =>
            {
                if (m.Deleted || m.Alive || m.NetState == null)
                    return;

                AssignArrow(m);
            });
        }

        public static void AssignArrow(Mobile ghost, bool silent = false)
        {
            if (ghost == null || ghost.Deleted || ghost.Alive)
                return;

            IEntity target = FindNearest(ghost);

            if (target == null)
            {
                if (!silent)
                    ghost.SendMessage(1153, "No resurrection points could be found on this facet.");
                return;
            }

            // Stop any existing quest arrow (e.g. from Tracking skill)
            if (ghost.QuestArrow != null && ghost.QuestArrow.Running)
                ghost.QuestArrow.Stop();

            ghost.QuestArrow = new ResurrectArrow(ghost, target);

            if (!silent)
            {
                string targetName;
                if (target is BaseHealer h)
                    targetName = string.IsNullOrEmpty(h.Title) ? "a healer" : h.Title;
                else
                    targetName = "an ankh";

                ghost.SendMessage(1153, $"A spirit guide appears, pointing you toward {targetName}. Right-click the arrow to dismiss.");
            }
        }

        /// <summary>
        /// Searches all mobiles and items on the ghost's current map for
        /// the nearest BaseHealer or Ankh. Returns null if none found.
        /// </summary>
        public static IEntity FindNearest(Mobile ghost)
        {
            if (ghost == null || ghost.Map == null || ghost.Map == Map.Internal)
                return null;

            Map map = ghost.Map;
            IEntity nearest = null;
            double nearestDist = double.MaxValue;

            // ── BaseHealer mobiles ────────────────────────────────────────────
            foreach (Mobile mob in World.Mobiles.Values)
            {
                if (mob.Deleted || mob.Map != map || !mob.Alive)
                    continue;

                if (!(mob is BaseHealer))
                    continue;

                double dist = ghost.GetDistanceToSqrt(mob);

                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = mob;
                }
            }

            // ── Ankh items ────────────────────────────────────────────────────
            foreach (Item item in World.Items.Values)
            {
                if (item.Deleted || item.Map != map)
                    continue;

                if (!(item is AnkhWest) && !(item is AnkhNorth))
                    continue;

                double dist = ghost.GetDistanceToSqrt(item.Location);

                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = item;
                }
            }

            return nearest;
        }
    }

    // ============================================================
    // QUEST ARROW
    // ============================================================

    public class ResurrectArrow : QuestArrow
    {
        private readonly Mobile _ghost;
        private readonly ResurrectArrowTimer _timer;

        public Mobile Ghost          => _ghost;
        // Target is always a Mobile or Item — safe to cast. IEntity : IPoint3D.
        public IEntity CurrentTarget => Target as IEntity;

        public ResurrectArrow(Mobile ghost, IEntity target)
            : base(ghost, target)
        {
            _ghost = ghost;
            _timer = new ResurrectArrowTimer(this);
            _timer.Start();
            Update();   // send the initial arrow position to the client immediately
        }

        public override void OnClick(bool rightClick)
        {
            if (rightClick)
            {
                _ghost.SendMessage(1153, "The spirit guide fades.");
                Stop();
            }
        }

        public override void OnStop()
        {
            _timer?.Stop();
        }
    }

    // ============================================================
    // TIMER — re-evaluates nearest target every 5 seconds
    // ============================================================

    public class ResurrectArrowTimer : Timer
    {
        private readonly ResurrectArrow _arrow;

        public ResurrectArrowTimer(ResurrectArrow arrow)
            : base(TimeSpan.FromSeconds(5.0), TimeSpan.FromSeconds(5.0))
        {
            _arrow = arrow;
            Priority = TimerPriority.FiveSeconds;
        }

        protected override void OnTick()
        {
            // Arrow already stopped (e.g. player dismissed it)
            if (!_arrow.Running)
            {
                Stop();
                return;
            }

            Mobile ghost = _arrow.Ghost;

            // Ghost disconnected or deleted
            if (ghost == null || ghost.Deleted || ghost.NetState == null)
            {
                _arrow.Stop();
                return;
            }

            // Player has resurrected — stop guiding
            if (ghost.Alive)
            {
                _arrow.Stop();
                return;
            }

            // Re-find nearest resurrection point
            IEntity nearest = ResurrectGuideSystem.FindNearest(ghost);

            if (nearest == null)
            {
                _arrow.Stop();
                return;
            }

            if (nearest != _arrow.CurrentTarget)
            {
                // A closer point was found — stop this arrow and start a fresh one silently
                // (no message — the player already got the initial "spirit guide appears" message)
                // OnStop will stop this timer; AssignArrow creates a new arrow + timer
                _arrow.Stop();
                Stop();
                ResurrectGuideSystem.AssignArrow(ghost, silent: true);
                return;
            }

            // Same target — refresh position so wandering healers are tracked accurately
            _arrow.Update();
        }
    }
}
