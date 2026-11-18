using System.Collections;
using UnityEngine;
using XRApartment.Data.ReviewV2;

namespace XRApartment.Runtime.ReviewV2.UI
{
    /// <summary>Places the onboarding canvas once after the session enters onboarding.</summary>
    public sealed class ReviewV2T2CanvasPlacement : MonoBehaviour
    {
        [SerializeField] ReviewV2UiSessionAdapter session;
        [SerializeField] Camera viewCamera;
        [SerializeField] Canvas onboardingCanvas;
        [SerializeField, Min(0.5f)] float viewingDistance = 1.05f;
        [SerializeField] float heightAboveEyes = 0.25f;

        Coroutine pendingPlacement;
        string onboardingSessionId;

        void OnEnable()
        {
            if (!Application.isPlaying || session == null) return;
            session.Changed += OnSnapshot;
            OnSnapshot(session.Snapshot);
        }

        void OnDisable()
        {
            if (session != null) session.Changed -= OnSnapshot;
            CancelPlacement();
            onboardingSessionId = null;
        }

        void OnSnapshot(SessionSnapshot snapshot)
        {
            if (snapshot == null || snapshot.Phase != SessionPhase.Onboarding)
            {
                CancelPlacement();
                onboardingSessionId = null;
                return;
            }

            if (onboardingSessionId == snapshot.SessionId) return;
            onboardingSessionId = snapshot.SessionId;
            CancelPlacement();
            pendingPlacement = StartCoroutine(PlaceAfterCalibration(snapshot.SessionId));
        }

        void CancelPlacement()
        {
            if (pendingPlacement == null) return;
            StopCoroutine(pendingPlacement);
            pendingPlacement = null;
        }

        IEnumerator PlaceAfterCalibration(string sessionId)
        {
            // Allow the calibration, tracking and camera offset updates to settle.
            yield return null;
            yield return null;
            pendingPlacement = null;

            var snapshot = session != null ? session.Snapshot : null;
            if (snapshot == null || snapshot.SessionId != sessionId ||
                snapshot.Phase != SessionPhase.Onboarding || viewCamera == null ||
                onboardingCanvas == null || onboardingCanvas.renderMode != RenderMode.WorldSpace)
                yield break;

            var forward = Vector3.ProjectOnPlane(viewCamera.transform.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f)
            {
                Debug.LogWarning("T2 onboarding placement skipped: camera has no horizontal heading.", this);
                yield break;
            }

            forward.Normalize();
            var rotation = Quaternion.LookRotation(forward, Vector3.up);
            var position = viewCamera.transform.position + forward * viewingDistance +
                Vector3.up * heightAboveEyes;
            var rect = (RectTransform)onboardingCanvas.transform;
            var scale = rect.lossyScale;
            var center = position + rotation * Vector3.Scale(rect.rect.center, scale);
            var halfExtents = new Vector3(
                Mathf.Abs(rect.rect.width * scale.x) * 0.5f,
                Mathf.Abs(rect.rect.height * scale.y) * 0.5f, 0.01f);

            foreach (var obstacle in Physics.OverlapBox(center, halfExtents, rotation,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (obstacle.transform.IsChildOf(viewCamera.transform.root) ||
                    obstacle.transform.IsChildOf(onboardingCanvas.transform))
                    continue;
                Debug.LogWarning("T2 onboarding placement skipped: canvas overlaps " +
                    obstacle.name + ".", this);
                yield break;
            }

            rect.SetPositionAndRotation(position, rotation);
        }
    }
}
