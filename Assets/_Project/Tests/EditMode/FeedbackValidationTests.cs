using NUnit.Framework;
using XRApartment.Data;
using XRApartment.Runtime.Feedback;
using XRApartment.Simulation;

namespace XRApartment.Tests
{
    public class FeedbackValidationTests
    {
        private FeedbackValidator validator;

        [SetUp]
        public void SetUp() => validator = new FeedbackValidator();

        [Test]
        public void CompletedFeedback_MapsFieldsToContractResult()
        {
            var request = Request("session-a", "B1", TaskOutcome.Completed);
            var draft = CompleteDraft("session-a", "B1");
            draft.Experience = ExperienceTag.DoorBlocked | ExperienceTag.HardToReach;
            draft.EverydayImpact = EverydayImpact.ProlongsTask;

            var result = validator.Validate(request, draft);

            Assert.IsTrue(result.IsValid);
            Assert.AreEqual("session-a", result.SessionId);
            Assert.AreEqual("B1", result.BenchmarkId);
            Assert.AreEqual("B1", result.Feedback.combinationId);
            Assert.AreEqual(draft.ShortExplanation, result.Feedback.shortExplanation);
            Assert.AreEqual(draft.Experience, result.Feedback.experience);
            Assert.AreEqual(draft.EverydayImpact, result.Feedback.everydayImpact);
            Assert.AreEqual(TaskOutcome.Completed, result.Feedback.taskOutcome);
        }

        [Test]
        public void CompletedFeedback_ReportsEveryMissingRequiredField()
        {
            var result = validator.Validate(Request("session-a", "B1", TaskOutcome.Completed),
                new FeedbackDraft { SessionId = "session-a", BenchmarkId = "B1" });

            Assert.IsFalse(result.IsValid);
            Assert.IsNull(result.Feedback);
            Assert.Contains("decision", new System.Collections.Generic.List<string>(result.FieldErrors.Keys));
            Assert.Contains("category", new System.Collections.Generic.List<string>(result.FieldErrors.Keys));
            Assert.Contains("importance", new System.Collections.Generic.List<string>(result.FieldErrors.Keys));
            Assert.Contains("shortExplanation", new System.Collections.Generic.List<string>(result.FieldErrors.Keys));
        }

        [Test]
        public void SkippedFeedback_RequiresOutcomeReasonButNotRatings()
        {
            var request = Request("session-a", "B2", TaskOutcome.Skipped);
            var draft = new FeedbackDraft { SessionId = "session-a", BenchmarkId = "B2" };
            Assert.IsFalse(validator.Validate(request, draft).IsValid);

            request.OutcomeReason = "Comfort stop";
            var result = validator.Validate(request, draft);
            Assert.IsTrue(result.IsValid);
            Assert.AreEqual("Comfort stop", result.Feedback.outcomeReason);
            Assert.AreEqual(CombinationDecision.Unset, result.Feedback.decision);
        }

        [Test]
        public void MismatchedIdentity_IsRejectedAtFieldLevel()
        {
            var result = validator.Validate(Request("session-a", "B1", TaskOutcome.Completed),
                CompleteDraft("session-a", "B2"));

            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.FieldErrors.ContainsKey("identity"));
        }

        [Test]
        public void DraftSession_PreservesEditsAcrossValidationAndUpdates()
        {
            var state = new FeedbackDraftSession();
            Assert.IsTrue(state.BeginRequest(Request("session-a", "B1", TaskOutcome.Completed)));
            var draft = state.Draft;
            draft.Decision = CombinationDecision.Accept;
            Assert.IsTrue(state.UpdateDraft(draft));

            var failed = state.Validate(validator);
            Assert.IsFalse(failed.IsValid);
            Assert.AreEqual(CombinationDecision.Accept, state.Draft.Decision);

            draft = state.Draft;
            draft.Category = FeedbackCategory.Circulation;
            draft.Importance = Importance.High;
            draft.ShortExplanation = "Clear route.";
            Assert.IsTrue(state.UpdateDraft(draft));
            Assert.IsTrue(state.Validate(validator).IsValid);
        }

        [Test]
        public void DraftSession_NewBenchmarkStartsCleanDraft()
        {
            var state = new FeedbackDraftSession();
            Assert.IsTrue(state.BeginRequest(Request("session-a", "B1", TaskOutcome.Completed)));
            var draft = CompleteDraft("session-a", "B1");
            Assert.IsTrue(state.UpdateDraft(draft));

            Assert.IsTrue(state.BeginRequest(Request("session-a", "B2", TaskOutcome.Completed)));

            Assert.AreEqual("B2", state.Draft.BenchmarkId);
            Assert.AreEqual(CombinationDecision.Unset, state.Draft.Decision);
            Assert.IsNull(state.LastValidation);
        }

        [Test]
        public void DraftSession_NewSessionStartsCleanDraft()
        {
            var state = new FeedbackDraftSession();
            Assert.IsTrue(state.BeginRequest(Request("session-a", "B1", TaskOutcome.Completed)));
            Assert.IsTrue(state.UpdateDraft(CompleteDraft("session-a", "B1")));

            Assert.IsTrue(state.BeginRequest(Request("session-b", "B1", TaskOutcome.Completed)));

            Assert.AreEqual("session-b", state.Draft.SessionId);
            Assert.AreEqual(CombinationDecision.Unset, state.Draft.Decision);
        }

        [Test]
        public void DraftSession_RejectsDraftFromAnotherBenchmark()
        {
            var state = new FeedbackDraftSession();
            Assert.IsTrue(state.BeginRequest(Request("session-a", "B1", TaskOutcome.Completed)));

            Assert.IsFalse(state.UpdateDraft(CompleteDraft("session-a", "B2")));
            Assert.AreEqual("B1", state.Draft.BenchmarkId);
        }

        [Test]
        public void DraftSession_ReenteringSameRequestPreservesSkipReasonEdit()
        {
            var state = new FeedbackDraftSession();
            Assert.IsTrue(state.BeginRequest(Request("session-a", "B1", TaskOutcome.Skipped)));
            var draft = state.Draft;
            draft.SkipReason = "Participant felt uncomfortable.";
            Assert.IsTrue(state.UpdateDraft(draft));

            Assert.IsTrue(state.BeginRequest(Request("session-a", "B1", TaskOutcome.Skipped)));

            Assert.AreEqual("Participant felt uncomfortable.", state.Draft.SkipReason);
            var result = state.Validate(validator);
            Assert.IsTrue(result.IsValid);
            Assert.AreEqual("Participant felt uncomfortable.", result.Feedback.outcomeReason);
        }

        private static FeedbackRequest Request(string session, string benchmark, TaskOutcome outcome)
        {
            return new FeedbackRequest
            {
                SessionId = session, BenchmarkId = benchmark, TaskOutcome = outcome,
                OutcomeReason = outcome == TaskOutcome.Skipped ? "Comfort stop" : null
            };
        }

        private static FeedbackDraft CompleteDraft(string session, string benchmark)
        {
            return new FeedbackDraft
            {
                SessionId = session,
                BenchmarkId = benchmark,
                Decision = CombinationDecision.Accept,
                Category = FeedbackCategory.Circulation,
                Importance = Importance.Medium,
                ShortExplanation = "The route feels clear."
            };
        }
    }
}
