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
        private const double HpMultiplierPerSS      = 1.5;   // +150% HP   per SS/100
        private const double DamageMultiplierPerSS  = 0.5;   // +50%  dmg  per SS/100
        private const double WrestlingBonusPerSS    = 50.0;  // +50 wrestling per SS/100
        private const double VirtualArmorBonusPerSS = 25.0;  // +25 armor     per SS/100
        private const double MagicResistBonusPerSS  = 50.0;  // +50 resist    per SS/100

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
                creature.HitsMaxSeed = creature.HitsMaxSeed + hpBonus;
                creature.Hits = creature.HitsMaxSeed; // full HP on summon
            }

            // ── Damage ─────────────────────────────────────────────────────
            creature.DamageMin = creature.DamageMin + (int)(creature.DamageMin * DamageMultiplierPerSS * factor);
            creature.DamageMax = creature.DamageMax + (int)(creature.DamageMax * DamageMultiplierPerSS * factor);

            // ── Wrestling ──────────────────────────────────────────────────
            double newWrestling = Math.Min(120.0,
                creature.Skills[SkillName.Wrestling].Base + WrestlingBonusPerSS * factor);
            creature.SetSkill(SkillName.Wrestling, newWrestling);

            // ── Virtual Armor ──────────────────────────────────────────────
            creature.VirtualArmor = creature.VirtualArmor + (int)(VirtualArmorBonusPerSS * factor);

            // ── Magic Resist ───────────────────────────────────────────────
            double newResist = Math.Min(120.0,
                creature.Skills[SkillName.MagicResist].Base + MagicResistBonusPerSS * factor);
            creature.SetSkill(SkillName.MagicResist, newResist);
        }
    }
}
