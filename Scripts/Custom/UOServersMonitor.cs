using System;
using System.Linq;
using Server.Network;

namespace Server.Custom
{
    /// <summary>
    /// Handles uoservers.com status checks.
    /// Protocol: client sends seed 7F 00 00 01, then packet F1 00 04 FF.
    /// We respond to F1 with player count and close the connection.
    /// </summary>
    public static class UOServersMonitor
    {
        public static void Initialize()
        {
            // Register as variable-length (0) so PacketReader starts at position 3,
            // after the 3-byte header [F1][00][04], correctly reading the 0xFF command byte.
            PacketHandlers.Register(0xF1, 0, false, OnUOGatewayQuery);
        }

        private static void OnUOGatewayQuery(NetState state, PacketReader pvSrc)
        {
            try
            {
                byte command = pvSrc.ReadByte(); // 0xFF = status request

                if (command != 0xFF)
                    return;

                var instances = NetState.Instances.ToArray();
                int online = 0;
                foreach (var ns in instances)
                {
                    if (ns != null && (ns.Mobile != null || ns.Account != null))
                        online++;
                }

                Console.WriteLine("[UOServers] F1 status query from {0}: reporting {1} players", state, online);

                state.Socket.Send(new byte[]
                {
                    0xF1,
                    (byte)(online & 0xFF),  // count low byte at [1]
                    (byte)(online >> 8),    // count high byte at [2]
                    0x09, 0xC4              // max players 2500
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine("[UOServers] F1 exception: " + ex.Message);
            }
            finally
            {
                state.Dispose();
            }
        }
    }
}
