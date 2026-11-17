using System.Linq;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using XRApartment.Data.ReviewV2;

namespace XRApartment.Runtime.ReviewV2
{
    // Physical objects only: UI rays and onboarding teleport remain available.
    public sealed class ReviewWorldInteractionGate : MonoBehaviour, IXRHoverFilter, IXRSelectFilter
    {
        public ReviewSessionHost host;
        XRBaseInteractable[] objects;
        bool blocked;
        bool busyOnly;
        bool Capturing => host.GetComponent<ReviewFeedbackCapture>()?.IsCapturing == true;
        public bool canProcess => isActiveAndEnabled;
        public bool Process(IXRHoverInteractor hand, IXRHoverInteractable target) => !blocked && !Capturing;
        public bool Process(IXRSelectInteractor hand, IXRSelectInteractable target) => (!Capturing || target.interactorsSelecting.Contains(hand)) && (!blocked ||
            (busyOnly && target.interactorsSelecting.Contains(hand)));
        void Awake() => objects = WorldObjects();
        XRBaseInteractable[] WorldObjects() => gameObject.scene.GetRootGameObjects()
            .Concat(host.station.gameObject.scene.GetRootGameObjects()).Distinct()
            .SelectMany(r => r.GetComponentsInChildren<XRBaseInteractable>(true))
            .Where(x => !(x is BaseTeleportationInteractable) && x.GetComponentInParent<XROrigin>(true) == null).ToArray();
        public void RefreshWorld()
        {
            if (objects == null) return;
            foreach (var item in objects) if (item != null) { item.hoverFilters.Remove(this); item.selectFilters.Remove(this); }
            objects = WorldObjects();
            if (!isActiveAndEnabled) return;
            foreach (var item in objects) { item.hoverFilters.Add(this); item.selectFilters.Add(this); }
            Apply(host.Snapshot);
        }
        void OnEnable()
        {
            foreach (var item in objects) { item.hoverFilters.Add(this); item.selectFilters.Add(this); }
            host.Changed += Apply; Apply(host.Snapshot);
        }
        void OnDisable()
        {
            host.Changed -= Apply;
            foreach (var item in objects) if (item != null) { item.hoverFilters.Remove(this); item.selectFilters.Remove(this); }
        }
        void Apply(SessionSnapshot snapshot)
        {
            bool hardBlock = snapshot.Phase == SessionPhase.Setup || snapshot.Phase == SessionPhase.Finished ||
                snapshot.Phase == SessionPhase.Exited || snapshot.IsPaused;
            blocked = hardBlock || snapshot.IsBusy;
            busyOnly = snapshot.IsBusy && !hardBlock;
            // Pending save/preflight blocks new selections, but preserves existing hands until
            // the world transaction commits. Reset and successful unload cancel their own holds.
            if (!hardBlock) return;
            foreach (var item in objects)
            {
                if (item == null || !item.isSelected || item.interactionManager == null) continue;
                var grab = item as XRGrabInteractable;
                bool throwOnDetach = grab != null && grab.throwOnDetach;
                if (grab != null) grab.throwOnDetach = false;
                item.interactionManager.CancelInteractableSelection((IXRSelectInteractable)item);
                if (grab != null)
                {
                    grab.ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase.Late);
                    if (grab.TryGetComponent<Rigidbody>(out var body) && !body.isKinematic)
                    { body.velocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
                    grab.throwOnDetach = throwOnDetach;
                }
            }
        }
    }
}
