// ============================================================
// ScheduleProfile.cs
// Scripts/Custom/ScheduleProfile.cs
//
// Defines when a SimPlayer is active based on server hour.
// Each SimPlayer gets a profile with a per-member drift so
// they don't all activate and deactivate at the exact same moment.
// ============================================================

using System;
using Server;

namespace Server.Custom
{
    public class ScheduleProfile
    {
        private readonly double[] _hourlyChance;
        private readonly int _driftMinutes;

        public ScheduleProfile(double[] hourlyChance, int driftMinutes = 0)
        {
            _hourlyChance = hourlyChance;
            _driftMinutes = driftMinutes;
        }

        public bool ShouldBeActive(DateTime utcNow)
        {
            int adjustedMinute = (int)(utcNow.Hour * 60 + utcNow.Minute + _driftMinutes) % (24 * 60);
            int hour = adjustedMinute / 60;
            return Utility.RandomDouble() < _hourlyChance[hour];
        }

        // -- Wanderers -- always active, 24/7 -------------------------
        public static ScheduleProfile Wanderers(int driftMinutes = 0)
        {
            double[] chance = new double[24];
            for (int h = 0; h < 24; h++)
                chance[h] = 1.0; // Wanderers are always out in the world
            return new ScheduleProfile(chance, driftMinutes);
        }

        // -- Craftsmen's League -- workday schedule -------------------
        public static ScheduleProfile CraftsmensLeague(int driftMinutes = 0)
        {
            double[] chance = new double[24];
            for (int h = 0; h < 24; h++)
            {
                if      (h >= 0  && h < 7)  chance[h] = 0.10;
                else if (h >= 7  && h < 18) chance[h] = 0.85;
                else if (h >= 18 && h < 21) chance[h] = 0.50;
                else                        chance[h] = 0.20;
            }
            return new ScheduleProfile(chance, driftMinutes);
        }

        // -- Iron Company -- prime time heavy -------------------------
        public static ScheduleProfile IronCompany(int driftMinutes = 0)
        {
            double[] chance = new double[24];
            for (int h = 0; h < 24; h++)
            {
                if      (h >= 0  && h < 8)  chance[h] = 0.30;
                else if (h >= 8  && h < 18) chance[h] = 0.60;
                else if (h >= 18 && h < 23) chance[h] = 0.95;
                else                        chance[h] = 0.40;
            }
            return new ScheduleProfile(chance, driftMinutes);
        }

        // -- Arcane Brotherhood -- late night heavy -------------------
        public static ScheduleProfile ArcaneBrotherhood(int driftMinutes = 0)
        {
            double[] chance = new double[24];
            for (int h = 0; h < 24; h++)
            {
                if      (h >= 0  && h < 4)  chance[h] = 0.70;
                else if (h >= 4  && h < 10) chance[h] = 0.20;
                else if (h >= 10 && h < 18) chance[h] = 0.50;
                else                        chance[h] = 0.80;
            }
            return new ScheduleProfile(chance, driftMinutes);
        }

        // -- Silver Wolves -- consistent patrol presence --------------
        public static ScheduleProfile SilverWolves(int driftMinutes = 0)
        {
            double[] chance = new double[24];
            for (int h = 0; h < 24; h++)
            {
                if      (h >= 0  && h < 6)  chance[h] = 0.40;
                else if (h >= 6  && h < 22) chance[h] = 0.75;
                else                        chance[h] = 0.50;
            }
            return new ScheduleProfile(chance, driftMinutes);
        }

        // -- Shadow Hand -- town thieves, peak-hours focus ------------
        public static ScheduleProfile ShadowHand(int driftMinutes = 0)
        {
            double[] chance = new double[24];
            for (int h = 0; h < 24; h++)
            {
                if      (h >= 0  && h < 6)  chance[h] = 0.10; // sleeping
                else if (h >= 6  && h < 10) chance[h] = 0.50; // morning shift
                else if (h >= 10 && h < 22) chance[h] = 0.80; // prime picking hours
                else                        chance[h] = 0.30;
            }
            return new ScheduleProfile(chance, driftMinutes);
        }

