// ============================================================
// ShadyFigure.cs
// Scripts/Custom/ShadyFigure.cs
//
// A mysterious hidden informant near the Britain Town Crier.
//
// Movement behaviour:
//   - Always hidden
//   - Stealths for ~15 seconds (moves), then pauses for ~8 seconds
//     to "re-hide" before moving again — mimicking a player using
//     the Stealth skill
//   - When revealed by Detect Hidden: freezes in place, stays
//     visible for 60 seconds, then re-hides and resumes stealthing
//
// Speech:
//   - Responds to "I need information" spoken within 8 tiles
//   - Charges 1,000 gold when player says "yes"
//   - Whispers current Rare Merchant location + Iron Company status
//
// Spawn: [add ShadyFigure
// ============================================================

using System;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Server.Custom
{
    public class ShadyFigure : BaseCreature
    {
        // ── Constants ─────────────────────────────────────────────────────────
        private static readonly Point3D _anchor    = new Point3D(1446, 1696, 5);
        private static readonly Map     _anchorMap = Map.Felucca;
        private const int  WanderRadius   = 10;
        private const int  InfoCost       = 1000;
        private const int  StealthSecs    = 15;  // seconds of active stealthy movement
        private const int  RechargeSecs   = 8;   // seconds frozen while "re-hiding"
        private const int  RevealSecs     = 60;  // seconds visible after Detect Hidden

        // ── State ─────────────────────────────────────────────────────────────
        private enum StealthState { Stealthing, Recharging, Revealed }

        private StealthState _state        = StealthState.Stealthing;
        private DateTime     _stateUntil   = DateTime.UtcNow.AddSeconds(StealthSecs);
        private bool         _wasHidden    = true;

        // Pending buyer (30-second window to say "yes")
        private Mobile   _pendingBuyer;
        private DateTime _pendingExpiry;

        // ── Construction ──────────────────────────────────────────────────────
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
            SetSkill(SkillName.Hiding,  120.0);
            SetSkill(SkillName.Stealth, 120.0);
            VirtualArmor = 5;
            Fame  = 0;
            Karma = 0;

            // All black
            AddItem(new Robe(1));
            AddItem(new Cloak(1));
            AddItem(new Boots(1));
            AddItem(new FloppyHat(1));
            AddItem(new BodySash(1));

            Home      = _anchor;
            RangeHome = WanderRadius;

            // Start stealthing right away
            _state      = StealthState.Stealthing;
            _stateUntil = DateTime.UtcNow.AddSeconds(StealthSecs);
        }

        public ShadyFigure(Serial serial) : base(serial) { }

        public override bool IsInvulnerable            => true;
        public override bool CanBeRenamedBy(Mobile from) => false;
        public override bool ShowFameTitle              => false;

        // ── Main tick ─────────────────────────────────────────────────────────

        public override void OnThink()
        {
            base.OnThink();

            // ── Detect-hidden reveal ──────────────────────────────────────────
            // If something externally unhid us, switch to Revealed state
            if (!Hidden && _wasHidden && _state != StealthState.Revealed)
            {
                _wasHidden  = false;
                _state      = StealthState.Revealed;
                _stateUntil = DateTime.UtcNow.AddSeconds(RevealSecs);
                CantWalk    = true; // freeze while visible
                return;
            }

            // ── State machine ─────────────────────────────────────────────────
            switch (_state)
            {
                case StealthState.Stealthing:
                    // Moving while hidden — let base AI wander
                    CantWalk = false;
                    _wasHidden = true;

                    if (DateTime.UtcNow >= _stateUntil)
                    {
                        // Time to pause and re-hide
                        CantWalk    = true;
                        Hidden      = true;
                        _wasHidden  = true;
                        _state      = StealthState.Recharging;
                        _stateUntil = DateTime.UtcNow.AddSeconds(RechargeSecs);
                    }
                    break;

                case StealthState.Recharging:
                    // Frozen in place, re-hiding
                    CantWalk   = true;
                    Hidden     = true;
                    _wasHidden = true;

                    if (DateTime.UtcNow >= _stateUntil)
                    {
                        // Resume stealthing
                        CantWalk    = false;
                        _state      = StealthState.Stealthing;
                        _stateUntil = DateTime.UtcNow.AddSeconds(StealthSecs);
                    }
                    break;

                case StealthState.Revealed:
                    // Visible after Detect Hidden — stay put
                    CantWalk = true;

                    if (DateTime.UtcNow >= _stateUntil)
                    {
                        // Re-hide and resume stealth cycle
                        Hidden      = true;
                        _wasHidden  = true;
                        CantWalk    = false;
                        _state      = StealthState.Stealthing;
                        _stateUntil = DateTime.UtcNow.AddSeconds(StealthSecs);
                    }
                    break;
            }
        }

        // ── Speech handler ────────────────────────────────────────────────────

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
                _pendingExpiry = DateTime.UtcNow.AddSeconds(30);

                pm.SendMessage(0x55, "*A voice whispers from the shadows...*");
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

                string champInfo = GetChampInfo();

                pm.SendMessage(0x55, "*Coins vanish into the shadows...*");
                pm.SendMessage(0x55, $"The wandering merchant — {GetMerchantInfo()}");
                pm.SendMessage(0x55, $"The Iron Company — {champInfo}");

                // If IC is idle (no cooldown, not already running), tip them off
                // and have them start a champion spawn
                if (!champInfo.Contains("right now")
                    && !champInfo.Contains("gathering")
                    && !champInfo.Contains("Next spawn run")
                    && !champInfo.Contains("minutes"))
                {
                    if (PlayerSimulatorManager.TriggerIronCompanyChamp())
                        pm.SendMessage(0x55, "*whispers* I've sent word. Expect the Iron Company to move shortly.");
                }
            }
        }

        // ── Information queries ───────────────────────────────────────────────

        private static string GetMerchantInfo()
        {
            string town = TravelingRareMerchantSystem.GetCurrentMerchantLocation();
            return string.IsNullOrEmpty(town)
                ? "he hasn't been spotted yet. Check the banks — he moves every 30 minutes."
                : $"last seen at the {town} bank. He moves every 30 minutes, so don't dawdle.";
        }

        private static string GetChampInfo()
            => PlayerSimulatorManager.GetIronCompanyChampStatus();

        // ── Serialization ─────────────────────────────────────────────────────

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write(0);
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            reader.ReadInt();

            // Always start hidden and stealthing on load
            Hidden      = true;
            _wasHidden  = true;
            CantWalk    = false;
            _state      = StealthState.Stealthing;
            _stateUntil = DateTime.UtcNow.AddSeconds(StealthSecs);
        }
    }
}
