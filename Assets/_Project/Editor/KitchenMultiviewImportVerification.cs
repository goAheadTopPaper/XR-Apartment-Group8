using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace XRApartment.EditorTools
{
    /// Technical import of the rejected visual candidate; never installs it in the kitchen.
    public static class KitchenMultiviewImportVerification
    {
        [Serializable] sealed class Report
        {
            public string job_id, source_sha256, unity_import, visual_review;
            public int triangles, renderers, colliders;
            public Vector3 size_unity_xyz;
            public bool cloud_generated, diagnostic_fixture, temporary_import_removed;
        }
        public static void RunBatch()
        {
            const string source = "ArtSource/KitchenAssets/cabinet_multiview/v001/inspection";
            const string folder = "Assets/_Project/Art/Generated/Diagnostic_MultiviewImport";
            if (AssetDatabase.IsValidFolder(folder)) throw new InvalidOperationException("Do not overwrite an existing diagnostic folder");
            var text = File.ReadAllText(source + "/unity_asset.json");
            var descriptor = JsonUtility.FromJson<KitchenPreparedAssetImporter.Manifest>(text);
            var report = new Report { job_id = "cfe4c1e5-af07-4954-80fa-c30853880588", cloud_generated = true,
                source_sha256 = descriptor.source_sha256, diagnostic_fixture = false, visual_review = "rejected_wrinkled_facade_not_placed_in_kitchen" };
            GameObject root = null;
            KitchenAuthoring.EnsureFolder(folder);
            try
            {
                KitchenPreparedAssetImporter.CopyRequired(source, descriptor.intact_fbx, folder);
                KitchenAuthoring.EnsureFolder(folder + "/Textures");
                foreach (var file in Directory.GetFiles(source + "/Textures", "*.png")) File.Copy(file, folder + "/Textures/" + Path.GetFileName(file));
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                KitchenPreparedAssetImporter.ConfigureModel(folder + "/" + descriptor.intact_fbx);
                KitchenPreparedAssetImporter.ConfigureTextures(folder);
                root = UnityEngine.Object.Instantiate(KitchenAuthoring.LoadRequired<GameObject>(folder + "/" + descriptor.intact_fbx));
                KitchenPreparedAssetImporter.AdaptMaterials(root, folder, descriptor.pbr_textures, new Dictionary<Material, Material>());
                report.colliders = KitchenPreparedAssetImporter.InstallColliders(root);
                var visuals = root.GetComponentsInChildren<MeshFilter>().Where(filter => !filter.name.StartsWith("COL_")).ToArray();
                report.triangles = visuals.Sum(filter => filter.sharedMesh.triangles.Length / 3);
                report.renderers = visuals.Length;
                var points = visuals.SelectMany(filter => filter.sharedMesh.vertices.Select(vertex => filter.transform.TransformPoint(vertex))).ToArray();
                var bounds = new Bounds(points[0], Vector3.zero); foreach (var point in points) bounds.Encapsulate(point);
                report.size_unity_xyz = bounds.size;
                if (report.triangles != 8000 || report.colliders < 1 || Mathf.Abs(bounds.size.y - 1f) > .01f || Mathf.Abs(bounds.min.y) > .002f)
                    throw new InvalidDataException("Actual multiview FBX failed its budget / metre / Y-up import checks");
                if (root.GetComponentsInChildren<Renderer>().Where(renderer => renderer.enabled).SelectMany(renderer => renderer.sharedMaterials).Any(material => material == null || !material.shader.isSupported))
                    throw new InvalidDataException("Missing material or unsupported shader");
                report.unity_import = "passed";
            }
            finally
            {
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                report.temporary_import_removed = AssetDatabase.DeleteAsset(folder);
                Directory.CreateDirectory("Logs/KitchenAssetPipeline");
                File.WriteAllText("Logs/KitchenAssetPipeline/multiview-import.json", JsonUtility.ToJson(report, true));
                AssetDatabase.SaveAssets();
            }
            Debug.Log("MULTIVIEW_TECHNICAL_IMPORT " + JsonUtility.ToJson(report));
        }
    }
}
