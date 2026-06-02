// ============================================================
// AssassinsStrikeAbility.cs
// Scripts/Custom/AssassinsStrikeAbility.cs
//
// Weapon special ability for bows.
//
// When fired:
//   1. Deals bonus damage based on the attacker's Stealth skill,
//      using the same multiplier as the custom backstab system:
//        bonus = (Stealth / 100) * 2.0
//        e.g. at GM Stealth: +200% → effectively 3× total damage
//   2. Re-hides the attacker 1 second after the shot resolves.
//
// Secondary skill requirement: Stealth 50+
// Mana cost: 20
// ============================================================

using System;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Server.Items
{
    public class AssassinsStrike : WeaponAbility
    {
        public override int BaseMana => 20;

        // Stealth is the secondary skill — you need 50+ to use this ability
        public override SkillName GetSecondarySkill(Mobile from) => SkillName.Stealth;

        public override double GetRequiredSecondarySkill(Mobile from) => 50.0;

        public override void OnHit(Mobile attacker, Mobile defender, int damage)
        {
            if (!Validate(attacker) || !CheckMana(attacker, true))
                return;

            ClearCurrentAbility(attacker);

            // ── Backstab damage bonus ──────────────────────────────────────
            // Scales identically to the custom backstab synergy:
            //   bonus = (Stealth / 100) * 2.0   (0–200% extra physical damage)
            double stealthValue = attacker.Skills[SkillName.Stealth].Value;
            double bonusFactor  = stealthValue / 100.0 * 2.0;
            int    bonusDamage  = (int)(damage * bonusFactor);

            if (bonusDamage > 0)
            {
                // Pure physical bonus hit
                AOS.Damage(defender, attacker, bonusDamage, 100, 0, 0, 0, 0);
                attacker.SendMessage(0x22, String.Format("[Assassin's Strike] +{0} damage", bonusDamage));
            }

            // ── Hit effects ────────────────────────────────────────────────
            defender.FixedParticles(0x374A, 10, 15, 5013, 0x496, 0, EffectLayer.Waist);
            defender.PlaySound(0x231);

            // ── Re-hide after 1 second ─────────────────────────────────────
            // Captures the attacker reference for the closure.
            Mobile a = attacker;
            Timer.DelayCall(TimeSpan.FromSeconds(1.0), () =>
            {
                if (a == null || a.Deleted || !a.Alive) return;

                a.Hidden = true;
                a.FixedParticles(0x3728, 1, 13, 5042, 0, 0, EffectLayer.Head);
                a.PlaySound(0x22F);
                a.SendMessage(0x59, "You slip back into the shadows...");
            });
        }
    }
}
