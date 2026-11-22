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
        [SerializeField, Range(1f, 10f)] float grabRotationStepDegrees = 3f;
        [SerializeField, Range(1, 64)] int grabRotationIterations = 32;
        [SerializeField, Min(0f)] float maxReleaseSpeed = 3f;
        [SerializeField, Min(0f)] float maxReleaseAngularSpeed = 12f;
        [SerializeField, Range(0f, 1f)] float blockedHapticAmplitude = 0.18f;
        [SerializeField, Min(0.01f)] float blockedHapticDuration = 0.035f;
        [SerializeField, Min(0.05f)] float blockedHapticInterval = 0.15f;
        [SerializeField, Range(1f, 90f)] float ghostShowAngle = 25f;
        [SerializeField, Range(0f, 90f)] float ghostHideAngle = 15f;

        public ModelAssumptions Assumptions => assumptions;
        public float FingerResponse => fingerResponse;
        public float HoverGrip => hoverGrip;
        public float HoldingGrip => holdingGrip;
        public int ContactLayers => contactLayers;
        public Color HoverEmission => hoverEmission;
        public Color HoldingEmission => holdingEmission;
        public float GrabRotationStepDegrees => grabRotationStepDegrees;
        public int GrabRotationIterations => grabRotationIterations;
        public float MaxReleaseSpeed => maxReleaseSpeed;
        public float MaxReleaseAngularSpeed => maxReleaseAngularSpeed;
        public float BlockedHapticAmplitude => blockedHapticAmplitude;
        public float BlockedHapticDuration => blockedHapticDuration;
        public float BlockedHapticInterval => blockedHapticInterval;
        public float GhostShowAngle => ghostShowAngle;
        public float GhostHideAngle => ghostHideAngle;

        public float Length(string key)
        {
            if (assumptions != null && assumptions.TryGetLength(key, out var length)) return length;
            throw new System.InvalidOperationException($"Hand profile requires registered parameter '{key}'.");
        }
    }
}
