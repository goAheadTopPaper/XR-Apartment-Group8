using NUnit.Framework;
using XRApartment.Simulation;

namespace XRApartment.Tests
{
    public class MotionKinematicsTests
    {
        [Test]
        public void SmoothStep_FixesEndpointsAndMidpoint()
        {
            Assert.AreEqual(0.0, MotionKinematics.SmoothStep(0.0), 1e-9);
            Assert.AreEqual(0.5, MotionKinematics.SmoothStep(0.5), 1e-9);
            Assert.AreEqual(1.0, MotionKinematics.SmoothStep(1.0), 1e-9);
        }

        [Test]
        public void SmoothStep_IsMonotonicInsideUnitRange()
        {
            var previous = MotionKinematics.SmoothStep(0.0);
            for (var i = 1; i <= 20; i++)
            {
                var current = MotionKinematics.SmoothStep(i / 20.0);
                Assert.GreaterOrEqual(current, previous);
                previous = current;
            }
        }

        [Test]
        public void SmoothStep_ClampsOutOfRangeInput()
        {
            Assert.AreEqual(0.0, MotionKinematics.SmoothStep(-3.0), 1e-9);
            Assert.AreEqual(1.0, MotionKinematics.SmoothStep(2.0), 1e-9);
        }

        [Test]
        public void StepToward_AdvancesTowardTargetWithoutOvershooting()
        {
            Assert.AreEqual(0.4, MotionKinematics.StepToward(0.0, 1.0, 0.4), 1e-9);
            Assert.AreEqual(1.0, MotionKinematics.StepToward(0.9, 1.0, 0.4), 1e-9);
            Assert.AreEqual(0.6, MotionKinematics.StepToward(1.0, 0.0, 0.4), 1e-9);
        }

        [Test]
        public void StepToward_IgnoresNonPositiveDelta()
        {
            Assert.AreEqual(0.3, MotionKinematics.StepToward(0.3, 1.0, 0.0), 1e-9);
        }

        [Test]
        public void SwingAngle_SignFollowsHingeSide()
        {
            Assert.AreEqual(-55.0, MotionKinematics.SwingAngle(0.5, 110.0, -1.0), 1e-9);
            Assert.AreEqual(110.0, MotionKinematics.SwingAngle(1.0, 110.0, 1.0), 1e-9);
        }

        [Test]
        public void SlideDistance_ScalesWithProgress()
        {
            Assert.AreEqual(0.0, MotionKinematics.SlideDistance(0.0, 0.35), 1e-9);
            Assert.AreEqual(0.35, MotionKinematics.SlideDistance(1.0, 0.35), 1e-9);
        }
    }
}
