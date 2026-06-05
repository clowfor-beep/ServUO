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

        }; // end Pool (non-PS items only — PS uses two-stage equal-skill draw)

        // ── All 34 PS-eligible skills (matches PowerScroll.cs m_Skills) ─────────
        private static readonly (SkillName skill, string display)[] PSSkills =
        {
            (SkillName.Swords,       "Swords"),
            (SkillName.Fencing,      "Fencing"),
            (SkillName.Macing,       "Macing"),
            (SkillName.Archery,      "Archery"),
            (SkillName.Wrestling,    "Wrestling"),
            (SkillName.Parry,        "Parrying"),
            (SkillName.Tactics,      "Tactics"),
            (SkillName.Anatomy,      "Anatomy"),
            (SkillName.Healing,      "Healing"),
            (SkillName.Magery,       "Magery"),
            (SkillName.EvalInt,      "Eval Intelligence"),
            (SkillName.Meditation,   "Meditation"),
            (SkillName.MagicResist,  "Magic Resistance"),
            (SkillName.Focus,        "Focus"),
            (SkillName.AnimalTaming, "Animal Taming"),
            (SkillName.AnimalLore,   "Animal Lore"),
            (SkillName.Veterinary,   "Veterinary"),
            (SkillName.Musicianship, "Musicianship"),
            (SkillName.Provocation,  "Provocation"),
            (SkillName.Discordance,  "Discordance"),
            (SkillName.Peacemaking,  "Peacemaking"),
            (SkillName.Chivalry,     "Chivalry"),
            (SkillName.Necromancy,   "Necromancy"),
            (SkillName.SpiritSpeak,  "Spirit Speak"),
            (SkillName.Bushido,      "Bushido"),
            (SkillName.Ninjitsu,     "Ninjitsu"),
            (SkillName.Spellweaving, "Spellweaving"),
            (SkillName.Mysticism,    "Mysticism"),
            (SkillName.Imbuing,      "Imbuing"),
            (SkillName.Blacksmith,   "Blacksmithy"),
            (SkillName.Tailoring,    "Tailoring"),
            (SkillName.Stealing,     "Stealing"),
            (SkillName.Stealth,      "Stealth"),
            (SkillName.Throwing,     "Throwing"),
            (SkillName.Cartography,  "Cartography"),
            (SkillName.Fishing,      "Fishing"),
            (SkillName.Tracking,     "Tracking"),
            (SkillName.Herding,      "Herding"),
            (SkillName.DetectHidden, "Detect Hidden"),
            (SkillName.Hiding,       "Hiding"),
            (SkillName.Snooping,     "Snooping"),
        };

        // Tier → coin cost
        private static readonly int[] PSTiers    = { 105, 110, 115, 120 };
        private static readonly int[] PSTierCosts = {  20,  45,  70,  90 };

        /// <summary>
        /// Builds a pool of PS entries where every skill has exactly equal
        /// representation. Each skill gets 2 slots (different random tiers),
        /// giving 68 virtual PS entries — close to the original 74, maintaining
        /// roughly the same PS-vs-non-PS ratio in the 10-slot stock.
        /// </summary>
        private static List<RareMerchantEntry> BuildPSPool()
        {
            var pool = new List<RareMerchantEntry>();
            foreach (var (skill, display) in PSSkills)
            {
                for (int rep = 0; rep < 2; rep++)
                {
                    int tierIdx = Utility.Random(PSTiers.Length);
                    int tier    = PSTiers[tierIdx];
                    int cost    = PSTierCosts[tierIdx];

                    // Capture for closure — both display item and factory use the same skill+tier
                    SkillName capturedSkill = skill;
                    double    capturedTier  = tier;
                    string    name          = string.Format("PS +{0}: {1}", tier, display);

                    pool.Add(new RareMerchantEntry(name, cost,
                        () => new PowerScroll(capturedSkill, capturedTier)));
                }
            }
            return pool;
        }

        public static List<RareMerchantEntry> GetRandomStock(int count = 10)
        {
            // Combine non-PS flat pool with dynamically built PS pool (equal skill weight)
            var combined = new List<RareMerchantEntry>(Pool);
            combined.AddRange(BuildPSPool()); // adds 68 PS entries, 2 per skill

            var result = new List<RareMerchantEntry>();
            count = Math.Min(count, combined.Count);
            while (result.Count < count)
            {
                int idx = Utility.Random(combined.Count);
                result.Add(combined[idx]);
                combined.RemoveAt(idx);
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
        };

        public static void Initialize()
        {
            CommandSystem.Register("respawnmerchant", AccessLevel.GameMaster,
                e => { SpawnNext(); e.Mobile.SendMessage(0x35, "Rare merchant respawned."); });

            Timer.DelayCall(TimeSpan.FromSeconds(30), SpawnNext);
        }

        public static string GetCurrentMerchantLocation()
        {
            if (_current == null || _current.Deleted) return null;
            return _current.TownName;
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

            World.Broadcast(0x8C4, true,
                string.Format("[Rumour] A mysterious merchant has appeared at the {0} bank. " +
                "He accepts only Merchant Coins and stays just 30 minutes.", entry.town));

            Timer.DelayCall(StayDuration, SpawnNext);
        }
    }

    // ── NPC ───────────────────────────────────────────────────────────────────

    public class TravelingRareMerchant : BaseCreature
    {
        private readonly string                  _townName;
        public string TownName => _townName;
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

        // Global price multiplier — change this to adjust all stock prices.
        private const double PriceMultiplier = 0.5;

        private void GenerateStock()
        {
            ClearStock();
            foreach (var e in RareMerchantStock.GetRandomStock(10))
            {
                Item display = e.Create();
                display.MoveToWorld(new Point3D(0, 0, 0), Map.Internal);
                int cost = Math.Max(1, (int)Math.Round(e.Cost * PriceMultiplier));
                _stock.Add(new RareMerchantSlot(display, cost, e.Name, e.Create));
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
            Say(string.Format("*sets out wares at the {0} bank* I have rare goods — Merchant Coins only.", _townName));
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

            // Pre-send OPL for all display items so hover tooltips work
            // (items live on Map.Internal and don't auto-send to clients)
            if (pm.NetState != null)
            {
                foreach (var slot in _stock)
                {
                    if (slot.Item != null && !slot.Item.Deleted)
                        pm.Send(slot.Item.PropertyList);
                }
            }

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

    // ── Discount helpers ──────────────────────────────────────────────────────

    public static class RareMerchantDiscount
    {
        /// <summary>
        /// Returns the total discount fraction (0.0-1.0) for a player:
        ///   +1% per Paladin Order reputation tier (max 4% at Allied)
        ///   +1% per 20 points of Begging skill (max 6% at skill 120)
        /// </summary>
        public static double GetDiscount(PlayerMobile pm)
        {
            int    repTier  = (int)ReputationSystem.GetTier(pm, FBGuilds.PaladinOrder);
            double repPct   = repTier * 0.01;

            double begging  = pm.Skills[SkillName.Begging].Value;
            double begPct   = Math.Floor(begging / 20.0) * 0.01;

            return repPct + begPct; // max 10% (4 rep tiers + 6 begging tiers)
        }

        /// <summary>Returns the discounted cost, minimum 1 coin.</summary>
        public static int ApplyCost(int baseCost, PlayerMobile pm)
        {
            double disc = GetDiscount(pm);
            return Math.Max(1, (int)Math.Round(baseCost * (1.0 - disc)));
        }

        /// <summary>Returns a short readable summary e.g. "3% (Rep: 2% + Begging: 1%)"</summary>
        public static string Summary(PlayerMobile pm)
        {
            int    repTier = (int)ReputationSystem.GetTier(pm, FBGuilds.PaladinOrder);
            double begging = pm.Skills[SkillName.Begging].Value;
            int    begTier = (int)Math.Floor(begging / 20.0);
            int    total   = repTier + begTier;

            if (total == 0) return string.Empty;
            return string.Format("{0}% off  (Paladin: {1}%  Begging: {2}%)", total, repTier, begTier);
        }
    }

    // ── Main shop gump ────────────────────────────────────────────────────────

    public class RareMerchantGump : Gump
    {
        private readonly PlayerMobile          _player;
        private readonly TravelingRareMerchant _npc;

        // Layout
        private const int GW        = 640;
        private const int ColW      = 305;
        private const int RowH      = 95;
        private const int GridLeft  = 8;
        private const int GridRight = 324;
        private const int GridTop   = 80;
        private const int HeaderH   = 72;

        private const int BtnClose   = 0;
        private const int BtnBuyBase = 100;

        public RareMerchantGump(PlayerMobile player, TravelingRareMerchant npc)
            : base(50, 30)
        {
            _player = player;
            _npc    = npc;

            var stock  = npc.Stock;
            int rows   = (int)Math.Ceiling(stock.Count / 2.0);
            int GH     = GridTop + rows * RowH + 48;

            // Outer frame
            AddBackground(0, 0, GW, GH, 9200);
            AddAlphaRegion(4, 4, GW - 8, GH - 8);

            // Header band
            AddImageTiled(4, 4, GW - 8, HeaderH, 9304);
            AddAlphaRegion(4, 4, GW - 8, HeaderH);

            // Title
            AddLabel(GW / 2 - 120, 10, 0x8A5, "~ The Rare Goods Merchant ~");
            AddLabel(GW / 2 - 105, 30, 1152,  "Accepts Merchant Coins only");

            // Coin balance (top right)
            int coins = player.Backpack != null
                ? player.Backpack.GetAmount(typeof(MerchantCoin)) : 0;
            AddItem(GW - 155, 8, 0xEED, 1153);
            AddLabel(GW - 135, 10, coins > 0 ? 0x35 : 33, string.Format("Coins: {0}", coins));

            // Discount (below coin balance)
            string discSummary = RareMerchantDiscount.Summary(player);
            if (!string.IsNullOrEmpty(discSummary))
                AddLabel(GW - 155, 30, 0x59, discSummary);

            // Separator line below header
            AddImageTiled(4, HeaderH + 2, GW - 8, 2, 9264);

            // Item grid — 2 columns
            for (int i = 0; i < stock.Count; i++)
            {
                var slot      = stock[i];
                int col       = (i % 2 == 0) ? GridLeft : GridRight;
                int row       = GridTop + (i / 2) * RowH;
                int finalCost = RareMerchantDiscount.ApplyCost(slot.Cost, player);
                bool disc     = finalCost < slot.Cost;
                bool canAfford = coins >= finalCost;

                // Cell background — alternate shade
                int cellBg = (i / 2) % 2 == 0 ? 9274 : 9200;
                AddImageTiled(col, row, ColW, RowH - 3, cellBg);
                AddAlphaRegion(col, row, ColW, RowH - 3);

                // Left accent bar
                AddImageTiled(col, row, 3, RowH - 3, canAfford ? 0x8A5 : 33);

                // Item icon (larger area, centred vertically)
                AddItem(col + 8, row + 10, slot.Item.ItemID, slot.Item.Hue);
                AddItemProperty(slot.Item.Serial);

                // Item name
                string name = slot.Name.Length > 30
                    ? slot.Name.Substring(0, 29) + "..."
                    : slot.Name;
                AddLabel(col + 62, row + 8, canAfford ? 1152 : 0x848, name);

                // Pricing row
                if (disc)
                {
                    AddLabel(col + 62, row + 28, 0x3B2, string.Format("{0}c  ->", slot.Cost));
                    AddLabel(col + 108, row + 28, 0x8A5, string.Format("{0} coins", finalCost));
                }
                else
                {
                    AddLabel(col + 62, row + 28, canAfford ? 0x8A5 : 0x3B2,
                        string.Format("{0} Merchant Coins", finalCost));
                }

                // Buy button or "insufficient funds" note
                if (canAfford)
                {
                    AddButton(col + 62, row + 56, 4005, 4007, BtnBuyBase + i,
                        GumpButtonType.Reply, 0);
                    AddLabel(col + 97, row + 58, 0x35, "Purchase");
                }
                else
                {
                    AddLabel(col + 62, row + 56, 33, "Insufficient coins");
                }

                // Vertical divider between columns
                if (i % 2 == 0)
                    AddImageTiled(GridRight - 2, row, 2, RowH - 3, 9264);
            }

            // Footer
            int footY = GH - 38;
            AddImageTiled(4, footY - 6, GW - 8, 2, 9264);
            AddButton(GW / 2 - 55, footY, 4017, 4019, BtnClose, GumpButtonType.Reply, 0);
            AddLabel(GW / 2 - 20, footY + 2, 0x848, "Close");
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

            var slot      = npc.Stock[slotIndex];
            int finalCost = RareMerchantDiscount.ApplyCost(slot.Cost, player);
            int coins     = player.Backpack != null
                ? player.Backpack.GetAmount(typeof(MerchantCoin)) : 0;

            string discSummary = RareMerchantDiscount.Summary(player);
            int    gumpH       = string.IsNullOrEmpty(discSummary) ? 200 : 215;

            AddBackground(0, 0, 340, gumpH, 9200);
            AddAlphaRegion(5, 5, 330, gumpH - 10);
            AddImageTiled(5, 5, 330, 45, 9304);

            AddLabel(170 - 60, 14, 0x8A5, "Confirm Purchase");

            // Item display
            AddItem(20, 60, slot.Item.ItemID, slot.Item.Hue);
            AddItemProperty(slot.Item.Serial);

            // Item name + pricing
            string name = slot.Name.Length > 26 ? slot.Name.Substring(0, 25) + "..." : slot.Name;
            AddLabel(80, 58, 1152, name);

            if (finalCost < slot.Cost)
            {
                AddLabel(80, 76, 33,    string.Format("Base: {0}c", slot.Cost));
                AddLabel(80, 94, 0x8A5, string.Format("Your price: {0}c  ({1})", finalCost, discSummary));
            }
            else
            {
                AddLabel(80, 76, 0x8A5, string.Format("Cost: {0} Merchant Coins", finalCost));
            }

            int remaining = coins - finalCost;
            int noteY = finalCost < slot.Cost ? 112 : 94;
            AddLabel(80, noteY, remaining >= 0 ? 0x35 : 33,
                string.Format("Balance after: {0} coins", remaining));

            // Separator + confirm question
            int sepY = gumpH - 80;
            AddImageTiled(10, sepY, 320, 1, 9264);
            AddLabel(30, sepY + 10, 1152, "Are you sure you want to buy this item?");

            // Buttons
            int btnY = gumpH - 38;
            AddButton(60,  btnY, 4005, 4007, BtnConfirm, GumpButtonType.Reply, 0);
            AddLabel(95,   btnY + 2, 0x35, "Confirm");
            AddButton(200, btnY, 4017, 4019, BtnCancel, GumpButtonType.Reply, 0);
            AddLabel(235,  btnY + 2, 33, "Cancel");
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

            var slot      = _npc.Stock[_slotIndex];
            int finalCost = RareMerchantDiscount.ApplyCost(slot.Cost, _player);
            int coins     = _player.Backpack != null
                ? _player.Backpack.GetAmount(typeof(MerchantCoin)) : 0;

            if (coins < finalCost)
            {
                _player.SendMessage(0x22, "You no longer have enough coins.");
                _player.SendGump(new RareMerchantGump(_player, _npc));
                return;
            }

            // Deduct coins and deliver item
            _player.Backpack.ConsumeTotal(typeof(MerchantCoin), finalCost);

            Item purchased = _npc.PurchaseSlot(_slotIndex);
            if (purchased != null)
                _player.AddToBackpack(purchased);

            _npc.Say(string.Format("A fine choice. Enjoy your {0}.", slot.Name.Split(':')[0].Trim()));
            _player.SendMessage(0x35,
                string.Format("You purchased {0} for {1} Merchant Coins.", slot.Name, finalCost));

            // Effects
            Effects.SendLocationParticles(
                EffectItem.Create(_player.Location, _player.Map, EffectItem.DefaultDuration),
                0x376A, 9, 20, 5023);
            _player.PlaySound(0x2E6);

            // Pre-send OPL for remaining items then reopen shop
            if (_player.NetState != null)
            {
                foreach (var s in _npc.Stock)
                    if (s.Item != null && !s.Item.Deleted)
                        _player.Send(s.Item.PropertyList);
            }
            _player.SendGump(new RareMerchantGump(_player, _npc));
        }
    }
}
