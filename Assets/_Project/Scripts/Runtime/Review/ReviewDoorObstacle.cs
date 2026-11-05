using UnityEngine;

namespace XRApartment.Runtime
{
    /// <summary>A fixed cabinet part whose geometry may be cited in a review notice.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(BoxCollider))]
    public sealed class ReviewDoorObstacle : MonoBehaviour
    {
        [SerializeField] private string stableId;
        [SerializeField] private string displayName;
        [SerializeField] private string assumptionKey;

        public string StableId => stableId;
        public string DisplayName => displayName;
        public string AssumptionKey => assumptionKey;
    }
}
