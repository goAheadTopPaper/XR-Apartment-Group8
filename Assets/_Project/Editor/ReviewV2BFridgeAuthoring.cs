using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using XRApartment.Data.ReviewV2;
using XRApartment.Runtime;
using XRApartment.Runtime.ReviewV2;
using Object = UnityEngine.Object;

namespace XRApartment.EditorTools
{
    public static class ReviewV2BFridgeAuthoring
    {
        public const string Version = ReviewV2BStorageAuthoring.Version;
        public const string MeshFolder = "Assets/_Project/Art/ReviewV2/B_NorthHinge";
        public static readonly Vector3 Position = new Vector3(-4.65f, 0, -.10f);
        const string SourcePath = "Assets/_Project/Prefabs/Cooking/PREF_Cooking_Workbench.prefab";

        [MenuItem("XR Apartment/Review V2/Apply B North Fridge And Hinge")]
        public static void Apply()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Stop Play Mode before authoring");
            var path = ReviewV2SchemeAuthoring.LayoutPath(SchemeIds.ExternalRefrigerator);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var world = root.GetComponent<ReviewSchemeWorld>(); ConfigureWorld(world);
                if (PrefabUtility.SaveAsPrefabAsset(root, path) == null) throw new IOException(path);
                File.WriteAllLines("Docs/M2/SCHEME_B_Identities.tsv", world.identities.Select(i =>
                    world.schemeId + "\t" + Version + "\t" + i.Id + "\t" + i.name));
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("REVIEW_V2_B_NORTH: " + Version + "; north hinge, south handle; existing wall/sink and shared assets retained");
        }

        static Vector3 Mirror(Vector3 v) => new Vector3(-v.x, v.y, v.z);
        static Quaternion Mirror(Quaternion q) => new Quaternion(q.x, -q.y, -q.z, q.w);
        static string Relative(Transform child, Transform root) => AnimationUtility.CalculateTransformPath(child, root);
        static Transform Match(Transform source, Transform sourceRoot, Transform targetRoot)
        {
            var path = Relative(source, sourceRoot);
            var target = path.Length == 0 ? targetRoot : targetRoot.Find(path);
            if (target == null) throw new InvalidOperationException("Missing original door node: " + path);
            return target;
        }

        public static void ConfigureWorld(ReviewSchemeWorld world)
        {
            if (world.schemeId != SchemeIds.ExternalRefrigerator) throw new InvalidOperationException("B-only authoring");
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath).GetComponent<KitchenCookingStation>().ActiveFridge;
            var fridge = world.station.ActiveFridge;
            if (source.SecondaryDoor != null || fridge.SecondaryDoor != null) throw new InvalidOperationException("Requires the original single door");
            ConfigurePosition(world);
            var canonical = source.Door.transform; var target = fridge.Door.transform;
            var meshes = new Dictionary<Mesh, Mesh>();
            if (!AssetDatabase.IsValidFolder(MeshFolder)) AssetDatabase.CreateFolder("Assets/_Project/Art/ReviewV2", "B_NorthHinge");
            // Always derive from the unchanged Cooking source, never from the last mirrored B.
            foreach (var node in canonical.GetComponentsInChildren<Transform>(true))
            {
                var t = Match(node, canonical, target);
                if (node.localScale.x <= 0 || node.localScale.y <= 0 || node.localScale.z <= 0)
                    throw new InvalidOperationException("Unexpected negative source scale: " + node.name);
                t.localPosition = Mirror(node.localPosition); t.localRotation = Mirror(node.localRotation); t.localScale = node.localScale;
                var filter = node.GetComponent<MeshFilter>();
                if (filter != null) t.GetComponent<MeshFilter>().sharedMesh = Derived(filter.sharedMesh, meshes);
                foreach (var collider in node.GetComponents<Collider>())
                {
                    var dest = t.GetComponent(collider.GetType()) as Collider;
                    if (dest == null) throw new InvalidOperationException("Missing collider: " + node.name);
                    var box = collider as BoxCollider;
                    var sphere = collider as SphereCollider;
                    var capsule = collider as CapsuleCollider;
                    var mesh = collider as MeshCollider;
                    if (box != null) { ((BoxCollider)dest).center = Mirror(box.center); ((BoxCollider)dest).size = box.size; }
                    else if (sphere != null) { ((SphereCollider)dest).center = Mirror(sphere.center); ((SphereCollider)dest).radius = sphere.radius; }
                    else if (capsule != null) { var c = (CapsuleCollider)dest; c.center = Mirror(capsule.center); c.radius = capsule.radius; c.height = capsule.height; c.direction = capsule.direction; }
                    else if (mesh != null) ((MeshCollider)dest).sharedMesh = Derived(mesh.sharedMesh, meshes);
                    else throw new InvalidOperationException("Unsupported door collider: " + collider.GetType());
                    dest.enabled = collider.enabled; dest.isTrigger = collider.isTrigger;
                }
            }
            var data = new SerializedObject(fridge.Door);
            data.FindProperty("swingSign").floatValue = -1;
            data.FindProperty("openAngleDegrees").floatValue = 105;
            data.FindProperty("startOpen").boolValue = false;
            var sourceData = new SerializedObject(source.Door);
            foreach (var key in new[] { "sweepProbe", "handleSweepProbe" })
            {
                var probe = sourceData.FindProperty(key).objectReferenceValue as BoxCollider;
                data.FindProperty(key).objectReferenceValue = probe == null ? null : Match(probe.transform, canonical, target).GetComponent<BoxCollider>();
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            fridge.Door.ConfigurePullHandle(Match(source.Door.PullHandle, canonical, target));
            var grab = target.GetComponent<XRSimpleInteractable>(); grab.colliders.Clear();
            foreach (var collider in canonical.GetComponent<XRSimpleInteractable>().colliders)
            {
                if (!collider.enabled || collider.isTrigger) continue;
                grab.colliders.Add((Collider)Match(collider.transform, canonical, target).GetComponent(collider.GetType()));
            }
            foreach (var lod in target.GetComponentsInChildren<LODGroup>(true)) lod.RecalculateBounds();
            ReviewV2BStorageAuthoring.Attach(world);
            world.modelVersion = Version;
        }

