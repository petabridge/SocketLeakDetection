// -----------------------------------------------------------------------
// <copyright file="ITcpCounter.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2019 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

namespace SocketLeakDetection
{
    public interface ITcpCounter
    {
        int GetTcpCount();
    }
}