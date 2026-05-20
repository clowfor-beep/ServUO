// ============================================================
// OrbAndCurrencySystem.cs
// Scripts/Custom/OrbAndCurrencySystem.cs
//
// Implements the Orb and Currency system for the Fun Stuff shard.
//
// Contents:
//   EssenceShard           — 3rd currency, stackable
//
//   Category 1 — Character Orbs (modify the character)
//     OrbOfEnhancement     — +skill points (3 tiers)
//     OrbOfMastery         — +stat points (2 tiers)
//     OrbOfExpansion       — +total skill cap (3 tiers)
//     OrbOfFortitude       — +total stat cap (2 tiers)
//     OrbOfAlacrity        — halves skill gain duration (3 tiers)
//     OrbOfBalance         — redistribute skill/stat points (3 tiers)
//
//   Category 2 — Item Orbs (modify an item)
//     OrbOfCorruption      — risky random enhancement, 50% destroy
//     OrbOfResonance       — push property beyond cap, 25% destroy
//     OrbOfCleansing       — remove property (random or chosen)
//     OrbOfTempering       — improve base item quality
//     OrbOfEnchantment     — add random magical property
//     OrbOfReforging       — randomise all properties
//
//   Category 3 — Scrolls (temporary buffs)
//     ScrollOfWarding      — absorb damage shield (3 tiers)
//     ScrollOfDeathtouch   — 1% instant-kill on hit (1 tier)
//     ScrollOfExecution    — bonus damage at low target HP (3 tiers)
//     ScrollOfLeeching     — drain random stat on hit (3 tiers)
//
// Design doc: Design/OrbAndCurrencyDesignDoc.txt
// ============================================================

using System;
using System.Collections.Generic;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Targeting;
using Server.Gumps;

namespace Server.Custom
{
    // ============================================================
    // SKILL / STAT CEILING CONSTANTS
    // ============================================================

    // ============================================================
    // EQUIPMENT HELPER — ServUO has no single BaseEquipment class;
    // SkillBonuses exist on BaseWeapon, BaseArmor, BaseClothing,
    // BaseJewel. This helper unifies access.
    // ============================================================

    public static class EquipHelper
    {
        public static bool TryGetSkillBonuses(Item item, out AosSkillBonuses bonuses)
        {
            if (item is BaseWeapon  w) { bonuses = w.SkillBonuses; return true; }
            if (item is BaseArmor   a) { bonuses = a.SkillBonuses; return true; }
            if (item is BaseClothing c) { bonuses = c.SkillBonuses; return true; }
            if (item is BaseJewel   j) { bonuses = j.SkillBonuses; return true; }
            bonuses = null;
            return false;
        }

        public static bool IsEquippable(Item item) =>
            item is BaseWeapon || item is BaseArmor || item is BaseClothing || item is BaseJewel;
    }

    public static class OrbCeilings
    {
        public const double MaxEnhancedSkill    = 130.0;   // individual skill ceiling
        public const int    MaxSkillCap         = 9000;    // total skill cap in ServUO units (900.0)
        public const int    MaxStatValue        = 150;     // individual stat ceiling
        public const int    MaxStatCap          = 250;     // total stat cap
    }

    // ============================================================
    // ESSENCE SHARD — the 3rd shard currency
    // ============================================================

    public class EssenceShard : Item
    {
        [Constructable]
        public EssenceShard() : this(1) { }

        [Constructable]
        public EssenceShard(int amount) : base(0x1F19)
        {
            Name      = "an essence shard";
            Hue       = 1153;
            Stackable = true;
            Amount    = amount;
            Weight    = 0.0;
        }

        public EssenceShard(Serial serial) : base(serial) { }

