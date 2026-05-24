// ============================================================
// FBEventBus.cs
// Scripts/Custom/FBEventBus.cs
//
// Cross-system event bus for Forsaken Britannia.
// Decouples all custom systems — they never reference each
// other directly; they subscribe to and fire events here.
//
// Usage:
//   // Subscribe
//   FBEventBus.PlayerKilledSimPlayer += OnPlayerKilledSim;
//
//   // Fire (always use Fire_* helpers, never invoke directly)
//   FBEventBus.Fire_PlayerKilledSimPlayer(killer, victim);
//
// No Initialize() needed — pure event declarations.
// ============================================================

using System;
using Server;

namespace Server.Custom
{
    public static class FBEventBus
    {
        // ── Events ────────────────────────────────────────────────────────

        /// <summary>Fired when a SimPlayer kills a real player.</summary>
        /// <param name="killer">The SimPlayer mobile.</param>
        /// <param name="victim">The real player mobile.</param>
        public static event Action<Mobile, Mobile> SimPlayerKilledPlayer;

        /// <summary>Fired when a real player kills a SimPlayer.</summary>
        /// <param name="killer">The real player mobile.</param>
        /// <param name="victim">The SimPlayer mobile.</param>
        public static event Action<Mobile, Mobile> PlayerKilledSimPlayer;

        /// <summary>Fired when a SimPlayer enters a named zone.</summary>
        /// <param name="simPlayer">The SimPlayer mobile.</param>
        /// <param name="zone">The zone entered.</param>
        public static event Action<Mobile, SpawnZone> SimPlayerEnteredZone;

        /// <summary>
        /// Fired when a SimPlayer becomes active (returns from cooldown
        /// or first spawn).
        /// </summary>
        /// <param name="simPlayer">The SimPlayer mobile.</param>
        public static event Action<Mobile> SimPlayerActivated;

        /// <summary>
        /// Fired when a SimPlayer goes on cooldown (after death or logout).
        /// </summary>
        /// <param name="simPlayer">The SimPlayer mobile.</param>
        public static event Action<Mobile> SimPlayerDeactivated;

        /// <summary>Fired when a champion spawn becomes active.</summary>
        /// <param name="spawn">The champion spawn object (typed as object until
        /// the champion class exists in Server.Custom).</param>
        public static event Action<object> ChampionSpawnActivated;

        /// <summary>Fired when a champion spawn is completed.</summary>
        /// <param name="spawn">The champion spawn object.</param>
        /// <param name="killer">The mobile that landed the killing blow on the
        /// champion.</param>
        public static event Action<object, Mobile> ChampionSpawnCompleted;

        /// <summary>Fired when a player's reputation with a guild changes.</summary>
        /// <param name="player">The player whose reputation changed.</param>
        /// <param name="guildName">Name of the guild (matches guild name strings
        /// in ReputationSystem).</param>
        /// <param name="delta">Amount of change — positive = gained, negative =
        /// lost.</param>
        public static event Action<Mobile, string, int> ReputationChanged;

        /// <summary>Fired when FBPKSpawner places a new PoolPK in the world.</summary>
        /// <param name="pk">The PoolPK mobile that was spawned.</param>
        /// <param name="zone">The zone it was spawned into.</param>
        public static event Action<Mobile, SpawnZone> PoolPKSpawned;

        /// <summary>Fired when a PoolPK is killed.</summary>
        /// <param name="pk">The PoolPK mobile that was killed.</param>
        /// <param name="killer">The mobile that landed the killing blow
        /// (may be null if killed by environmental damage or script).</param>
        /// <param name="zone">The zone the PoolPK belonged to.</param>
        public static event Action<Mobile, Mobile, SpawnZone> PoolPKKilled;

        // ── Fire helpers ──────────────────────────────────────────────────
        // Always call these instead of invoking events directly.
        // They null-check for you and give you a single call-site to add
        // logging or filtering in the future.

        public static void Fire_SimPlayerKilledPlayer(Mobile killer, Mobile victim)
            => SimPlayerKilledPlayer?.Invoke(killer, victim);

        public static void Fire_PlayerKilledSimPlayer(Mobile killer, Mobile victim)
            => PlayerKilledSimPlayer?.Invoke(killer, victim);

        public static void Fire_SimPlayerEnteredZone(Mobile simPlayer, SpawnZone zone)
            => SimPlayerEnteredZone?.Invoke(simPlayer, zone);

        public static void Fire_SimPlayerActivated(Mobile simPlayer)
            => SimPlayerActivated?.Invoke(simPlayer);

        public static void Fire_SimPlayerDeactivated(Mobile simPlayer)
            => SimPlayerDeactivated?.Invoke(simPlayer);

        public static void Fire_ChampionSpawnActivated(object spawn)
            => ChampionSpawnActivated?.Invoke(spawn);

        public static void Fire_ChampionSpawnCompleted(object spawn, Mobile killer)
            => ChampionSpawnCompleted?.Invoke(spawn, killer);

        public static void Fire_ReputationChanged(Mobile player, string guildName, int delta)
            => ReputationChanged?.Invoke(player, guildName, delta);

        public static void Fire_PoolPKSpawned(Mobile pk, SpawnZone zone)
            => PoolPKSpawned?.Invoke(pk, zone);

        public static void Fire_PoolPKKilled(Mobile pk, Mobile killer, SpawnZone zone)
            => PoolPKKilled?.Invoke(pk, killer, zone);
    }
}
