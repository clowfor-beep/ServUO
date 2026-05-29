// ============================================================
// RepTierBenefits.cs
// Scripts/Custom/RepTierBenefits.cs
//
// Fires tier-up rewards when a player's reputation with a guild
// crosses a threshold: Known → message, Trusted → Standard sash,
// Allied → Refined sash.
//
// Also gates Rare/Legendary quests on the Bounty Board behind rep,
// and provides a [myrep player command.
// ============================================================

using System;
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
        public static void Initialize()
        {
            FBEventBus.ReputationChanged += OnRepChanged;
            CommandSystem.Register("myrep", AccessLevel.Player, OnMyRepCommand);
        }

        // ──────────────────────────────────────────────────────────────────────
        // Tier-up handler
        // ──────────────────────────────────────────────────────────────────────

        private static void OnRepChanged(Mobile player, string guildName, int delta)
        {
            if (player == null || delta <= 0) return;

            int newStanding = ReputationSystem.GetStanding(player, guildName);
            int oldStanding = newStanding - delta;

            StandingTier oldTier = ReputationSystem.GetTier(oldStanding);
            StandingTier newTier = ReputationSystem.GetTier(newStanding);

            if (newTier <= oldTier) return; // no tier-up

            switch (newTier)
            {
                case StandingTier.Known:
                    player.SendMessage(0x53,
                        $"You are now Known to the {guildName}. Rare bounty quests are now available to you.");
                    break;

                case StandingTier.Trusted:
                    player.SendMessage(0x53,
                        $"You are now Trusted by the {guildName}. Legendary bounty quests are now available. You receive a guild sash as a mark of trust.");
                    if (!HasSash(player, guildName, SashTier.Standard))
                    {
                        GuildSash sash = GuildSash.For(guildName, SashTier.Standard);
                        if (sash != null && player.Backpack != null)
                            player.Backpack.DropItem(sash);
                    }
                    break;

                case StandingTier.Allied:
                    player.SendMessage(0x53,
                        $"You are now Allied with the {guildName} — the highest honour. Your sash has been upgraded.");
                    if (!HasSash(player, guildName, SashTier.Refined) &&
                        !HasSash(player, guildName, SashTier.Exalted))
                    {
                        GuildSash sash = GuildSash.For(guildName, SashTier.Refined);
                        if (sash != null && player.Backpack != null)
                            player.Backpack.DropItem(sash);
                    }
                    break;
            }
        }

        // Checks backpack and equipped items for a sash of this guild at or above minTier.
        private static bool HasSash(Mobile player, string guildName, SashTier minTier)
        {
            if (player.Backpack != null)
            {
                foreach (Item item in player.Backpack.Items)
                {
                    GuildSash s = item as GuildSash;
                    if (s != null && s.GuildAffiliation == guildName && s.Tier >= minTier)
                        return true;
                }
            }

            foreach (Item item in player.Items)
            {
                GuildSash s = item as GuildSash;
                if (s != null && s.GuildAffiliation == guildName && s.Tier >= minTier)
                    return true;
            }

            return false;
        }

        // ──────────────────────────────────────────────────────────────────────
        // [myrep command
        // ──────────────────────────────────────────────────────────────────────

        private static void OnMyRepCommand(CommandEventArgs e)
        {
            e.Mobile.SendGump(new MyRepGump(e.Mobile));
        }

        private class MyRepGump : Gump
        {
            public MyRepGump(Mobile from) : base(60, 60)
            {
                const int W    = 420;
                const int PadX = 16;
                const int rowH = 22;
                int       rows = FBGuilds.All.Length;
                int       H    = 78 + rows * rowH + 30;

                AddBackground(0, 0, W, H, 9200);
                AddAlphaRegion(2, 2, W - 4, H - 4);

                AddLabel(PadX, 12, 0x53, "Your Guild Standing");
                AddImageTiled(PadX, 30, W - PadX * 2, 1, 9264);

                // Column headers
                AddLabel(PadX,       44, 2119, "Guild");
                AddLabel(255, 44, 2119, "Tier");
                AddLabel(330, 44, 2119, "Standing");
                AddImageTiled(PadX, 62, W - PadX * 2, 1, 9264);

                int y = 70;
                foreach (string guild in FBGuilds.All)
                {
                    int standing = ReputationSystem.GetStanding(from, guild);
                    StandingTier tier = ReputationSystem.GetTier(standing);

                    int    tierHue;
                    string tierName;
                    switch (tier)
                    {
                        case StandingTier.Allied:  tierHue = 0x35;  tierName = "Allied";  break;
                        case StandingTier.Trusted: tierHue = 0x53;  tierName = "Trusted"; break;
                        case StandingTier.Known:   tierHue = 0x3B2; tierName = "Known";   break;
                        case StandingTier.Hostile: tierHue = 33;    tierName = "Hostile"; break;
                        default:                   tierHue = 1153;  tierName = "Neutral"; break;
                    }

                    // Trim very long guild names so they fit
                    string display = guild.Length > 28 ? guild.Substring(0, 28) : guild;

                    AddLabel(PadX, y, 1153,    display);
                    AddLabel(255, y, tierHue,  tierName);
                    AddLabel(330, y, tierHue,  standing.ToString());
                    y += rowH;
                }

                AddButton(W / 2 - 30, H - 26, 4005, 4007, 0, GumpButtonType.Reply, 0);
                AddLabel(W / 2 + 6,   H - 26, 2119, "Close");
            }
        }
    }
}
