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
            var sky = KitchenAuthoring.LoadRequired<Material>(
                KitchenAuthoring.Root + "Art/Lighting/MAT_Sky_KitchenDaylight.mat");
            sky.shader = Shader.Find("Skybox/Cubemap");
            sky.SetTexture("_Tex", cube);
            sky.SetColor("_Tint", new Color(.5f, .5f, .5f));
            sky.SetFloat("_Exposure", 1f);
            sky.SetFloat("_Rotation", 0f);
            RenderSettings.skybox = sky;
            DynamicGI.UpdateEnvironment();
            EditorUtility.SetDirty(sky);
            KitchenAuthoring.SaveScene(scene);
            Debug.Log("AllSky Free Cold Sunset applied; rebake lighting and reflection probes before final verification.");
        }
    }
}
