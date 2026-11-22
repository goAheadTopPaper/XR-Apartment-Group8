using UnityEngine;

namespace XRApartment.Runtime.UI
{
    internal static class KitchenPanelPlacement
    {
        const float SurfaceClearance = .045f;

        internal static void Place(RectTransform panel, Camera camera, Transform rig, Pose preferred,
            Vector3 authoredScale, RaycastHit[] hits)
        {
            panel.localScale = authoredScale;
            var scale = panel.lossyScale;
            var rect = panel.rect;
            var eye = camera.transform.position;
            var fraction = 1f;
            for (var y = -1; y <= 1; y++)
            {
                for (var x = -1; x <= 1; x++)
                {
                    var sample = new Vector3((rect.center.x + x * rect.width * .5f) * scale.x,
                        (rect.center.y + y * rect.height * .5f) * scale.y, 0f);
                    var ray = preferred.position + preferred.rotation * sample - eye;
                    var distance = ray.magnitude;
                    if (distance < .001f) continue;
                    var count = Physics.RaycastNonAlloc(eye, ray / distance, hits, distance + SurfaceClearance,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                    for (var i = 0; i < count; i++)
                    {
                        var obstacle = hits[i].collider;
                        // Fit to room geometry so a cup held near the headset cannot collapse the menu.
                        if (obstacle.attachedRigidbody == null && !obstacle.transform.IsChildOf(rig))
                            fraction = Mathf.Min(fraction, (hits[i].distance - SurfaceClearance) / distance);
                    }
                }
            }

            // Preserve the panel's viewing angle and usable button sizes when it moves closer.
            var nearest = camera.nearClipPlane * 3f / Mathf.Max(.01f, Vector3.Distance(eye, preferred.position));
            fraction = Mathf.Clamp(fraction, Mathf.Min(nearest, 1f), 1f);
            panel.localScale = authoredScale * fraction;
            panel.SetPositionAndRotation(Vector3.LerpUnclamped(eye, preferred.position, fraction), preferred.rotation);
        }
    }
}
