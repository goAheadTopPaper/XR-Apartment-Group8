using Unity.Profiling;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Transformers;
using XRApartment.Data;

namespace XRApartment.Runtime
{
    /// XRI still computes the dynamic grip; only its final requested prop pose is constrained.
    /// Throw smoothing therefore sees the accepted motion, never a controller inside a table.
    [DisallowMultipleComponent]
    public sealed class CollisionConstrainedGrabTransformer : XRGeneralGrabTransformer, IXRDropTransformer
    {
        static readonly ProfilerMarker SweepMarker = new ProfilerMarker("XR Apartment.Held Prop Sweep");
        [SerializeField] HandInteractionProfile profile;
        [SerializeField] BoxCollider sweepProbe;
        HeldPropSweep sweep;
        Pose accepted;
        Transform rig;
        bool restoreThrow;
        bool previousThrow;
        float skin;

        public bool IsBlocked { get; private set; }
        public bool QuerySaturated => sweep != null && sweep.QuerySaturated;
        public Pose RequestedPose { get; private set; }
        public Pose AcceptedPose => accepted;
        public bool canProcessOnDrop => false;

        public override void OnLink(XRGrabInteractable grabInteractable)
        {
            base.OnLink(grabInteractable);
            sweep = new HeldPropSweep(sweepProbe, transform, profile.ContactLayers);
            skin = profile.Length("grab_contact_skin");
        }

        public override void OnGrab(XRGrabInteractable grabInteractable)
        {
            RestoreThrow(grabInteractable);
            base.OnGrab(grabInteractable);
            accepted = new Pose(transform.position, transform.rotation);
            RequestedPose = accepted;
            rig = grabInteractable.interactorsSelecting[0].transform.root;
            IsBlocked = false;
        }

        public override void Process(XRGrabInteractable grabInteractable,
            XRInteractionUpdateOrder.UpdatePhase updatePhase, ref Pose targetPose, ref Vector3 localScale)
        {
            base.Process(grabInteractable, updatePhase, ref targetPose, ref localScale);
            if (!grabInteractable.isSelected || (updatePhase != XRInteractionUpdateOrder.UpdatePhase.Dynamic &&
                updatePhase != XRInteractionUpdateOrder.UpdatePhase.OnBeforeRender)) return;
            RequestedPose = targetPose;
            using (SweepMarker.Auto())
                accepted = sweep.Resolve(accepted, targetPose, rig, skin,
                    profile.GrabRotationStepDegrees, profile.GrabRotationIterations);
            IsBlocked = sweep.QuerySaturated || Vector3.SqrMagnitude(accepted.position - targetPose.position) > .000001f ||
                        Quaternion.Angle(accepted.rotation, targetPose.rotation) > .5f;
            targetPose = accepted;
        }

        public Pose ResolveHand(Pose tracked)
        {
            var correction = transform.rotation * Quaternion.Inverse(RequestedPose.rotation);
            return new Pose(transform.position + correction * (tracked.position - RequestedPose.position),
                correction * tracked.rotation);
        }

        public void OnDrop(XRGrabInteractable grabInteractable, DropEventArgs args)
        {
            // XRI invokes this once per registration list; this transformer is in both.
            if (!IsBlocked || restoreThrow) return;
            previousThrow = grabInteractable.throwOnDetach;
            restoreThrow = true;
            grabInteractable.throwOnDetach = false;
            var body = grabInteractable.GetComponent<Rigidbody>();
            body.velocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        public override void OnUnlink(XRGrabInteractable grabInteractable)
        {
            RestoreThrow(grabInteractable);
            base.OnUnlink(grabInteractable);
        }

        void RestoreThrow(XRGrabInteractable grabInteractable)
        {
            if (!restoreThrow) return;
            grabInteractable.throwOnDetach = previousThrow;
            restoreThrow = false;
        }
    }
}
