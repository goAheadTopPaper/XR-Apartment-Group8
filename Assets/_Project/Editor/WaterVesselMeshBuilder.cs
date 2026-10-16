using System.Collections.Generic;
using UnityEngine;

namespace XRApartment.EditorTools
{
    /// Builds the authored open vessel and preview surface without touching assets or scenes.
    internal static class WaterVesselMeshBuilder
    {
        public static Mesh OpenVessel(float radius, float bottom, float height, float wall)
        {
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            const int segments = 40;
            for (int i = 0; i < segments; i++)
            {
                float a = i * 2f * Mathf.PI / segments, b = (i + 1) * 2f * Mathf.PI / segments;
                var ra = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                var rb = new Vector3(Mathf.Cos(b), 0, Mathf.Sin(b));
                var low = Vector3.up * bottom; var high = Vector3.up * (bottom + height);
                Quad(vertices, triangles, ra * radius + low, rb * radius + low, rb * radius + high, ra * radius + high);
                Quad(vertices, triangles, rb * (radius + wall) + low - Vector3.up * wall, ra * (radius + wall) + low - Vector3.up * wall, ra * (radius + wall) + high, rb * (radius + wall) + high);
                Quad(vertices, triangles, ra * radius + high, rb * radius + high, rb * (radius + wall) + high, ra * (radius + wall) + high);
                Quad(vertices, triangles, low, rb * radius + low, ra * radius + low, low);
                Quad(vertices, triangles, low - Vector3.up * wall, ra * (radius + wall) + low - Vector3.up * wall, rb * (radius + wall) + low - Vector3.up * wall, low - Vector3.up * wall);
            }
            var mesh = new Mesh(); mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            var normals = mesh.normals;
            for (int i = 0; i < segments; i++)
            {
                float a = i * 2f * Mathf.PI / segments, b = (i + 1) * 2f * Mathf.PI / segments;
                var ra = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                var rb = new Vector3(Mathf.Cos(b), 0, Mathf.Sin(b));
                int n = i * 20;
                normals[n] = normals[n + 3] = -ra;
                normals[n + 1] = normals[n + 2] = -rb;
                normals[n + 4] = normals[n + 7] = rb;
                normals[n + 5] = normals[n + 6] = ra;
            }
            mesh.normals = normals;
            mesh.RecalculateBounds(); return mesh;
        }

        static void Quad(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int n = vertices.Count; vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
            triangles.Add(n); triangles.Add(n + 1); triangles.Add(n + 2);
            triangles.Add(n); triangles.Add(n + 2); triangles.Add(n + 3);
        }

        public static Mesh PreviewDisk(float radius, float y)
        {
            var vertices = new Vector3[41]; var uv = new Vector2[41]; var triangles = new int[40 * 3];
            vertices[0] = Vector3.up * y;
            for (int i = 0; i < 40; i++)
            {
                float angle = -i * 2f * Mathf.PI / 40;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius);
                uv[i + 1] = new Vector2(vertices[i + 1].x, vertices[i + 1].z) * 5f;
                triangles[i * 3] = 0; triangles[i * 3 + 1] = i + 1; triangles[i * 3 + 2] = (i + 1) % 40 + 1;
            }
            var mesh = new Mesh { vertices = vertices, uv = uv, triangles = triangles };
            mesh.RecalculateNormals(); return mesh;
        }
    }
}
