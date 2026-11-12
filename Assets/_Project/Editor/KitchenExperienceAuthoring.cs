using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using XRApartment.Runtime;
using Object = UnityEngine.Object;

namespace XRApartment.EditorTools
{
    public static partial class KitchenCookingAuthoring
    {
        const string CookingFill = Prefabs + "PREF_Lighting_CookingFill.prefab";
        const string TomatoCutModel = FoodArt + "tomato_cut_stages.fbx";
        const string RoomEnvelope = "COL_KitchenEnvelope";
        internal const string TomatoRemainder = "RemainingSlices";

        [MenuItem("XR Apartment/Cooking/Apply Physical Interaction and Lighting")]
        public static void ApplyExperienceMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) ApplyExperienceBatch();
        }

        public static void ApplyExperienceBatch()
        {
            KitchenAuthoring.RequireEditMode();
            if (AssetImporter.GetAtPath(TomatoCutModel) is ModelImporter importer && !importer.isReadable)
                KitchenPreparedAssetImporter.ConfigureModel(TomatoCutModel);
            ConfigureSharedExperience();
            var scene = EditorSceneManager.OpenScene(ScenePath);
            ConfigureCookingFill(scene);
            KitchenAuthoring.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        static void ConfigureSharedExperience()
        {
            KitchenAuthoring.EditPrefab(KitchenAuthoring.Prefabs + "PREF_XR_KitchenRig.prefab", root =>
            {
                var audio = GetOrAdd<KitchenAudio>(root);
                audio.Configure(Enum.GetNames(typeof(KitchenCue))
                    .Select(cue => KitchenAuthoring.LoadRequired<AudioClip>(Art + "SFX_" + cue + ".wav")).ToArray());
            });
            KitchenAuthoring.EditPrefab(KitchenAuthoring.Prefabs + "PREF_Kitchen_Environment.prefab", ConfigureRoomEnvelope);
            KitchenAuthoring.EditPrefab(KitchenAuthoring.Prefabs + "PREF_Lighting_KitchenDaylight.prefab", ConfigureDaylightLighting);
            KitchenAuthoring.EditPrefab(KitchenAuthoring.Prefabs + "PREF_Fridge_F2.prefab",
                root => ConfigurePull(root, "Handle"));
            KitchenAuthoring.EditPrefab(RecipePrefabs + "PREF_Cooking_Fridge.prefab",
                root => ConfigurePull(root, "VIS_FridgeHandle"));
            KitchenAuthoring.EditPrefab(RecipeTomato, ConfigureProgressiveTomato);
        }

        static void ConfigurePull(GameObject root, string handleName)
        {
            var door = root.GetComponentInChildren<KinematicSwingDoor>(true);
            var handle = door.GetComponentsInChildren<Transform>(true).Single(t => t.name == handleName);
            door.ConfigurePullHandle(handle);
            var target = door.GetComponent<XRSimpleInteractable>();
            // Keep the panel registered so XRI's far caster treats it as an occluding target.
            var colliders = door.GetComponentsInChildren<Collider>(true).Where(c => c.enabled && !c.isTrigger).ToArray();
            if (!colliders.Any(c => c.transform.IsChildOf(handle)))
                throw new InvalidOperationException("No physical handle collider on " + root.name);
            target.colliders.Clear();
            target.colliders.AddRange(colliders);
        }

        static void ConfigureProgressiveTomato(GameObject root)
        {
            var previous = root.transform.Find(TomatoRemainder);
            if (previous != null) Object.DestroyImmediate(previous.gameObject);
            var preview = Child(root.transform, TomatoRemainder);
            var prop = root.GetComponent<PreparedKitchenProp>();
            var intact = root.transform.Find("Intact");
            var skin = intact.GetComponentsInChildren<MeshRenderer>()
                .Single(renderer => renderer.name.StartsWith("VIS_", StringComparison.Ordinal)).sharedMaterial;
            var flesh = KitchenAuthoring.LoadRequired<Material>(FoodArt + "MAT_Food_TomatoFleshDetailed.mat");
            var model = KitchenAuthoring.LoadRequired<GameObject>(TomatoCutModel);
            var meshes = model.GetComponentsInChildren<MeshFilter>().ToDictionary(mesh => mesh.name);
            foreach (var slice in prop.Pieces)
            {
                var visual = slice.transform.Find("Intact").GetComponentsInChildren<MeshFilter>()
                    .Single(mesh => mesh.name.StartsWith("VIS_", StringComparison.Ordinal));
                var source = meshes[visual.name.Replace("(Clone)", "")];
                visual.sharedMesh = source.sharedMesh;
                ApplyCutMaterials(visual.GetComponent<MeshRenderer>(), source.GetComponent<MeshRenderer>());
            }

            var stages = new Transform[prop.Pieces.Count - 2];
            for (var i = 0; i < stages.Length; i++)
            {
                var suffix = (i + 1).ToString("00");
                var stage = stages[i] = Child(preview, "AfterCut_" + suffix);
                KitchenPreparedAssetImporter.CopyMesh(model, "VIS_Remainder_" + suffix, stage);
                var visual = stage.GetComponentInChildren<MeshRenderer>();
                ApplyCutMaterials(visual, visual);
                KitchenPreparedAssetImporter.CopyMesh(model, "COL_Remainder_" + suffix, stage);
                var hull = stage.GetComponentsInChildren<MeshFilter>().Single(mesh => mesh.name.StartsWith("COL_", StringComparison.Ordinal));
                var collider = hull.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = hull.sharedMesh;
                collider.convex = true;
                collider.sharedMaterial = intact.GetComponentInChildren<Collider>().sharedMaterial;
                Object.DestroyImmediate(hull.GetComponent<MeshRenderer>());
                Object.DestroyImmediate(hull);
                stage.gameObject.SetActive(false);
            }
            prop.ConfigureProgressiveCuts(stages);

            void ApplyCutMaterials(MeshRenderer target, MeshRenderer source) =>
                target.sharedMaterials = source.sharedMaterials.Select(material =>
                    material.name.StartsWith("MAT_Interior_", StringComparison.Ordinal) ? flesh : skin).ToArray();
        }

        static void ConfigureRoomEnvelope(GameObject root)
        {
            var floor = root.GetComponentsInChildren<MeshCollider>(true).Single(c => c.name == "SM_Ground");
            var old = root.transform.Find(RoomEnvelope);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var envelope = Child(root.transform, RoomEnvelope);
            var bounds = floor.bounds;
            const float skin = .08f;
            const float height = 3.2f;
            var mid = bounds.center;
            mid.y = bounds.max.y + height * .5f;
            Add("Underfloor", new Vector3(mid.x, bounds.max.y - .065f, mid.z),
                new Vector3(bounds.size.x, .12f, bounds.size.z));
            Add("West", new Vector3(bounds.min.x - skin * .5f, mid.y, mid.z),
                new Vector3(skin, height, bounds.size.z + skin * 2f));
            Add("East", new Vector3(bounds.max.x + skin * .5f, mid.y, mid.z),
                new Vector3(skin, height, bounds.size.z + skin * 2f));
            Add("South", new Vector3(mid.x, mid.y, bounds.min.z - skin * .5f),
                new Vector3(bounds.size.x, height, skin));
            Add("North", new Vector3(mid.x, mid.y, bounds.max.z + skin * .5f),
                new Vector3(bounds.size.x, height, skin));
            envelope.gameObject.AddComponent<KitchenRoomBounds>().Configure(floor);
            Parameter("kitchen_virtual_edge_height", height);
            Parameter("kitchen_virtual_edge_thickness", skin);

            void Add(string name, Vector3 centre, Vector3 size)
            {
                var wall = Child(envelope, name);
                wall.SetPositionAndRotation(centre, Quaternion.identity);
                wall.gameObject.AddComponent<BoxCollider>().size = size;
            }
        }

        static void ConfigureDaylightLighting(GameObject root)
        {
            var group = root.GetComponentInChildren<LightProbeGroup>();
            var positions = new List<Vector3>(group.probePositions);
            // Thin cookware can settle with its lighting sample about 2 cm from the floor.
            foreach (var y in new[] { .008f, .24f })
            {
                foreach (var x in new[] { -4.84f, -4.15f, -3.5f, -2.8f, -2.1f, -1.68f })
                    foreach (var z in new[] { .67f, 1.20f, 1.66f, 2.22f })
                        positions.Add(group.transform.InverseTransformPoint(new Vector3(x, y, z)));
                foreach (var x in new[] { -4.15f, -3f, -1.85f, -.7f })
                    foreach (var z in new[] { -.32f, -1.45f, -2.6f, -3.75f, -4.9f, -6.1f, -7.3f })
                        positions.Add(group.transform.InverseTransformPoint(new Vector3(x, y, z)));
            }
            // The passage and its return to the kitchen extend beyond the original probe volume.
            foreach (var y in new[] { .10f, 1.05f, 1.65f, 2.35f })
                foreach (var x in new[] { -.75f, -.36f, .01f })
                    foreach (var z in new[] { -7.60f, -6.65f, -5.75f, -4.70f, -3.65f, -2.60f,
                        -1.55f, -.55f, .35f, 1.30f, 2.16f })
                        positions.Add(group.transform.InverseTransformPoint(new Vector3(x, y, z)));
            group.probePositions = positions.Distinct().ToArray();

            PassageFill("Entry passage fill", -4.85f, .9f);
            PassageFill("Kitchen passage fill", .4f, .75f);
            ConfigureApartmentLights(root);

            void PassageFill(string name, float z, float intensity)
            {
                var lamp = root.transform.Find(name) ?? Child(root.transform, name);
                lamp.localPosition = new Vector3(-.37f, 2.52f, z);
                lamp.localRotation = Quaternion.Euler(90f, 0f, 0f);
                var light = GetOrAdd<Light>(lamp.gameObject);
                light.type = LightType.Rectangle;
                light.lightmapBakeType = LightmapBakeType.Baked;
                light.areaSize = new Vector2(.64f, 3.4f);
                light.intensity = intensity;
                light.range = 4.6f;
                light.color = new Color(.94f, .97f, 1f);
                light.shadows = LightShadows.Soft;
            }
        }

        static void ConfigureCookingFill(Scene scene)
        {
            var sun = RenderSettings.sun;
            sun.intensity = 3.2f;
            PrefabUtility.RecordPrefabInstancePropertyModifications(sun);
            var workbench = scene.GetRootGameObjects().Single(root => root.name == "PREF_Cooking_Workbench").transform;
            var board = workbench.Find("PREF_Cooking_ChoppingBoard").position;
            var burner = workbench.Find("LOC_BurnerSurface").position;
            var centre = (board + burner) * .5f;
            KitchenAuthoring.EditPrefab(FurniturePrefabs + "PREF_Furniture_Pendant.prefab", ConfigurePendantLight);

            if (File.Exists(CookingFill)) KitchenAuthoring.EditPrefab(CookingFill, ConfigureTaskLight);
            else
            {
                var root = new GameObject("PREF_Lighting_CookingFill");
                try
                {
                    ConfigureTaskLight(root);
                    PrefabUtility.SaveAsPrefabAsset(root, CookingFill);
                }
                finally { Object.DestroyImmediate(root); }
            }
            if (scene.GetRootGameObjects().All(root => root.name != "PREF_Lighting_CookingFill"))
                PrefabUtility.InstantiatePrefab(KitchenAuthoring.LoadRequired<GameObject>(CookingFill), scene);

            void ConfigureTaskLight(GameObject root)
            {
                var diningFill = root.transform.Find("Dining fill");
                if (diningFill != null) Object.DestroyImmediate(diningFill.gameObject);

                var emitter = root.transform.Find("Kitchen ceiling fill") ?? Child(root.transform, "Kitchen ceiling fill");
                emitter.localPosition = new Vector3(centre.x, 2.53f, centre.z);
                emitter.localRotation = Quaternion.Euler(90f, 0f, 0f);
                var light = GetOrAdd<Light>(emitter.gameObject);
                light.type = LightType.Rectangle;
                light.lightmapBakeType = LightmapBakeType.Baked;
                light.areaSize = new Vector2(Mathf.Abs(board.x - burner.x) + 1.10f, .56f);
                light.intensity = 1.35f;
                light.color = new Color(1f, .95f, .88f);
                light.shadows = LightShadows.Soft;
            }
        }

        public static void BakeExperienceBatch()
        {
            BakeLightingBatch();
            var review = EditorSceneManager.OpenScene("Assets/_Project/Scenes/SCN_Kitchen_Review.unity");
            if (!Lightmapping.Bake()) throw new InvalidOperationException("Review probe bake failed.");
            KitchenAuthoring.SaveScene(review);
            AssetDatabase.SaveAssets();
            Debug.Log("KITCHEN_EXPERIENCE_BAKED probes=" + LightmapSettings.lightProbes.count);
        }
    }
}
