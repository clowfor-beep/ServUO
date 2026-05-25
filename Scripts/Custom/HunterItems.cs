// ============================================================
// HunterItems.cs
// Scripts/Custom/HunterItems.cs
//
// All Hunter System item classes:
//   HunterHead       — severed head, turn-in for reward
//   HunterMedallion  — prestige neck item, named for kill
//   HunterToken      — blessed currency for Guildmaster shop
//   HunterNamedWeapon helpers (GenerateNamedWeapon)
//   Tier 4 rare artifacts (6 boss-specific items)
//
// Design doc: Design/HunterSystemDesignDoc.txt
// ============================================================

using System;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Server.Custom
{
    // ============================================================
    // HUNTER HEAD
    // ============================================================

    public class HunterHead : Item
    {
        private string   _creatureName;
        private int      _hunterTier;     // 1-4 for creatures, 10-12 for Wanted
        private string   _slayerName;
        private DateTime _killedAt;

        [CommandProperty(AccessLevel.GameMaster)]
        public string CreatureName
        {
            get => _creatureName;
            set { _creatureName = value; InvalidateProperties(); }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public int HunterTier
        {
            get => _hunterTier;
            set { _hunterTier = value; InvalidateProperties(); }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public string SlayerName
        {
            get => _slayerName;
            set { _slayerName = value; InvalidateProperties(); }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public DateTime KilledAt
        {
            get => _killedAt;
            set => _killedAt = value;
        }

        [Constructable]
        public HunterHead() : this("Unknown Target", 1, "Unknown", DateTime.UtcNow) { }

        public HunterHead(string creatureName, int tier, string slayerName, DateTime killedAt)
            : base(0x1DA0)
        {
            _creatureName = creatureName;
            _hunterTier   = tier;
            _slayerName   = slayerName;
            _killedAt     = killedAt;

            Name   = $"the severed head of {creatureName}";
            Weight = 1.0;
            Hue    = tier <= 4 ? TierHue(tier) : WantedHue(tier);

            // 48-hour decay
            Timer.DelayCall(TimeSpan.FromHours(48), () =>
            {
                if (!Deleted)
                {
                    if (RootParent is Mobile m)
                        m.SendMessage(0x22, $"The head of {_creatureName} has rotted away.");
                    Delete();
                }
            });
        }

        private static int TierHue(int tier)
        {
            switch (tier)
            {
                case 1: return 0x21;
                case 2: return 0x47B;
                case 3: return 0x4A0;
                default: return 0x497;
            }
        }

        private static int WantedHue(int tier)
        {
            // 10=Cutthroat, 11=Murderer, 12=DreadLord
            switch (tier)
            {
                case 10: return 0x21;
                case 11: return 0x47B;
                default: return 0x497;
            }
        }

        public HunterHead(Serial serial) : base(serial) { }

        public string TierLabel()
        {
            switch (_hunterTier)
            {
                case 1:  return "Tier I";
                case 2:  return "Tier II";
                case 3:  return "Tier III";
                case 4:  return "Tier IV";
                case 10: return "Wanted: Cutthroat";
                case 11: return "Wanted: Murderer";
                case 12: return "Wanted: Dread Lord";
                default: return "Unknown";
            }
        }

        public override void GetProperties(ObjectPropertyList list)
        {
            base.GetProperties(list);
            list.Add($"Slain by: {_slayerName}");
            list.Add($"Hunter {TierLabel()}");
            list.Add($"Killed: {_killedAt.ToLocalTime():yyyy-MM-dd HH:mm}");
            list.Add("Turn this in at a Hunter's Guildmaster for reward.");
        }

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write(0);
            writer.Write(_creatureName);
            writer.Write(_hunterTier);
            writer.Write(_slayerName);
            writer.Write(_killedAt);
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            reader.ReadInt();
            _creatureName = reader.ReadString();
            _hunterTier   = reader.ReadInt();
            _slayerName   = reader.ReadString();
            _killedAt     = reader.ReadDateTime();
        }
    }

    // ============================================================
    // HUNTER MEDALLION
    // ============================================================

    public class HunterMedallion : Item
    {
        private string   _creatureName;
        private string   _slayerName;
        private DateTime _killedAt;

        [Constructable]
        public HunterMedallion() : this("Unknown", "Unknown", DateTime.UtcNow) { }

        public HunterMedallion(string creatureName, string slayerName, DateTime killedAt)
            : base(0x1088)  // amulet graphic
        {
            _creatureName = creatureName;
            _slayerName   = slayerName;
            _killedAt     = killedAt;

            Name      = $"Medallion of {creatureName}'s Fall";
            Hue       = 0x4AA;
            Weight    = 0.1;
            Layer     = Layer.Neck;
            LootType  = LootType.Regular;
        }

        public HunterMedallion(Serial serial) : base(serial) { }

        public override void GetProperties(ObjectPropertyList list)
        {
            base.GetProperties(list);
            list.Add($"Slain by: {_slayerName}");
            list.Add($"Date: {_killedAt.ToLocalTime():yyyy-MM-dd}");
            list.Add("A trophy of the hunt. Pure prestige.");
        }

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write(0);
            writer.Write(_creatureName);
            writer.Write(_slayerName);
            writer.Write(_killedAt);
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            reader.ReadInt();
            _creatureName = reader.ReadString();
            _slayerName   = reader.ReadString();
            _killedAt     = reader.ReadDateTime();
        }
    }

    // ============================================================
    // HUNTER TOKEN
    // ============================================================

    public class HunterToken : Item
    {
        [Constructable]
        public HunterToken() : this(1) { }

        [Constructable]
        public HunterToken(int amount) : base(0x0EED)
        {
            Name      = "a hunter's token";
            Hue       = 0x4AA;
            Stackable = true;
            Amount    = amount;
            Weight    = 0.0;
            LootType  = LootType.Blessed;
        }

        public HunterToken(Serial serial) : base(serial) { }

        public override void GetProperties(ObjectPropertyList list)
        {
            base.GetProperties(list);
            list.Add("Spend at a Hunter's Guildmaster");
            list.Add("Non-tradeable — blessed");
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
    // NAMED WEAPON GENERATOR
    // ============================================================

    public static class HunterWeaponFactory
    {
        public static BaseWeapon GenerateNamedWeapon(int tier, string creatureName)
        {
            BaseWeapon weapon;

            switch (tier)
            {
                case 1:
                    weapon = Tier1Weapon();
                    ApplyMods(weapon, 1);
                    break;
                case 2:
                    weapon = Tier2Weapon();
                    ApplyMods(weapon, 2);
                    break;
                case 3:
                    weapon = Tier3Weapon();
                    ApplyMods(weapon, 3);
                    break;
                default:
                    weapon = Tier4Weapon();
                    ApplyMods(weapon, 4);
                    weapon.LootType = LootType.Blessed;
                    break;
            }

            string shortName = creatureName.Split(' ')[0]; // e.g. "Grimtooth"
            weapon.Name    = $"{shortName}'s {WeaponSuffix(weapon)}";
            weapon.Quality = ItemQuality.Exceptional;
            weapon.Hue     = TierWeaponHue(tier);

            return weapon;
        }

        private static BaseWeapon Tier1Weapon()
        {
            switch (Utility.Random(3))
            {
                case 0:  return new Katana();
                case 1:  return new Kryss();
                default: return new Hatchet();
            }
        }

        private static BaseWeapon Tier2Weapon()
        {
            switch (Utility.Random(3))
            {
                case 0:  return new Halberd();
                case 1:  return new Bardiche();
                default: return new HeavyCrossbow();
            }
        }

        private static BaseWeapon Tier3Weapon()
        {
            switch (Utility.Random(3))
            {
                case 0:  return new VikingSword();
                case 1:  return new WarAxe();
                default: return new Maul();
            }
        }

        private static BaseWeapon Tier4Weapon()
        {
            switch (Utility.Random(3))
            {
                case 0:  return new Katana();
                case 1:  return new BoneHarvester();
                default: return new WarCleaver();
            }
        }

        private static void ApplyMods(BaseWeapon weapon, int tier)
        {
            // Tier 1: +damage only
            weapon.WeaponAttributes.HitLeechHits = tier >= 2 ? 10 + (tier * 5) : 0;
            weapon.Attributes.WeaponDamage       = 10 * tier;

            if (tier >= 3)
                weapon.Attributes.AttackChance = 10;

            if (tier >= 4)
                weapon.Attributes.WeaponSpeed = 10;
        }

        private static int TierWeaponHue(int tier)
        {
            switch (tier)
            {
                case 1: return 0x21;
                case 2: return 0x47B;
                case 3: return 0x4A0;
                default: return 0x497;
            }
        }

        private static string WeaponSuffix(BaseWeapon w)
        {
            if (w is Katana || w is Kryss || w is BoneHarvester || w is WarCleaver)
                return "Fang";
            if (w is Hatchet || w is WarAxe)
                return "Claw";
            if (w is Halberd || w is Bardiche)
                return "Talon";
            if (w is HeavyCrossbow)
                return "Aim";
            return "Edge";
        }
    }

    // ============================================================
    // TIER 4 RARE BOSS ARTIFACTS
    // ============================================================

    public class RikktorScaleShield : BaseShield
    {
        [Constructable]
        public RikktorScaleShield() : base(0x1B76)
        {
            Name         = "Rikktor's Scale Shield";
            Hue          = 0x497;
            BaseArmorRating = 18;
            LootType     = LootType.Blessed;
            Attributes.ReflectPhysical = 10;
        }

        public RikktorScaleShield(Serial serial) : base(serial) { }

        public override void GetProperties(ObjectPropertyList list)
        {
            base.GetProperties(list);
            list.Add("Torn from the hide of Rikktor the Undying");
        }

        public override void Serialize(GenericWriter writer) { base.Serialize(writer); writer.Write(0); }
        public override void Deserialize(GenericReader reader) { base.Deserialize(reader); reader.ReadInt(); }
    }

    public class BarracoonPipe : Item
    {
        [Constructable]
        public BarracoonPipe() : base(0xE9C)
        {
            Name     = "Barracoon's Pipe";
            Hue      = 0x47B;
            Weight   = 1.0;
            LootType = LootType.Blessed;
            Layer    = Layer.Earrings; // cosmetic slot
        }

        public BarracoonPipe(Serial serial) : base(serial) { }

        public override void GetProperties(ObjectPropertyList list)
        {
            base.GetProperties(list);
            list.Add("The pipe of the Eternal Piper — cosmetic trophy");
        }

        public override void Serialize(GenericWriter writer) { base.Serialize(writer); writer.Write(0); }
        public override void Deserialize(GenericReader reader) { base.Deserialize(reader); reader.ReadInt(); }
    }

    public class NeirasDeathShroud : BaseOuterTorso
    {
        [Constructable]
        public NeirasDeathShroud() : base(0x1F03)
        {
            Name         = "Neira's Death Shroud";
            Hue          = 0x497;
            LootType     = LootType.Blessed;
            Attributes.RegenMana    = 2;
            Resistances.Cold        = 8;
        }

        public NeirasDeathShroud(Serial serial) : base(serial) { }

        public override void GetProperties(ObjectPropertyList list)
        {
            base.GetProperties(list);
            list.Add("Woven from the shadow of Neira the Deathless");
        }

        public override void Serialize(GenericWriter writer) { base.Serialize(writer); writer.Write(0); }
        public override void Deserialize(GenericReader reader) { base.Deserialize(reader); reader.ReadInt(); }
    }

    public class MephitisFang : Kryss
    {
        [Constructable]
        public MephitisFang() : base()
        {
            Name     = "Mephitis's Fang";
            Hue      = 0x4A0;
            LootType = LootType.Blessed;
            WeaponAttributes.HitPoisonArea = 30;
            Attributes.WeaponDamage        = 15;
            Slayer = SlayerName.None;
        }

        public MephitisFang(Serial serial) : base(serial) { }

        public override void GetProperties(ObjectPropertyList list)
        {
            base.GetProperties(list);
            list.Add("Always delivers poison on hit");
            list.Add("Carved from Mephitis's venomous stinger");
        }

        public override void Serialize(GenericWriter writer) { base.Serialize(writer); writer.Write(0); }
        public override void Deserialize(GenericReader reader) { base.Deserialize(reader); reader.ReadInt(); }
    }

    public class SemidarBinding : GoldRing
    {
        [Constructable]
        public SemidarBinding() : base()
        {
            Name     = "Semidar's Binding";
            Hue      = 0x4A0;
            LootType = LootType.Blessed;
            Attributes.BonusInt  = 8;
            Attributes.RegenMana = 3;
        }

        public SemidarBinding(Serial serial) : base(serial) { }

        public override void GetProperties(ObjectPropertyList list)
        {
            base.GetProperties(list);
            list.Add("Bound from Semidar's chain of command");
        }

        public override void Serialize(GenericWriter writer) { base.Serialize(writer); writer.Write(0); }
        public override void Deserialize(GenericReader reader) { base.Deserialize(reader); reader.ReadInt(); }
    }

    public class OaksBarkTalisman : Item
    {
        [Constructable]
        public OaksBarkTalisman() : base(0x2F59)
        {
            Name     = "Oaks' Bark Talisman";
            Hue      = 0x4A0;
            LootType = LootType.Blessed;
            Weight   = 1.0;
            Layer    = Layer.Talisman;
        }

        public OaksBarkTalisman(Serial serial) : base(serial) { }

        public override void GetProperties(ObjectPropertyList list)
        {
            base.GetProperties(list);
            list.Add("+5 Strength (talisman slot)");
            list.Add("Grown from the heartwood of Lord Oaks");
        }

        public override void OnAdded(object parent)
        {
            base.OnAdded(parent);
            if (parent is Mobile m)
                m.AddStatMod(new StatMod(StatType.Str, "OaksBarkStr", 5, TimeSpan.Zero));
        }

        public override void OnRemoved(object parent)
        {
            base.OnRemoved(parent);
            if (parent is Mobile m)
                m.RemoveStatMod("OaksBarkStr");
        }

        public override void Serialize(GenericWriter writer) { base.Serialize(writer); writer.Write(0); }
        public override void Deserialize(GenericReader reader) { base.Deserialize(reader); reader.ReadInt(); }
    }

    // ============================================================
    // BAG OF HOLDING
    // ============================================================
    //
    // Blessed container. Holds up to 5 items.
    // All items stored inside have their weight reduced by 50%.
    // Purchased from the Hunter Token Shop for 10 tokens.
    // ============================================================

    public class BagOfHolding : Bag
    {
        [Constructable]
        public BagOfHolding() : base()
        {
            Name     = "a bag of holding";
            Hue      = 0x4B5;   // deep blue-purple
            Weight   = 2.0;
            LootType = LootType.Blessed;
            MaxItems = 5;
        }

        public BagOfHolding(Serial serial) : base(serial) { }

        // Intercept weight updates and propagate only half the delta upward.
        // This means the bag's m_TotalWeight (and everything above it in the
        // container chain, including the player's carry weight) only accumulates
        // 50% of each stored item's weight.
        public override void UpdateTotal(Item sender, TotalType type, int delta)
        {
            if (type == TotalType.Weight && sender != this)
                base.UpdateTotal(sender, type, (int)Math.Round(delta / 2.0));
            else
                base.UpdateTotal(sender, type, delta);
        }

        public override void GetProperties(ObjectPropertyList list)
        {
            base.GetProperties(list);
            list.Add("Holds up to 5 items");
            list.Add("50% weight reduction on stored items");
            list.Add("Blessed — will not drop on death");
        }

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write(0); // version
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            reader.ReadInt();
        }
    }

    // ============================================================
    // HUNTER'S COMPASS
    // ============================================================
    //
    // Double-click to receive a directional message toward the
    // active hunt (creature) target.  If a Wanted NPC is also up,
    // a second line reports its direction.
    //
    // Purchased from the Hunter Token Shop for 8 tokens.
    // Blessed — won't drop on death.
    // ============================================================

    public class HunterCompass : Item
    {
        [Constructable]
        public HunterCompass() : base(0x14F8)   // compass/sextant graphic
        {
            Name     = "Hunter's Compass";
            Hue      = 0x4AA;
            Weight   = 1.0;
            LootType = LootType.Blessed;
        }

        public HunterCompass(Serial serial) : base(serial) { }

        public override void GetProperties(ObjectPropertyList list)
        {
            base.GetProperties(list);
            list.Add("Double-click to sense the active hunt target");
            list.Add("Points toward the current World Hunt creature");
        }

        public override void OnDoubleClick(Mobile from)
        {
            if (!IsChildOf(from.Backpack))
            {
                from.SendLocalizedMessage(1042001); // That must be in your pack.
                return;
            }

            from.PlaySound(0x1F4);

            var hunts  = HunterSystem.GetAllActiveHunts();
            var wanted = HunterSystem.GetAllActiveWanted();

            bool reported = false;

            // ---- Active creature hunts ----
            foreach (var t in hunts)
            {
                if (t.Map != from.Map)
                {
                    from.SendMessage(0x22, $"[Hunt] {t.Name} lurks on another facet ({t.Location}).");
                }
                else
                {
                    string dir = CompassDirection(from.Location, t.Position);
                    int    dist = (int)from.GetDistanceToSqrt(t.Position);
                    from.SendMessage(0x35,
                        $"[Hunt] {dir} — {t.Name} is roughly {dist} paces away ({t.Location}).");
                }
                reported = true;
            }

            // ---- Active wanted NPCs ----
            foreach (var t in wanted)
            {
                if (t.Map != from.Map)
                {
                    from.SendMessage(0x22, $"[Wanted] {t.Name} is on another facet ({t.Location}).");
                }
                else
                {
                    string dir = CompassDirection(from.Location, t.Position);
                    int    dist = (int)from.GetDistanceToSqrt(t.Position);
                    from.SendMessage(0x35,
                        $"[Wanted] {dir} — {t.Name} is roughly {dist} paces away ({t.Location}).");
                }
                reported = true;
            }

            if (!reported)
                from.SendMessage(1153, "The compass needle rests still. There is no active hunt at this time.");
        }

        private static string CompassDirection(Point3D from, Point3D to)
        {
            int dx = to.X - from.X;
            int dy = to.Y - from.Y;   // positive dy = South in UO

            if (dx == 0 && dy == 0)
                return "right at your feet";

            double angle = Math.Atan2(dy, dx) * 180.0 / Math.PI;
            // Atan2 in UO coords: 0=East, 90=South, ±180=West, -90=North
            if (angle < 0) angle += 360.0;

            if (angle < 22.5  || angle >= 337.5) return "East";
            if (angle < 67.5)  return "South-East";
            if (angle < 112.5) return "South";
            if (angle < 157.5) return "South-West";
            if (angle < 202.5) return "West";
            if (angle < 247.5) return "North-West";
            if (angle < 292.5) return "North";
            return "North-East";
        }

        public override void Serialize(GenericWriter writer) { base.Serialize(writer); writer.Write(0); }
        public override void Deserialize(GenericReader reader) { base.Deserialize(reader); reader.ReadInt(); }
    }
}
