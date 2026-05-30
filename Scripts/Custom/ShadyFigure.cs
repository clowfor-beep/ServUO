// ============================================================
// ShadyFigure.cs
// Scripts/Custom/ShadyFigure.cs
//
// A mysterious hidden informant who lurks near the Britain
// Town Crier. He can be found via Detect Hidden, which reveals
// him for 60 seconds before he vanishes again.
//
// He also responds to "I need information" spoken nearby,
// whispering that intel costs 1,000 gold. If the player says
// "yes" he charges the gold and reveals:
//   - Current location of the Traveling Rare Merchant
//   - What the Iron Company is doing with champion spawns
//
// Spawn: [add ShadyFigure
// ============================================================

using System;
using Server;
using Server.Commands;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Server.Custom
{
    public class ShadyFigure : BaseCreature
    {
        // Anchor point — Britain Town Crier area, Felucca
        private static readonly Point3D _anchor    = new Point3D(1446, 1696, 5);
        private static readonly Map     _anchorMap = Map.Felucca;
        private const int WanderRadius = 10;
        private const int InfoCost     = 1000;

        // Re-hide tracking
        private bool  _wasHidden   = true;
        private Timer _revealTimer = null;

        // Pending buyer (30-second window to say "yes")
        private Mobile   _pendingBuyer;
        private DateTime _pendingExpiry;

        [Constructable]
        public ShadyFigure()
            : base(AIType.AI_Melee, FightMode.None, 10, 1, 0.2, 0.4)
        {
            Name     = "a shady figure";
            Body     = Utility.RandomBool() ? 0x190 : 0x191;
            Hue      = Utility.RandomSkinHue();
            Hidden   = true;
            CantWalk = false;

            SetStr(60); SetDex(100); SetInt(100);
            SetHits(100);
            SetSkill(SkillName.Hiding,  100.0);
            SetSkill(SkillName.Stealth, 100.0);
            VirtualArmor = 5;
            Fame         = 0;
            Karma        = 0;

            // All black outfit
            AddItem(new Robe(1));
            AddItem(new Cloak(1));
            AddItem(new Boots(1));
            AddItem(new FloppyHat(1));
            AddItem(new BodySash(1));

            // Wander within 10 tiles of anchor
            Home      = _anchor;
            RangeHome = WanderRadius;
        }

        public ShadyFigure(Serial serial) : base(serial) { }

        public override bool IsInvulnerable            => true;
        public override bool CanBeRenamedBy(Mobile from) => false;
        public override bool ShowFameTitle              => false;

        // ── Hidden detection ──────────────────────────────────────────

        public override void OnThink()
        {
            base.OnThink();

            // Was hidden, now revealed — start 60-second re-hide timer
            if (!Hidden && _wasHidden)
            {
                _wasHidden = false;

                if (_revealTimer != null) { _revealTimer.Stop(); _revealTimer = null; }

                _revealTimer = Timer.DelayCall(TimeSpan.FromSeconds(60), () =>
                {
                    if (Deleted) return;
                    Hidden       = true;
                    _wasHidden   = true;
                    _revealTimer = null;
                });
            }
            else if (Hidden && !_wasHidden)
            {
                // Re-hidden (e.g. timer fired)
                _wasHidden = true;
            }
        }

        // ── Speech handler ────────────────────────────────────────────

        public override void OnSpeech(SpeechEventArgs e)
        {
            base.OnSpeech(e);

            Mobile from = e.Mobile;
            if (from == null || from.Deleted || !(from is PlayerMobile pm)) return;
            if (!InRange(from.Location, 8)) return;

            string speech = e.Speech.Trim().ToLowerInvariant();

            // Trigger phrase — works even when hidden
            if (speech.Contains("i need information"))
            {
                _pendingBuyer  = from;
                _pendingExpiry = DateTime.UtcNow + TimeSpan.FromSeconds(30);

                pm.SendMessage(0x55, "*A voice from the shadows...*");
                pm.SendMessage(0x55, $"Information costs {InfoCost:N0} gold. Say 'yes' if you want it.");
                return;
            }

            // Confirmation
            if (speech == "yes"
                && from == _pendingBuyer
                && DateTime.UtcNow <= _pendingExpiry)
            {
                _pendingBuyer = null;

                if (pm.Backpack == null || pm.Backpack.GetAmount(typeof(Gold)) < InfoCost)
                {
                    pm.SendMessage(0x55, "*A whisper* You don't have enough gold. Come back when you do.");
                    return;
                }

                pm.Backpack.ConsumeTotal(typeof(Gold), InfoCost);

                // Briefly reveal self for effect
                if (Hidden)
                {
                    Hidden     = true; // stays hidden — ghostly whisper only
                }

                string merchantInfo = GetMerchantInfo();
                string champInfo    = GetChampInfo();

                pm.SendMessage(0x55, "*Coins vanish into the shadows...*");
                pm.SendMessage(0x55, $"The wandering merchant — {merchantInfo}");
                pm.SendMessage(0x55, $"The Iron Company — {champInfo}");
            }
        }

        // ── Information queries ───────────────────────────────────────

        private static string GetMerchantInfo()
        {
            string town = TravelingRareMerchantSystem.GetCurrentMerchantLocation();

            if (string.IsNullOrEmpty(town))
                return "he hasn't been spotted yet today. Check the banks — he moves every 30 minutes.";

            return $"last seen at the {town} bank. He moves on every 30 minutes, so don't dawdle.";
        }

        private static string GetChampInfo()
        {
            return PlayerSimulatorManager.GetIronCompanyChampStatus();
        }

        // ── Serialization ─────────────────────────────────────────────

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write(0); // version
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            reader.ReadInt();

            // Always re-hide on load — timers don't persist
            Hidden     = true;
            _wasHidden = true;
        }
    }
}