        // Also used by the incremental cabinet tool: moving the fridge must carry its
        // loose stock without rebuilding its door, and repeated applications must not drift.
        public static void ConfigurePosition(ReviewSchemeWorld world)
        {
            if (world.schemeId != SchemeIds.ExternalRefrigerator) throw new InvalidOperationException("B-only authoring");
            var fridge = world.station.ActiveFridge;
            var oldPosition = fridge.transform.position; var oldRotation = fridge.transform.rotation;
            var rotation = Quaternion.Euler(0, 90, 0);
            if (!oldPosition.Equals(Position) || Quaternion.Angle(oldRotation, rotation) > .001f)
            foreach (var food in world.station.GetComponentsInChildren<KitchenStoredIngredient>(true))
            {
                if (world.groceries.groceries.Any(g => g.gameObject == food.gameObject) || food.transform.IsChildOf(fridge.transform)) continue;
                food.transform.SetPositionAndRotation(Position + rotation * (Quaternion.Inverse(oldRotation) * (food.transform.position - oldPosition)),
                    rotation * Quaternion.Inverse(oldRotation) * food.transform.rotation);
            }
            fridge.transform.SetPositionAndRotation(Position, rotation);
        }

        static Mesh Derived(Mesh source, Dictionary<Mesh, Mesh> cache)
        {
            if (source == null) return null;
            if (cache.TryGetValue(source, out var result)) return result;
            if (source.blendShapeCount != 0) throw new InvalidOperationException("Door mesh must be rigid");
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source, out string guid, out long id)) throw new InvalidOperationException(source.name);
            var path = MeshFolder + "/MESH_B_" + guid + "_" + id + ".asset";
            var mesh = Object.Instantiate(source); mesh.name = "B_North_" + source.name;
            mesh.vertices = source.vertices.Select(Mirror).ToArray();
            mesh.normals = source.normals.Select(Mirror).ToArray();
            mesh.tangents = source.tangents.Select(t => new Vector4(-t.x, t.y, t.z, -t.w)).ToArray();
            for (var sub = 0; sub < source.subMeshCount; sub++)
            {
                if (source.GetTopology(sub) != MeshTopology.Triangles) throw new InvalidOperationException("Non-triangle door mesh");
                var indices = source.GetTriangles(sub);
                for (var i = 0; i < indices.Length; i += 3) { var swap = indices[i + 1]; indices[i + 1] = indices[i + 2]; indices[i + 2] = swap; }
                mesh.SetTriangles(indices, sub);
            }
            mesh.RecalculateBounds(); result = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (result == null) { AssetDatabase.CreateAsset(mesh, path); result = mesh; }
            else { EditorUtility.CopySerialized(mesh, result); Object.DestroyImmediate(mesh); EditorUtility.SetDirty(result); }
            cache.Add(source, result); return result;
        }
    }
}
