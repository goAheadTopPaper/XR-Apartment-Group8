using UnityEngine;

namespace XRApartment.Runtime
{
    /// Plane/cylinder intersection, rebuilt without managed allocations or shader clipping.
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class LiquidSurface : MonoBehaviour
    {
        const int Sides = 40;
        readonly Vector3[] points = new Vector3[Sides * 3];
        readonly Vector3[] normals = new Vector3[Sides * 3];
        readonly Vector4[] tangents = new Vector4[Sides * 3];
        readonly Vector2[] uv = new Vector2[Sides * 3];
        readonly float[] angles = new float[Sides * 3];
        readonly int[] indices = new int[(Sides * 3 - 2) * 3];
        Mesh mesh;
        MeshRenderer surfaceRenderer;
        int count;

        void Awake()
        {
            mesh = new Mesh { name = "Runtime_LiquidSurface" };
            mesh.MarkDynamic();
            GetComponent<MeshFilter>().sharedMesh = mesh;
            surfaceRenderer = GetComponent<MeshRenderer>();
            for (int i = 0; i < Sides * 3 - 2; i++)
            {
                indices[i * 3] = 0;
                indices[i * 3 + 1] = i + 1;
                indices[i * 3 + 2] = i + 2;
            }
        }

        public void SetPlane(float radius, float height, Vector3 normal, float offset, bool visible)
        {
            if (mesh == null) return;
            surfaceRenderer.enabled = visible;
            if (!visible) return;
            count = 0;
            Vector3 first = new Vector3(radius, 0, 0);
            Vector3 previous = first;
            for (int i = 1; i <= Sides; i++)
            {
                float angle = 2f * Mathf.PI * i / Sides;
                var next = new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
                var top = Vector3.up * height;
                Edge(previous, next, normal, offset);
                Edge(previous + top, next + top, normal, offset);
                Edge(previous, previous + top, normal, offset);
                previous = next;
            }
            if (count < 3) { surfaceRenderer.enabled = false; return; }
            Vector3 center = Vector3.zero;
            for (int i = 0; i < count; i++) center += points[i];
            center /= count;
            var axis = Vector3.Cross(normal, Mathf.Abs(normal.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            var other = Vector3.Cross(normal, axis);
            for (int i = 0; i < count; i++)
            {
                var p = points[i] - center;
                float angle = Mathf.Atan2(Vector3.Dot(p, other), Vector3.Dot(p, axis));
                int j = i;
                var value = points[i];
                while (j > 0 && angles[j - 1] > angle)
                { points[j] = points[j - 1]; angles[j] = angles[j - 1]; j--; }
                points[j] = value;
                angles[j] = angle;
            }
            for (int i = 0; i < count; i++)
            {
                normals[i] = normal;
                var tangent = Vector3.ProjectOnPlane(Vector3.right, normal).normalized;
                if (tangent.sqrMagnitude < 0.1f) tangent = Vector3.ProjectOnPlane(Vector3.forward, normal).normalized;
                tangents[i] = new Vector4(tangent.x, tangent.y, tangent.z,
                    Vector3.Dot(Vector3.Cross(normal, tangent), Vector3.forward) >= 0 ? 1 : -1);
                uv[i] = new Vector2(points[i].x, points[i].z) * 5f;
            }
            mesh.Clear(false);
            mesh.SetVertices(points, 0, count);
            mesh.SetNormals(normals, 0, count);
            mesh.SetTangents(tangents, 0, count);
            mesh.SetUVs(0, uv, 0, count);
            mesh.SetTriangles(indices, 0, (count - 2) * 3, 0, false);
            mesh.bounds = new Bounds(Vector3.up * height * 0.5f, new Vector3(radius * 2, height, radius * 2));
        }

        void Edge(Vector3 a, Vector3 b, Vector3 normal, float offset)
        {
            float da = Vector3.Dot(normal, a) - offset;
            float db = Vector3.Dot(normal, b) - offset;
            if (da * db > 0f || Mathf.Abs(da - db) < 0.0000001f) return;
            var p = Vector3.LerpUnclamped(a, b, da / (da - db));
            for (int i = 0; i < count; i++) if ((points[i] - p).sqrMagnitude < 0.0000000001f) return;
            if (count < points.Length) points[count++] = p;
        }

        void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
        }
    }
}
