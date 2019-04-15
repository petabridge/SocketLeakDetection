using System;
using System.Collections.Generic;
using System.Text;
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
            // scenario that stays under the min connection threshold
            yield return new object[]{ new SocketScenario(new[] {10, 20, 30, 40, 50, 60, 70, 80, 70, 70},
                new SocketLeakDetectorSettings(), false) };

            // aggressive ramp-up scenario above the min connection threshold
            yield return new object[]{ new SocketScenario(new[] {100, 120, 130, 140, 150, 160, 170, 180, 190, 200, 200, 200, 210, 220, 230},
                new SocketLeakDetectorSettings(), false) };
        }

        [Theory]
        [MemberData(nameof(GetSocketScenarios))]
        public void LeakDetectorScenario(SocketScenario scenario)
        {
            var leakDetector = new LeakDetector(scenario.Settings);
            foreach (var i in scenario.SocketCounts)
            {
                leakDetector.Next(i);
                _helper.WriteLine("Connections: {0}, Observed % Diff: {1}, Fail? {2}", i, leakDetector.RelativeDifference, leakDetector.ShouldFail);
                if (!scenario.ShouldFail) // if we can't fail in this scenario, no intermittent failures allowed
                {
                    scenario.ShouldFail.Should().BeFalse();
                }
            }

            leakDetector.ShouldFail.Should().Be(scenario.ShouldFail,
                "Failure state should have been {0}, but was {1} in scenario {2}. Final % difference: {3} vs. target of {4}", scenario.ShouldFail,
                leakDetector.ShouldFail, scenario, leakDetector.RelativeDifference, scenario.Settings.MaxDifference);
        }
    }
}
