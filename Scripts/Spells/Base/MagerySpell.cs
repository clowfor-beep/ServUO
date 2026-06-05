using Server.Items;
using System;

namespace Server.Spells
{
    public abstract class MagerySpell : Spell
    {
        private static readonly int[] m_ManaTable = new int[] { 4, 6, 9, 11, 14, 20, 40, 50 };
        private const double ChanceOffset = 20.0, ChanceLength = 100.0 / 7.0;
        public MagerySpell(Mobile caster, Item scroll, SpellInfo info)
            : base(caster, scroll, info)
        {
        }

        public abstract SpellCircle Circle { get; }
        public override TimeSpan CastDelayBase => TimeSpan.FromMilliseconds(((4 + (int)Circle) * CastDelaySecondsPerTick) * 1000);
        public override bool ConsumeReagents()
        {
            if (base.ConsumeReagents())
                return true;

            if (ArcaneGem.ConsumeCharges(Caster, 1))
                return true;

            return false;
        }

        public override void GetCastSkills(out double min, out double max)
        {
            int circle = (int)Circle;

            if (Scroll != null)
                circle -= 2;

            double avg = ChanceLength * circle;

            min = avg - ChanceOffset;
            max = avg + ChanceOffset;
        }

        public override int GetMana()
        {
            if (Scroll is BaseWand)
                return 0;

            return m_ManaTable[(int)Circle];
        }

        public virtual bool CheckResisted(Mobile target)
        {
            // Outlands formula: (40% - circle * 5%) * (MagicResist / 100)
            // Circle 1 = 35% at GM, circle 7 = 5% at GM, circle 8+ = 0%
            int circleNumber = (int)Circle + 1;
            double baseChance = (40.0 - circleNumber * 5.0) / 100.0;

            if (baseChance <= 0.0)
                return false;

            double resistSkill = GetResistSkill(target);
            double chance = baseChance * (resistSkill / 100.0);

            if (chance <= 0.0)
                return false;

            target.CheckSkill(SkillName.MagicResist, 0.0, target.Skills[SkillName.MagicResist].Cap);

            return chance >= Utility.RandomDouble();
        }

        public virtual double GetResistPercentForCircle(Mobile target, SpellCircle circle)
        {
            int circleNumber = (int)circle + 1;
            double baseChance = Math.Max(0.0, 40.0 - circleNumber * 5.0);
            return baseChance * (GetResistSkill(target) / 100.0);
        }

        public virtual double GetResistPercent(Mobile target)
        {
            return GetResistPercentForCircle(target, Circle);
        }
    }
}
