using System;
using System.Collections.Generic;
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
    public static class KitchenCookingVerification
    {
        const string Folder = "Logs/KitchenAssetPipeline/";
        [Serializable] sealed class Report
        {
            public string scene, unity, platform;
            public int preparedProps, foodVessels, ingredients, missingScripts, invalidShaders;
            public List<Placement> placements = new List<Placement>();
        }
        [Serializable] sealed class Placement { public string name; public Vector3 position, size; public float bottom, tabletop; }

        public static void InspectBatch()
        {
            var scene = EditorSceneManager.OpenScene(KitchenCookingAuthoring.ScenePath);
            var report = new Report { scene = scene.path, unity = Application.unityVersion, platform = Application.platform.ToString() };
            var roots = scene.GetRootGameObjects();
            foreach (var root in roots)
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                    report.missingScripts += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
            report.preparedProps = UnityEngine.Object.FindObjectsByType<PreparedKitchenProp>(FindObjectsSortMode.None).Length;
            report.foodVessels = UnityEngine.Object.FindObjectsByType<KitchenFoodVessel>(FindObjectsSortMode.None).Length;
            report.ingredients = UnityEngine.Object.FindObjectsByType<CookableIngredient>(FindObjectsSortMode.None).Length;
            var environment = roots.Single(root => root.name == "PREF_Kitchen_Environment");
            var station = roots.Single(root => root.name == "PREF_Cooking_Workbench");
            Physics.SyncTransforms();
            foreach (Transform item in station.transform)
            {
                if (item.GetComponent<Rigidbody>() == null) continue;
                var bounds = VisualBounds(item.gameObject);
                var table = environment.GetComponentsInChildren<MeshCollider>().Where(collider => collider.name == "SM_DiningTable")
                    .Select(collider => { var hit = collider.Raycast(new Ray(new Vector3(bounds.center.x, 2, bounds.center.z), Vector3.down), out var contact, 3); return (hit, contact); })
                    .Where(hit => hit.hit && hit.contact.normal.y > .9f).OrderByDescending(hit => hit.contact.point.y).ToArray();
                report.placements.Add(new Placement { name = item.name, position = item.position, size = bounds.size, bottom = bounds.min.y,
                    tabletop = table.Length > 0 ? table[0].contact.point.y : float.NaN });
            }
            foreach (var renderer in station.GetComponentsInChildren<Renderer>(true))
                foreach (var material in renderer.sharedMaterials)
                    if (material != null && (material.shader == null || !material.shader.isSupported)) report.invalidShaders++;
            Directory.CreateDirectory(Folder); File.WriteAllText(Folder + "cooking-scene-inspection.json", JsonUtility.ToJson(report, true));
            Render(new Vector3(-2.6f, 2.25f, .25f), new Vector3(-2.6f, 1.1f, -1.15f), "cooking-workbench.png", 45);
            Render(new Vector3(-1.3f, 2.4f, .5f), new Vector3(-3.1f, .9f, -.2f), "cooking-kitchen.png", 65);
            Debug.Log("COOKING_INSPECTED " + JsonUtility.ToJson(report));
            if (report.missingScripts != 0 || report.invalidShaders != 0 || report.preparedProps != 8 || report.foodVessels != 2)
                throw new InvalidOperationException("Cooking scene asset validation failed. See inspection JSON.");
        }

        static Bounds VisualBounds(GameObject root)
        {
            var first = true; var result = new Bounds();
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.name.StartsWith("COL_") || filter.sharedMesh == null) continue;
                foreach (var vertex in filter.sharedMesh.vertices)
                {
                    var point = filter.transform.TransformPoint(vertex);
                    if (first) { result = new Bounds(point, Vector3.zero); first = false; } else result.Encapsulate(point);
                }
            }
            return result;
        }
        static void Render(Vector3 position, Vector3 target, string filename, float fov)
        {
            var cameraObject = new GameObject("Temporary cooking QA camera");
            var texture = new RenderTexture(1400, 1000, 24, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(1400, 1000, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                var camera = cameraObject.AddComponent<Camera>(); camera.transform.position = position; camera.transform.LookAt(target);
                camera.fieldOfView = fov; camera.nearClipPlane = .02f; camera.farClipPlane = 60; camera.allowHDR = true;
                var source = UnityEngine.Object.FindObjectsByType<PostProcessLayer>(FindObjectsSortMode.None).FirstOrDefault();
                if (source != null)
                {
                    var layer = cameraObject.AddComponent<PostProcessLayer>();
                    EditorUtility.CopySerialized(source, layer); layer.volumeTrigger = camera.transform;
                }
                camera.targetTexture = texture; camera.Render(); RenderTexture.active = texture;
                pixels.ReadPixels(new Rect(0, 0, 1400, 1000), 0, 0); pixels.Apply(); File.WriteAllBytes(Folder + filename, pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous; texture.Release(); UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(pixels); UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }
    }
}
