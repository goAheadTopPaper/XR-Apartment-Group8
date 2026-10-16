using UnityEngine;

namespace XRApartment.Data
{
    [CreateAssetMenu(menuName = "XR Apartment/Water Interaction Profile", fileName = "SET_WaterInteraction")]
    public sealed class WaterInteractionProfile : ScriptableObject
    {
        [SerializeField] ModelAssumptions assumptions;
        [SerializeField] Material surfaceMaterial;
        [SerializeField] Material streamMaterial;
        [SerializeField] Material splashMaterial;
        [SerializeField, Min(1f)] float tapMillilitresPerSecond = 180f;
        [SerializeField, Min(0.1f)] float emptyingSeconds = 1.5f;
        [SerializeField, Min(0.1f)] float sloshFrequency = 9f;
        [SerializeField, Range(0.1f, 2f)] float sloshDamping = 0.8f;
        [SerializeField, Range(0f, 0.5f)] float maximumSloshSlope = 0.18f;

        public Material SurfaceMaterial => surfaceMaterial;
        public Material StreamMaterial => streamMaterial;
        public Material SplashMaterial => splashMaterial;
        public float TapMillilitresPerSecond => tapMillilitresPerSecond;
        public float EmptyingSeconds => emptyingSeconds;
        public float SloshFrequency => sloshFrequency;
        public float SloshDamping => sloshDamping;
        public float MaximumSloshSlope => maximumSloshSlope;

        public float Length(string key)
        {
            if (assumptions != null && assumptions.TryGetLength(key, out var value)) return value;
            throw new System.InvalidOperationException($"Water profile requires registered prototype parameter '{key}'.");
        }
    }
}
