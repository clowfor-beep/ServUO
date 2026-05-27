// ============================================================
// SimChatBrain.cs
// Scripts/Custom/SimChatBrain.cs
//
// Manages ambient speech for SimPlayers.
// Phase 1: ambient lines only.
// Phase 2: bank speech pool, Shadow Hand ambient lines,
//          TryBankSpeech() method.
//
// Called by SimPlayer.OnThink:
//   - TryAmbientSpeech when state == Idle
//   - TryBankSpeech   when state == Banking
// Only speaks if at least one other mobile is within 10 tiles.
// ============================================================

using System;
using Server;
using Server.Custom;

namespace Server.Custom
{
    public class SimChatBrain
    {
        // -- Wanderers ------------------------------------------------
        private static readonly string[] WandererAmbient = {
            "Quite a journey getting here...",
            "Anyone know where I can find a good healer?",
            "This shard has been busy lately.",
            "The roads to the dungeons are dangerous these days.",
            "I heard there are hunters tracking rare creatures.",
            "Have you seen any Blood Pact around here?",
            "Good day, traveller.",
            "Stay safe out there.",
            "The Britain bank is always a good place to meet people.",
            "I could use a few more bandages before heading out.",
        };

        // -- Craftsmen's League ---------------------------------------
        private static readonly string[] CraftsmensLeagueAmbient = {
            "These ingots won't smelt themselves.",
            "Anyone need repairs? I can fix that armour.",
            "The forge has been busy today.",
            "Good ore is getting harder to find near Britain.",
            "I heard the mines near Wrong are rich but dangerous.",
            "A well-made blade lasts a lifetime. Most people forget that.",
            "Selling leather? I'll pay fair.",
            "My back's killing me from the anvil work.",
        };

        // -- Iron Company ---------------------------------------------
        private static readonly string[] IronCompanyAmbient = {
            "Hold the line!",
            "Blood Pact spotted near Destard yesterday.",
            "Anyone heading to a champion spawn? We're forming up.",
            "Support on my position.",
            "Keep formation. Always.",
            "We lost two at the last spawn. Not again.",
            "Iron Company doesn't run. Remember that.",
            "The spawn resets in an hour -- be ready.",
        };

        // -- Arcane Brotherhood ---------------------------------------
        private static readonly string[] ArcaneBrotherhoodAmbient = {
            "The deeper circles require absolute focus.",
            "These reagent prices in Moonglow are robbery.",
            "I've been studying a new inscription pattern.",
            "The Void grows bolder. We must remain vigilant.",
            "Magic is precision. Never forget that.",
            "Deceit level three is cleaner since we swept it last week.",
            "Have you read the latest transcription from the archive?",
            "Mana recovery has been slower than usual today.",
        };

        // -- Silver Wolves --------------------------------------------
        private static readonly string[] SilverWolvesAmbient = {
            "Stay alert. Blood Pact could be anywhere.",
            "We protect the blue. That's the oath.",
            "If you see red names near Britain, call it out.",
            "The Shadow Hand was spotted near the bank earlier.",
            "Always move in threes. Never alone.",
            "Someone's been watching the bank. Not one of ours.",
            "Clear skies mean nothing in this city.",
            "We run toward danger, not away from it.",
        };

        // -- Shadow Hand ----------------------------------------------
        private static readonly string[] ShadowHandAmbient = {
            "Just passing through.",
            "Nice weather today.",
            "Have you seen the new weapons at the smithy?",
            "I'm looking for someone.",
            "Don't mind me.",
        };

        // -- Paladin Order --------------------------------------------
        private static readonly string[] PaladinOrderAmbient = {
            "Evil will not stand while we draw breath.",
            "Honour, duty, sacrifice -- these are not suggestions.",
            "Have you seen anything suspicious near the city gates?",
            "The Blood Pact grows bold. We will answer.",
            "By the Virtues, this city will not fall.",
            "Every red name near Britain is an affront to justice.",
        };

        // -- Dead Watchers --------------------------------------------
        private static readonly string[] DeadWatchersAmbient = {
            "We watch. We wait. We end.",
            "The living do not see what moves in the dark.",
            "Every soul passes through. We are there when it does.",
            "Judge not the dead -- they did not ask for this.",
            "There is peace in the silence between heartbeats.",
            "Speak carefully. The wrong words linger long after death.",
        };

        // -- Dread Hunters --------------------------------------------
        private static readonly string[] DreadHuntersAmbient = {
            "The deep dungeons hold no mysteries for us.",
            "Tier four clears in under six minutes when we focus.",
            "Call your target before you engage. Every time.",
            "I've hunted worse than this in Hythloth level three.",
            "Coordination wins fights. Ego loses them.",
            "We don't hunt for glory. We hunt because someone has to.",
        };

        // -- Blood Pact -----------------------------------------------
        private static readonly string[] BloodPactAmbient = {
            "They should not have come here alone.",
            "The ritual demands payment. It always does.",
            "Power is not given. It is taken.",
            "Curious... you are still breathing.",
            "The pact was made long before you arrived.",
            "Every drop of blood is a contract fulfilled.",
        };

        // -- The Void -------------------------------------------------
        private static readonly string[] TheVoidAmbient = {
            "Everything burns. Everything ends.",
            "The silence is loudest just before the collapse.",
            "You were never really here.",
            "Names mean nothing. Only the Void remains.",
            "I have seen the edge of what is. There is nothing.",
            "Come closer. Or don't. It does not matter.",
        };

