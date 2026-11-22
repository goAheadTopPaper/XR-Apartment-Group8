using UnityEngine;
using UnityEngine.UI;

namespace XRApartment.Runtime.UI
{
    internal sealed class KitchenMenuHoldIndicator : MaskableGraphic
    {
        const int CircleSegments = 64;
        static readonly Color Disc = new Color(.035f, .055f, .07f, .97f);
        static readonly Color Track = new Color(.26f, .34f, .38f);
        static readonly Color Fill = new Color(.43f, .94f, .79f);
        static readonly Color Glyph = new Color(.92f, .97f, .96f);
        float progress;

        internal static KitchenMenuHoldIndicator Create(Transform parent, Material surface)
        {
            var view = new GameObject("Menu hold indicator", typeof(RectTransform), typeof(CanvasRenderer), typeof(KitchenMenuHoldIndicator));
            view.layer = parent.gameObject.layer;
            var indicator = view.GetComponent<KitchenMenuHoldIndicator>();
            var rect = indicator.rectTransform;
            rect.SetParent(parent, false);
            rect.sizeDelta = Vector2.one * 120f;
            rect.localScale = Vector3.one * .0012f;
            indicator.raycastTarget = false;
            indicator.material = surface;
            return indicator;
        }

        internal void Show(Transform head, float amount)
        {
            transform.SetPositionAndRotation(head.TransformPoint(0f, .12f, .9f), head.rotation);
            progress = Mathf.Clamp01(amount);
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            SetVerticesDirty();
        }

        internal void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var rect = rectTransform.rect;
            var center = rect.center;
            var radius = .5f * Mathf.Min(rect.width, rect.height);
            AddBand(mesh, center, 0f, radius, 1f, Disc);
            AddBand(mesh, center, radius * .8f, radius * .93f, 1f, Track);
            AddBand(mesh, center, radius * .8f, radius * .93f, progress, Fill);

            var halfWidth = radius * .34f;
            var halfHeight = radius * .042f;
            for (var bar = -1; bar <= 1; bar++)
            {
                var row = center + Vector2.up * (bar * radius * .2f);
                AddQuad(mesh, row + new Vector2(-halfWidth, -halfHeight), row + new Vector2(-halfWidth, halfHeight),
                    row + new Vector2(halfWidth, halfHeight), row + new Vector2(halfWidth, -halfHeight), Glyph);
            }
        }

        static void AddBand(VertexHelper mesh, Vector2 center, float inner, float outer, float amount, Color32 color)
        {
            var count = Mathf.CeilToInt(amount * CircleSegments);
            var from = Vector2.up;
            for (var segment = 1; segment <= count; segment++)
            {
                var angle = 2f * Mathf.PI * Mathf.Min(amount, segment / (float)CircleSegments);
                var to = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
                AddQuad(mesh, center + from * inner, center + from * outer, center + to * outer, center + to * inner, color);
                from = to;
            }
        }

        static void AddQuad(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color32 color)
        {
            var index = mesh.currentVertCount;
            mesh.AddVert(a, color, Vector2.zero);
            mesh.AddVert(b, color, Vector2.zero);
            mesh.AddVert(c, color, Vector2.zero);
            mesh.AddVert(d, color, Vector2.zero);
            mesh.AddTriangle(index, index + 1, index + 2);
            mesh.AddTriangle(index, index + 2, index + 3);
        }
    }
}
