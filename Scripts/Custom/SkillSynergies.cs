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
        // BACKSTAB STATE -- tracks who was hidden at swing start
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
        // OFFENSIVE SYNERGIES
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

        // ============================================================
        // DEFENSIVE SYNERGIES -- Step 9
        // ============================================================

        // -- Helpers --------------------------------------------------

        /// <summary>Returns 0.0 at skill &lt;80, 1.0 at skill 100+, linear between.</summary>
        private static double SynergyScale(double skillValue)
        {
            if (skillValue < 80.0) return 0.0;
            return System.Math.Min(1.0, (skillValue - 80.0) / 20.0);
        }

        /// <summary>Returns 0 below 100, bonusAt100 at skill 100, bonusAt120 at skill 120+.</summary>
        private static int CapScale(double skillValue, int bonusAt100, int bonusAt120)
        {
            if (skillValue < 100.0) return 0;
            if (skillValue >= 120.0) return bonusAt120;
            return bonusAt100 + (int)((skillValue - 100.0) / 20.0 * (bonusAt120 - bonusAt100));
        }

        // -- Section A -- Resist bonuses (% points) -------------------

        public static int GetPhysicalResistBonus(Mobile m)
        {
            if (!(m is PlayerMobile)) return 0;
            double sum = 0;
            sum += SynergyScale(m.Skills[SkillName.Blacksmith].Value)    * 10;
            sum += SynergyScale(m.Skills[SkillName.Mining].Value)        *  5;
            sum += SynergyScale(m.Skills[SkillName.Lumberjacking].Value) *  5;
            sum += SynergyScale(m.Skills[SkillName.Carpentry].Value)     *  3;
            sum += SynergyScale(m.Skills[SkillName.Camping].Value)       *  5;
            sum += SynergyScale(m.Skills[SkillName.AnimalTaming].Value)  *  5;
            sum += SynergyScale(m.Skills[SkillName.Wrestling].Value)     *  8;
            return (int)sum;
        }

        public static int GetFireResistBonus(Mobile m)
        {
            if (!(m is PlayerMobile)) return 0;
            double sum = 0;
            sum += SynergyScale(m.Skills[SkillName.Blacksmith].Value) *  5;
            sum += SynergyScale(m.Skills[SkillName.Mining].Value)     *  5;
            sum += SynergyScale(m.Skills[SkillName.Alchemy].Value)    * 12;
            sum += SynergyScale(m.Skills[SkillName.TasteID].Value)    *  5;
            return (int)sum;
        }

        public static int GetColdResistBonus(Mobile m)
        {
            if (!(m is PlayerMobile)) return 0;
            double sum = 0;
            sum += SynergyScale(m.Skills[SkillName.Lumberjacking].Value) * 8;
            sum += SynergyScale(m.Skills[SkillName.Carpentry].Value)     * 5;
            sum += SynergyScale(m.Skills[SkillName.Camping].Value)       * 8;
            sum += SynergyScale(m.Skills[SkillName.Herding].Value)       * 5;
            sum += SynergyScale(m.Skills[SkillName.Fishing].Value)       * 5;
            sum += SynergyScale(m.Skills[SkillName.Inscribe].Value)      * 5;
            return (int)sum;
        }

        public static int GetPoisonResistBonus(Mobile m)
        {
            if (!(m is PlayerMobile)) return 0;
            double sum = 0;
            sum += SynergyScale(m.Skills[SkillName.TasteID].Value)     * 12;
            sum += SynergyScale(m.Skills[SkillName.Herding].Value)      *  8;
            sum += SynergyScale(m.Skills[SkillName.AnimalTaming].Value) *  5;
            sum += SynergyScale(m.Skills[SkillName.Alchemy].Value)      *  5;
            return (int)sum;
        }

        public static int GetEnergyResistBonus(Mobile m)
        {
            if (!(m is PlayerMobile)) return 0;
            double sum = 0;
            sum += SynergyScale(m.Skills[SkillName.Inscribe].Value) * 12;
            sum += SynergyScale(m.Skills[SkillName.Fishing].Value)  *  3;
            return (int)sum;
        }

        // -- Section B -- Resist cap raises ---------------------------

        public static int GetPhysicalResistCap(Mobile m)
        {
            if (!(m is PlayerMobile)) return 0;
            int cap = 0;
            cap += CapScale(m.Skills[SkillName.Blacksmith].Value,    5, 8);
            cap += CapScale(m.Skills[SkillName.Lumberjacking].Value, 2, 3);
            cap += CapScale(m.Skills[SkillName.Carpentry].Value,     2, 3);
            cap += CapScale(m.Skills[SkillName.Camping].Value,       2, 3);
            return cap;
        }

        public static int GetFireResistCap(Mobile m)
        {
            if (!(m is PlayerMobile)) return 0;
            int cap = 0;
            cap += CapScale(m.Skills[SkillName.Alchemy].Value,    5, 8);
            cap += CapScale(m.Skills[SkillName.Blacksmith].Value, 2, 3);
            cap += CapScale(m.Skills[SkillName.Mining].Value,     2, 3);
            cap += CapScale(m.Skills[SkillName.TasteID].Value,    2, 3);
            return cap;
        }

        public static int GetColdResistCap(Mobile m)
        {
            if (!(m is PlayerMobile)) return 0;
            int cap = 0;
            cap += CapScale(m.Skills[SkillName.Camping].Value,       5, 8);
            cap += CapScale(m.Skills[SkillName.Lumberjacking].Value, 2, 3);
            cap += CapScale(m.Skills[SkillName.Herding].Value,       2, 3);
            cap += CapScale(m.Skills[SkillName.Inscribe].Value,      2, 3);
            return cap;
        }

        public static int GetPoisonResistCap(Mobile m)
        {
            if (!(m is PlayerMobile)) return 0;
            int cap = 0;
            cap += CapScale(m.Skills[SkillName.TasteID].Value,      5, 8);
            cap += CapScale(m.Skills[SkillName.Alchemy].Value,      2, 3);
            cap += CapScale(m.Skills[SkillName.Herding].Value,      2, 3);
            cap += CapScale(m.Skills[SkillName.AnimalTaming].Value, 2, 3);
            return cap;
        }

        public static int GetEnergyResistCap(Mobile m)
        {
            if (!(m is PlayerMobile)) return 0;
            int cap = 0;
            cap += CapScale(m.Skills[SkillName.Inscribe].Value, 5, 8);
            cap += CapScale(m.Skills[SkillName.Fishing].Value,  2, 3);
            return cap;
        }

        // -- Section C -- DCI bonus (fraction, e.g. 0.08 = +8%) ------

        public static double GetDCIBonus(Mobile m)
        {
            if (!(m is PlayerMobile)) return 0.0;
            double bonus = 0.0;
            bonus += SynergyScale(m.Skills[SkillName.Tracking].Value)     * 0.08;
            bonus += SynergyScale(m.Skills[SkillName.DetectHidden].Value) * 0.10;
            return bonus;
        }

        // -- Section D -- HP bonus ------------------------------------

        public static int GetBonusHP(Mobile m)
        {
            if (!(m is PlayerMobile)) return 0;
            double sum = 0;
            sum += SynergyScale(m.Skills[SkillName.Herding].Value)      * 20;
            sum += SynergyScale(m.Skills[SkillName.AnimalTaming].Value) * 15;
            sum += SynergyScale(m.Skills[SkillName.Fishing].Value)      * 10;
            return (int)sum;
        }

        // -- Section E -- Bandage heal bonus (multiplier fraction) ----

        public static double GetBandageHealBonus(Mobile m)
        {
            if (!(m is PlayerMobile)) return 0.0;
            double bonus = 0.0;
            bonus += SynergyScale(m.Skills[SkillName.Forensics].Value)  * 0.25;
            bonus += SynergyScale(m.Skills[SkillName.Veterinary].Value) * 0.20;
            return bonus;
        }

        // -- Section F -- HP regen multiplier (1.0 = no change) ------

        /// <summary>
        /// Returns 1.0 at Camping &lt;80, 2.0 (double regen) at Camping 100+, linear between.
        /// Applied as a multiplier to the regen rate in RegenRates.cs.
        /// </summary>
        public static double GetHPRegenMultiplier(Mobile m)
        {
            if (!(m is PlayerMobile)) return 1.0;
            return 1.0 + SynergyScale(m.Skills[SkillName.Camping].Value);
        }

        // ============================================================
        // HERDING SYNERGIES -- follower damage and resistance bonuses
        // ============================================================

        // +22% damage per (Herding/100) while controller has a Shepherd's Crook
        private const double HerdingDamageBonusPerPoint = 0.22;
        // +11% resistance per (Herding/100)
        private const double HerdingResistBonusPerPoint = 0.11;

        /// <summary>
        /// Call from BaseCreature.AlterMeleeDamageTo when the creature is a follower attacking.
        /// Multiplies outgoing melee damage by the Herding bonus.
        /// </summary>
        public static void ApplyHerdingDamageBonus(Mobile master, ref int damage)
        {
            if (master == null || !master.Alive) return;

            double herding = master.Skills[SkillName.Herding].Value;
            if (herding < 30.0) return;
            if (!HasShepherdsCrook(master)) return;

            double bonus = HerdingDamageBonusPerPoint * (herding / 100.0);
            damage = (int)(damage * (1.0 + bonus));

            // Passive Herding skill gain: 5% chance per successful follower hit
            if (Utility.RandomDouble() < 0.05)
                master.CheckSkill(SkillName.Herding, 50.0, 120.0);
        }

        /// <summary>
        /// Call from BaseCreature.AlterMeleeDamageFrom when the creature is a follower being hit.
        /// Reduces incoming melee damage by the Herding resist bonus.
        /// </summary>
        public static void ApplyHerdingResistBonus(Mobile master, ref int damage)
        {
            if (master == null || !master.Alive) return;

            double herding = master.Skills[SkillName.Herding].Value;
            if (herding < 30.0) return;
            if (!HasShepherdsCrook(master)) return;

            double reduction = HerdingResistBonusPerPoint * (herding / 100.0);
            damage = (int)(damage * (1.0 - reduction));
        }

        /// <summary>Returns true if the mobile has a Shepherd's Crook equipped or in backpack.</summary>
        private static bool HasShepherdsCrook(Mobile m)
        {
            if (m.FindItemOnLayer(Layer.TwoHanded) is ShepherdsCrook) return true;
            if (m.Backpack != null && m.Backpack.FindItemByType(typeof(ShepherdsCrook)) != null) return true;
            return false;
        }
    }
}
