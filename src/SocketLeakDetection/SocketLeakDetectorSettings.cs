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
    ///     we experience an increase in TCP connections
    /// </summary>
    /// <param name="MaxConnections">Maximum number of connections allowed</param>
    /// <param name="PercentDifference">Percent difference we set to send warning. Value can be between 0-1</param>
    /// <param name="MaxDifference">Max percent difference allowed before we message the supervisor actor for temination</param>
    /// <param name="LargeSampleSize">The sample size we want to use for the large sample EMWA</param>
    /// <param name="SmallSampleSize">The sample size we want to use for the small sample EMWA</param>
    public class SocketLeakDetectorSettings
    {
        public const int DefaultMaxConnections = 16777214;
        public const int DefaultMinConnections = 100;
        public const double DefaultMaxDifference = 0.20;
        public const int DefaultLongSampleSize = 25;
        public const int DefaultShortSampleSize = 10;
        public static readonly TimeSpan DefaultTcpPollInterval = TimeSpan.FromMilliseconds(500);
        public static readonly TimeSpan DefaultBreachDuration = TimeSpan.FromSeconds(DefaultLongSampleSize / 2.0);


        public SocketLeakDetectorSettings(double maxDifference = DefaultMaxDifference,
            int maxConnections = DefaultMaxConnections, int minConnections = DefaultMinConnections,
            int smallSampleSize = DefaultShortSampleSize, int largeSampleSize = DefaultLongSampleSize,
            TimeSpan? rate = null, TimeSpan? breachDuration = null)
        {
            if (minConnections < 1)
                throw new ArgumentOutOfRangeException(nameof(minConnections),
                    "Min connections must be greater than or equal to 1.");

            if (maxConnections > minConnections)
                MaxConnections = maxConnections;
            else
                throw new ArgumentOutOfRangeException(nameof(maxConnections),
                    "maxConnections must be greater than minConnections");

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


            PortCheckInterval = rate ?? DefaultTcpPollInterval;
            BreachDuration = breachDuration ?? DefaultBreachDuration;
            MinConnections = minConnections;
        }

        public double MaxDifference { get; set; }
        public int MaxConnections { get; set; }
        public int MinConnections { get; set; }
        public int ShortSampleSize { get; set; }
        public int LongSampleSize { get; set; }
        public TimeSpan PortCheckInterval { get; set; }
        public IPAddress InterfaceAddress { get; set; }

        /// <summary>
        ///     How long the <see cref="LeakDetector" /> needs report true prior
        ///     to firing off an <see cref="ActorSystem" /> termination event.
        /// </summary>
        public TimeSpan BreachDuration { get; set; }

        public override string ToString()
        {
            return
                $"SocketLeakDetectorSettings(MaxDifference={MaxDifference}, MaxConnections={MaxConnections}, MinConnections={MinConnections}" +
                $"ShortSampleSize={ShortSampleSize}, LargeSampleSize={LongSampleSize}," +
                $"PortCheckInterval={PortCheckInterval}, BreachDuration={BreachDuration}";
        }
    }
}