        // -- Shadowblade ----------------------------------------------
        private static readonly string[] ShadowbladeAmbient = {
            "Contract accepted.",
            "The client asked for silence. I provide it.",
            "I was never here.",
            "Precision costs more. It is worth it.",
            "Amateur work leaves witnesses.",
            "The job is already done. You just don't know it yet.",
        };

        // -- Bank chat -- all guilds when in Banking state ------------
        private static readonly string[] BankAmbient = {
            "WTS 10k iron ingots, 2gp each. PM me.",
            "LFG Hythloth -- need one more mage.",
            "Anyone selling a GM katana?",
            "Heard there's a PK camping near Despise.",
            "Nice suit. What hue is that armour?",
            "Selling leather, 5gp per hide.",
            "Is the Britain moongate still clear?",
            "Anyone need a res? I've got bandages.",
            "WTB valorite ore. Good price.",
            "Three Blood Pact in Tier 2 last night. Be careful.",
            "The Silver Wolves were at Destard earlier.",
            "LFG champion spawn -- Iron Company forming up.",
            "Anyone know a good house location for sale?",
            "The bank is unusually quiet today.",
            "WTS scrolls -- Power, Energy Bolt, Flamestrike.",
        };

        // -- Fields ---------------------------------------------------
        private readonly string _guildName;
        private DateTime _nextSpeechTime;

        public SimChatBrain(string guildName)
        {
            _guildName      = guildName;
            _nextSpeechTime = DateTime.UtcNow
                + TimeSpan.FromSeconds(Utility.RandomMinMax(15, 45));
        }

        // -- TryAmbientSpeech -----------------------------------------
        // Called by SimPlayer.OnThink when state == Idle.
        // Returns true if speech was fired.
        public bool TryAmbientSpeech(Mobile speaker)
        {
            if (DateTime.UtcNow < _nextSpeechTime) return false;

            if (speaker == null || speaker.Deleted
                || speaker.Map == null || speaker.Map == Map.Internal)
                return false;

            bool anyoneNearby = false;
            foreach (Mobile m in speaker.GetMobilesInRange(10))
            {
                if (m != speaker && !m.Deleted && m.Alive)
                { anyoneNearby = true; break; }
            }
            if (!anyoneNearby) return false;

            string[] lines = GetAmbientLines();
            if (lines == null || lines.Length == 0) return false;

            speaker.Say(lines[Utility.Random(lines.Length)]);
            _nextSpeechTime = DateTime.UtcNow
                + TimeSpan.FromSeconds(Utility.RandomMinMax(30, 90));
            return true;
        }

        // -- TryBankSpeech --------------------------------------------
        // Called by SimPlayer.OnThink when state == Banking.
        // Much lower frequency than ambient -- bank chat is occasional.
        public bool TryBankSpeech(Mobile speaker)
        {
            if (DateTime.UtcNow < _nextSpeechTime) return false;

            if (speaker == null || speaker.Deleted
                || speaker.Map == null || speaker.Map == Map.Internal)
                return false;

            bool anyoneNearby = false;
            foreach (Mobile m in speaker.GetMobilesInRange(8))
            {
                if (m != speaker && !m.Deleted)
                { anyoneNearby = true; break; }
            }
            if (!anyoneNearby) return false;

            // Bank chat is rarer -- 20% chance to speak per window
            if (Utility.RandomDouble() > 0.20)
            {
                _nextSpeechTime = DateTime.UtcNow
                    + TimeSpan.FromSeconds(Utility.RandomMinMax(30, 60));
                return false;
            }

            string[] lines = BankAmbient;
            if (lines == null || lines.Length == 0) return false;

            speaker.Say(lines[Utility.Random(lines.Length)]);
            _nextSpeechTime = DateTime.UtcNow
                + TimeSpan.FromSeconds(Utility.RandomMinMax(60, 180));
            return true;
        }

        // -- Helpers --------------------------------------------------
        private string[] GetAmbientLines()
        {
            if (_guildName == FBGuilds.Wanderers)         return WandererAmbient;
            if (_guildName == FBGuilds.CraftsmenLeague)   return CraftsmensLeagueAmbient;
            if (_guildName == FBGuilds.IronCompany)       return IronCompanyAmbient;
            if (_guildName == FBGuilds.ArcaneBrotherhood) return ArcaneBrotherhoodAmbient;
            if (_guildName == FBGuilds.SilverWolves)      return SilverWolvesAmbient;
            if (_guildName == FBGuilds.ShadowHand)        return ShadowHandAmbient;
            if (_guildName == FBGuilds.PaladinOrder)      return PaladinOrderAmbient;
            if (_guildName == FBGuilds.DeadWatchers)      return DeadWatchersAmbient;
            if (_guildName == FBGuilds.DreadHunters)      return DreadHuntersAmbient;
            if (_guildName == FBGuilds.BloodPact)         return BloodPactAmbient;
            if (_guildName == FBGuilds.TheVoid)           return TheVoidAmbient;
            if (_guildName == FBGuilds.Shadowblade)       return ShadowbladeAmbient;
            return WandererAmbient; // fallback
        }
    }
}
