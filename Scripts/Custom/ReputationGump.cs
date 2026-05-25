// ============================================================
// ReputationGump.cs
// Scripts/Custom/ReputationGump.cs
//
// Player-facing gump showing standing with all 12 FB guilds.
// Command: [repgump  (AccessLevel.Player, self only)
//
// Layout: 300 × 370 px, dark background.
// Each guild row shows the name (silver) and standing + tier
// in a colour matching the tier.
// ============================================================

using System;
using Server;
using Server.Commands;
using Server.Custom;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Server.Gumps
{
    public class ReputationGump : Gump
    {
        // ---- Layout ----
        private const int GumpWidth  = 300;
        private const int GumpHeight = 370;

        // ---- Hues ----
        private const int BgHue        = 9270;  // dark background
        private const int TitleHue     = 1258;  // gold
        private const int GuildNameHue = 2100;  // silver
        private const int HueAllied    = 63;    // green
        private const int HueTrusted   = 1154;  // bright blue
        private const int HueKnown     = 1153;  // white
        private const int HueNeutral   = 2100;  // grey
        private const int HueHostile   = 33;    // red

        // ---- Command registration ----
        public static void Initialize()
        {
            CommandSystem.Register("repgump", AccessLevel.Player, OnRepGump);
        }

        private static void OnRepGump(CommandEventArgs e)
        {
            e.Mobile.CloseGump(typeof(ReputationGump));
            e.Mobile.SendGump(new ReputationGump(e.Mobile));
        }

        // ---- Constructor ----
        public ReputationGump(Mobile owner) : base(50, 50)
        {
            AddPage(0);

            // Background panel
            AddBackground(0, 0, GumpWidth, GumpHeight, BgHue);

            // Title
            AddLabel(GumpWidth / 2 - 55, 8, TitleHue, "Guild Standings");

            // Refresh button (top-right)
            AddButton(GumpWidth - 68, 6, 4005, 4007, 1, GumpButtonType.Reply, 0);
            AddLabel(GumpWidth - 52, 8, GuildNameHue, "Refresh");

            // Guild rows — 12 guilds, 27 px per row, starting at y=35
            int y = 35;
            foreach (string guild in FBGuilds.All)
            {
                int          standing = ReputationSystem.GetStanding(owner, guild);
                StandingTier tier     = ReputationSystem.GetTier(standing);
                int          hue      = HueForTier(tier);

                // Guild name in silver
                AddLabel(10, y, GuildNameHue, guild);

                // Standing value + tier name in tier colour
                AddLabel(10, y + 13, hue, $"{standing}  ({tier})");

                y += 27;
            }
        }

        // ---- Tier → hue mapping ----
        private static int HueForTier(StandingTier tier)
        {
            switch (tier)
            {
                case StandingTier.Allied:  return HueAllied;
                case StandingTier.Trusted: return HueTrusted;
                case StandingTier.Known:   return HueKnown;
                case StandingTier.Hostile: return HueHostile;
                default:                   return HueNeutral; // Neutral
            }
        }

        // ---- Button handler ----
        public override void OnResponse(NetState sender, RelayInfo info)
        {
            if (info.ButtonID == 1) // Refresh
            {
                Mobile m = sender.Mobile;
                if (m != null && !m.Deleted)
                {
                    m.CloseGump(typeof(ReputationGump));
                    m.SendGump(new ReputationGump(m));
                }
            }
        }
    }
}
