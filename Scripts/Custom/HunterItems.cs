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
using System.Collections.Generic;
using Server;
using Server.Gumps;
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

        private const int LuckBonus = 100;

        public override void OnAdded(object parent)
        {
            base.OnAdded(parent);
            if (parent is Mobile m)
                Enhancement.SetValue(m, AosAttribute.Luck, LuckBonus, "HunterMedallion");
        }

        public override void OnRemoved(object parent)
        {
            base.OnRemoved(parent);
            if (parent is Mobile m)
                Enhancement.SetValue(m, AosAttribute.Luck, 0, "HunterMedallion");
        }

        public override void GetProperties(ObjectPropertyList list)
        {
            base.GetProperties(list);
            list.Add($"Slain by: {_slayerName}");
            list.Add($"Date: {_killedAt.ToLocalTime():yyyy-MM-dd}");
            list.Add($"Luck Bonus: +{LuckBonus}");
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
    // ============================================================
    // BAGS OF HOLDING — base class + 4 tiers
    //
    // Tier       Items  Weight Reduction  Shop Cost
    // ─────────────────────────────────────────────
    // Lesser         1              33%   10 tokens
    // Standard       2              50%   25 tokens
    // Greater        4              75%   50 tokens
    // Supreme        5             100%  100 tokens
    //
    // Rule: a player may carry only ONE bag of holding at a time.
    // Placing a second in the pack ejects it to the ground.
    // ============================================================

    public abstract class BaseBagOfHolding : Bag
    {
        protected abstract int    WeightReductionPct { get; }
        protected abstract int    BagMaxItems        { get; }
        protected abstract string BagDisplayName     { get; }

        protected BaseBagOfHolding() : base()
        {
            Weight   = 2.0;
            LootType = LootType.Blessed;
            MaxItems = BagMaxItems;
        }

        protected BaseBagOfHolding(Serial serial) : base(serial) { }

        // ── Item slot limit — direct children only ────────────────────────
        // ServUO's base CheckHold uses recursive TotalItems which counts all
        // nested items toward MaxItems.  A BagOfHolding (MaxItems=2) with one
        // container-with-1-item inside would already show TotalItems=2 and
        // reject any further additions, making it appear to "hold only 1 item".
        // We enforce the limit as a DIRECT child count (Items.Count) so each
        // tier correctly holds N top-level items regardless of their contents.
        public override bool CheckHold(Mobile m, Item item, bool message, bool checkItems, int plusItems, int plusWeight)
        {
            if (checkItems && Items.Count >= BagMaxItems)
            {
                if (message)
                    m.SendMessage("The bag is full.");
                return false;
            }
            return true;
        }

        // ── Weight reduction ──────────────────────────────────────────────
        // TWO-PART fix to cover both code paths that contribute to carried weight:
        //
        // 1. GetTotal() — covers the full-rebuild path.
        //    UpdateTotals() (called on login / every world load) rebuilds
        //    m_TotalWeight from scratch by summing children, then the parent
        //    queries TotalWeight → GetTotal() → we apply reduction here.
        //    Result: correct weight after every server restart / login. ✓
        //
        // 2. UpdateTotal() correction — covers the incremental-delta path.
        //    When an item is added/removed at runtime, base.UpdateTotal()
        //    propagates the FULL unreduced delta up to the parent container
        //    (backpack → player).  We immediately follow with a negative
        //    correction delta so the net change reaching the parent equals
        //    the reduced amount.  This also fixes edge cases like trade and
        //    banking that bypass our AddItem/RemoveItem overrides.
        //    The correction uses sender=this so our own override ignores it
        //    (sender == this is filtered by Container.UpdateTotal), preventing
        //    infinite recursion.
        public override int GetTotal(TotalType type)
        {
            int total = base.GetTotal(type);

            if (type == TotalType.Weight && WeightReductionPct > 0)
                total = (int)Math.Round(total * (100 - WeightReductionPct) / 100.0, MidpointRounding.AwayFromZero);

            return total;
        }

        public override void UpdateTotal(Item sender, TotalType type, int delta)
        {
            base.UpdateTotal(sender, type, delta);

            // Only intercept weight deltas from child items (not self-updates)
            if (type == TotalType.Weight && sender != this && delta != 0 && WeightReductionPct > 0)
            {
                // base.UpdateTotal already propagated the full delta upward.
                // Send a correction = (reduced_delta - full_delta) to cancel
                // the excess.  Net effect on parent: full + correction = reduced.
                int reducedDelta  = (int)Math.Round(delta * (100 - WeightReductionPct) / 100.0, MidpointRounding.AwayFromZero);
                int correctionDelta = reducedDelta - delta; // negative value

                if (correctionDelta != 0)
                {
                    if (Parent is Item pi)
                        pi.UpdateTotal(this, type, correctionDelta);
                    else if (Parent is Mobile pm)
                        pm.UpdateTotal(this, type, correctionDelta);
                }
            }
        }

        public override void AddItem(Item item)
        {
            base.AddItem(item);
        }

        public override void RemoveItem(Item item)
        {
            base.RemoveItem(item);
        }

        // ── One bag per player ────────────────────────────────────────────
        // Defer to Timer.DelayCall so the item is fully parented before we
        // inspect the pack — calling MoveToWorld directly inside OnAdded is
        // unsafe (item is mid-add, parent chain not yet finalized).
        // Use FindItemsByType<BaseBagOfHolding> (generic, uses 'is T' check)
        // so all subclass instances are found correctly.
        public override void OnAdded(object parent)
        {
            base.OnAdded(parent);

            Timer.DelayCall(TimeSpan.Zero, () =>
            {
                if (Deleted) return;

                Mobile owner = RootParent as Mobile;
                if (owner?.Backpack == null) return;

                if (owner.Backpack.FindItemsByType<BaseBagOfHolding>(true).Count > 1)
                {
                    owner.SendMessage(0x22, "You may only carry one bag of holding at a time.");
                    MoveToWorld(owner.Location, owner.Map);
                }
            });
        }

        public override void GetProperties(ObjectPropertyList list)
        {
            base.GetProperties(list);
            list.Add($"Holds up to {BagMaxItems} {(BagMaxItems == 1 ? "item" : "items")}");
            list.Add($"{WeightReductionPct}% weight reduction on stored items");
            list.Add("Blessed — will not drop on death");
            list.Add("Limit: one bag of holding per player");
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

    // ── Lesser Bag of Holding ──────────────────────────────────────────────
    public class LesserBagOfHolding : BaseBagOfHolding
    {
        protected override int    WeightReductionPct => 33;
        protected override int    BagMaxItems        => 1;
        protected override string BagDisplayName     => "a lesser bag of holding";

        [Constructable]
        public LesserBagOfHolding() : base()
        {
            Name = BagDisplayName;
            Hue  = 0x47D;
        }

        public LesserBagOfHolding(Serial serial) : base(serial) { }
    }

    // ── Bag of Holding ────────────────────────────────────────────────────
    public class BagOfHolding : BaseBagOfHolding
    {
        protected override int    WeightReductionPct => 50;
        protected override int    BagMaxItems        => 2;
        protected override string BagDisplayName     => "a bag of holding";

        [Constructable]
        public BagOfHolding() : base()
        {
            Name = BagDisplayName;
            Hue  = 0x4B5;
        }

        public BagOfHolding(Serial serial) : base(serial) { }
    }

    // ── Greater Bag of Holding ────────────────────────────────────────────
    public class GreaterBagOfHolding : BaseBagOfHolding
    {
        protected override int    WeightReductionPct => 75;
        protected override int    BagMaxItems        => 4;
        protected override string BagDisplayName     => "a greater bag of holding";

        [Constructable]
        public GreaterBagOfHolding() : base()
        {
            Name = BagDisplayName;
            Hue  = 0x4AA;
        }

        public GreaterBagOfHolding(Serial serial) : base(serial) { }
    }

    // ── Supreme Bag of Holding ────────────────────────────────────────────
    public class SupremeBagOfHolding : BaseBagOfHolding
    {
        protected override int    WeightReductionPct => 100;
        protected override int    BagMaxItems        => 5;
        protected override string BagDisplayName     => "a supreme bag of holding";

        [Constructable]
        public SupremeBagOfHolding() : base()
        {
            Name = BagDisplayName;
            Hue  = 0x497;
        }

        public SupremeBagOfHolding(Serial serial) : base(serial) { }
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

    // ============================================================
    // HuntersMap — shows all active hunt targets with dungeon,
    // floor/level, and coordinates. Reusable, blessed.
    // Data is cached for 30 minutes per use.
    // Sold in the Hunter Token Shop for 5 tokens.
    // ============================================================
    public class HuntersMap : Item
    {
        private static readonly TimeSpan CooldownDuration = TimeSpan.FromMinutes(30);

        // Cached snapshot entry
        public struct SnapEntry
        {
            public string Type;      // "[Hunt]" or "[Wanted]"
            public string Name;
            public string Location;
            public int    X;
            public int    Y;
        }

        private DateTime         _lastRefresh = DateTime.MinValue;
        private List<SnapEntry>  _snapshot    = new List<SnapEntry>();

        [Constructable]
        public HuntersMap() : base(0x14EC)  // map graphic
        {
            Name     = "Hunter's Map";
            Hue      = 0x4AA;
            Weight   = 1.0;
            LootType = LootType.Blessed;
        }

        public HuntersMap(Serial serial) : base(serial) { }

        public override void GetProperties(ObjectPropertyList list)
        {
            base.GetProperties(list);
            list.Add("Shows all active hunt targets");
            list.Add("Dungeon, floor and coordinates per target");
            list.Add("Updates every 30 minutes");
        }

        public override void OnDoubleClick(Mobile from)
        {
            if (!IsChildOf(from.Backpack))
            {
                from.SendLocalizedMessage(1042001);
                return;
            }

            bool expired = DateTime.UtcNow - _lastRefresh >= CooldownDuration;

            if (expired)
            {
                // Refresh snapshot
                _snapshot.Clear();

                foreach (var t in HunterSystem.GetAllActiveHunts())
                    _snapshot.Add(new SnapEntry { Type = "[Hunt]",   Name = t.Name, Location = t.Location, X = t.Position.X, Y = t.Position.Y });

                foreach (var t in HunterSystem.GetAllActiveWanted())
                    _snapshot.Add(new SnapEntry { Type = "[Wanted]", Name = t.Name, Location = t.Location, X = t.Position.X, Y = t.Position.Y });

                _lastRefresh = DateTime.UtcNow;
                InvalidateProperties();
            }

            from.PlaySound(0x249);
            from.CloseGump(typeof(HuntersMapGump));
            from.SendGump(new HuntersMapGump(_snapshot, _lastRefresh, CooldownDuration));
        }

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write(1); // version

            writer.Write(_lastRefresh);
            writer.Write(_snapshot.Count);
            foreach (var e in _snapshot)
            {
                writer.Write(e.Type);
                writer.Write(e.Name);
                writer.Write(e.Location);
                writer.Write(e.X);
                writer.Write(e.Y);
            }
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            int version = reader.ReadInt();

            if (version >= 1)
            {
                _lastRefresh = reader.ReadDateTime();
                int count = reader.ReadInt();
                _snapshot = new List<SnapEntry>(count);
                for (int i = 0; i < count; i++)
                {
                    _snapshot.Add(new SnapEntry
                    {
                        Type     = reader.ReadString(),
                        Name     = reader.ReadString(),
                        Location = reader.ReadString(),
                        X        = reader.ReadInt(),
                        Y        = reader.ReadInt(),
                    });
                }
            }
        }
    }

    // ============================================================
    // HuntersMapGump
    // ============================================================
    public class HuntersMapGump : Gump
    {
        private const int W       = 560;
        private const int RowH    = 46;
        private const int HeaderH = 80;
        private const int FooterH = 20;

        public HuntersMapGump(
            List<HuntersMap.SnapEntry> snapshot,
            DateTime lastRefresh,
            TimeSpan cooldown)
            : base(80, 80)
        {
            // Cooldown status line
            TimeSpan age        = lastRefresh == DateTime.MinValue ? cooldown : DateTime.UtcNow - lastRefresh;
            bool     stale      = age >= cooldown;
            string   statusText;
            string   statusCol;
            if (lastRefresh == DateTime.MinValue)
            {
                statusText = "Never consulted.";
                statusCol  = "#886655";
            }
            else if (stale)
            {
                statusText = "Data is current — map refreshed on next open.";
                statusCol  = "#44AA66";
            }
            else
            {
                TimeSpan remaining = cooldown - age;
                int mins = (int)remaining.TotalMinutes + 1;
                statusText = $"Snapshot taken {(int)age.TotalMinutes} min ago — refreshes in ~{mins} min.";
                statusCol  = "#AAAAAA";
            }

            int rows  = snapshot.Count > 0 ? snapshot.Count : 1;
            int gumpH = HeaderH + rows * RowH + FooterH + 10;

            AddBackground(0, 0, W, gumpH, 9200);
            AddAlphaRegion(8, 8, W - 16, gumpH - 16);

            // Title
            AddHtml(0, 12, W, 22,
                "<CENTER><BASEFONT COLOR=#C8A428><BIG>Hunter's Map</BIG></BASEFONT></CENTER>",
                false, false);

            // Cooldown status
            AddHtml(18, 34, W - 36, 18,
                $"<BASEFONT COLOR={statusCol}>{statusText}</BASEFONT>",
                false, false);

            // Column headers
            AddHtml(18,  56, 70,  16, "<BASEFONT COLOR=#666655>Type</BASEFONT>",     false, false);
            AddHtml(90,  56, 160, 16, "<BASEFONT COLOR=#666655>Target</BASEFONT>",   false, false);
            AddHtml(255, 56, 200, 16, "<BASEFONT COLOR=#666655>Location</BASEFONT>", false, false);
            AddHtml(460, 56, 80,  16, "<BASEFONT COLOR=#666655>Coords</BASEFONT>",   false, false);
            AddImageTiled(12, 72, W - 24, 1, 9264);

            if (snapshot.Count == 0)
            {
                AddHtml(18, HeaderH + 10, W - 36, 22,
                    "<BASEFONT COLOR=#886655>No active hunts at this time.</BASEFONT>",
                    false, false);
            }
            else
            {
                for (int i = 0; i < snapshot.Count; i++)
                {
                    int  y       = HeaderH + i * RowH;
                    var  e       = snapshot[i];
                    bool isWanted = e.Type == "[Wanted]";
                    string typeCol  = isWanted ? "#CC6644" : "#44AA66";

                    AddHtml(18,  y, 70,  RowH, $"<BASEFONT COLOR={typeCol}>{e.Type}</BASEFONT>",    false, false);
                    AddHtml(90,  y, 160, RowH, $"<BASEFONT COLOR=#DDCCAA>{e.Name}</BASEFONT>",      false, false);
                    AddHtml(255, y, 200, RowH, $"<BASEFONT COLOR=#AABBCC>{e.Location}</BASEFONT>",  false, false);
                    AddHtml(460, y, 80,  RowH, $"<BASEFONT COLOR=#888888>{e.X}, {e.Y}</BASEFONT>",  false, false);

                    if (i < snapshot.Count - 1)
                        AddImageTiled(12, y + RowH - 2, W - 24, 1, 9264);
                }
            }
        }
    }
}
