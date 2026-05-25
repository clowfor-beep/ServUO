// ============================================================
// ReputationSystem.cs
// Scripts/Custom/ReputationSystem.cs
//
// Tracks each player's standing with the 12 Forsaken Britannia
// guilds. Persists to Saves/Misc/ReputationSystem.bin.
//
// Standing tiers:
//   < 0       Hostile
//   0–99      Neutral  (default)
//   100–299   Known
//   300–599   Trusted
//   600+      Allied
// ============================================================

using System;
using System.Collections.Generic;
using System.IO;
using Server;
using Server.Commands;
using Server.Custom;
using Server.Mobiles;
using Server.Network;
using Server.Targeting;

namespace Server.Custom
{
    // -------------------------------------------------------
    // Guild name constants
    // -------------------------------------------------------
    public static class FBGuilds
    {
        public const string Wanderers         = "The Wanderers";
        public const string CraftsmenLeague   = "The Craftsmen's League";
        public const string ShadowHand        = "The Shadow Hand";
        public const string IronCompany       = "Iron Company";
        public const string ArcaneBrotherhood = "The Arcane Brotherhood";
        public const string SilverWolves      = "The Silver Wolves";
        public const string PaladinOrder      = "The Paladin Order";
        public const string DeadWatchers      = "The Dead Watchers";
        public const string DreadHunters      = "The Dread Hunters";
        public const string BloodPact         = "Blood Pact";
        public const string TheVoid           = "The Void";
        public const string Shadowblade       = "Shadowblade";

        public static readonly string[] All = {
            Wanderers, CraftsmenLeague, ShadowHand, IronCompany,
            ArcaneBrotherhood, SilverWolves, PaladinOrder, DeadWatchers,
            DreadHunters, BloodPact, TheVoid, Shadowblade
        };
    }

    // -------------------------------------------------------
    // Standing tiers
    // -------------------------------------------------------
    public enum StandingTier { Hostile, Neutral, Known, Trusted, Allied }

    // -------------------------------------------------------
    // Reputation System
    // -------------------------------------------------------
    public static class ReputationSystem
    {
        // ---- Storage ----
        // Key = player Serial, value = dict of guild name → standing int.
        // Missing entry = 0 (Neutral). Zero is never stored explicitly.
        private static readonly Dictionary<Serial, Dictionary<string, int>> _standings =
            new Dictionary<Serial, Dictionary<string, int>>();

        // ---- Persistence ----
        private static readonly string SavePath =
            Path.Combine("Saves/Misc", "ReputationSystem.bin");

        // -------------------------------------------------------
        // Initialize — auto-called at startup
        // -------------------------------------------------------
        public static void Initialize()
        {
            LoadData();

            EventSink.WorldSave   += OnWorldSave;
            EventSink.PlayerDeath += OnPlayerDeath;

            FBEventBus.PlayerKilledSimPlayer += OnPlayerKilledSimPlayer;
            FBEventBus.SimPlayerKilledPlayer += OnSimPlayerKilledPlayer;
            FBEventBus.PoolPKKilled          += OnPoolPKKilled;

            CommandSystem.Register("repcheck", AccessLevel.GameMaster, OnRepCheck);
            CommandSystem.Register("repset",   AccessLevel.GameMaster, OnRepSet);
        }

        // -------------------------------------------------------
        // Public API
        // -------------------------------------------------------

        /// <summary>Returns the player's current standing with a guild (0 if no record).</summary>
        public static int GetStanding(Mobile from, string guild)
        {
            Dictionary<string, int> dict;
            if (!_standings.TryGetValue(from.Serial, out dict)) return 0;
            int value;
            return dict.TryGetValue(guild, out value) ? value : 0;
        }

        /// <summary>Returns the StandingTier for a player's standing with a guild.</summary>
        public static StandingTier GetTier(Mobile from, string guild)
        {
            return GetTier(GetStanding(from, guild));
        }

        /// <summary>Returns the StandingTier for a raw standing value.</summary>
        public static StandingTier GetTier(int standing)
        {
            if (standing < 0)   return StandingTier.Hostile;
            if (standing < 100) return StandingTier.Neutral;
            if (standing < 300) return StandingTier.Known;
            if (standing < 600) return StandingTier.Trusted;
            return StandingTier.Allied;
        }

