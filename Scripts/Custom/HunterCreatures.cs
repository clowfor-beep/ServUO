// ============================================================
// HunterCreatures.cs
// Scripts/Custom/HunterCreatures.cs
//
// Abstract HunterCreature base class + all 19 hunter creature
// variants across Tiers 1–4.
//
//   Tier 1 (hue 0x021, 2x hits): OgreLord, TrollChief,
//     Harpy, EttinWarlord, LizardShaman
//   Tier 2 (hue 0x47B, 3x hits): Dragon, Balron,
//     LichLord, Daemon
//   Tier 3 (hue 0x4A0, 4x hits): AncientWyrm, PrimevalLich,
//     UndeadLord, BloodDragon, AbyssalDaemon
//   Tier 4 (hue 0x497, 5x hits): Rikktor, Barracoon, Neira,
//     Mephitis, Semidar, LordOaks
//
// Design doc: Design/HunterSystemDesignDoc.txt
// ============================================================

using System;
using System.Collections.Generic;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Server.Custom
{
    // ============================================================
    // ABSTRACT BASE
    // ============================================================

    public abstract class HunterCreature : BaseCreature
    {
        // Set by subclass constructor
        protected abstract int    HunterTier        { get; }
        protected abstract string HunterCreatureName { get; }  // picks a random name — call once via InitHunterName
        protected abstract string HunterTitle        { get; }  // e.g. "the Ancient Ogre"

        // Resolved once in InitHunterName so all references use the same name
        private string _resolvedName;
        protected string ResolvedName => _resolvedName ?? string.Empty;

        // Periodic presence shout (Tier 3+)
        private Timer _shoutTimer;

        protected HunterCreature(AIType ai, FightMode mode, int range, int perRange,
                                  double activeSpeed, double passiveSpeed)
            : base(ai, mode, range, perRange, activeSpeed, passiveSpeed)
        {
        }

        public HunterCreature(Serial serial) : base(serial) { }

        // Called by subclass after base constructor sets stats
        protected void InitHunterName()
        {
            _resolvedName = HunterCreatureName;  // evaluate random name exactly once
            Name  = $"[Hunted] {_resolvedName} {HunterTitle}";
            Title = string.Empty;

            // Apply stat multipliers after subclass has set all values:
            //   HP x2   — double staying power vs base creature
            //   Str x1.25, Int x1.25 — 25% more melee and spell damage
            SetHits(HitsMax * 2);
            SetStr((int)(RawStr * 1.25));
            SetInt((int)(RawInt * 1.25));
        }

        protected void StartPresenceShouts()
        {
            if (HunterTier < 3) return;

            _shoutTimer = Timer.DelayCall(
                TimeSpan.FromSeconds(Utility.RandomMinMax(45, 90)),
                TimeSpan.FromSeconds(Utility.RandomMinMax(60, 120)),
                ShoutPresence);
        }

        private void ShoutPresence()
        {
            if (Deleted || !Alive) return;

            string[] shouts = {
                $"*The ground trembles as {_resolvedName} stirs*",
                $"*A chilling roar echoes from {_resolvedName}*",
                $"*{_resolvedName} lets out a deafening bellow*",
                $"*The air grows heavy around {_resolvedName}*"
            };

            PublicOverheadMessage(MessageType.Regular, 0x22, false,
                shouts[Utility.Random(shouts.Length)]);
        }

        public override void OnDeath(Container corpse)
        {
            base.OnDeath(corpse);

            if (_shoutTimer != null)
            {
                _shoutTimer.Stop();
                _shoutTimer = null;
            }

            // Find killer — walk up to pet owner if killed by a controlled creature
            Mobile killer = LastKiller;
            if (killer is BaseCreature bc && bc.Controlled && bc.ControlMaster is PlayerMobile)
                killer = bc.ControlMaster;

            string killerName = killer?.Name ?? "an unknown hunter";
            string fullName   = $"{_resolvedName} {HunterTitle}";

            // World broadcast
            HunterSystem.BroadcastHuntKill(fullName, killerName);

            // Award hunter points
            if (killer is PlayerMobile pm)
            {
                int pts = HunterSystem.TierPoints(HunterTier);
                HunterSystem.AddPoints(pm, pts);
                HunterSystem.CheckRankUp(pm);

                // Head — into killer's pack if room, else corpse
                var head = new HunterHead(
                    fullName,
                    HunterTier,
                    killerName,
                    DateTime.UtcNow);

                if (pm.Backpack != null && pm.Backpack.TryDropItem(pm, head, false))
                    pm.SendMessage(0x35, "The head of your quarry falls into your pack.");
                else
                    corpse.DropItem(head);
            }

            // Medallion always on corpse
            corpse.DropItem(new HunterMedallion(
                fullName,
                killerName,
                DateTime.UtcNow));

            // Tokens
            int tokens = HunterSystem.TierTokens(HunterTier);
            if (tokens > 0)
                corpse.DropItem(new HunterToken(tokens));

            // Named weapon chance
            double namedChance = HunterSystem.TierNamedWeaponChance(HunterTier);
            if (Utility.RandomDouble() < namedChance)
                corpse.DropItem(HunterWeaponFactory.GenerateNamedWeapon(HunterTier,
                    _resolvedName));

            // Tier 4 unique boss artifact (5%) — subclass-specific flavor drop
            if (HunterTier == 4 && Utility.RandomDouble() < 0.05)
                corpse.DropItem(GenerateTier4Artifact());

            // Power scroll drops: T2=15%→105, T3=30%→105-110, T4=50%→110-120
            if (HunterTier == 2 && Utility.RandomDouble() < 0.15)
                corpse.DropItem(PowerScroll.CreateRandomNoCraft(5, 5));
            else if (HunterTier == 3 && Utility.RandomDouble() < 0.30)
                corpse.DropItem(PowerScroll.CreateRandomNoCraft(5, 10));
            else if (HunterTier == 4 && Utility.RandomDouble() < 0.50)
                corpse.DropItem(PowerScroll.CreateRandomNoCraft(10, 20));

            // Tiered minor artifact: T2=2%, T3=5%, T4=15%
            double artifactChance = HunterTier == 2 ? 0.02
                                  : HunterTier == 3 ? 0.05
                                  : HunterTier == 4 ? 0.15
                                  : 0.0;
            if (artifactChance > 0.0 && Utility.RandomDouble() < artifactChance)
            {
                Item artifact = GenerateMinorArtifact(HunterTier);
                if (artifact != null)
                    corpse.DropItem(artifact);
            }

            // Orb drop — first orb
            double orbChance = HunterSystem.TierOrbChance(HunterTier);
            if (Utility.RandomDouble() < orbChance)
                corpse.DropItem(GenerateOrb(HunterTier));

            // Bonus second orb (T3: 20%, T4: 50%)
            double bonusOrbChance = HunterSystem.TierBonusOrbChance(HunterTier);
            if (bonusOrbChance > 0.0 && Utility.RandomDouble() < bonusOrbChance)
                corpse.DropItem(GenerateOrb(HunterTier));

            // Clear active spawn slot and fire FBEventBus kill event
            // (killer already resolved above with pet-owner walk-up)
            HunterSystem.OnHunterKilled(this, killer);
        }

        protected virtual Item GenerateTier4Artifact() => new HunterToken(1); // override in boss classes

        // -- Minor artifact pools --------------------------------------
        // T2: accessible desirables.  T3: adds mid-tier pieces.  T4: full pool.
        private static readonly Type[] ArtifactPoolT2 = {
            typeof(CaptainJohnsHat),
            typeof(DetectiveBoots),
            typeof(OblivionsNeedle),
            typeof(ANecromancerShroud),
            typeof(TheMostKnowledgePerson),
        };

        private static readonly Type[] ArtifactPoolT3 = {
            typeof(CaptainJohnsHat),
            typeof(DetectiveBoots),
            typeof(OblivionsNeedle),
            typeof(ANecromancerShroud),
            typeof(TheMostKnowledgePerson),
            typeof(BraveKnightOfTheBritannia),
            typeof(LieutenantOfTheBritannianRoyalGuard),
            typeof(TokenOfHolyFavor),
            typeof(ProtectoroftheBattleMage),
        };

        private static readonly Type[] ArtifactPoolT4 = {
            typeof(CaptainJohnsHat),
            typeof(DetectiveBoots),
            typeof(OblivionsNeedle),
            typeof(ANecromancerShroud),
            typeof(TheMostKnowledgePerson),
            typeof(BraveKnightOfTheBritannia),
            typeof(LieutenantOfTheBritannianRoyalGuard),
            typeof(TokenOfHolyFavor),
            typeof(ProtectoroftheBattleMage),
            typeof(ShroudOfDeceit),
            typeof(RoyalGuardSurvivalKnife),
            typeof(RoyalGuardInvestigatorsCloak),
        };

        private static Item GenerateMinorArtifact(int tier)
        {
            Type[] pool = tier >= 4 ? ArtifactPoolT4
                        : tier == 3 ? ArtifactPoolT3
                        : ArtifactPoolT2;

            Type t = pool[Utility.Random(pool.Length)];
            return Loot.Construct(t);
        }

        private static Item GenerateOrb(int tier)
        {
            switch (tier)
            {
                case 1:
                    // T1: EssenceShard only — reward for engaging at all
                    return new EssenceShard(Utility.RandomMinMax(5, 12));

                case 2:
                    // T2: Cat 1 T1 orbs + Essences
                    switch (Utility.Random(5))
                    {
                        case 0: return new OrbOfEnhancement(1);
                        case 1: return new OrbOfMastery(1);
                        case 2: return new OrbOfExpansion(1);
                        case 3: return new OrbOfFortitude(1);
                        default: return new EssenceShard(Utility.RandomMinMax(8, 18));
                    }

                case 3:
                    // T3: Cat 1 T1-2 + Alacrity + Insight + richer Essences
                    switch (Utility.Random(8))
                    {
                        case 0: return new OrbOfEnhancement(Utility.RandomMinMax(1, 2));
                        case 1: return new OrbOfMastery(1);
                        case 2: return new OrbOfExpansion(Utility.RandomMinMax(1, 2));
                        case 3: return new OrbOfFortitude(1);
                        case 4: return new OrbOfAlacrity(1);
                        case 5: return new OrbOfInsight();
                        case 6: return new OrbOfBalance(1);
                        default: return new EssenceShard(Utility.RandomMinMax(15, 30));
                    }

                case 4:
                default:
                    // T4: Full pool — Cat 1 T2-3, Cat 2 item orbs, Cat 3 scrolls
                    switch (Utility.Random(12))
                    {
                        case 0:  return new OrbOfEnhancement(Utility.RandomMinMax(2, 3));
                        case 1:  return new OrbOfMastery(Utility.RandomMinMax(1, 2));
                        case 2:  return new OrbOfExpansion(Utility.RandomMinMax(2, 3));
                        case 3:  return new OrbOfFortitude(Utility.RandomMinMax(1, 2));
                        case 4:  return new OrbOfAlacrity(Utility.RandomMinMax(1, 2));
                        case 5:  return new OrbOfBalance(Utility.RandomMinMax(1, 2));
                        case 6:  return new OrbOfEnchantment();
                        case 7:  return new OrbOfTempering();
                        case 8:  return new OrbOfCorruption();
                        case 9:  return new OrbOfResonance();
                        case 10: return new ScrollOfExecution(Utility.RandomMinMax(1, 2));
                        default: return new EssenceShard(Utility.RandomMinMax(25, 50));
                    }
            }
        }

        public override bool ShowFameTitle => false;

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write(1);  // version
            writer.Write(_resolvedName ?? string.Empty);
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            int version = reader.ReadInt();
            if (version >= 1)
                _resolvedName = reader.ReadString();
        }
    }

    // ============================================================
    // TIER 1 — DUNGEON BRUTES  (hue 0x21, 2x hits)
    // ============================================================

    public class HunterOgreLord : HunterCreature
    {
        private static readonly string[] Names  = { "Grimtooth", "Skarruk", "Bonecrusher", "Ironjaw", "Rotfist" };
        private static readonly string   Title_ = "the Ancient Ogre";

        protected override int    HunterTier         => 1;
        protected override string HunterCreatureName => Names[Utility.Random(Names.Length)];
        protected override string HunterTitle        => Title_;

        [Constructable]
        public HunterOgreLord() : base(AIType.AI_Melee, FightMode.Closest, 10, 1, 0.2, 0.4)
        {
            Body = 83;
            Hue  = 0x21;

            SetStr(1534, 1890);  // ~2x base (767-945)
            SetDex(66, 75);
            SetInt(46, 70);
            SetHits(952, 1104); // 2x base
            SetStam(66, 75);
            SetMana(46, 70);

            SetSkill(SkillName.Tactics,     80.0, 100.0);
            SetSkill(SkillName.MagicResist, 80.0, 90.0);
            SetSkill(SkillName.Wrestling,   80.0, 100.0);

            Fame        = 22000;
            Karma       = -22000;
            VirtualArmor = 50;

            InitHunterName();
        }

        public HunterOgreLord(Serial serial) : base(serial) { }

        public override void GenerateLoot()
        {
            AddLoot(LootPack.UltraRich, 3);
            PackGold(400, 1000);
            PackItem(new MandrakeRoot(Utility.RandomMinMax(30, 50)));
            PackItem(new BlackPearl(Utility.RandomMinMax(30, 50)));
        }

        public override void Serialize(GenericWriter writer) { base.Serialize(writer); writer.Write(0); }
        public override void Deserialize(GenericReader reader) { base.Deserialize(reader); reader.ReadInt(); }
    }

    public class HunterTrollChief : HunterCreature
    {
        private static readonly string[] Names  = { "Skarr", "Grubfang", "Mudtusk", "Warcry", "Splitskull" };
        private static readonly string   Title_ = "the Troll Chief";

        protected override int    HunterTier         => 1;
        protected override string HunterCreatureName => Names[Utility.Random(Names.Length)];
        protected override string HunterTitle        => Title_;

        [Constructable]
        public HunterTrollChief() : base(AIType.AI_Melee, FightMode.Closest, 10, 1, 0.2, 0.4)
        {
            Body = Utility.RandomList(53, 54);
            Hue  = 0x21;

            SetStr(352, 410);
            SetDex(46, 65);
            SetInt(46, 70);
            SetHits(212, 246);
            SetStam(46, 65);
            SetMana(46, 70);

            SetSkill(SkillName.Tactics,     80.0, 95.0);
            SetSkill(SkillName.MagicResist, 80.0, 90.0);
            SetSkill(SkillName.Wrestling,   80.0, 95.0);

            Fame        = 10000;
            Karma       = -10000;
            VirtualArmor = 44;

            InitHunterName();
        }

        public HunterTrollChief(Serial serial) : base(serial) { }

        public override void GenerateLoot()
        {
            AddLoot(LootPack.UltraRich, 3);
            PackGold(400, 1000);
            PackItem(new Bandage(Utility.RandomMinMax(30, 50)));
        }

        public override void Serialize(GenericWriter writer) { base.Serialize(writer); writer.Write(0); }
        public override void Deserialize(GenericReader reader) { base.Deserialize(reader); reader.ReadInt(); }
    }

    public class HunterHarpy : HunterCreature
    {
        private static readonly string[] Names  = { "Screamwing", "Taloncrest", "Bladeshriek", "Venomquill", "Skydeath" };
        private static readonly string   Title_ = "the Harpy Queen";

        protected override int    HunterTier         => 1;
        protected override string HunterCreatureName => Names[Utility.Random(Names.Length)];
        protected override string HunterTitle        => Title_;

        [Constructable]
        public HunterHarpy() : base(AIType.AI_Melee, FightMode.Closest, 10, 1, 0.2, 0.4)
        {
            Body = 30;
            Hue  = 0x21;

            SetStr(192, 240);
            SetDex(80, 100);
            SetInt(46, 70);
            SetHits(116, 144);
            SetStam(80, 100);
            SetMana(46, 70);

            SetSkill(SkillName.Tactics,     80.0, 95.0);
            SetSkill(SkillName.MagicResist, 80.0, 90.0);
            SetSkill(SkillName.Wrestling,   80.0, 95.0);

            Fame        = 8000;
            Karma       = -8000;
            VirtualArmor = 36;

            InitHunterName();
        }

        public HunterHarpy(Serial serial) : base(serial) { }

        public override void GenerateLoot()
        {
            AddLoot(LootPack.UltraRich, 3);
            PackGold(400, 1000);
        }

        public override void Serialize(GenericWriter writer) { base.Serialize(writer); writer.Write(0); }
        public override void Deserialize(GenericReader reader) { base.Deserialize(reader); reader.ReadInt(); }
    }

    public class HunterEttinWarlord : HunterCreature
    {
        private static readonly string[] Names  = { "Twinrage", "Doublefist", "Gorecrush", "Splitwrath", "Boulderback" };
        private static readonly string   Title_ = "the Ettin Warlord";

        protected override int    HunterTier         => 1;
        protected override string HunterCreatureName => Names[Utility.Random(Names.Length)];
        protected override string HunterTitle        => Title_;

        [Constructable]
        public HunterEttinWarlord() : base(AIType.AI_Melee, FightMode.Closest, 10, 1, 0.2, 0.4)
        {
            Body = 18;
            Hue  = 0x21;

            SetStr(272, 330);
            SetDex(56, 80);
            SetInt(36, 62);
            SetHits(164, 198);
            SetStam(56, 80);
            SetMana(36, 62);

            SetSkill(SkillName.Tactics,     80.0, 95.0);
            SetSkill(SkillName.MagicResist, 80.0, 90.0);
            SetSkill(SkillName.Wrestling,   80.0, 95.0);

            Fame        = 8000;
            Karma       = -8000;
            VirtualArmor = 42;

            InitHunterName();
        }

        public HunterEttinWarlord(Serial serial) : base(serial) { }

        public override void GenerateLoot()
        {
            AddLoot(LootPack.UltraRich, 3);
            PackGold(400, 1000);
        }

        public override void Serialize(GenericWriter writer) { base.Serialize(writer); writer.Write(0); }
        public override void Deserialize(GenericReader reader) { base.Deserialize(reader); reader.ReadInt(); }
    }

    public class HunterLizardShaman : HunterCreature
    {
        private static readonly string[] Names  = { "Sssrak", "Venomtongue", "Scalecaller", "Swamprite", "Poisonscale" };
        private static readonly string   Title_ = "the Lizard Shaman";

        protected override int    HunterTier         => 1;
        protected override string HunterCreatureName => Names[Utility.Random(Names.Length)];
        protected override string HunterTitle        => Title_;

        [Constructable]
        public HunterLizardShaman() : base(AIType.AI_Mage, FightMode.Closest, 10, 1, 0.2, 0.4)
        {
            Body = Utility.RandomList(35, 36);
            Hue  = 0x21;

            SetStr(192, 240);
            SetDex(56, 80);
            SetInt(200, 250);
            SetHits(116, 144);
            SetStam(56, 80);
            SetMana(200, 250);

            SetSkill(SkillName.Tactics,     80.0, 90.0);
            SetSkill(SkillName.MagicResist, 80.0, 95.0);
            SetSkill(SkillName.Magery,      80.0, 95.0);
            SetSkill(SkillName.EvalInt,     80.0, 90.0);

            Fame        = 8000;
            Karma       = -8000;
            VirtualArmor = 32;

            InitHunterName();
        }

        public HunterLizardShaman(Serial serial) : base(serial) { }

        public override void GenerateLoot()
        {
            AddLoot(LootPack.UltraRich, 3);
            PackGold(400, 1000);
            PackItem(new SpidersSilk(Utility.RandomMinMax(30, 50)));
            PackItem(new SulfurousAsh(Utility.RandomMinMax(30, 50)));
        }

        public override void Serialize(GenericWriter writer) { base.Serialize(writer); writer.Write(0); }
        public override void Deserialize(GenericReader reader) { base.Deserialize(reader); reader.ReadInt(); }
    }

    // ============================================================
    // TIER 2 — DUNGEON PREDATORS  (hue 0x47B, 3x hits)
    // ============================================================

    public class HunterDragon : HunterCreature
    {
        private static readonly string[] Names  = { "Vaelthorn", "Emberclaw", "Scorchwing", "Ashfang", "Blazecoil" };
        private static readonly string   Title_ = "the Burning Dragon";

        protected override int    HunterTier         => 2;
        protected override string HunterCreatureName => Names[Utility.Random(Names.Length)];
        protected override string HunterTitle        => Title_;

        [Constructable]
        public HunterDragon() : base(AIType.AI_Mage, FightMode.Closest, 12, 1, 0.1, 0.2)
        {
            Body = Utility.RandomList(12, 59);
            Hue  = 0x47B;

            SetStr(796, 825);
            SetDex(86, 105);
            SetInt(436, 475);
            SetHits(1434, 1485);  // 3x base
            SetStam(86, 105);
            SetMana(436, 475);

            SetSkill(SkillName.Tactics,     100.0, 110.0);
            SetSkill(SkillName.MagicResist, 100.0, 110.0);
            SetSkill(SkillName.Magery,      100.0, 110.0);
            SetSkill(SkillName.EvalInt,     100.0, 110.0);
            SetSkill(SkillName.Wrestling,   100.0, 110.0);

            Fame        = 22500;
            Karma       = -22500;
            VirtualArmor = 72;

            InitHunterName();
        }

        public HunterDragon(Serial serial) : base(serial) { }

        public override void GenerateLoot()
        {
            AddLoot(LootPack.UltraRich, 3);
            AddLoot(LootPack.SuperBoss, 1);
            PackGold(800, 2000);
            for (int i = 0; i < Utility.RandomMinMax(2, 4); i++)
                PackItem(Loot.RandomScroll(6, 7, SpellbookType.Regular));
            PackItem(new MandrakeRoot(Utility.RandomMinMax(50, 80)));
            PackItem(new BlackPearl(Utility.RandomMinMax(50, 80)));
        }

        public override void Serialize(GenericWriter writer) { base.Serialize(writer); writer.Write(0); }
        public override void Deserialize(GenericReader reader) { base.Deserialize(reader); reader.ReadInt(); }
    }

    public class HunterBalron : HunterCreature
    {
        private static readonly string[] Names  = { "Xan", "Mordrath", "Vexar", "Deathlord", "Soultear" };
        private static readonly string   Title_ = "the Fallen Balron";

        protected override int    HunterTier         => 2;
        protected override string HunterCreatureName => Names[Utility.Random(Names.Length)];
        protected override string HunterTitle        => Title_;

        [Constructable]
        public HunterBalron() : base(AIType.AI_Mage, FightMode.Closest, 12, 1, 0.1, 0.2)
        {
            Body = 40;
            Hue  = 0x47B;

            SetStr(986, 1185);
            SetDex(177, 255);
            SetInt(151, 250);
            SetHits(1776, 2133);  // 3x base
            SetStam(177, 255);
            SetMana(151, 250);

            SetSkill(SkillName.Tactics,     100.0, 110.0);
            SetSkill(SkillName.MagicResist, 100.0, 110.0);
            SetSkill(SkillName.Magery,      100.0, 110.0);
            SetSkill(SkillName.EvalInt,     100.0, 110.0);
            SetSkill(SkillName.Wrestling,   100.0, 110.0);

            Fame        = 24000;
            Karma       = -24000;
            VirtualArmor = 80;

            InitHunterName();
        }

        public HunterBalron(Serial serial) : base(serial) { }

        public override void GenerateLoot()
        {
            AddLoot(LootPack.UltraRich, 3);
            AddLoot(LootPack.SuperBoss, 1);
            PackGold(800, 2000);
            for (int i = 0; i < Utility.RandomMinMax(2, 4); i++)
                PackItem(Loot.RandomScroll(6, 7, SpellbookType.Regular));
        }

        public override void Serialize(GenericWriter writer) { base.Serialize(writer); writer.Write(0); }
        public override void Deserialize(GenericReader reader) { base.Deserialize(reader); reader.ReadInt(); }
    }

    public class HunterLichLord : HunterCreature
    {
        private static readonly string[] Names  = { "Ossivane", "Deathwhisper", "Bonecall", "Necroveil", "Grimspell" };
        private static readonly string   Title_ = "the Lich Eternal";

        protected override int    HunterTier         => 2;
        protected override string HunterCreatureName => Names[Utility.Random(Names.Length)];
        protected override string HunterTitle        => Title_;

        [Constructable]
        public HunterLichLord() : base(AIType.AI_Mage, FightMode.Closest, 12, 1, 0.1, 0.2)
        {
            Body = 79;
            Hue  = 0x47B;

            SetStr(416, 505);
            SetDex(146, 165);
            SetInt(566, 655);
            SetHits(750, 909);   // 3x base
            SetStam(146, 165);
            SetMana(566, 655);

            SetSkill(SkillName.Tactics,     100.0, 110.0);
            SetSkill(SkillName.MagicResist, 100.0, 110.0);
            SetSkill(SkillName.Magery,      100.0, 110.0);
            SetSkill(SkillName.EvalInt,     100.0, 110.0);
            SetSkill(SkillName.Necromancy,  100.0, 110.0);
            SetSkill(SkillName.SpiritSpeak, 100.0, 110.0);

            Fame        = 18000;
            Karma       = -18000;
            VirtualArmor = 60;

            InitHunterName();
        }

        public HunterLichLord(Serial serial) : base(serial) { }

        public override void GenerateLoot()
        {
            AddLoot(LootPack.UltraRich, 3);
            AddLoot(LootPack.SuperBoss, 1);
            PackGold(800, 2000);
            for (int i = 0; i < Utility.RandomMinMax(2, 4); i++)
                PackItem(Loot.RandomScroll(6, 7, SpellbookType.Regular));
            PackItem(new BatWing(Utility.RandomMinMax(50, 80)));
            PackItem(new NoxCrystal(Utility.RandomMinMax(50, 80)));
        }

        public override void Serialize(GenericWriter writer) { base.Serialize(writer); writer.Write(0); }
        public override void Deserialize(GenericReader reader) { base.Deserialize(reader); reader.ReadInt(); }
    }

    public class HunterDaemon : HunterCreature
    {
        private static readonly string[] Names  = { "Pyraxis", "Hellvore", "Cinderhorn", "Shadowbrand", "Voidclaw" };
        private static readonly string   Title_ = "the Abyssal Daemon";

        protected override int    HunterTier         => 2;
        protected override string HunterCreatureName => Names[Utility.Random(Names.Length)];
        protected override string HunterTitle        => Title_;

        [Constructable]
        public HunterDaemon() : base(AIType.AI_Mage, FightMode.Closest, 12, 1, 0.1, 0.2)
        {
            Body = 9;
            Hue  = 0x47B;

            SetStr(476, 505);
            SetDex(76, 95);
            SetInt(301, 325);
            SetHits(858, 909);   // 3x base
            SetStam(76, 95);
            SetMana(301, 325);

            SetSkill(SkillName.Tactics,     100.0, 110.0);
            SetSkill(SkillName.MagicResist, 100.0, 110.0);
            SetSkill(SkillName.Magery,      100.0, 110.0);
            SetSkill(SkillName.EvalInt,     100.0, 110.0);
            SetSkill(SkillName.Wrestling,   100.0, 110.0);

            Fame        = 15000;
            Karma       = -15000;
            VirtualArmor = 64;

            InitHunterName();
        }

        public HunterDaemon(Serial serial) : base(serial) { }

        public override void GenerateLoot()
        {
            AddLoot(LootPack.UltraRich, 3);
            AddLoot(LootPack.SuperBoss, 1);
            PackGold(800, 2000);
            for (int i = 0; i < Utility.RandomMinMax(2, 4); i++)
                PackItem(Loot.RandomScroll(6, 7, SpellbookType.Regular));
        }

        public override void Serialize(GenericWriter writer) { base.Serialize(writer); writer.Write(0); }
        public override void Deserialize(GenericReader reader) { base.Deserialize(reader); reader.ReadInt(); }
    }

    // ============================================================
    // TIER 3 — ANCIENT & LEGENDARY  (hue 0x4A0, 4x hits)
    // ============================================================

    public class HunterAncientWyrm : HunterCreature
    {
        private static readonly string[] Names  = { "Atherix", "Worldrend", "Stonegale", "Coilbane", "Doombreath" };
        private static readonly string   Title_ = "the Ancient Wyrm";

        protected override int    HunterTier         => 3;
        protected override string HunterCreatureName => Names[Utility.Random(Names.Length)];
        protected override string HunterTitle        => Title_;

        [Constructable]
        public HunterAncientWyrm() : base(AIType.AI_Mage, FightMode.Closest, 14, 1, 0.1, 0.2)
        {
            Body = 46;
            Hue  = 0x4A0;

            SetStr(1096, 1185);
            SetDex(86, 175);
            SetInt(686, 775);
            SetHits(2632, 2844);  // 4x base
            SetStam(86, 175);
            SetMana(686, 775);

            SetSkill(SkillName.Tactics,     110.0, 120.0);
            SetSkill(SkillName.MagicResist, 110.0, 120.0);
            SetSkill(SkillName.Magery,      110.0, 120.0);
            SetSkill(SkillName.EvalInt,     110.0, 120.0);
            SetSkill(SkillName.Wrestling,   110.0, 120.0);

            Fame        = 22500;
            Karma       = -22500;
            VirtualArmor = 85;

            InitHunterName();
            StartPresenceShouts();
        }

        public HunterAncientWyrm(Serial serial) : base(serial) { }

        public override void GenerateLoot()
        {
            AddLoot(LootPack.SuperBoss, 3);
            PackGold(1500, 4000);
            for (int i = 0; i < Utility.RandomMinMax(3, 5); i++)
                PackItem(Loot.RandomScroll(6, 7, SpellbookType.Regular));
            PackItem(new GreaterHealPotion());
            PackItem(new GreaterHealPotion());
            PackItem(new GreaterHealPotion());
            PackItem(new GreaterCurePotion());
            PackItem(new GreaterCurePotion());
        }

        public override void Serialize(GenericWriter writer) { base.Serialize(writer); writer.Write(0); }
        public override void Deserialize(GenericReader reader) { base.Deserialize(reader); reader.ReadInt(); }
    }

    public class HunterPrimevalLich : HunterCreature
    {
        private static readonly string[] Names  = { "Morvain", "Ageblight", "Soulcrumble", "Voidwhisper", "Deathborn" };
        private static readonly string   Title_ = "the Undying Lich";

        protected override int    HunterTier         => 3;
        protected override string HunterCreatureName => Names[Utility.Random(Names.Length)];
        protected override string HunterTitle        => Title_;

        [Constructable]
        public HunterPrimevalLich() : base(AIType.AI_Mage, FightMode.Closest, 14, 1, 0.1, 0.2)
        {
            Body = 830;
            Hue  = 0x4A0;

            SetStr(500);
            SetDex(80, 120);
            SetInt(800, 900);
            SetHits(120000);     // 4x base (PrimevalLich = 30000)
            SetStam(80, 120);
            SetMana(800, 900);

            SetSkill(SkillName.Tactics,     110.0, 120.0);
            SetSkill(SkillName.MagicResist, 110.0, 120.0);
            SetSkill(SkillName.Magery,      110.0, 120.0);
            SetSkill(SkillName.EvalInt,     110.0, 120.0);
            SetSkill(SkillName.Necromancy,  110.0, 120.0);
            SetSkill(SkillName.SpiritSpeak, 110.0, 120.0);

            Fame        = 28000;
            Karma       = -28000;
            VirtualArmor = 90;

            InitHunterName();
            StartPresenceShouts();
        }

        public HunterPrimevalLich(Serial serial) : base(serial) { }

        public override void GenerateLoot()
        {
            AddLoot(LootPack.SuperBoss, 3);
            PackGold(1500, 4000);
            for (int i = 0; i < Utility.RandomMinMax(3, 5); i++)
                PackItem(Loot.RandomScroll(6, 7, SpellbookType.Regular));
            PackItem(new GreaterHealPotion());
            PackItem(new GreaterCurePotion());
            PackItem(new BatWing(Utility.RandomMinMax(80, 100)));
            PackItem(new NoxCrystal(Utility.RandomMinMax(80, 100)));
        }

        public override void Serialize(GenericWriter writer) { base.Serialize(writer); writer.Write(0); }
        public override void Deserialize(GenericReader reader) { base.Deserialize(reader); reader.ReadInt(); }
    }

    // HunterJuonar — undead warlord variant (extends LichLord stat profile)
    public class HunterUndeadLord : HunterCreature
    {
        private static readonly string[] Names  = { "Juonar", "Gravecrown", "Soulbane", "Deathrend", "Voidlord" };
        private static readonly string   Title_ = "the Undead Warlord";

        protected override int    HunterTier         => 3;
        protected override string HunterCreatureName => Names[Utility.Random(Names.Length)];
        protected override string HunterTitle        => Title_;

        [Constructable]
        public HunterUndeadLord() : base(AIType.AI_Mage, FightMode.Closest, 14, 1, 0.1, 0.2)
        {
            Body = 79;
            Hue  = 0x4A0;

            SetStr(832, 1010);
            SetDex(146, 165);
            SetInt(1132, 1310);
            SetHits(1000, 1212);  // 4x LichLord base
            SetStam(146, 165);
            SetMana(1132, 1310);

            SetSkill(SkillName.Tactics,     110.0, 120.0);
            SetSkill(SkillName.MagicResist, 110.0, 120.0);
            SetSkill(SkillName.Magery,      110.0, 120.0);
            SetSkill(SkillName.EvalInt,     110.0, 120.0);
            SetSkill(SkillName.Necromancy,  110.0, 120.0);
            SetSkill(SkillName.SpiritSpeak, 110.0, 120.0);
            SetSkill(SkillName.Swords,      110.0, 120.0);

            Fame        = 22000;
            Karma       = -22000;
            VirtualArmor = 80;

            InitHunterName();
            StartPresenceShouts();
        }

        public HunterUndeadLord(Serial serial) : base(serial) { }

        public override void GenerateLoot()
        {
            AddLoot(LootPack.SuperBoss, 3);
            PackGold(1500, 4000);
            for (int i = 0; i < Utility.RandomMinMax(3, 5); i++)
                PackItem(Loot.RandomScroll(6, 7, SpellbookType.Regular));
            PackItem(new GreaterHealPotion());
            PackItem(new GreaterCurePotion());
        }

        public override void Serialize(GenericWriter writer) { base.Serialize(writer); writer.Write(0); }
        public override void Deserialize(GenericReader reader) { base.Deserialize(reader); reader.ReadInt(); }
    }

    public class HunterBloodDragon : HunterCreature
    {
        private static readonly string[] Names  = { "Bloodfang", "Crimsonwing", "Gorewyrm", "Scarlettooth", "Redcoil" };
        private static readonly string   Title_ = "the Blood Dragon";

        protected override int    HunterTier         => 3;
        protected override string HunterCreatureName => Names[Utility.Random(Names.Length)];
        protected override string HunterTitle        => Title_;

        [Constructable]
        public HunterBloodDragon() : base(AIType.AI_Mage, FightMode.Closest, 14, 1, 0.1, 0.2)
        {
            Body = 12;
            Hue  = 0x4A0;  // Tier 3 hue (blood red variant uses body 12, not hue override)

            SetStr(1592, 1650);
            SetDex(86, 105);
            SetInt(436, 475);
            SetHits(1912, 1980);  // 4x Dragon base
            SetStam(86, 105);
            SetMana(436, 475);

            SetSkill(SkillName.Tactics,     110.0, 120.0);
            SetSkill(SkillName.MagicResist, 110.0, 120.0);
            SetSkill(SkillName.Magery,      110.0, 120.0);
            SetSkill(SkillName.EvalInt,     110.0, 120.0);
            SetSkill(SkillName.Wrestling,   110.0, 120.0);

            Fame        = 22500;
            Karma       = -22500;
            VirtualArmor = 85;

            InitHunterName();
            StartPresenceShouts();
        }

        public HunterBloodDragon(Serial serial) : base(serial) { }

        public override void GenerateLoot()
        {
            AddLoot(LootPack.SuperBoss, 3);
            PackGold(1500, 4000);
            for (int i = 0; i < Utility.RandomMinMax(3, 5); i++)
                PackItem(Loot.RandomScroll(6, 7, SpellbookType.Regular));
            PackItem(new GreaterHealPotion());
            PackItem(new GreaterHealPotion());
            PackItem(new GreaterCurePotion());
        }

        public override void Serialize(GenericWriter writer) { base.Serialize(writer); writer.Write(0); }
        public override void Deserialize(GenericReader reader) { base.Deserialize(reader); reader.ReadInt(); }
    }

    public class HunterAbyssalDaemon : HunterCreature
    {
        private static readonly string[] Names  = { "Voidsunder", "Hellrend", "Abysshorn", "Darkshatter", "Riftclaw" };
        private static readonly string   Title_ = "the Abyssal Daemon";

        protected override int    HunterTier         => 3;
        protected override string HunterCreatureName => Names[Utility.Random(Names.Length)];
        protected override string HunterTitle        => Title_;

        [Constructable]
        public HunterAbyssalDaemon() : base(AIType.AI_Mage, FightMode.Closest, 14, 1, 0.1, 0.2)
        {
            Body = 9;
            Hue  = 0x4A0;

            SetStr(952, 1010);
            SetDex(76, 95);
            SetInt(602, 650);
            SetHits(1144, 1212);  // 4x Balron-scale
            SetStam(76, 95);
            SetMana(602, 650);

            SetSkill(SkillName.Tactics,     110.0, 120.0);
            SetSkill(SkillName.MagicResist, 110.0, 120.0);
            SetSkill(SkillName.Magery,      110.0, 120.0);
            SetSkill(SkillName.EvalInt,     110.0, 120.0);
            SetSkill(SkillName.Wrestling,   110.0, 120.0);

            Fame        = 22000;
            Karma       = -22000;
            VirtualArmor = 80;

            InitHunterName();
            StartPresenceShouts();
        }

        public HunterAbyssalDaemon(Serial serial) : base(serial) { }

        public override void GenerateLoot()
        {
            AddLoot(LootPack.SuperBoss, 3);
            PackGold(1500, 4000);
            for (int i = 0; i < Utility.RandomMinMax(3, 5); i++)
                PackItem(Loot.RandomScroll(6, 7, SpellbookType.Regular));
            PackItem(new GreaterHealPotion());
            PackItem(new GreaterCurePotion());
        }

        public override void Serialize(GenericWriter writer) { base.Serialize(writer); writer.Write(0); }
        public override void Deserialize(GenericReader reader) { base.Deserialize(reader); reader.ReadInt(); }
    }

    // ============================================================
    // TIER 4 — CHAMPION ROAMERS  (hue 0x497, 5x hits)
    // ============================================================

    public class HunterRikktor : HunterCreature
    {
        protected override int    HunterTier         => 4;
        protected override string HunterCreatureName => "Rikktor";
        protected override string HunterTitle        => "the Undying";

        [Constructable]
        public HunterRikktor() : base(AIType.AI_Mage, FightMode.Closest, 14, 1, 0.1, 0.2)
        {
            Body = 172;
            Hue  = 0x497;

            SetStr(701, 900);
            SetDex(201, 350);
            SetInt(51, 100);
            SetHits(75000);       // 5x Rikktor (15000)
            SetStam(201, 350);
            SetMana(51, 100);

            SetSkill(SkillName.Tactics,     120.0);
            SetSkill(SkillName.MagicResist, 120.0);
            SetSkill(SkillName.Wrestling,   120.0);
            SetSkill(SkillName.Anatomy,     120.0);

            Fame        = 22500;
            Karma       = -22500;
            VirtualArmor = 100;

            InitHunterName();
            StartPresenceShouts();
        }

        public HunterRikktor(Serial serial) : base(serial) { }

        protected override Item GenerateTier4Artifact() => new RikktorScaleShield();

        public override void GenerateLoot()
        {
            AddLoot(LootPack.SuperBoss, 4);
            PackGold(3000, 8000);
            for (int i = 0; i < Utility.RandomMinMax(4, 6); i++)
                PackItem(Loot.RandomScroll(6, 7, SpellbookType.Regular));
            PackItem(new GreaterHealPotion());
            PackItem(new GreaterHealPotion());
            PackItem(new GreaterHealPotion());
            PackItem(new GreaterHealPotion());
            PackItem(new GreaterHealPotion());
            PackItem(new GreaterCurePotion());
            PackItem(new GreaterCurePotion());
            PackItem(new GreaterCurePotion());
            PackItem(new ExplosionPotion());
            PackItem(new ExplosionPotion());
        }

        public override void Serialize(GenericWriter writer) { base.Serialize(writer); writer.Write(0); }
        public override void Deserialize(GenericReader reader) { base.Deserialize(reader); reader.ReadInt(); }
    }

    public class HunterBarracoon : HunterCreature
    {
        protected override int    HunterTier         => 4;
        protected override string HunterCreatureName => "Barracoon";
        protected override string HunterTitle        => "the Eternal Piper";

        [Constructable]
        public HunterBarracoon() : base(AIType.AI_Mage, FightMode.Closest, 14, 1, 0.1, 0.2)
        {
            Body = 0x190;
            Hue  = 0x497;

            SetStr(283, 425);
            SetDex(72, 150);
            SetInt(505, 750);
            SetHits(60000);       // 5x Barracoon (12000)
            SetStam(72, 150);
            SetMana(505, 750);

            SetSkill(SkillName.Tactics,     120.0);
            SetSkill(SkillName.MagicResist, 120.0);
            SetSkill(SkillName.Magery,      120.0);
            SetSkill(SkillName.EvalInt,     120.0);
            SetSkill(SkillName.Anatomy,     120.0);

            Fame        = 22500;
            Karma       = -22500;
            VirtualArmor = 100;

            InitHunterName();
            StartPresenceShouts();
        }

        public HunterBarracoon(Serial serial) : base(serial) { }

        protected override Item GenerateTier4Artifact() => new BarracoonPipe();

        public override void GenerateLoot()
        {
            AddLoot(LootPack.SuperBoss, 4);
            PackGold(3000, 8000);
            for (int i = 0; i < Utility.RandomMinMax(4, 6); i++)
                PackItem(Loot.RandomScroll(6, 7, SpellbookType.Regular));
            PackItem(new GreaterHealPotion());
            PackItem(new GreaterHealPotion());
            PackItem(new GreaterHealPotion());
            PackItem(new GreaterHealPotion());
            PackItem(new GreaterCurePotion());
            PackItem(new GreaterCurePotion());
            PackItem(new GreaterCurePotion());
            PackItem(new ExplosionPotion());
            PackItem(new ExplosionPotion());
        }

        public override void Serialize(GenericWriter writer) { base.Serialize(writer); writer.Write(0); }
        public override void Deserialize(GenericReader reader) { base.Deserialize(reader); reader.ReadInt(); }
    }

    public class HunterNeira : HunterCreature
    {
        protected override int    HunterTier         => 4;
        protected override string HunterCreatureName => "Neira";
        protected override string HunterTitle        => "the Deathless";

        [Constructable]
        public HunterNeira() : base(AIType.AI_Mage, FightMode.Closest, 14, 1, 0.1, 0.2)
        {
            Body = 401;
            Hue  = 0x497;

            SetStr(305, 425);
            SetDex(72, 150);
            SetInt(505, 750);
            SetHits(24000);       // 5x Neira (4800)
            SetStam(72, 150);
            SetMana(505, 750);

            SetSkill(SkillName.Tactics,     120.0);
            SetSkill(SkillName.MagicResist, 120.0);
            SetSkill(SkillName.Magery,      120.0);
            SetSkill(SkillName.EvalInt,     120.0);
            SetSkill(SkillName.Necromancy,  120.0);
            SetSkill(SkillName.SpiritSpeak, 120.0);

            Fame        = 22500;
            Karma       = -22500;
            VirtualArmor = 100;

            InitHunterName();
            StartPresenceShouts();
        }

        public HunterNeira(Serial serial) : base(serial) { }

        protected override Item GenerateTier4Artifact() => new NeirasDeathShroud();

        public override void GenerateLoot()
        {
            AddLoot(LootPack.SuperBoss, 4);
            PackGold(3000, 8000);
            for (int i = 0; i < Utility.RandomMinMax(4, 6); i++)
                PackItem(Loot.RandomScroll(6, 7, SpellbookType.Regular));
            PackItem(new GreaterHealPotion());
            PackItem(new GreaterHealPotion());
            PackItem(new GreaterHealPotion());
            PackItem(new GreaterHealPotion());
            PackItem(new GreaterCurePotion());
            PackItem(new GreaterCurePotion());
            PackItem(new GreaterCurePotion());
            PackItem(new ExplosionPotion());
            PackItem(new ExplosionPotion());
        }

        public override void Serialize(GenericWriter writer) { base.Serialize(writer); writer.Write(0); }
        public override void Deserialize(GenericReader reader) { base.Deserialize(reader); reader.ReadInt(); }
    }

    public class HunterMephitis : HunterCreature
    {
        protected override int    HunterTier         => 4;
        protected override string HunterCreatureName => "Mephitis";
        protected override string HunterTitle        => "the Venomborn";

        [Constructable]
        public HunterMephitis() : base(AIType.AI_Mage, FightMode.Closest, 14, 1, 0.1, 0.2)
        {
            Body = 173;
            Hue  = 0x497;

            SetStr(505, 1000);
            SetDex(102, 300);
            SetInt(402, 600);
            SetHits(60000);       // 5x Mephitis (12000)
            SetStam(102, 300);
            SetMana(402, 600);

            SetSkill(SkillName.Tactics,     120.0);
            SetSkill(SkillName.MagicResist, 120.0);
            SetSkill(SkillName.Magery,      120.0);
            SetSkill(SkillName.EvalInt,     120.0);
            SetSkill(SkillName.Poisoning,   120.0);

            Fame        = 22500;
            Karma       = -22500;
            VirtualArmor = 100;

            InitHunterName();
            StartPresenceShouts();
        }

        public HunterMephitis(Serial serial) : base(serial) { }

        protected override Item GenerateTier4Artifact() => new MephitisFang();

        public override void GenerateLoot()
        {
            AddLoot(LootPack.SuperBoss, 4);
            PackGold(3000, 8000);
            for (int i = 0; i < Utility.RandomMinMax(4, 6); i++)
                PackItem(Loot.RandomScroll(6, 7, SpellbookType.Regular));
            PackItem(new GreaterHealPotion());
            PackItem(new GreaterHealPotion());
            PackItem(new GreaterHealPotion());
            PackItem(new GreaterHealPotion());
            PackItem(new GreaterCurePotion());
            PackItem(new GreaterCurePotion());
            PackItem(new GreaterCurePotion());
            PackItem(new ExplosionPotion());
            PackItem(new ExplosionPotion());
        }

        public override void Serialize(GenericWriter writer) { base.Serialize(writer); writer.Write(0); }
        public override void Deserialize(GenericReader reader) { base.Deserialize(reader); reader.ReadInt(); }
    }

    public class HunterSemidar : HunterCreature
    {
        protected override int    HunterTier         => 4;
        protected override string HunterCreatureName => "Semidar";
        protected override string HunterTitle        => "the Eternal";

        [Constructable]
        public HunterSemidar() : base(AIType.AI_Mage, FightMode.Closest, 14, 1, 0.1, 0.2)
        {
            Body = 174;
            Hue  = 0x497;

            SetStr(502, 600);
            SetDex(102, 200);
            SetInt(601, 750);
            SetHits(50000);       // 5x Semidar (10000)
            SetStam(102, 200);
            SetMana(601, 750);

            SetSkill(SkillName.Tactics,     120.0);
            SetSkill(SkillName.MagicResist, 120.0);
            SetSkill(SkillName.Magery,      120.0);
            SetSkill(SkillName.EvalInt,     120.0);

            Fame        = 24000;
            Karma       = -24000;
            VirtualArmor = 100;

            InitHunterName();
            StartPresenceShouts();
        }

        public HunterSemidar(Serial serial) : base(serial) { }

        protected override Item GenerateTier4Artifact() => new SemidarBinding();

        public override void GenerateLoot()
        {
            AddLoot(LootPack.SuperBoss, 4);
            PackGold(3000, 8000);
            for (int i = 0; i < Utility.RandomMinMax(4, 6); i++)
                PackItem(Loot.RandomScroll(6, 7, SpellbookType.Regular));
            PackItem(new GreaterHealPotion());
            PackItem(new GreaterHealPotion());
            PackItem(new GreaterHealPotion());
            PackItem(new GreaterHealPotion());
            PackItem(new GreaterCurePotion());
            PackItem(new GreaterCurePotion());
            PackItem(new GreaterCurePotion());
            PackItem(new ExplosionPotion());
            PackItem(new ExplosionPotion());
        }

        public override void Serialize(GenericWriter writer) { base.Serialize(writer); writer.Write(0); }
        public override void Deserialize(GenericReader reader) { base.Deserialize(reader); reader.ReadInt(); }
    }

    public class HunterLordOaks : HunterCreature
    {
        protected override int    HunterTier         => 4;
        protected override string HunterCreatureName => "Lord Oaks";
        protected override string HunterTitle        => "the Worldroot";

        [Constructable]
        public HunterLordOaks() : base(AIType.AI_Mage, FightMode.Closest, 14, 1, 0.1, 0.2)
        {
            Body = 175;
            Hue  = 0x497;

            SetStr(403, 850);
            SetDex(101, 150);
            SetInt(503, 800);
            SetHits(60000);       // 5x LordOaks (12000)
            SetStam(101, 150);
            SetMana(503, 800);

            SetSkill(SkillName.Tactics,     120.0);
            SetSkill(SkillName.MagicResist, 120.0);
            SetSkill(SkillName.Magery,      120.0);
            SetSkill(SkillName.EvalInt,     120.0);

            Fame        = 22500;
            Karma       = -22500;
            VirtualArmor = 100;

            InitHunterName();
            StartPresenceShouts();
        }

        public HunterLordOaks(Serial serial) : base(serial) { }

        protected override Item GenerateTier4Artifact() => new OaksBarkTalisman();

        public override void GenerateLoot()
        {
            AddLoot(LootPack.SuperBoss, 4);
            PackGold(3000, 8000);
            for (int i = 0; i < Utility.RandomMinMax(4, 6); i++)
                PackItem(Loot.RandomScroll(6, 7, SpellbookType.Regular));
            PackItem(new GreaterHealPotion());
            PackItem(new GreaterHealPotion());
            PackItem(new GreaterHealPotion());
            PackItem(new GreaterHealPotion());
            PackItem(new GreaterCurePotion());
            PackItem(new GreaterCurePotion());
            PackItem(new GreaterCurePotion());
            PackItem(new ExplosionPotion());
            PackItem(new ExplosionPotion());
        }

        public override void Serialize(GenericWriter writer) { base.Serialize(writer); writer.Write(0); }
        public override void Deserialize(GenericReader reader) { base.Deserialize(reader); reader.ReadInt(); }
    }
}
