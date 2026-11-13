using UnityEngine;

namespace XRApartment.Runtime.ReviewV2
{
    public sealed class ReviewSceneIdentity : MonoBehaviour
    {
        [SerializeField] string id;
        public string Id => id;
        public void Configure(string stableId) => id = stableId;
    }
}
