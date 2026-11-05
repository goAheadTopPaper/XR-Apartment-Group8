using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.XR;
using XRApartment.Simulation;

namespace XRApartment.Runtime.Core
{
    [RequireComponent(typeof(JourneyRuntimeHost), typeof(TaskEvidenceBridge))]
    public sealed class ReviewBoundarySafety : MonoBehaviour, ISafetySource
    {
        private const string Warning = "Near the physical play-area boundary.";
        private const string Action = "Stop and move back toward the centre.";
        [SerializeField] private Shader warningOverlay;
        [SerializeField, Min(.01f)] private float warnWithinMetres = .25f;
        [SerializeField, Min(.01f)] private float clearBeyondMetres = .35f;

        private readonly List<XRInputSubsystem> inputs = new List<XRInputSubsystem>();
        private readonly List<Vector3> boundary = new List<Vector3>();
        private readonly List<InputDevice> devices = new List<InputDevice>();
        private JourneyRuntimeHost host;
        private TaskEvidenceBridge evidence;
        private TextMeshPro banner;
        private Material bannerMaterial;
        private bool warning;
        private float nextSample;

        public event Action<SafetyNotice> SafetyObserved;

        private void Awake()
        {
            host = GetComponent<JourneyRuntimeHost>();
            evidence = GetComponent<TaskEvidenceBridge>();
        }

        private void LateUpdate()
        {
            if (warning && banner == null) ShowBanner();
            if (host.Core == null || Time.unscaledTime < nextSample) return;
            nextSample = Time.unscaledTime + .1f;
            ApplyClearanceSample(TryGetTrackedClearance(out var clearance) ? clearance : (float?)null);
        }

        private void ApplyClearanceSample(float? clearance)
        {
            // Loss of tracking or of stage bounds cannot confirm that the user is clear.
            if (!clearance.HasValue) return;
            var threshold = warning ? Mathf.Max(warnWithinMetres, clearBeyondMetres) : warnWithinMetres;
            SetWarning(clearance.Value <= threshold);
        }

        private bool TryGetTrackedClearance(out float clearance)
        {
            clearance = float.PositiveInfinity;
            SubsystemManager.GetSubsystems(inputs);
            foreach (var input in inputs)
            {
                // Floor is the supported Unity OpenXR mode for stage boundary points.
                if (!input.running || input.GetTrackingOriginMode() != TrackingOriginModeFlags.Floor)
                    continue;
                boundary.Clear();
                if (!input.TryGetBoundaryPoints(boundary)) continue;
                devices.Clear();
                if (!input.TryGetInputDevices(devices)) continue;
                var hasTrackedHead = false;
                var nearest = float.PositiveInfinity;
                foreach (var device in devices)
                {
                    var isHead = (device.characteristics & InputDeviceCharacteristics.HeadMounted) != 0;
                    if (!isHead && (device.characteristics & InputDeviceCharacteristics.Controller) == 0)
                        continue;
                    if (!device.TryGetFeatureValue(CommonUsages.isTracked, out var tracked) || !tracked ||
                        !device.TryGetFeatureValue(CommonUsages.devicePosition, out var position) ||
                        !TryGetClearance(boundary, position, out var distance))
                        continue;
                    hasTrackedHead |= isHead;
                    nearest = Mathf.Min(nearest, distance);
                }
                if (!hasTrackedHead) continue;
                clearance = nearest;
                return true;
            }
            return false;
        }

        /// <summary>Returns signed horizontal distance to the closest boundary segment in tracking space.</summary>
        public static bool TryGetClearance(IReadOnlyList<Vector3> corners, Vector3 point, out float clearance)
        {
            clearance = 0f;
            if (corners == null || corners.Count < 3) return false;
            var position = new Vector2(point.x, point.z);
            var closest = float.PositiveInfinity;
            var area = 0f;
            var inside = false;
            var a = new Vector2(corners[corners.Count - 1].x, corners[corners.Count - 1].z);
            for (var index = 0; index < corners.Count; index++)
            {
                var corner = corners[index];
                var b = new Vector2(corner.x, corner.z);
                var edge = b - a;
                area += a.x * b.y - b.x * a.y;
                if (edge.sqrMagnitude > 0f)
                {
                    var t = Mathf.Clamp01(Vector2.Dot(position - a, edge) / edge.sqrMagnitude);
                    closest = Mathf.Min(closest, (position - a - edge * t).sqrMagnitude);
                    if ((a.y > position.y) != (b.y > position.y) &&
                        position.x < a.x + edge.x * (position.y - a.y) / edge.y) inside = !inside;
                }
                a = b;
            }
            if (float.IsNaN(closest) || float.IsInfinity(closest) ||
                float.IsInfinity(area) || !(Mathf.Abs(area) > .001f))
                return false;
            clearance = Mathf.Sqrt(closest) * (inside ? 1f : -1f);
            return true;
        }

        private void SetWarning(bool active)
        {
            if (warning == active) return;
            warning = active;
            if (active) ShowBanner();
            else if (banner != null) banner.gameObject.SetActive(false);
            var snapshot = host.Core?.Snapshot;
            if (snapshot == null || snapshot.State == SessionState.Finished ||
                snapshot.State == SessionState.Exited) return;
            SafetyObserved?.Invoke(new SafetyNotice
            {
                SafetyEventId = "physical-play-area",
                SessionId = snapshot.SessionId,
                RiskType = "boundary",
                IsActive = active,
                OccurredAtSessionSeconds = evidence.SessionSeconds,
                Message = Warning,
                SuggestedAction = Action
            });
        }

        private void ShowBanner()
        {
            if (banner == null)
            {
                var camera = Camera.main;
                if (camera == null || camera.gameObject.scene != gameObject.scene) return;
                var label = new GameObject("Physical Play Area Warning");
                label.transform.SetParent(camera.transform, false);
                label.transform.localPosition = new Vector3(0f, .1f, .55f);
                label.transform.localScale = Vector3.one * .06f;
                banner = label.AddComponent<TextMeshPro>();
                banner.rectTransform.sizeDelta = new Vector2(8f, 2f);
                banner.alignment = TextAlignmentOptions.Center;
                banner.fontSize = 4f;
                banner.color = new Color(1f, .39f, .41f);
                banner.text = "<mark=#170608ee>!  SAFETY  •  " + Warning + "\n" + Action + "</mark>";
                if (warningOverlay != null && banner.fontSharedMaterial != null)
                {
                    bannerMaterial = new Material(banner.fontSharedMaterial) { shader = warningOverlay };
                    banner.fontSharedMaterial = bannerMaterial;
                }
            }
            banner.gameObject.SetActive(true);
        }

        private void OnDisable() => SetWarning(false);

        private void OnDestroy()
        {
            if (banner != null) Destroy(banner.gameObject);
            if (bannerMaterial != null) Destroy(bannerMaterial);
        }
    }
}
