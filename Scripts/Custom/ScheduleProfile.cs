// ============================================================
// ScheduleProfile.cs
// Scripts/Custom/ScheduleProfile.cs
//
// Defines when a SimPlayer is active based on server hour.
// Each SimPlayer gets a profile with a per-member drift so
// they don't all activate and deactivate at the exact same
// moment.
// ============================================================

using System;
using Server;

namespace Server.Custom
{
    public class ScheduleProfile
    {
        // ActivityChance[hour 0-23] = 0.0 to 1.0
        private readonly double[] _hourlyChance;

        // Per-member random drift in minutes — offsets the hour calculation
        private readonly int _driftMinutes;

        public ScheduleProfile(double[] hourlyChance, int driftMinutes = 0)
        {
            _hourlyChance  = hourlyChance;
            _driftMinutes  = driftMinutes;
        }

        /// <summary>
        /// Returns true if the SimPlayer should be active at the given UTC time,
        /// based on hourly activity chance and per-member drift.
        /// </summary>
        public bool ShouldBeActive(DateTime utcNow)
        {
            int adjustedMinute = (int)(utcNow.Hour * 60 + utcNow.Minute + _driftMinutes) % (24 * 60);
            int hour = adjustedMinute / 60;
            return Utility.RandomDouble() < _hourlyChance[hour];
        }

        // -------------------------------------------------------
        // Built-in profiles
        // -------------------------------------------------------

        /// <summary>
        /// Wanderers schedule — friendly, daytime-focused.
        /// Prime time (18–23) = 90 % chance, night (0–8) = 20 % chance.
        /// </summary>
        public static ScheduleProfile Wanderers(int driftMinutes = 0)
        {
            double[] chance = new double[24];
            for (int h = 0; h < 24; h++)
            {
                if      (h >= 0  && h < 8)  chance[h] = 0.20; // night
                else if (h >= 8  && h < 12) chance[h] = 0.60; // morning
                else if (h >= 12 && h < 18) chance[h] = 0.70; // afternoon
                else if (h >= 18 && h < 23) chance[h] = 0.90; // prime time
                else                        chance[h] = 0.40; // late night
            }
            return new ScheduleProfile(chance, driftMinutes);
        }
    }
}
