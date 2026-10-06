using UnityEngine;
using XRApartment.Data;

namespace XRApartment.Runtime
{
    /// Marks what an object means to the review task and what may be done to it.
    /// Targets are resolved by role rather than by name so one task chain serves B1/B2/B3,
    /// and the status is what Orientation shows to the reviewer (UX-01).
    [DisallowMultipleComponent]
    public class ReviewElement : MonoBehaviour
    {
        [SerializeField] private ReviewRole role;
        [SerializeField] private ElementStatus status = ElementStatus.Simplified;
        [SerializeField] private string assumptionKey;
        [SerializeField] private string reviewerLabel;

        public ReviewRole Role => role;
        public ElementStatus Status => status;
        public string AssumptionKey => assumptionKey;
        public string ReviewerLabel => string.IsNullOrEmpty(reviewerLabel) ? name : reviewerLabel;

        public bool IsModifiableByReviewer => status == ElementStatus.Reviewable;
    }
}
