// -----------------------------------------------------------------------
// <copyright file="LeakDetectorScenarioTests.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2019 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace SocketLeakDetection.Tests
{
    public sealed class SocketScenario
    {
        public SocketScenario(IReadOnlyList<int> socketCounts, SocketLeakDetectorSettings settings, bool shouldFail)
        {
            SocketCounts = socketCounts;
            Settings = settings;
            ShouldFail = shouldFail;
        }

        public IReadOnlyList<int> SocketCounts { get; }

        public SocketLeakDetectorSettings Settings { get; }

        public bool ShouldFail { get; }

        public override string ToString()
        {
            return $"Settings={Settings}, Counts=[{string.Join(",", SocketCounts)}], Fail={ShouldFail}";
        }
    }

    public class LeakDetectorScenarioTests
    {
        private readonly ITestOutputHelper _helper;

        public LeakDetectorScenarioTests(ITestOutputHelper helper)
        {
            _helper = helper;
        }

        public static IEnumerable<object[]> GetSocketScenarios()
        {
            // scenario that stays under the min port threshold
            yield return new object[]
            {
                new SocketScenario(new[] {10, 20, 30, 40, 50, 60, 70, 80, 70, 70},
                    new SocketLeakDetectorSettings(), false)
            };

            // ramp-up scenario above the min port threshold with larger long sample window
            yield return new object[]
            {
                new SocketScenario(
                    new[]
                    {
                        100, 120, 130, 140, 150, 160, 170, 180, 190, 200, 200, 200, 210, 220, 230, 230, 230, 230, 230
                    },
                    new SocketLeakDetectorSettings(largeSampleSize: 40), true)
            };

            // ramp-up scenario above the min port threshold with smaller long sample window
            yield return new object[]
            {
                new SocketScenario(
                    new[]
                    {
                        100, 120, 130, 140, 150, 160, 170, 180, 190, 200, 200, 200, 210, 220, 230, 230, 230, 230, 230
                    },
                    new SocketLeakDetectorSettings(largeSampleSize: 25), false)
            };

            // exceed max ports
            yield return new object[]
            {
                new SocketScenario(
                    new[]
                    {
                        100, 120, 130, 140, 150, 160, 170, 180, 190, 200, 200, 200, 210, 220, 230, 230, 230, 230, 230
                    },
                    new SocketLeakDetectorSettings(maxPorts: 200), true)
            };
        }

        [Theory]
        [MemberData(nameof(GetSocketScenarios))]
        public void LeakDetectorScenario(SocketScenario scenario)
        {
            var leakDetector = new LeakDetector(scenario.Settings);
            foreach (var i in scenario.SocketCounts)
            {
                leakDetector.Next(i);
                _helper.WriteLine("ports: {0}, Observed % Diff: {1}, Fail? {2}", i,
                    leakDetector.RelativeDifference, leakDetector.ShouldFail);
                if (!scenario.ShouldFail) // if we can't fail in this scenario, no intermittent failures allowed
                    scenario.ShouldFail.Should().BeFalse();
            }

            leakDetector.ShouldFail.Should().Be(scenario.ShouldFail,
                "Failure state should have been {0}, but was {1} in scenario {2}. Final % difference: {3} vs. target of {4}",
                scenario.ShouldFail,
                leakDetector.ShouldFail, scenario, leakDetector.RelativeDifference, scenario.Settings.MaxDifference);
        }
    }
}