using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace XRApartment.Runtime
{
    public sealed class KitchenStirTool : MonoBehaviour
    {
        [SerializeField] XRGrabInteractable grab;
        [SerializeField] Transform tip;
        Vector3 previous;
        bool wasHeld;
        public void Configure(XRGrabInteractable interactable, Transform stirringTip) { grab = interactable; tip = stirringTip; }
        void FixedUpdate()
        {
            if (tip == null || grab == null) return;
            var held = grab.isSelected;
            if (held && wasHeld)
            {
                var vessel = KitchenFoodVessel.StirAt(tip.position);
                if (vessel != null) vessel.Stir(Vector3.Distance(tip.position, previous));
            }
            previous = tip.position; wasHeld = held;
        }
    }
}
