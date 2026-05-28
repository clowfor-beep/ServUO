using System;
using Server;
using Server.Custom;
using Server.Items;
using Server.Mobiles;

namespace Server.Items
{
    // ─────────────────────────────────────────────────────────────────────────────
    // Rarity tiers for the GuildSash.
    //   Standard  — +5  to each of three guild skills  (Rare quest, 30%)
    //   Refined   — +10 to each                        (Legendary quest, 40%)
    //   Exalted   — +15 to each                        (Legendary quest, 20%)
    // ─────────────────────────────────────────────────────────────────────────────
    public enum SashTier { Standard = 0, Refined = 1, Exalted = 2 }

    /// <summary>
    /// A guild sash worn at the waist (Layer.Waist — the only uncontested stat slot).
    /// Grants skill bonuses reflecting the guild's focus.  Equip/unequip handling
    /// is automatic via BaseClothing's AosSkillBonuses pipeline.
    ///
    /// Drop sources:
    ///   Rare quest     → Standard  (+5)  at 30 %
    ///   Legendary quest → Refined  (+10) at 40 %, Exalted (+15) at 20 %
    ///
    /// Staff spawn: [add GuildSash
    ///   or via code: GuildSash.For(FBGuilds.PaladinOrder, SashTier.Exalted)
    /// </summary>
    public class GuildSash : BaseWaist
    {
        // ── Item graphic ─────────────────────────────────────────────────────────
        // 0x153B = HalfApron silhouette; closest available sash shape.
        private const int SashItemID = 0x153B;

        // ── Per-tier skill bonus amount ───────────────────────────────────────────
        private static double BonusFor(SashTier tier)
        {
            switch (tier)
            {
                case SashTier.Refined: return 10.0;
                case SashTier.Exalted: return 15.0;
                default:               return  5.0;
            }
        }

        // ── Per-tier name prefix ──────────────────────────────────────────────────
        private static string PrefixFor(SashTier tier)
        {
            switch (tier)
            {
                case SashTier.Refined: return "Refined ";
                case SashTier.Exalted: return "Exalted ";
                default:               return string.Empty;
            }
        }

        // ── Guild base hues ───────────────────────────────────────────────────────
        // Each tier brightens the hue slightly:
        //   Standard  → base hue
        //   Refined   → base hue + 0x40  (brighter tint)
        //   Exalted   → base hue + 0x80  (most vibrant)
        //
        // We keep the values well below 0xBB8 (max safe hue) so they always render.
        private static int BaseHueFor(string guild)
        {
            if (guild == FBGuilds.Wanderers)         return 0x482; // warm earth-brown
            if (guild == FBGuilds.CraftsmenLeague)   return 0x8AB; // ashen grey
            if (guild == FBGuilds.ShadowHand)        return 0x455; // deep purple
            if (guild == FBGuilds.IronCompany)       return 0x021; // blood red
            if (guild == FBGuilds.ArcaneBrotherhood) return 0x4AA; // arcane blue
            if (guild == FBGuilds.SilverWolves)      return 0x3E9; // silver-white
            if (guild == FBGuilds.PaladinOrder)      return 0x053; // holy gold
            if (guild == FBGuilds.DeadWatchers)      return 0x835; // necrotic green
            if (guild == FBGuilds.DreadHunters)      return 0x59C; // forest green
            if (guild == FBGuilds.BloodPact)         return 0x026; // deep crimson
            if (guild == FBGuilds.TheVoid)           return 0x02E; // void grey
            if (guild == FBGuilds.Shadowblade)       return 0x001; // shadow black
            return 0;
        }

        private static int HueFor(string guild, SashTier tier)
        {
            int base_ = BaseHueFor(guild);
            if (base_ == 0) return 0;
            switch (tier)
            {
                case SashTier.Refined: return Math.Min(base_ + 0x40, 0xBB7);
                case SashTier.Exalted: return Math.Min(base_ + 0x80, 0xBB7);
                default:               return base_;
            }
        }

