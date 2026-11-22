using System;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;

namespace XRApartment.Runtime
{
    public enum KitchenMovementMode { Teleport, Smooth }

    /// <summary>Local player preference; has no ownership of review sessions or task state.</summary>
    [RequireComponent(typeof(XROrigin))]
    [DefaultExecutionOrder(100)]
    public sealed class KitchenLocomotionSettings : MonoBehaviour
    {
        public const string PreferenceKey = "XRApartment.Locomotion.Mode";
        public const float ComfortEyeHeight = 1.68f;
        const float CrouchDepth = .5f;
        const float MinimumCrouchedEyeHeight = .75f;
        const float WindowEyeClearance = .055f;
        [SerializeField] ContinuousMoveProvider moveProvider;
        [SerializeField] KitchenTeleportationProvider teleportProvider;
        [SerializeField] SnapTurnProvider snapTurnProvider;
        [SerializeField] XRRayInteractor leftTeleportRay, rightTeleportRay;
        [SerializeField] BoxCollider[] windowPanes = Array.Empty<BoxCollider>();
        // Persistent calls target the existing Starter Assets managers' public property setters.
        // This keeps Runtime independent of the replaceable Samples assembly.
        [SerializeField] UnityEvent<bool> leftSmoothInput = new UnityEvent<bool>();
        [SerializeField] UnityEvent<bool> rightSmoothInput = new UnityEvent<bool>();

        bool started;
        KitchenRoomBounds room;
        Vector3 lastFloorPosition;
        Transform eyeHeightOffset;
        Vector3 windowOffset;
        Vector3 previousHead;
        bool hasPreviousHead;
        float guardedWindowX = float.NegativeInfinity;
        float crouchOffset;
        public XROrigin Origin { get; private set; }
        public CharacterController Character { get; private set; }
        public KitchenMovementMode Mode { get; private set; }
        public bool MenuOpen { get; private set; }
        public ContinuousMoveProvider MoveProvider => moveProvider;
        public KitchenTeleportationProvider TeleportProvider => teleportProvider;
        public event Action Changed;

        void Awake()
        {
            Origin = GetComponent<XROrigin>();
            Character = Origin.Origin.GetComponent<CharacterController>();
        }

        void Start()
        {
            KitchenRoomBounds.TryGet(gameObject.scene, out room);
            lastFloorPosition = Origin.Origin.transform.position;
            started = true;
            ReloadPreference();
        }

        void OnEnable()
        {
            if (windowPanes.Length > 0) Application.onBeforeRender += GuardTrackedWindowCrossing;
            if (started) Apply();
        }

        void OnDisable()
        {
            Application.onBeforeRender -= GuardTrackedWindowCrossing;
            if (eyeHeightOffset != null) eyeHeightOffset.localPosition -= windowOffset;
            windowOffset = Vector3.zero;
            guardedWindowX = float.NegativeInfinity;
            hasPreviousHead = false;
        }

        void LateUpdate()
        {
            GuardTrackedWindowCrossing();
            if (room == null || Character == null || !Character.enabled) return;
            var body = Origin.Origin.transform;
            if (room.Contains(body.position))
            {
                if (room.AtFloor(body.position)) lastFloorPosition = body.position;
                return;
            }
            CancelAim();
            Character.enabled = false;
            body.position = lastFloorPosition;
            Character.enabled = true;
            Physics.SyncTransforms();
        }

        [BeforeRenderOrder(100)]
        void GuardTrackedWindowCrossing()
        {
            if (windowPanes.Length == 0 || Character == null || !Character.enabled) return;
            var bodyX = Origin.Origin.transform.position.x;
            var applied = eyeHeightOffset == null ? Vector3.zero : eyeHeightOffset.parent.TransformVector(windowOffset);
            var tracked = Origin.Camera.transform.position - applied;
            var limit = bodyX >= guardedWindowX - WindowEyeClearance && tracked.x < guardedWindowX
                ? guardedWindowX : float.NegativeInfinity;
            foreach (var pane in windowPanes)
            {
                if (pane == null || !pane.enabled || !pane.gameObject.activeInHierarchy) continue;
                var bounds = pane.bounds;
                var plane = bounds.max.x + WindowEyeClearance;
                if (bodyX < bounds.max.x || tracked.x >= plane) continue;
                var crossing = tracked;
                if (hasPreviousHead && previousHead.x > plane)
                {
                    var fraction = (previousHead.x - plane) / (previousHead.x - tracked.x);
                    crossing = Vector3.Lerp(previousHead, tracked, fraction);
                }
                if (crossing.y < bounds.min.y - WindowEyeClearance || crossing.y > bounds.max.y + WindowEyeClearance ||
                    crossing.z < bounds.min.z - WindowEyeClearance || crossing.z > bounds.max.z + WindowEyeClearance) continue;
                limit = Mathf.Max(limit, plane);
            }
            guardedWindowX = limit;
            var correction = Mathf.Max(0f, limit - tracked.x);
            previousHead = tracked + Vector3.right * correction;
            hasPreviousHead = true;
            if (Mathf.Abs(correction - applied.x) < .0001f && Mathf.Abs(applied.z) < .0001f) return;
            if (!EnsureEyeOffset(Origin, ref eyeHeightOffset)) return;
            var next = eyeHeightOffset.parent.InverseTransformVector(Vector3.right * correction);
            // Offset the view and controllers together. Tracked rotation and the body's floor position stay independent.
            eyeHeightOffset.localPosition += next - windowOffset;
            windowOffset = next;
        }

