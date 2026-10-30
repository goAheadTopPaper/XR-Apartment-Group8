using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;
using XRApartment.Runtime;

namespace XRApartment.EditorTools
{
    public static class KitchenFurnitureVerification
    {
        [Serializable] public class Item
        {
            public string path;
            public Vector3 centre, size;
            public string[] materials;
        }
        [Serializable] public class Survey { public Item[] renderers; }
        [Serializable] public class LodInfo { public string name; public int nearTriangles, farTriangles, nearRenderers, farRenderers; }
        [Serializable] public class Result
        {
            public string scene, unity, platform, buildTarget;
            public LodInfo[] lods;
            public Vector3 tableSize, chairSize;
            public float tabletopHeight, ceilingHeight, canopyTopHeight;
            public int chairs, missingScripts, invalidMaterials;
            public bool tableUnderpassOpen, collisionRetainedAtLowDetail, originalDiningHidden;
        }
        const string Evidence = "ArtSource/KitchenAssets/Validation/Furniture/";

        public static void BakeAndInspectBatch()
        {
            KitchenCookingAuthoring.BakeLightingBatch();
            InspectBatch();
        }

        public static void RebuildAndBakeBatch()
        {
            KitchenCookingAuthoring.ApplyFurnitureBatch();
            BakeAndInspectBatch();
        }

        public static void InspectBatch()
        {
            var scene = EditorSceneManager.OpenScene(KitchenCookingAuthoring.ScenePath);
            var roots = scene.GetRootGameObjects();
            var dining = roots.Single(g => g.name == "PREF_Furniture_DiningSet");
            var environment = roots.Single(g => g.name == "PREF_Kitchen_Environment");
            var table = dining.transform.Cast<Transform>().Single(t => t.name == "PREF_Furniture_DiningTable");
            var chair = dining.transform.Cast<Transform>().First(t => t.name == "PREF_Furniture_DiningChair");
            var result = new Result { scene = scene.path, unity = Application.unityVersion, platform = Application.platform.ToString(),
                buildTarget = EditorUserBuildSettings.activeBuildTarget.ToString(), tableSize = NearBounds(table).size, chairSize = NearBounds(chair).size,
                chairs = dining.transform.Cast<Transform>().Count(t => t.name == "PREF_Furniture_DiningChair") };
            var groups = roots.SelectMany(g => g.GetComponentsInChildren<LODGroup>()).ToArray();
            result.lods = groups.Select(g =>
            {
                var lods = g.GetLODs();
                if (lods.Length != 2) throw new InvalidOperationException("Expected two furniture LODs: " + g.name);
                return new LodInfo { name = PathOf(g.transform), nearTriangles = Triangles(lods[0].renderers), farTriangles = Triangles(lods[1].renderers),
                    nearRenderers = lods[0].renderers.Length, farRenderers = lods[1].renderers.Length };
            }).ToArray();
            foreach (var root in roots)
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                    result.missingScripts += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
            foreach (var group in groups)
                foreach (var renderer in group.GetComponentsInChildren<Renderer>(true))
                    foreach (var material in renderer.sharedMaterials)
                        if (material == null || material.shader == null || !material.shader.isSupported ||
                            (material.name.StartsWith("MAT_Furniture_Cloud_") && material.mainTexture == null)) result.invalidMaterials++;
            Physics.SyncTransforms();
            var tabletop = table.Find("COL_Tabletop").GetComponent<Collider>();
            if (!tabletop.Raycast(new Ray(table.position + Vector3.up * 2, Vector3.down), out var top, 3))
                throw new InvalidOperationException("Saved table has no solid work surface");
            result.tabletopHeight = top.point.y;
            result.tableUnderpassOpen = !table.GetComponentsInChildren<Collider>().Any(c =>
                c.Raycast(new Ray(table.position + new Vector3(0, .4f, -.3f), Vector3.forward), out _, .6f));
            foreach (var group in groups) group.ForceLOD(1);
            result.collisionRetainedAtLowDetail = dining.GetComponentsInChildren<Collider>().All(c => c.enabled && c.gameObject.activeInHierarchy);
            foreach (var group in groups) group.ForceLOD(-1);
            result.originalDiningHidden = new[] { "SM_DiningTable", "SM_DiningLight", "SM_DiningLight (1)" }
                .All(n => !environment.transform.Find(n).gameObject.activeSelf);
            var ceiling = environment.transform.Find("SM_Celling");
            foreach (var collider in ceiling.GetComponentsInChildren<Collider>())
                if (collider.Raycast(new Ray(table.position + Vector3.up * 1.5f, Vector3.up), out var contact, 3)) result.ceilingHeight = contact.point.y;
            result.canopyTopHeight = NearBounds(dining.transform.Cast<Transform>().Single(t => t.name == "PREF_Furniture_Pendant")).max.y;
            Directory.CreateDirectory(Evidence);
            File.WriteAllText(Evidence + "saved-scene-inspection.json", JsonUtility.ToJson(result, true));
            Render(new Vector3(-.85f, 1.6f, .12f), new Vector3(-2.65f, 1.12f, -1.3f), Evidence + "dining-room.png", 62);
            var fridge = roots.SelectMany(g => g.GetComponentsInChildren<KitchenFridge>()).Single();
            Render(new Vector3(-1.78f, 1.35f, .75f), fridge.transform.position + Vector3.up * .95f, Evidence + "fridge-closed.png", 75);
            var leaf = fridge.Door.transform; var rotation = leaf.localRotation;
            try
            {
                leaf.localRotation = rotation * Quaternion.Euler(0, 85, 0);
                Physics.SyncTransforms();
                Render(new Vector3(-1.78f, 1.35f, .75f), fridge.transform.position + Vector3.up * .95f, Evidence + "fridge-open.png", 75);
            }
            finally { leaf.localRotation = rotation; }
            RenderStudio();
            if (result.chairs != 4 || result.missingScripts != 0 || result.invalidMaterials != 0 || result.lods.Length != 8 ||
                result.lods.Any(l => l.farTriangles >= l.nearTriangles || l.farTriangles <= 0) || !result.tableUnderpassOpen ||
                !result.collisionRetainedAtLowDetail || !result.originalDiningHidden || Mathf.Abs(result.tabletopHeight - .758f) > .003f ||
                Mathf.Abs(result.canopyTopHeight - result.ceilingHeight) > .02f)
                throw new InvalidOperationException("Furniture validation failed; see saved-scene-inspection.json");
            Debug.Log("FURNITURE_INSPECTED " + JsonUtility.ToJson(result));
        }

