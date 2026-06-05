// ============================================================
// SpellSiphonSystem.cs
// Scripts/Custom/SpellSiphonSystem.cs
//
// Magic Resist skill enhancements (Outlands-inspired):
//   - Spell Damage Reduction: flat 5% per 20 MagicResist
//   - Spell Absorption (PvM): 25% * (MR/100) chance, 75% reduction
//   - Spell Siphon buff: triggered once per 5 min on first spell hit
//       Buff lasts 60 min and grants (PvM only):
//         +10% * (MR/100) Swing Speed
//         +10% * (MR/100) Mana Refund chance
//         +10% * (MR/100) Damage Resistance
// ============================================================

using System;
using System.Collections.Generic;
using Server;
using Server.Mobiles;
using Server.Spells;

namespace Server.Custom
{
    public static class SpellSiphonSystem
    {
        private static readonly BuffIcon SiphonBuffIcon = BuffIcon.ActiveMeditation;
        private const int SiphonBuffTitleCliloc  = 1151285; // reuse ForceArrow title cliloc as placeholder
        private const int SiphonBuffDetailCliloc = 1151286;

        private static readonly Dictionary<Mobile, DateTime> _lastTrigger = new Dictionary<Mobile, DateTime>();
        private static readonly Dictionary<Mobile, DateTime> _buffExpiry   = new Dictionary<Mobile, DateTime>();

        // ── Spell Damage Reduction ────────────────────────────────────────────
        // 5% per 20 MagicResist. Applied in AOS.Damage for spell hits on players.
        public static double GetSpellDamageReduction(Mobile m)
        {
            if (!(m is PlayerMobile)) return 0.0;
            return Math.Floor(m.Skills[SkillName.MagicResist].Value / 20.0) * 0.05;
        }

        // ── Spell Absorption (PvM) ────────────────────────────────────────────
        // 25% * (MR/100) chance to absorb a creature spell for 75% reduction.
        // Returns true if absorbed (caller should apply 75% reduction + visual).
        public static bool TryAbsorbSpell(Mobile defender, Mobile attacker)
        {
            if (!(defender is PlayerMobile)) return false;
            if (!(attacker is BaseCreature))  return false;

            double chance = 0.25 * (defender.Skills[SkillName.MagicResist].Value / 100.0);
            return chance > Utility.RandomDouble();
        }

        // ── Spell Siphon Trigger ──────────────────────────────────────────────
        // Call when a spell hits a player. Triggers once per 5 min; buff lasts 60 min.
        public static void OnSpellHit(Mobile defender, Mobile attacker)
        {
            if (!(defender is PlayerMobile)) return;

            DateTime now = DateTime.UtcNow;

            // Check 5-minute cooldown
            if (_lastTrigger.TryGetValue(defender, out DateTime last) && (now - last).TotalMinutes < 5.0)
                return;

            _lastTrigger[defender] = now;

            DateTime expiry = now + TimeSpan.FromMinutes(60);
            _buffExpiry[defender] = expiry;

            double mr    = defender.Skills[SkillName.MagicResist].Value;
            int pct      = (int)(mr * 0.10);      // 10% * (MR/100) as percentage integer

            defender.SendMessage(0x35, $"You siphon energy from the spell! ({pct}% bonus vs creatures, 60 min)");

            BuffInfo.AddBuff(defender, new BuffInfo(
                SiphonBuffIcon,
                SiphonBuffTitleCliloc,
                SiphonBuffDetailCliloc,
                TimeSpan.FromMinutes(60),
                defender,
                pct.ToString()
            ));
        }

        // ── Buff queries ──────────────────────────────────────────────────────

        public static bool HasSiphon(Mobile m)
        {
            if (!_buffExpiry.TryGetValue(m, out DateTime expiry)) return false;

            if (DateTime.UtcNow > expiry)
            {
                _buffExpiry.Remove(m);
                BuffInfo.RemoveBuff(m, SiphonBuffIcon);
                m.SendMessage(0x35, "Your Spell Siphon bonus has expired.");
                return false;
            }

            return true;
        }

        /// <summary>Fractional damage resistance bonus from Spell Siphon (e.g. 0.08 = 8%).</summary>
        public static double GetSiphonDamageResistance(Mobile m)
        {
            if (!HasSiphon(m)) return 0.0;
            return 0.10 * (m.Skills[SkillName.MagicResist].Value / 100.0);
        }

        /// <summary>Fractional swing speed bonus from Spell Siphon (e.g. 0.08 = 8% SSI).</summary>
        public static double GetSiphonSwingSpeedBonus(Mobile m)
        {
            if (!HasSiphon(m)) return 0.0;
            return 0.10 * (m.Skills[SkillName.MagicResist].Value / 100.0);
        }

        /// <summary>Chance (0-1) to refund mana on a spell cast while Spell Siphon is active.</summary>
        public static double GetSiphonManaRefundChance(Mobile m)
        {
            if (!HasSiphon(m)) return 0.0;
            return 0.10 * (m.Skills[SkillName.MagicResist].Value / 100.0);
        }
    }
}
