// ============================================================
// PKTestCommand.cs
// Scripts/Custom/PKTestCommand.cs
//
// Staff command: [pktest
//
// Forces a GraveyardPKEncounter to trigger on yourself or a
// targeted player, bypassing all cooldown / map / access-level
// guards. Useful for testing the NovicePlayerKiller spawn
// without needing a player account in Felucca.
//
// Usage (in-game, GameMaster or above):
//   [pktest          <- triggers on yourself
//   [pktest <name>   <- triggers on a named online player
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
            PlayerMobile target = null;

            if (e.Arguments.Length > 0)
            {
                // [pktest <player name>
                string name = e.GetString(0);

                foreach (NetState ns in NetState.Instances)
                {
                    if (ns.Mobile is PlayerMobile pm &&
                        pm.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    {
                        target = pm;
                        break;
                    }
                }

                if (target == null)
                {
                    e.Mobile.SendMessage(0x26, $"[pktest] No online player named \"{name}\" found.");
                    return;
                }
            }
            else
            {
                // No argument — target the command user themselves
                target = e.Mobile as PlayerMobile;

                if (target == null)
                {
                    e.Mobile.SendMessage(0x26, "[pktest] You must be a PlayerMobile to test this.");
                    return;
                }
            }

            e.Mobile.SendMessage(0x35, $"[pktest] Forcing PK encounter on {target.Name}...");
            GraveyardPKEncounter.ForceEncounter(target);
        }
    }
}
