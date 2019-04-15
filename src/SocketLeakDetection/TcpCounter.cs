using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Text;

namespace SocketLeakDetection
{
    public class TcpCounter : ITcpCounter
    {
        public int GetTcpCount()
        {
            IPGlobalProperties properties = IPGlobalProperties.GetIPGlobalProperties();
            TcpConnectionInformation[] connections = properties.GetActiveTcpConnections();
            return connections.Length;
        }
    }
}
