using System;
using System.Collections.Generic;
using System.Text;

namespace SocketLeakDetection.Tests
{
    /// <summary>
    /// Fake counter will increase the count for a set range of request for the count. 
    /// </summary>
    public class FakeCounter : ITcpCounter
    {
        int _currentCount;
        int counter = 0;

        public FakeCounter(int currentCount)
        {
            _currentCount = currentCount;

        }
        public FakeCounter()
        {
            _currentCount = 0;

        }
        public int GetTcpCount()
        {
            counter += 1;
            if (counter > 10 && counter < 1000)
                _currentCount += 10;
            Console.WriteLine("TCP : {0}", _currentCount);
            return _currentCount;
        }
        public void IncreaseCount(int increase)
        {
            _currentCount += increase;
        }


    }
}

