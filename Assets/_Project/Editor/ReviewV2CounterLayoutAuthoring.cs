using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using XRApartment.Runtime;
using XRApartment.Runtime.ReviewV2;
using Object = UnityEngine.Object;

namespace XRApartment.EditorTools
{
    /// A-only worktop and cooking placements. Cabinet fronts and shared Cooking assets stay untouched.
    public static class ReviewV2CounterLayoutAuthoring
    {
        public const string Version = ReviewV2PanSnapAuthoring.Version;
        public const string MeshPath = "Assets/_Project/Art/ReviewV2/MESH_A_ChamferWorktop.asset";
        public const float WorktopHeight = 1.01f;
        public static readonly Vector3 HobPosition = new Vector3(-3.95f, 1.015f, 1.95f);
        public static readonly Vector3 BoardPosition = new Vector3(-2.95f, 1.015f, 1.95f);
        // Measured prototype coordinates. The diagonal recovers part of the old rectangular notch.
        public static readonly Vector2 ChamferStart = new Vector2(-2.56f, 1.67661285f);
        public static readonly Vector2 ChamferEnd = new Vector2(-1.99f, 1.81f);

        [MenuItem("XR Apartment/Review V2/Apply A Chamfer Worktop and Cooking Layout")]
        public static void Apply()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Stop Play Mode before authoring");
            var path = ReviewV2SchemeAuthoring.LayoutPath("SCHEME_A");
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var world = root.GetComponent<ReviewSchemeWorld>();
                ConfigureWorld(world);
                world.modelVersion = Version;
                world.identities = root.GetComponentsInChildren<ReviewSceneIdentity>(true);
                if (!world.IsConfigured) throw new InvalidOperationException("Incomplete A world");
                if (PrefabUtility.SaveAsPrefabAsset(root, path) == null) throw new IOException(path);
                File.WriteAllLines("Docs/M2/SCHEME_A_Identities.tsv", world.identities.Select(i =>
                    "SCHEME_A\t" + Version + "\t" + i.Id + "\t" + i.name));
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("REVIEW_V2_A_COUNTER: " + Version + "; chamfered top, left hob/pan, right prep; cabinet fronts unchanged");
        }

