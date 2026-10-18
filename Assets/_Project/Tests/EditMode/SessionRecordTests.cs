using System.Linq;
using NUnit.Framework;
using XRApartment.Data;

namespace XRApartment.Tests
{
    public class SessionRecordTests
    {
        private static BenchmarkFeedback Complete(string id)
        {
            return new BenchmarkFeedback
            {
                combinationId = id,
                decision = CombinationDecision.NeedsRevision,
                category = FeedbackCategory.DoorClearance,
                experience = ExperienceTag.DoorBlocked | ExperienceTag.NeedsToStepBack,
                everydayImpact = EverydayImpact.ProlongsTask,
                importance = Importance.High,
                discoveryMode = DiscoveryMode.PresetPrompt,
                taskOutcome = TaskOutcome.Completed,
                shortExplanation = "Door blocks the route."
            };
        }

        private static SessionRecord SessionWith(params BenchmarkFeedback[] feedback)
        {
            var record = new SessionRecord
            {
                sessionId = "anon-0001",
                modelVersion = "0.1-graybox",
                finalPreferenceCombinationId = "B2",
                finalTradeOffSummary = "flush improves circulation, drawer still blocks",
                sessionOutcome = SessionOutcome.Completed
            };
            foreach (var f in feedback) record.benchmarkFeedback.Add(f);
            return record;
        }

        [Test]
        public void CompletedFeedback_HasNoMissingFields()
        {
            Assert.IsEmpty(Complete("B1").MissingRequiredFields());
            Assert.IsTrue(Complete("B1").HasAllRequiredFields());
        }

        [Test]
        public void CompletedFeedback_RequiresChoicesButAllowsEmptyExplanation()
        {
            var f = Complete("B1");
            f.importance = Importance.Unset;
            f.shortExplanation = " ";
            f.experience = ExperienceTag.None;
            f.everydayImpact = EverydayImpact.Unset;
            f.discoveryMode = DiscoveryMode.Unset;

            var missing = f.MissingRequiredFields().ToArray();
            CollectionAssert.AreEquivalent(new[] { "importance" }, missing);
            Assert.IsFalse(f.HasAllRequiredFields());
            f.importance = Importance.High;
            Assert.IsTrue(f.HasAllRequiredFields());
        }

        [Test]
        public void SkippedTask_RequiresAReason()
        {
            var f = new BenchmarkFeedback { combinationId = "B1", taskOutcome = TaskOutcome.Skipped };
            CollectionAssert.Contains(f.MissingRequiredFields().ToArray(), "outcomeReason");
            f.outcomeReason = "comfort stop";
            Assert.IsTrue(f.HasAllRequiredFields());
        }

        [Test]
        public void AbortedTask_IsNotAResolvedBenchmark()
        {
            var f = new BenchmarkFeedback { combinationId = "B1", taskOutcome = TaskOutcome.Aborted };
            CollectionAssert.Contains(f.MissingRequiredFields().ToArray(), "taskOutcome");
            Assert.IsFalse(f.HasAllRequiredFields());
        }

        [Test]
        public void Record_IncompleteWithoutFinalPreferenceOrModelVersion()
        {
            var record = SessionWith(Complete("B1"), Complete("B2"));
            Assert.IsTrue(record.IsComplete(2));

            record.modelVersion = null;
            Assert.IsFalse(record.IsComplete(2), "model_version must be traceable (FR-10)");

            record.modelVersion = "0.1-graybox";
            record.finalPreferenceCombinationId = null;
            Assert.IsFalse(record.IsComplete(2));
        }

        [Test]
        public void Record_WithABenchmarkStillUnjudged_IsNotComplete()
        {
            var half = Complete("B2");
            half.decision = CombinationDecision.Unset;

            Assert.IsFalse(SessionWith(Complete("B1"), half).IsComplete(2));
        }

        [Test]
        public void AllSkipped_IsCompleteAsIncompleteWithoutPreference()
        {
            var record = SessionWith(
                new BenchmarkFeedback { combinationId = "B1", taskOutcome = TaskOutcome.Skipped, outcomeReason = "comfort" },
                new BenchmarkFeedback { combinationId = "B2", taskOutcome = TaskOutcome.Skipped, outcomeReason = "fatigue" });
            record.sessionOutcome = SessionOutcome.Incomplete;
            record.finalPreferenceCombinationId = null;
            record.finalTradeOffSummary = null;
            Assert.IsTrue(record.IsComplete(2));
        }

        [Test]
        public void CompletedSession_RequiresMatchingOutcomeAndTradeOff()
        {
            var record = SessionWith(Complete("B1"), Complete("B2"));
            record.finalTradeOffSummary = null;
            Assert.IsFalse(record.IsComplete(2));
            record.finalTradeOffSummary = "B2 leaves more space.";
            record.sessionOutcome = SessionOutcome.Incomplete;
            Assert.IsFalse(record.IsComplete(2));
        }

        [Test]
        public void Record_CarriesNoPersonalIdentityField()
        {
            var fields = typeof(SessionRecord).GetFields().Select(f => f.Name.ToLower()).ToArray();
            foreach (var forbidden in new[] { "name", "email", "participant", "audio", "video", "account" })
            {
                Assert.IsFalse(fields.Any(f => f.Contains(forbidden)), "unexpected field for " + forbidden);
            }
        }
    }
}
