// ============================================================
// SimChatBrain.cs
// Scripts/Custom/SimChatBrain.cs
//
// Manages ambient speech for SimPlayers.
// Phase 1: ambient lines only. Reactive/combat speech in Phase 2+.
//
// Called by SimPlayer.OnThink when state == Idle.
// Only speaks if at least one other mobile is within 10 tiles.
// ============================================================

using System;
using Server;
using Server.Custom;

namespace Server.Custom
{
    public class SimChatBrain
    {
        // ── Wanderers ─────────────────────────────────────────────────────
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

        // ── Craftsmen's League ────────────────────────────────────────────
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

        // ── Iron Company ──────────────────────────────────────────────────
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

        // ── Arcane Brotherhood ────────────────────────────────────────────
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

        // ── Silver Wolves ─────────────────────────────────────────────────
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

        // ── Fields ────────────────────────────────────────────────────────
        private readonly string _guildName;
        private DateTime _nextSpeechTime;

        public SimChatBrain(string guildName)
        {
            _guildName      = guildName;
            _nextSpeechTime = DateTime.UtcNow
                + TimeSpan.FromSeconds(Utility.RandomMinMax(15, 45));
        }

        // ── TryAmbientSpeech ──────────────────────────────────────────────
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

        // ── Helpers ───────────────────────────────────────────────────────
        private string[] GetAmbientLines()
        {
            if (_guildName == FBGuilds.Wanderers)         return WandererAmbient;
            if (_guildName == FBGuilds.CraftsmenLeague)   return CraftsmensLeagueAmbient;
            if (_guildName == FBGuilds.IronCompany)       return IronCompanyAmbient;
            if (_guildName == FBGuilds.ArcaneBrotherhood) return ArcaneBrotherhoodAmbient;
            if (_guildName == FBGuilds.SilverWolves)      return SilverWolvesAmbient;
            return WandererAmbient; // fallback
        }
    }
}
