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

        // ── Wanderers — friendly, daytime-focused ─────────────────────────
        public static ScheduleProfile Wanderers(int driftMinutes = 0)
        {
            double[] chance = new double[24];
            for (int h = 0; h < 24; h++)
            {
                if      (h >= 0  && h < 8)  chance[h] = 0.20;
                else if (h >= 8  && h < 12) chance[h] = 0.60;
                else if (h >= 12 && h < 18) chance[h] = 0.70;
                else if (h >= 18 && h < 23) chance[h] = 0.90;
                else                        chance[h] = 0.40;
            }
            return new ScheduleProfile(chance, driftMinutes);
        }

        // ── Craftsmen's League — workday schedule ─────────────────────────
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

        // ── Iron Company — prime time heavy ──────────────────────────────
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

        // ── Arcane Brotherhood — late night heavy ─────────────────────────
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

        // ── Silver Wolves — consistent patrol presence ────────────────────
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
    }
}
