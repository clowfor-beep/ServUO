using System;
using System.IO;
using System.Linq;
using Server;
using Server.Mobiles;
using Server.Network;

namespace Server.Custom
{
    public static class PlayerCountExport
    {
        private static string OutputPath => Path.Combine(Core.BaseDirectory, "website", "playercount.json");
        private static Timer _timer;

        public static void Initialize()
        {
            _timer = Timer.DelayCall(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(60), WriteCount);
        }

        private static void WriteCount()
        {
            try
            {
                int count = NetState.Instances
                    .Count(ns => ns?.Mobile is PlayerMobile);

                string json = $"{{\"count\":{count},\"ts\":{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}}}";

                File.WriteAllText(OutputPath, json);
                Console.WriteLine($"[PlayerCount] Wrote {count} to {OutputPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PlayerCount] Failed to write: {ex.Message}");
            }
        }
    }
}
