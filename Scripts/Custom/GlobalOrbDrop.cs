// ============================================================
// GlobalOrbDrop.cs
// Scripts/Custom/GlobalOrbDrop.cs
//
// Two responsibilities:
//   1. 0.5% chance for ANY non-player, non-vendor, non-controlled
//      creature to drop a random orb on death.
//   2. Champion bosses (BaseChampion) get an enhanced guaranteed
//      orb drop on top of their normal loot.
// ============================================================

using System;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Server.Custom
{
    public static class GlobalOrbDrop
    {
        public static void Initialize()
        {
            EventSink.Death += OnDeath;
        }

        private static void OnDeath(DeathEventArgs e)
        {
            Mobile m = e.Mobile;

            // Players and vendors never drop orbs this way
            if (m is PlayerMobile) return;
            if (!(m is BaseCreature bc)) return;
            if (bc is BaseVendor) return;

            // Tamed / summoned creatures don't count
            if (bc.Controlled || bc.Summoned) return;

            Container corpse = bc.Corpse as Container;
            if (corpse == null) return;

            // Champion bosses get a dedicated enhanced drop
            if (bc is BaseChampion)
            {
                DropChampionOrbs(corpse);
                return;
            }

            // Universal 0.5% — every other creature in the world
            if (Utility.RandomDouble() < 0.005)
                corpse.DropItem(GenerateUniversalOrb());
        }

        // --------------------------------------------------------
        // Champion boss loot — called instead of the 0.5% roll
        // Guaranteed Cat1 T2 orb + 25% Cat2 item orb + 15% scroll
        // --------------------------------------------------------
        private static void DropChampionOrbs(Container corpse)
        {
            // Always: Cat1 T2 character orb
            corpse.DropItem(GenerateChampionCat1Orb());

            // 25%: Cat2 item enhancement orb
            if (Utility.RandomDouble() < 0.25)
                corpse.DropItem(GenerateItemOrb());

            // 15%: Cat3 combat scroll
            if (Utility.RandomDouble() < 0.15)
                corpse.DropItem(GenerateScroll());
        }

        // --------------------------------------------------------
        // Orb pools
        // --------------------------------------------------------

        // 0.5% universal — Cat1 T1 skewed, EssenceShard most common
        private static Item GenerateUniversalOrb()
        {
            switch (Utility.Random(20))
            {
                case 0:
                case 1:
                case 2: return new OrbOfEnhancement(1);
                case 3:
                case 4: return new OrbOfExpansion(1);
                case 5:
                case 6: return new OrbOfMastery(1);
                case 7:  return new OrbOfFortitude(1);
                case 8:  return new OrbOfAlacrity(1);
                case 9:  return new OrbOfCorruption(); // rare Cat2 surprise
                default: return new EssenceShard(Utility.RandomMinMax(3, 10));
            }
        }

        // Champion Cat1 — T2 tier orbs only
        private static Item GenerateChampionCat1Orb()
        {
            switch (Utility.Random(5))
            {
                case 0: return new OrbOfEnhancement(2);
                case 1: return new OrbOfExpansion(2);
                case 2: return new OrbOfAlacrity(2);
                case 3: return new OrbOfBalance(2);
                default: return new OrbOfFortitude(1);
            }
        }

        // Cat2 item orbs (risky enhancement)
        private static Item GenerateItemOrb()
        {
            switch (Utility.Random(4))
            {
                case 0: return new OrbOfEnchantment();
                case 1: return new OrbOfTempering();
                case 2: return new OrbOfCorruption();
                default: return new OrbOfResonance();
            }
        }

        // Cat3 combat scrolls
        private static Item GenerateScroll()
        {
            switch (Utility.Random(3))
            {
                case 0: return new ScrollOfExecution(Utility.RandomMinMax(1, 2));
                case 1: return new ScrollOfLeeching(1);
                default: return new ScrollOfWarding(1);
            }
        }
    }
}
