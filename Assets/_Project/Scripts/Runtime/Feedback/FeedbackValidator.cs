using System.Collections.Generic;
using XRApartment.Data;
using XRApartment.Simulation;

namespace XRApartment.Runtime.Feedback
{
    /// <summary>Contract-based field validation and mapping for the formal Feedback port.</summary>
    public sealed class FeedbackValidator : IFeedbackPort
    {
        public FeedbackValidationResult Validate(FeedbackRequest request, FeedbackDraft draft)
        {
            var errors = new Dictionary<string, string>();
            if (request == null || draft == null ||
                string.IsNullOrWhiteSpace(request.SessionId) ||
                string.IsNullOrWhiteSpace(request.BenchmarkId) ||
                request.SessionId != draft.SessionId || request.BenchmarkId != draft.BenchmarkId)
            {
                errors["identity"] = "Feedback is for another session or benchmark.";
            }

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
                    errors["outcomeReason"] = "Give a skip reason.";
            }
            else
            {
                errors["taskOutcome"] = "Challenge has no result.";
            }

            ValidateOptionalEnums(draft, errors);
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

        private static void ValidateOptionalEnums(FeedbackDraft draft, IDictionary<string, string> errors)
        {
            if (draft == null) return;
            if (!System.Enum.IsDefined(typeof(CombinationDecision), draft.Decision))
                errors["decision"] = "Select a valid decision.";
            if (!System.Enum.IsDefined(typeof(FeedbackCategory), draft.Category))
                errors["category"] = "Select a valid category.";
            if (!System.Enum.IsDefined(typeof(Importance), draft.Importance))
                errors["importance"] = "Select a valid importance.";
            if (!System.Enum.IsDefined(typeof(EverydayImpact), draft.EverydayImpact))
                errors["everydayImpact"] = "Select a valid everyday impact.";
            if (!System.Enum.IsDefined(typeof(DiscoveryMode), draft.DiscoveryMode))
                errors["discoveryMode"] = "Select a valid discovery mode.";

            const ExperienceTag allowedExperience = ExperienceTag.DoorBlocked |
                ExperienceTag.NeedsToStepBack | ExperienceTag.HardToReach |
                ExperienceTag.NotEnoughTempSpace | ExperienceTag.HardToTurn | ExperienceTag.Other;
            if ((draft.Experience & ~allowedExperience) != 0)
                errors["experience"] = "Select valid experience tags.";
        }
    }
}
