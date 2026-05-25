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
        // -------------------------------------------------------
        // Ambient line tables
        // -------------------------------------------------------
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

        // -------------------------------------------------------
        // Fields
        // -------------------------------------------------------
        private readonly string _guildName;
        private DateTime _nextSpeechTime;

        public SimChatBrain(string guildName)
        {
            _guildName      = guildName;
            _nextSpeechTime = DateTime.UtcNow
                + TimeSpan.FromSeconds(Utility.RandomMinMax(15, 45));
        }

        // -------------------------------------------------------
        // TryAmbientSpeech
        // Called by SimPlayer.OnThink when state == Idle.
        // Returns true if speech was fired.
        // -------------------------------------------------------
        public bool TryAmbientSpeech(Mobile speaker)
        {
            if (DateTime.UtcNow < _nextSpeechTime) return false;

            if (speaker == null || speaker.Deleted
                || speaker.Map == null || speaker.Map == Map.Internal)
                return false;

            // Only speak if at least one other mobile is within 10 tiles
            bool anyoneNearby = false;
            foreach (Mobile m in speaker.GetMobilesInRange(10))
            {
                if (m != speaker && !m.Deleted && m.Alive)
                {
                    anyoneNearby = true;
                    break;
                }
            }
            if (!anyoneNearby) return false;

            string[] lines = GetAmbientLines();
            if (lines == null || lines.Length == 0) return false;

            speaker.Say(lines[Utility.Random(lines.Length)]);
            _nextSpeechTime = DateTime.UtcNow
                + TimeSpan.FromSeconds(Utility.RandomMinMax(30, 90));
            return true;
        }

        // -------------------------------------------------------
        // Helpers
        // -------------------------------------------------------
        private string[] GetAmbientLines()
        {
            if (_guildName == FBGuilds.Wanderers) return WandererAmbient;
            // Other guilds added in later phases
            return WandererAmbient; // fallback
        }
    }
}
