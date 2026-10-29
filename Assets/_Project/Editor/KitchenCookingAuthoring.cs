using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using XRApartment.Data;
using XRApartment.Runtime;

namespace XRApartment.EditorTools
{
    /// An optional cooking sandbox using the existing authored kitchen and XR rig.
    public static partial class KitchenCookingAuthoring
    {
        public const string ScenePath = KitchenAuthoring.Root + "Scenes/SCN_Kitchen_Cooking.unity";
        const string Art = KitchenAuthoring.Root + "Art/Cooking/";
        const string Prefabs = KitchenAuthoring.Prefabs + "Cooking/";
        const string ProfilePath = KitchenAuthoring.Settings + "SET_KitchenCooking.asset";
        const string StationPath = Prefabs + "PREF_Cooking_Workbench.prefab";

        [MenuItem("XR Apartment/Cooking/Open Cooking Kitchen")]
        public static void Open()
        {
            KitchenAuthoring.RequireEditMode();
            if (!File.Exists(ScenePath)) throw new InvalidOperationException("Build the cooking sandbox first.");
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
        }

        public static void BuildBatch()
        {
            KitchenAuthoring.RequireEditMode();
            KitchenAuthoring.EnsureFolder(Art.TrimEnd('/')); KitchenAuthoring.EnsureFolder(Prefabs.TrimEnd('/'));
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var name in new[] { "egg", "carrot", "apple", "lemon", "knife" })
            {
                var source = "ArtSource/KitchenAssets/" + name + "/v001/prepared/unity_asset.json";
                if (File.Exists(source) && !File.Exists(Generated(name))) KitchenPreparedAssetImporter.Import(source);
            }
            var profile = AssetDatabase.LoadAssetAtPath<KitchenCookingProfile>(ProfilePath);
            if (profile == null) { profile = ScriptableObject.CreateInstance<KitchenCookingProfile>(); AssetDatabase.CreateAsset(profile, ProfilePath); }
            foreach (var name in new[] { "plate", "tomato", "carrot", "apple", "lemon", "egg", "knife" })
            {
                var descriptor = KitchenAuthoring.Root + "Art/Generated/" + name + "_v001/unity_asset.json";
                if (!File.Exists(descriptor)) continue;
                var data = JsonUtility.FromJson<KitchenPreparedAssetImporter.Manifest>(File.ReadAllText(descriptor));
                Parameter("cooking_" + name + "_span", data.size_blender_xyz.Max());
            }
            Parameter("cooking_character_height", 1.75f);
            Parameter("cooking_cabinet_width", .6f); Parameter("cooking_cabinet_depth", .6f); Parameter("cooking_cabinet_height", 1f);
            foreach (var name in new[] { "tomato", "carrot", "apple", "lemon" })
                if (File.Exists(Generated(name))) ConfigureFood(name, profile);
            if (File.Exists(Generated("egg"))) KitchenAuthoring.EditPrefab(Generated("egg"), root =>
            {
                var egg = GetOrAdd<KitchenEgg>(root); egg.Configure(profile);
                ConfigureSplitFeedback(root, "egg");
            });
            KitchenAuthoring.EditPrefab(Generated("plate"), root => ConfigureSplitFeedback(root, "ceramic"));
            if (File.Exists(Generated("knife"))) ConfigureKnife();
            BuildVessel("MixingBowl", .075f, .008f, .05f, .004f, false, profile);
            BuildVessel("FryingPan", .105f, .008f, .025f, .004f, true, profile);
            BuildWhisk();
            BuildRecipeAssets();
            PrepareCookwareVisuals();
            ConfigureCookwareProps();
            BuildWorkbench();
            if (!File.Exists(ScenePath)) BuildScene();
            var cookingScene = EditorSceneManager.OpenScene(ScenePath);
            EnsureWaterSupport(cookingScene);
            ApplyCookingWaterVariants(cookingScene);
            var recipeRig = cookingScene.GetRootGameObjects().Single(go => go.name == "PREF_XR_KitchenRig");
            recipeRig.transform.SetPositionAndRotation(new Vector3(-3.15f, 0, 1.12f), Quaternion.identity);
            PrefabUtility.RecordPrefabInstancePropertyModifications(recipeRig.transform);
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflection = KitchenAuthoring.LoadRequired<Cubemap>("Assets/ThirdParty/AllSkyFree/ColdSunset/Cold Sunset Equirect.png");
            KitchenAuthoring.SaveScene(cookingScene);
            AssetDatabase.SaveAssets();
            Debug.Log("COOKING_AUTHORING_READY " + ScenePath);
        }

