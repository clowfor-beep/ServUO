using System.Linq;
using Server.Misc;
using Server.Network;

namespace Server.Custom
{
    /// <summary>
    /// Handles 0x7F server status query packets from external monitoring services (e.g. uoservers.com).
    /// Responds with basic server info: online player count and server name.
    /// </summary>
    public static class UOServersMonitor
    {
        public static void Initialize()
        {
            PacketHandlers.Register(0x7F, 0, false, OnServerStatusQuery);
        }

        private static void OnServerStatusQuery(NetState state, PacketReader pvSrc)
        {
            try
            {
                int onlineCount = NetState.Instances.Count(ns => ns != null && ns.Mobile != null);
                state.Send(new ServerStatusResponse(onlineCount));
            }
            catch
            {
                state.Dispose();
            }
        }
    }

    public sealed class ServerStatusResponse : Packet
    {
        public ServerStatusResponse(int onlineCount) : base(0x7F)
        {
            string name = ServerList.ServerName ?? "Aither";

            EnsureCapacity(6 + name.Length);

            m_Stream.Write((short)onlineCount);  // current online players
            m_Stream.Write((short)2500);          // max players
            m_Stream.WriteAsciiNull(name);        // server name
        }
    }
}