        /// <summary>
        /// Add delta to a player's standing with a guild.
        /// Clamps to minimum -500. No explicit maximum.
        /// Fires FBEventBus.ReputationChanged and sends a coloured message to the player.
        /// </summary>
        public static void AddStanding(Mobile from, string guild, int delta)
        {
            if (from == null || delta == 0) return;

            int current = GetStanding(from, guild);
            int newVal  = Math.Max(-500, current + delta);

            SetRaw(from.Serial, guild, newVal);

            // Notify the player if they are online
            if (from.NetState != null)
            {
                if (delta > 0)
                    from.SendMessage(0x3B2, $"Your standing with {guild} has improved. (+{delta})");
                else
                    from.SendMessage(0x22, $"Your standing with {guild} has decreased. ({delta})");
            }

            FBEventBus.Fire_ReputationChanged(from, guild, delta);
        }

        /// <summary>Set standing directly. For GM use only — no clamping, no message.</summary>
        public static void SetStanding(Mobile from, string guild, int value)
        {
            if (from == null) return;
            SetRaw(from.Serial, guild, value);
        }

        // -------------------------------------------------------
        // Internal helpers
        // -------------------------------------------------------

        private static void SetRaw(Serial serial, string guild, int value)
        {
            if (value == 0)
            {
                // Never store 0 — remove the key to keep storage clean
                Dictionary<string, int> dict;
                if (_standings.TryGetValue(serial, out dict))
                {
                    dict.Remove(guild);
                    if (dict.Count == 0)
                        _standings.Remove(serial);
                }
                return;
            }

            if (!_standings.ContainsKey(serial))
                _standings[serial] = new Dictionary<string, int>();

            _standings[serial][guild] = value;
        }

        /// <summary>
        /// Determine which guild a SimPlayer NPC belongs to.
        /// SimPlayer class is not yet implemented — detect by GuildName property (future)
        /// or by Title matching a known guild name (interim fallback).
        /// Returns null if not identifiable as a SimPlayer.
        /// </summary>
        private static string GetSimGuild(Mobile m)
        {
            if (m == null || !(m is BaseCreature)) return null;

            // Check for a GuildName property (SimPlayer will expose this)
            var prop = m.GetType().GetProperty("GuildName");
            if (prop != null)
            {
                string val = prop.GetValue(m, null) as string;
                if (!string.IsNullOrEmpty(val)) return val;
            }

            // Fall back to Title if it matches a known guild name exactly
            if (!string.IsNullOrEmpty(m.Title))
            {
                foreach (string guild in FBGuilds.All)
                {
                    if (m.Title == guild) return guild;
                }
            }

            return null;
        }

        // -------------------------------------------------------
        // EventSink.PlayerDeath — real player kills real player
        // -------------------------------------------------------
        private static void OnPlayerDeath(PlayerDeathEventArgs e)
        {
            PlayerMobile killer = e.Killer as PlayerMobile;
            PlayerMobile victim = e.Mobile as PlayerMobile;

            if (killer == null || victim == null) return;
            if (killer == victim) return;
            if (killer.AccessLevel > AccessLevel.Player) return;
            if (victim.AccessLevel > AccessLevel.Player) return;

            AddStanding(killer, FBGuilds.SilverWolves, -20);
            AddStanding(killer, FBGuilds.PaladinOrder, -15);
        }

        // -------------------------------------------------------
        // FBEventBus.PoolPKKilled — player kills a PoolPK
        // -------------------------------------------------------
        private static void OnPoolPKKilled(Mobile pk, Mobile killer, SpawnZone zone)
        {
            PlayerMobile pm = killer as PlayerMobile;
            if (pm == null) return;

            AddStanding(pm, FBGuilds.SilverWolves, +5);
            AddStanding(pm, FBGuilds.PaladinOrder, +5);
        }

        // -------------------------------------------------------
        // FBEventBus.PlayerKilledSimPlayer — player kills a SimPlayer NPC
        // -------------------------------------------------------
        private static void OnPlayerKilledSimPlayer(Mobile killer, Mobile victim)
        {
            PlayerMobile pm = killer as PlayerMobile;
            if (pm == null) return;

            string simGuild = GetSimGuild(victim);
            if (simGuild == null) return;

            if (simGuild == FBGuilds.BloodPact)
            {
                AddStanding(pm, FBGuilds.SilverWolves, +10);
                AddStanding(pm, FBGuilds.PaladinOrder, +15);
            }
            else if (simGuild == FBGuilds.TheVoid)
            {
                AddStanding(pm, FBGuilds.ArcaneBrotherhood, +10);
                AddStanding(pm, FBGuilds.DreadHunters,      +10);
            }
            else if (simGuild == FBGuilds.Shadowblade)
            {
                AddStanding(pm, FBGuilds.PaladinOrder, +10);
                AddStanding(pm, FBGuilds.SilverWolves, +5);
            }
        }

