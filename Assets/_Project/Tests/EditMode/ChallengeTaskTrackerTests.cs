using System;
using NUnit.Framework;
using XRApartment.Simulation;

namespace XRApartment.Tests
{
    public class ChallengeTaskTrackerTests
    {
        [Test]
        public void Chain_IsTheEightStepsOfPRD84_InOrder()
        {
            CollectionAssert.AreEqual(
                new[]
                {
                    ChallengeStep.Approach, ChallengeStep.Open, ChallengeStep.TakeOut,
                    ChallengeStep.TemporaryPlacement, ChallengeStep.Reorganise,
                    ChallengeStep.PutBack, ChallengeStep.Close, ChallengeStep.Leave
                },
                ChallengeTaskTracker.StandardChain);
        }

        [Test]
        public void CompletingEveryStep_IsIdenticalRegardlessOfItemCount()
        {
            for (int items = ChallengeTaskTracker.MinItemCount; items <= ChallengeTaskTracker.MaxItemCount; items++)
            {
                var tracker = new ChallengeTaskTracker(items);
                int walked = 0;
                while (!tracker.IsComplete)
                {
                    Assert.IsTrue(tracker.TryAdvanceTo(tracker.CurrentStep.Value), "step " + walked);
                    walked++;
                }
                Assert.AreEqual(ChallengeTaskTracker.StandardChain.Count, walked);
            }
        }

        [Test]
        public void OutOfOrderAdvance_IsRejected()
        {
            var tracker = new ChallengeTaskTracker(4);
            Assert.AreEqual(ChallengeStep.Approach, tracker.CurrentStep.Value);
            Assert.IsFalse(tracker.TryAdvanceTo(ChallengeStep.Open));
            Assert.IsFalse(tracker.IsComplete);
            Assert.AreEqual(0, tracker.CompletedStepCount);
        }

        [TestCase(0, false)]
        [TestCase(2, false)]
        [TestCase(3, true)]
        [TestCase(5, true)]
        [TestCase(6, false)]
        public void ItemCount_OutsideThreeToFive_IsRejected(int items, bool expected)
        {
            Assert.AreEqual(expected, ChallengeTaskTracker.IsValidItemCount(items));
            if (!expected) Assert.Throws<ArgumentOutOfRangeException>(() => new ChallengeTaskTracker(items));
        }

        [Test]
        public void Reset_PutsTheSameBenchmarkBackAtTheFirstStep()
        {
            var tracker = new ChallengeTaskTracker(3);
            tracker.Advance();
            tracker.Advance();
            tracker.Reset();
            Assert.AreEqual(ChallengeStep.Approach, tracker.CurrentStep.Value);
            Assert.IsFalse(tracker.IsComplete);
        }
    }
}
