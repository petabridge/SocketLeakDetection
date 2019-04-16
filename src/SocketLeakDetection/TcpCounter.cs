// -----------------------------------------------------------------------
// <copyright file="TcpCounter.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2019 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System.Linq;
using System.Net;
using System.Net.NetworkInformation;

namespace SocketLeakDetection
{
    public class TcpCounter : ITcpCounter
    {
        private readonly IPAddress Address;

        public TcpCounter(IPAddress address)
        {
            Address = address;
        }

        public int GetTcpCount()
        {
            var ipProperties = IPGlobalProperties.GetIPGlobalProperties();
            var iPEndPoints = ipProperties.GetActiveTcpListeners();

            var ports = from points in iPEndPoints
                where points.Address.ToString().Contains(Address.ToString())
                select points.Port;

            return ports.Count();
        }
    }
}