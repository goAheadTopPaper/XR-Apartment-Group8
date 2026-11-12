using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

namespace XRApartment.EditorTools
{
    internal static class KitchenHdrpAuthoring
    {
        internal const string Folder = KitchenAuthoring.Settings + "Rendering/";

        internal static void ConfigureCamera(Camera camera)
        {
            var data = camera.GetComponent<HDAdditionalCameraData>();
            if (data == null) data = camera.gameObject.AddComponent<HDAdditionalCameraData>();
            camera.allowMSAA = false;
            data.antialiasing = HDAdditionalCameraData.AntialiasingMode.FastApproximateAntialiasing;
            data.customRenderingSettings = true;
            data.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)FrameSettingsField.RayTracing] = true;
            data.renderingPathCustomFrameSettings.SetEnabled(FrameSettingsField.RayTracing, true);
            data.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)FrameSettingsField.SSR] = true;
            data.renderingPathCustomFrameSettings.SetEnabled(FrameSettingsField.SSR, true);
        }

        internal static HDRISky Sky(Scene scene)
        {
            var volume = LightingRoot(scene).GetComponent<Volume>();
            return RequireSky(volume.sharedProfile);
        }

        internal static void ConfigureEnvironment(Scene scene, Cubemap cubemap, float rotation)
        {
            var lighting = LightingRoot(scene);
            var profile = KitchenAuthoring.LoadRequired<VolumeProfile>(
                Folder + (scene.path == KitchenAuthoring.ScenePath ? "VOL_KitchenReview.asset" : "VOL_KitchenCooking.asset"));
            var sky = RequireSky(profile);
            sky.hdriSky.Override(cubemap);
            sky.rotation.Override(rotation);
            sky.skyIntensityMode.Override(SkyIntensityMode.Multiplier);
            sky.multiplier.Override(1f);
            EditorUtility.SetDirty(sky);

            var volume = lighting.GetComponent<Volume>();
            if (volume == null) volume = lighting.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
            PrefabUtility.RecordPrefabInstancePropertyModifications(volume);

            var bakedSky = lighting.GetComponent<StaticLightingSky>();
            if (bakedSky == null) bakedSky = lighting.AddComponent<StaticLightingSky>();
            bakedSky.profile = profile;
            bakedSky.staticLightingSkyUniqueID = (int)SkyType.HDRI;
            PrefabUtility.RecordPrefabInstancePropertyModifications(bakedSky);
        }

        static GameObject LightingRoot(Scene scene) =>
            scene.GetRootGameObjects().Single(root => root.name == "PREF_Lighting_KitchenDaylight");

        static HDRISky RequireSky(VolumeProfile profile)
        {
            if (profile != null && profile.TryGet(out HDRISky sky)) return sky;
            throw new InvalidOperationException("The kitchen lighting profile requires an HDRI Sky override.");
        }

        internal static void BakeReflections(Scene scene)
        {
            if (!(RenderPipelineManager.currentPipeline is HDRenderPipeline))
            {
                var camera = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Camera>()).First();
                camera.Render();
            }
            if (!ScriptableBakedReflectionSystemSettings.system.BakeAllReflectionProbes())
                throw new InvalidOperationException("HDRP reflection probe bake failed for " + scene.path);
            var probes = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<HDAdditionalReflectionData>());
            if (probes.Any(probe => probe.mode == ProbeSettings.Mode.Baked && probe.texture == null))
                throw new InvalidOperationException("An HDRP reflection probe has no baked cubemap in " + scene.path);
        }

        // Use physical light units while retaining the existing authored light's output.
        internal static HDAdditionalLightData ConfigureLight(Light light)
        {
            var type = light.type;
            var bakeType = light.lightmapBakeType;
            var intensity = light.intensity;
            var area = light.areaSize;
            var data = light.GetComponent<HDAdditionalLightData>();
            if (data == null) data = light.gameObject.AddComponent<HDAdditionalLightData>();
            switch (type)
            {
                case LightType.Directional:
                    data.SetIntensity(intensity, LightUnit.Lux);
                    data.shadowResolution.useOverride = false;
                    data.shadowResolution.level = 1;
                    break;
                case LightType.Rectangle:
                    data.SetLightTypeAndShape(HDLightTypeAndShape.RectangleArea);
                    data.SetAreaLightSize(area);
                    data.SetIntensity(intensity, LightUnit.Nits);
                    break;
                case LightType.Disc:
                    data.SetIntensity(intensity, LightUnit.Nits);
                    break;
                default:
                    data.SetIntensity(intensity, LightUnit.Candela);
                    break;
            }
            // HDRP represents rectangle lights as area-typed points and defaults new ones to realtime.
            light.lightmapBakeType = bakeType;
            PrefabUtility.RecordPrefabInstancePropertyModifications(light);
            PrefabUtility.RecordPrefabInstancePropertyModifications(data);
            return data;
        }
    }
}
