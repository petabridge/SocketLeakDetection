using Akka.Actor;
using System;
using System.Collections.Generic;
using System.Text;

namespace SocketLeakDetection
{
    /// <summary>
    /// Constructor will setup the values that we will need to determine if we need to message our supervisor actor in case we experience an increase in TCP connections
    /// </summary>
    /// <param name="MaxConnections">Maximum number of connections allowed</param>
    /// <param name="PercentDifference">Percent difference we set to send warning. Value can be between 0-1</param>
    /// <param name="MaxDifference">Max percent difference allowed before we message the supervisor actor for temination</param>
    /// <param name="LargeSampleSize">The sample size we want to use for the large sample EMWA</param>
    /// <param name="SmallSampleSize">The sample size we want to use for the small sample EMWA</param>
    public class SocketLeakDetectorSettigns
    {
            public const long DefaultMaxConnections = 16777214;
            public const double DefaultMaxDifference = 0.25;
            public const double DefaultPercenDifference = 0.2;
            public const int DefaultLargeSampleSize = 2;
            public const int DefaultSmallSampleSize = 1;
            

        public SocketLeakDetectorSettigns()
            : this(DefaultPercenDifference, DefaultMaxDifference, DefaultMaxConnections,
               DefaultSmallSampleSize, DefaultLargeSampleSize, TimeSpan.FromMilliseconds(500))
        {
        }

        public SocketLeakDetectorSettigns(double percentDifference, double maxDifference, long maxConnections,
                int smallSampleSize, int largeSampleSize , TimeSpan rate)
        {
            if (maxConnections > 0)
                MaxConnections = maxConnections;
            else 
                MaxConnections = 16777214;

            if (maxDifference > 0)
                MaxDifference = maxDifference;
            else
                MaxDifference = 0.25;

            if (percentDifference > 0)
                PercenDifference = percentDifference;
            else
                PercenDifference = 0.2;

            if (largeSampleSize > 2)
                LargeSampleSize = largeSampleSize;
            else
                LargeSampleSize = 2;

            if (smallSampleSize < LargeSampleSize)
                SmallSampleSize = smallSampleSize;
            else
                SmallSampleSize = 1;

            Rate = rate;
            
        }

        public double PercenDifference { get; set; }
        public double MaxDifference { get; set; }
        public long MaxConnections { get; set; }
        public int SmallSampleSize { get; set; }
        public int LargeSampleSize { get; set; }
        public TimeSpan Rate { get; set; }
        


    }
}