        // ── Skill bonus table ─────────────────────────────────────────────────────
        // Pass the bonus amount so one method handles all tiers.
        private static void ApplySkillBonuses(GuildSash sash, string guild, double bonus)
        {
            if (guild == FBGuilds.Wanderers)
            {
                sash.SkillBonuses.SetValues(0, SkillName.Camping,      bonus);
                sash.SkillBonuses.SetValues(1, SkillName.Cartography,  bonus);
                sash.SkillBonuses.SetValues(2, SkillName.Herding,      bonus);
            }
            else if (guild == FBGuilds.CraftsmenLeague)
            {
                sash.SkillBonuses.SetValues(0, SkillName.Blacksmith,   bonus);
                sash.SkillBonuses.SetValues(1, SkillName.Tailoring,    bonus);
                sash.SkillBonuses.SetValues(2, SkillName.Carpentry,    bonus);
            }
            else if (guild == FBGuilds.ShadowHand)
            {
                sash.SkillBonuses.SetValues(0, SkillName.Stealing,     bonus);
                sash.SkillBonuses.SetValues(1, SkillName.Hiding,       bonus);
                sash.SkillBonuses.SetValues(2, SkillName.Stealth,      bonus);
            }
            else if (guild == FBGuilds.IronCompany)
            {
                sash.SkillBonuses.SetValues(0, SkillName.Swords,       bonus);
                sash.SkillBonuses.SetValues(1, SkillName.Tactics,      bonus);
                sash.SkillBonuses.SetValues(2, SkillName.Parry,     bonus);
            }
            else if (guild == FBGuilds.ArcaneBrotherhood)
            {
                sash.SkillBonuses.SetValues(0, SkillName.Magery,       bonus);
                sash.SkillBonuses.SetValues(1, SkillName.EvalInt,      bonus);
                sash.SkillBonuses.SetValues(2, SkillName.Meditation,   bonus);
            }
            else if (guild == FBGuilds.SilverWolves)
            {
                sash.SkillBonuses.SetValues(0, SkillName.Swords,       bonus);
                sash.SkillBonuses.SetValues(1, SkillName.Parry,     bonus);
                sash.SkillBonuses.SetValues(2, SkillName.Tracking,     bonus);
            }
            else if (guild == FBGuilds.PaladinOrder)
            {
                sash.SkillBonuses.SetValues(0, SkillName.Healing,      bonus);
                sash.SkillBonuses.SetValues(1, SkillName.Anatomy,      bonus);
                sash.SkillBonuses.SetValues(2, SkillName.Chivalry,     bonus);
            }
            else if (guild == FBGuilds.DeadWatchers)
            {
                sash.SkillBonuses.SetValues(0, SkillName.Necromancy,   bonus);
                sash.SkillBonuses.SetValues(1, SkillName.SpiritSpeak,  bonus);
                sash.SkillBonuses.SetValues(2, SkillName.Meditation,   bonus);
            }
            else if (guild == FBGuilds.DreadHunters)
            {
                sash.SkillBonuses.SetValues(0, SkillName.Archery,      bonus);
                sash.SkillBonuses.SetValues(1, SkillName.Tracking,     bonus);
                sash.SkillBonuses.SetValues(2, SkillName.AnimalTaming, bonus);
            }
            else if (guild == FBGuilds.BloodPact)
            {
                sash.SkillBonuses.SetValues(0, SkillName.Macing,       bonus);
                sash.SkillBonuses.SetValues(1, SkillName.Tactics,      bonus);
                sash.SkillBonuses.SetValues(2, SkillName.Wrestling,    bonus);
            }
            else if (guild == FBGuilds.TheVoid)
            {
                sash.SkillBonuses.SetValues(0, SkillName.Magery,       bonus);
                sash.SkillBonuses.SetValues(1, SkillName.Necromancy,   bonus);
                sash.SkillBonuses.SetValues(2, SkillName.EvalInt,      bonus);
            }
            else if (guild == FBGuilds.Shadowblade)
            {
                sash.SkillBonuses.SetValues(0, SkillName.Fencing,      bonus);
                sash.SkillBonuses.SetValues(1, SkillName.Hiding,       bonus);
                sash.SkillBonuses.SetValues(2, SkillName.Poisoning,    bonus);
            }
        }

        // ── Fields ────────────────────────────────────────────────────────────────
        private string   _guildAffiliation;
        private SashTier _tier;

        [CommandProperty(AccessLevel.GameMaster)]
        public string GuildAffiliation
        {
            get => _guildAffiliation;
            set { _guildAffiliation = value; InvalidateProperties(); }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public SashTier Tier
        {
            get => _tier;
            set { _tier = value; InvalidateProperties(); }
        }

        // ── Constructors ─────────────────────────────────────────────────────────
        [Constructable]
        public GuildSash() : this(FBGuilds.Wanderers, SashTier.Standard) { }

        [Constructable]
        public GuildSash(string guildAffiliation) : this(guildAffiliation, SashTier.Standard) { }

        [Constructable]
        public GuildSash(string guildAffiliation, SashTier tier) : base(SashItemID)
        {
            _guildAffiliation = guildAffiliation ?? FBGuilds.Wanderers;
            _tier             = tier;

            Name   = $"{PrefixFor(_tier)}{_guildAffiliation} Sash";
            Hue    = HueFor(_guildAffiliation, _tier);
            Weight = 1.0;

            ApplySkillBonuses(this, _guildAffiliation, BonusFor(_tier));
        }

        public GuildSash(Serial serial) : base(serial) { }

        // ── Factory helper ───────────────────────────────────────────────────────
        /// <summary>
        /// Create a sash for the named guild at the given tier.
        /// Returns null if the guild name is not a known FBGuilds constant.
        /// </summary>
        public static GuildSash For(string guildName, SashTier tier = SashTier.Standard)
        {
            foreach (string g in FBGuilds.All)
            {
                if (string.Equals(g, guildName, StringComparison.OrdinalIgnoreCase))
                    return new GuildSash(g, tier);
            }
            return null;
        }

        // ── Properties tooltip ───────────────────────────────────────────────────
        public override void GetProperties(ObjectPropertyList list)
        {
            base.GetProperties(list);

            if (!string.IsNullOrEmpty(_guildAffiliation))
                list.Add($"Guild: {_guildAffiliation}");

            int bonus = (int)BonusFor(_tier);
            list.Add($"Skill Bonus: +{bonus} to three guild skills");
            list.Add($"Tier: {_tier}");
        }

        // ── Serialization ────────────────────────────────────────────────────────
        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write(1); // version
            writer.Write(_guildAffiliation);
            writer.Write((int)_tier);
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            int version = reader.ReadInt();

            _guildAffiliation = reader.ReadString();

            if (version >= 1)
                _tier = (SashTier)reader.ReadInt();
            else
                _tier = SashTier.Standard; // legacy saves were always Standard

            // BaseClothing.Deserialize restores SkillBonuses from the save file
            // automatically — no need to call ApplySkillBonuses again here.
        }
    }
}
