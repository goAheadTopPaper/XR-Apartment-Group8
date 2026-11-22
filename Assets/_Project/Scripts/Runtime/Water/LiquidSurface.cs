using UnityEngine;
using XRApartment.Simulation;

namespace XRApartment.Runtime
{
    /// Plane/cavity intersection, rebuilt without managed allocations or shader clipping.
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class LiquidSurface : MonoBehaviour
    {
        const int Sides = 40;
        const int MaximumEdges = Sides * (VesselLiquidGeometry.MaximumSections * 2 - 1);
        readonly Vector3[] points = new Vector3[MaximumEdges];
        readonly Vector3[] normals = new Vector3[MaximumEdges];
        readonly Vector4[] tangents = new Vector4[MaximumEdges];
        readonly Vector2[] uv = new Vector2[MaximumEdges];
        readonly float[] angles = new float[MaximumEdges];
        readonly int[] indices = new int[(MaximumEdges - 2) * 3];
        readonly Vector3[] hull = new Vector3[Sides * VesselLiquidGeometry.MaximumSections];
        Mesh mesh;
        MeshRenderer surfaceRenderer;
        VesselLiquidGeometry previousGeometry;
        Vector3 previousNormal;
        float previousOffset;
        int count;

        void Awake()
        {
            mesh = new Mesh { name = "Runtime_LiquidSurface" };
            mesh.MarkDynamic();
            GetComponent<MeshFilter>().sharedMesh = mesh;
            surfaceRenderer = GetComponent<MeshRenderer>();
            for (int i = 0; i < MaximumEdges - 2; i++)
            {
                indices[i * 3] = 0;
                indices[i * 3 + 1] = i + 1;
                indices[i * 3 + 2] = i + 2;
            }
        }

        public void SetPlane(VesselLiquidGeometry geometry, Vector3 normal, float offset, bool visible)
        {
            if (mesh == null) return;
            if (!visible) { surfaceRenderer.enabled = false; return; }
            if (geometry == previousGeometry && normal == previousNormal && offset == previousOffset && surfaceRenderer.enabled) return;
            if (geometry != previousGeometry) BuildHull(geometry);
            previousGeometry = geometry;
            previousNormal = normal;
            previousOffset = offset;
            count = 0;
            for (var ring = 0; ring < geometry.SectionCount; ring++)
            {
                var start = ring * Sides;
                for (var i = 0; i < Sides; i++)
                {
                    var vertex = hull[start + i];
                    Edge(vertex, hull[start + (i + 1) % Sides], normal, offset);
                    if (ring > 0) Edge(hull[start + i - Sides], vertex, normal, offset);
                }
            }
            surfaceRenderer.enabled = count >= 3;
            if (count < 3) return;
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
            var tangent = Vector3.ProjectOnPlane(Vector3.right, normal).normalized;
            if (tangent.sqrMagnitude < 0.1f) tangent = Vector3.ProjectOnPlane(Vector3.forward, normal).normalized;
            var tangentVector = new Vector4(tangent.x, tangent.y, tangent.z,
                Vector3.Dot(Vector3.Cross(normal, tangent), Vector3.forward) >= 0 ? 1 : -1);
            for (int i = 0; i < count; i++)
            {
                normals[i] = normal;
                tangents[i] = tangentVector;
                uv[i] = new Vector2(points[i].x, points[i].z) * 5f;
            }
            mesh.Clear(false);
            mesh.SetVertices(points, 0, count);
            mesh.SetNormals(normals, 0, count);
            mesh.SetTangents(tangents, 0, count);
            mesh.SetUVs(0, uv, 0, count);
            mesh.SetTriangles(indices, 0, (count - 2) * 3, 0, false);
            mesh.bounds = new Bounds(Vector3.up * geometry.Height * 0.5f,
                new Vector3(geometry.RadiusX * 2f, geometry.Height, geometry.RadiusZ * 2f));
        }

        void BuildHull(VesselLiquidGeometry geometry)
        {
            for (var ring = 0; ring < geometry.SectionCount; ring++)
            {
                var section = geometry.SectionAt(ring);
                for (var i = 0; i < Sides; i++)
                {
                    var angle = 2f * Mathf.PI * i / Sides;
                    hull[ring * Sides + i] = new Vector3(
                        Mathf.Cos(angle) * geometry.RadiusX * section.radiusFraction,
                        geometry.Height * section.heightFraction,
                        Mathf.Sin(angle) * geometry.RadiusZ * section.radiusFraction);
                }
            }
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
