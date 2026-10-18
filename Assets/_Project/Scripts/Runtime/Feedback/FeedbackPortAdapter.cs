using UnityEngine;
using XRApartment.Simulation;

namespace XRApartment.Runtime.Feedback
{
    /// <summary>Unity component adapter for assigning the contract validator to JourneyRuntimeHost.</summary>
    public sealed class FeedbackPortAdapter : MonoBehaviour, IFeedbackPort
    {
        private readonly FeedbackValidator validator = new FeedbackValidator();

        public FeedbackValidationResult Validate(FeedbackRequest request, FeedbackDraft draft)
        {
            return validator.Validate(request, draft);
        }
    }
}