        public static void ConfigureWorld(ReviewSchemeWorld world)
        {
            if (world.schemeId != "SCHEME_A") throw new InvalidOperationException("A-only authoring");
            var old = world.transform.Find("A_ChamferWorktop");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var counter = world.GetComponentsInChildren<MeshRenderer>(true).Single(r => r.name == "SM_KitchenTable");
            var top = new GameObject("A_ChamferWorktop"); top.transform.SetParent(world.transform, false);
            top.AddComponent<ReviewSceneIdentity>().Configure("COUNTER_WORKTOP");
            var mesh = BuildWorktopMesh();
            top.AddComponent<MeshFilter>().sharedMesh = mesh;
            top.AddComponent<MeshRenderer>().sharedMaterial = counter.sharedMaterial;
            top.AddComponent<MeshCollider>().sharedMesh = mesh;

            var station = world.station.transform;
            var hob = Required(station, "VIS_Real_gas_hob"); var delta = HobPosition - hob.position;
            foreach (var name in new[] { "VIS_Real_gas_hob", "COL_GasHob", "ACT_STOVE", "VIS_GasIndicator",
                "LOC_BurnerSurface", "ACT_HeatZone", "FX_GasBurner" }) Required(station, name).position += delta;

            Place(station, "PREF_Cooking_ChoppingBoard", BoardPosition);
            Place(station, "PREF_Prop_knife", BoardPosition + Vector3.up * .03f);
            Place(station, "PREF_Cooking_MixingBowl", new Vector3(-2.43f, 1.015f, 1.90f));
            Place(station, "PREF_Cooking_TomatoBowl", new Vector3(-2.42f, 1.015f, 2.25f));
            Place(station, "PREF_Cooking_Whisk", new Vector3(-3.32f, 1.03f, 2.25f));
            Place(station, "PREF_Cooking_Spatula", new Vector3(-2.88f, 1.03f, 2.26f));
            Place(station, "PREF_Cooking_Ladle", new Vector3(-2.67f, 1.04f, 2.32f));
            Place(station, "PREF_Prop_plate", new Vector3(-3.38f, 1.015f, 1.86f));

            var pan = Required(station, "PREF_Cooking_FryingPan");
            pan.rotation = Quaternion.identity;
            pan.position = new Vector3(HobPosition.x, 1.015f, HobPosition.z);
            var support = Required(station, "LOC_BurnerSurface");
            var bottom = float.PositiveInfinity;
            foreach (var collider in pan.GetComponentsInChildren<BoxCollider>(true).Where(c => c.enabled && !c.isTrigger))
                for (var corner = 0; corner < 8; corner++)
                {
                    var p = collider.center + Vector3.Scale(collider.size * .5f, new Vector3(
                        (corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                    bottom = Mathf.Min(bottom, collider.transform.TransformPoint(p).y);
                }
            if (float.IsInfinity(bottom)) throw new InvalidOperationException("Pan support collider missing");
            pan.position += Vector3.up * (support.position.y + .002f - bottom);
            ReviewV2PanSnapAuthoring.ConfigureWorld(world);
            world.GetComponentsInChildren<ReviewSceneIdentity>(true).Single(i => i.Id == "WORKTOP").transform.position = BoardPosition;
            // Positions are authored before Awake, so CookingStation/RoomReset cache these exact starting poses.
        }

        static Transform Required(Transform parent, string name) => parent.Find(name) ??
            throw new InvalidOperationException("Missing layout binding: " + name);
        static void Place(Transform parent, string name, Vector3 position) => Required(parent, name).position = position;

        static Mesh BuildWorktopMesh()
        {
            // The 20mm top bridges above the lower-door sweep; its underside is 5mm above that leaf.
            // Existing recessed lower cabinetry is retained. The six-point front contour has one diagonal, no step.
            var polygon = new[] {
                new Vector2(-4.45910263f, 2.361947f), new Vector2(-4.45910263f, 1.67661285f),
                ChamferStart, ChamferEnd, new Vector2(-1.45194459f, 1.81f), new Vector2(-1.45194459f, 2.361947f)
            };
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            for (var layer = 0; layer < 2; layer++)
            {
                var start = vertices.Count; var y = layer == 0 ? WorktopHeight : WorktopHeight - .02f;
                foreach (var p in polygon) { vertices.Add(new Vector3(p.x, y, p.y)); uv.Add(p); }
                for (var i = 1; i + 1 < polygon.Length; i++)
                { triangles.Add(start); triangles.Add(start + (layer == 0 ? i + 1 : i)); triangles.Add(start + (layer == 0 ? i : i + 1)); }
            }
            for (var i = 0; i < polygon.Length; i++)
            {
                var a = polygon[i]; var b = polygon[(i + 1) % polygon.Length]; var start = vertices.Count;
                vertices.Add(new Vector3(a.x, WorktopHeight - .02f, a.y));
                vertices.Add(new Vector3(a.x, WorktopHeight, a.y));
                vertices.Add(new Vector3(b.x, WorktopHeight, b.y));
                vertices.Add(new Vector3(b.x, WorktopHeight - .02f, b.y));
                uv.Add(Vector2.zero); uv.Add(Vector2.up); uv.Add(Vector2.one); uv.Add(Vector2.right);
                triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }
            var mesh = new Mesh { name = "MESH_A_ChamferWorktop" };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
            var asset = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (asset == null) { AssetDatabase.CreateAsset(mesh, MeshPath); asset = mesh; }
            else { EditorUtility.CopySerialized(mesh, asset); Object.DestroyImmediate(mesh); EditorUtility.SetDirty(asset); }
            return asset;
        }
    }
}
