using System;
using NUnit.Framework;
using XRApartment.Simulation;

namespace XRApartment.Tests
{
    public class WaterVolumeTests
    {
        [Test]
        public void PouringIntoAlmostFullCup_OnlyTransfersAvailableSpace()
        {
            var pot = new WaterVolume(2000, 800);
            var cup = new WaterVolume(300, 280);
            Assert.That(pot.TransferTo(cup, 100), Is.EqualTo(20));
            Assert.That(cup.Amount, Is.EqualTo(300));
            Assert.That(pot.Amount, Is.EqualTo(780));
            Assert.That(pot.Amount + cup.Amount, Is.EqualTo(1080));
        }

        [Test]
        public void RepeatedTransfers_ConserveWaterAndNeverGoNegative()
        {
            var a = new WaterVolume(350, 175);
            var b = new WaterVolume(2000, 500);
            for (int i = 0; i < 10000; i++)
            {
                a.TransferTo(b, 7.25f);
                b.TransferTo(a, 8.15f);
            }
            Assert.That(a.Amount + b.Amount, Is.EqualTo(675).Within(0.02f));
            Assert.That(a.Amount, Is.InRange(0, a.Capacity));
            Assert.That(b.Amount, Is.InRange(0, b.Capacity));
        }

        [Test]
        public void EmptySourceCannotCreateWaterInReceiver()
        {
            var source = new WaterVolume(300);
            var receiver = new WaterVolume(500);
            Assert.That(source.TransferTo(receiver, 100), Is.Zero);
            Assert.That(receiver.Amount, Is.Zero);
            Assert.That(source.Remove(100), Is.Zero);
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(-100f)]
        public void InvalidFlowDoesNotCorruptStoredVolume(float flow)
        {
            var water = new WaterVolume(300, 100);
            Assert.That(water.Add(flow), Is.Zero);
            Assert.That(water.Remove(flow), Is.Zero);
            Assert.That(water.Amount, Is.EqualTo(100));
        }

        [Test]
        public void CapacityMustBePositiveAndFinite()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new WaterVolume(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new WaterVolume(float.PositiveInfinity));
        }
    }

    public class CylindricalLiquidTests
    {
        const float Radius = 0.034f, Height = 0.094f;

        [TestCase(0.1f)]
        [TestCase(0.5f)]
        [TestCase(0.9f)]
        public void UprightVessel_WaterHeightMatchesFill(float fill)
        {
            float level = CylindricalLiquid.SurfaceOffset(Radius, Height, 0, 1, 0, fill);
            Assert.That(level, Is.EqualTo(Height * fill).Within(0.00002f));
        }

        [Test]
        public void IncreasingTilt_LeavesLessRetainableWater()
        {
            float previous = 1f;
            for (int degrees = 0; degrees <= 90; degrees += 5)
            {
                double radians = degrees * Math.PI / 180;
                float retained = CylindricalLiquid.RetainedFraction(Radius, Height,
                    (float)Math.Sin(radians), (float)Math.Cos(radians), 0);
                Assert.That(retained, Is.LessThanOrEqualTo(previous + 0.0001f), degrees.ToString());
                Assert.That(retained, Is.InRange(0, 1));
                previous = retained;
            }
            Assert.That(previous, Is.LessThan(0.001f));
        }

        [TestCase(1f, 0f)]
        [TestCase(0f, -1f)]
        public void SidewaysOrInvertedVessel_HasNoStableRetainedWater(float nx, float ny)
            => Assert.That(CylindricalLiquid.RetainedFraction(Radius, Height, nx, ny, 0), Is.Zero);

        [TestCase(0.05f)]
        [TestCase(0.4f)]
        [TestCase(0.85f)]
        public void TiltedSurfaceSolver_ReconstructsTheRequestedVolume(float fill)
        {
            const float nx = 0.6f, ny = 0.8f;
            float offset = CylindricalLiquid.SurfaceOffset(Radius, Height, nx, ny, 0, fill);
            float actual = CylindricalLiquid.FractionBelowPlane(Radius, Height, nx, ny, 0, offset);
            Assert.That(actual, Is.EqualTo(fill).Within(0.0002f));
        }
    }
}
