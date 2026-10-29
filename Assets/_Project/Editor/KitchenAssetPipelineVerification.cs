using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using XRApartment.Runtime;

namespace XRApartment.EditorTools
{
    /// Batch-only fixture QA; does not alter the kitchen scene or build settings.
    public static class KitchenAssetPipelineVerification
    {
        [Serializable] sealed class Result
        {
            public string sample;
            public bool diagnostic_fixture = true;
            public bool cloud_generated = false;
            public int pieces, convex_colliders, material_slots;
            public float span_metres, assembly_bounds_error_metres;
            public Vector3 unity_size;
            public float bottom_y_metres;
        }

        [Serializable] sealed class Report { public Result[] results; }

        public static void RunBatch()
        {
            var results = new List<Result>();
            var generated = KitchenAuthoring.Root + "Art/Generated";
            var parentExisted = AssetDatabase.IsValidFolder(generated);
            var prefabs = KitchenAuthoring.Prefabs + "Generated";
            var prefabParentExisted = AssetDatabase.IsValidFolder(prefabs);
            try
            {
                foreach (var name in new[] { "plate", "egg" })
                    results.Add(Check(name));
                File.WriteAllText("Logs/KitchenAssetPipeline/unity-import.json",
                    JsonUtility.ToJson(new Report { results = results.ToArray() }, true));
                Debug.Log("Kitchen asset bridge verified with synthetic fixtures; cloud pilots are still pending.");
            }
            finally
            {
                if (!parentExisted && Directory.Exists(generated) && !Directory.EnumerateFileSystemEntries(generated).Any())
                    AssetDatabase.DeleteAsset(generated);
                if (!prefabParentExisted && Directory.Exists(prefabs) && !Directory.EnumerateFileSystemEntries(prefabs).Any())
                    AssetDatabase.DeleteAsset(prefabs);
            }
        }

        static Result Check(string name)
        {
            var source = "Logs/KitchenAssetPipeline/" + name + "_fixture/unity_asset.json";
            var manifest = JsonUtility.FromJson<KitchenPreparedAssetImporter.Manifest>(File.ReadAllText(source));
            var folder = KitchenAuthoring.Root + "Art/Generated/Diagnostic_" + name + "_v001";
            var prefabFolder = KitchenAuthoring.Prefabs + "Generated/Diagnostic_" + name + "_v001";
            if (AssetDatabase.IsValidFolder(folder)) throw new InvalidOperationException("Diagnostic target already exists: " + folder);
            GameObject instance = null;
            try
            {
                var prefab = KitchenPreparedAssetImporter.Import(source, diagnostic: true);
                instance = UnityEngine.Object.Instantiate(prefab);
                if (instance.GetComponent<XRGrabInteractable>() == null ||
                    instance.GetComponent<CollisionConstrainedGrabTransformer>() == null)
                    throw new InvalidOperationException("The imported prop lost constrained XRI grabbing.");
                var intact = instance.transform.Find("Intact");
                var fragments = instance.transform.Find("DerivedPieces");
                fragments.gameObject.SetActive(true);
                var intactBounds = RenderBounds(intact);
                var pieceBounds = RenderBounds(fragments);
                var error = Vector3.Distance(intactBounds.min, pieceBounds.min) + Vector3.Distance(intactBounds.max, pieceBounds.max);
                var span = Mathf.Max(intactBounds.size.x, intactBounds.size.y, intactBounds.size.z);
                var expected = manifest.size_blender_xyz.Max();
                var expectedAxes = new Vector3(manifest.size_blender_xyz[0], manifest.size_blender_xyz[2], manifest.size_blender_xyz[1]);
                if (Mathf.Abs(span - expected) > .0001f || error > .0002f ||
                    Vector3.Distance(intactBounds.size, expectedAxes) > .0002f || Mathf.Abs(intactBounds.min.y) > .0001f)
                    throw new InvalidOperationException($"FBX scale/pivot mismatch: span={span}, expected={expected}, assembly={error}");
                var materials = instance.GetComponentsInChildren<MeshRenderer>(true)
                    .Where(r => !r.name.StartsWith("COL_", StringComparison.Ordinal))
                    .SelectMany(r => r.sharedMaterials).ToArray();
                if (materials.Any(m => m == null || m.shader == null || m.shader.name != "Standard"))
                    throw new InvalidOperationException("Expected Built-in Standard materials.");
                if (!materials.Any(m => m.name.StartsWith("MAT_", StringComparison.Ordinal)))
                    throw new InvalidOperationException("Missing saved material assets.");
                var colliders = instance.GetComponentsInChildren<MeshCollider>(true);
                if (colliders.Length < manifest.piece_count + 1 || colliders.Any(c => !c.convex || c.sharedMesh.triangles.Length / 3 > 255))
                    throw new InvalidOperationException("Missing or excessive convex collision meshes.");
                return new Result { sample = name, pieces = fragments.childCount,
                    convex_colliders = colliders.Length, material_slots = materials.Length,
                    span_metres = span, assembly_bounds_error_metres = error,
                    unity_size = intactBounds.size, bottom_y_metres = intactBounds.min.y };
            }
            finally
            {
                if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
                if (AssetDatabase.IsValidFolder(prefabFolder)) AssetDatabase.DeleteAsset(prefabFolder);
                if (AssetDatabase.IsValidFolder(folder)) AssetDatabase.DeleteAsset(folder);
            }
        }

        static Bounds RenderBounds(Transform root)
        {
            var renderers = root.GetComponentsInChildren<MeshRenderer>(true)
                .Where(r => !r.name.StartsWith("COL_", StringComparison.Ordinal)).ToArray();
            if (renderers.Length == 0) throw new InvalidOperationException("No visible meshes.");
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }
    }
}
