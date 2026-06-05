using System;
using Server.Custom;
using Server.Items;
using Server.Mobiles;

namespace Server.Items
{
    public abstract class BaseDungeonChest : LockableContainer
    {
        public static readonly string m_DeleteTimerID = "DungeonChest";

        private static readonly Type[] _orbTypes = new Type[]
        {
            typeof(OrbOfEnhancement), typeof(OrbOfMastery),   typeof(OrbOfExpansion),
            typeof(OrbOfFortitude),   typeof(OrbOfAlacrity),  typeof(OrbOfInsight),
            typeof(OrbOfBalance),     typeof(OrbOfCorruption), typeof(OrbOfResonance),
            typeof(OrbOfCleansing),   typeof(OrbOfTempering), typeof(OrbOfEnchantment),
            typeof(OrbOfReforging)
        };

        /// <summary>Returns a random currency orb instance.</summary>
        public static Item RandomOrb()
        {
            return (Item)Activator.CreateInstance(Utility.RandomList(_orbTypes));
        }

        // True once the opener's luck has been applied to equipment inside.
        // Items are added plain at spawn; properties are rolled on first open
        // so that the opener's Luck stat actually influences the roll.
        private bool _lootRolled;

        public override int DefaultGumpID => 0x42;
        public override int DefaultDropSound => 0x42;
        public override Rectangle2D Bounds => new Rectangle2D(20, 105, 150, 180);
        public override bool IsDecoContainer => false;

        public BaseDungeonChest(int itemID) : base(itemID)
        {
            Locked = true;
            Movable = false;

            Key key = (Key)FindItemByType(typeof(Key));

            if (key != null)
                key.Delete();

            RefinementComponent.Roll(this, 1, 0.08);
        }

        public BaseDungeonChest(Serial serial) : base(serial)
        {
        }

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);

            writer.Write(2); // version

            writer.Write(_lootRolled);
            writer.Write(TimerRegistry.HasTimer(m_DeleteTimerID, this));
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);

            int version = reader.ReadInt();

            if (version >= 2)
            {
                _lootRolled = reader.ReadBool();
            }
            else
            {
                // Old chests loaded from save already had luck=0 items — treat as rolled
                _lootRolled = true;
            }

            if ((version == 1 || version >= 2) && reader.ReadBool())
            {
                StartDeleteTimer();
            }
        }

        private void ApplyLuckToLoot(Mobile from)
        {
            if (_lootRolled || !RandomItemGenerator.Enabled)
            {
                _lootRolled = true;
                return;
            }

            int luck = from is PlayerMobile pm ? pm.RealLuck : from.Luck;

            foreach (Item item in Items)
            {
                if (item is BaseWeapon || item is BaseArmor || item is BaseJewel || item is BaseHat)
                {
                    int min, max;
                    TreasureMapChest.GetRandomItemStat(out min, out max);
                    RunicReforging.GenerateRandomItem(item, luck, min, max);
                }
            }

            _lootRolled = true;
        }

        public override void OnTelekinesis(Mobile from)
        {
            if (CheckLocked(from))
            {
                Effects.SendLocationParticles(EffectItem.Create(Location, Map, EffectItem.DefaultDuration), 0x376A, 9, 32, 5022);
                Effects.PlaySound(Location, Map, 0x1F5);
                return;
            }

            ApplyLuckToLoot(from);
            base.OnTelekinesis(from);
            Name = "a treasure chest";
            StartDeleteTimer();
        }

        public override void OnDoubleClick(Mobile from)
        {
            if (CheckLocked(from))
                return;

            ApplyLuckToLoot(from);
            base.OnDoubleClick(from);
            Name = "a treasure chest";
            StartDeleteTimer();
        }

        // Items are added plain at spawn — properties are deferred to first open
        // so the opener's Luck influences the roll rather than using luck=0.
        protected void AddLoot(Item item)
        {
            if (item == null)
                return;

            DropItem(item);
        }

        private void StartDeleteTimer()
        {
            TimerRegistry.Register(m_DeleteTimerID, this, TimeSpan.FromMinutes(Utility.RandomMinMax(2, 5)), chest => chest.Delete());
        }
    }
}
