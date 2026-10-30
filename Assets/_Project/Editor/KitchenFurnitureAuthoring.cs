using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace XRApartment.EditorTools
{
    public static partial class KitchenCookingAuthoring
    {
        internal const string FurnitureArt = Art + "Furniture/";
        internal const string FurniturePrefabs = KitchenAuthoring.Prefabs + "Furniture/";
        const string FurnitureSource = "ArtSource/KitchenAssets/Recipe/v004_furniture/";
        static readonly string[] FurnitureModels = { "dining_table", "dining_chair", "pendant", "fridge_body", "fridge_door" };

        [MenuItem("XR Apartment/Cooking/Apply Reviewed Furniture")]
        public static void ApplyFurnitureMenu()
        {
            KitchenAuthoring.RequireEditMode();
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) ApplyFurnitureBatch();
        }

        public static void ApplyFurnitureBatch()
        {
            KitchenAuthoring.RequireEditMode();
            PrepareFurnitureVisuals();
            BuildFurniturePrefabs();
            var scene = EditorSceneManager.OpenScene(ScenePath);
            ApplyFurnitureScene(scene);
            KitchenAuthoring.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("FURNITURE_AUTHORING_READY " + ScenePath);
        }

        static void PrepareFurnitureVisuals()
        {
            KitchenAuthoring.EnsureFolder(FurnitureArt.TrimEnd('/'));
            foreach (var name in FurnitureModels)
                foreach (var suffix in new[] { "", "_lod1" })
                    File.Copy(FurnitureSource + name + suffix + ".fbx", FurnitureArt + name + suffix + ".fbx", true);
            foreach (var name in new[] { "dining_table", "dining_chair", "pendant_shade" })
            {
                var target = FurnitureArt + name + "/Textures";
                KitchenAuthoring.EnsureFolder(FurnitureArt + name);
                KitchenAuthoring.EnsureFolder(target);
                foreach (var map in new[] { "basecolor.png", "metallic_smoothness.png", "normal.png", "ao.png" })
                    File.Copy("ArtSource/KitchenAssets/furniture_" + name + "/v001/prepared/Textures/" + map, target + "/" + map, true);
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var name in FurnitureModels)
                foreach (var suffix in new[] { "", "_lod1" })
                {
                    var path = FurnitureArt + name + suffix + ".fbx";
                    KitchenPreparedAssetImporter.ConfigureModel(path);
                    var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                    importer.generateSecondaryUV = !name.StartsWith("fridge", StringComparison.Ordinal);
                    importer.SaveAndReimport();
                }
            KitchenPreparedAssetImporter.ConfigureTextures(FurnitureArt);
        }

        static Material FurnitureMaterial(Material source)
        {
            var name = source.name.Replace("(Instance)", "").Trim();
            var path = FurnitureArt + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Standard")) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.enableInstancing = true;
            if (name.StartsWith("MAT_Furniture_Cloud_", StringComparison.Ordinal))
            {
                var asset = name.Substring("MAT_Furniture_Cloud_".Length);
                var folder = FurnitureArt + asset;
                material.color = Color.white;
                KitchenPreparedAssetImporter.SetMap(material, "_MainTex", "basecolor.png", folder);
                KitchenPreparedAssetImporter.SetMap(material, "_MetallicGlossMap", "metallic_smoothness.png", folder);
                KitchenPreparedAssetImporter.SetMap(material, "_BumpMap", "normal.png", folder);
                KitchenPreparedAssetImporter.SetMap(material, "_OcclusionMap", "ao.png", folder);
                material.SetFloat("_GlossMapScale", .32f); material.SetFloat("_BumpScale", .12f);
                material.EnableKeyword("_METALLICGLOSSMAP"); material.EnableKeyword("_NORMALMAP");
            }
            else
            {
                var colour = new Color(.84f, .845f, .81f); var metal = .12f; var smooth = .72f;
                if (name.EndsWith("FridgeLiner")) { colour = new Color(.88f, .89f, .85f); metal = 0; smooth = .62f; }
                else if (name.EndsWith("BrushedSteel")) { colour = new Color(.47f, .50f, .51f); metal = .85f; smooth = .72f; }
                else if (name.EndsWith("DoorGasket")) { colour = new Color(.034f, .038f, .036f); metal = 0; smooth = .35f; }
                else if (name.EndsWith("Cable")) { colour = new Color(.075f, .071f, .061f); metal = 0; smooth = .2f; }
                else if (name.EndsWith("Brass")) { colour = new Color(.53f, .36f, .13f); metal = .78f; smooth = .7f; }
                else if (name.EndsWith("IvoryEnamel")) { colour = new Color(.88f, .85f, .77f); metal = .1f; smooth = .69f; }
                else if (name.EndsWith("OpalLamp")) { colour = new Color(.94f, .94f, .85f); metal = 0; smooth = .76f; }
                else if (name.EndsWith("Crisper")) { colour = new Color(.78f, .82f, .79f); metal = 0; smooth = .64f; }
                else if (name.EndsWith("ShelfGlass"))
                {
                    colour = new Color(.59f, .75f, .73f, .38f); metal = 0; smooth = .86f;
                    material.SetFloat("_Mode", 2); material.SetOverrideTag("RenderType", "Transparent");
                    material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                    material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                    material.SetInt("_ZWrite", 0); material.EnableKeyword("_ALPHABLEND_ON");
                    material.renderQueue = (int)RenderQueue.Transparent;
                }
                material.color = colour; material.SetFloat("_Metallic", metal); material.SetFloat("_Glossiness", smooth);
            }
            EditorUtility.SetDirty(material); return material;
        }

        static GameObject FurnitureVisual(Transform parent, string name, bool fixedFurniture)
        {
            var group = Child(parent, "VIS_Furniture_" + name).gameObject;
            var lods = new LOD[2];
            for (var level = 0; level < 2; level++)
            {
                var model = UnityEngine.Object.Instantiate(KitchenAuthoring.LoadRequired<GameObject>(FurnitureArt + name + (level == 0 ? "" : "_lod1") + ".fbx"), group.transform);
                model.name = "LOD" + level;
                var renderers = model.GetComponentsInChildren<MeshRenderer>(true);
                foreach (var renderer in renderers)
                {
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(FurnitureMaterial).ToArray();
                    renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
                    renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
                    GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, fixedFurniture ?
                        StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic : 0);
                }
                lods[level] = new LOD(level == 0 ? .3f : .01f, renderers);
            }
            var lod = group.AddComponent<LODGroup>(); lod.SetLODs(lods); lod.RecalculateBounds();
            return group;
        }

        static void BuildFurniturePrefabs()
        {
            KitchenAuthoring.EnsureFolder(FurniturePrefabs.TrimEnd('/'));
            foreach (var name in new[] { "DiningTable", "DiningChair", "Pendant" })
            {
                var root = new GameObject("PREF_Furniture_" + name);
                try
                {
                    var modelName = name == "DiningTable" ? "dining_table" : name == "DiningChair" ? "dining_chair" : "pendant";
                    var visual = FurnitureVisual(root.transform, modelName, true);
                    if (name == "DiningTable")
                    {
                        FurnitureBox(root.transform, "COL_Tabletop", new Vector3(0, .740f, 0), new Vector3(1.5f, .036f, .85f));
                        foreach (var x in new[] { -.65f, .65f }) foreach (var z in new[] { -.32f, .32f })
                            FurnitureBox(root.transform, "COL_TableLeg", new Vector3(x, .35f, z), new Vector3(.065f, .70f, .065f));
                    }
                    else
                    {
                        // LODGroup toggles renderers only. These static colliders
                        // remain active at either visible detail level.
                        foreach (var mesh in visual.transform.Find("LOD1").GetComponentsInChildren<MeshFilter>())
                            mesh.gameObject.AddComponent<MeshCollider>().sharedMesh = mesh.sharedMesh;
                        if (name == "Pendant")
                        {
                            var bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere); bulb.name = "VIS_OpalBulb";
                            bulb.transform.SetParent(root.transform, false); bulb.transform.localPosition = new Vector3(0, .080f, 0);
                            bulb.transform.localScale = new Vector3(.055f, .085f, .055f);
                            UnityEngine.Object.DestroyImmediate(bulb.GetComponent<Collider>());
                            var material = Material("PendantBulb", new Color(1, .9f, .69f), 0, .3f);
                            material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", new Color(1, .83f, .57f) * 1.3f);
                            bulb.GetComponent<Renderer>().sharedMaterial = material;
                            var light = Child(root.transform, "LGT_DiningWarm").gameObject.AddComponent<Light>();
                            light.transform.localPosition = new Vector3(0, .075f, 0); light.transform.localRotation = Quaternion.Euler(90, 0, 0);
                            light.type = LightType.Spot; light.range = 3.3f; light.spotAngle = 85;
                            light.intensity = 1.35f; light.color = new Color(1, .86f, .69f);
                            light.lightmapBakeType = LightmapBakeType.Baked; light.shadows = LightShadows.Soft;
                        }
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, FurniturePrefabs + root.name + ".prefab");
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
            KitchenAuthoring.EditPrefab(RecipePrefabs + "PREF_Cooking_Fridge.prefab", ConfigureFurnitureFridge);
            RegisterFurnitureAssumptionsBatch();
        }

        internal static void ConfigureFurnitureFridge(GameObject root)
        {
            RemoveFurnitureVisual(root.transform); RemoveRealVisual(root); HideOldVisual(root.transform.Find("VIS_fridge_body"));
            FurnitureVisual(root.transform, "fridge_body", false);
            var hinge = root.transform.Find("ACT_FridgeDoor"); RemoveFurnitureVisual(hinge); RemoveRealVisual(hinge.gameObject);
            HideOldVisual(hinge.Find("VIS_fridge_door"));
            FurnitureVisual(hinge, "fridge_door", false);
        }

        public static void RegisterFurnitureAssumptionsBatch()
        {
            KitchenAuthoring.RequireEditMode();
            Parameter("cooking_dining_table_width", 1.5f); Parameter("cooking_dining_table_depth", .85f); Parameter("cooking_dining_table_height", .758f);
            Parameter("cooking_dining_chair_width", .49f); Parameter("cooking_dining_chair_depth", .53f); Parameter("cooking_dining_chair_height", .83f);
            Parameter("cooking_dining_centre_x", -2.5f); Parameter("cooking_dining_centre_z", -1.25f);
            Parameter("cooking_pendant_diameter", .4f); Parameter("cooking_pendant_bottom_height", 2.1f);
            Parameter("cooking_route_character_height", 1.7f); Parameter("cooking_route_character_radius", .2f);
            Parameter("cooking_route_start_x", -1.25f); Parameter("cooking_route_start_z", -1.25f);
            Parameter("cooking_route_far_end_x", -3.75f); Parameter("cooking_route_approach_z", .08f);
            AssetDatabase.SaveAssets();
        }

        static void FurnitureBox(Transform parent, string name, Vector3 centre, Vector3 size)
        {
            var collider = Child(parent, name).gameObject.AddComponent<BoxCollider>(); collider.center = centre; collider.size = size;
        }

        static void RemoveFurnitureVisual(Transform parent)
        {
            foreach (var child in parent.Cast<Transform>().Where(t => t.name.StartsWith("VIS_Furniture_", StringComparison.Ordinal)).ToArray())
                UnityEngine.Object.DestroyImmediate(child.gameObject);
        }

        static void ApplyFurnitureScene(Scene scene)
        {
            var environment = scene.GetRootGameObjects().Single(g => g.name == "PREF_Kitchen_Environment");
            foreach (var name in new[] { "SM_DiningTable", "SM_DiningLight", "SM_DiningLight (1)" })
            {
                var old = environment.transform.Find(name);
                if (old == null) throw new InvalidOperationException("Dining replacement target missing: " + name);
                old.gameObject.SetActive(false); PrefabUtility.RecordPrefabInstancePropertyModifications(old.gameObject);
            }
            const string rootName = "PREF_Furniture_DiningSet";
            var previous = scene.GetRootGameObjects().SingleOrDefault(g => g.name == rootName);
            if (previous != null) UnityEngine.Object.DestroyImmediate(previous);
            var root = new GameObject(rootName);
            try
            {
                FurnitureInstance(root.transform, "DiningTable", new Vector3(0, 0, 0), 0);
                foreach (var x in new[] { -.45f, .45f })
                {
                    FurnitureInstance(root.transform, "DiningChair", new Vector3(x, 0, .77f), 180);
                    FurnitureInstance(root.transform, "DiningChair", new Vector3(x, 0, -.77f), 0);
                }
                FurnitureInstance(root.transform, "Pendant", new Vector3(0, 2.1f, 0), 0);
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, FurniturePrefabs + rootName + ".prefab");
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                instance.transform.position = new Vector3(-2.5f, 0, -1.25f);
                PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        static void FurnitureInstance(Transform parent, string name, Vector3 position, float yaw)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(KitchenAuthoring.LoadRequired<GameObject>(FurniturePrefabs + "PREF_Furniture_" + name + ".prefab"), parent);
            instance.transform.localPosition = position; instance.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
        }
    }
}
