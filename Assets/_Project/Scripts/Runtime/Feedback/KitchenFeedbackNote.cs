using System;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace XRApartment.Runtime
{
    [DefaultExecutionOrder(250)]
    [RequireComponent(typeof(XRGrabInteractable), typeof(Rigidbody), typeof(BoxCollider))]
    public sealed class KitchenFeedbackNote : MonoBehaviour
    {
        const float PinDistance = .085f;
        const float ThrowSpeed = .55f;
        const float FlightSeconds = 4f;
        [SerializeField] TMP_Text caption;
        readonly RaycastHit[] hits = new RaycastHit[32];
        readonly Collider[] overlaps = new Collider[32];
        XRGrabInteractable grab;
        CollisionConstrainedGrabTransformer sweep;
        Rigidbody body;
        BoxCollider paper;
        Collider contacted;
        Vector3 contactPoint, contactNormal, trackedPosition, trackedVelocity;
        float contactDistance, expiresAt;
        bool released, cancelled, flying, allowThrow;

        internal event Action<KitchenFeedbackTarget> Pinned;
        internal bool IsPinned { get; private set; }

        void Awake()
        {
            grab = GetComponent<XRGrabInteractable>();
            sweep = GetComponent<CollisionConstrainedGrabTransformer>();
            body = GetComponent<Rigidbody>();
            paper = GetComponent<BoxCollider>();
            // XRI restores this non-kinematic state on drop, then applies the
            // tracked release velocity during its late update.
            body.isKinematic = false;
            grab.throwOnDetach = true;
            Park();
            grab.selectEntered.AddListener(OnGrabbed);
            grab.lastSelectExited.AddListener(OnReleased);
            KitchenLocale.Set(caption, "Feedback note");
        }

        void OnDestroy()
        {
            grab.selectEntered.RemoveListener(OnGrabbed);
            grab.lastSelectExited.RemoveListener(OnReleased);
        }

        void OnGrabbed(SelectEnterEventArgs _)
        {
            flying = released = cancelled = false;
            contacted = null;
            trackedPosition = transform.position;
            trackedVelocity = Vector3.zero;
            body.useGravity = false;
            body.constraints = RigidbodyConstraints.None;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

        void OnReleased(SelectExitEventArgs args)
        {
            released = true;
            cancelled = args.isCanceled;
            allowThrow = grab.throwOnDetach && (sweep == null || !sweep.IsBlocked);
        }

        void Park()
        {
            flying = false;
            body.useGravity = false;
            body.constraints = RigidbodyConstraints.FreezeAll;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            if (body.isKinematic) return;
            body.velocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        internal bool PlaceInView(Transform head)
        {
            if (grab.isSelected || flying || released) return true;
            var forward = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .1f) forward = Vector3.forward;
            var facing = Quaternion.LookRotation(forward);
            var near = head.position + forward * .48f - Vector3.up * .19f;
            var sideways = facing * (Vector3.right * .18f);
            var eye = head.position;
            return TryPosition(eye, near, facing) || TryPosition(eye, near + sideways, facing) ||
                TryPosition(eye, near - sideways, facing) || TryPosition(eye, near - forward * .18f, facing);
        }

        bool TryPosition(Vector3 eye, Vector3 position, Quaternion rotation)
        {
            var direction = position - eye;
            var obstruction = new RaycastHit { distance = direction.magnitude };
            CheckSurface(eye, direction.normalized, ref obstruction);
            if (obstruction.collider != null) return false;
            var count = Physics.OverlapBoxNonAlloc(position, paper.size * .5f + Vector3.one * .01f, overlaps,
                rotation, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count == overlaps.Length) return false;
            for (var i = 0; i < count; i++) if (!Ignore(overlaps[i])) return false;
            body.position = position;
            body.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
            grab.SetTargetPose(new Pose(position, rotation));
            return true;
        }

        void LateUpdate()
        {
            if (grab.isSelected)
            {
                var position = transform.position;
                var dt = Time.deltaTime;
                if (dt > .0001f)
                    trackedVelocity = Vector3.Lerp(trackedVelocity, (position - trackedPosition) / dt, 1f - Mathf.Exp(-dt / .045f));
                trackedPosition = position;
                return;
            }
            if (released)
            {
                released = false;
                if (IsPinned) return;
                if (cancelled) { Park(); return; }
                // Use XRI's smoothed release, falling back to recent motion of the
                // accepted, collision-constrained paper pose if XRI missed samples.
                var velocity = allowThrow ? body.velocity : Vector3.zero;
                if (allowThrow && velocity.sqrMagnitude < ThrowSpeed * ThrowSpeed &&
                    trackedVelocity.sqrMagnitude >= ThrowSpeed * ThrowSpeed)
                    velocity = Vector3.ClampMagnitude(trackedVelocity, grab.maxLinearVelocityDelta);
                if (velocity.sqrMagnitude < ThrowSpeed * ThrowSpeed && TryPinNearby()) return;
                body.constraints = RigidbodyConstraints.None;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                body.velocity = velocity;
                body.useGravity = true;
                expiresAt = Time.time + FlightSeconds;
                flying = true;
                return;
            }
            if (!flying) return;
            if (contacted != null)
            {
                if (!TryPin(contacted, contactPoint, contactNormal)) Destroy(gameObject);
            }
            else if (Time.time >= expiresAt) Destroy(gameObject);
        }

        void OnCollisionEnter(Collision collision)
        {
            var other = collision.collider;
            if (!flying || Ignore(other) || collision.contactCount == 0) return;
            var contact = collision.GetContact(0);
            // If one simulation step has multiple contacts, prefer the surface
            // closest to the note instead of depending on callback order.
            var distance = (contact.point - body.position).sqrMagnitude;
            if (contacted != null && distance >= contactDistance) return;
            contacted = other;
            contactPoint = contact.point;
            contactNormal = contact.normal;
            contactDistance = distance;
        }

        bool TryPinNearby()
        {
            Physics.SyncTransforms();
            var point = transform.position;
            var nearest = new RaycastHit { distance = PinDistance };
            CheckSurface(point, transform.forward, ref nearest);
            CheckSurface(point, -transform.forward, ref nearest);
            CheckSurface(point, transform.up, ref nearest);
            CheckSurface(point, -transform.up, ref nearest);
            CheckSurface(point, transform.right, ref nearest);
            CheckSurface(point, -transform.right, ref nearest);
            return nearest.collider != null && TryPin(nearest.collider, nearest.point, nearest.normal);
        }

        bool TryPin(Collider other, Vector3 point, Vector3 normal)
        {
            var target = other.GetComponentInParent<KitchenFeedbackTarget>();
            if (target == null) return false;
            var up = Vector3.ProjectOnPlane(transform.up, normal);
            if (up.sqrMagnitude < .01f) up = Vector3.ProjectOnPlane(transform.forward, normal);
            var rotation = Quaternion.LookRotation(-normal, up);
            Park();
            grab.enabled = false;
            paper.enabled = false;
            body.detectCollisions = false;
            body.isKinematic = true;
            // Interpolation would otherwise write the last world pose back while the door moves.
            body.interpolation = RigidbodyInterpolation.None;
            var position = point + normal * .004f;
            transform.SetPositionAndRotation(position, rotation);
            transform.SetParent(other.transform, true);
            body.position = position;
            body.rotation = rotation;
            IsPinned = true;
            KitchenLocale.Set(caption, "Draft");
            Pinned?.Invoke(target);
            return true;
        }

        void CheckSurface(Vector3 position, Vector3 direction, ref RaycastHit nearest)
        {
            var count = Physics.RaycastNonAlloc(position, direction, hits, nearest.distance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count == hits.Length) return;
            for (var i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (hit.distance < nearest.distance && !Ignore(hit.collider)) nearest = hit;
            }
        }

        bool Ignore(Collider other) => other == null || other.gameObject.scene != gameObject.scene ||
            other.attachedRigidbody == body || other.GetComponentInParent<XROrigin>() != null;

        internal void MarkSaved()
        {
            KitchenLocale.Set(caption, "Saved feedback");
            Pinned = null;
            enabled = false;
        }
    }
}
