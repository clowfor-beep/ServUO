using Server.Custom;

namespace Server.Items
{
    public class TreasureLevel1 : BaseDungeonChest
    {
        public override int DefaultGumpID => 0x49;

        // Matches: Average (108-180 gold, 1-2 magic items)
        [Constructable]
        public TreasureLevel1() : base(Utility.RandomList(0xE3C, 0xE3E, 0x9a9)) // Large, Medium and Small Crate
        {
            RequiredSkill = 52;
            LockLevel = RequiredSkill - Utility.Random(1, 10);
            MaxLockLevel = RequiredSkill;
            TrapType = TrapType.MagicTrap;
            TrapPower = 1 * Utility.Random(1, 25);

            DropItem(new Gold(Utility.RandomMinMax(108, 180)));
            DropItem(Loot.RandomClothing());

            // 1-2 magic items
            AddLoot(Loot.RandomArmorOrShield());
            if (Utility.RandomBool())
                AddLoot(Loot.RandomWeapon());

            // 1-3 gems
            for (int i = Utility.RandomMinMax(1, 3); i > 0; i--)
                DropItem(Loot.RandomGem());

            // 1% chance: random currency orb
            if (Utility.RandomDouble() < 0.01)
                DropItem(RandomOrb());
        }

        public TreasureLevel1(Serial serial) : base(serial)
        {
        }

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);