        static Bounds NearBounds(Transform root)
        {
            var renderers = root.GetComponentInChildren<LODGroup>().GetLODs()[0].renderers;
            var bounds = renderers[0].bounds; foreach (var r in renderers) bounds.Encapsulate(r.bounds); return bounds;
        }

        static int Triangles(Renderer[] renderers) => renderers.Sum(r => r.GetComponent<MeshFilter>().sharedMesh.triangles.Length / 3);

        static void RenderStudio()
        {
            const int layer = 30;
            var root = new GameObject("Temporary furniture studio");
            var lights = UnityEngine.Object.FindObjectsOfType<Light>().Where(l => l.enabled).ToArray();
            var ambientMode = RenderSettings.ambientMode; var ambient = RenderSettings.ambientLight;
            try
            {
                foreach (var light in lights) light.enabled = false;
                RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.44f, .44f, .44f);
                var items = new[] { ("DiningTable", new Vector3(-.65f, 0, 0)), ("DiningChair", new Vector3(-1.5f, 0, .85f)), ("Pendant", new Vector3(-.65f, 2.1f, 0)) };
                foreach (var entry in items)
                {
                    var instance = UnityEngine.Object.Instantiate(KitchenAuthoring.LoadRequired<GameObject>(KitchenCookingAuthoring.FurniturePrefabs + "PREF_Furniture_" + entry.Item1 + ".prefab"), root.transform);
                    instance.transform.localPosition = entry.Item2;
                }
                var fridge = UnityEngine.Object.Instantiate(KitchenAuthoring.LoadRequired<GameObject>("Assets/_Project/Prefabs/Cooking/Recipe/PREF_Cooking_Fridge.prefab"), root.transform);
                fridge.transform.localPosition = new Vector3(1.2f, 0, 0);
                foreach (var l in root.GetComponentsInChildren<Light>()) l.enabled = false;
                var floor = GameObject.CreatePrimitive(PrimitiveType.Plane); floor.transform.SetParent(root.transform, false); floor.transform.localScale = Vector3.one * 2;
                var floorMaterial = new Material(Shader.Find("Standard")); floorMaterial.color = new Color(.3f, .3f, .3f); floorMaterial.SetFloat("_Glossiness", .14f);
                floor.GetComponent<Renderer>().sharedMaterial = floorMaterial;
                try
                {
                    foreach (var transform in root.GetComponentsInChildren<Transform>(true)) transform.gameObject.layer = layer;
                    for (var i = 0; i < 2; i++)
                    {
                        var light = new GameObject("Temporary studio light").AddComponent<Light>(); light.transform.SetParent(root.transform);
                        light.type = LightType.Directional; light.transform.rotation = Quaternion.Euler(i == 0 ? 48 : 25, i == 0 ? 145 : -40, 0);
                        light.intensity = i == 0 ? 1.8f : .6f; light.shadows = i == 0 ? LightShadows.Soft : LightShadows.None; light.cullingMask = 1 << layer;
                    }
                    var groups = root.GetComponentsInChildren<LODGroup>();
                    foreach (var group in groups) group.ForceLOD(0);
                    Render(new Vector3(-3.7f, 2.75f, 4.6f), new Vector3(0, 1.15f, 0), Evidence + "furniture-unity-studio.png", 48, 1 << layer);
                    foreach (var group in groups) group.ForceLOD(1);
                    Render(new Vector3(-3.7f, 2.75f, 4.6f), new Vector3(0, 1.15f, 0), Evidence + "furniture-unity-lod1.png", 48, 1 << layer);
                }
                finally { UnityEngine.Object.DestroyImmediate(floorMaterial); }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                foreach (var light in lights) if (light != null) light.enabled = true;
                RenderSettings.ambientMode = ambientMode; RenderSettings.ambientLight = ambient;
            }
        }

