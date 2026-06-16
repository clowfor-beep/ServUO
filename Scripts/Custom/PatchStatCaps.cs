// ============================================================
// PatchStatCaps.cs
// Scripts/Custom/PatchStatCaps.cs
//
// One-time startup patch: updates all existing PlayerMobile
// StrCap/DexCap/IntCap to match OrbCeilings.MaxStatValue (150).
//
// Safe to leave in permanently — the version flag means it only
// runs once per character, and is a no-op after the first restart.
// ============================================================

using System;
using Server;
using Server.Mobiles;

namespace Server.Custom
{
    public static class PatchStatCaps
    {
        private const int TargetCap = 150; // must match OrbCeilings.MaxStatValue

        public static void Initialize()
        {
            // Run after world load so all mobiles are available
            EventSink.WorldLoad += OnWorldLoad;
        }

        private static void OnWorldLoad()
        {
            int count = 0;

            foreach (Mobile m in World.Mobiles.Values)
            {
                if (!(m is PlayerMobile))
                    continue;

                bool changed = false;

                if (m.StrCap < TargetCap) { m.StrCap = TargetCap; changed = true; }
                if (m.DexCap < TargetCap) { m.DexCap = TargetCap; changed = true; }
                if (m.IntCap < TargetCap) { m.IntCap = TargetCap; changed = true; }

                if (changed)
                    count++;
            }

            if (count > 0)
                Console.WriteLine($"[PatchStatCaps] Updated stat caps to {TargetCap} for {count} player(s).");
        }
    }
}
