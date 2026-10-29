using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using XRApartment.Runtime;

namespace XRApartment.EditorTools
{
    public static partial class KitchenCookingAuthoring
    {
        const string CookwareArt = Art + "Cookware/";
        const string CookwareSource = "ArtSource/KitchenAssets/Recipe/v003_cookware/";
        static readonly string[] CookwareNames = { "fridge_body", "fridge_door", "frying_pan", "stockpot", "spatula", "ladle", "gas_hob", "whisk" };

        [MenuItem("XR Apartment/Cooking/Apply Realistic Kitchen and Gas Burner")]
        public static void ApplyRealisticKitchenMenu()
        {
            KitchenAuthoring.RequireEditMode();
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) BuildBatch();
        }

        static void PrepareCookwareVisuals()
        {
            KitchenAuthoring.EnsureFolder(CookwareArt.TrimEnd('/'));
            foreach (var name in CookwareNames)
            {
                var asset = "kitchen_" + name;
                if (!File.Exists(Generated(asset)))
                    KitchenPreparedAssetImporter.Import("ArtSource/KitchenAssets/" + asset + "/v001/prepared/unity_asset.json");
                File.Copy(CookwareSource + name + ".fbx", CookwareArt + name + ".fbx", true);
            }
            foreach (var name in new[] { "sink_basin", "sink_tap", "tap_lever" })
                File.Copy(CookwareSource+name+".fbx",CookwareArt+name+".fbx",true);
            File.Copy("ArtSource/KitchenAssets/References/kitchen_gas_flame_sprite_v001.png", CookwareArt + "TEX_GasFlame.png", true);
            File.Copy("ArtSource/KitchenAssets/References/kitchen_board_wood_v001.png", CookwareArt + "TEX_BoardWood.png", true);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var name in CookwareNames) KitchenPreparedAssetImporter.ConfigureModel(CookwareArt + name + ".fbx");
            foreach (var name in new[] { "sink_basin", "sink_tap", "tap_lever" }) KitchenPreparedAssetImporter.ConfigureModel(CookwareArt+name+".fbx");
            var importer = (TextureImporter)AssetImporter.GetAtPath(CookwareArt + "TEX_GasFlame.png");
            importer.alphaIsTransparency = true; importer.maxTextureSize = 256; importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp; importer.SaveAndReimport();
        }

        static Material CookwareMaterial(string sourceName)
        {
            var name = sourceName.Replace("(Instance)", "").Trim();
            var path = CookwareArt + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Standard")) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            if (name.StartsWith("MAT_Cookware_", StringComparison.Ordinal))
            {
                var asset = name.Substring("MAT_Cookware_".Length);
                var folder = KitchenAuthoring.Root + "Art/Generated/kitchen_" + asset + "_v001";
                material.color = Color.white;
                KitchenPreparedAssetImporter.SetMap(material, "_MainTex", "basecolor.png", folder);
                KitchenPreparedAssetImporter.SetMap(material, "_MetallicGlossMap", "metallic_smoothness.png", folder);
                KitchenPreparedAssetImporter.SetMap(material, "_BumpMap", "normal.png", folder);
                KitchenPreparedAssetImporter.SetMap(material, "_OcclusionMap", "ao.png", folder);
                material.SetFloat("_GlossMapScale", .6f); material.SetFloat("_BumpScale", asset.StartsWith("fridge") ? .02f : .22f);
                material.EnableKeyword("_METALLICGLOSSMAP"); material.EnableKeyword("_NORMALMAP");
                if (asset == "gas_hob")
                {
                    // A brushed tray needs a broad reflection, rather than the
                    // mirror-dark finish suggested by the generated metal map.
                    material.DisableKeyword("_METALLICGLOSSMAP"); material.SetTexture("_MetallicGlossMap",null);
                    material.SetFloat("_Metallic",.45f); material.SetFloat("_Glossiness",.42f);
                }
            }
            else
            {
                material.color = name.Contains("Shelf") ? new Color(.60f, .72f, .70f) : name.Contains("Gasket") ? new Color(.035f,.039f,.037f) :
                    name.Contains("Drain") ? new Color(.055f,.06f,.06f) : name.Contains("Chrome") ? new Color(.62f,.65f,.66f) :
                    name.Contains("SinkSteel") ? new Color(.45f,.48f,.49f) : name.Contains("VentSteel") ? new Color(.25f,.27f,.26f) : name.Contains("Enamel") ? new Color(.80f,.81f,.77f) : new Color(.85f, .85f, .80f);
                material.SetFloat("_Glossiness", name.Contains("Shelf") ? .72f : .4f);
                material.SetFloat("_Metallic", name.Contains("Chrome") ? .9f : name.Contains("SinkSteel") ? .8f : name.Contains("VentSteel") ? .7f : name.Contains("Enamel") ? .12f : 0);
            }
            EditorUtility.SetDirty(material); return material;
        }

        static GameObject CookwareModel(Transform parent, string name)
        {
            var model = UnityEngine.Object.Instantiate(KitchenAuthoring.LoadRequired<GameObject>(CookwareArt + name + ".fbx"), parent);
            model.name = "VIS_Real_" + name;
            model.transform.localPosition = Vector3.zero; model.transform.localRotation = Quaternion.identity;
            foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>(true))
            {
                renderer.gameObject.isStatic = false;
                renderer.sharedMaterials = renderer.sharedMaterials.Select(m => CookwareMaterial(m.name)).ToArray();
                renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
            }
            return model;
        }

        static void HideOldVisual(Transform old)
        {
            if (old == null) return;
            if (old.GetComponentsInChildren<Collider>(true).Length == 0)
            { UnityEngine.Object.DestroyImmediate(old.gameObject); return; }
            // Retain authored solid collision proxies, without loading a second
            // unused render mesh alongside the replacement.
            foreach (var renderer in old.GetComponentsInChildren<Renderer>(true)) UnityEngine.Object.DestroyImmediate(renderer);
            foreach (var filter in old.GetComponentsInChildren<MeshFilter>(true)) UnityEngine.Object.DestroyImmediate(filter);
            if (!old.name.StartsWith("COL_Proxy_",StringComparison.Ordinal)) old.name = "COL_Proxy_" + old.name;
        }

        static void RemoveRealVisual(GameObject root)
        {
            foreach (var child in root.transform.Cast<Transform>().Where(t => t.name.StartsWith("VIS_Real_", StringComparison.Ordinal)).ToArray())
                UnityEngine.Object.DestroyImmediate(child.gameObject);
        }

        static void ConfigureCookwareProps()
        {
            foreach (var path in new[] { Prefabs + "PREF_Cooking_FryingPan.prefab", RecipePrefabs + "PREF_Cooking_FryingPan.prefab" })
                KitchenAuthoring.EditPrefab(path, root =>
                {
                    RemoveRealVisual(root); HideOldVisual(root.transform.Find("VIS_FryingPan")); HideOldVisual(root.transform.Find("VIS_PanHandle"));
                    CookwareModel(root.transform, "frying_pan"); KitchenGrabAuthoring.ConfigureProp(root);
                });
            foreach (var path in new[] { Prefabs + "PREF_Cooking_MixingBowl.prefab", RecipePrefabs + "PREF_Cooking_MixingBowl.prefab", Prefabs + "PREF_Cooking_TomatoBowl.prefab" })
                KitchenAuthoring.EditPrefab(path, root =>
                {
                    RemoveRealVisual(root); var tomato = root.name.Contains("Tomato");
                    HideOldVisual(root.transform.Find(tomato ? "VIS_TomatoBowl" : "VIS_MixingBowl"));
                    var source = KitchenAuthoring.LoadRequired<GameObject>(KitchenAuthoring.Prefabs + "Props/PREF_Prop_Bowl.prefab");
                    var model = UnityEngine.Object.Instantiate(source.transform.Find("VIS_Bowl").gameObject, root.transform);
                    model.name = "VIS_Real_CeramicBowl";
                    foreach (var collider in model.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
                    foreach (var renderer in model.GetComponentsInChildren<Renderer>(true)) renderer.gameObject.isStatic = false;
                    FitVisual(model, new Vector3(tomato ? .188f : .158f, tomato ? .077f : .062f, tomato ? .188f : .158f), Vector3.zero);
                    KitchenGrabAuthoring.ConfigureProp(root);
                });
            KitchenAuthoring.EditPrefab(Prefabs + "PREF_Cooking_Whisk.prefab", root =>
            {
                RemoveRealVisual(root);
                foreach (var t in root.transform.Cast<Transform>().Where(t => t.name.StartsWith("VIS_", StringComparison.Ordinal)).ToArray()) HideOldVisual(t);
                CookwareModel(root.transform, "whisk"); KitchenGrabAuthoring.ConfigureProp(root);
            });
            KitchenAuthoring.EditPrefab(RecipePrefabs + "PREF_Cooking_Spatula.prefab", root =>
            {
                RemoveRealVisual(root); HideOldVisual(root.transform.Find("VIS_spatula"));
                CookwareModel(root.transform, "spatula"); KitchenGrabAuthoring.ConfigureProp(root);
            });
            KitchenAuthoring.EditPrefab(RecipePrefabs + "PREF_Cooking_Fridge.prefab", root =>
            {
                RemoveRealVisual(root); HideOldVisual(root.transform.Find("VIS_fridge_body"));
                CookwareModel(root.transform, "fridge_body");
                var hinge = root.transform.Find("ACT_FridgeDoor"); RemoveRealVisual(hinge.gameObject);
                HideOldVisual(hinge.Find("VIS_fridge_door")); CookwareModel(hinge, "fridge_door");
            });
            KitchenAuthoring.EditPrefab(RecipePrefabs + "PREF_Cooking_ChoppingBoard.prefab", root =>
            {
                var material = CookwareMaterial("MAT_BoardBeech"); material.color = Color.white;
                material.mainTexture = KitchenAuthoring.LoadRequired<Texture2D>(CookwareArt+"TEX_BoardWood.png");
                material.SetFloat("_Glossiness",.25f); EditorUtility.SetDirty(material);
                foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>()) renderer.sharedMaterial = material;
            });
            BuildLadle(); BuildCookingWaterVariants();
        }

        static void FitVisual(GameObject model, Vector3 size, Vector3 bottomCentre)
        {
            model.transform.localPosition = Vector3.zero; model.transform.localRotation = Quaternion.identity;
            var renderers = model.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds; foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            model.transform.localScale = Vector3.Scale(model.transform.localScale, new Vector3(size.x / bounds.size.x, size.y / bounds.size.y, size.z / bounds.size.z));
            bounds = renderers[0].bounds; foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            model.transform.position += model.transform.parent.TransformPoint(bottomCentre) - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        }

        static void BuildLadle()
        {
            var root = new GameObject("PREF_Cooking_Ladle");
            try
            {
                CookwareModel(root.transform, "ladle");
                var head = root.AddComponent<BoxCollider>(); head.center = new Vector3(0, .025f, 0); head.size = new Vector3(.07f, .045f, .07f);
                var handle = Child(root.transform, "COL_Handle").gameObject.AddComponent<BoxCollider>();
                handle.center = new Vector3(0, .05f, .15f); handle.size = new Vector3(.024f, .024f, .24f);
                root.AddComponent<Rigidbody>().mass = .14f; root.AddComponent<XRGrabInteractable>(); KitchenGrabAuthoring.ConfigureProp(root);
                PrefabUtility.SaveAsPrefabAsset(root, Prefabs + root.name + ".prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        static void BuildCookingWaterVariants()
        {
            foreach (var name in new[] { "Pot", "Cup" })
            {
                var path = Prefabs + "PREF_Cooking_Water" + name + ".prefab";
                if (!File.Exists(path)) AssetDatabase.CopyAsset(KitchenAuthoring.Prefabs + "PREF_Prop_" + name + ".prefab", path);
                KitchenAuthoring.EditPrefab(path, root =>
                {
                    RemoveRealVisual(root); HideOldVisual(root.transform.Find("BODY_" + name));
                    foreach (var t in root.transform.Cast<Transform>().Where(t => t.name.IndexOf("Handle",StringComparison.OrdinalIgnoreCase)>=0).ToArray()) HideOldVisual(t);
                    if (name == "Pot") CookwareModel(root.transform, "stockpot");
                    else
                    {
                        var source = KitchenAuthoring.LoadRequired<GameObject>(KitchenAuthoring.Prefabs + "Props/PREF_Prop_Mug.prefab");
                        var model = UnityEngine.Object.Instantiate(source.transform.Find("VIS_Mug").gameObject, root.transform);
                        model.name = "VIS_Real_WaterCup";
                        foreach (var collider in model.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
                        foreach (var renderer in model.GetComponentsInChildren<Renderer>(true)) renderer.gameObject.isStatic = false;
                        FitVisual(model, new Vector3(.095f, .106f, .080f), new Vector3(.008f, -.050f, 0));
                    }
                    KitchenGrabAuthoring.ConfigureProp(root);
                });
            }
            var stationPath=Prefabs+"PREF_Cooking_WaterStation.prefab";
            if (!File.Exists(stationPath)) AssetDatabase.CopyAsset(KitchenAuthoring.Prefabs+"PREF_Water_KitchenStation.prefab",stationPath);
            KitchenAuthoring.EditPrefab(stationPath,root=>
            {
                var basin=root.transform.Find("BASIN_Collect"); RemoveRealVisual(basin.gameObject); HideOldVisual(basin.Find("BODY_Basin")); CookwareModel(basin,"sink_basin");
                var tap=root.transform.Find("TAP_Water"); RemoveRealVisual(tap.gameObject);
                foreach(var child in tap.Cast<Transform>().Where(t=>t.name.StartsWith("PIPE_",StringComparison.Ordinal)).ToArray()) HideOldVisual(child);
                CookwareModel(tap,"sink_tap");
                var lever=tap.Find("CONTROL_TapLever");
                foreach(var renderer in lever.GetComponents<Renderer>()) UnityEngine.Object.DestroyImmediate(renderer);
                foreach(var filter in lever.GetComponents<MeshFilter>()) UnityEngine.Object.DestroyImmediate(filter);
                RemoveRealVisual(lever.gameObject); var model=CookwareModel(lever,"tap_lever");
                model.transform.localScale=new Vector3(1/lever.localScale.x,1/lever.localScale.y,1/lever.localScale.z);
            });
        }

        static void ApplyCookingWaterVariants(UnityEngine.SceneManagement.Scene scene)
        {
            Parameter("cooking_water_station_x",-4.73f); Parameter("cooking_water_station_y",1.115f); Parameter("cooking_water_station_z",1.18f);
            Parameter("cooking_sink_outer_radius",.192f); Parameter("cooking_fridge_body_outer_depth",.673f);
            foreach (var pair in new[] { ("PREF_Prop_Pot", "Pot", new Vector3(-4.73f, 1.175f, 1.86f)), ("PREF_Prop_Cup", "Cup", new Vector3(-4.73f, 1.165f, 2.16f)) })
            {
                var old = scene.GetRootGameObjects().Single(g => g.name == pair.Item1);
                UnityEngine.Object.DestroyImmediate(old);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(KitchenAuthoring.LoadRequired<GameObject>(Prefabs + "PREF_Cooking_Water" + pair.Item2 + ".prefab"), scene);
                instance.name = pair.Item1; instance.transform.position = pair.Item3;
                PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
                PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
            }
            UnityEngine.Object.DestroyImmediate(scene.GetRootGameObjects().Single(g => g.name == "PREF_Water_KitchenStation"));
            var water=(GameObject)PrefabUtility.InstantiatePrefab(KitchenAuthoring.LoadRequired<GameObject>(Prefabs+"PREF_Cooking_WaterStation.prefab"),scene);
            water.name="PREF_Water_KitchenStation";
            water.transform.SetPositionAndRotation(new Vector3(-4.73f, 1.115f, 1.18f), Quaternion.Euler(0, 90, 0));
            PrefabUtility.RecordPrefabInstancePropertyModifications(water.transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(water);
        }

        static KitchenHeatSource BuildGasBurner(Transform parent, Vector3 position)
        {
            var model = CookwareModel(parent, "gas_hob"); model.transform.position = position;
            model.transform.localRotation = Quaternion.Euler(0,180,0);
            var supports = Child(parent, "COL_GasHob"); supports.position = position;
            var slab = supports.gameObject.AddComponent<BoxCollider>(); slab.center = Vector3.up * .017f; slab.size = new Vector3(.32f, .034f, .36f);
            foreach (var side in new[] { -1f, 1f })
            {
                var support = Child(supports, "COL_PanSupport_" + side).gameObject.AddComponent<BoxCollider>();
                support.center = new Vector3(side * .076f, .048f, 0); support.size = new Vector3(.018f, .014f, .22f);
            }
            var knob = Child(parent, "ACT_STOVE"); knob.position = position + new Vector3(0, .025f, -.18f);
            knob.gameObject.AddComponent<BoxCollider>().size = new Vector3(.062f, .045f, .055f);
            var button = knob.gameObject.AddComponent<XRSimpleInteractable>();
            var glow = Cube(parent, "VIS_GasIndicator", position + new Vector3(.055f, .035f, -.13f), Vector3.one * .007f,
                Material("BurnerGlow", new Color(.12f, .08f, .07f), 0, .2f, true));
            UnityEngine.Object.DestroyImmediate(glow.GetComponent<Collider>());
            var surface = Child(parent, "LOC_BurnerSurface"); surface.position = position + Vector3.up * .055f;
            var heat = Child(parent, "ACT_HeatZone"); heat.position = position + Vector3.up * .105f;
            var zone = heat.gameObject.AddComponent<BoxCollider>(); zone.isTrigger = true; zone.size = new Vector3(.23f, .13f, .23f);
            var stove = heat.gameObject.AddComponent<KitchenHeatSource>(); stove.Configure(button, glow.GetComponent<Renderer>()); stove.SetHeatSurface(surface);
            stove.ConfigureFlame(BuildFlame(parent, position + Vector3.up * .035f));
            Parameter("cooking_burner_support_height", .055f); Parameter("cooking_gas_flame_radius", .073f); Parameter("cooking_gas_flame_height", .035f);
            return stove;
        }

        static KitchenBurnerFlame BuildFlame(Transform parent, Vector3 position)
        {
            var root = Child(parent, "FX_GasBurner"); root.position = position;
            var ps = root.gameObject.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.playOnAwake = false; main.loop = true; main.maxParticles = 96;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.12f, .18f); main.startSpeed = 0;
            main.startSize3D = true; main.startSizeX = new ParticleSystem.MinMaxCurve(.009f, .013f); main.startSizeY = .035f; main.startSizeZ = .01f;
            main.startColor = Color.white; main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var emission = ps.emission; emission.enabled = true; emission.rateOverTime = 420;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = .073f; shape.radiusThickness = .04f; shape.rotation = new Vector3(90,0,0);
            var velocity = ps.velocityOverLifetime; velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.Local; velocity.y = .06f;
            var colours = ps.colorOverLifetime; colours.enabled = true;
            var gradient = new Gradient(); gradient.SetKeys(new[] { new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1) },
                new[] { new GradientAlphaKey(0,0),new GradientAlphaKey(.95f,.15f),new GradientAlphaKey(.65f,.7f),new GradientAlphaKey(0,1) }); colours.color = gradient;
            var renderer = ps.GetComponent<ParticleSystemRenderer>(); renderer.renderMode = ParticleSystemRenderMode.VerticalBillboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            var path = CookwareArt + "MAT_GasFlame.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("Particles/Standard Unlit")) { name = "MAT_GasFlame" }; AssetDatabase.CreateAsset(material, path); }
            material.mainTexture = KitchenAuthoring.LoadRequired<Texture2D>(CookwareArt + "TEX_GasFlame.png");
            material.SetFloat("_Mode", 4); material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha); material.SetInt("_DstBlend", (int)BlendMode.One);
            material.SetInt("_ZWrite",0); material.SetColor("_Color", new Color(1.1f,1.1f,1.1f,1)); material.renderQueue = 3000; EditorUtility.SetDirty(material);
            renderer.sharedMaterial = material;
            var lamp = Child(root, "LGT_GasBlue").gameObject.AddComponent<Light>(); lamp.type = LightType.Point;
            lamp.color = new Color(.18f,.4f,1); lamp.range = .35f; lamp.intensity = .18f; lamp.shadows = LightShadows.None;
            var flame = root.gameObject.AddComponent<KitchenBurnerFlame>(); flame.Configure(ps,lamp); return flame;
        }
    }
}
