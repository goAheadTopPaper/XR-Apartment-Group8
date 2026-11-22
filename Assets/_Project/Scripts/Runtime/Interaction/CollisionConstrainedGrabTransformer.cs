using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Transformers;
using XRApartment.Data;

namespace XRApartment.Runtime
{
    /// Constrains the final prop pose computed from the selected XRI grip.
    /// Throw smoothing therefore sees the accepted motion, never a controller inside a table.
    [DisallowMultipleComponent]
    public sealed class CollisionConstrainedGrabTransformer : XRGeneralGrabTransformer, IXRDropTransformer
    {
        static readonly ProfilerMarker SweepMarker = new ProfilerMarker("XR Apartment.Held Prop Sweep");
        [SerializeField] HandInteractionProfile profile;
        [SerializeField] BoxCollider sweepProbe;
        [SerializeField] Transform alternateGrip;
        HeldPropSweep sweep;
        Pose accepted;
        Transform rig;
        bool restoreThrow;
        bool previousThrow;
        float skin;
        float nextImpact;
        int gripSoundFrame = -1;
        Rigidbody body;
        Transform lightSample;
        bool searchedLightRenderers;

        public bool IsBlocked { get; private set; }
        public bool QuerySaturated => sweep != null && sweep.QuerySaturated;
        public Pose RequestedPose { get; private set; }
        public Pose AcceptedPose => accepted;
        public bool canProcessOnDrop => false;

        public override void OnLink(XRGrabInteractable grabInteractable)
        {
            base.OnLink(grabInteractable);
            body = grabInteractable.GetComponent<Rigidbody>();
            sweep = new HeldPropSweep(sweepProbe, transform, profile.ContactLayers);
            skin = profile.Length("grab_contact_skin");
        }

        public override void OnGrab(XRGrabInteractable grabInteractable)
        {
            RestoreThrow(grabInteractable);
            var interactor = grabInteractable.interactorsSelecting[0];
            if (alternateGrip != null)
            {
                var primary = grabInteractable.attachTransform;
                var handPosition = interactor.GetAttachTransform(grabInteractable).position;
                var grip = (handPosition - alternateGrip.position).sqrMagnitude < (handPosition - primary.position).sqrMagnitude
                    ? alternateGrip : primary;
                // XRI made a private dynamic attach transform from the primary grip.
                // Choose the closest handle before the transformer captures its relative pose.
                grabInteractable.GetAttachTransform(interactor).SetPositionAndRotation(grip.position, grip.rotation);
            }
            base.OnGrab(grabInteractable);
            accepted = new Pose(transform.position, transform.rotation);
            RequestedPose = accepted;
            rig = interactor.transform.root;
            IsBlocked = false;
            if (gripSoundFrame != Time.frameCount)
            {
                gripSoundFrame = Time.frameCount;
                KitchenAudio.Play(gameObject.scene, KitchenCue.Grip, transform.position, .5f);
            }
        }

        public override void Process(XRGrabInteractable grabInteractable,
            XRInteractionUpdateOrder.UpdatePhase updatePhase, ref Pose targetPose, ref Vector3 localScale)
        {
            base.Process(grabInteractable, updatePhase, ref targetPose, ref localScale);
            if (!grabInteractable.isSelected || (updatePhase != XRInteractionUpdateOrder.UpdatePhase.Dynamic &&
                updatePhase != XRInteractionUpdateOrder.UpdatePhase.OnBeforeRender)) return;
            RequestedPose = targetPose;
            using (SweepMarker.Auto())
            {
                // XRI applies instantaneous poses through Transform. A second held prop
                // must query the first prop's accepted pose, not its previous physics pose.
                Physics.SyncTransforms();
                accepted = sweep.Resolve(accepted, targetPose, rig, skin,
                    profile.GrabRotationStepDegrees, profile.GrabRotationIterations);
            }
            IsBlocked = sweep.QuerySaturated || Vector3.SqrMagnitude(accepted.position - targetPose.position) > .000001f ||
                        Quaternion.Angle(accepted.rotation, targetPose.rotation) > .5f;
            targetPose = accepted;
        }

        public void OnDrop(XRGrabInteractable grabInteractable, DropEventArgs args)
        {
            // XRI invokes this once per registration list; this transformer is in both.
            if (!IsBlocked || restoreThrow) return;
            previousThrow = grabInteractable.throwOnDetach;
            restoreThrow = true;
            grabInteractable.throwOnDetach = false;
            if (!body.isKinematic)
            {
                body.velocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
        }

        public override void OnUnlink(XRGrabInteractable grabInteractable)
        {
            RestoreThrow(grabInteractable);
            base.OnUnlink(grabInteractable);
        }

        void OnCollisionEnter(Collision collision)
        {
            var speed = collision.relativeVelocity.magnitude;
            if (speed < .65f || Time.unscaledTime < nextImpact || collision.contactCount == 0) return;
            nextImpact = Time.unscaledTime + .13f;
            var cue = GetComponent<CookableIngredient>() != null ? KitchenCue.SoftImpact : KitchenCue.Impact;
            KitchenAudio.Play(gameObject.scene, cue, collision.GetContact(0).point,
                Mathf.Lerp(.16f, .75f, Mathf.InverseLerp(.65f, 3f, speed)));
        }

        void LateUpdate()
        {
            if (body == null || !KitchenRoomBounds.TryGet(gameObject.scene, out var room)) return;
            var centre = body.worldCenterOfMass;
            // A thin prop can rest close enough to the floor that its sample falls inside the bake's shadow bias.
            var sampleHeight = room.FloorHeight + .14f;
            if (!searchedLightRenderers && centre.y < sampleHeight)
            {
                searchedLightRenderers = true;
                foreach (var renderer in GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (renderer.probeAnchor != null || renderer.lightProbeUsage != LightProbeUsage.BlendProbes ||
                        renderer.GetComponentInParent<Rigidbody>(true) != body) continue;
                    if (lightSample == null)
                    {
                        lightSample = new GameObject("Prop light sample").transform;
                        lightSample.SetParent(transform, false);
                    }
                    renderer.probeAnchor = lightSample;
                }
            }
            if (lightSample == null) return;
            centre.y = Mathf.Max(centre.y, sampleHeight);
            if (lightSample.position != centre) lightSample.position = centre;
        }

        void RestoreThrow(XRGrabInteractable grabInteractable)
        {
            if (!restoreThrow) return;
            grabInteractable.throwOnDetach = previousThrow;
            restoreThrow = false;
        }
    }
}
