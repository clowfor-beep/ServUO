using System;
using System.Linq;
using System.Text;
using Server.Network;

namespace Server.Custom
{
    /// <summary>
    /// Handles uoservers.com status checks.
    /// Protocol: client sends seed 7F 00 00 01, then packet F1 00 04 FF.
    /// uoservers.com parses the response as plain text, looking for "Clients=N"
    /// in a comma-separated Key=Value format.
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

                // uoservers.com parses plain text Key=Value, looks for "Clients=N"
                string response = string.Format("Name=AIther UO,Clients={0},MaxPlayers=2500", online);
                byte[] responseBytes = Encoding.ASCII.GetBytes(response);
                state.Socket.Send(responseBytes);
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
