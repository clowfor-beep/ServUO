// ============================================================
// PKTestCommand.cs
// Scripts/Custom/PKTestCommand.cs
//
// Staff command: [pktest
//
// Forces a PKEncounterSystem encounter on yourself or a named
// player, bypassing all cooldown / zone / access-level guards.
//
// Usage (in-game, GameMaster or above):
//   [pktest                    <- trigger based on your location
//   [pktest newbie             <- force Newbie tier on yourself
//   [pktest advanced           <- force Advanced tier on yourself
//   [pktest expert             <- force Expert tier on yourself
//   [pktest <name>             <- trigger on named player (location-based tier)
//   [pktest <name> newbie      <- force Newbie tier on named player
//   [pktest <name> advanced    <- force Advanced tier on named player
//   [pktest <name> expert      <- force Expert tier on named player
// ============================================================

using System;
using Server;
using Server.Commands;
using Server.Mobiles;
using Server.Network;

namespace Server.Custom
{
    public static class PKTestCommand
    {
        public static void Initialize()
        {
            CommandSystem.Register("pktest", AccessLevel.GameMaster, OnCommand);
        }

        private static void OnCommand(CommandEventArgs e)
        {
            PlayerMobile target    = null;
            PKTier?      forceTier = null;

            // Parse arguments:
            //   [pktest
            //   [pktest newbie|advanced|expert
            //   [pktest <playername>
            //   [pktest <playername> newbie|advanced|expert

            string arg0 = e.Arguments.Length > 0 ? e.GetString(0).ToLower() : null;
            string arg1 = e.Arguments.Length > 1 ? e.GetString(1).ToLower() : null;

            if (arg0 != null && IsTierKeyword(arg0))
            {
                // [pktest newbie/advanced/expert — tier only, target self
                forceTier = ParseTier(arg0);
                target    = e.Mobile as PlayerMobile;
            }
            else if (arg0 != null)
            {
                // [pktest <name>  or  [pktest <name> tier
                foreach (NetState ns in NetState.Instances)
                {
                    if (ns.Mobile is PlayerMobile pm &&
                        pm.Name.Equals(arg0, StringComparison.OrdinalIgnoreCase))
                    {
                        target = pm;
                        break;
                    }
                }

                if (target == null)
                {
                    e.Mobile.SendMessage(0x26, $"[pktest] No online player named \"{arg0}\" found.");
                    return;
                }

                if (arg1 != null && IsTierKeyword(arg1))
                    forceTier = ParseTier(arg1);
            }
            else
            {
                // No arguments — target self
                target = e.Mobile as PlayerMobile;
            }

            if (target == null)
            {
                e.Mobile.SendMessage(0x26, "[pktest] You must be a PlayerMobile to test this.");
                return;
            }

            string tierLabel = forceTier.HasValue ? forceTier.Value.ToString() : "location-based";
            e.Mobile.SendMessage(0x35, $"[pktest] Forcing {tierLabel} PK encounter on {target.Name}...");

            PKEncounterSystem.ForceEncounter(target, forceTier);
        }

        private static bool IsTierKeyword(string s)
        {
            return s == "newbie" || s == "advanced" || s == "expert";
        }

        private static PKTier ParseTier(string s)
        {
            switch (s)
            {
                case "advanced": return PKTier.Advanced;
                case "expert":   return PKTier.Expert;
                default:         return PKTier.Newbie;
            }
        }
    }
}
