// ============================================================
// TravelingRareMerchant.cs
// Scripts/Custom/TravelingRareMerchant.cs
//
// A mysterious merchant who appears at a random town bank for
// 30 minutes before moving on.
//
// - Black robe with gold trim
// - Accepts only Merchant Coins
// - Carries 10 randomly selected top-tier items
// - Items and location refresh each visit
// - Gump shows item icons with hover tooltips + confirmation
// ============================================================

using System;
using System.Collections.Generic;
using Server;
using Server.Commands;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Server.Custom
{
    // ── Stock entry definition ────────────────────────────────────────────────

    public class RareMerchantEntry
    {
        public readonly string    Name;
        public readonly int       Cost;
        public readonly Func<Item> Create;

        public RareMerchantEntry(string name, int cost, Func<Item> create)
        {
            Name   = name;
            Cost   = cost;
            Create = create;
        }
    }

    // ── Live stock slot (actual item instance stored on Map.Internal) ─────────

    public class RareMerchantSlot
    {
        public Item   Item;
        public int    Cost;
        public string Name;
        public readonly Func<Item> Factory; // used to create the sold copy

        public RareMerchantSlot(Item item, int cost, string name, Func<Item> factory)
        {
            Item    = item;
            Cost    = cost;
            Name    = name;
            Factory = factory;
        }
    }

    // ── Pool definition ───────────────────────────────────────────────────────

    public static class RareMerchantStock
    {
        public static readonly List<RareMerchantEntry> Pool = new List<RareMerchantEntry>
        {
            // ── Armor ──────────────────────────────────────────────────────
            new RareMerchantEntry("Aegis",                       80,  () => new Aegis()),
            new RareMerchantEntry("Arcane Shield",               65,  () => new ArcaneShield()),
            new RareMerchantEntry("Armor of Fortune",            80,  () => new ArmorOfFortune()),
            new RareMerchantEntry("Gauntlets of Nobility",       55,  () => new GauntletsOfNobility()),
            new RareMerchantEntry("Helm of Insight",             65,  () => new HelmOfInsight()),
            new RareMerchantEntry("Holy Knight's Breastplate",   90,  () => new HolyKnightsBreastplate()),
            new RareMerchantEntry("Inquisitor's Resolution",     80,  () => new InquisitorsResolution()),
            new RareMerchantEntry("Jackal's Collar",             65,  () => new JackalsCollar()),
            new RareMerchantEntry("Leggings of Bane",            75,  () => new LeggingsOfBane()),
            new RareMerchantEntry("Light's Rampart",             85,  () => new LightsRampart()),
            new RareMerchantEntry("Lord Blackthorn's Exemplar",  100, () => new LordBlackthornsExemplar()),
            new RareMerchantEntry("Midnight Bracers",            55,  () => new MidnightBracers()),
            new RareMerchantEntry("Mystic's Guard",              75,  () => new MysticsGuard()),
            new RareMerchantEntry("Ornate Crown of the Harrower",150, () => new OrnateCrownOfTheHarrower()),
            new RareMerchantEntry("Sentinel's Guard",            80,  () => new SentinelsGuard()),
            new RareMerchantEntry("Shadow Dancer Leggings",      95,  () => new ShadowDancerLeggings()),
            new RareMerchantEntry("Tunic of Fire",               85,  () => new TunicOfFire()),
            new RareMerchantEntry("Voice of the Fallen King",    100, () => new VoiceOfTheFallenKing()),

            // ── Clothing ──────────────────────────────────────────────────
            new RareMerchantEntry("Divine Countenance",          80,  () => new DivineCountenance()),
            new RareMerchantEntry("Hat of the Magi",             100, () => new HatOfTheMagi()),
            new RareMerchantEntry("Hunter's Headdress",          85,  () => new HuntersHeaddress()),
            new RareMerchantEntry("Spirit of the Totem",         90,  () => new SpiritOfTheTotem()),
            new RareMerchantEntry("Hooded Shroud of Shadows",    80,  () => new HoodedShroudOfShadows()),
            new RareMerchantEntry("Crimson Cincture",            75,  () => new CrimsonCincture()),
            new RareMerchantEntry("Robe of the Eclipse",         60,  () => new RobeOfTheEclipse()),
            new RareMerchantEntry("Sash of Might",               55,  () => new SashOfMight()),
            new RareMerchantEntry("Runed Sash of Warding",       70,  () => new RunedSashOfWarding()),
            new RareMerchantEntry("Pads of the Cu Sidhe",        100, () => new PadsOfTheCuSidhe()),
            new RareMerchantEntry("Embroidered Oak Leaf Cloak",  75,  () => new EmbroideredOakLeafCloak()),
            new RareMerchantEntry("Cloak of Death",              70,  () => new CloakOfDeath()),

            // ── Jewelry ───────────────────────────────────────────────────
            new RareMerchantEntry("Crystalline Ring",            175, () => new CrystallineRing()),
            new RareMerchantEntry("Bracelet of Health",          80,  () => new BraceletOfHealth()),
            new RareMerchantEntry("Ornament of the Magician",    90,  () => new OrnamentOfTheMagician()),
            new RareMerchantEntry("Ring of the Elements",        85,  () => new RingOfTheElements()),
            new RareMerchantEntry("Ring of the Vile",            100, () => new RingOfTheVile()),
            new RareMerchantEntry("Ring of the Savant",          95,  () => new RingOfTheSavant()),
            new RareMerchantEntry("Pendant of the Magi",         90,  () => new PendantOfTheMagi()),
            new RareMerchantEntry("Burning Amber",               70,  () => new BurningAmber()),
            new RareMerchantEntry("Lavaliere",                   60,  () => new Lavaliere()),
            new RareMerchantEntry("Torc of the Guardians",       75,  () => new TorcOfTheGuardians()),
            new RareMerchantEntry("Mace and Shield Glasses",     90,  () => new MaceAndShieldGlasses()),
            new RareMerchantEntry("Night Eyes",                  80,  () => new NightEyes()),

            // ── Weapons ───────────────────────────────────────────────────
            new RareMerchantEntry("Axe of the Heavens",          100, () => new AxeOfTheHeavens()),
            new RareMerchantEntry("Blade of Insanity",           120, () => new BladeOfInsanity()),
            new RareMerchantEntry("Blade of the Righteous",      90,  () => new BladeOfTheRighteous()),
            new RareMerchantEntry("Bone Crusher",                80,  () => new BoneCrusher()),
            new RareMerchantEntry("Breath of the Dead",          100, () => new BreathOfTheDead()),
            new RareMerchantEntry("Dragon's End",                75,  () => new DragonsEnd()),
            new RareMerchantEntry("Frostbringer",                90,  () => new Frostbringer()),
            new RareMerchantEntry("Jaana's Staff",               80,  () => new JaanasStaff()),
            new RareMerchantEntry("Katrina's Crook",             75,  () => new KatrinasCrook()),
            new RareMerchantEntry("Legacy of the Dread Lord",    100, () => new LegacyOfTheDreadLord()),
            new RareMerchantEntry("Light in the Void",           110, () => new LightInTheVoid()),
            new RareMerchantEntry("Serpent's Fang",              90,  () => new SerpentsFang()),
            new RareMerchantEntry("Staff of Resonance",          75,  () => new StaffOfResonance()),
            new RareMerchantEntry("Staff of Shattered Dreams",   90,  () => new StaffOfShatteredDreams()),
            new RareMerchantEntry("Staff of the Magi",           100, () => new StaffOfTheMagi()),
            new RareMerchantEntry("Sword of Shattered Hopes",    100, () => new SwordOfShatteredHopes()),
            new RareMerchantEntry("The Berserker's Maul",        120, () => new TheBeserkersMaul()),
            new RareMerchantEntry("The Dragon Slayer",           100, () => new TheDragonSlayer()),
            new RareMerchantEntry("The Dryad Bow",               110, () => new TheDryadBow()),
            new RareMerchantEntry("The Redeemer",                100, () => new TheRedeemer()),
            new RareMerchantEntry("The Taskmaster",              80,  () => new TheTaskmaster()),
            new RareMerchantEntry("Titan's Hammer",              110, () => new TitansHammer()),
            new RareMerchantEntry("Valkyrie's Glaive",           90,  () => new ValkyriesGlaive()),
            new RareMerchantEntry("Zyronic Claw",                120, () => new ZyronicClaw()),
            new RareMerchantEntry("The Dragon's Tail",           75,  () => new TheDragonsTail()),
            new RareMerchantEntry("Rune Blade of Knowledge",     80,  () => new RuneBladeOfKnowledge()),
            new RareMerchantEntry("Soul Seeker",                 110, () => new SoulSeeker()),
            new RareMerchantEntry("Wrath of the Dryad",          90,  () => new WrathOfTheDryad()),
            new RareMerchantEntry("Knight's War Cleaver",        85,  () => new KnightsWarCleaver()),
            new RareMerchantEntry("Night's Kiss",                100, () => new NightsKiss()),

            // ── TOT / Special ─────────────────────────────────────────────
            new RareMerchantEntry("Rune Beetle Carapace",        175, () => new RuneBeetleCarapace()),
            new RareMerchantEntry("Darkened Sky",                150, () => new DarkenedSky()),
            new RareMerchantEntry("Kasa of the Rajin",           100, () => new KasaOfTheRajin()),
            new RareMerchantEntry("Stormgrip",                   120, () => new Stormgrip()),
            new RareMerchantEntry("Sword of the Stampede",       130, () => new SwordOfTheStampede()),
            new RareMerchantEntry("Tome of Lost Knowledge",      150, () => new TomeOfLostKnowledge()),
            new RareMerchantEntry("Wind's Edge",                 140, () => new WindsEdge()),
            new RareMerchantEntry("Primer on Arms",              80,  () => new PrimerOnArmsTalisman()),

            // ── Orbs (random category assigned on creation) ───────────────
            new RareMerchantEntry("Orb of Enhancement",          40,  () => new OrbOfEnhancement()),
            new RareMerchantEntry("Orb of Mastery",              55,  () => new OrbOfMastery()),
            new RareMerchantEntry("Orb of Expansion",            35,  () => new OrbOfExpansion()),
            new RareMerchantEntry("Orb of Fortitude",            35,  () => new OrbOfFortitude()),
            new RareMerchantEntry("Orb of Alacrity",             60,  () => new OrbOfAlacrity()),
            new RareMerchantEntry("Orb of Insight",              40,  () => new OrbOfInsight()),
            new RareMerchantEntry("Orb of Balance",              45,  () => new OrbOfBalance()),
            new RareMerchantEntry("Orb of Corruption",           30,  () => new OrbOfCorruption()),
            new RareMerchantEntry("Orb of Resonance",            35,  () => new OrbOfResonance()),
            new RareMerchantEntry("Orb of Cleansing",            40,  () => new OrbOfCleansing()),
            new RareMerchantEntry("Orb of Tempering",            45,  () => new OrbOfTempering()),
            new RareMerchantEntry("Orb of Enchantment",          50,  () => new OrbOfEnchantment()),
            new RareMerchantEntry("Orb of Reforging",            55,  () => new OrbOfReforging()),

            // ── Power Scrolls — combat ────────────────────────────────────
            new RareMerchantEntry("PS +105: Swords",             20,  () => new PowerScroll(SkillName.Swords,      105)),
            new RareMerchantEntry("PS +110: Swords",             45,  () => new PowerScroll(SkillName.Swords,      110)),
            new RareMerchantEntry("PS +115: Swords",             70,  () => new PowerScroll(SkillName.Swords,      115)),
            new RareMerchantEntry("PS +120: Swords",             90,  () => new PowerScroll(SkillName.Swords,      120)),
            new RareMerchantEntry("PS +105: Archery",            20,  () => new PowerScroll(SkillName.Archery,     105)),
            new RareMerchantEntry("PS +110: Archery",            45,  () => new PowerScroll(SkillName.Archery,     110)),
            new RareMerchantEntry("PS +115: Archery",            70,  () => new PowerScroll(SkillName.Archery,     115)),
            new RareMerchantEntry("PS +120: Archery",            90,  () => new PowerScroll(SkillName.Archery,     120)),
            new RareMerchantEntry("PS +105: Fencing",            20,  () => new PowerScroll(SkillName.Fencing,     105)),
            new RareMerchantEntry("PS +110: Fencing",            45,  () => new PowerScroll(SkillName.Fencing,     110)),
            new RareMerchantEntry("PS +115: Fencing",            70,  () => new PowerScroll(SkillName.Fencing,     115)),
            new RareMerchantEntry("PS +120: Fencing",            90,  () => new PowerScroll(SkillName.Fencing,     120)),
            new RareMerchantEntry("PS +105: Macing",             20,  () => new PowerScroll(SkillName.Macing,      105)),
            new RareMerchantEntry("PS +110: Macing",             45,  () => new PowerScroll(SkillName.Macing,      110)),
            new RareMerchantEntry("PS +115: Macing",             70,  () => new PowerScroll(SkillName.Macing,      115)),
            new RareMerchantEntry("PS +120: Macing",             90,  () => new PowerScroll(SkillName.Macing,      120)),
            new RareMerchantEntry("PS +105: Tactics",            20,  () => new PowerScroll(SkillName.Tactics,     105)),
            new RareMerchantEntry("PS +110: Tactics",            45,  () => new PowerScroll(SkillName.Tactics,     110)),
            new RareMerchantEntry("PS +115: Tactics",            70,  () => new PowerScroll(SkillName.Tactics,     115)),
            new RareMerchantEntry("PS +120: Tactics",            90,  () => new PowerScroll(SkillName.Tactics,     120)),
            new RareMerchantEntry("PS +105: Healing",            20,  () => new PowerScroll(SkillName.Healing,     105)),
            new RareMerchantEntry("PS +110: Healing",            45,  () => new PowerScroll(SkillName.Healing,     110)),
            new RareMerchantEntry("PS +115: Healing",            70,  () => new PowerScroll(SkillName.Healing,     115)),
            new RareMerchantEntry("PS +120: Healing",            90,  () => new PowerScroll(SkillName.Healing,     120)),
            new RareMerchantEntry("PS +105: Parry",              20,  () => new PowerScroll(SkillName.Parry,       105)),
            new RareMerchantEntry("PS +110: Parry",              45,  () => new PowerScroll(SkillName.Parry,       110)),
            new RareMerchantEntry("PS +115: Parry",              70,  () => new PowerScroll(SkillName.Parry,       115)),
            new RareMerchantEntry("PS +120: Parry",              90,  () => new PowerScroll(SkillName.Parry,       120)),

            // ── Power Scrolls — magic ──────────────────────────────────────
            new RareMerchantEntry("PS +105: Magery",             20,  () => new PowerScroll(SkillName.Magery,      105)),
            new RareMerchantEntry("PS +110: Magery",             50,  () => new PowerScroll(SkillName.Magery,      110)),
            new RareMerchantEntry("PS +115: Magery",             75,  () => new PowerScroll(SkillName.Magery,      115)),
            new RareMerchantEntry("PS +120: Magery",             100, () => new PowerScroll(SkillName.Magery,      120)),
            new RareMerchantEntry("PS +105: Eval Intelligence",  20,  () => new PowerScroll(SkillName.EvalInt,     105)),
            new RareMerchantEntry("PS +110: Eval Intelligence",  50,  () => new PowerScroll(SkillName.EvalInt,     110)),
            new RareMerchantEntry("PS +115: Eval Intelligence",  75,  () => new PowerScroll(SkillName.EvalInt,     115)),
            new RareMerchantEntry("PS +120: Eval Intelligence",  100, () => new PowerScroll(SkillName.EvalInt,     120)),
            new RareMerchantEntry("PS +105: Meditation",         20,  () => new PowerScroll(SkillName.Meditation,  105)),
            new RareMerchantEntry("PS +110: Meditation",         50,  () => new PowerScroll(SkillName.Meditation,  110)),
            new RareMerchantEntry("PS +115: Meditation",         75,  () => new PowerScroll(SkillName.Meditation,  115)),
            new RareMerchantEntry("PS +120: Meditation",         100, () => new PowerScroll(SkillName.Meditation,  120)),

            // ── Power Scrolls — taming ────────────────────────────────────
            new RareMerchantEntry("PS +105: Animal Taming",      25,  () => new PowerScroll(SkillName.AnimalTaming,105)),
            new RareMerchantEntry("PS +110: Animal Taming",      60,  () => new PowerScroll(SkillName.AnimalTaming,110)),
            new RareMerchantEntry("PS +115: Animal Taming",      100, () => new PowerScroll(SkillName.AnimalTaming,115)),
            new RareMerchantEntry("PS +120: Animal Taming",      150, () => new PowerScroll(SkillName.AnimalTaming,120)),

            // ── Power Scrolls — bard ──────────────────────────────────────
            new RareMerchantEntry("PS +105: Musicianship",       18,  () => new PowerScroll(SkillName.Musicianship,105)),
            new RareMerchantEntry("PS +110: Musicianship",       35,  () => new PowerScroll(SkillName.Musicianship,110)),
            new RareMerchantEntry("PS +115: Provocation",        55,  () => new PowerScroll(SkillName.Provocation, 115)),
            new RareMerchantEntry("PS +120: Provocation",        75,  () => new PowerScroll(SkillName.Provocation, 120)),
            new RareMerchantEntry("PS +115: Discordance",        55,  () => new PowerScroll(SkillName.Discordance, 115)),
            new RareMerchantEntry("PS +120: Discordance",        75,  () => new PowerScroll(SkillName.Discordance, 120)),
            new RareMerchantEntry("PS +115: Peacemaking",        55,  () => new PowerScroll(SkillName.Peacemaking, 115)),
            new RareMerchantEntry("PS +120: Peacemaking",        75,  () => new PowerScroll(SkillName.Peacemaking, 120)),

            // ── Power Scrolls — crafting ──────────────────────────────────
            new RareMerchantEntry("PS +115: Blacksmithy",        50,  () => new PowerScroll(SkillName.Blacksmith,  115)),
            new RareMerchantEntry("PS +120: Blacksmithy",        70,  () => new PowerScroll(SkillName.Blacksmith,  120)),
            new RareMerchantEntry("PS +115: Tailoring",          50,  () => new PowerScroll(SkillName.Tailoring,   115)),
            new RareMerchantEntry("PS +120: Tailoring",          70,  () => new PowerScroll(SkillName.Tailoring,   120)),

            // ── Power Scrolls — special ───────────────────────────────────
            new RareMerchantEntry("PS +105: Chivalry",           20,  () => new PowerScroll(SkillName.Chivalry,    105)),
            new RareMerchantEntry("PS +110: Chivalry",           45,  () => new PowerScroll(SkillName.Chivalry,    110)),
            new RareMerchantEntry("PS +115: Chivalry",           70,  () => new PowerScroll(SkillName.Chivalry,    115)),
            new RareMerchantEntry("PS +120: Chivalry",           90,  () => new PowerScroll(SkillName.Chivalry,    120)),
            new RareMerchantEntry("PS +105: Necromancy",         20,  () => new PowerScroll(SkillName.Necromancy,  105)),
            new RareMerchantEntry("PS +110: Necromancy",         45,  () => new PowerScroll(SkillName.Necromancy,  110)),
            new RareMerchantEntry("PS +115: Necromancy",         70,  () => new PowerScroll(SkillName.Necromancy,  115)),
            new RareMerchantEntry("PS +120: Necromancy",         90,  () => new PowerScroll(SkillName.Necromancy,  120)),
            new RareMerchantEntry("PS +115: Bushido",            70,  () => new PowerScroll(SkillName.Bushido,     115)),
            new RareMerchantEntry("PS +120: Bushido",            90,  () => new PowerScroll(SkillName.Bushido,     120)),
            new RareMerchantEntry("PS +115: Ninjitsu",           70,  () => new PowerScroll(SkillName.Ninjitsu,    115)),
            new RareMerchantEntry("PS +120: Ninjitsu",           90,  () => new PowerScroll(SkillName.Ninjitsu,    120)),
            new RareMerchantEntry("PS +115: Spellweaving",       70,  () => new PowerScroll(SkillName.Spellweaving,115)),
            new RareMerchantEntry("PS +120: Spellweaving",       90,  () => new PowerScroll(SkillName.Spellweaving,120)),
            new RareMerchantEntry("PS +115: Mysticism",          70,  () => new PowerScroll(SkillName.Mysticism,   115)),
            new RareMerchantEntry("PS +120: Mysticism",          90,  () => new PowerScroll(SkillName.Mysticism,   120)),
            new RareMerchantEntry("PS +115: Imbuing",            70,  () => new PowerScroll(SkillName.Imbuing,     115)),
            new RareMerchantEntry("PS +120: Imbuing",            90,  () => new PowerScroll(SkillName.Imbuing,     120)),
        };

        public static List<RareMerchantEntry> GetRandomStock(int count = 10)
        {
            var pool   = new List<RareMerchantEntry>(Pool);
            var result = new List<RareMerchantEntry>();
            count = Math.Min(count, pool.Count);
            while (result.Count < count)
            {
                int idx = Utility.Random(pool.Count);
                result.Add(pool[idx]);
                pool.RemoveAt(idx);
            }
            return result;
        }
    }

    // ── System manager ────────────────────────────────────────────────────────

    public static class TravelingRareMerchantSystem
    {
        private static readonly TimeSpan StayDuration = TimeSpan.FromMinutes(30);

        private static TravelingRareMerchant _current;

        private static readonly (Point3D loc, Map map, string town)[] BankLocations =
        {
            (new Point3D(1439, 1698, 20), Map.Felucca, "Britain"),
            (new Point3D(1859, 2754,  0), Map.Felucca, "Trinsic"),
            (new Point3D(2526,  582,  0), Map.Felucca, "Minoc"),
            (new Point3D(2893,  686,  0), Map.Felucca, "Vesper"),
            (new Point3D( 586, 2150,  0), Map.Felucca, "Skara Brae"),
            (new Point3D( 621, 1000,  0), Map.Felucca, "Yew"),
            (new Point3D(1355, 3834, 20), Map.Felucca, "Jhelom"),
            (new Point3D(4471, 1177,  0), Map.Felucca, "Moonglow"),
            (new Point3D(3714, 2190, 20), Map.Felucca, "Magincia"),
            (new Point3D(1439, 1698, 20), Map.Trammel, "Britain (Trammel)"),
            (new Point3D(1859, 2754,  0), Map.Trammel, "Trinsic (Trammel)"),
            (new Point3D(2526,  582,  0), Map.Trammel, "Minoc (Trammel)"),
            (new Point3D(2893,  686,  0), Map.Trammel, "Vesper (Trammel)"),
            (new Point3D( 586, 2150,  0), Map.Trammel, "Skara Brae (Trammel)"),
            (new Point3D(4471, 1177,  0), Map.Trammel, "Moonglow (Trammel)"),
            (new Point3D(3503, 2574, 14), Map.Trammel, "New Haven"),
        };

        public static void Initialize()
        {
            CommandSystem.Register("respawnmerchant", AccessLevel.GameMaster,
                e => { SpawnNext(); e.Mobile.SendMessage(0x35, "Rare merchant respawned."); });

            Timer.DelayCall(TimeSpan.FromSeconds(30), SpawnNext);
        }

        public static void SpawnNext()
        {
            if (_current != null && !_current.Deleted)
            {
                _current.SayFarewell();
                TravelingRareMerchant captured = _current;
                Timer.DelayCall(TimeSpan.FromSeconds(3), () =>
                {
                    if (!captured.Deleted) captured.Delete();
                });
            }

            var entry = BankLocations[Utility.Random(BankLocations.Length)];
            _current  = new TravelingRareMerchant(entry.town);

            int x = entry.loc.X + Utility.RandomMinMax(-3, 3);
            int y = entry.loc.Y + Utility.RandomMinMax(-3, 3);
            int z = entry.map.GetAverageZ(x, y);

            _current.MoveToWorld(new Point3D(x, y, z), entry.map);
            _current.SayArrival();

            World.Broadcast(0x8A5, true,
                $"[Rumour] A mysterious merchant has appeared at the {entry.town} bank. " +
                "He accepts only Merchant Coins and stays just 30 minutes.");

            Timer.DelayCall(StayDuration, SpawnNext);
        }
    }

    // ── NPC ───────────────────────────────────────────────────────────────────

    public class TravelingRareMerchant : BaseCreature
    {
        private readonly string             _townName;
        private readonly List<RareMerchantSlot> _stock = new List<RareMerchantSlot>();

        [Constructable]
        public TravelingRareMerchant() : this("Unknown Town") { }

        public TravelingRareMerchant(string townName)
            : base(AIType.AI_Melee, FightMode.None, 10, 1, 0.2, 0.4)
        {
            _townName = townName;

            Name  = "the Rare Goods Merchant";
            Title = "of the Far Markets";
            Body  = Utility.RandomBool() ? 0x190 : 0x191;
            Hue   = Utility.RandomSkinHue();

            Utility.AssignRandomHair(this);

            AddItem(new Robe(1));
            AddItem(new WizardsHat(1));
            AddItem(new Sandals(0x8A5));
            AddItem(new GoldBracelet());
            AddItem(new GoldNecklace());

            SetStr(75); SetDex(75); SetInt(150);
            SetHits(200);
            VirtualArmor = 20;
            Fame = 0; Karma = 1000;
            CantWalk = true;

            GenerateStock();
        }

        public TravelingRareMerchant(Serial serial) : base(serial) { }

        public IReadOnlyList<RareMerchantSlot> Stock => _stock;

        private void GenerateStock()
        {
            ClearStock();
            foreach (var e in RareMerchantStock.GetRandomStock(10))
            {
                Item display = e.Create();
                display.MoveToWorld(new Point3D(0, 0, 0), Map.Internal);
                _stock.Add(new RareMerchantSlot(display, e.Cost, e.Name, e.Create));
            }
        }

        public void ClearStock()
        {
            foreach (var s in _stock)
                if (s.Item != null && !s.Item.Deleted)
                    s.Item.Delete();
            _stock.Clear();
        }

        /// <summary>
        /// Called when a player buys slot at index. Removes display item,
        /// creates a fresh copy for the player, returns it.
        /// </summary>
        public Item PurchaseSlot(int index)
        {
            if (index < 0 || index >= _stock.Count) return null;
            var slot = _stock[index];
            if (slot.Item == null || slot.Item.Deleted) return null;

            slot.Item.Delete(); // remove display copy
            _stock.RemoveAt(index);

            return slot.Factory(); // fresh item for the player
        }

        public override bool IsInvulnerable => true;
        public override bool CanBeRenamedBy(Mobile from) => false;

        public void SayArrival()
        {
            Say($"*sets out wares at the {_townName} bank* " +
                "I have rare goods — Merchant Coins only.");
        }

        public void SayFarewell()
        {
            Say("*packs up quietly* Time to move on...");
            Effects.SendLocationParticles(
                EffectItem.Create(Location, Map, EffectItem.DefaultDuration),
                0x376A, 9, 32, 5023);
            PlaySound(0x1FE);
        }

        public override void OnDoubleClick(Mobile from)
        {
            if (!(from is PlayerMobile pm)) return;
            if (!pm.InRange(Location, 5))
            {
                pm.SendMessage("You are too far away.");
                return;
            }
            Say("*glances over* Let me show you what I have.");
            pm.CloseGump(typeof(RareMerchantGump));
            pm.SendGump(new RareMerchantGump(pm, this));
        }

        public override void OnDelete()
        {
            ClearStock();
            base.OnDelete();
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
            Timer.DelayCall(TimeSpan.Zero, Delete);
        }
    }

    // ── Main shop gump ────────────────────────────────────────────────────────

    public class RareMerchantGump : Gump
    {
        private readonly PlayerMobile          _player;
        private readonly TravelingRareMerchant _npc;

        // Layout
        private const int GW        = 620;
        private const int ColW      = 295;
        private const int RowH      = 85;
        private const int GridLeft  = 10;
        private const int GridRight = 315;
        private const int GridTop   = 70;

        private const int BtnClose  = 0;
        private const int BtnBuyBase = 100; // + slot index

        public RareMerchantGump(PlayerMobile player, TravelingRareMerchant npc)
            : base(60, 40)
        {
            _player = player;
            _npc    = npc;

            var stock  = npc.Stock;
            int rows   = (int)Math.Ceiling(stock.Count / 2.0);
            int GH     = GridTop + rows * RowH + 50;

            AddBackground(0, 0, GW, GH, 9200);
            AddAlphaRegion(5, 5, GW - 10, GH - 10);

            // Title bar
            AddImageTiled(5, 5, GW - 10, 55, 9304);
            AddLabel(GW / 2 - 110, 14, 0x8A5, "The Rare Goods Merchant");
            AddLabel(GW / 2 - 95, 32, 1152,   "Accepts Merchant Coins only");

            // Coin balance
            int coins = player.Backpack != null
                ? player.Backpack.GetAmount(typeof(MerchantCoin)) : 0;
            AddLabel(GW - 150, 14, 0x35, $"Your coins: {coins}");
            AddItem(GW - 160, 10, 0xEED, 1153); // coin graphic

            // Item grid — 2 columns
            for (int i = 0; i < stock.Count; i++)
            {
                var slot = stock[i];
                int col  = (i % 2 == 0) ? GridLeft : GridRight;
                int row  = GridTop + (i / 2) * RowH;

                // Cell background
                AddImageTiled(col, row, ColW, RowH - 5, 9274);
                AddAlphaRegion(col, row, ColW, RowH - 5);

                // Item icon + tooltip on hover
                AddItem(col + 8, row + 17, slot.Item.ItemID, slot.Item.Hue);
                AddItemProperty(slot.Item.Serial);

                // Item name — truncate if too long
                string name = slot.Name.Length > 28 ? slot.Name.Substring(0, 27) + "…" : slot.Name;
                bool canAfford = coins >= slot.Cost;
                AddLabel(col + 60, row + 10, canAfford ? 1152 : 33, name);

                // Price
                AddLabel(col + 60, row + 28, canAfford ? 0x8A5 : 33, $"{slot.Cost} Merchant Coins");

                // Buy button
                if (canAfford)
                {
                    AddButton(col + 60, row + 50, 4005, 4007, BtnBuyBase + i, GumpButtonType.Reply, 0);
                    AddLabel(col + 95,  row + 52, 0x35, "Purchase");
                }
                else
                {
                    AddLabel(col + 60, row + 52, 33, "Cannot afford");
                }
            }

            // Close button
            int closeY = GH - 38;
            AddButton(GW / 2 - 50, closeY, 4017, 4019, BtnClose, GumpButtonType.Reply, 0);
            AddLabel(GW / 2 - 15,  closeY + 2, 33, "Leave");
        }

        public override void OnResponse(NetState sender, RelayInfo info)
        {
            if (_player == null || _player.Deleted) return;
            if (_npc    == null || _npc.Deleted)    return;
            if (info.ButtonID == BtnClose)          return;

            if (!_player.InRange(_npc.Location, 6))
            {
                _player.SendMessage("You have moved too far away.");
                return;
            }

            int idx = info.ButtonID - BtnBuyBase;
            if (idx < 0 || idx >= _npc.Stock.Count) return;

            var slot = _npc.Stock[idx];

            int coins = _player.Backpack != null
                ? _player.Backpack.GetAmount(typeof(MerchantCoin)) : 0;

            if (coins < slot.Cost)
            {
                _player.SendMessage(0x22, "You no longer have enough coins for that.");
                _player.SendGump(new RareMerchantGump(_player, _npc));
                return;
            }

            // Open confirmation gump
            _player.CloseGump(typeof(RareMerchantConfirmGump));
            _player.SendGump(new RareMerchantConfirmGump(_player, _npc, idx));
        }
    }

    // ── Confirmation gump ─────────────────────────────────────────────────────

    public class RareMerchantConfirmGump : Gump
    {
        private readonly PlayerMobile          _player;
        private readonly TravelingRareMerchant _npc;
        private readonly int                   _slotIndex;

        private const int BtnConfirm = 1;
        private const int BtnCancel  = 2;

        public RareMerchantConfirmGump(PlayerMobile player, TravelingRareMerchant npc, int slotIndex)
            : base(200, 200)
        {
            _player    = player;
            _npc       = npc;
            _slotIndex = slotIndex;

            var slot  = npc.Stock[slotIndex];
            int coins = player.Backpack != null
                ? player.Backpack.GetAmount(typeof(MerchantCoin)) : 0;

            AddBackground(0, 0, 340, 200, 9200);
            AddAlphaRegion(5, 5, 330, 190);
            AddImageTiled(5, 5, 330, 45, 9304);

            AddLabel(170 - 60, 14, 0x8A5, "Confirm Purchase");

            // Item display
            AddItem(20, 60, slot.Item.ItemID, slot.Item.Hue);
            AddItemProperty(slot.Item.Serial);

            // Item name
            string name = slot.Name.Length > 26 ? slot.Name.Substring(0, 25) + "…" : slot.Name;
            AddLabel(80, 58, 1152, name);
            AddLabel(80, 76, 0x8A5, $"Cost: {slot.Cost} Merchant Coins");

            int remaining = coins - slot.Cost;
            AddLabel(80, 94, remaining >= 0 ? 0x35 : 33,
                $"Balance after: {remaining} coins");

            // Separator
            AddImageTiled(10, 120, 320, 1, 9264);

            AddLabel(30, 130, 1152, "Are you sure you want to buy this item?");

            // Buttons
            AddButton(60,  160, 4005, 4007, BtnConfirm, GumpButtonType.Reply, 0);
            AddLabel(95,   162, 0x35, "Confirm");

            AddButton(200, 160, 4017, 4019, BtnCancel, GumpButtonType.Reply, 0);
            AddLabel(235,  162, 33, "Cancel");
        }

        public override void OnResponse(NetState sender, RelayInfo info)
        {
            if (_player == null || _player.Deleted) return;
            if (_npc    == null || _npc.Deleted)    return;

            if (info.ButtonID == BtnCancel)
            {
                // Return to shop
                _player.SendGump(new RareMerchantGump(_player, _npc));
                return;
            }

            if (info.ButtonID != BtnConfirm) return;

            if (!_player.InRange(_npc.Location, 6))
            {
                _player.SendMessage("You have moved too far away.");
                return;
            }

            if (_slotIndex >= _npc.Stock.Count)
            {
                _player.SendMessage("That item is no longer available.");
                _player.SendGump(new RareMerchantGump(_player, _npc));
                return;
            }

            var slot  = _npc.Stock[_slotIndex];
            int coins = _player.Backpack != null
                ? _player.Backpack.GetAmount(typeof(MerchantCoin)) : 0;

            if (coins < slot.Cost)
            {
                _player.SendMessage(0x22, "You no longer have enough coins.");
                _player.SendGump(new RareMerchantGump(_player, _npc));
                return;
            }

            // Deduct coins and deliver item
            _player.Backpack.ConsumeTotal(typeof(MerchantCoin), slot.Cost);

            Item purchased = _npc.PurchaseSlot(_slotIndex);
            if (purchased != null)
                _player.AddToBackpack(purchased);

            _npc.Say($"A fine choice. Enjoy your {slot.Name.Split(':')[0].Trim()}.");
            _player.SendMessage(0x35,
                $"You purchased {slot.Name} for {slot.Cost} Merchant Coins.");

            // Effects
            Effects.SendLocationParticles(
                EffectItem.Create(_player.Location, _player.Map, EffectItem.DefaultDuration),
                0x376A, 9, 20, 5023);
            _player.PlaySound(0x2E6);

            // Reopen shop with updated stock
            _player.SendGump(new RareMerchantGump(_player, _npc));
        }
    }
}
