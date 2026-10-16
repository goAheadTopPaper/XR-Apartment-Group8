using UnityEngine;

namespace XRApartment.Data
{
    /// Controller-driven hand presentation. Lengths describe a virtual palm proxy,
    /// never the participant's anatomy or a kitchen clearance requirement.
    [CreateAssetMenu(menuName = "XR Apartment/Hand Interaction Profile", fileName = "SET_HandInteraction")]
    public class HandInteractionProfile : ScriptableObject
    {
        [SerializeField] ModelAssumptions assumptions;
        [SerializeField, Min(0.1f)] float fingerResponse = 10f;
        [SerializeField, Range(0f, 1f)] float hoverGrip = 0.12f;
        [SerializeField, Range(0f, 1f)] float holdingGrip = 0.72f;
        [SerializeField] LayerMask contactLayers = Physics.DefaultRaycastLayers;
        [SerializeField] Color hoverEmission = new Color(0.04f, 0.10f, 0.12f);
        [SerializeField] Color holdingEmission = new Color(0.06f, 0.16f, 0.18f);

        public ModelAssumptions Assumptions => assumptions;
        public float FingerResponse => fingerResponse;
        public float HoverGrip => hoverGrip;
        public float HoldingGrip => holdingGrip;
        public int ContactLayers => contactLayers;
        public Color HoverEmission => hoverEmission;
        public Color HoldingEmission => holdingEmission;

        public float Length(string key)
        {
            if (assumptions != null && assumptions.TryGetLength(key, out var length)) return length;
            throw new System.InvalidOperationException($"Hand profile requires registered parameter '{key}'.");
        }
    }
}