        static void Render(Vector3 position, Vector3 target, string path, float fov, int mask = -1)
        {
            var go = new GameObject("Temporary furniture QA camera");
            var texture = new RenderTexture(1600, 1100, 24, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(1600, 1100, TextureFormat.RGB24, false);
            var active = RenderTexture.active;
            try
            {
                var camera = go.AddComponent<Camera>(); camera.transform.position = position; camera.transform.LookAt(target);
                camera.fieldOfView = fov; camera.nearClipPlane = .02f; camera.farClipPlane = 60; camera.allowHDR = true; camera.cullingMask = mask;
                if (mask != -1) { camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.2f, .2f, .2f); }
                var source = UnityEngine.Object.FindObjectsOfType<PostProcessLayer>().FirstOrDefault();
                if (source != null) { var layer = go.AddComponent<PostProcessLayer>(); EditorUtility.CopySerialized(source, layer); layer.volumeTrigger = camera.transform; }
                camera.targetTexture = texture; camera.Render(); RenderTexture.active = texture;
                pixels.ReadPixels(new Rect(0, 0, 1600, 1100), 0, 0); pixels.Apply(); File.WriteAllBytes(path, pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = active; texture.Release(); UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(pixels); UnityEngine.Object.DestroyImmediate(go);
            }
        }

        public static void SurveyBatch()
        {
            var scene = EditorSceneManager.OpenScene(KitchenCookingAuthoring.ScenePath);
            var items = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MeshRenderer>(true))
                .Where(r => r.transform.root.name == "PREF_Kitchen_Environment")
                .Select(r => new Item { path = PathOf(r.transform), centre = r.bounds.center, size = r.bounds.size,
                    materials = r.sharedMaterials.Select(m => m == null ? "missing" : m.name).ToArray() }).ToArray();
            Directory.CreateDirectory("Logs/KitchenFurniture");
            File.WriteAllText("Logs/KitchenFurniture/room-survey.json", JsonUtility.ToJson(new Survey { renderers = items }, true));
            Debug.Log("FURNITURE_SURVEY_READY " + items.Length);
        }

        static string PathOf(Transform value) => value.parent == null ? value.name : PathOf(value.parent) + "/" + value.name;
    }
}
