using XRApartment.Simulation;

namespace XRApartment.Runtime.Feedback
{
    /// <summary>
    /// Keeps an in-memory draft for one feedback request. It does not persist data or
    /// submit commands to Core; a different session, benchmark, or outcome starts fresh.
    /// </summary>
    public sealed class FeedbackDraftSession
    {
        private FeedbackRequest request;
        private FeedbackDraft draft;
        private FeedbackValidationResult lastValidation;

        public FeedbackRequest Request => Copy(request);
        public FeedbackDraft Draft => Copy(draft);
        public FeedbackValidationResult LastValidation => lastValidation;

        public bool BeginRequest(FeedbackRequest incoming)
        {
            if (incoming == null || string.IsNullOrWhiteSpace(incoming.SessionId) ||
                string.IsNullOrWhiteSpace(incoming.BenchmarkId))
            {
                request = null;
                draft = null;
                lastValidation = null;
                return false;
            }

            var sameRequest = request != null && request.SessionId == incoming.SessionId &&
                request.BenchmarkId == incoming.BenchmarkId && request.TaskOutcome == incoming.TaskOutcome;
            request = Copy(incoming);
            if (!sameRequest)
            {
                draft = new FeedbackDraft
                {
                    SessionId = incoming.SessionId,
                    BenchmarkId = incoming.BenchmarkId,
                    SkipReason = incoming.OutcomeReason
                };
                lastValidation = null;
            }
            return true;
        }

        public bool UpdateDraft(FeedbackDraft updated)
        {
            if (request == null || updated == null || updated.SessionId != request.SessionId ||
                updated.BenchmarkId != request.BenchmarkId) return false;
            draft = Copy(updated);
            draft.SessionId = request.SessionId;
            draft.BenchmarkId = request.BenchmarkId;
            lastValidation = null;
            return true;
        }

        public FeedbackValidationResult Validate(IFeedbackPort validator)
        {
            if (request == null || validator == null) return null;
            var validationRequest = Copy(request);
            if (validationRequest.TaskOutcome == XRApartment.Data.TaskOutcome.Skipped)
                validationRequest.OutcomeReason = draft.SkipReason;
            lastValidation = validator.Validate(validationRequest, Copy(draft));
            return lastValidation;
        }

        private static FeedbackRequest Copy(FeedbackRequest source)
        {
            if (source == null) return null;
            return new FeedbackRequest
            {
                SessionId = source.SessionId,
                BenchmarkId = source.BenchmarkId,
                TaskOutcome = source.TaskOutcome,
                OutcomeReason = source.OutcomeReason
            };
        }

        private static FeedbackDraft Copy(FeedbackDraft source)
        {
            if (source == null) return null;
            return new FeedbackDraft
            {
                SessionId = source.SessionId,
                BenchmarkId = source.BenchmarkId,
                Decision = source.Decision,
                Category = source.Category,
                Importance = source.Importance,
                ShortExplanation = source.ShortExplanation,
                SkipReason = source.SkipReason,
                Experience = source.Experience,
                EverydayImpact = source.EverydayImpact,
                DiscoveryMode = source.DiscoveryMode
            };
        }
    }
}
