// ============================================================
// SummonerSynergySystem.cs
// Scripts/Custom/SummonerSynergySystem.cs
//
// Spirit Speak scaling for summoned followers.
// Called by each summoning spell immediately after the
// creature is placed in the world.
//
// No Initialize() needed -- pure helper called by spells.
// No Serialize/Deserialize -- no persistent state.
//
// At 100 Spirit Speak: HP x2.5, Damage x1.5, Wrestling +50,
//                      VirtualArmor +25, MagicResist +50
// At 120 Spirit Speak: HP x2.8, Damage x1.6, Wrestling +60,
//                      VirtualArmor +30, MagicResist +60
// ============================================================

using System;
using Server;
using Server.Mobiles;

namespace Server.Custom
{
    public static class SummonerSynergySystem
    {
        // ── Tuning constants ────────────────────────────────────────────────
        private const double HpMultiplierPerSS         = 1.5;   // +150% HP        per SS/100
        private const double DamageMultiplierPerSS     = 0.5;   // +50%  dmg       per SS/100
        private const double AttackSpeedMultiplierPerSS = 0.25; // +25%  atk speed per SS/100
        private const double WrestlingBonusPerSS       = 50.0;  // +50  wrestling  per SS/100
        private const double VirtualArmorBonusPerSS    = 25.0;  // +25  armor      per SS/100
        private const double MagicResistBonusPerSS     = 50.0;  // +50  resist     per SS/100

        private const double MinActiveSpeed  = 0.05;
        private const double MinPassiveSpeed = 0.10;

        // ── Summon duration ─────────────────────────────────────────────────
        // Formula: 2 min + 8 min * (SS / 100)
        // SS   0 → 120 s  |  SS 100 → 600 s  |  SS 120 → 696 s
        public static TimeSpan GetSummonDuration(Mobile caster)
        {
            double ss = caster.Skills[SkillName.SpiritSpeak].Value;
            return TimeSpan.FromSeconds(120.0 + 480.0 * (ss / 100.0));
        }

        // ── Corpse-harvest timer extension (called from SpiritSpeak.cs) ─────
        // Extends all nearby summoned creatures' timers by extensionSeconds,
        // capped so no summon exceeds 30 minutes of total remaining time.
        public static void ExtendNearbyTimers(Mobile caster, int extensionSeconds)
        {
            if (caster == null || caster.Map == null) return;

            const int MaxRemainingSeconds = 1800; // 30-minute hard cap
            bool extended = false;

            foreach (Mobile m in caster.GetMobilesInRange(12))
            {
                BaseCreature bc = m as BaseCreature;
                if (bc == null || bc.Deleted || !bc.Summoned) continue;
                if (bc.SummonMaster != caster) continue;

                DateTime now       = DateTime.UtcNow;
                DateTime newExpiry = bc.SummonEnd.AddSeconds(extensionSeconds);
                DateTime capExpiry = now.AddSeconds(MaxRemainingSeconds);

                if (newExpiry > capExpiry) newExpiry = capExpiry;
                if (newExpiry <= bc.SummonEnd) continue; // already at cap

                TimeSpan remaining = newExpiry - now;
                if (remaining <= TimeSpan.Zero) continue;

                TimerRegistry.RemoveFromRegistry<BaseCreature>("UnsummonTimer", bc);
                bc.SummonEnd = newExpiry;
                TimerRegistry.Register<BaseCreature>("UnsummonTimer", bc, remaining, c => c.Delete());
                extended = true;
            }

            if (extended)
                caster.SendMessage(0x59, "Your summoned companions feel renewed energy from the harvested spirit.");
        }

        // ── Dispel resistance helper ────────────────────────────────────────
        // Returns the chance (0.0–1.0) that this summoned creature resists a
        // dispel attempt from a hostile caster, based on its master's SS.
        public static double GetDispelResistChance(BaseCreature bc)
        {
            if (bc?.SummonMaster == null) return 0.0;
            return 0.5 * (bc.SummonMaster.Skills[SkillName.SpiritSpeak].Value / 100.0);
        }

        /// <summary>
        /// Apply Spirit Speak scaling to a freshly summoned creature.
        /// Call this AFTER SpellHelper.Summon / BaseCreature.Summon returns.
        /// </summary>
        public static void ApplyBonuses(BaseCreature creature, Mobile caster)
        {
            if (creature == null || caster == null || creature.Deleted)
                return;

            double ss = caster.Skills[SkillName.SpiritSpeak].Value;
            if (ss < 20.0)
                return; // no bonus below 20 Spirit Speak

            double factor = ss / 100.0;

            // ── Hit Points ─────────────────────────────────────────────────
            if (creature.HitsMaxSeed > 0)
            {
                int hpBonus = (int)(creature.HitsMaxSeed * HpMultiplierPerSS * factor);
                creature.HitsMaxSeed += hpBonus;
                creature.Hits = creature.HitsMaxSeed;
            }

            // ── Damage ─────────────────────────────────────────────────────
            creature.DamageMin += (int)(creature.DamageMin * DamageMultiplierPerSS * factor);
            creature.DamageMax += (int)(creature.DamageMax * DamageMultiplierPerSS * factor);

            // ── Attack Speed ───────────────────────────────────────────────
            // Reduce AI action delay — lower value = faster actions/attacks
            double speedReduction = 1.0 - (AttackSpeedMultiplierPerSS * factor);
            creature.ActiveSpeed  = Math.Max(MinActiveSpeed,  creature.ActiveSpeed  * speedReduction);
            creature.PassiveSpeed = Math.Max(MinPassiveSpeed, creature.PassiveSpeed * speedReduction);

            // ── Wrestling ──────────────────────────────────────────────────
            double newWrestling = Math.Min(120.0,
                creature.Skills[SkillName.Wrestling].Base + WrestlingBonusPerSS * factor);
            creature.SetSkill(SkillName.Wrestling, newWrestling);

            // ── Virtual Armor ──────────────────────────────────────────────
            creature.VirtualArmor += (int)(VirtualArmorBonusPerSS * factor);

            // ── Magic Resist ───────────────────────────────────────────────
            double newResist = Math.Min(120.0,
                creature.Skills[SkillName.MagicResist].Base + MagicResistBonusPerSS * factor);
            creature.SetSkill(SkillName.MagicResist, newResist);
        }
    }
}
