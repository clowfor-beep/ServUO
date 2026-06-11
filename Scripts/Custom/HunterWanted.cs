// ============================================================
// HunterWanted.cs
// Scripts/Custom/HunterWanted.cs
//
// The three Wanted NPC tiers — named infamous murderers that
// spawn alongside the creature Hunt system as bounty targets.
//
//   WantedCutthroat  — enhanced dexxer (≈ Tier 1-2 difficulty)
//   WantedMurderer   — enhanced tank-mage (≈ Tier 2-3)
//   WantedDreadLord  — enhanced NecroMage (≈ Tier 3-4)
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
    // ABSTRACT WANTED BASE
    // ============================================================

    public abstract class BaseWantedNPC : BaseFBCombatNPC
    {
        protected abstract int    WantedTier  { get; }   // 10=Cutthroat, 11=Murderer, 12=DreadLord
        protected abstract string WantedLabel { get; }   // "the Cutthroat" etc.

        protected BaseWantedNPC(AIType ai, FightMode mode, int range)
            : base(ai, mode, range)
        {
        }

        public BaseWantedNPC(Serial serial) : base(serial) { }

        // Absorbs the legacy BasePKNPC version int that was written before this
        // class extended BaseFBCombatNPC directly.
        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write(1); // version (was BasePKNPC version=0, now BaseWantedNPC version=1)
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            int version = reader.ReadInt();
            // version 0 = legacy BasePKNPC int — nothing extra to read
            // version 1 = BaseWantedNPC owns this layer
        }

        protected void InitWantedName(string characterName)
        {
            Name  = $"[Wanted] {characterName} {WantedLabel}";
            Title = string.Empty;
        }

        public override void OnDeath(Container corpse)
        {
            base.OnDeath(corpse);

            Mobile killer = LastKiller;
            if (killer is BaseCreature bc && bc.Controlled && bc.ControlMaster is PlayerMobile)
                killer = bc.ControlMaster;

            string killerName = killer?.Name ?? "an unknown hunter";

            // World broadcast
            HunterSystem.BroadcastWantedKill(Name.Replace("[Wanted] ", ""), killerName);

            // Award Hunter Points + Justice virtue
            if (killer is PlayerMobile pm)
            {
                int pts = WantedTier == 10 ? 1 : WantedTier == 11 ? 3 : 6;
                HunterSystem.AddPoints(pm, pts);
                HunterSystem.CheckRankUp(pm);

                // Justice virtue — negative-karma target gives Justice
                // (handled automatically by ServUO virtue system via Karma < 0)

                // Head into killer's pack
                var head = new HunterHead(
                    Name.Replace("[Wanted] ", ""),
                    WantedTier,
                    killerName,
                    DateTime.UtcNow);

                if (pm.Backpack != null && pm.Backpack.TryDropItem(pm, head, false))
                    pm.SendMessage(0x35, "The head of the wanted criminal falls into your pack.");
                else
                    corpse.DropItem(head);
            }

            // Medallion on corpse
            corpse.DropItem(new HunterMedallion(
                Name.Replace("[Wanted] ", ""),
                killerName,
                DateTime.UtcNow));

            // Tokens: 1 / 2 / 3 for Cutthroat / Murderer / Dread Lord
            int tokens = WantedTier == 10 ? 1 : WantedTier == 11 ? 2 : WantedTier == 12 ? 3 : 0;
            if (tokens > 0)
                corpse.DropItem(new HunterToken(tokens));

            // Orb drop — tiered pool per Wanted rank
            // Cutthroat (T10): 10% EssenceShard
            // Murderer   (T11): 25% full Cat1 T1 pool
            // Dread Lord (T12): 45% Cat1 T1-2 pool + bonus 10% Cat2 item orb
            if (WantedTier == 10 && Utility.RandomDouble() < 0.10)
            {
                corpse.DropItem(new EssenceShard(Utility.RandomMinMax(8, 15)));
            }
            else if (WantedTier == 11 && Utility.RandomDouble() < 0.25)
            {
                switch (Utility.Random(5))
                {
                    case 0: corpse.DropItem(new OrbOfEnhancement(1)); break;
                    case 1: corpse.DropItem(new OrbOfMastery(1));     break;
                    case 2: corpse.DropItem(new OrbOfExpansion(1));   break;
                    case 3: corpse.DropItem(new OrbOfFortitude(1));   break;
                    default: corpse.DropItem(new EssenceShard(Utility.RandomMinMax(12, 22))); break;
                }
            }
            else if (WantedTier == 12)
            {
                if (Utility.RandomDouble() < 0.45)
                {
                    switch (Utility.Random(7))
                    {
                        case 0: corpse.DropItem(new OrbOfEnhancement(Utility.RandomMinMax(1, 2))); break;
                        case 1: corpse.DropItem(new OrbOfMastery(1));                              break;
                        case 2: corpse.DropItem(new OrbOfExpansion(Utility.RandomMinMax(1, 2)));   break;
                        case 3: corpse.DropItem(new OrbOfFortitude(1));                            break;
                        case 4: corpse.DropItem(new OrbOfAlacrity(1));                             break;
                        case 5: corpse.DropItem(new OrbOfBalance(1));                              break;
                        default: corpse.DropItem(new EssenceShard(Utility.RandomMinMax(18, 30)));  break;
                    }
                }
                // Bonus Cat2 item orb (10%)
                if (Utility.RandomDouble() < 0.10)
                {
                    switch (Utility.Random(3))
                    {
                        case 0: corpse.DropItem(new OrbOfEnchantment()); break;
                        case 1: corpse.DropItem(new OrbOfTempering());   break;
                        default: corpse.DropItem(new OrbOfCorruption()); break;
                    }
                }
            }

            // Named item (Murderer 10%, Dread Lord 20%)
            double namedChance = WantedTier == 11 ? 0.10 : WantedTier == 12 ? 0.20 : 0.0;
            if (Utility.RandomDouble() < namedChance)
            {
                int weaponTier = WantedTier == 12 ? 3 : 2;
                corpse.DropItem(HunterWeaponFactory.GenerateNamedWeapon(
                    weaponTier,
                    Name.Replace("[Wanted] ", "").Split(' ')[0]));
            }

            // Clear wanted spawn slot and fire FBEventBus kill event
            HunterSystem.OnWantedKilled(this, killer);
        }

        public override bool ShowFameTitle => false;
    }

    // ============================================================
    // WANTED: CUTTHROAT (Tier 10)
    // ============================================================

    public class WantedCutthroat : BaseWantedNPC
    {
        protected override int    WantedTier  => 10;
        protected override string WantedLabel => "the Cutthroat";

        private static readonly string[] Names = {
            "Harlan", "Breswick", "Corvin", "Aldric", "Tomas",
            "Sylva", "Maren", "Brynn", "Eada", "Kessa"
        };

        protected override string[] AggroLines => new[] {
            "You shouldn't have come here.", "This is my road now!",
            "Last mistake you'll make.", "Draw your blade and die!"
        };
        protected override string[] KillLines => new[] {
            "Should've walked away.", "Easy coin.",
            "*wipes blade* Next.", "Stay down."
        };

        [Constructable]
        public WantedCutthroat() : base(AIType.AI_Melee, FightMode.Closest, 10)
        {
            Hue = 0x21;

            SetStr(175, 200);
            SetDex(175, 200);   // high dex = fast swing speed + hard to hit
            SetInt(75,  100);
            SetHits(550, 700);
            SetStam(175, 200);
            SetMana(75,  100);

            SetSkill(SkillName.Swords,      105.0, 110.0);
            SetSkill(SkillName.Tactics,     105.0, 110.0);
            SetSkill(SkillName.Anatomy,      97.0, 100.0);
            SetSkill(SkillName.Healing,      97.0, 100.0);
            SetSkill(SkillName.Parry,        97.0, 100.0);
            SetSkill(SkillName.Hiding,       57.0,  62.0);
            SetSkill(SkillName.MagicResist,  75.0,  80.0);

            SetResistance(ResistanceType.Physical, 28, 33);
            SetResistance(ResistanceType.Fire,     20, 25);
            SetResistance(ResistanceType.Cold,     20, 25);
            SetResistance(ResistanceType.Poison,   20, 25);
            SetResistance(ResistanceType.Energy,   20, 25);

            Fame        = 5000;
            Karma       = -5000;
            VirtualArmor = 21;

            string charName = Names[Utility.Random(Names.Length)];
            InitWantedName(charName);

            // Magic gear — dexxer focus, randomised ±50% on each stat (drops on corpse)
            var katana = new Katana();
            katana.Attributes.WeaponDamage       = Utility.RandomMinMax(7,  22);
            katana.Attributes.AttackChance       = Utility.RandomMinMax(5,  15);
            katana.Attributes.WeaponSpeed        = Utility.RandomMinMax(5,  15);
            katana.WeaponAttributes.HitLeechHits = Utility.RandomMinMax(10, 30);
            AddItem(katana);

            var chest = new LeatherChest();
            chest.Attributes.BonusStr = Utility.RandomMinMax(1, 4);
            chest.PhysicalBonus       = Utility.RandomMinMax(4, 12);
            AddItem(chest);

            var legs = new LeatherLegs();
            legs.Attributes.BonusDex = Utility.RandomMinMax(1, 4);
            legs.PhysicalBonus       = Utility.RandomMinMax(2, 7);
            AddItem(legs);

            var arms = new LeatherArms();
            arms.Attributes.BonusStam = Utility.RandomMinMax(2, 7);
            AddItem(arms);

            var gorget = new LeatherGorget();
            gorget.Attributes.RegenHits = 1;
            AddItem(gorget);

            var shield = new WoodenShield();
            shield.Attributes.DefendChance = Utility.RandomMinMax(4, 12);
            AddItem(shield);

            AddItem(new Boots(Utility.RandomNeutralHue()));
        }

        public WantedCutthroat(Serial serial) : base(serial) { }

        public override void OnAfterSpawn()
        {
            base.OnAfterSpawn();
            if (Mount == null)
            {
                var horse = new Horse();
                horse.Hue = Utility.RandomList(0, 0, 0x83C, 0x901, 0x8AC);
                horse.MoveToWorld(Location, Map);
                horse.Rider = this;
            }
        }

        public override void GenerateLoot()
        {
            PackGold(100, 400);
            PackItem(new Bandage(Utility.RandomMinMax(20, 40)));
        }

        public override void Serialize(GenericWriter writer) { base.Serialize(writer); writer.Write(0); }
        public override void Deserialize(GenericReader reader) { base.Deserialize(reader); reader.ReadInt(); }
    }

    // ============================================================
    // WANTED: MURDERER (Tier 11)
    // ============================================================

    public class WantedMurderer : BaseWantedNPC
    {
        protected override int    WantedTier  => 11;
        protected override string WantedLabel => "the Bloodsoaked";

        private static readonly string[] Names = {
            "Sevik", "Corvath", "Aldren", "Brannoc", "Torvin",
            "Cassara", "Morvaine", "Sylvara", "Dyrenna", "Kressel"
        };

        protected override string[] AggroLines => new[] {
            "I've killed a hundred like you.", "Your gold is already mine.",
            "Recall won't save you here.", "Bleed for me."
        };
        protected override string[] KillLines => new[] {
            "Another one down.", "Barely worth the effort.",
            "Collect the corpse.", "Too slow."
        };

        [Constructable]
        public WantedMurderer() : base(AIType.AI_Mage, FightMode.Closest, 12)
        {
            Hue = 0x47B;

            SetStr(150, 175);
            SetDex(125, 150);
            SetInt(175, 200);   // large mana pool for sustained casting
            SetHits(750, 950);
            SetStam(125, 150);
            SetMana(175, 200);

            SetSkill(SkillName.Swords,      107.0, 112.0);
            SetSkill(SkillName.Tactics,     107.0, 112.0);
            SetSkill(SkillName.Anatomy,      87.0,  92.0);
            SetSkill(SkillName.Healing,      97.0, 100.0);
            SetSkill(SkillName.Parry,        97.0, 100.0);
            SetSkill(SkillName.Magery,      107.0, 112.0);  // casts 7th/8th circle
            SetSkill(SkillName.EvalInt,     107.0, 112.0);
            SetSkill(SkillName.MagicResist,  97.0, 100.0);

            SetResistance(ResistanceType.Physical, 33, 38);
            SetResistance(ResistanceType.Fire,     25, 30);
            SetResistance(ResistanceType.Cold,     25, 30);
            SetResistance(ResistanceType.Poison,   25, 30);
            SetResistance(ResistanceType.Energy,   25, 30);

            Fame        = 12000;
            Karma       = -12000;
            VirtualArmor = 28;

            string charName = Names[Utility.Random(Names.Length)];
            InitWantedName(charName);

            // Magic gear — tank-mage focus, randomised ±50% on each stat (drops on corpse)
            var katana = new Katana();
            katana.Attributes.WeaponDamage       = Utility.RandomMinMax(12, 37);
            katana.Attributes.AttackChance       = Utility.RandomMinMax(7,  22);
            katana.Attributes.WeaponSpeed        = Utility.RandomMinMax(7,  22);
            katana.WeaponAttributes.HitLeechHits = Utility.RandomMinMax(15, 45);
            AddItem(katana);

            var chest = new RingmailChest();
            chest.ArmorAttributes.MageArmor = 1;
            chest.Attributes.BonusHits      = Utility.RandomMinMax(5,  15);
            chest.PhysicalBonus             = Utility.RandomMinMax(6,  18);
            AddItem(chest);

            var legs = new RingmailLegs();
            legs.ArmorAttributes.MageArmor = 1;
            legs.PhysicalBonus             = Utility.RandomMinMax(5,  15);
            AddItem(legs);

            var arms = new LeatherArms();
            arms.Attributes.BonusInt = Utility.RandomMinMax(2, 7);
            AddItem(arms);

            var gorget = new LeatherGorget();
            gorget.Attributes.RegenMana = Utility.RandomMinMax(1, 3);
            AddItem(gorget);

            var shield = new HeaterShield();
            shield.Attributes.DefendChance = Utility.RandomMinMax(7, 22);
            AddItem(shield);

            var spellbook = new Spellbook();
            spellbook.Attributes.SpellDamage = Utility.RandomMinMax(5, 15);
            spellbook.Attributes.CastSpeed   = 1;
            AddItem(spellbook);

            AddItem(new Boots(Utility.RandomNeutralHue()));
        }

        public WantedMurderer(Serial serial) : base(serial) { }

        public override void GenerateLoot()
        {
            PackGold(300, 900);
            PackItem(new Bandage(Utility.RandomMinMax(30, 50)));
            PackItem(new Spellbook());
            PackItem(new MandrakeRoot(Utility.RandomMinMax(30, 50)));
            PackItem(new BlackPearl(Utility.RandomMinMax(30, 50)));
        }

        public override void Serialize(GenericWriter writer) { base.Serialize(writer); writer.Write(0); }
        public override void Deserialize(GenericReader reader) { base.Deserialize(reader); reader.ReadInt(); }
    }

    // ============================================================
    // WANTED: DREAD LORD (Tier 12)
    // ============================================================

    public class WantedDreadLord : BaseWantedNPC
    {
        protected override int    WantedTier  => 12;
        protected override string WantedLabel => "the Deathless";

        private static readonly string[] Names = {
            "Valdris", "Morthen", "Draeven", "Noctaran", "Vorath",
            "Seryn", "Kyressa", "Malavin", "Duskara", "Nethara"
        };

        protected override string[] AggroLines => new[] {
            "Your soul feeds my power.", "Strangle...",
            "In Vas Nox!", "Kal Vas Xen Corp Ort!",
            "Death is only your beginning."
        };
        protected override string[] KillLines => new[] {
            "Rise and serve.", "Your agony is beautiful.",
            "Another soul for the darkness.", "Inevitable."
        };

        [Constructable]
        public WantedDreadLord() : base(AIType.AI_Mage, FightMode.Closest, 14)
        {
            Hue = 0x497;

            SetStr(175, 200);
            SetDex(100, 125);
            SetInt(225, 250);   // necromage needs massive mana for sustained necro+magery
            SetHits(1300, 1600);
            SetStam(100, 125);
            SetMana(225, 250);

            SetSkill(SkillName.Magery,       120.0, 125.0);
            SetSkill(SkillName.EvalInt,       120.0, 125.0);
            SetSkill(SkillName.MagicResist,   120.0, 125.0);
            SetSkill(SkillName.Necromancy,    120.0, 125.0);
            SetSkill(SkillName.SpiritSpeak,   120.0, 125.0);
            SetSkill(SkillName.Swords,        107.0, 112.0);
            SetSkill(SkillName.Tactics,       107.0, 112.0);
            SetSkill(SkillName.DetectHidden,   97.0, 100.0);

            SetResistance(ResistanceType.Physical, 30, 35);
            SetResistance(ResistanceType.Fire,     25, 30);
            SetResistance(ResistanceType.Cold,     33, 38);
            SetResistance(ResistanceType.Poison,   35, 40);
            SetResistance(ResistanceType.Energy,   28, 33);

            Fame        = 22000;
            Karma       = -22000;
            VirtualArmor = 33;

            string charName = Names[Utility.Random(Names.Length)];
            InitWantedName(charName);

            // Magic gear — necromage focus, randomised ±50% on each stat (drops on corpse)
            var harvester = new BoneHarvester();
            harvester.Attributes.WeaponDamage        = Utility.RandomMinMax(17, 52);
            harvester.Attributes.SpellChanneling     = 1;
            harvester.WeaponAttributes.HitLeechHits  = Utility.RandomMinMax(20, 60);
            harvester.WeaponAttributes.HitLeechMana  = Utility.RandomMinMax(15, 45);
            AddItem(harvester);

            var chest = new BoneChest();
            chest.Attributes.BonusHits  = Utility.RandomMinMax(10, 30);
            chest.Attributes.RegenHits  = Utility.RandomMinMax(1,  3);
            chest.PhysicalBonus         = Utility.RandomMinMax(7,  22);
            AddItem(chest);

            var arms = new BoneArms();
            arms.Attributes.BonusStr = Utility.RandomMinMax(2, 7);
            arms.PhysicalBonus       = Utility.RandomMinMax(5, 15);
            AddItem(arms);

            var legs = new BoneLegs();
            legs.Attributes.BonusDex = Utility.RandomMinMax(2, 7);
            legs.PhysicalBonus       = Utility.RandomMinMax(5, 15);
            AddItem(legs);

            var gloves = new BoneGloves();
            gloves.Attributes.BonusInt = Utility.RandomMinMax(2, 7);
            AddItem(gloves);

            var helm = new BoneHelm();
            helm.Attributes.LowerManaCost = Utility.RandomMinMax(4, 12);
            AddItem(helm);

            var spellbook = new Spellbook();
            spellbook.Attributes.SpellDamage   = Utility.RandomMinMax(10, 30);
            spellbook.Attributes.CastSpeed     = 1;
            spellbook.Attributes.CastRecovery  = Utility.RandomMinMax(1, 3);
            spellbook.Attributes.LowerManaCost = Utility.RandomMinMax(4, 12);
            AddItem(spellbook);

            var necroBook = new NecromancerSpellbook();
            necroBook.Attributes.SpellDamage  = Utility.RandomMinMax(7, 22);
            necroBook.Attributes.CastRecovery = 1;
            AddItem(necroBook);

            AddItem(new Sandals());

            PackItem(new GreaterHealPotion());
            PackItem(new GreaterHealPotion());
            PackItem(new GreaterCurePotion());
            PackItem(new GreaterCurePotion());
        }

        public WantedDreadLord(Serial serial) : base(serial) { }

        public override void GenerateLoot()
        {
            PackGold(500, 1500);
            PackItem(new Bandage(Utility.RandomMinMax(50, 80)));
            PackItem(new Spellbook());
            PackItem(new NecromancerSpellbook());
            PackItem(new BatWing(Utility.RandomMinMax(80, 100)));
            PackItem(new NoxCrystal(Utility.RandomMinMax(80, 100)));
            PackItem(new PigIron(Utility.RandomMinMax(80, 100)));
            PackItem(new GraveDust(Utility.RandomMinMax(80, 100)));
            PackItem(new MandrakeRoot(Utility.RandomMinMax(80, 100)));
            PackItem(new BlackPearl(Utility.RandomMinMax(80, 100)));
            PackItem(new GreaterHealPotion());
            PackItem(new GreaterHealPotion());
            PackItem(new GreaterCurePotion());
            PackItem(new GreaterCurePotion());
        }

        public override void Serialize(GenericWriter writer) { base.Serialize(writer); writer.Write(0); }
        public override void Deserialize(GenericReader reader) { base.Deserialize(reader); reader.ReadInt(); }
    }
}
