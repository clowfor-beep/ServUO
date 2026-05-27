// ============================================================
// SimPlayerGuilds.cs
// Scripts/Custom/SimPlayerGuilds.cs
//
// Guild-specific SimPlayer subclasses.
// Each overrides ApplyTemplate() with the correct stats,
// skills, and gear for that guild.
//
// Equipment rule (from CLAUDE.md):
//   Weapons / armour  --> AddItem(...)
//   Spellbooks        --> PackItem(...)  NEVER AddItem
//   BookOfChivalry    --> PackItem(...)  NEVER AddItem
// ============================================================

using System;
using Server;
using Server.Custom;
using Server.Items;
using Server.Mobiles;

namespace Server.Custom
{
    // ============================================================
    // Craftsmen's League
    // Blacksmith template -- slow moving, trade-focused
    // ============================================================
    public class CraftsmensLeagueSimPlayer : SimPlayer
    {
        public CraftsmensLeagueSimPlayer(string memberName, Point3D home,
                                         SpawnZone zone, ScheduleProfile schedule)
            : base(FBGuilds.CraftsmenLeague, memberName, home, zone, schedule) { }

        public CraftsmensLeagueSimPlayer(Serial serial) : base(serial) { }

        protected override void ApplyTemplate()
        {
            SetStr(70, 70);
            SetDex(50, 50);
            SetInt(30, 30);
            SetHits(80, 80);
            SetSkill(SkillName.Blacksmith, 100.0, 100.0);
            SetSkill(SkillName.Mining,     100.0, 100.0);
            SetSkill(SkillName.ArmsLore,    80.0,  80.0);
            SetSkill(SkillName.Tinkering,   60.0,  60.0);
            SetSkill(SkillName.Swords,      30.0,  30.0); // flee only
            VirtualArmor = 10;
            Fame  = 0;
            Karma = 1000;
            Kills = 0;
            ActiveSpeed = 0.3; // working pace -- moves slower

            AddItem(new LeatherChest());
            AddItem(new LeatherLegs());
            AddItem(new Boots());
            AddItem(new SmithHammer());
        }

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write(0); // version
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            reader.ReadInt(); // version
        }
    }

    // ============================================================
    // Iron Company
    // Heavy Warrior template -- plate armour, chivalry
    // ============================================================
    public class IronCompanySimPlayer : SimPlayer
    {
        public IronCompanySimPlayer(string memberName, Point3D home,
                                    SpawnZone zone, ScheduleProfile schedule)
            : base(FBGuilds.IronCompany, memberName, home, zone, schedule) { }

        public IronCompanySimPlayer(Serial serial) : base(serial) { }

        protected override void ApplyTemplate()
        {
            SetStr(85, 85);
            SetDex(65, 65);
            SetInt(30, 30);
            SetHits(120, 120);
            SetSkill(SkillName.Swords,      100.0, 100.0);
            SetSkill(SkillName.Tactics,     100.0, 100.0);
            SetSkill(SkillName.Anatomy,      90.0,  90.0);
            SetSkill(SkillName.Healing,      90.0,  90.0);
            SetSkill(SkillName.Parry,       100.0, 100.0);
            SetSkill(SkillName.MagicResist,  80.0,  80.0);
            SetSkill(SkillName.Chivalry,     80.0,  80.0);
            VirtualArmor = 40;
            Fame  = 2000;
            Karma = 2000;
            Kills = 0;

            AddItem(new PlateChest());
            AddItem(new PlateLegs());
            AddItem(new PlateArms());
            AddItem(new PlateGloves());
            AddItem(new PlateGorget());
            AddItem(new PlateHelm());
            AddItem(new Boots());
            AddItem(new Longsword());
            AddItem(new HeaterShield());
            PackItem(new BookOfChivalry()); // always PackItem
        }

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write(0); // version
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            reader.ReadInt(); // version
        }
    }

    // ============================================================
    // Arcane Brotherhood
    // War Mage template -- robes, spellbook
    // ============================================================
    public class ArcaneBrotherhoodSimPlayer : SimPlayer
    {
        public ArcaneBrotherhoodSimPlayer(string memberName, Point3D home,
                                          SpawnZone zone, ScheduleProfile schedule)
            : base(FBGuilds.ArcaneBrotherhood, memberName, home, zone, schedule) { }

        public ArcaneBrotherhoodSimPlayer(Serial serial) : base(serial) { }

        protected override void ApplyTemplate()
        {
            SetStr(30, 30);
            SetDex(35, 35);
            SetInt(115, 115);
            SetHits(60, 60);
            SetSkill(SkillName.Magery,      100.0, 100.0);
            SetSkill(SkillName.EvalInt,     100.0, 100.0);
            SetSkill(SkillName.MagicResist, 100.0, 100.0);
            SetSkill(SkillName.Meditation,   90.0,  90.0);
            SetSkill(SkillName.Wrestling,    80.0,  80.0);
            SetSkill(SkillName.Focus,        70.0,  70.0);
            SetSkill(SkillName.Inscribe,     60.0,  60.0);
            VirtualArmor = 10;
            Fame  = 2000;
            Karma = 2000;
            Kills = 0;

            AddItem(new Robe());
            AddItem(new Sandals());
            AddItem(new WizardsHat());
            PackItem(new Spellbook()); // always PackItem for spellbooks
        }

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write(0); // version
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            reader.ReadInt(); // version
        }
    }

    // ============================================================
    // Silver Wolves
    // Wolf Warrior template -- chain armour, patrol role
    // ============================================================
    public class SilverWolvesSimPlayer : SimPlayer
    {
        public SilverWolvesSimPlayer(string memberName, Point3D home,
                                     SpawnZone zone, ScheduleProfile schedule)
            : base(FBGuilds.SilverWolves, memberName, home, zone, schedule) { }

        public SilverWolvesSimPlayer(Serial serial) : base(serial) { }

        protected override void ApplyTemplate()
        {
            SetStr(80, 80);
            SetDex(65, 65);
            SetInt(35, 35);
            SetHits(110, 110);
            SetSkill(SkillName.Swords,        100.0, 100.0);
            SetSkill(SkillName.Tactics,       100.0, 100.0);
            SetSkill(SkillName.Anatomy,        90.0,  90.0);
            SetSkill(SkillName.Healing,        90.0,  90.0);
            SetSkill(SkillName.Parry,          90.0,  90.0);
            SetSkill(SkillName.MagicResist,    80.0,  80.0);
            SetSkill(SkillName.DetectHidden,   50.0,  50.0);
            VirtualArmor = 35;
            Fame  = 2000;
            Karma = 3000;
            Kills = 0;

            AddItem(new ChainChest());
            AddItem(new ChainLegs());
            AddItem(new LeatherGorget());
            AddItem(new LeatherArms());
            AddItem(new LeatherGloves());
            AddItem(new Boots());
            AddItem(new Longsword());
            AddItem(new HeaterShield());
        }

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write(0); // version
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            reader.ReadInt(); // version
        }
    }

    // ============================================================
    // The Shadow Hand
    // Thief/Rogue template -- grey, hiding, town operations.
    // Never fights. Flees Silver Wolves.
    // ============================================================
    public class ShadowHandSimPlayer : SimPlayer
    {
        private DateTime _nextHideTime = DateTime.MinValue;

        public ShadowHandSimPlayer(string memberName, Point3D home,
                                   SpawnZone zone, ScheduleProfile schedule)
            : base(FBGuilds.ShadowHand, memberName, home, zone, schedule) { }

        public ShadowHandSimPlayer(Serial serial) : base(serial) { }

        protected override void ApplyTemplate()
        {
            SetStr(35, 35);
            SetDex(75, 75);
            SetInt(50, 50);
            SetHits(60, 60);

            SetSkill(SkillName.Stealing,      80.0,  80.0);
            SetSkill(SkillName.Hiding,       100.0, 100.0);
            SetSkill(SkillName.Stealth,       90.0,  90.0);
            SetSkill(SkillName.Snooping,      80.0,  80.0);
            SetSkill(SkillName.DetectHidden,  50.0,  50.0);
            SetSkill(SkillName.ItemID,        60.0,  60.0);
            SetSkill(SkillName.Fencing,       40.0,  40.0);
            SetSkill(SkillName.Tactics,       30.0,  30.0);

            VirtualArmor = 5;
            Fame  = 0;
            Karma = -500; // grey alignment
            Kills = 0;
            Hue   = 0;    // normal skin -- blend in

            // Dark, inconspicuous clothing
            var robe = new Robe();
            robe.Hue = 1109; // dark charcoal
            AddItem(robe);

            var boots = new Boots();
            boots.Hue = 1109;
            AddItem(boots);
        }

        /// <summary>
        /// Shadow Hand idle hook -- hide periodically and flee Silver Wolves.
        /// </summary>
        protected override void OnTickIdle()
        {
            // Periodically go Hidden
            if (DateTime.UtcNow >= _nextHideTime)
            {
                this.Hidden   = true;
                _nextHideTime = DateTime.UtcNow + TimeSpan.FromSeconds(Utility.RandomMinMax(20, 60));
            }

            // Flee Silver Wolves on sight
            Mobile wolf = FindNearbyWolf();
            if (wolf != null)
                FleeFrom(wolf);
        }

        private Mobile FindNearbyWolf()
        {
            foreach (Mobile m in GetMobilesInRange(12))
            {
                if (!m.Deleted && m is SilverWolvesSimPlayer)
                    return m;
            }
            return null;
        }

        private void FleeFrom(Mobile threat)
        {
            // Pick a point roughly opposite the threat direction
            int dx = X - threat.X;
            int dy = Y - threat.Y;

            int fx = X + dx + Utility.RandomMinMax(-8, 8);
            int fy = Y + dy + Utility.RandomMinMax(-8, 8);
            int fz = Map.GetAverageZ(fx, fy);

            if (!Map.CanSpawnMobile(fx, fy, fz))
            {
                fx = _homeLocation.X + Utility.RandomMinMax(-15, 15);
                fy = _homeLocation.Y + Utility.RandomMinMax(-15, 15);
                fz = Map.GetAverageZ(fx, fy);
            }

            if (Map.CanSpawnMobile(fx, fy, fz))
            {
                this.Hidden = true;
                StartTravelTo(new Point3D(fx, fy, fz), TimeSpan.FromSeconds(20));
            }
        }

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write(0); // version
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            reader.ReadInt(); // version
        }
    }

    // ============================================================
    // Paladin Order  (Tier 3 — Blue)
    // Heavy plate, chivalry, healing. Blue alignment.
    // StatCap 225, elite warrior.
    // ============================================================
    public class PaladinOrderSimPlayer : SimPlayer
    {
        public PaladinOrderSimPlayer(string memberName, Point3D home,
                                     SpawnZone zone, ScheduleProfile schedule)
            : base(FBGuilds.PaladinOrder, memberName, home, zone, schedule) { }

        public PaladinOrderSimPlayer(Serial serial) : base(serial) { }

        // Blue -- override BaseFBCombatNPC defaults
        public override bool AlwaysAttackable => false;
        public override bool AlwaysInnocent   => true;

        protected override void ApplyTemplate()
        {
            SetStr(90, 90);
            SetDex(80, 80);
            SetInt(55, 55);
            SetHits(200, 200);

            SetSkill(SkillName.Swords,      100.0, 100.0);
            SetSkill(SkillName.Tactics,     100.0, 100.0);
            SetSkill(SkillName.Anatomy,     100.0, 100.0);
            SetSkill(SkillName.Healing,     100.0, 100.0);
            SetSkill(SkillName.Parry,       100.0, 100.0);
            SetSkill(SkillName.Chivalry,    100.0, 100.0);
            SetSkill(SkillName.MagicResist, 100.0, 100.0);

            VirtualArmor = 55;
            Fame  = 10000;
            Karma = 5000;
            Kills = 0;

            // White/silver plate
            var chest = new PlateChest();   chest.Hue = 1153; AddItem(chest);
            var legs  = new PlateLegs();    legs.Hue  = 1153; AddItem(legs);
            var arms  = new PlateArms();    arms.Hue  = 1153; AddItem(arms);
            var glove = new PlateGloves();  glove.Hue = 1153; AddItem(glove);
            var helm  = new PlateHelm();    helm.Hue  = 1153; AddItem(helm);
            var shield = new HeaterShield(); shield.Hue = 1153; AddItem(shield);
            AddItem(new Longsword());
            PackItem(new BookOfChivalry()); // always PackItem
        }

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write(0); // version
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            reader.ReadInt(); // version
        }
    }

    // ============================================================
    // Dead Watchers  (Tier 3 -- Perm Grey)
    // Bone armour, necromancy. Grey alignment -- attackable.
    // StatCap 225.
    // ============================================================
    public class DeadWatchersSimPlayer : SimPlayer
    {
        public DeadWatchersSimPlayer(string memberName, Point3D home,
                                     SpawnZone zone, ScheduleProfile schedule)
            : base(FBGuilds.DeadWatchers, memberName, home, zone, schedule) { }

        public DeadWatchersSimPlayer(Serial serial) : base(serial) { }

        // Grey -- attackable but not murderer
        public override bool AlwaysAttackable => true;

        protected override void ApplyTemplate()
        {
            SetStr(90, 90);
            SetDex(80, 80);
            SetInt(55, 55);
            SetHits(200, 200);

            SetSkill(SkillName.Swords,      100.0, 100.0);
            SetSkill(SkillName.Tactics,     100.0, 100.0);
            SetSkill(SkillName.Anatomy,     100.0, 100.0);
            SetSkill(SkillName.Healing,     100.0, 100.0);
            SetSkill(SkillName.Necromancy,  100.0, 100.0);
            SetSkill(SkillName.SpiritSpeak, 100.0, 100.0);
            SetSkill(SkillName.Parry,       100.0, 100.0);

            VirtualArmor = 45;
            Fame  = 8000;
            Karma = -100; // perm grey
            Kills = 0;

            // Dark bone armour
            var chest = new BoneChest();  chest.Hue = 0x455; AddItem(chest);
            var legs  = new BoneLegs();   legs.Hue  = 0x455; AddItem(legs);
            var arms  = new BoneArms();   arms.Hue  = 0x455; AddItem(arms);
            var glove = new BoneGloves(); glove.Hue = 0x455; AddItem(glove);
            var helm  = new BoneHelm();   helm.Hue  = 0x455; AddItem(helm);
            AddItem(new Longsword());
            PackItem(new NecromancerSpellbook()); // always PackItem
        }

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write(0); // version
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            reader.ReadInt(); // version
        }
    }

    // ============================================================
    // Dread Hunters  (Tier 3 -- Blue/Grey)
    // Elite plate + chivalry, GM120 skills. Blue alignment.
    // StatCap 300, skill cap 880.
    // ============================================================
    public class DreadHuntersSimPlayer : SimPlayer
    {
        public DreadHuntersSimPlayer(string memberName, Point3D home,
                                     SpawnZone zone, ScheduleProfile schedule)
            : base(FBGuilds.DreadHunters, memberName, home, zone, schedule) { }

        public DreadHuntersSimPlayer(Serial serial) : base(serial) { }

        // Blue -- override BaseFBCombatNPC defaults
        public override bool AlwaysAttackable => false;
        public override bool AlwaysInnocent   => true;

        protected override void ApplyTemplate()
        {
            SetStr(125, 125);
            SetDex(125, 125);
            SetInt(50, 50);
            SetHits(300, 300);

            SetSkill(SkillName.Swords,      120.0, 120.0);
            SetSkill(SkillName.Tactics,     120.0, 120.0);
            SetSkill(SkillName.Anatomy,     120.0, 120.0);
            SetSkill(SkillName.Healing,     120.0, 120.0);
            SetSkill(SkillName.Parry,       120.0, 120.0);
            SetSkill(SkillName.MagicResist, 100.0, 100.0);
            SetSkill(SkillName.Chivalry,    100.0, 100.0);
            SetSkill(SkillName.Focus,        80.0,  80.0);

            VirtualArmor = 60;
            Fame  = 15000;
            Karma = 1000;
            Kills = 0;

            // Dark grey plate
            var chest  = new PlateChest();   chest.Hue  = 0x497; AddItem(chest);
            var legs   = new PlateLegs();    legs.Hue   = 0x497; AddItem(legs);
            var arms   = new PlateArms();    arms.Hue   = 0x497; AddItem(arms);
            var glove  = new PlateGloves();  glove.Hue  = 0x497; AddItem(glove);
            var helm   = new PlateHelm();    helm.Hue   = 0x497; AddItem(helm);
            var shield = new HeaterShield(); shield.Hue = 0x497; AddItem(shield);
            AddItem(new Longsword());
            PackItem(new BookOfChivalry()); // always PackItem
        }

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write(0); // version
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            reader.ReadInt(); // version
        }
    }

    // ============================================================
    // Blood Pact  (Tier 3 -- Red/Murderer)
    // NecroMage template. Red alignment -- AlwaysMurderer.
    // StatCap 250, skill cap 800. Home: Destard outskirts.
    // ============================================================
    public class BloodPactSimPlayer : SimPlayer
    {
        public BloodPactSimPlayer(string memberName, Point3D home,
                                  SpawnZone zone, ScheduleProfile schedule)
            : base(FBGuilds.BloodPact, memberName, home, zone, schedule) { }

        public BloodPactSimPlayer(Serial serial) : base(serial) { }

        // Red -- murderer flag
        public override bool AlwaysAttackable => true;
        public override bool AlwaysMurderer   => true;

        protected override void ApplyTemplate()
        {
            SetStr(50, 50);
            SetDex(75, 75);
            SetInt(125, 125);
            SetHits(150, 150);

            SetSkill(SkillName.Magery,      100.0, 100.0);
            SetSkill(SkillName.EvalInt,     100.0, 100.0);
            SetSkill(SkillName.MagicResist, 100.0, 100.0);
            SetSkill(SkillName.Necromancy,  100.0, 100.0);
            SetSkill(SkillName.SpiritSpeak, 100.0, 100.0);
            SetSkill(SkillName.Meditation,  100.0, 100.0);
            SetSkill(SkillName.Wrestling,   100.0, 100.0);
            SetSkill(SkillName.Hiding,      100.0, 100.0);

            VirtualArmor = 15;
            Fame  = 10000;
            Karma = -5000; // red
            Kills = 10;

            // Black robes
            var robe = new Robe(); robe.Hue = 0x455; AddItem(robe);
            AddItem(new Sandals());
            PackItem(new Spellbook());            // always PackItem
            PackItem(new NecromancerSpellbook()); // always PackItem
        }

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write(0); // version
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            reader.ReadInt(); // version
        }
    }

    // ============================================================
    // The Void  (Tier 3 -- Red/Murderer)
    // GM120 NecroMage. Red alignment -- AlwaysMurderer.
    // StatCap 270, skill cap 850. Home: Deceit outskirts.
    // ============================================================
    public class TheVoidSimPlayer : SimPlayer
    {
        public TheVoidSimPlayer(string memberName, Point3D home,
                                SpawnZone zone, ScheduleProfile schedule)
            : base(FBGuilds.TheVoid, memberName, home, zone, schedule) { }

        public TheVoidSimPlayer(Serial serial) : base(serial) { }

        // Red -- murderer flag
        public override bool AlwaysAttackable => true;
        public override bool AlwaysMurderer   => true;

        protected override void ApplyTemplate()
        {
            SetStr(60, 60);
            SetDex(85, 85);
            SetInt(125, 125);
            SetHits(170, 170);

            SetSkill(SkillName.Magery,      120.0, 120.0);
            SetSkill(SkillName.EvalInt,     120.0, 120.0);
            SetSkill(SkillName.MagicResist, 120.0, 120.0);
            SetSkill(SkillName.Necromancy,  120.0, 120.0);
            SetSkill(SkillName.SpiritSpeak, 120.0, 120.0);
            SetSkill(SkillName.Meditation,  100.0, 100.0);
            SetSkill(SkillName.Wrestling,   100.0, 100.0);
            SetSkill(SkillName.Hiding,       50.0,  50.0);

            VirtualArmor = 15;
            Fame  = 15000;
            Karma = -8000; // red
            Kills = 15;

            // Black robes
            var robe = new Robe(); robe.Hue = 0x455; AddItem(robe);
            AddItem(new Sandals());
            PackItem(new Spellbook());            // always PackItem
            PackItem(new NecromancerSpellbook()); // always PackItem
        }

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write(0); // version
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            reader.ReadInt(); // version
        }
    }

    // ============================================================
    // Shadowblade  (Tier 3 -- Grey)
    // Shadow Assassin template. Grey alignment -- attackable.
    // StatCap 250, skill cap 800. Home: Wrong outskirts.
    // ============================================================
    public class ShadowbladeSimPlayer : SimPlayer
    {
        public ShadowbladeSimPlayer(string memberName, Point3D home,
                                    SpawnZone zone, ScheduleProfile schedule)
            : base(FBGuilds.Shadowblade, memberName, home, zone, schedule) { }

        public ShadowbladeSimPlayer(Serial serial) : base(serial) { }

        // Grey -- attackable but not murderer
        public override bool AlwaysAttackable => true;

        protected override void ApplyTemplate()
        {
            SetStr(75, 75);
            SetDex(125, 125);
            SetInt(50, 50);
            SetHits(160, 160);

            SetSkill(SkillName.Ninjitsu,      100.0, 100.0);
            SetSkill(SkillName.Hiding,        100.0, 100.0);
            SetSkill(SkillName.Stealth,       100.0, 100.0);
            SetSkill(SkillName.Fencing,       100.0, 100.0);
            SetSkill(SkillName.Tactics,       100.0, 100.0);
            SetSkill(SkillName.Anatomy,       100.0, 100.0);
            SetSkill(SkillName.Healing,       100.0, 100.0);
            SetSkill(SkillName.DetectHidden,  100.0, 100.0);

            VirtualArmor = 20;
            Fame  = 5000;
            Karma = -500; // grey
            Kills = 0;

            // Black ninja gear
            var jacket = new LeatherNinjaJacket(); jacket.Hue = 0x455; AddItem(jacket);
            var hood   = new LeatherNinjaHood();   hood.Hue   = 0x455; AddItem(hood);
            var tabi   = new NinjaTabi();           tabi.Hue   = 0x455; AddItem(tabi);

            // Alternate between Kryss and AssassinSpike for variety
            if (Utility.RandomBool())
                AddItem(new Kryss());
            else
                AddItem(new AssassinSpike());
        }

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write(0); // version
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            reader.ReadInt(); // version
        }
    }
}
