// ============================================================
// GraveyardPKEncounter.cs
// Scripts/Custom/GraveyardPKEncounter.cs
//
// Detects players entering the Britain graveyard and
// portals in a random Newbie-tier PK who immediately hunts them.
//
// Spawns one of 7 archetypes at random (all Newbie tier):
//   ClassicDexxerNewbie, PureMageNewbie, NecroMageNewbie,
//   NinjaDexxerNewbie, PaladinNewbie, ArcherNewbie, SampireNewbie
//
// Features:
//   - 3-minute cooldown per player (no spam)
//   - Max 1 active PK per player at a time
//   - Portal moongate VFX + sound on spawn
//   - PK appears within 5 tiles of the player
//   - PK auto-deletes after 5 min if player escapes
//   - Only triggers in Felucca
//   - GMs and staff are ignored
// ============================================================

using System;
using System.Collections.Generic;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Server.Custom
{
    public static class GraveyardPKEncounter
    {
        // -------------------------------------------------------
        // Graveyard bounds (from Regions.xml rect)
        // -------------------------------------------------------
        private static readonly Rectangle2D GraveyardRect =
            new Rectangle2D(1333, 1441, 84, 82);

        // Britain graveyard Z range
        private const int MinZ = 0;
        private const int MaxZ = 20;

        // -------------------------------------------------------
        // Per-player cooldown and active PK tracking
        // -------------------------------------------------------
        private static readonly Dictionary<Mobile, DateTime>        Cooldowns  = new Dictionary<Mobile, DateTime>();
        private static readonly Dictionary<Mobile, BasePKNPC> ActivePKs = new Dictionary<Mobile, BasePKNPC>();

        private static readonly TimeSpan CooldownDuration = TimeSpan.FromMinutes(3.0);

        // -------------------------------------------------------
        // Hook into EventSink on startup
        // -------------------------------------------------------
        public static void Initialize()
        {
            EventSink.Movement += OnMovement;
        }

        // -------------------------------------------------------
        // Movement handler — fires on every player step
        // -------------------------------------------------------
        private static void OnMovement(MovementEventArgs e)
        {
            if (!(e.Mobile is PlayerMobile pm))
                return;

            // Ignore GMs and staff
            if (pm.AccessLevel > AccessLevel.Player)
                return;

            // Only Felucca
            if (pm.Map != Map.Felucca)
                return;

            // Only alive players
            if (!pm.Alive)
                return;

            // Check they're inside the graveyard rect and Z
            if (!GraveyardRect.Contains(pm.Location))
                return;

            if (pm.Z < MinZ || pm.Z > MaxZ)
                return;

            // Already has an active PK hunting them?
            if (ActivePKs.TryGetValue(pm, out BasePKNPC existingPK))
            {
                if (existingPK != null && !existingPK.Deleted)
                    return; // PK is still alive and hunting

                // PK was killed or deleted — clean up
                ActivePKs.Remove(pm);
            }

            // On cooldown?
            if (Cooldowns.TryGetValue(pm, out var lastSpawn))
            {
                if (DateTime.UtcNow < lastSpawn + CooldownDuration)
                    return;
            }

            // All checks passed — trigger the encounter
            TriggerEncounter(pm);
        }

        // -------------------------------------------------------
        // Public entry point for the [pktest command — skips all checks
        // -------------------------------------------------------
        public static void ForceEncounter(PlayerMobile target)
        {
            TriggerEncounter(target);
        }

        // -------------------------------------------------------
        // Trigger: portal VFX then spawn the PK
        // -------------------------------------------------------
        private static void TriggerEncounter(PlayerMobile target)
        {
            // Record cooldown immediately to prevent double-triggers
            Cooldowns[target] = DateTime.UtcNow;

            // Pick a spawn point within 3-5 tiles of the player,
            // staying inside the graveyard rect
            Point3D spawnPoint = FindSpawnPoint(target);

            if (spawnPoint == Point3D.Zero)
                return; // Couldn't find a valid spot

            // ── Step 1: Portal opening VFX at spawn location ──
            Effects.SendLocationParticles(
                EffectItem.Create(spawnPoint, Map.Felucca, EffectItem.DefaultDuration),
                0x3728, 10, 14, 5042); // moongate-style gate

            Effects.PlaySound(spawnPoint, Map.Felucca, 0x20E); // moongate open sound

            // ── Step 2: Short delay then NPC appears ──
            Timer.DelayCall(TimeSpan.FromSeconds(1.2), () =>
            {
                if (target == null || target.Deleted || !target.Alive)
                    return;

                // Second VFX burst as NPC steps through
                Effects.SendLocationParticles(
                    EffectItem.Create(spawnPoint, Map.Felucca, EffectItem.DefaultDuration),
                    0x3728, 10, 10, 2023);
                Effects.PlaySound(spawnPoint, Map.Felucca, 0x1FE);

                // Spawn a random Newbie-tier PK archetype
                BasePKNPC pk = CreateRandomNewbie();
                pk.MoveToWorld(spawnPoint, Map.Felucca);
                pk.Activate(); // start AI ticking immediately on spawn
                pk.InitEncounter(target);

                // Track it
                ActivePKs[target] = pk;

                // Notify player
                target.SendMessage(0x26, "A dark figure steps through a shimmering portal...");

                // Clean up ActivePKs entry when PK is killed/deleted
                Timer.DelayCall(TimeSpan.FromSeconds(5.0), TimeSpan.FromSeconds(5.0), () =>
                {
                    if (pk == null || pk.Deleted)
                    {
                        ActivePKs.Remove(target);
                        return; // stop the repeating timer via exception path
                    }
                });
            });
        }

        // -------------------------------------------------------
        // Pick a random Newbie-tier archetype
        // -------------------------------------------------------
        private static BasePKNPC CreateRandomNewbie()
        {
            switch (Utility.Random(7))
            {
                case 0:  return new ClassicDexxerNewbie();
                case 1:  return new PureMageNewbie();
                case 2:  return new NecroMageNewbie();
                case 3:  return new NinjaDexxerNewbie();
                case 4:  return new PaladinNewbie();
                case 5:  return new ArcherNewbie();
                default: return new SampireNewbie();
            }
        }

        // -------------------------------------------------------
        // Find a valid spawn point near the player inside the GY
        // -------------------------------------------------------
        private static Point3D FindSpawnPoint(Mobile target)
        {
            Map map = Map.Felucca;

            for (int attempt = 0; attempt < 20; attempt++)
            {
                int dx = Utility.RandomMinMax(-5, 5);
                int dy = Utility.RandomMinMax(-5, 5);

                // Keep at least 3 tiles away
                if (Math.Abs(dx) < 3 && Math.Abs(dy) < 3)
                    continue;

                int x = target.X + dx;
                int y = target.Y + dy;

                // Must be inside graveyard
                if (!GraveyardRect.Contains(new Point2D(x, y)))
                    continue;

                // Find a valid Z
                int z = map.GetAverageZ(x, y);

                Point3D p = new Point3D(x, y, z);

                // Check it's passable and has LOS to player
                if (map.CanSpawnMobile(p) && target.InLOS(p))
                    return p;
            }

            // Fallback: spawn right at graveyard centre
            return new Point3D(1375, 1475, 10);
        }
    }
}
