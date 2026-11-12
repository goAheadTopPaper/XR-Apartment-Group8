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
        [SerializeField] TMP_Text caption;
        readonly RaycastHit[] hits = new RaycastHit[32];
        readonly Collider[] overlaps = new Collider[32];
        XRGrabInteractable grab;
        Rigidbody body;
        BoxCollider paper;
        bool released;

        internal event Action<KitchenFeedbackTarget> Pinned;
        internal bool IsPinned { get; private set; }

        void Awake()
        {
            grab = GetComponent<XRGrabInteractable>();
            body = GetComponent<Rigidbody>();
            paper = GetComponent<BoxCollider>();
            grab.selectExited.AddListener(OnReleased);
            KitchenLocale.Set(caption, "Feedback note");
        }

        void OnReleased(SelectExitEventArgs args)
        {
            released = !args.isCanceled && !grab.isSelected;
        }

        internal bool PlaceInView(Transform head)
        {
            if (grab.isSelected) return true;
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
            if (!released) return;
            released = false;
            if (grab.isSelected || IsPinned) return;

            Physics.SyncTransforms();
            var point = transform.position;
            var nearest = new RaycastHit { distance = .085f };
            CheckSurface(point, transform.forward, ref nearest);
            CheckSurface(point, -transform.forward, ref nearest);
            CheckSurface(point, transform.up, ref nearest);
            CheckSurface(point, -transform.up, ref nearest);
            CheckSurface(point, transform.right, ref nearest);
            CheckSurface(point, -transform.right, ref nearest);
            if (nearest.collider == null) return;
            var target = nearest.collider.GetComponentInParent<KitchenFeedbackTarget>();
            if (target == null) return;

            var up = Vector3.ProjectOnPlane(transform.up, nearest.normal);
            if (up.sqrMagnitude < .01f) up = Vector3.ProjectOnPlane(transform.forward, nearest.normal);
            var rotation = Quaternion.LookRotation(-nearest.normal, up);
            grab.enabled = false;
            paper.enabled = false;
            body.detectCollisions = false;
            // Interpolation would otherwise write the last world pose back while the door moves.
            body.interpolation = RigidbodyInterpolation.None;
            var position = nearest.point + nearest.normal * .004f;
            transform.SetPositionAndRotation(position, rotation);
            transform.SetParent(nearest.collider.transform, true);
            body.position = position;
            body.rotation = rotation;
            IsPinned = true;
            KitchenLocale.Set(caption, "Draft");
            Pinned?.Invoke(target);
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

        bool Ignore(Collider other) => other.gameObject.scene != gameObject.scene ||
            other.attachedRigidbody == body || other.GetComponentInParent<XROrigin>() != null;

        internal void MarkSaved()
        {
            KitchenLocale.Set(caption, "Saved feedback");
            Pinned = null;
            enabled = false;
        }
    }
}
