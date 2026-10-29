using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using XRApartment.Data;

namespace XRApartment.Runtime
{
    /// Closed doors also block selection when a tracked hand clips through the door.
    [RequireComponent(typeof(XRGrabInteractable))]
    public sealed class KitchenStoredIngredient : MonoBehaviour, IXRSelectFilter, IXRHoverFilter
    {
        [SerializeField] KitchenFridge fridge;
        [SerializeField] KitchenStoredFood food;
        XRGrabInteractable grab;
        bool heldSinceReset;
        public bool WasRetrieved { get; private set; }
        public bool canProcess => isActiveAndEnabled;
        public void Configure(KitchenFridge source, KitchenStoredFood role) { fridge = source; food = role; }
        void Awake() => grab = GetComponent<XRGrabInteractable>();
        void OnEnable()
        {
            grab.selectFilters.Add(this); grab.hoverFilters.Add(this);
            grab.selectEntered.AddListener(Held);
        }
        void OnDisable()
        {
            grab.selectFilters.Remove(this); grab.hoverFilters.Remove(this);
            grab.selectEntered.RemoveListener(Held);
        }
        bool Available => fridge == null || !fridge.Contains(transform.position) || fridge.Accessible;
        public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable) => grab.isSelected || Available;
        public bool Process(IXRHoverInteractor interactor, IXRHoverInteractable interactable) => Available;
        void Held(SelectEnterEventArgs args) => heldSinceReset = true;
        void Update()
        {
            if (WasRetrieved || !heldSinceReset || fridge == null || fridge.Contains(transform.position)) return;
            WasRetrieved = true; fridge.RecordRetrieval(food);
        }
        public void ResetStored() { WasRetrieved = false; heldSinceReset = false; }
    }
}