            writer.Write(0); // version 
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);

            int version = reader.ReadInt();
        }
    }

    public class TreasureLevel2 : BaseDungeonChest
    {
        // Matches: Rich (225-375 gold, 2-3 magic items)
        [Constructable]
        public TreasureLevel2() : base(Utility.RandomList(0xe3c, 0xE3E, 0x9a9, 0xe42, 0x9ab, 0xe40, 0xe7f, 0xe77))
        {
            RequiredSkill = 72;
            LockLevel = RequiredSkill - Utility.Random(1, 10);
            MaxLockLevel = RequiredSkill;
            TrapType = TrapType.MagicTrap;
            TrapPower = 2 * Utility.Random(1, 25);

            DropItem(new Gold(Utility.RandomMinMax(225, 375)));
            DropItem(Loot.RandomPotion());

            // 1 reagent stack
            Item reagent = Loot.RandomReagent();
            reagent.Amount = Utility.RandomMinMax(5, 10);
            DropItem(reagent);

            // 50% chance: 4-8 low-mid scrolls
            if (Utility.RandomBool())
                for (int i = Utility.RandomMinMax(4, 8); i > 0; i--)
                    DropItem(Loot.RandomScroll(0, 39, SpellbookType.Regular));

            // 50% chance: 3-6 gems
            if (Utility.RandomBool())
                for (int i = Utility.RandomMinMax(3, 6); i > 0; i--)
                    DropItem(Loot.RandomGem());

            // 2-3 magic items
            AddLoot(Loot.RandomArmorOrShield());
            AddLoot(Loot.RandomWeapon());
            if (Utility.RandomBool())
                AddLoot(Loot.RandomJewelry());

            // 1% chance: random currency orb
            if (Utility.RandomDouble() < 0.01)
                DropItem(RandomOrb());
        }

        public TreasureLevel2(Serial serial) : base(serial)
        {
        }

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);

            writer.Write(0); // version 
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);

            int version = reader.ReadInt();
        }
    }

    public class TreasureLevel3 : BaseDungeonChest
    {
        public override int DefaultGumpID => 0x4A;

        // Matches: FilthyRich (400-600 gold, 3-4 magic items)
        [Constructable]
        public TreasureLevel3() : base(Utility.RandomList(0x9ab, 0xe40, 0xe42)) // Wooden, Metal and Metal Golden Chest
        {
            RequiredSkill = 84;
            LockLevel = RequiredSkill - Utility.Random(1, 10);
            MaxLockLevel = RequiredSkill;
            TrapType = TrapType.MagicTrap;
            TrapPower = 3 * Utility.Random(1, 25);

            DropItem(new Gold(Utility.RandomMinMax(400, 600)));

            // 1-2 reagent stacks
            for (int i = Utility.RandomMinMax(1, 2); i > 0; i--)
            {
                Item reagent = Loot.RandomReagent();
                reagent.Amount = Utility.RandomMinMax(5, 15);
                DropItem(reagent);
            }

            // 1-2 potions
            for (int i = Utility.RandomMinMax(1, 2); i > 0; i--)
                DropItem(Loot.RandomPotion());

            // 67% chance: 6-12 mid scrolls
            if (0.67 > Utility.RandomDouble())
                for (int i = Utility.RandomMinMax(6, 12); i > 0; i--)
                    DropItem(Loot.RandomScroll(0, 47, SpellbookType.Regular));

            // 5-8 gems guaranteed
            for (int i = Utility.RandomMinMax(5, 8); i > 0; i--)
                DropItem(Loot.RandomGem());

            DropItem(Loot.RandomWand());

            // 3-4 magic items
            AddLoot(Loot.RandomArmorOrShieldOrWeapon());
            AddLoot(Loot.RandomArmorOrShieldOrWeapon());
            AddLoot(Loot.RandomArmorOrShieldOrWeapon());
            if (Utility.RandomBool())
                AddLoot(Loot.RandomJewelry());

            // 1% chance: random currency orb
            if (Utility.RandomDouble() < 0.01)
                DropItem(RandomOrb());
        }

        public TreasureLevel3(Serial serial) : base(serial)
        {
        }

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);

            writer.Write(0); // version 
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);

            int version = reader.ReadInt();
        }
    }

    public class TreasureLevel4 : BaseDungeonChest
    {
        // Matches: UltraRich (600-900 gold, 4-6 magic items)
        [Constructable]
        public TreasureLevel4() : base(Utility.RandomList(0xe40, 0xe42, 0x9ab))
        {
            RequiredSkill = 92;
            LockLevel = RequiredSkill - Utility.Random(1, 10);
            MaxLockLevel = RequiredSkill;
            TrapType = TrapType.MagicTrap;
            TrapPower = 4 * Utility.Random(1, 25);

            DropItem(new Gold(Utility.RandomMinMax(600, 900)));
            DropItem(new BlankScroll(Utility.RandomMinMax(2, 5)));

            // 2-3 reagent stacks
            for (int i = Utility.RandomMinMax(2, 3); i > 0; i--)
            {
                Item reagent = Loot.RandomReagent();
                reagent.Amount = Utility.RandomMinMax(8, 15);
                DropItem(reagent);
            }

            // 2-3 potions
            for (int i = Utility.RandomMinMax(2, 3); i > 0; i--)
                DropItem(Loot.RandomPotion());

            // 75% chance: 10-16 high scrolls
            if (0.75 > Utility.RandomDouble())
                for (int i = Utility.RandomMinMax(10, 16); i > 0; i--)
                    DropItem(Loot.RandomScroll(0, 55, SpellbookType.Regular));

            // 8-12 gems guaranteed
            for (int i = Utility.RandomMinMax(8, 12); i > 0; i--)
                DropItem(Loot.RandomGem());

            // 1-2 wands
            for (int i = Utility.RandomMinMax(1, 2); i > 0; i--)
                DropItem(Loot.RandomWand());

            // 4-6 magic items
            for (int i = Utility.RandomMinMax(4, 6); i > 0; i--)
                AddLoot(Loot.RandomArmorOrShieldOrWeapon());

            AddLoot(Loot.RandomJewelry());

            // 10% chance: circle 8 scroll
            if (Utility.RandomDouble() < 0.10)
                DropItem(Loot.RandomScroll(56, 63, SpellbookType.Regular));

            // 1% chance: random currency orb
            if (Utility.RandomDouble() < 0.01)
                DropItem(RandomOrb());

            // 2% chance: 105 or 110 power scroll
            if (Utility.RandomDouble() < 0.02)
                DropItem(PowerScroll.CreateRandom(105, 110));
        }

        public TreasureLevel4(Serial serial) : base(serial)
        {
        }

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);

            writer.Write(0); // version
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);

            int version = reader.ReadInt();
        }
    }

    public class TreasureLevel5 : BaseDungeonChest
    {
        [Constructable]
        public TreasureLevel5() : base(0xe42) // Metal Golden Chest
        {
            RequiredSkill = 100;
            LockLevel = RequiredSkill - Utility.Random(1, 10);
            MaxLockLevel = RequiredSkill;
            TrapType = TrapType.MagicTrap;
            TrapPower = 5 * Utility.Random(1, 25);

            // Matches: SuperBoss (800-1400 gold, 6-10 magic items)
            DropItem(new Gold(Utility.RandomMinMax(800, 1400)));
            DropItem(new BlankScroll(Utility.RandomMinMax(3, 8)));

            // 3-4 reagent stacks
            for (int i = Utility.RandomMinMax(3, 4); i > 0; i--)
            {
                Item reagent = Loot.RandomReagent();
                reagent.Amount = Utility.RandomMinMax(12, 20);
                DropItem(reagent);
            }

            // 3-4 potions
            for (int i = Utility.RandomMinMax(3, 4); i > 0; i--)
                DropItem(Loot.RandomPotion());

            // 90% chance: 14-20 max-circle scrolls
            if (0.90 > Utility.RandomDouble())
                for (int i = Utility.RandomMinMax(14, 20); i > 0; i--)
                    DropItem(Loot.RandomScroll(0, 63, SpellbookType.Regular));

            // 12-18 gems guaranteed
            for (int i = Utility.RandomMinMax(12, 18); i > 0; i--)
                DropItem(Loot.RandomGem());

            // 2-3 wands
            for (int i = Utility.RandomMinMax(2, 3); i > 0; i--)
                DropItem(Loot.RandomWand());

            // 6-10 magic items
            for (int i = Utility.RandomMinMax(6, 10); i > 0; i--)
                AddLoot(Loot.RandomArmorOrShieldOrWeapon());

            AddLoot(Loot.RandomJewelry());
            AddLoot(Loot.RandomJewelry());

            // 10% chance: circle 8 scroll
            if (Utility.RandomDouble() < 0.10)
                DropItem(Loot.RandomScroll(56, 63, SpellbookType.Regular));

            // 1% chance: random currency orb
            if (Utility.RandomDouble() < 0.01)
                DropItem(RandomOrb());

            // 2% chance: 105 or 110 power scroll
            if (Utility.RandomDouble() < 0.02)
                DropItem(PowerScroll.CreateRandom(105, 110));
        }

        public TreasureLevel5(Serial serial) : base(serial)
        {
        }

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);

            writer.Write(0); // version
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);

            int version = reader.ReadInt();
        }
    }

}
