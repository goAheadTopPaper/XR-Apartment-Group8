using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using XRApartment.Data.ReviewV2;

namespace XRApartment.Runtime.ReviewV2
{
    [RequireComponent(typeof(XRGrabInteractable), typeof(ReviewSceneIdentity))]
    public sealed class CookingGrabBridge : MonoBehaviour
    {
        public ReviewSessionHost host;
        XRGrabInteractable grab;
        ReviewSceneIdentity identity;
        void Awake() { grab = GetComponent<XRGrabInteractable>(); identity = GetComponent<ReviewSceneIdentity>(); }
        void OnEnable() { grab.firstSelectEntered.AddListener(Grabbed); grab.lastSelectExited.AddListener(Released); }
        void OnDisable() { grab.firstSelectEntered.RemoveListener(Grabbed); grab.lastSelectExited.RemoveListener(Released); }
        void Grabbed(SelectEnterEventArgs _) => host.Observe(InteractionKind.ObjectGrabbed, identity.Id);
        void Released(SelectExitEventArgs args)
        { if (!args.isCanceled) host.Observe(InteractionKind.ObjectReleased, identity.Id); }
    }
}
