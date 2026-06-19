using System;
using System.Linq;
using System.Text;
using Server.Network;

namespace Server.Custom
{
    /// <summary>
    /// Handles server status queries from external monitoring services (e.g. uoservers.com).
    /// 0x7F — simple probe (seed starts with 0x7F, handled in MessagePump engine layer)
    /// 0xF1 — UOGateway probe (seed 0xFF×4 then F1 00 04 FF packet)
    /// </summary>
    public static class UOServersMonitor
    {
        public static void Initialize()
        {
            // 0xF1 handler: fires after seed 0xFFFFFFFF is consumed; command byte 0xFF = status request
            PacketHandlers.Register(0xF1, 4, false, OnUOGatewayQuery);
        }

        private static void OnUOGatewayQuery(NetState state, PacketReader pvSrc)
        {
            try
            {
                byte command = pvSrc.ReadByte(); // 0xFF = request server info

                if (command != 0xFF)
                    return;

                var instances = NetState.Instances.ToArray();
                int online = 0;
                foreach (var ns in instances)
                {
                    if (ns != null && ns.Mobile != null)
                        online++;
                }

                string name = Config.Get("Server.Name", "Aither");
                byte[] nameBytes = Encoding.ASCII.GetBytes(name);

                Console.WriteLine("[UOServers] F1 query: reporting {0} players online", online);

                // Same format as 0x7F response: [7F][count_hi][count_lo][max_hi][max_lo][name\0]
                int totalLen = 5 + nameBytes.Length + 1;
                var resp = new byte[totalLen];
                resp[0] = 0x7F;
                resp[1] = (byte)(online >> 8);
                resp[2] = (byte)(online & 0xFF);
                resp[3] = 0x09; // max 2500 = 0x09C4
                resp[4] = 0xC4;
                Array.Copy(nameBytes, 0, resp, 5, nameBytes.Length);
                // resp[5 + nameBytes.Length] = 0x00; already zero-initialised

                state.Socket.Send(resp);
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
