using UnityEditor;
using UnityEngine;

namespace XRApartment.EditorTools
{
    /// Applies the fixed kitchen sky; water authoring has no lighting side effects.
    public static class KitchenLightingAuthoring
    {
        [MenuItem("XR Apartment/Lighting/Apply AllSky Free Daylight")]
        public static void ApplyAllSky()
        {
            var scene = KitchenAuthoring.RequireKitchenScene();
            var cube = KitchenAuthoring.LoadRequired<Cubemap>(
                "Assets/ThirdParty/AllSkyFree/ColdSunset/Cold Sunset Equirect.png");
            KitchenHdrpAuthoring.ConfigureEnvironment(scene, cube, 0f);
            KitchenAuthoring.SaveScene(scene);
            Debug.Log("AllSky Free Cold Sunset applied; rebake lighting and reflection probes before final verification.");
        }
    }
}