        public void ReloadPreference()
        {
            var saved = PlayerPrefs.GetInt(PreferenceKey, (int)KitchenMovementMode.Teleport);
            SetMode(saved == (int)KitchenMovementMode.Smooth ? KitchenMovementMode.Smooth : KitchenMovementMode.Teleport, false);
        }

        public void SelectTeleport() => SetMode(KitchenMovementMode.Teleport);
        public void SelectSmooth() => SetMode(KitchenMovementMode.Smooth);

        internal void ToggleCrouch()
        {
            var currentHeight = Origin.CameraInOriginSpaceHeight;
            var nextOffset = crouchOffset > 0f ? 0f : Mathf.Clamp(currentHeight - MinimumCrouchedEyeHeight, 0f, CrouchDepth);
            if (Mathf.Approximately(crouchOffset, nextOffset)) return;
            if (CalibrateEyeHeight(currentHeight + crouchOffset - nextOffset)) crouchOffset = nextOffset;
        }

        internal bool CalibrateEyeHeight(float height) =>
            CalibrateEyeHeight(Origin, teleportProvider, height, ref eyeHeightOffset);

        internal bool AlignViewHeight(bool seated)
        {
            var origin = Origin.Origin.transform;
            var adjustment = eyeHeightOffset == null ? 0f : origin.InverseTransformVector(
                eyeHeightOffset.position - eyeHeightOffset.parent.position).y;
            var actualStanding = Origin.CameraInOriginSpaceHeight + crouchOffset - adjustment;
            var standing = seated ? ComfortEyeHeight : Mathf.Max(ComfortEyeHeight, actualStanding);
            return CalibrateEyeHeight(standing - crouchOffset);
        }

        internal static bool CalibrateEyeHeight(XROrigin xrOrigin, KitchenTeleportationProvider teleport,
            float height, ref Transform offset)
        {
            if (!EnsureEyeOffset(xrOrigin, ref offset)) return false;
            var origin = xrOrigin.Origin.transform;
            var shift = height - xrOrigin.CameraInOriginSpaceHeight;
            offset.position += origin.TransformVector(Vector3.up * shift);
            if (origin.TryGetComponent<CharacterController>(out var character))
            {
                var head = xrOrigin.CameraInOriginSpacePos;
                character.height = Mathf.Max(head.y, 2f * character.radius);
                character.center = new Vector3(head.x, character.height * .5f + character.skinWidth, head.z);
            }
            if (teleport != null) teleport.CancelPending();
            Physics.SyncTransforms();
            return true;
        }

        static bool EnsureEyeOffset(XROrigin xrOrigin, ref Transform offset)
        {
            if (xrOrigin == null || xrOrigin.Origin == null || xrOrigin.Camera == null) return false;
            var origin = xrOrigin.Origin.transform;
            var tracking = xrOrigin.CameraFloorOffsetObject?.transform;
            if (tracking == null || tracking == origin || !tracking.IsChildOf(origin) ||
                !xrOrigin.Camera.transform.IsChildOf(tracking)) return false;

            if (offset == null)
            {
                // Keep the calibrated height separate from XROrigin's tracking-floor offset.
                offset = new GameObject("Eye Height Offset").transform;
                offset.SetParent(tracking.parent, false);
                offset.SetSiblingIndex(tracking.GetSiblingIndex());
                tracking.SetParent(offset, false);
            }
            return true;
        }

        public void SetMode(KitchenMovementMode mode, bool persist = true)
        {
            if (mode != KitchenMovementMode.Teleport && mode != KitchenMovementMode.Smooth)
                throw new ArgumentOutOfRangeException(nameof(mode));
            CancelAim();
            Mode = mode;
            Apply();
            if (persist)
            {
                PlayerPrefs.SetInt(PreferenceKey, (int)mode);
                PlayerPrefs.Save();
            }
        }

        public void SetMenuOpen(bool open)
        {
            if (MenuOpen == open) return;
            if (open) CancelAim();
            MenuOpen = open;
            Apply();
        }

        void CancelAim()
        {
            if (teleportProvider != null) teleportProvider.CancelPending();
            if (leftTeleportRay != null) leftTeleportRay.gameObject.SetActive(false);
            if (rightTeleportRay != null) rightTeleportRay.gameObject.SetActive(false);
        }

        void Apply()
        {
            var smooth = Mode == KitchenMovementMode.Smooth;
            leftSmoothInput.Invoke(smooth);
            // The right stick retains snap turn and teleport in both modes.
            rightSmoothInput.Invoke(false);
            if (moveProvider != null) moveProvider.enabled = smooth && !MenuOpen;
            if (snapTurnProvider != null) snapTurnProvider.enabled = !MenuOpen;
            if (teleportProvider != null) teleportProvider.enabled = !MenuOpen;
            Changed?.Invoke();
        }
    }
}
