// ============================================================
// RepTierBenefits.cs
// Scripts/Custom/RepTierBenefits.cs
// ============================================================
//
// Fires tier-up messages and sash awards when a player's
// reputation with a guild crosses a threshold for the first time.
//
// Also registers the [myrep player command which opens a gump
// showing the player's standing toward every guild.
//
// Stateless — no Serialize/Deserialize, no Serial constructor.
// ============================================================

using Server;
using Server.Commands;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Server.Custom
{
    public static class RepTierBenefits
    {
        // ── Startup hook ────────────────────────────────────────────────────────

        public static void Initialize()
        {
            FBEventBus.ReputationChanged += OnRepChanged;
            CommandSystem.Register("myrep", AccessLevel.Player, OnMyRepCommand);
        }

        // ── Tier-up handler ─────────────────────────────────────────────────────

        private static void OnRepChanged(Mobile player, string guildName, int delta)
        {
            if (player == null || string.IsNullOrEmpty(guildName)) return;

            int          newStanding = ReputationSystem.GetStanding(player, guildName);
            int          oldStanding = newStanding - delta;
            StandingTier oldTier     = ReputationSystem.GetTier(oldStanding);
            StandingTier newTier     = ReputationSystem.GetTier(newStanding);

            // Only fire on tier-up — ignore tier-down and same-tier deltas
            if (newTier <= oldTier) return;

            switch (newTier)
            {
                case StandingTier.Known:
                    player.SendMessage(0x53,
                        $"You are now Known to the {guildName}. Rare bounty quests are now available to you.");
                    break;

                case StandingTier.Trusted:
                    player.SendMessage(0x53,
                        $"You are now Trusted by the {guildName}. Legendary bounty quests are now available. You receive a guild sash as a mark of trust.");
                    // Award Standard sash if player holds no sash at all for this guild
                    AwardSash(player, guildName, SashTier.Standard, SashTier.Standard);
                    break;

                case StandingTier.Allied:
                    player.SendMessage(0x53,
                        $"You are now Allied with the {guildName} — the highest honour. Your sash has been upgraded.");
                    // Award Refined sash only if player has no Refined or Exalted sash for this guild
                    AwardSash(player, guildName, SashTier.Refined, SashTier.Refined);
                    break;
            }
        }

        // ── Sash award helper ───────────────────────────────────────────────────

        /// <summary>
        /// Drops <paramref name="awardTier"/> sash into player's backpack, unless they already
        /// carry a sash for this guild at <paramref name="skipIfAtLeast"/> tier or higher.
        /// </summary>
        private static void AwardSash(Mobile player, string guildName,
                                       SashTier awardTier, SashTier skipIfAtLeast)
        {
            if (player.Backpack == null) return;

            if (HasSashAtOrAbove(player, guildName, skipIfAtLeast)) return;

            GuildSash sash = GuildSash.For(guildName, awardTier);
            if (sash == null) return;

            player.Backpack.DropItem(sash);
        }

        /// <summary>
        /// Returns true if the player holds any GuildSash for <paramref name="guildName"/>
        /// whose tier is at or above <paramref name="minTier"/>.
        /// Checks both backpack contents and equipped item layers.
        /// </summary>
        private static bool HasSashAtOrAbove(Mobile player, string guildName, SashTier minTier)
        {
            // Check backpack
            if (player.Backpack != null)
            {
                foreach (Item item in player.Backpack.Items)
                {
                    GuildSash s = item as GuildSash;
                    if (s != null && s.GuildAffiliation == guildName && s.Tier >= minTier)
                        return true;
                }
            }

            // Check equipped layers
            foreach (Item item in player.Items)
            {
                GuildSash s = item as GuildSash;
                if (s != null && s.GuildAffiliation == guildName && s.Tier >= minTier)
                    return true;
            }

            return false;
        }

        // ── [myrep command ──────────────────────────────────────────────────────

        private static void OnMyRepCommand(CommandEventArgs e)
        {
            e.Mobile.SendGump(new MyRepGump(e.Mobile));
        }

        // ── MyRepGump ───────────────────────────────────────────────────────────

        private class MyRepGump : Gump
        {
            // Layout constants
            private const int W       = 420;
            private const int PadX    = 18;
            private const int RowH    = 22;
            private const int HeaderH = 56;   // title + column labels
            private const int FootH   = 20;

            public MyRepGump(Mobile from) : base(60, 50)
            {
                int rows = FBGuilds.All.Length;
                int h    = HeaderH + rows * RowH + FootH;

                AddBackground(0, 0, W, h, 9270);
                AddAlphaRegion(2, 2, W - 4, h - 4);

                // ── Title ────────────────────────────────────────────────────────
                AddLabel(PadX, 12, 0x4AA, "Your Guild Standing");

                // Close button (button ID 0)
                AddButton(W - 40, 10, 4017, 4019, 0, GumpButtonType.Reply, 0);

                // ── Column headers ───────────────────────────────────────────────
                int y = 32;
                AddLabel(PadX,       y, 2119, "Guild");
                AddLabel(PadX + 210, y, 2119, "Tier");
                AddLabel(PadX + 300, y, 2119, "Standing");
                y += 20;

                // ── Guild rows ───────────────────────────────────────────────────
                foreach (string guildName in FBGuilds.All)
                {
                    int          standing = ReputationSystem.GetStanding(from, guildName);
                    StandingTier tier     = ReputationSystem.GetTier(standing);

                    string tierName;
                    int    tierHue;

                    switch (tier)
                    {
                        case StandingTier.Allied:
                            tierName = "Allied";
                            tierHue  = 0x35;
                            break;
                        case StandingTier.Trusted:
                            tierName = "Trusted";
                            tierHue  = 0x53;
                            break;
                        case StandingTier.Known:
                            tierName = "Known";
                            tierHue  = 0x3B2;
                            break;
                        case StandingTier.Hostile:
                            tierName = "Hostile";
                            tierHue  = 33;
                            break;
                        default:
                            tierName = "Neutral";
                            tierHue  = 0xFFFF;
                            break;
                    }

                    AddLabel(PadX,       y, 2119,    guildName);
                    AddLabel(PadX + 210, y, tierHue, tierName);
                    AddLabel(PadX + 300, y, tierHue, $"({standing})");

                    y += RowH;
                }
            }
        }
    }
}
