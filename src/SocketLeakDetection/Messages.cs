using System;
using System.Collections.Generic;
using System.Text;

namespace SocketLeakDetection
{
    public class Messages
    {
        public class TcpCount { }
        public class TimerExpired { }
        public class Stat { public int CurretStatus { get; set; } }
    }
}
