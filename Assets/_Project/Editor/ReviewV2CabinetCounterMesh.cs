using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using XRApartment.Runtime.ReviewV2;

namespace XRApartment.EditorTools
{
    /// Trims only the A cabinet corner. Never edits the imported mesh or other worlds.
    internal static class ReviewV2CabinetCounterMesh
    {
        struct Vertex
        {
            public Vector3 p, n;
            public Vector2 uv, uv2;
            public Vector4 tangent;
            public static Vertex Lerp(Vertex a, Vertex b, float t) => new Vertex
            { p = Vector3.Lerp(a.p, b.p, t), n = Vector3.Lerp(a.n, b.n, t).normalized,
                uv = Vector2.Lerp(a.uv, b.uv, t), uv2 = Vector2.Lerp(a.uv2, b.uv2, t), tangent = Vector4.Lerp(a.tangent, b.tangent, t) };
        }
        public static void Apply(ReviewSchemeWorld world, string folder)
        {
            var filter = world.GetComponentsInChildren<MeshFilter>(true).Single(f => f.name == "SM_KitchenTable");
            // Always derive from the same source, including on repeated authoring runs.
            var source = AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Kitchen/Meshes/SM_KitchenTable.fbx").OfType<Mesh>().Single();
            if (source.subMeshCount != 1) throw new InvalidOperationException("Inspect new counter submeshes before trimming");
            var fridge = world.station.ActiveFridge.transform;
            var positions = source.vertices; var normals = source.normals; var uv = source.uv; var uv2 = source.uv2; var tangents = source.tangents;
            var vertices = new Vertex[positions.Length];
            for (var i = 0; i < vertices.Length; i++) vertices[i] = new Vertex
            { p = positions[i], n = normals[i], uv = uv.Length == positions.Length ? uv[i] : Vector2.zero,
                uv2 = uv2.Length == positions.Length ? uv2[i] : Vector2.zero,
                tangent = tangents.Length == positions.Length ? tangents[i] : new Vector4(1, 0, 0, 1) };
            var output = new List<Vertex>(); var triangles = new List<int>();
            Func<Vertex, float> x = v => fridge.InverseTransformPoint(filter.transform.TransformPoint(v.p)).x - .755f;
            Func<Vertex, float> z = v => fridge.InverseTransformPoint(filter.transform.TransformPoint(v.p)).z - .75f;
            var sourceTriangles = source.triangles;
            for (var i = 0; i < sourceTriangles.Length; i += 3)
            {
                var tri = new List<Vertex> { vertices[sourceTriangles[i]], vertices[sourceTriangles[i + 1]], vertices[sourceTriangles[i + 2]] };
                // Outside an L-shaped notch = x>=cut OR (x<cut AND z>=cut).
                Emit(Clip(tri, x, true), output, triangles);
                Emit(Clip(Clip(tri, x, false), z, true), output, triangles);
            }
            var mesh = new Mesh { name = "MESH_A_CounterCabinetRelief" };
            mesh.SetVertices(output.Select(v => v.p).ToList()); mesh.SetNormals(output.Select(v => v.n).ToList());
            mesh.SetUVs(0, output.Select(v => v.uv).ToList()); mesh.SetUVs(1, output.Select(v => v.uv2).ToList());
            mesh.SetTangents(output.Select(v => v.tangent).ToList()); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
            var path = folder + "/MESH_A_CounterCabinetRelief.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) { AssetDatabase.CreateAsset(mesh, path); existing = mesh; }
            else { EditorUtility.CopySerialized(mesh, existing); UnityEngine.Object.DestroyImmediate(mesh); EditorUtility.SetDirty(existing); }
            filter.sharedMesh = existing; filter.GetComponent<MeshCollider>().sharedMesh = existing;
        }
        static List<Vertex> Clip(List<Vertex> input, Func<Vertex, float> distance, bool positive)
        {
            var result = new List<Vertex>();
            for (var i = 0; i < input.Count; i++)
            {
                var a = input[i]; var b = input[(i + 1) % input.Count];
                var da = distance(a) * (positive ? 1 : -1); var db = distance(b) * (positive ? 1 : -1);
                if (da >= 0) result.Add(a);
                if ((da >= 0) != (db >= 0)) result.Add(Vertex.Lerp(a, b, da / (da - db)));
            }
            return result;
        }
        static void Emit(List<Vertex> polygon, List<Vertex> vertices, List<int> triangles)
        {
            for (var i = 1; i + 1 < polygon.Count; i++)
            {
                if (Vector3.Cross(polygon[i].p - polygon[0].p, polygon[i + 1].p - polygon[0].p).sqrMagnitude < 1e-12f) continue;
                var start = vertices.Count; vertices.Add(polygon[0]); vertices.Add(polygon[i]); vertices.Add(polygon[i + 1]);
                triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
            }
        }
    }
}
