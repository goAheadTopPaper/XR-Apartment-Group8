using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace XRApartment.Runtime
{
    public sealed class KitchenSpatula : MonoBehaviour
    {
        [SerializeField] XRGrabInteractable grab;
        [SerializeField] Transform tip;
        [SerializeField] KitchenPanContents pan;
        Vector3 previous;
        bool wasHeld;
        public Transform Tip => tip;
        public void Configure(XRGrabInteractable held, Transform bladeTip, KitchenPanContents target)
        { grab = held; tip = bladeTip; pan = target; }
        public void SetPan(KitchenPanContents target) => pan = target;
        void FixedUpdate()
        {
            if (tip == null || grab == null) return;
            var held = grab.isSelected;
            if (held && wasHeld && pan != null && pan.ContainsTip(tip.position))
                pan.Stir(Vector3.Distance(previous, tip.position));
            previous = tip.position; wasHeld = held;
        }
    }
}
