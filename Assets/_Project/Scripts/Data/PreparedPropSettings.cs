using UnityEngine;

namespace XRApartment.Data
{
    [CreateAssetMenu(menuName = "XR Apartment/Prepared Prop Settings")]
    public sealed class PreparedPropSettings : ScriptableObject
    {
        [Min(.01f)] public float impactSpeed = 1.6f;
        [Min(.01f)] public float knifeSpeed = .15f;
        [Min(0f)] public float separationSpeed = .12f;
        [Min(.1f)] public float fragmentLifetime = 8f;
        [Min(.1f)] public float maxFragmentSpeed = 6f;
        [Min(.001f)] public float intactMass = .25f;

        // Interaction tuning for the asset lab, not measured client properties.
    }
}