        // -------------------------------------------------------
        // FBEventBus.SimPlayerKilledPlayer — stub (no change for now)
        // -------------------------------------------------------
        private static void OnSimPlayerKilledPlayer(Mobile killer, Mobile victim)
        {
            // Blood Pact killing a player: no reputation change.
            // Blood Pact killing you is just part of the world.
            // Stub preserved for future expansion.
            //
            // PlayerMobile pm = victim as PlayerMobile;
            // if (pm == null) return;
            // string simGuild = GetSimGuild(killer);
            // if (simGuild == Guilds.BloodPact)
            //     ; // No change
        }

        // -------------------------------------------------------
        // Stub — Champion spawn assist
        // FBEventBus.ChampionSpawnCompleted — +15 IronCompany for nearby players
        // Not yet implemented — requires SimPlayer system
        // -------------------------------------------------------

        // -------------------------------------------------------
        // GM Commands
        // -------------------------------------------------------

        private static void OnRepCheck(CommandEventArgs e)
        {
            e.Mobile.SendMessage("Target a player to view their reputation standings.");
            e.Mobile.Target = new RepCheckTarget();
        }

        private sealed class RepCheckTarget : Target
        {
            public RepCheckTarget() : base(15, false, TargetFlags.None) { }

            protected override void OnTarget(Mobile from, object targeted)
            {
                Mobile target = targeted as Mobile;
                if (target == null)
                {
                    from.SendMessage("That is not a mobile.");
                    return;
                }

                from.SendMessage($"--- Reputation: {target.Name} ---");
                foreach (string guild in FBGuilds.All)
                {
                    int standing          = GetStanding(target, guild);
                    StandingTier tier     = GetTier(standing);
                    from.SendMessage($"{guild}: {standing} ({tier})");
                }
            }

            protected override void OnTargetCancel(Mobile from, TargetCancelType cancelType)
            {
                from.SendMessage("Cancelled.");
            }
        }

        private static void OnRepSet(CommandEventArgs e)
        {
            string[] args = e.Arguments;

            if (args == null || args.Length < 2)
            {
                e.Mobile.SendMessage("Usage: [repset \"Guild Name\" Value");
                return;
            }

            // Last argument is the value; everything before it is the guild name
            string valuePart = args[args.Length - 1];
            int value;
            if (!int.TryParse(valuePart, out value))
            {
                e.Mobile.SendMessage("Invalid value. Usage: [repset \"Guild Name\" Value");
                return;
            }

            string guildName = string.Join(" ", args, 0, args.Length - 1).Trim('"', ' ');

            // Find a matching guild — exact match first, then contains (case-insensitive)
            string matched = null;
            foreach (string g in FBGuilds.All)
            {
                if (string.Equals(g, guildName, StringComparison.OrdinalIgnoreCase))
                {
                    matched = g;
                    break;
                }
            }

            if (matched == null)
            {
                foreach (string g in FBGuilds.All)
                {
                    if (g.IndexOf(guildName, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        matched = g;
                        break;
                    }
                }
            }

            if (matched == null)
            {
                e.Mobile.SendMessage($"Unknown guild: \"{guildName}\". Use the full guild name.");
                return;
            }

            SetStanding(e.Mobile, matched, value);
            e.Mobile.SendMessage(
                $"Your standing with {matched} set to {value} ({GetTier(value)}).");
        }

        // -------------------------------------------------------
        // Persistence
        // -------------------------------------------------------

        private static void OnWorldSave(WorldSaveEventArgs e)
        {
            Persistence.Serialize(SavePath, writer =>
            {
                writer.Write(0); // version

                writer.Write(_standings.Count);
                foreach (var kvp in _standings)
                {
                    writer.Write(kvp.Key); // Serial
                    writer.Write(kvp.Value.Count);
                    foreach (var rep in kvp.Value)
                    {
                        writer.Write(rep.Key);   // guild name string
                        writer.Write(rep.Value); // standing int
                    }
                }
            });
        }

        private static void LoadData()
        {
            if (!File.Exists(SavePath)) return;

            Persistence.Deserialize(SavePath, reader =>
            {
                int version = reader.ReadInt();

                int count = reader.ReadInt();
                for (int i = 0; i < count; i++)
                {
                    Serial serial   = reader.ReadInt();
                    int    repCount = reader.ReadInt();
                    var    dict     = new Dictionary<string, int>();
                    for (int j = 0; j < repCount; j++)
                    {
                        string guild   = reader.ReadString();
                        int    standing = reader.ReadInt();
                        if (standing != 0)
                            dict[guild] = standing;
                    }
                    if (dict.Count > 0)
                        _standings[serial] = dict;
                }
            });
        }
    }
}
