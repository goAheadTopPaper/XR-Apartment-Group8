using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace XRApartment.Runtime
{
    /// <summary>Rejects occupied landings and straight paths through solid furniture or walls.</summary>
    public sealed class KitchenTeleportationProvider : TeleportationProvider
    {
        readonly Collider[] overlaps = new Collider[64];
        readonly RaycastHit[] crossings = new RaycastHit[64];
        KitchenLocomotionSettings settings;
        public bool HasPendingRequest => validRequest;

        protected override void Awake()
        {
            base.Awake();
            settings = GetComponentInParent<KitchenLocomotionSettings>();
        }

        public bool CanTeleportTo(Vector3 destination)
        {
            if (!isActiveAndEnabled || settings == null || settings.MenuOpen || Time.timeScale <= 0 ||
                settings.Origin == null || settings.Origin.Camera == null || settings.Character == null) return false;
            var character = settings.Character;
            var origin = settings.Origin;
            var up = origin.transform.up;
            var scale = character.transform.lossyScale;
            var radius = character.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            var height = Mathf.Max(character.height * Mathf.Abs(scale.y), radius * 2);
            var skin = character.skinWidth * Mathf.Abs(scale.y);
            // The target must be on one of this project's authored floor surfaces.
            if (!Physics.Raycast(destination + up * .05f, -up, out var support, .1f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ||
                support.collider.GetComponent<KitchenTeleportationArea>() == null || Vector3.Dot(support.normal, up) < .9f)
                return false;

            var bottom = destination + up * (radius + skin);
            var top = destination + up * (height - radius + skin);
            var count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, overlaps,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count >= overlaps.Length) return false;
            for (var i = 0; i < count; i++) if (Blocks(overlaps[i])) return false;

            var localHead = origin.Origin.transform.InverseTransformPoint(origin.Camera.transform.position);
            localHead.y = 0;
            var start = origin.Origin.transform.TransformPoint(localHead);
            var delta = destination - start;
            if (delta.sqrMagnitude < .0004f) return true;
            count = Physics.CapsuleCastNonAlloc(start + up * (radius + skin), start + up * (height - radius + skin),
                radius, delta.normalized, crossings, delta.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count >= crossings.Length) return false;
            for (var i = 0; i < count; i++) if (Blocks(crossings[i].collider)) return false;
            return true;
        }

        bool Blocks(Collider obstacle) => obstacle != null &&
            !obstacle.transform.IsChildOf(settings.transform) && obstacle.GetComponent<KitchenTeleportationArea>() == null;

        public override bool QueueTeleportRequest(TeleportRequest request) =>
            CanTeleportTo(request.destinationPosition) && base.QueueTeleportRequest(request);

        public void CancelPending()
        {
            validRequest = false;
            if (locomotionState == LocomotionState.Preparing || locomotionState == LocomotionState.Moving)
                TryEndLocomotion();
        }

        protected override void OnDisable()
        {
            CancelPending();
            base.OnDisable();
        }

        protected override void Update()
        {
            // Recheck after aiming, so a door or object entering the path can cancel a queued request.
            if (validRequest && !CanTeleportTo(currentRequest.destinationPosition)) CancelPending();
            base.Update();
        }
    }
}
