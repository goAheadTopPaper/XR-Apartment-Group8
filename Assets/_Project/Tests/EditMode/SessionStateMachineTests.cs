using NUnit.Framework;
using XRApartment.Simulation;

namespace XRApartment.Tests
{
    public class SessionStateMachineTests
    {
        [Test]
        public void HappyPath_ReachesFinishedThroughAllThreeBenchmarks()
        {
            var sm = new SessionStateMachine(3);

            Assert.IsTrue(sm.TryAdvance(SessionTrigger.CalibrationComplete));
            Assert.AreEqual(SessionState.Orientation, sm.Current);
            Assert.IsTrue(sm.TryAdvance(SessionTrigger.OrientationAcknowledged));
            Assert.AreEqual(SessionState.Explore, sm.Current);

            for (int i = 0; i < 3; i++)
            {
                Assert.IsTrue(sm.TryAdvance(SessionTrigger.ChallengeRequested));
                Assert.AreEqual(SessionState.ChallengeActive, sm.Current);
                Assert.IsTrue(sm.TryAdvance(SessionTrigger.ChallengeTaskFinished));
                Assert.AreEqual(SessionState.FeedbackPending, sm.Current);
                Assert.IsTrue(sm.TryAdvance(SessionTrigger.FeedbackSubmitted));
                Assert.AreEqual(SessionState.Explore, sm.Current);
            }

            Assert.IsTrue(sm.TryAdvance(SessionTrigger.PreferenceConfirmed));
            Assert.AreEqual(SessionState.ComparisonSummary, sm.Current);
            Assert.IsTrue(sm.TryAdvance(SessionTrigger.PreferenceConfirmed));
            Assert.AreEqual(SessionState.Finished, sm.Current);
        }

        [Test]
        public void PreferenceConfirmation_BlockedUntilEveryBenchmarkResolved()
        {
            var sm = new SessionStateMachine(3);
            sm.TryAdvance(SessionTrigger.CalibrationComplete);
            sm.TryAdvance(SessionTrigger.OrientationAcknowledged);

            Assert.IsFalse(sm.TryAdvance(SessionTrigger.PreferenceConfirmed));
            StringAssert.Contains("of 3", sm.LastRejection);
            Assert.AreEqual(SessionState.Explore, sm.Current);
        }

        [Test]
        public void SkippedBenchmark_StillCountsAsResolved_SoSafetyStopIsNotAFailure()
        {
            var sm = new SessionStateMachine(1);
            sm.TryAdvance(SessionTrigger.CalibrationComplete);
            sm.TryAdvance(SessionTrigger.OrientationAcknowledged);
            sm.TryAdvance(SessionTrigger.ChallengeRequested);

            Assert.IsTrue(sm.TryAdvance(SessionTrigger.ChallengeTaskSkipped));
            Assert.IsTrue(sm.TryAdvance(SessionTrigger.FeedbackSubmitted));
            Assert.IsTrue(sm.AllBenchmarksResolved);
            Assert.IsTrue(sm.TryAdvance(SessionTrigger.PreferenceConfirmed));
        }

        [Test]
        public void PauseAndResume_ReturnToTheStateTheyLeft()
        {
            var sm = new SessionStateMachine(3);
            sm.TryAdvance(SessionTrigger.CalibrationComplete);
            sm.TryAdvance(SessionTrigger.OrientationAcknowledged);
            sm.TryAdvance(SessionTrigger.ChallengeRequested);

            Assert.IsTrue(sm.TryAdvance(SessionTrigger.PauseRequested));
            Assert.AreEqual(SessionState.Paused, sm.Current);
            Assert.IsFalse(sm.TryAdvance(SessionTrigger.ChallengeTaskFinished));
            Assert.IsTrue(sm.TryAdvance(SessionTrigger.ResumeRequested));
            Assert.AreEqual(SessionState.ChallengeActive, sm.Current);
        }

        [Test]
        public void ExitRequested_IsAvailableFromAnyLiveState()
        {
            var sm = new SessionStateMachine(3);
            Assert.IsTrue(sm.TryAdvance(SessionTrigger.ExitRequested));
            Assert.AreEqual(SessionState.Finished, sm.Current);
            Assert.IsFalse(sm.TryAdvance(SessionTrigger.ExitRequested));
        }

        [Test]
        public void ChallengeCannotRestart_OnceEveryBenchmarkIsResolved()
        {
            var sm = new SessionStateMachine(1);
            sm.TryAdvance(SessionTrigger.CalibrationComplete);
            sm.TryAdvance(SessionTrigger.OrientationAcknowledged);
            sm.TryAdvance(SessionTrigger.ChallengeRequested);
            sm.TryAdvance(SessionTrigger.ChallengeTaskFinished);
            sm.TryAdvance(SessionTrigger.FeedbackSubmitted);

            Assert.IsFalse(sm.TryAdvance(SessionTrigger.ChallengeRequested));
            StringAssert.Contains("already resolved", sm.LastRejection);
        }
    }
}