        // -- Paladin Order -- heavy prime time, late nights ----------
        // Active 16:00-02:00, quiet 02:00-16:00
        public static ScheduleProfile PaladinOrder(int driftMinutes = 0)
        {
            double[] chance = new double[24];
            for (int h = 0; h < 24; h++)
            {
                if      (h >= 0  && h < 2)  chance[h] = 0.80; // late night patrol
                else if (h >= 2  && h < 16) chance[h] = 0.15; // resting
                else if (h >= 16 && h < 20) chance[h] = 0.70; // evening muster
                else                        chance[h] = 0.90; // prime time vigil
            }
            return new ScheduleProfile(chance, driftMinutes);
        }

        // -- Dead Watchers -- late night hunters ----------------------
        // Active 20:00-04:00, quiet 04:00-20:00
        public static ScheduleProfile DeadWatchers(int driftMinutes = 0)
        {
            double[] chance = new double[24];
            for (int h = 0; h < 24; h++)
            {
                if      (h >= 0  && h < 4)  chance[h] = 0.85; // peak hunting
                else if (h >= 4  && h < 20) chance[h] = 0.10; // dormant by day
                else if (h >= 20 && h < 24) chance[h] = 0.75; // rising at dusk
                else                        chance[h] = 0.10;
            }
            return new ScheduleProfile(chance, driftMinutes);
        }

        // -- Dread Hunters -- extreme prime time ----------------------
        // Active 17:00-01:00, quiet 01:00-17:00
        public static ScheduleProfile DreadHunters(int driftMinutes = 0)
        {
            double[] chance = new double[24];
            for (int h = 0; h < 24; h++)
            {
                if      (h >= 0  && h < 1)  chance[h] = 0.70; // final hour
                else if (h >= 1  && h < 17) chance[h] = 0.10; // resting
                else if (h >= 17 && h < 20) chance[h] = 0.75; // forming up
                else                        chance[h] = 0.95; // full hunt mode
            }
            return new ScheduleProfile(chance, driftMinutes);
        }

        // -- Blood Pact -- prime time predators -----------------------
        // Active 18:00-00:00, quiet 00:00-18:00
        public static ScheduleProfile BloodPact(int driftMinutes = 0)
        {
            double[] chance = new double[24];
            for (int h = 0; h < 24; h++)
            {
                if      (h >= 0  && h < 18) chance[h] = 0.10; // lurking in darkness
                else if (h >= 18 && h < 21) chance[h] = 0.70; // gathering
                else                        chance[h] = 0.90; // hunting hour
            }
            return new ScheduleProfile(chance, driftMinutes);
        }

        // -- The Void -- always active, all hours --------------------
        // On 00:00-23:59 (nearly always present)
        public static ScheduleProfile TheVoid(int driftMinutes = 0)
        {
            double[] chance = new double[24];
            for (int h = 0; h < 24; h++)
            {
                chance[h] = 0.85; // The Void never sleeps
            }
            return new ScheduleProfile(chance, driftMinutes);
        }

        // -- Shadowblade -- opportunistic, prime time -----------------
        // Active 17:00-23:00, quiet 23:00-17:00
        public static ScheduleProfile Shadowblade(int driftMinutes = 0)
        {
            double[] chance = new double[24];
            for (int h = 0; h < 24; h++)
            {
                if      (h >= 0  && h < 17) chance[h] = 0.10; // waiting, watching
                else if (h >= 17 && h < 20) chance[h] = 0.60; // moving into position
                else if (h >= 20 && h < 23) chance[h] = 0.90; // contract hours
                else                        chance[h] = 0.30; // withdrawing
            }
            return new ScheduleProfile(chance, driftMinutes);
        }
    }
}
