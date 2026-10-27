using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using XRApartment.Data;

namespace XRApartment.Runtime.Core
{
    /// <summary>Stable identity for a task item, access target or spatial zone.</summary>
    [DisallowMultipleComponent]
    public sealed class TaskEvidenceIdentity : MonoBehaviour
    {
        [SerializeField] private string stableId;
        [SerializeField] private ReviewRole role;
        [SerializeField] private bool taskItem;

        public string StableId => stableId;
        public ReviewRole Role => role;
        public bool IsTaskItem => taskItem;
        public Collider Sensor => GetComponent<Collider>();
        public Rigidbody Body => GetComponent<Rigidbody>();
        public XRGrabInteractable Grab => GetComponent<XRGrabInteractable>();

        /// <summary>A logical storage zone may have one sensor per physical shelf.</summary>
        public bool ContainsPoint(Vector3 point)
        {
            foreach (var sensor in GetComponents<Collider>())
                if (sensor.enabled && sensor.isTrigger && sensor.bounds.Contains(point) &&
                    (sensor.ClosestPoint(point) - point).sqrMagnitude < 0.000001f)
                    return true;
            return false;
        }

        public bool TryGetAccessProgress(out float progress)
        {
            var door = GetComponent<KinematicSwingDoor>();
            if (door != null)
            {
                progress = (float)door.Progress;
                return true;
            }
            var drawer = GetComponent<KinematicLinearDrawer>();
            if (drawer != null)
            {
                progress = (float)drawer.Progress;
                return true;
            }
            progress = 0f;
            return false;
        }
    }
}
