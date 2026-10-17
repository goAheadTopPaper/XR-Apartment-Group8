using System.Collections.Generic;
using UnityEngine;
using XRApartment.Data;
using XRApartment.Simulation;

namespace XRApartment.Runtime.Debugging
{
    /// <summary>Temporary validator; formal Feedback owns the production form and validation.</summary>
    public sealed class MockFeedbackAdapter : MonoBehaviour, IFeedbackPort
    {
        public FeedbackValidationResult Validate(FeedbackRequest request, FeedbackDraft draft)
        {
            var errors = new Dictionary<string, string>();
            if (request == null || draft == null || request.SessionId != draft.SessionId ||
                request.BenchmarkId != draft.BenchmarkId)
                errors["identity"] = "Feedback is for another session or benchmark.";
            if (request != null && request.TaskOutcome == TaskOutcome.Completed)
            {
                if (draft == null || draft.Decision == CombinationDecision.Unset)
                    errors["decision"] = "Select a decision.";
                if (draft == null || draft.Category == FeedbackCategory.Unset)
                    errors["category"] = "Select a category.";
                if (draft == null || draft.Importance == Importance.Unset)
                    errors["importance"] = "Select importance.";
                if (draft == null || string.IsNullOrWhiteSpace(draft.ShortExplanation))
                    errors["shortExplanation"] = "Describe the reason briefly.";
            }
            else if (request != null && request.TaskOutcome == TaskOutcome.Skipped)
            {
                if (string.IsNullOrWhiteSpace(request.OutcomeReason))
                    errors["skipReason"] = "Give a skip reason.";
            }
            else errors["taskOutcome"] = "Challenge has no result.";

            var valid = errors.Count == 0;
            return new FeedbackValidationResult
            {
                SessionId = request?.SessionId,
                BenchmarkId = request?.BenchmarkId,
                IsValid = valid,
                FieldErrors = errors,
                Feedback = valid ? new BenchmarkFeedback
                {
                    combinationId = request.BenchmarkId,
                    decision = draft.Decision,
                    category = draft.Category,
                    importance = draft.Importance,
                    shortExplanation = draft.ShortExplanation,
                    experience = draft.Experience,
                    everydayImpact = draft.EverydayImpact,
                    discoveryMode = draft.DiscoveryMode,
                    taskOutcome = request.TaskOutcome,
                    outcomeReason = request.OutcomeReason
                } : null
            };
        }
    }
}
