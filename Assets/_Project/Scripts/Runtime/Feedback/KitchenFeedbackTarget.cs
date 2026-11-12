using UnityEngine;

namespace XRApartment.Runtime
{
    [DisallowMultipleComponent]
    public sealed class KitchenFeedbackTarget : MonoBehaviour
    {
        [SerializeField] string objectId, displayName;

        public string ObjectId => objectId;
        public string DisplayName => displayName;

        public void Configure(string id, string label)
        {
            objectId = id;
            displayName = label;
        }
    }
}
