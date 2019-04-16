// -----------------------------------------------------------------------
// <copyright file="SocketLeakDetectorSettings.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2019 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Net;
using Akka.Actor;

namespace SocketLeakDetection
{
    /// <summary>
    ///     Constructor will setup the values that we will need to determine if we need to message our supervisor actor in case
    ///     we experience an increase in TCP Ports
    /// </summary>
    public class SocketLeakDetectorSettings
    {
        public const int DefaultMaxPorts = 65536;
        public const int DefaultMinPorts = 100;
        public const double DefaultMaxDifference = 0.20;
        public const int DefaultLongSampleSize = 50;
        public const int DefaultShortSampleSize = 10;
        public static readonly TimeSpan DefaultTcpPollInterval = TimeSpan.FromMilliseconds(1000);
        public static readonly TimeSpan DefaultBreachDuration = TimeSpan.FromSeconds(DefaultLongSampleSize / 2.0);


        public SocketLeakDetectorSettings(double maxDifference = DefaultMaxDifference,
            int maxPorts = DefaultMaxPorts, int minPorts = DefaultMinPorts,
            int smallSampleSize = DefaultShortSampleSize, int largeSampleSize = DefaultLongSampleSize,
            TimeSpan? portCheckInterval = null, TimeSpan? breachDuration = null)
        {
            if (minPorts < 1)
                throw new ArgumentOutOfRangeException(nameof(minPorts),
                    "minPorts must be greater than or equal to 1.");

            if (maxPorts > minPorts)
                MaxPorts = maxPorts;
            else
                throw new ArgumentOutOfRangeException(nameof(maxPorts),
                    "maxPors must be greater than minPorts");

            if (maxDifference > 0)
                MaxDifference = maxDifference;
            else
                throw new ArgumentOutOfRangeException(nameof(maxDifference), "maxDifference must be greater than 0");


            if (largeSampleSize > 2)
                LongSampleSize = largeSampleSize;
            else
                throw new ArgumentOutOfRangeException(nameof(largeSampleSize), "LargeSampleSize must be at least 2");

            if (smallSampleSize < LongSampleSize)
                ShortSampleSize = smallSampleSize;
            else
                throw new ArgumentOutOfRangeException(nameof(smallSampleSize),
                    "smallSampleSize must greater than largeSampleSize");


            PortCheckInterval = portCheckInterval ?? DefaultTcpPollInterval;
            BreachDuration = breachDuration ?? DefaultBreachDuration;
            MinPorts = minPorts;
        }

        public double MaxDifference { get; set; }
        public int MaxPorts { get; set; }
        public int MinPorts { get; set; }
        public int ShortSampleSize { get; set; }
        public int LongSampleSize { get; set; }
        public TimeSpan PortCheckInterval { get; set; }

        /// <summary>
        ///     How long the <see cref="LeakDetector" /> needs report true prior
        ///     to firing off an <see cref="ActorSystem" /> termination event.
        /// </summary>
        public TimeSpan BreachDuration { get; set; }

        public override string ToString()
        {
            return
                $"SocketLeakDetectorSettings(MaxDifference={MaxDifference}, MaxPorts={MaxPorts}, MinPorts={MinPorts}" +
                $"ShortSampleSize={ShortSampleSize}, LargeSampleSize={LongSampleSize}," +
                $"PortCheckInterval={PortCheckInterval}, BreachDuration={BreachDuration}";
        }
    }
}