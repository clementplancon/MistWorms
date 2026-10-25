using MistWorms.Colony;
using UnityEngine;
using Xunit;

namespace MistWorms.Tests
{
    public class FireMathTests
    {
        [Theory]
        [InlineData(5.3f, 1.37f, 0.0f, true)]     // 3.93 from the edge
        [InlineData(5.4f, 1.37f, 0.0f, false)]    // 4.03 from the edge
        [InlineData(5.9f, 1.37f, 0.6f, true)]     // the margin makes the mass turn before the line
        [InlineData(6.1f, 1.37f, 0.6f, false)]
        public void MassIsAfraidMeasuredFromItsEdge(float centre, float r, float margin, bool expected)
        {
            Assert.Equal(expected, FireMath.MassAfraid(centre, r, 4f, margin));
        }

        [Fact]
        public void WormIsAfraidWithinTheRadius()
        {
            Assert.True(FireMath.PointAfraid(3.9f, 4f));
            Assert.False(FireMath.PointAfraid(4.1f, 4f));
        }

        [Fact]
        public void FlightGoesAwayFromTheSource()
        {
            Vector3 away = FireMath.AwayFrom(new Vector3(0f, 1f, 0f), new Vector3(3f, 0f, 4f), Vector3.forward);
            Assert.Equal(0.6f, away.x, 3);
            Assert.Equal(0.8f, away.z, 3);
            Assert.Equal(0f, away.y, 3);
            Assert.Equal(Vector3.forward, FireMath.AwayFrom(Vector3.zero, Vector3.zero, Vector3.forward));
        }
    }
}
