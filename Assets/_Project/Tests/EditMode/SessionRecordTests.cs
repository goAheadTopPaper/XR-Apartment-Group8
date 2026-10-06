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
                taskOutcome = TaskOutcome.Completed
            };
        }

        private static SessionRecord SessionWith(params BenchmarkFeedback[] feedback)
        {
            var record = new SessionRecord
            {
                sessionId = "anon-0001",
                modelVersion = "0.1-graybox",
                finalPreferenceCombinationId = "B2",
                finalTradeOffSummary = "flush improves circulation, drawer still blocks"
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
        public void MissingImportanceOrDiscoveryMode_IsReportedByFieldName()
        {
            var f = Complete("B1");
            f.importance = Importance.Unset;
            f.discoveryMode = DiscoveryMode.Unset;

            var missing = f.MissingRequiredFields().ToArray();
            CollectionAssert.AreEquivalent(new[] { "importance", "discoveryMode" }, missing);
            Assert.IsFalse(f.HasAllRequiredFields());
        }

        [Test]
        public void SkippedTask_RequiresAReason()
        {
            var f = Complete("B1");
            f.taskOutcome = TaskOutcome.Skipped;
            CollectionAssert.Contains(f.MissingRequiredFields().ToArray(), "outcomeReason");
        }

        [Test]
        public void Record_IncompleteWithoutFinalPreferenceOrModelVersion()
        {
            var record = SessionWith(Complete("B1"), Complete("B2"), Complete("B3"));
            Assert.IsTrue(record.IsComplete(3));

            record.modelVersion = null;
            Assert.IsFalse(record.IsComplete(3), "model_version must be traceable (FR-10)");

            record.modelVersion = "0.1-graybox";
            record.finalPreferenceCombinationId = null;
            Assert.IsFalse(record.IsComplete(3));
        }

        [Test]
        public void Record_WithABenchmarkStillUnjudged_IsNotComplete()
        {
            var half = Complete("B3");
            half.decision = CombinationDecision.Unset;

            Assert.IsFalse(SessionWith(Complete("B1"), Complete("B2"), half).IsComplete(3));
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
