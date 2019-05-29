// -----------------------------------------------------------------------
// <copyright file="EMWASanitySpecs.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2019 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace SocketLeakDetection.Tests
{
    /// <summary>
    ///     Validation specs for the basics of the <see cref="EMWA" /> struct.
    /// </summary>
    public class EMWASanitySpecs
    {
        [Fact(DisplayName = "EMWA % operator should be affected by operator order")]
        public void EMWA_percentage_difference_order_matters()
        {
            var e1 = EMWA.Init(10, 1);
            var e2 = EMWA.Init(25, 1);

            foreach (var i in Enumerable.Range(2, 35))
            {
                e1 += i;
                e2 += i;
            }

            var p1 = e1 % e2;
            var p2 = e2 % e1;

            p1.Should().NotBe(p2);

            // in this scenario, p2 should be negative as it includes many more "smaller" values in its weight
            p2.Should().BeLessThan(p1);
        }

        [Fact(DisplayName = "EMWAs with different alpha values should diverge despite observing same data")]
        public void EMWA_should_diverge_with_different_alphas()
        {
            var e1 = EMWA.Init(10, 1);
            var e2 = EMWA.Init(25, 1);

            var percentages = new List<double>();
            foreach (var i in Enumerable.Range(2, 35))
            {
                e1 += i;
                e2 += i;
                percentages.Add(e1 % e2);
            }

            // differences should diverge over time
            percentages.All(x => x > 0.0d).Should().BeTrue();
        }
    }
}