        [MenuItem("XR Apartment/Cooking/Rebuild Authored Workbench")]
        public static void RebuildMenu()
        {
            KitchenAuthoring.RequireEditMode();
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) BuildBatch();
        }

        static void EnsureWaterSupport(UnityEngine.SceneManagement.Scene scene)
        {
            var required = new[] { "PREF_Water_KitchenStation", "PREF_Prop_Cup", "PREF_Prop_Pot" };
            var present = new HashSet<string>(scene.GetRootGameObjects().Select(root => root.name));
            if (required.All(present.Contains)) return;
            var review = EditorSceneManager.OpenScene(KitchenAuthoring.ScenePath, OpenSceneMode.Additive);
            try
            {
                foreach (var source in review.GetRootGameObjects().Where(root => required.Contains(root.name) && !present.Contains(root.name)))
                {
                    var prefab = PrefabUtility.GetCorrespondingObjectFromSource(source);
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                    instance.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
                    instance.transform.localScale = source.transform.localScale;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
                }
            }
            finally { EditorSceneManager.CloseScene(review, true); UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene); }
        }

        public static void BakeLightingBatch()
        {
            KitchenAuthoring.RequireEditMode();
            var scene = EditorSceneManager.OpenScene(ScenePath);
            const string settingsPath = Art + "LGT_KitchenCooking.lighting";
            if (AssetDatabase.LoadAssetAtPath<LightingSettings>(settingsPath) == null)
            {
                var source = AssetDatabase.GetAssetPath(Lightmapping.lightingSettings);
                if (string.IsNullOrEmpty(source) || !AssetDatabase.CopyAsset(source, settingsPath))
                    throw new InvalidOperationException("Could not fork the cooking scene's lighting settings");
            }
            Lightmapping.lightingSettings = KitchenAuthoring.LoadRequired<LightingSettings>(settingsPath);
            // The 2022 Apple silicon OIDN pass thrashes memory in batch runs.
            // Keep the same light samples and use the lightweight Gaussian filter.
            if (Application.platform == RuntimePlatform.OSXEditor)
            {
                var settings = Lightmapping.lightingSettings;
                settings.filteringMode = LightingSettings.FilterMode.Advanced;
                settings.denoiserTypeDirect = LightingSettings.DenoiserType.None;
                settings.denoiserTypeIndirect = LightingSettings.DenoiserType.None;
                settings.denoiserTypeAO = LightingSettings.DenoiserType.None;
                settings.filterTypeDirect = LightingSettings.FilterType.Gaussian;
                settings.filterTypeIndirect = LightingSettings.FilterType.Gaussian;
                settings.filterTypeAO = LightingSettings.FilterType.Gaussian;
                EditorUtility.SetDirty(settings);
            }
            // The copied review data contains shadows from its static dressing.
            // Unlink it before baking this scene to its own output directory.
            Lightmapping.lightingDataAsset = null;
            KitchenAuthoring.SaveScene(scene);
            if (!Lightmapping.Bake()) throw new InvalidOperationException("Cooking lighting bake failed");
            var output = AssetDatabase.GetAssetPath(Lightmapping.lightingDataAsset);
            if (string.IsNullOrEmpty(output) || !output.Contains("SCN_Kitchen_Cooking/"))
                throw new InvalidOperationException("Cooking bake did not produce an independent lighting data asset: " + output);
            KitchenAuthoring.SaveScene(scene);
            Debug.Log("COOKING_LIGHTING_BAKED " + output + " target=" + EditorUserBuildSettings.activeBuildTarget);
        }

        static string Generated(string name) => KitchenAuthoring.Prefabs + "Generated/" + name + "_v001/PREF_Prop_" + name + ".prefab";

        static void ConfigureFood(string name, KitchenCookingProfile profile) => KitchenAuthoring.EditPrefab(Generated(name), root =>
        {
            var pieces = root.transform.Find("DerivedPieces").Cast<Transform>().Select(piece =>
            {
                var food = GetOrAdd<CookableIngredient>(piece.gameObject);
                food.Configure(profile, Array.Empty<CookableIngredient>()); return food;
            }).ToArray();
            var whole = GetOrAdd<CookableIngredient>(root); whole.Configure(profile, pieces);
        });

        static void ConfigureKnife() => KitchenAuthoring.EditPrefab(Generated("knife"), root =>
        {
            // Reviewed source: blade at -X, handle at +X, flat on the tabletop.
            var old = root.transform.Find("ACT_Blade"); if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var blade = Child(root.transform, "ACT_Blade");
            var trigger = blade.gameObject.AddComponent<BoxCollider>();
            trigger.isTrigger = true; trigger.center = new Vector3(-.055f, .006f, 0); trigger.size = new Vector3(.18f, .024f, .08f);
            var tip = Child(blade, "BladeTip"); tip.localPosition = new Vector3(-.14f, .009f, 0);
            blade.gameObject.AddComponent<PreparedKitchenKnife>().Configure(root.GetComponent<XRGrabInteractable>(), tip);
            var attach = root.transform.Find("ATT_Handle") ?? Child(root.transform, "ATT_Handle");
            attach.localPosition = new Vector3(.095f, .014f, 0);
            root.GetComponent<XRGrabInteractable>().attachTransform = attach;
            KitchenGrabAuthoring.ConfigureProp(root);
        });

        static void BuildVessel(string name, float radius, float bottom, float height, float wall, bool heated, KitchenCookingProfile profile)
        {
            var root = new GameObject("PREF_Cooking_" + name);
            try
            {
                var visual = Child(root.transform, "VIS_" + name).gameObject;
                visual.AddComponent<MeshFilter>().sharedMesh = SaveMesh("MESH_" + name, WaterVesselMeshBuilder.OpenVessel(radius, bottom, height, wall));
                visual.AddComponent<MeshRenderer>().sharedMaterial = Material(heated ? "PanSteel" : "WarmCeramic",
                    heated ? new Color(.045f, .05f, .055f) : new Color(.89f, .88f, .82f), heated ? .7f : .08f, .4f);
                var colliders = Child(root.transform, "COL_OpenVessel");
                var floor = colliders.gameObject.AddComponent<BoxCollider>(); floor.center = Vector3.up * (bottom - wall * .5f);
                floor.size = new Vector3(radius * 2, wall, radius * 2);
                for (var i = 0; i < 16; i++)
                {
                    var angle = 2 * Mathf.PI * i / 16;
                    var side = Child(colliders, "Wall_" + i);
                    side.localPosition = new Vector3(Mathf.Cos(angle) * (radius + wall / 2), bottom + height / 2, Mathf.Sin(angle) * (radius + wall / 2));
                    side.localRotation = Quaternion.Euler(0, -angle * Mathf.Rad2Deg, 0);
                    side.gameObject.AddComponent<BoxCollider>().size = new Vector3(wall, height, 2 * (radius + wall) * Mathf.Tan(Mathf.PI / 16));
                }
                if (heated) Cube(root.transform, "VIS_PanHandle", new Vector3(.19f, .028f, 0), new Vector3(.18f, .018f, .035f), Material("DarkHandle", new Color(.035f, .034f, .032f), 0, .24f));
                var surface = Child(root.transform, "LIQ_Egg"); surface.localPosition = Vector3.up * bottom;
                surface.gameObject.AddComponent<MeshFilter>();
                var renderer = surface.gameObject.AddComponent<MeshRenderer>(); renderer.sharedMaterial = LiquidMaterial();
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                var liquid = surface.gameObject.AddComponent<LiquidSurface>();
                var yolks = new Transform[3];
                for (var i = 0; i < yolks.Length; i++)
                {
                    var yolk = GameObject.CreatePrimitive(PrimitiveType.Sphere); yolk.name = "VIS_Yolk_" + i; yolk.transform.SetParent(root.transform, false);
                    UnityEngine.Object.DestroyImmediate(yolk.GetComponent<Collider>());
                    yolk.GetComponent<Renderer>().sharedMaterial = Material("EggYolk", new Color(1, .45f, .015f), 0, .58f);
                    yolk.SetActive(false); yolks[i] = yolk.transform;
                }
                var stream = Child(root.transform, "FX_EggStream").gameObject.AddComponent<LineRenderer>();
                stream.sharedMaterial = Material("EggStream", new Color(1, .67f, .1f), 0, .5f);
                stream.useWorldSpace = true; stream.widthMultiplier = .006f; stream.numCapVertices = 3; stream.enabled = false;
                var steam = Particles(root.transform, "FX_Steam", 16, .6f, .035f); steam.transform.localPosition = Vector3.up * (bottom + height);
                var emission = steam.emission; emission.enabled = false;
                root.AddComponent<KitchenFoodVessel>().Configure(profile, radius, bottom, height, heated, liquid, yolks, stream, steam);
                root.AddComponent<Rigidbody>().mass = heated ? .55f : .3f;
                root.AddComponent<XRGrabInteractable>(); KitchenGrabAuthoring.ConfigureProp(root);
                PrefabUtility.SaveAsPrefabAsset(root, Prefabs + root.name + ".prefab");
                Parameter("cooking_" + name.ToLowerInvariant() + "_radius", radius);
                Parameter("cooking_" + name.ToLowerInvariant() + "_bottom", bottom);
                Parameter("cooking_" + name.ToLowerInvariant() + "_height", height);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        static void BuildWhisk()
        {
            var root = new GameObject("PREF_Cooking_Whisk");
            try
            {
                var steel = Material("WhiskSteel", new Color(.65f, .68f, .7f), .9f, .6f);
                Cube(root.transform, "VIS_Handle", new Vector3(0, .23f, 0), new Vector3(.018f, .12f, .018f), Material("DarkHandle", new Color(.035f, .034f, .032f), 0, .24f));
                Cube(root.transform, "VIS_Shaft", new Vector3(0, .135f, 0), new Vector3(.004f, .07f, .004f), steel);
                for (var i = 0; i < 6; i++)
                {
                    var wire = Child(root.transform, "VIS_Wire_" + i).gameObject.AddComponent<LineRenderer>();
                    wire.useWorldSpace = false; wire.sharedMaterial = steel; wire.widthMultiplier = .0014f; wire.positionCount = 25;
                    var angle = i * Mathf.PI / 6;
                    for (var j = 0; j < 25; j++)
                    {
                        var t = j * 2 * Mathf.PI / 24;
                        wire.SetPosition(j, new Vector3(Mathf.Cos(angle) * Mathf.Sin(t) * .013f,
                            .059f + Mathf.Cos(t) * .045f, Mathf.Sin(angle) * Mathf.Sin(t) * .013f));
                    }
                }
                var head = Child(root.transform, "COL_WhiskHead").gameObject.AddComponent<BoxCollider>();
                head.center = Vector3.up * .06f; head.size = new Vector3(.027f, .09f, .027f);
                var tip = Child(root.transform, "StirTip"); tip.localPosition = Vector3.up * .02f;
                root.AddComponent<Rigidbody>().mass = .1f;
                var grab = root.AddComponent<XRGrabInteractable>(); KitchenGrabAuthoring.ConfigureProp(root);
                root.AddComponent<KitchenStirTool>().Configure(grab, tip);
                PrefabUtility.SaveAsPrefabAsset(root, Prefabs + root.name + ".prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        static void BuildWorkbench() => BuildRecipeWorkbench();

        static void BuildScene()
        {
            var review = EditorSceneManager.OpenScene(KitchenAuthoring.ScenePath);
            // Save a copy before touching any object; formal review content and build settings stay intact.
            EditorSceneManager.SaveScene(review, ScenePath, true);
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var keep = new HashSet<string> { "EventSystem", "XR Interaction Manager", "PREF_XR_KitchenRig", "PREF_Lighting_KitchenDaylight", "PREF_Kitchen_Environment", "PREF_Kitchen_TeleportArea" };
            foreach (var root in scene.GetRootGameObjects()) if (!keep.Contains(root.name)) UnityEngine.Object.DestroyImmediate(root);
            var station = (GameObject)PrefabUtility.InstantiatePrefab(KitchenAuthoring.LoadRequired<GameObject>(StationPath), scene);
            var rig = scene.GetRootGameObjects().Single(go => go.name == "PREF_XR_KitchenRig");
            rig.transform.position = new Vector3(-2.6f, 0, -.1f); rig.transform.rotation = Quaternion.Euler(0, 180, 0);
            PrefabUtility.RecordPrefabInstancePropertyModifications(rig.transform);
            KitchenAuthoring.SaveScene(scene);
        }

        static GameObject Place(Transform parent, string prefab, Vector3 position, List<Transform> targets)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(KitchenAuthoring.LoadRequired<GameObject>(prefab), parent);
            instance.transform.position = position; targets.Add(instance.transform); return instance;
        }
        static XRSimpleInteractable Button(Transform parent, string name, Vector3 position, Color colour)
        {
            var button = Cube(parent, "ACT_" + name, position, new Vector3(.075f, .03f, .055f), Material(name, colour, 0, .3f));
            var label = Child(button.transform, "Label"); label.localPosition = Vector3.up * .6f; label.localRotation = Quaternion.Euler(90, 180, 0);
            // Undo the visual cube scale for legible text in world metres.
            label.localScale = new Vector3(1 / .075f, 1 / .03f, 1 / .055f);
            var text = label.gameObject.AddComponent<TextMesh>(); text.text = name; text.characterSize = .007f; text.fontSize = 40; text.anchor = TextAnchor.MiddleCenter;
            return button.AddComponent<XRSimpleInteractable>();
        }
        static void ConfigureSplitFeedback(GameObject root, string type)
        {
            var fx = root.transform.Find("FX_Split");
            var particles = fx != null ? fx.GetComponent<ParticleSystem>() : Particles(root.transform, "FX_Split", 0, .32f, .004f);
            var main = particles.main; main.loop = false; main.playOnAwake = false; main.startSpeed = .5f; main.gravityModifier = .8f;
            main.startColor = type == "egg" ? new Color(.91f, .73f, .39f) : new Color(.82f, .8f, .74f);
            var emission = particles.emission; emission.SetBursts(new[] { new ParticleSystem.Burst(0, 14) });
            var audio = GetOrAdd<AudioSource>(particles.gameObject);
            audio.playOnAwake = false; audio.spatialBlend = 1; audio.minDistance = .2f; audio.maxDistance = 5;
            var clipPath = Art + "SFX_" + type + ".wav";
            if (File.Exists(clipPath)) audio.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
            var data = new SerializedObject(root.GetComponent<PreparedKitchenProp>());
            data.FindProperty("splitParticles").objectReferenceValue = particles;
            data.FindProperty("splitAudio").objectReferenceValue = audio; data.ApplyModifiedPropertiesWithoutUndo();
        }
        static ParticleSystem Particles(Transform parent, string name, float rate, float lifetime, float size)
        {
            var ps = Child(parent, name).gameObject.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.duration = 1; main.startLifetime = lifetime; main.startSpeed = .13f; main.startSize = size; main.maxParticles = 48;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission; emission.rateOverTime = rate;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.radius = .04f; shape.angle = 12;
            ps.transform.localRotation = Quaternion.Euler(-90, 0, 0);
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            var path = Art + "MAT_Cooking_Particles.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("XR Apartment/Water Droplet")) { color = Color.white };
                AssetDatabase.CreateAsset(material, path);
            }
            renderer.sharedMaterial = material;
            return ps;
        }
        static T GetOrAdd<T>(GameObject root) where T : Component
        {
            var component = root.GetComponent<T>();
            if (component == null) component = root.AddComponent<T>();
            return component;
        }
        static Transform Child(Transform parent, string name) { var go = new GameObject(name); go.transform.SetParent(parent, false); return go.transform; }
        static GameObject Cube(Transform parent, string name, Vector3 position, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(parent, false);
            go.transform.localPosition = position; go.transform.localScale = size; go.GetComponent<Renderer>().sharedMaterial = material; return go;
        }
        static Mesh SaveMesh(string name, Mesh mesh)
        {
            var path = Art + name + ".asset"; var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) { mesh.name = name; AssetDatabase.CreateAsset(mesh, path); return mesh; }
            EditorUtility.CopySerialized(mesh, existing); UnityEngine.Object.DestroyImmediate(mesh); return existing;
        }
        static Material Material(string name, Color colour, float metallic, float smoothness, bool emissive = false)
        {
            var path = Art + "MAT_Cooking_" + name + ".mat"; var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Standard")) { name = "MAT_Cooking_" + name, color = colour };
            material.SetFloat("_Metallic", metallic); material.SetFloat("_Glossiness", smoothness);
            if (emissive) material.EnableKeyword("_EMISSION");
            AssetDatabase.CreateAsset(material, path); return material;
        }
        static Material LiquidMaterial()
        {
            var material = Material("EggLiquid", Color.white, 0, .7f);
            material.SetFloat("_Mode", 2); material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha); material.SetInt("_ZWrite", 0);
            material.EnableKeyword("_ALPHABLEND_ON"); material.renderQueue = 3000; return material;
        }
        static void Parameter(string key, float value)
        {
            KitchenAuthoring.Assumptions.Upsert(new AssumptionParameter { key = key, valueMetres = value, owner = "Hzc", status = "Prototype assumption",
                source = "Optional cooking sandbox authored asset geometry; not a client dimension.", verification = "Editor bounds and interaction tests; VR handling pending." });
            EditorUtility.SetDirty(KitchenAuthoring.Assumptions);
        }
    }
}
