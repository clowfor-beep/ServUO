// ============================================================
// MagicClothingSystem.cs
// Scripts/Custom/MagicClothingSystem.cs
//
// Generates magic shoes and tunics with up to 4 random properties:
//   - Skill bonus (any skill, +1 to +10)
//   - Life/Mana/Stamina Leech (1–25%)
//   - Hit/Mana/Stamina Regen (1–5)
//   - Luck (10–80)
//   - Movement Speed Increase (5–20%)
//
// Usage: MagicClothingSystem.RandomMagicShoes()
//        MagicClothingSystem.RandomMagicTunic()
// ============================================================

using System;
using System.Collections.Generic;
using Server;
using Server.Items;

namespace Server.Custom
{
    public static class MagicClothingSystem
    {
        // ── Item type pools ───────────────────────────────────────────────────

        private static readonly Type[] ShoeTypes = new Type[]
        {
            typeof(Boots), typeof(Shoes), typeof(Sandals), typeof(ThighBoots),
            typeof(FurBoots), typeof(ElvenBoots), typeof(NinjaTabi),
            typeof(SamuraiTabi), typeof(Waraji), typeof(JesterShoes)
        };

        private static readonly Type[] TunicTypes = new Type[]
        {
            typeof(Tunic), typeof(Doublet), typeof(Surcoat), typeof(BodySash),
            typeof(FullApron), typeof(FormalShirt), typeof(JesterSuit), typeof(JinBaori)
        };

        // ── Property enum ─────────────────────────────────────────────────────

        private enum Prop
        {
            SkillBonus,
            LifeLeech,
            ManaLeech,
            StamLeech,
            HitRegen,
            ManaRegen,
            StamRegen,
            Luck,
            MovementSpeed
        }

        // ── Public API ────────────────────────────────────────────────────────

        public static BaseClothing RandomMagicShoes() => Generate(ShoeTypes);
        public static BaseClothing RandomMagicTunic() => Generate(TunicTypes);

        // ── Generation ────────────────────────────────────────────────────────

        private static BaseClothing Generate(Type[] pool)
        {
            Type type = pool[Utility.Random(pool.Length)];
            BaseClothing item = Activator.CreateInstance(type) as BaseClothing;

            if (item == null)
                return null;

            // Roll 1–4 unique properties
            int propCount = Utility.RandomMinMax(1, 4);
            List<Prop> available = new List<Prop>((Prop[])Enum.GetValues(typeof(Prop)));
            for (int j = available.Count - 1; j > 0; j--)
            {
                int k = Utility.Random(j + 1);
                Prop tmp = available[j]; available[j] = available[k]; available[k] = tmp;
            }

            for (int i = 0; i < propCount && i < available.Count; i++)
                ApplyProperty(item, available[i]);

            // Give it a small hue to distinguish it as magic
            if (item.Hue == 0)
                item.Hue = 1150;

            return item;
        }

        private static void ApplyProperty(BaseClothing item, Prop prop)
        {
            switch (prop)
            {
                case Prop.SkillBonus:
                    SkillName skill = GetRandomSkill(item);
                    double bonus = Utility.RandomMinMax(1, 10);
                    // Find first empty skill bonus slot
                    for (int i = 0; i < 5; i++)
                    {
                        SkillName existing; double existingBonus;
                        if (!item.SkillBonuses.GetValues(i, out existing, out existingBonus))
                        {
                            item.SkillBonuses.SetValues(i, skill, bonus);
                            break;
                        }
                    }
                    break;

                case Prop.LifeLeech:
                    item.WeaponAttributes.HitLeechHits = Utility.RandomMinMax(1, 25);
                    break;

                case Prop.ManaLeech:
                    item.WeaponAttributes.HitLeechMana = Utility.RandomMinMax(1, 25);
                    break;

                case Prop.StamLeech:
                    item.WeaponAttributes.HitLeechStam = Utility.RandomMinMax(1, 25);
                    break;

                case Prop.HitRegen:
                    item.Attributes.RegenHits = Utility.RandomMinMax(1, 5);
                    break;

                case Prop.ManaRegen:
                    item.Attributes.RegenMana = Utility.RandomMinMax(1, 5);
                    break;

                case Prop.StamRegen:
                    item.Attributes.RegenStam = Utility.RandomMinMax(1, 5);
                    break;

                case Prop.Luck:
                    item.Attributes.Luck = Utility.RandomMinMax(10, 80);
                    break;

                case Prop.MovementSpeed:
                    item.MovementSpeedBonus = Utility.RandomMinMax(5, 20);
                    break;
            }
        }

        private static SkillName GetRandomSkill(BaseClothing item)
        {
            SkillName[] allSkills = (SkillName[])Enum.GetValues(typeof(SkillName));
            SkillName sk;

            do
            {
                sk = allSkills[Utility.Random(allSkills.Length)];
            }
            while (SkillAlreadyOnItem(item, sk));

            return sk;
        }

        private static bool SkillAlreadyOnItem(BaseClothing item, SkillName skill)
        {
            for (int i = 0; i < 5; i++)
            {
                SkillName existing; double bonus;
                if (item.SkillBonuses.GetValues(i, out existing, out bonus) && existing == skill)
                    return true;
            }
            return false;
        }
    }
}
