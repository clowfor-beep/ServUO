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
        private static readonly HashSet<Mobile> PendingShadowHide = new HashSet<Mobile>();

        /// <summary>Called by ForceArrow shadow mode — suppresses the next RevealingAction in BaseRanged.OnSwing.</summary>
        public static void RequestShadowHide(Mobile m)
        {
            if (m != null) PendingShadowHide.Add(m);
        }

        /// <summary>Called at end of BaseRanged.OnSwing. Returns true (and hides the mobile) if shadow hide was requested, suppressing RevealingAction.</summary>
        public static bool ConsumeShadowHide(Mobile m)
        {
            if (m == null || !PendingShadowHide.Remove(m))
                return false;

            m.Hidden = true;

            if (m is PlayerMobile pm)
                pm.AllowedStealthSteps = (int)(pm.Skills[SkillName.Stealth].Value / 5.0);

            return true;
        }

        /// <summary>
        /// Call this in BaseWeapon.OnSwing BEFORE DisruptiveAction().
        /// Captures the hidden state before the attacker is revealed.
        /// </summary>
        public static void CancelBackstab(Mobile attacker)
        {
            if (attacker != null)
                PendingBackstab.Remove(attacker);
        }

        /// <summary>
        /// Returns 0.50 if a backstab is pending for this attacker (i.e. they were hidden
        /// at swing start and meet the skill requirements), otherwise 0.0.
        /// Does NOT consume the pending state — use GetBackstabBonus for that.
        /// </summary>
        public static double GetBackstabHitBonus(Mobile attacker)
        {
            if (attacker == null) return 0.0;
            return PendingBackstab.Contains(attacker) ? 0.50 : 0.0;
        }

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
        //
        // Outlands design:
        //   PvM damage bonus  = 22% × (effectiveSkill / 100)
        //   PvP damage bonus  = 11% × (effectiveSkill / 100)
        //   PvM resist bonus  = 11% × (effectiveSkill / 100)
        //   PvP resist bonus  =  5.5% × (effectiveSkill / 100)
        //
        //   effectiveSkill = herdingSkill + crookBonus
        //   crookBonus     = crook.Attributes.AttackChance + crook.Attributes.WeaponDamage
        //
        // Requires the player to have an ACTIVATED shepherd's crook
        // (equipped or in backpack, set via double-click or equipping).
        // ============================================================

        // ── Activated crook registry ──────────────────────────────────────────
        // Maps player Serial → activated ShepherdsCrook Serial
        private static readonly System.Collections.Generic.Dictionary<Serial, Serial>
            _activatedCrooks = new System.Collections.Generic.Dictionary<Serial, Serial>();

        /// <summary>Registers <paramref name="crook"/> as the active crook for <paramref name="pm"/>.
        /// Returns true if newly activated, false if it was already active.</summary>
        public static bool ActivateCrook(PlayerMobile pm, ShepherdsCrook crook)
        {
            if (pm == null || crook == null || crook.Deleted) return false;

            // Check it's accessible (equipped or in backpack)
            bool accessible = crook.IsChildOf(pm.Backpack)
                || (pm.FindItemOnLayer(Layer.TwoHanded) == crook);
            if (!accessible) return false;

            bool alreadyActive = _activatedCrooks.TryGetValue(pm.Serial, out Serial cur)
                                  && cur == crook.Serial;
            _activatedCrooks[pm.Serial] = crook.Serial;
            return !alreadyActive;
        }

        /// <summary>Returns the activated ShepherdsCrook for <paramref name="pm"/>,
        /// or null if none is set / the crook is no longer accessible.</summary>
        public static ShepherdsCrook GetActivatedCrook(PlayerMobile pm)
        {
            if (pm == null) return null;
            if (!_activatedCrooks.TryGetValue(pm.Serial, out Serial crookSerial)) return null;

            Item item = World.FindItem(crookSerial);
            if (!(item is ShepherdsCrook crook) || crook.Deleted) return null;

            // Still must be equipped or in backpack
            if (!crook.IsChildOf(pm.Backpack) && pm.FindItemOnLayer(Layer.TwoHanded) != crook)
            {
                _activatedCrooks.Remove(pm.Serial);
                return null;
            }
            return crook;
        }

        public static bool IsActivated(PlayerMobile pm, ShepherdsCrook crook)
        {
            if (pm == null || crook == null) return false;
            return _activatedCrooks.TryGetValue(pm.Serial, out Serial s) && s == crook.Serial;
        }

        // ── Persistence helpers (called from PlayerMobile Serialize/Deserialize) ─

        /// <summary>Returns the raw Serial stored for this player's activated crook,
        /// or Serial(-1) if none.</summary>
        public static Serial GetActivatedCrookSerial(PlayerMobile pm)
        {
            if (pm == null) return Serial.MinusOne;
            return _activatedCrooks.TryGetValue(pm.Serial, out Serial s) ? s : Serial.MinusOne;
        }

        /// <summary>Restores the activated-crook mapping from a saved Serial without
        /// performing accessibility checks (those happen lazily on first use).</summary>
        public static void RestoreActivatedCrook(PlayerMobile pm, Serial crookSerial)
        {
            if (pm == null || crookSerial == Serial.MinusOne) return;
            _activatedCrooks[pm.Serial] = crookSerial;
        }

        // ── Effective skill calculation ───────────────────────────────────────

        private static double GetEffectiveHerding(Mobile master)
        {
            double herding = master.Skills[SkillName.Herding].Value;
            if (!(master is PlayerMobile pm)) return herding;

            ShepherdsCrook crook = GetActivatedCrook(pm);
            if (crook == null) return -1; // no activated crook

            // Crook bonus = HCI + DI on the weapon (as raw percentage points)
            double crookBonus = crook.Attributes.AttackChance + crook.Attributes.WeaponDamage;
            return herding + crookBonus;
        }

        // ── Damage bonus applied when follower ATTACKS ────────────────────────

        /// <summary>Call from BaseCreature.AlterMeleeDamageTo / AlterSpellDamageTo
        /// when the creature is a player-controlled follower attacking.</summary>
        public static void ApplyHerdingDamageBonus(Mobile master, Mobile target, ref int damage)
        {
            if (master == null || !master.Alive || damage <= 0) return;

            double effectiveSkill = GetEffectiveHerding(master);
            if (effectiveSkill < 30.0) return;

            // PvP vs PvM multiplier
            double pct = (target is PlayerMobile) ? 0.11 : 0.22;
            double bonus = pct * (effectiveSkill / 100.0);
            damage = (int)(damage * (1.0 + bonus));

            // Passive skill gain while fighting with followers (50–120 range)
            if (Utility.RandomDouble() < 0.05)
                master.CheckSkill(SkillName.Herding, 50.0, 120.0);
        }

        // Legacy overload — called from BaseCreature without a target reference
        public static void ApplyHerdingDamageBonus(Mobile master, ref int damage)
            => ApplyHerdingDamageBonus(master, null, ref damage);

        // ── Resist bonus applied when follower RECEIVES damage ────────────────

        /// <summary>Call from BaseCreature.AlterMeleeDamageFrom / AlterSpellDamageFrom
        /// when the creature is a player-controlled follower being hit.</summary>
        public static void ApplyHerdingResistBonus(Mobile master, Mobile attacker, ref int damage)
        {
            if (master == null || !master.Alive || damage <= 0) return;

            double effectiveSkill = GetEffectiveHerding(master);
            if (effectiveSkill < 30.0) return;

            // PvP vs PvM multiplier
            double pct = (attacker is PlayerMobile) ? 0.055 : 0.11;
            double reduction = pct * (effectiveSkill / 100.0);
            damage = (int)(damage * (1.0 - reduction));
        }

        // Legacy overload
        public static void ApplyHerdingResistBonus(Mobile master, ref int damage)
            => ApplyHerdingResistBonus(master, null, ref damage);
    }
}
