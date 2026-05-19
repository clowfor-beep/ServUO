// ============================================================
// SkillSynergies.cs
// Scripts/Custom/SkillSynergies.cs
// ============================================================

using Server.Items;
using Server.Mobiles;
using System.Collections.Generic;

namespace Server.Custom
{
    public static class SkillSynergies
    {
        // ============================================================
        // CONFIG
        // ============================================================

        public static double CampingDamageBonus      = 0.10;
        public static double ForensicDamageBonus     = 0.10;
        public static double TrackingDamageBonus     = 0.10;
        public static double BlacksmithyDamageBonus  = 0.10;
        public static double CarpentryDamageBonus    = 0.10;
        public static double MiningMacingBonus       = 0.10;
        public static double LumberjackAxeBonus      = 0.10;

        // 2.00 = +200% bonus = 3x total damage at GM Stealth
        public static double StealthBackstabBonus    = 2.00;

        public static double AnatomyHealBonus        = 0.50;
        public static double AnimalLoreVetBonus      = 0.50;
        public static double EvalIntSpellBonus       = 0.50;
        public static double ForensicBardingBonus    = 0.10;
        public static double TrackingBardingBonus    = 0.10;
        public static double CarpentryBardingBonus   = 0.10;

        // ============================================================
        // BACKSTAB STATE — tracks who was hidden at swing start
        // ============================================================

        private static readonly HashSet<Mobile> PendingBackstab = new HashSet<Mobile>();

        /// <summary>
        /// Call this in BaseWeapon.OnSwing BEFORE DisruptiveAction().
        /// Captures the hidden state before the attacker is revealed.
        /// </summary>
        public static void RegisterBackstab(Mobile attacker)
        {
            if (attacker == null || !attacker.Hidden)
                return;

            if (attacker.Skills[SkillName.Hiding].Value  >= 80.0 &&
                attacker.Skills[SkillName.Stealth].Value >= 80.0)
            {
                PendingBackstab.Add(attacker);
            }
        }

        // ============================================================
        // LOGIC
        // ============================================================

        public static double GetWeaponDamageBonus(Mobile attacker, BaseWeapon weapon)
        {
            if (attacker == null || weapon == null)
                return 0.0;

            double bonus = 0.0;
            WeaponType wtype = weapon.Type;

            bonus += (attacker.Skills[SkillName.Camping].Value   / 100.0) * CampingDamageBonus;
            bonus += (attacker.Skills[SkillName.Forensics].Value / 100.0) * ForensicDamageBonus;
            bonus += (attacker.Skills[SkillName.Tracking].Value  / 100.0) * TrackingDamageBonus;

            if (wtype == WeaponType.Axe     || wtype == WeaponType.Slashing ||
                wtype == WeaponType.Bashing || wtype == WeaponType.Piercing  ||
                wtype == WeaponType.Polearm || wtype == WeaponType.Fists)
            {
                bonus += (attacker.Skills[SkillName.Blacksmith].Value / 100.0) * BlacksmithyDamageBonus;
            }

            if (wtype == WeaponType.Staff || wtype == WeaponType.Ranged)
                bonus += (attacker.Skills[SkillName.Carpentry].Value / 100.0) * CarpentryDamageBonus;

            if (wtype == WeaponType.Bashing)
                bonus += (attacker.Skills[SkillName.Mining].Value / 100.0) * MiningMacingBonus;

            if (wtype == WeaponType.Axe)
                bonus += (attacker.Skills[SkillName.Lumberjacking].Value / 100.0) * LumberjackAxeBonus;

            return bonus;
        }

        /// <summary>
        /// Returns backstab multiplier. PvM only.
        /// Uses PendingBackstab so timing issue with DisruptiveAction is avoided.
        /// </summary>
        public static double GetBackstabBonus(Mobile attacker, Mobile defender)
        {
            if (attacker == null || defender == null)
                return 0.0;

            if (defender is PlayerMobile)
                return 0.0;

            if (!PendingBackstab.Remove(attacker))
                return 0.0;

            return (attacker.Skills[SkillName.Stealth].Value / 100.0) * StealthBackstabBonus;
        }

        public static double GetHealBonus(Mobile healer)
        {
            if (healer == null)
                return 0.0;

            double scale = System.Math.Min(
                healer.Skills[SkillName.Anatomy].Value,
                healer.Skills[SkillName.Healing].Value) / 100.0;

            return scale * AnatomyHealBonus;
        }

        public static double GetBardingBonus(Mobile bard)
        {
            if (bard == null)
                return 0.0;

            double bonus = 0.0;
            bonus += (bard.Skills[SkillName.Forensics].Value / 100.0) * ForensicBardingBonus  * 10.0;
            bonus += (bard.Skills[SkillName.Tracking].Value  / 100.0) * TrackingBardingBonus  * 10.0;
            bonus += (bard.Skills[SkillName.Carpentry].Value / 100.0) * CarpentryBardingBonus * 10.0;
            return bonus;
        }
    }
}
