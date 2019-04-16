using System;
using System.Collections.Generic;
using System.Text;

namespace SocketLeakDetection
{
    public class Messages
    {


        public class TimerExpired
        {
            public static readonly TimerExpired Instance = new TimerExpired();
            private TimerExpired() { }
        }
        public class Stat { public int CurretStatus { get; set; } }
    }
}
