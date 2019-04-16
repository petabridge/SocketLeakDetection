using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace SocketLeakDetection
{
    public interface ITcpCounter
    {
          int GetTcpCount();
    }
}
