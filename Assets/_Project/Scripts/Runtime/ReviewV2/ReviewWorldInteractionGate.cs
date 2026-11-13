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
        public bool canProcess => isActiveAndEnabled;
        public bool Process(IXRHoverInteractor hand, IXRHoverInteractable target) => !blocked;
        public bool Process(IXRSelectInteractor hand, IXRSelectInteractable target) => !blocked;
        void Awake() => objects = gameObject.scene.GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<XRBaseInteractable>(true))
            .Where(x => !(x is BaseTeleportationInteractable) && x.GetComponentInParent<XROrigin>(true) == null).ToArray();
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
            blocked = snapshot.Phase == SessionPhase.Setup || snapshot.Phase == SessionPhase.Finished ||
                snapshot.Phase == SessionPhase.Exited || snapshot.IsPaused || snapshot.IsBusy;
            if (!blocked) return;
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