        public override void GetProperties(ObjectPropertyList list)
        {
            base.GetProperties(list);
            list.Add("Currency of augmentation");
        }

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write(0);
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            reader.ReadInt();
        }
    }

    // ============================================================
    // CATEGORY 1 — CHARACTER ORBS
    // ============================================================

    // ----------------------------------------------------------
    // ORB OF ENHANCEMENT — +skill points to a chosen skill
    // ----------------------------------------------------------

    public class OrbOfEnhancement : Item
    {
        private int _tier;

        [CommandProperty(AccessLevel.GameMaster)]
        public int Tier
        {
            get => _tier;
            set { _tier = Math.Max(1, Math.Min(3, value)); InvalidateProperties(); }
        }

        public double SkillBonus => _tier == 1 ? 0.1 : _tier == 2 ? 0.2 : 0.3;

        [Constructable]
        public OrbOfEnhancement() : this(1) { }

        [Constructable]
        public OrbOfEnhancement(int tier) : base(0x0E2D)
        {
            _tier  = Math.Max(1, Math.Min(3, tier));
            Hue    = 1153;
            Weight = 1.0;
            Name   = $"an orb of enhancement ({TierLabel()})";
        }

        public OrbOfEnhancement(Serial serial) : base(serial) { }

        private string TierLabel() => _tier == 1 ? "minor" : _tier == 2 ? "greater" : "superior";

        public override void GetProperties(ObjectPropertyList list)
        {
            base.GetProperties(list);
            list.Add($"Permanently raises one skill by +{SkillBonus:0.0}");
            list.Add($"Individual skill ceiling: {OrbCeilings.MaxEnhancedSkill}");
        }

        public override void OnDoubleClick(Mobile from)
        {
            if (!IsChildOf(from.Backpack))
            {
                from.SendLocalizedMessage(1042001);
                return;
            }

            from.SendMessage(0x35, "Choose a skill to enhance.");
            from.SendGump(new SkillSelectGump(from, this));
        }

        public void ApplyToSkill(Mobile from, SkillName skillName)
        {
            Skill skill = from.Skills[skillName];

            if (skill == null)
            {
                from.SendMessage("That is not a valid skill.");
                return;
            }

            if (skill.Base >= OrbCeilings.MaxEnhancedSkill)
            {
                from.SendMessage(0x22, $"Your {skill.Name} is already at the enhancement ceiling ({OrbCeilings.MaxEnhancedSkill}).");
                return;
            }

            double newValue = Math.Min(skill.Base + SkillBonus, OrbCeilings.MaxEnhancedSkill);
            skill.Base = newValue;

            from.SendMessage(0x35, $"Your {skill.Name} has been enhanced to {newValue:0.0}.");
            from.PlaySound(0x1F7);
            from.FixedParticles(0x375A, 9, 20, 5016, Hue, 0, EffectLayer.Waist);
            Delete();
        }

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write(0);
            writer.Write(_tier);
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            reader.ReadInt();
            _tier = reader.ReadInt();
        }
    }

    // ----------------------------------------------------------
    // ORB OF MASTERY — +stat points to a chosen stat
    // ----------------------------------------------------------

    public class OrbOfMastery : Item
    {
        private int _tier;

        [CommandProperty(AccessLevel.GameMaster)]
        public int Tier
        {
            get => _tier;
            set { _tier = Math.Max(1, Math.Min(2, value)); InvalidateProperties(); }
        }

        public int StatBonus => _tier == 1 ? 1 : 2;

        [Constructable]
        public OrbOfMastery() : this(1) { }

        [Constructable]
        public OrbOfMastery(int tier) : base(0x0E2D)
        {
            _tier  = Math.Max(1, Math.Min(2, tier));
            Hue    = 1175;
            Weight = 1.0;
            Name   = $"an orb of mastery ({TierLabel()})";
        }

        public OrbOfMastery(Serial serial) : base(serial) { }

        private string TierLabel() => _tier == 1 ? "rare" : "very rare";

        public override void GetProperties(ObjectPropertyList list)
        {
            base.GetProperties(list);
            list.Add($"Permanently raises one stat by +{StatBonus}");
            list.Add($"Individual stat ceiling: {OrbCeilings.MaxStatValue}");
        }

        public override void OnDoubleClick(Mobile from)
        {
            if (!IsChildOf(from.Backpack))
            {
                from.SendLocalizedMessage(1042001);
                return;
            }

            from.SendGump(new StatSelectGump(from, this));
        }

        public void ApplyToStat(Mobile from, StatType stat)
        {
            int current;
            switch (stat)
            {
                case StatType.Str: current = from.RawStr; break;
                case StatType.Dex: current = from.RawDex; break;
                default:           current = from.RawInt; break;
            }

            if (current >= OrbCeilings.MaxStatValue)
            {
                from.SendMessage(0x22, $"That stat is already at the enhancement ceiling ({OrbCeilings.MaxStatValue}).");
                return;
            }

            int newVal = Math.Min(current + StatBonus, OrbCeilings.MaxStatValue);

            switch (stat)
            {
                case StatType.Str: from.RawStr = newVal; break;
                case StatType.Dex: from.RawDex = newVal; break;
                default:           from.RawInt = newVal; break;
            }

            string statName = stat == StatType.Str ? "Strength" : stat == StatType.Dex ? "Dexterity" : "Intelligence";
            from.SendMessage(0x35, $"Your {statName} has been increased to {newVal}.");
            from.PlaySound(0x1F7);
            from.FixedParticles(0x375A, 9, 20, 5016, Hue, 0, EffectLayer.Waist);
            Delete();
        }

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write(0);
            writer.Write(_tier);
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            reader.ReadInt();
            _tier = reader.ReadInt();
        }
    }

    // ----------------------------------------------------------
    // ORB OF EXPANSION — raises total skill cap
    // ----------------------------------------------------------

    public class OrbOfExpansion : Item
    {
        private int _tier;

        [CommandProperty(AccessLevel.GameMaster)]
        public int Tier
        {
            get => _tier;
            set { _tier = Math.Max(1, Math.Min(3, value)); InvalidateProperties(); }
        }

        public int CapBonus => _tier == 1 ? 10 : _tier == 2 ? 20 : 30;  // ServUO stores cap as x10

        [Constructable]
        public OrbOfExpansion() : this(1) { }

        [Constructable]
        public OrbOfExpansion(int tier) : base(0x0E2D)
        {
            _tier  = Math.Max(1, Math.Min(3, tier));
            Hue    = 1171;
            Weight = 1.0;
            Name   = $"an orb of expansion ({TierLabel()})";
        }

        public OrbOfExpansion(Serial serial) : base(serial) { }

        private string TierLabel() => _tier == 1 ? "uncommon" : _tier == 2 ? "rare" : "very rare";

        public override void GetProperties(ObjectPropertyList list)
        {
            base.GetProperties(list);
            list.Add($"Permanently raises total skill cap by +{_tier} point{(_tier > 1 ? "s" : "")}");
            list.Add($"Skill cap ceiling: {OrbCeilings.MaxSkillCap / 10}");
        }

        public override void OnDoubleClick(Mobile from)
        {
            if (!IsChildOf(from.Backpack))
            {
                from.SendLocalizedMessage(1042001);
                return;
            }

            if (from.SkillsCap >= OrbCeilings.MaxSkillCap)
            {
                from.SendMessage(0x22, $"Your skill cap is already at the ceiling ({OrbCeilings.MaxSkillCap / 10}).");
                return;
            }

            int newCap = Math.Min(from.SkillsCap + CapBonus, OrbCeilings.MaxSkillCap);
            from.SkillsCap = newCap;

            from.SendMessage(0x35, $"Your total skill cap has been raised to {newCap / 10}.");
            from.PlaySound(0x1F7);
            from.FixedParticles(0x375A, 9, 20, 5016, Hue, 0, EffectLayer.Waist);
            Delete();
        }

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write(0);
            writer.Write(_tier);
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            reader.ReadInt();
            _tier = reader.ReadInt();
        }
    }

    // ----------------------------------------------------------
    // ORB OF FORTITUDE — raises total stat cap
    // ----------------------------------------------------------

    public class OrbOfFortitude : Item
    {
        private int _tier;

        [CommandProperty(AccessLevel.GameMaster)]
        public int Tier
        {
            get => _tier;
            set { _tier = Math.Max(1, Math.Min(2, value)); InvalidateProperties(); }
        }

        public int CapBonus => _tier == 1 ? 1 : 2;

        [Constructable]
        public OrbOfFortitude() : this(1) { }

        [Constructable]
        public OrbOfFortitude(int tier) : base(0x0E2D)
        {
            _tier  = Math.Max(1, Math.Min(2, tier));
            Hue    = 1157;
            Weight = 1.0;
            Name   = $"an orb of fortitude ({TierLabel()})";
        }

        public OrbOfFortitude(Serial serial) : base(serial) { }

        private string TierLabel() => _tier == 1 ? "rare" : "very rare";

        public override void GetProperties(ObjectPropertyList list)
        {
            base.GetProperties(list);
            list.Add($"Permanently raises total stat cap by +{CapBonus}");
            list.Add($"Stat cap ceiling: {OrbCeilings.MaxStatCap}");
        }

        public override void OnDoubleClick(Mobile from)
        {
            if (!IsChildOf(from.Backpack))
            {
                from.SendLocalizedMessage(1042001);
                return;
            }

            if (from.StatCap >= OrbCeilings.MaxStatCap)
            {
                from.SendMessage(0x22, $"Your stat cap is already at the ceiling ({OrbCeilings.MaxStatCap}).");
                return;
            }

            int newCap = Math.Min(from.StatCap + CapBonus, OrbCeilings.MaxStatCap);
            from.StatCap = newCap;

            from.SendMessage(0x35, $"Your total stat cap has been raised to {newCap}.");
            from.PlaySound(0x1F7);
            from.FixedParticles(0x375A, 9, 20, 5016, Hue, 0, EffectLayer.Waist);
            Delete();
        }

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write(0);
            writer.Write(_tier);
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            reader.ReadInt();
            _tier = reader.ReadInt();
        }
    }

    // ----------------------------------------------------------
    // ORB OF ALACRITY — halves skill gain duration
    // ----------------------------------------------------------

    public class OrbOfAlacrity : Item
    {
        private int _tier;

        [CommandProperty(AccessLevel.GameMaster)]
        public int Tier
        {
            get => _tier;
            set { _tier = Math.Max(1, Math.Min(3, value)); InvalidateProperties(); }
        }

        public TimeSpan Duration => TimeSpan.FromMinutes(_tier == 1 ? 30 : _tier == 2 ? 60 : 120);

        // Tracks active Alacrity end times per mobile
        private static readonly Dictionary<Mobile, DateTime> _activeBuffs = new Dictionary<Mobile, DateTime>();

        public static bool IsActive(Mobile m) =>
            _activeBuffs.TryGetValue(m, out DateTime end) && DateTime.UtcNow < end;

        public static void RemoveBuff(Mobile m) => _activeBuffs.Remove(m);

        [Constructable]
        public OrbOfAlacrity() : this(1) { }

        [Constructable]
        public OrbOfAlacrity(int tier) : base(0x0E2D)
        {
            _tier  = Math.Max(1, Math.Min(3, tier));
            Hue    = 1161;
            Weight = 1.0;
            Name   = $"an orb of alacrity ({TierLabel()})";
        }

        public OrbOfAlacrity(Serial serial) : base(serial) { }

        private string TierLabel() => _tier == 1 ? "uncommon" : _tier == 2 ? "rare" : "very rare";

        public override void GetProperties(ObjectPropertyList list)
        {
            base.GetProperties(list);
            list.Add($"Halves skill gain duration for {(int)Duration.TotalMinutes} minutes");
        }

        public override void OnDoubleClick(Mobile from)
        {
            if (!IsChildOf(from.Backpack))
            {
                from.SendLocalizedMessage(1042001);
                return;
            }

            if (IsActive(from))
            {
                from.SendMessage(0x22, "You already have an alacrity buff active.");
                return;
            }

            _activeBuffs[from] = DateTime.UtcNow + Duration;
            from.SendMessage(0x35, $"You feel your learning quicken! Skill gains doubled for {(int)Duration.TotalMinutes} minutes.");
            from.PlaySound(0x1F7);
            from.FixedParticles(0x375A, 9, 20, 5016, Hue, 0, EffectLayer.Waist);

            // Auto-expire
            Timer.DelayCall(Duration, () =>
            {
                if (_activeBuffs.TryGetValue(from, out DateTime end) && DateTime.UtcNow >= end)
                {
                    _activeBuffs.Remove(from);
                    if (from.NetState != null)
                        from.SendMessage(0x22, "Your alacrity buff has faded.");
                }
            });

            Delete();
        }

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write(0);
            writer.Write(_tier);
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            reader.ReadInt();
            _tier = reader.ReadInt();
        }
    }

    // ----------------------------------------------------------
    // ORB OF BALANCE — redistribute skill or stat points
    //   Tier 1: transfer up to 10 skill points
    //   Tier 2: transfer up to 50 skill points
    //   Tier 3: transfer up to 20 stat points
    // ----------------------------------------------------------

    public class OrbOfBalance : Item
    {
        private int _tier;

        [CommandProperty(AccessLevel.GameMaster)]
        public int Tier
        {
            get => _tier;
            set { _tier = Math.Max(1, Math.Min(3, value)); InvalidateProperties(); }
        }

        [Constructable]
        public OrbOfBalance() : this(1) { }

        [Constructable]
        public OrbOfBalance(int tier) : base(0x0E2D)
        {
            _tier  = Math.Max(1, Math.Min(3, tier));
            Hue    = 1165;
            Weight = 1.0;
            Name   = $"an orb of balance ({TierLabel()})";
        }

        public OrbOfBalance(Serial serial) : base(serial) { }

        private string TierLabel()
        {
            switch (_tier)
            {
                case 1:  return "skill retraining";
                case 2:  return "skill mastery transfer";
                default: return "stat retraining";
            }
        }

        public override void GetProperties(ObjectPropertyList list)
        {
            base.GetProperties(list);
            switch (_tier)
            {
                case 1: list.Add("Transfer up to 10 skill points between skills"); break;
                case 2: list.Add("Transfer up to 50 skill points between skills"); break;
                case 3: list.Add("Transfer up to 20 stat points between stats");   break;
            }
            list.Add("Single character only — cannot transfer between characters");
        }

        public override void OnDoubleClick(Mobile from)
        {
            if (!IsChildOf(from.Backpack))
            {
                from.SendLocalizedMessage(1042001);
                return;
            }

            from.SendGump(new BalanceOrbGump(from, this));
        }

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write(0);
            writer.Write(_tier);
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            reader.ReadInt();
            _tier = reader.ReadInt();
        }
    }

    // ============================================================
    // CATEGORY 2 — ITEM ORBS
    // ============================================================

    // ----------------------------------------------------------
    // ORB OF CORRUPTION — risky random enhancement, 50% destroy
    // ----------------------------------------------------------

    public class OrbOfCorruption : Item
    {
        private int _tier;

        [CommandProperty(AccessLevel.GameMaster)]
        public int Tier
        {
            get => _tier;
            set { _tier = Math.Max(1, Math.Min(3, value)); InvalidateProperties(); }
        }

        [Constructable]
        public OrbOfCorruption() : this(1) { }

        [Constructable]
        public OrbOfCorruption(int tier) : base(0x0E2D)
        {
            _tier  = Math.Max(1, Math.Min(3, tier));
            Hue    = 1109;
            Weight = 1.0;
            Name   = $"an orb of corruption ({TierLabel()})";
        }

        public OrbOfCorruption(Serial serial) : base(serial) { }

        private string TierLabel() => _tier == 1 ? "minor" : _tier == 2 ? "moderate" : "major";

        public override void GetProperties(ObjectPropertyList list)
        {
            base.GetProperties(list);
            list.Add("Applies a powerful random effect to an item");
            list.Add("50% chance to destroy the item");
            list.Add("Corrupted items cannot be corrupted again");
        }

        public override void OnDoubleClick(Mobile from)
        {
            if (!IsChildOf(from.Backpack))
            {
                from.SendLocalizedMessage(1042001);
                return;
            }

            from.SendMessage(0x22, "Warning: 50% chance to destroy the target item!");
            from.SendMessage(0x35, "Target an item to corrupt.");
            from.Target = new CorruptTarget(this);
        }

        private class CorruptTarget : Target
        {
            private readonly OrbOfCorruption _orb;

            public CorruptTarget(OrbOfCorruption orb) : base(2, false, TargetFlags.None)
            {
                _orb = orb;
            }

            protected override void OnTarget(Mobile from, object targeted)
            {
                if (!(targeted is Item item))
                {
                    from.SendMessage("That is not an item.");
                    return;
                }

                if (!item.IsChildOf(from.Backpack))
                {
                    from.SendMessage("The item must be in your backpack.");
                    return;
                }

                if (item.Name != null && item.Name.Contains("[Corrupted]"))
                {
                    from.SendMessage(0x22, "That item has already been corrupted.");
                    return;
                }

                // 50% destruction
                if (Utility.RandomDouble() < 0.50)
                {
                    from.SendMessage(0x22, "The corruption overwhelms the item — it shatters!");
                    from.PlaySound(0x1D8);
                    item.Delete();
                    _orb.Delete();
                    return;
                }

                // Apply corruption effect based on tier
                ApplyCorruption(from, item, _orb.Tier);
                item.Name = (item.Name ?? item.DefaultName ?? "an item") + " [Corrupted]";

                from.SendMessage(0x35, "The item has been corrupted!");
                from.PlaySound(0x213);
                from.FixedParticles(0x36BD, 20, 10, 5044, EffectLayer.Head);
                _orb.Delete();
            }

            private static void ApplyCorruption(Mobile from, Item item, int tier)
            {
                // Apply a skill bonus to the item based on tier
                // In ServUO this is done via AosSkillBonuses on equipment
                AosSkillBonuses bonuses;
                if (EquipHelper.TryGetSkillBonuses(item, out bonuses))
                {
                    int bonus = tier == 1 ? 2 : tier == 2 ? 5 : 10;
                    SkillName[] skills = (SkillName[])Enum.GetValues(typeof(SkillName));
                    SkillName randSkill = skills[Utility.Random(skills.Length)];

                    bonuses.SetValues(0, randSkill, bonus);
                    from.SendMessage(0x35, $"The item now grants +{bonus} to {randSkill}.");
                }
                else
                {
                    from.SendMessage(0x35, $"A strange energy lingers in the item.");
                }
            }

            protected override void OnTargetCancel(Mobile from, TargetCancelType cancelType)
            {
                from.SendMessage("Corruption cancelled.");
            }
        }

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write(0);
            writer.Write(_tier);
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            reader.ReadInt();
            _tier = reader.ReadInt();
        }
    }

    // ----------------------------------------------------------
    // ORB OF RESONANCE — push property beyond cap, 25% destroy
    // ----------------------------------------------------------

    public class OrbOfResonance : Item
    {
        private int _tier;

        [CommandProperty(AccessLevel.GameMaster)]
        public int Tier
        {
            get => _tier;
            set { _tier = Math.Max(1, Math.Min(3, value)); InvalidateProperties(); }
        }

        public int DamageBonus => _tier == 1 ? 1 : _tier == 2 ? 3 : 5;

        [Constructable]
        public OrbOfResonance() : this(1) { }

        [Constructable]
        public OrbOfResonance(int tier) : base(0x0E2D)
        {
            _tier  = Math.Max(1, Math.Min(3, tier));
            Hue    = 1266;
            Weight = 1.0;
            Name   = $"an orb of resonance ({TierLabel()})";
        }

        public OrbOfResonance(Serial serial) : base(serial) { }

        private string TierLabel() => _tier == 1 ? "rare" : _tier == 2 ? "very rare" : "epic";

        public override void GetProperties(ObjectPropertyList list)
        {
            base.GetProperties(list);
            list.Add($"Pushes weapon damage beyond normal cap (+{DamageBonus})");
            list.Add("25% chance to destroy the item");
        }

        public override void OnDoubleClick(Mobile from)
        {
            if (!IsChildOf(from.Backpack))
            {
                from.SendLocalizedMessage(1042001);
                return;
            }

            from.SendMessage(0x22, "Warning: 25% chance to destroy the target item!");
            from.SendMessage(0x35, "Target a weapon or armour to resonate.");
            from.Target = new ResonanceTarget(this);
        }

        private class ResonanceTarget : Target
        {
            private readonly OrbOfResonance _orb;

            public ResonanceTarget(OrbOfResonance orb) : base(2, false, TargetFlags.None)
            {
                _orb = orb;
            }

            protected override void OnTarget(Mobile from, object targeted)
            {
                if (!(targeted is Item item) || !item.IsChildOf(from.Backpack))
                {
                    from.SendMessage("Target an item in your backpack.");
                    return;
                }

                // 25% destruction
                if (Utility.RandomDouble() < 0.25)
                {
                    from.SendMessage(0x22, "The resonance fractures the item — it crumbles to dust!");
                    from.PlaySound(0x1D8);
                    item.Delete();
                    _orb.Delete();
                    return;
                }

                // Apply damage bonus to weapons, AR to armour
                if (item is BaseWeapon weapon)
                {
                    weapon.MaxDamage += _orb.DamageBonus;
                    weapon.MinDamage += _orb.DamageBonus;
                    from.SendMessage(0x35, $"The weapon pulses with amplified power (+{_orb.DamageBonus} damage).");
                }
                else if (item is BaseArmor armor)
                {
                    armor.ArmorBase += _orb.DamageBonus;
                    from.SendMessage(0x35, $"The armour resonates with hardened protection (+{_orb.DamageBonus} AR).");
                }
                else
                {
                    from.SendMessage(0x22, "Resonance has no effect on that type of item.");
                    return;
                }

                from.PlaySound(0x213);
                from.FixedParticles(0x375A, 9, 20, 5016, _orb.Hue, 0, EffectLayer.Waist);
                _orb.Delete();
            }

            protected override void OnTargetCancel(Mobile from, TargetCancelType cancelType)
            {
                from.SendMessage("Resonance cancelled.");
            }
        }

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write(0);
            writer.Write(_tier);
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            reader.ReadInt();
            _tier = reader.ReadInt();
        }
    }

    // ----------------------------------------------------------
    // ORB OF CLEANSING — remove a property from an item
    //   Tier 1: removes one RANDOM property
    //   Tier 2: removes one CHOSEN property
    // ----------------------------------------------------------

    public class OrbOfCleansing : Item
    {
        private int _tier;

        [CommandProperty(AccessLevel.GameMaster)]
        public int Tier
        {
            get => _tier;
            set { _tier = Math.Max(1, Math.Min(2, value)); InvalidateProperties(); }
        }

        [Constructable]
        public OrbOfCleansing() : this(1) { }

        [Constructable]
        public OrbOfCleansing(int tier) : base(0x0E2D)
        {
            _tier  = Math.Max(1, Math.Min(2, tier));
            Hue    = 1150;
            Weight = 1.0;
            Name   = $"an orb of cleansing ({TierLabel()})";
        }

        public OrbOfCleansing(Serial serial) : base(serial) { }

        private string TierLabel() => _tier == 1 ? "random removal" : "targeted removal";

        public override void GetProperties(ObjectPropertyList list)
        {
            base.GetProperties(list);
            if (_tier == 1)
                list.Add("Removes one random magical property from an item");
            else
                list.Add("Removes one chosen magical property from an item");
            list.Add("No destruction risk");
        }

        public override void OnDoubleClick(Mobile from)
        {
            if (!IsChildOf(from.Backpack))
            {
                from.SendLocalizedMessage(1042001);
                return;
            }

            from.SendMessage(0x35, _tier == 1
                ? "Target an item to remove a random property."
                : "Target an item to select a property to remove.");
            from.Target = new CleanseTarget(this);
        }

        private class CleanseTarget : Target
        {
            private readonly OrbOfCleansing _orb;

            public CleanseTarget(OrbOfCleansing orb) : base(2, false, TargetFlags.None)
            {
                _orb = orb;
            }

            protected override void OnTarget(Mobile from, object targeted)
            {
                if (!(targeted is Item item) || !item.IsChildOf(from.Backpack))
                {
                    from.SendMessage("Target an item in your backpack.");
                    return;
                }

                AosSkillBonuses cleanseBonuses;
                if (!EquipHelper.TryGetSkillBonuses(item, out cleanseBonuses))
                {
                    from.SendMessage(0x22, "That item has no magical properties to cleanse.");
                    return;
                }

                if (_orb.Tier == 1)
                {
                    bool removed = false;
                    for (int i = 0; i < 5; i++)
                    {
                        SkillName skill;
                        double bonus;
                        cleanseBonuses.GetValues(i, out skill, out bonus);
                        if (bonus != 0)
                        {
                            cleanseBonuses.SetValues(i, SkillName.Alchemy, 0);
                            from.SendMessage(0x35, $"A random property has been removed from the item.");
                            removed = true;
                            break;
                        }
                    }

                    if (!removed)
                        from.SendMessage(0x22, "That item has no removable properties.");
                    else
                        _orb.Delete();
                }
                else
                {
                    // Tier 2: open a gump to choose which skill bonus to remove
                    from.SendGump(new CleansingSelectGump(from, cleanseBonuses, item, _orb));
                }

                from.PlaySound(0x1F5);
            }

            protected override void OnTargetCancel(Mobile from, TargetCancelType cancelType)
            {
                from.SendMessage("Cleansing cancelled.");
            }
        }

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write(0);
            writer.Write(_tier);
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            reader.ReadInt();
            _tier = reader.ReadInt();
        }
    }

    // ----------------------------------------------------------
    // ORB OF TEMPERING — improves base item quality
    // ----------------------------------------------------------

    public class OrbOfTempering : Item
    {
        private int _tier;

        [CommandProperty(AccessLevel.GameMaster)]
        public int Tier
        {
            get => _tier;
            set { _tier = Math.Max(1, Math.Min(3, value)); InvalidateProperties(); }
        }

        public int QualityBonus => _tier == 1 ? 5 : _tier == 2 ? 10 : 20;

        [Constructable]
        public OrbOfTempering() : this(1) { }

        [Constructable]
        public OrbOfTempering(int tier) : base(0x0E2D)
        {
            _tier  = Math.Max(1, Math.Min(3, tier));
            Hue    = 1190;
            Weight = 1.0;
            Name   = $"an orb of tempering ({TierLabel()})";
        }

        public OrbOfTempering(Serial serial) : base(serial) { }

        private string TierLabel() => _tier == 1 ? "common" : _tier == 2 ? "uncommon" : "rare";

        public override void GetProperties(ObjectPropertyList list)
        {
            base.GetProperties(list);
            list.Add($"Raises item maximum durability by +{QualityBonus}");
            list.Add("No destruction risk");
        }

        public override void OnDoubleClick(Mobile from)
        {
            if (!IsChildOf(from.Backpack))
            {
                from.SendLocalizedMessage(1042001);
                return;
            }

            from.SendMessage(0x35, "Target an item to temper.");
            from.Target = new TemperTarget(this);
        }

        private class TemperTarget : Target
        {
            private readonly OrbOfTempering _orb;

            public TemperTarget(OrbOfTempering orb) : base(2, false, TargetFlags.None)
            {
                _orb = orb;
            }

            protected override void OnTarget(Mobile from, object targeted)
            {
                if (!(targeted is Item item) || !item.IsChildOf(from.Backpack))
                {
                    from.SendMessage("Target an item in your backpack.");
                    return;
                }

                if (item is BaseWeapon w)
                {
                    w.MaxHitPoints = Math.Min(w.MaxHitPoints + _orb.QualityBonus, 255);
                    w.HitPoints    = Math.Min(w.HitPoints + _orb.QualityBonus, w.MaxHitPoints);
                    from.SendMessage(0x35, $"The weapon's durability has been improved.");
                }
                else if (item is BaseArmor a)
                {
                    a.MaxHitPoints = Math.Min(a.MaxHitPoints + _orb.QualityBonus, 255);
                    a.HitPoints    = Math.Min(a.HitPoints + _orb.QualityBonus, a.MaxHitPoints);
                    from.SendMessage(0x35, $"The armour's durability has been improved.");
                }
                else
                {
                    from.SendMessage(0x22, "Tempering has no effect on that item.");
                    return;
                }

                from.PlaySound(0x2A);
                from.FixedParticles(0x36FE, 1, 15, 9904, _orb.Hue, 0, EffectLayer.Waist);
                _orb.Delete();
            }

            protected override void OnTargetCancel(Mobile from, TargetCancelType cancelType)
            {
                from.SendMessage("Tempering cancelled.");
            }
        }

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write(0);
            writer.Write(_tier);
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            reader.ReadInt();
            _tier = reader.ReadInt();
        }
    }

    // ----------------------------------------------------------
    // ORB OF ENCHANTMENT — adds a random magical property
    // ----------------------------------------------------------

    public class OrbOfEnchantment : Item
    {
        private int _tier;

        [CommandProperty(AccessLevel.GameMaster)]
        public int Tier
        {
            get => _tier;
            set { _tier = Math.Max(1, Math.Min(3, value)); InvalidateProperties(); }
        }

        private static readonly SkillName[] CommonSkills = {
            SkillName.Swords, SkillName.Macing, SkillName.Fencing,
            SkillName.Archery, SkillName.Tactics, SkillName.Anatomy,
            SkillName.Healing, SkillName.Magery, SkillName.MagicResist,
            SkillName.Meditation, SkillName.EvalInt, SkillName.Parry
        };

        [Constructable]
        public OrbOfEnchantment() : this(1) { }

        [Constructable]
        public OrbOfEnchantment(int tier) : base(0x0E2D)
        {
            _tier  = Math.Max(1, Math.Min(3, tier));
            Hue    = 1272;
            Weight = 1.0;
            Name   = $"an orb of enchantment ({TierLabel()})";
        }

        public OrbOfEnchantment(Serial serial) : base(serial) { }

        private string TierLabel() => _tier == 1 ? "uncommon" : _tier == 2 ? "rare" : "very rare";

        public override void GetProperties(ObjectPropertyList list)
        {
            base.GetProperties(list);
            int bonus = _tier == 1 ? 3 : _tier == 2 ? 5 : 10;
            list.Add($"Adds one random magical property (+{bonus} to a random skill)");
            list.Add("No destruction risk");
        }

        public override void OnDoubleClick(Mobile from)
        {
            if (!IsChildOf(from.Backpack))
            {
                from.SendLocalizedMessage(1042001);
                return;
            }

            from.SendMessage(0x35, "Target an item to enchant.");
            from.Target = new EnchantTarget(this);
        }

        private class EnchantTarget : Target
        {
            private readonly OrbOfEnchantment _orb;

            public EnchantTarget(OrbOfEnchantment orb) : base(2, false, TargetFlags.None)
            {
                _orb = orb;
            }

            protected override void OnTarget(Mobile from, object targeted)
            {
                if (!(targeted is Item item) || !item.IsChildOf(from.Backpack))
                {
                    from.SendMessage("Target an item in your backpack.");
                    return;
                }

                AosSkillBonuses enchBonuses;
                if (!EquipHelper.TryGetSkillBonuses(item, out enchBonuses))
                {
                    from.SendMessage(0x22, "That item cannot be enchanted.");
                    return;
                }

                int slot = -1;
                for (int i = 0; i < 5; i++)
                {
                    SkillName sk;
                    double bv;
                    enchBonuses.GetValues(i, out sk, out bv);
                    if (bv == 0) { slot = i; break; }
                }

                if (slot < 0)
                {
                    from.SendMessage(0x22, "That item has no empty enchantment slots.");
                    return;
                }

                SkillName randSkill = CommonSkills[Utility.Random(CommonSkills.Length)];
                int bonus = _orb.Tier == 1 ? 3 : _orb.Tier == 2 ? 5 : 10;
                enchBonuses.SetValues(slot, randSkill, bonus);

                from.SendMessage(0x35, $"The item now grants +{bonus} to {randSkill}.");
                from.PlaySound(0x1F7);
                from.FixedParticles(0x375A, 9, 20, 5016, _orb.Hue, 0, EffectLayer.Waist);
                _orb.Delete();
            }

            protected override void OnTargetCancel(Mobile from, TargetCancelType cancelType)
            {
                from.SendMessage("Enchantment cancelled.");
            }
        }

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write(0);
            writer.Write(_tier);
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            reader.ReadInt();
            _tier = reader.ReadInt();
        }
    }

    // ----------------------------------------------------------
    // ORB OF REFORGING — randomises all properties on an item
    // ----------------------------------------------------------

    public class OrbOfReforging : Item
    {
        private int _tier;

        [CommandProperty(AccessLevel.GameMaster)]
        public int Tier
        {
            get => _tier;
            set { _tier = Math.Max(1, Math.Min(2, value)); InvalidateProperties(); }
        }

        [Constructable]
        public OrbOfReforging() : this(1) { }

        [Constructable]
        public OrbOfReforging(int tier) : base(0x0E2D)
        {
            _tier  = Math.Max(1, Math.Min(2, tier));
            Hue    = 1281;
            Weight = 1.0;
            Name   = $"an orb of reforging ({TierLabel()})";
        }

        public OrbOfReforging(Serial serial) : base(serial) { }

        private string TierLabel() => _tier == 1 ? "rare" : "very rare";

        public override void GetProperties(ObjectPropertyList list)
        {
            base.GetProperties(list);
            list.Add(_tier == 1
                ? "Randomises all magical properties (standard rolls)"
                : "Randomises all magical properties (biased toward high rolls)");
            list.Add("No destruction risk — number of properties preserved");
        }

        public override void OnDoubleClick(Mobile from)
        {
            if (!IsChildOf(from.Backpack))
            {
                from.SendLocalizedMessage(1042001);
                return;
            }

            from.SendMessage(0x22, "Warning: All current properties will be replaced!");
            from.SendMessage(0x35, "Target an item to reforge.");
            from.Target = new ReforgeTarget(this);
        }

        private static readonly SkillName[] CommonSkills = {
            SkillName.Swords, SkillName.Macing, SkillName.Fencing,
            SkillName.Archery, SkillName.Tactics, SkillName.Anatomy,
            SkillName.Healing, SkillName.Magery, SkillName.MagicResist,
            SkillName.Meditation, SkillName.EvalInt, SkillName.Parry
        };

        private class ReforgeTarget : Target
        {
            private readonly OrbOfReforging _orb;

            public ReforgeTarget(OrbOfReforging orb) : base(2, false, TargetFlags.None)
            {
                _orb = orb;
            }

            protected override void OnTarget(Mobile from, object targeted)
            {
                if (!(targeted is Item item) || !item.IsChildOf(from.Backpack))
                {
                    from.SendMessage("Target an item in your backpack.");
                    return;
                }

                AosSkillBonuses reforgeBonuses;
                if (!EquipHelper.TryGetSkillBonuses(item, out reforgeBonuses))
                {
                    from.SendMessage(0x22, "That item cannot be reforged.");
                    return;
                }

                int filledSlots = 0;
                for (int i = 0; i < 5; i++)
                {
                    SkillName sk;
                    double bv;
                    reforgeBonuses.GetValues(i, out sk, out bv);
                    if (bv != 0) filledSlots++;
                }

                if (filledSlots == 0)
                {
                    from.SendMessage(0x22, "That item has no properties to reforge.");
                    return;
                }

                for (int i = 0; i < 5; i++)
                    reforgeBonuses.SetValues(i, SkillName.Alchemy, 0);

                int minBonus = _orb.Tier == 1 ? 1 : 3;
                int maxBonus = _orb.Tier == 1 ? 5 : 10;

                for (int i = 0; i < filledSlots; i++)
                {
                    SkillName randSkill = CommonSkills[Utility.Random(CommonSkills.Length)];
                    int bonus = Utility.RandomMinMax(minBonus, maxBonus);
                    reforgeBonuses.SetValues(i, randSkill, bonus);
                }

                from.SendMessage(0x35, $"The item has been reforged with {filledSlots} new propert{(filledSlots > 1 ? "ies" : "y")}.");
                from.PlaySound(0x2A);
                from.FixedParticles(0x36BD, 20, 10, 5044, _orb.Hue, 0, EffectLayer.Head);
                _orb.Delete();
            }

            protected override void OnTargetCancel(Mobile from, TargetCancelType cancelType)
            {
                from.SendMessage("Reforging cancelled.");
            }
        }

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write(0);
            writer.Write(_tier);
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            reader.ReadInt();
            _tier = reader.ReadInt();
        }
    }

    // ============================================================
    // CATEGORY 3 — SCROLLS
    // ============================================================

    // ----------------------------------------------------------
    // SCROLL OF WARDING — absorbs incoming damage
    // ----------------------------------------------------------

    public class ScrollOfWarding : Item
    {
        private int _tier;

        [CommandProperty(AccessLevel.GameMaster)]
        public int Tier
        {
            get => _tier;
            set { _tier = Math.Max(1, Math.Min(3, value)); InvalidateProperties(); }
        }

        public int AbsorptionAmount => _tier == 1 ? 50 : _tier == 2 ? 100 : 150;

        // Active ward shields: mobile -> remaining absorption
        public static readonly Dictionary<Mobile, int> ActiveWards = new Dictionary<Mobile, int>();

        /// <summary>
        /// Called by the combat engine to reduce damage through an active ward.
        /// Returns the amount of damage that should actually be applied.
        /// </summary>
        public static int AbsorbDamage(Mobile defender, int damage)
        {
            if (!ActiveWards.ContainsKey(defender))
                return damage;

            int remaining = ActiveWards[defender];
            if (remaining <= 0)
            {
                ActiveWards.Remove(defender);
                return damage;
            }

            int absorbed = Math.Min(damage, remaining);
            remaining -= absorbed;

            if (remaining <= 0)
            {
                ActiveWards.Remove(defender);
                defender.SendMessage(0x22, "Your ward has been depleted.");
            }
            else
            {
                ActiveWards[defender] = remaining;
            }

            return damage - absorbed;
        }

        [Constructable]
        public ScrollOfWarding() : this(1) { }

        [Constructable]
        public ScrollOfWarding(int tier) : base(0x1F4E)
        {
            _tier  = Math.Max(1, Math.Min(3, tier));
            Hue    = 1150;
            Weight = 1.0;
            Name   = $"a scroll of warding ({TierLabel()})";
        }

        public ScrollOfWarding(Serial serial) : base(serial) { }

        private string TierLabel() => _tier == 1 ? "uncommon" : _tier == 2 ? "rare" : "very rare";

        public override void GetProperties(ObjectPropertyList list)
        {
            base.GetProperties(list);
            list.Add($"Absorbs up to {AbsorptionAmount} incoming damage");
            list.Add("Consumed when absorption is depleted");
        }

        public override void OnDoubleClick(Mobile from)
        {
            if (!IsChildOf(from.Backpack))
            {
                from.SendLocalizedMessage(1042001);
                return;
            }

            if (ActiveWards.ContainsKey(from))
            {
                from.SendMessage(0x22, "You already have a ward active.");
                return;
            }

            ActiveWards[from] = AbsorptionAmount;
            from.SendMessage(0x35, $"A ward of protection surrounds you! (Absorbs {AbsorptionAmount} damage)");
            from.PlaySound(0x1F7);
            from.FixedParticles(0x375A, 9, 20, 5016, Hue, 0, EffectLayer.Waist);
            Delete();
        }

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write(0);
            writer.Write(_tier);
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            reader.ReadInt();
            _tier = reader.ReadInt();
        }
    }

    // ----------------------------------------------------------
    // SCROLL OF DEATHTOUCH — 1% chance to instantly kill on hit
    // ----------------------------------------------------------

    public class ScrollOfDeathtouch : Item
    {
        // Active Deathtouch end times
        public static readonly Dictionary<Mobile, DateTime> ActiveBuffs = new Dictionary<Mobile, DateTime>();
        public static readonly TimeSpan Duration = TimeSpan.FromMinutes(30);

        public static bool IsActive(Mobile m) =>
            ActiveBuffs.TryGetValue(m, out DateTime end) && DateTime.UtcNow < end;

        [Constructable]
        public ScrollOfDeathtouch() : base(0x1F4E)
        {
            Hue    = 1109;
            Weight = 1.0;
            Name   = "a scroll of deathtouch";
        }

        public ScrollOfDeathtouch(Serial serial) : base(serial) { }

        public override void GetProperties(ObjectPropertyList list)
        {
            base.GetProperties(list);
            list.Add("1% chance on hit to instantly kill target");
            list.Add($"Duration: {(int)Duration.TotalMinutes} minutes");
            list.Add("Does not work on champion spawn bosses");
        }

        public override void OnDoubleClick(Mobile from)
        {
            if (!IsChildOf(from.Backpack))
            {
                from.SendLocalizedMessage(1042001);
                return;
            }

            if (IsActive(from))
            {
                from.SendMessage(0x22, "Deathtouch is already active.");
                return;
            }

            ActiveBuffs[from] = DateTime.UtcNow + Duration;
            from.SendMessage(0x35, "Your touch becomes lethal. Deathtouch active for 30 minutes.");
            from.PlaySound(0x482);
            from.FixedParticles(0x36BD, 20, 10, 5044, Hue, 0, EffectLayer.Head);

            Timer.DelayCall(Duration, () =>
            {
                if (ActiveBuffs.TryGetValue(from, out DateTime end) && DateTime.UtcNow >= end)
                {
                    ActiveBuffs.Remove(from);
                    if (from.NetState != null)
                        from.SendMessage(0x22, "Your deathtouch has faded.");
                }
            });

            Delete();
        }

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write(0);
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            reader.ReadInt();
        }
    }

    // ----------------------------------------------------------
    // SCROLL OF EXECUTION — bonus damage at low target HP
    // ----------------------------------------------------------

    public class ScrollOfExecution : Item
    {
        private int _tier;

        [CommandProperty(AccessLevel.GameMaster)]
        public int Tier
        {
            get => _tier;
            set { _tier = Math.Max(1, Math.Min(3, value)); InvalidateProperties(); }
        }

        // HP threshold (%) below which bonus activates
        public double HPThreshold => _tier == 1 ? 0.05 : _tier == 2 ? 0.10 : 0.15;
        // Damage bonus multiplier
        public double DamageBonus => _tier == 1 ? 0.25 : _tier == 2 ? 0.40 : 0.60;

        public static readonly Dictionary<Mobile, DateTime> ActiveBuffs = new Dictionary<Mobile, DateTime>();
        public static readonly Dictionary<Mobile, int> ActiveTiers = new Dictionary<Mobile, int>();
        public static readonly TimeSpan Duration = TimeSpan.FromMinutes(30);

        public static bool IsActive(Mobile m) =>
            ActiveBuffs.TryGetValue(m, out DateTime end) && DateTime.UtcNow < end;

        public static int GetTier(Mobile m) =>
            ActiveTiers.TryGetValue(m, out int t) ? t : 0;

        [Constructable]
        public ScrollOfExecution() : this(1) { }

        [Constructable]
        public ScrollOfExecution(int tier) : base(0x1F4E)
        {
            _tier  = Math.Max(1, Math.Min(3, tier));
            Hue    = 1285;
            Weight = 1.0;
            Name   = $"a scroll of execution ({TierLabel()})";
        }

        public ScrollOfExecution(Serial serial) : base(serial) { }

        private string TierLabel() => _tier == 1 ? "rare" : _tier == 2 ? "very rare" : "epic";

        public override void GetProperties(ObjectPropertyList list)
        {
            base.GetProperties(list);
            list.Add($"+{(int)(DamageBonus * 100)}% damage when target is below {(int)(HPThreshold * 100)}% HP");
            list.Add($"Duration: {(int)Duration.TotalMinutes} minutes");
        }

        public override void OnDoubleClick(Mobile from)
        {
            if (!IsChildOf(from.Backpack))
            {
                from.SendLocalizedMessage(1042001);
                return;
            }

            if (IsActive(from))
            {
                from.SendMessage(0x22, "Execution is already active.");
                return;
            }

            ActiveBuffs[from] = DateTime.UtcNow + Duration;
            ActiveTiers[from] = _tier;
            from.SendMessage(0x35, $"You will execute weakened foes with fury! (+{(int)(DamageBonus * 100)}% damage below {(int)(HPThreshold * 100)}% HP)");
            from.PlaySound(0x213);
            from.FixedParticles(0x375A, 9, 20, 5016, Hue, 0, EffectLayer.Waist);

            Timer.DelayCall(Duration, () =>
            {
                if (ActiveBuffs.TryGetValue(from, out DateTime end) && DateTime.UtcNow >= end)
                {
                    ActiveBuffs.Remove(from);
                    ActiveTiers.Remove(from);
                    if (from.NetState != null)
                        from.SendMessage(0x22, "Your execution buff has faded.");
                }
            });

            Delete();
        }

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write(0);
            writer.Write(_tier);
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            reader.ReadInt();
            _tier = reader.ReadInt();
        }
    }

    // ----------------------------------------------------------
    // SCROLL OF LEECHING — drains a random stat on hit
    // ----------------------------------------------------------

    public class ScrollOfLeeching : Item
    {
        private int _tier;

        [CommandProperty(AccessLevel.GameMaster)]
        public int Tier
        {
            get => _tier;
            set { _tier = Math.Max(1, Math.Min(3, value)); InvalidateProperties(); }
        }

        public int DrainMin => _tier == 1 ? 1 : _tier == 2 ? 2 : 3;
        public int DrainMax => _tier == 1 ? 3 : _tier == 2 ? 5 : 8;

        public static readonly Dictionary<Mobile, DateTime> ActiveBuffs    = new Dictionary<Mobile, DateTime>();
        public static readonly Dictionary<Mobile, int>      ActiveTiers    = new Dictionary<Mobile, int>();

        // Tracks drained stats so they can be returned on expiry: target -> (stat, amount)
        public static readonly Dictionary<Mobile, (StatType stat, int amount)> DrainedStats
            = new Dictionary<Mobile, (StatType stat, int amount)>();

        public static TimeSpan GetDuration(int tier) =>
            TimeSpan.FromSeconds(tier == 1 ? 30 : tier == 2 ? 45 : 60);

        public static bool IsActive(Mobile m) =>
            ActiveBuffs.TryGetValue(m, out DateTime end) && DateTime.UtcNow < end;

        public static int GetTier(Mobile m) =>
            ActiveTiers.TryGetValue(m, out int t) ? t : 0;

        [Constructable]
        public ScrollOfLeeching() : this(1) { }

        [Constructable]
        public ScrollOfLeeching(int tier) : base(0x1F4E)
        {
            _tier  = Math.Max(1, Math.Min(3, tier));
            Hue    = 1175;
            Weight = 1.0;
            Name   = $"a scroll of leeching ({TierLabel()})";
        }

        public ScrollOfLeeching(Serial serial) : base(serial) { }

        private string TierLabel() => _tier == 1 ? "rare" : _tier == 2 ? "very rare" : "epic";

        public override void GetProperties(ObjectPropertyList list)
        {
            base.GetProperties(list);
            list.Add($"Drains {DrainMin}-{DrainMax} of a random stat per hit");
            list.Add($"Duration: {(int)GetDuration(_tier).TotalSeconds} seconds");
            list.Add("Stat is chosen randomly per hit");
        }

        public override void OnDoubleClick(Mobile from)
        {
            if (!IsChildOf(from.Backpack))
            {
                from.SendLocalizedMessage(1042001);
                return;
            }

            if (IsActive(from))
            {
                from.SendMessage(0x22, "Leeching is already active.");
                return;
            }

            TimeSpan dur = GetDuration(_tier);
            ActiveBuffs[from] = DateTime.UtcNow + dur;
            ActiveTiers[from] = _tier;

            from.SendMessage(0x35, $"Your strikes will drain your enemy's strength! ({DrainMin}-{DrainMax} random stat per hit)");
            from.PlaySound(0x482);
            from.FixedParticles(0x36BD, 20, 10, 5044, Hue, 0, EffectLayer.Head);

            Timer.DelayCall(dur, () =>
            {
                if (ActiveBuffs.TryGetValue(from, out DateTime end) && DateTime.UtcNow >= end)
                {
                    ActiveBuffs.Remove(from);
                    ActiveTiers.Remove(from);
                    if (from.NetState != null)
                        from.SendMessage(0x22, "Your leeching buff has faded.");
                }
            });

            Delete();
        }

        /// <summary>
        /// Called from combat to apply the leech on a successful hit.
        /// Drains a random stat from 'target' and gives it to 'attacker'.
        /// </summary>
        public static void ProcessLeech(Mobile attacker, Mobile target)
        {
            if (!IsActive(attacker) || target == null || target.Deleted)
                return;

            int tier = GetTier(attacker);
            int drainMin = tier == 1 ? 1 : tier == 2 ? 2 : 3;
            int drainMax = tier == 1 ? 3 : tier == 2 ? 5 : 8;
            int amount = Utility.RandomMinMax(drainMin, drainMax);

            // Pick a random stat to drain
            StatType[] stats = { StatType.Str, StatType.Dex, StatType.Int };
            StatType stat = stats[Utility.Random(3)];

            string statName = stat == StatType.Str ? "Strength" : stat == StatType.Dex ? "Dexterity" : "Intelligence";

            int targetCurrent;
            switch (stat)
            {
                case StatType.Str: targetCurrent = target.RawStr; break;
                case StatType.Dex: targetCurrent = target.RawDex; break;
                default:           targetCurrent = target.RawInt; break;
            }

            amount = Math.Min(amount, targetCurrent - 1); // never drain to 0
            if (amount <= 0) return;

            // Drain from target
            switch (stat)
            {
                case StatType.Str: target.RawStr -= amount; break;
                case StatType.Dex: target.RawDex -= amount; break;
                default:           target.RawInt -= amount; break;
            }

            // Give to attacker (up to stat cap)
            switch (stat)
            {
                case StatType.Str: attacker.RawStr = Math.Min(attacker.RawStr + amount, OrbCeilings.MaxStatValue); break;
                case StatType.Dex: attacker.RawDex = Math.Min(attacker.RawDex + amount, OrbCeilings.MaxStatValue); break;
                default:           attacker.RawInt = Math.Min(attacker.RawInt + amount, OrbCeilings.MaxStatValue); break;
            }

            target.SendMessage(0x22, $"Your {statName} is being drained!");
        }

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write(0);
            writer.Write(_tier);
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            reader.ReadInt();
            _tier = reader.ReadInt();
        }
    }

    // ============================================================
    // COMBAT INTEGRATION — hook into melee hits
    // Called from BaseWeapon.OnHit or a patched method.
    // ============================================================

    public static class ScrollCombatHooks
    {
        /// <summary>
        /// Call this from BaseWeapon after a successful melee hit.
        /// Pass in the attacker, the defender, and the pre-calculated damage.
        /// Returns the final adjusted damage after scroll effects.
        /// </summary>
        public static int ProcessScrollEffects(Mobile attacker, Mobile defender, int damage)
        {
            // Warding — reduce incoming damage for defender
            damage = ScrollOfWarding.AbsorbDamage(defender, damage);

            // Deathtouch — 1% instant kill
            if (ScrollOfDeathtouch.IsActive(attacker) && !(defender is BaseChampion))
            {
                if (Utility.RandomDouble() < 0.01)
                {
                    defender.Kill();
                    attacker.SendMessage(0x35, "Deathtouch!");
                    attacker.PlaySound(0x482);
                    return 0;
                }
            }

            // Execution — bonus damage when target HP is low
            if (ScrollOfExecution.IsActive(attacker) && defender.HitsMax > 0)
            {
                int execTier = ScrollOfExecution.GetTier(attacker);
                double threshold = execTier == 1 ? 0.05 : execTier == 2 ? 0.10 : 0.15;
                double bonus     = execTier == 1 ? 0.25 : execTier == 2 ? 0.40 : 0.60;

                double hpPct = (double)defender.Hits / defender.HitsMax;
                if (hpPct <= threshold)
                    damage = (int)(damage * (1.0 + bonus));
            }

            // Leeching — drain random stat
            ScrollOfLeeching.ProcessLeech(attacker, defender);

            return damage;
        }
    }

    // ============================================================
    // GUMPS — skill and stat selection dialogs
    // ============================================================

    // Simple skill selection gump for Orb of Enhancement
    public class SkillSelectGump : Gump
    {
        private readonly Mobile _from;
        private readonly OrbOfEnhancement _orb;

        private static readonly SkillName[] Skills = {
            SkillName.Swords, SkillName.Macing, SkillName.Fencing,
            SkillName.Archery, SkillName.Tactics, SkillName.Anatomy,
            SkillName.Healing, SkillName.Magery, SkillName.MagicResist,
            SkillName.Meditation, SkillName.EvalInt, SkillName.Parry,
            SkillName.Hiding, SkillName.Stealth, SkillName.Poisoning,
            SkillName.Necromancy, SkillName.SpiritSpeak, SkillName.Bushido,
            SkillName.Ninjitsu, SkillName.Chivalry
        };

        public SkillSelectGump(Mobile from, OrbOfEnhancement orb) : base(100, 100)
        {
            _from = from;
            _orb  = orb;

            AddPage(0);
            AddBackground(0, 0, 300, 450, 9270);
            AddLabel(20, 15, 1153, $"Orb of Enhancement (+{orb.SkillBonus:0.0})");
            AddLabel(20, 35, 0xFFFF, "Select a skill to enhance:");

            for (int i = 0; i < Skills.Length; i++)
            {
                int x = (i % 2) * 140 + 20;
                int y = 70 + (i / 2) * 25;
                AddButton(x, y, 4005, 4007, i + 1, GumpButtonType.Reply, 0);
                AddLabel(x + 35, y, 0xFFFF, Skills[i].ToString());
            }
        }

        public override void OnResponse(NetState sender, RelayInfo info)
        {
            if (info.ButtonID == 0 || _orb.Deleted) return;

            int idx = info.ButtonID - 1;
            if (idx >= 0 && idx < Skills.Length)
                _orb.ApplyToSkill(_from, Skills[idx]);
        }
    }

    // Stat selection gump for Orb of Mastery
    public class StatSelectGump : Gump
    {
        private readonly Mobile _from;
        private readonly OrbOfMastery _orb;

        public StatSelectGump(Mobile from, OrbOfMastery orb) : base(200, 200)
        {
            _from = from;
            _orb  = orb;

            AddPage(0);
            AddBackground(0, 0, 240, 140, 9270);
            AddLabel(20, 15, 1175, $"Orb of Mastery (+{orb.StatBonus})");
            AddLabel(20, 35, 0xFFFF, "Select a stat to raise:");

            AddButton(20,  70, 4005, 4007, 1, GumpButtonType.Reply, 0);
            AddLabel(55,   70, 0xFFFF, "Strength");

            AddButton(20,  95, 4005, 4007, 2, GumpButtonType.Reply, 0);
            AddLabel(55,   95, 0xFFFF, "Dexterity");

            AddButton(20, 115, 4005, 4007, 3, GumpButtonType.Reply, 0);
            AddLabel(55,  115, 0xFFFF, "Intelligence");
        }

        public override void OnResponse(NetState sender, RelayInfo info)
        {
            if (info.ButtonID == 0 || _orb.Deleted) return;

            switch (info.ButtonID)
            {
                case 1: _orb.ApplyToStat(_from, StatType.Str); break;
                case 2: _orb.ApplyToStat(_from, StatType.Dex); break;
                case 3: _orb.ApplyToStat(_from, StatType.Int); break;
            }
        }
    }

    // Balance orb gump — shows current skill/stat values for redistribution
    public class BalanceOrbGump : Gump
    {
        private readonly Mobile _from;
        private readonly OrbOfBalance _orb;

        public BalanceOrbGump(Mobile from, OrbOfBalance orb) : base(100, 100)
        {
            _from = from;
            _orb  = orb;

            AddPage(0);
            AddBackground(0, 0, 320, 120, 9270);

            string desc = orb.Tier == 1 ? "Transfer up to 10 skill pts" :
                          orb.Tier == 2 ? "Transfer up to 50 skill pts" :
                                          "Transfer up to 20 stat pts";

            AddLabel(20, 15, 1165, "Orb of Balance");
            AddLabel(20, 35, 0xFFFF, desc);
            AddLabel(20, 60, 0xFFFF, "Use [props to edit skills/stats directly after applying.");
            AddLabel(20, 80, 0x22,   "This orb consumes on use — contact GM to transfer.");

            // NOTE: Full skill/stat redistribution UI requires a more complex
            // multi-page gump. For now the orb grants a GM-visible note and
            // removes itself. A GM can then use [props to adjust accordingly,
            // or this gump can be extended with a full skill picker in a later
            // iteration.
            AddButton(140, 95, 4005, 4007, 1, GumpButtonType.Reply, 0);
            AddLabel(175, 95, 0xFFFF, "Consume Orb");
        }

        public override void OnResponse(NetState sender, RelayInfo info)
        {
            if (info.ButtonID == 1 && !_orb.Deleted)
            {
                _from.SendMessage(0x35, "The orb is consumed. A redistribution token has been noted — contact a GM or use [props to apply your changes.");
                _from.PlaySound(0x1F7);
                _orb.Delete();
            }
        }
    }

    // Cleansing property selection gump (Tier 2 Orb of Cleansing)
    public class CleansingSelectGump : Gump
    {
        private readonly Mobile          _from;
        private readonly AosSkillBonuses _bonuses;
        private readonly OrbOfCleansing  _orb;

        public CleansingSelectGump(Mobile from, AosSkillBonuses bonuses, Item item, OrbOfCleansing orb) : base(100, 100)
        {
            _from    = from;
            _bonuses = bonuses;
            _orb     = orb;

            AddPage(0);
            AddBackground(0, 0, 300, 200, 9270);
            AddLabel(20, 15, 1150, "Orb of Cleansing — Choose Property");

            int y = 50;
            for (int i = 0; i < 5; i++)
            {
                SkillName sk;
                double bv;
                bonuses.GetValues(i, out sk, out bv);

                if (bv != 0)
                {
                    AddButton(20, y, 4005, 4007, i + 1, GumpButtonType.Reply, 0);
                    AddLabel(55, y, 0xFFFF, $"+{bv} {sk}");
                    y += 25;
                }
            }

            if (y == 50)
                AddLabel(20, 50, 0x22, "No removable properties found.");
        }

        public override void OnResponse(NetState sender, RelayInfo info)
        {
            if (info.ButtonID == 0 || _orb.Deleted) return;

            int slot = info.ButtonID - 1;
            if (slot >= 0 && slot < 5)
            {
                _bonuses.SetValues(slot, SkillName.Alchemy, 0);
                _from.SendMessage(0x35, "The chosen property has been removed.");
                _from.PlaySound(0x1F5);
                _orb.Delete();
            }
        }
    }
}
