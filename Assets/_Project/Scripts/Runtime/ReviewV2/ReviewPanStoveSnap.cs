using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using XRApartment.Data;

namespace XRApartment.Runtime.ReviewV2
{
    /// Unheld, upright pans settle into a collision-checked stove pose. XRI keeps owning all grabs.
    [DefaultExecutionOrder(250)]
    [RequireComponent(typeof(Rigidbody), typeof(XRGrabInteractable))]
    public sealed class ReviewPanStoveSnap : MonoBehaviour
    {
        [SerializeField] KitchenCookingStation station;
        [SerializeField] Transform support;
        [SerializeField] Vector3 dockOffset;
        [SerializeField] BoxCollider sweepProbe;
        [SerializeField] HandInteractionProfile collisionProfile;
        [SerializeField, Min(.001f)] float captureRadius = .065f;
        [SerializeField, Min(.001f)] float captureHeight = .06f;
        [SerializeField, Range(0, 45)] float maximumTilt = 20f;
        [SerializeField, Min(.01f)] float maximumSpeed = .6f;
        Rigidbody body;
        XRGrabInteractable grab;
        RigidbodyConstraints originalConstraints;
        HeldPropSweep sweep;
        Pose lockedPose;
        int notBeforeFrame;
        public bool IsDocked { get; private set; }
        public bool IsConfigured => station != null && support != null && sweepProbe != null && collisionProfile != null;
        public Vector3 DockPosition => support.TransformPoint(dockOffset);

        public void Configure(KitchenCookingStation owner, Transform surface, Vector3 offset,
            BoxCollider probe, HandInteractionProfile profile)
        { station = owner; support = surface; dockOffset = offset; sweepProbe = probe; collisionProfile = profile; }

        void Awake()
        {
            body = GetComponent<Rigidbody>(); grab = GetComponent<XRGrabInteractable>();
            originalConstraints = body.constraints;
            if (IsConfigured) sweep = new HeldPropSweep(sweepProbe, transform, collisionProfile.ContactLayers);
        }
        void OnEnable()
        {
            grab.selectEntered.AddListener(Selected);
            grab.selectExited.AddListener(Released);
            if (station != null) station.ResetStarting += ResetStarting;
        }
        void OnDisable()
        {
            grab.selectEntered.RemoveListener(Selected);
            grab.selectExited.RemoveListener(Released);
            if (station != null) station.ResetStarting -= ResetStarting;
            Unlock();
        }
        void Selected(SelectEnterEventArgs args)
        {
            // XRI caches kinematic/gravity state before selectEntered. We never change those
            // for docking, and XRI does not cache constraints: restoring them here is sufficient.
            Unlock();
        }
        void Released(SelectExitEventArgs args) => notBeforeFrame = Time.frameCount + 2;
        void ResetStarting()
        {
            Unlock();
            notBeforeFrame = Time.frameCount + 4;
        }
        void Unlock()
        {
            if (!IsDocked) return;
            IsDocked = false;
            if (body != null) body.constraints = originalConstraints;
        }
        void ClearMotion()
        {
            if (body.isKinematic) return;
            body.velocity = Vector3.zero; body.angularVelocity = Vector3.zero;
        }
        void LateUpdate()
        {
            if (!IsConfigured || sweep == null) return;
            if (grab.isSelected || station.IsResetting) { Unlock(); return; }
            if (Time.deltaTime <= 0 || !grab.enabled || Time.frameCount < notBeforeFrame) return;
            if (IsDocked)
            {
                ClearMotion();
                body.position = lockedPose.position; body.rotation = lockedPose.rotation;
                transform.SetPositionAndRotation(lockedPose.position, lockedPose.rotation);
                return;
            }
            if (body.isKinematic || body.velocity.sqrMagnitude > maximumSpeed * maximumSpeed ||
                body.angularVelocity.sqrMagnitude > 3.5f * 3.5f) return;
            var delta = transform.position - DockPosition;
            var vertical = Vector3.Dot(delta, support.up);
            if (Vector3.ProjectOnPlane(delta, support.up).sqrMagnitude > captureRadius * captureRadius ||
                Mathf.Abs(vertical) > captureHeight || Vector3.Angle(transform.up, support.up) > maximumTilt) return;
            var forward = Vector3.ProjectOnPlane(transform.forward, support.up).normalized;
            var desired = new Pose(DockPosition, Quaternion.LookRotation(forward, support.up));
            Physics.SyncTransforms();
            var accepted = sweep.Resolve(new Pose(transform.position, transform.rotation), desired, null,
                collisionProfile.Length("grab_contact_skin"), collisionProfile.GrabRotationStepDegrees,
                collisionProfile.GrabRotationIterations);
            var correction = accepted.position - desired.position;
            var lift = Vector3.Dot(correction, support.up);
            // The conservative full-pan probe may need a small support clearance correction.
            // A blocked horizontal path, blocked rotation or saturated query never docks.
            if (sweep.QuerySaturated || Vector3.ProjectOnPlane(correction, support.up).sqrMagnitude > .002f * .002f ||
                lift < -.001f || lift > .015f || Quaternion.Angle(accepted.rotation, desired.rotation) > .2f) return;
            lockedPose = accepted;
            ClearMotion();
            body.position = accepted.position; body.rotation = accepted.rotation;
            transform.SetPositionAndRotation(accepted.position, accepted.rotation);
            body.constraints = RigidbodyConstraints.FreezeAll;
            body.WakeUp(); // The heat trigger must observe the pan even when docking before the first physics step.
            IsDocked = true;
        }
    }
}
