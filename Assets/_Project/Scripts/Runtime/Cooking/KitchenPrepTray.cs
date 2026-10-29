using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using XRApartment.Data;

namespace XRApartment.Runtime
{
    /// Diced food stays physical on a resting board/bowl. On pickup it is carried
    /// with the container, then released to gravity when tipped or put down.
    [RequireComponent(typeof(XRGrabInteractable))]
    public sealed class KitchenPrepTray : MonoBehaviour
    {
        sealed class Item
        {
            public CookableIngredient food;
            public Rigidbody body;
            public XRGrabInteractable grab;
            public Transform parent;
            public Collider[] colliders;
            public bool[] enabled;
            public bool gravity, kinematic, grabbable, carried;
        }
        [SerializeField] bool rectangular;
        [SerializeField] float halfWidth = .21f, halfDepth = .17f, bottom = .022f, height = .1f;
        readonly List<Item> items = new List<Item>(24);
        XRGrabInteractable grab;
        Vector3 previousPosition, velocity;
        public int CarriedCount { get; private set; }
        public int TrackedFoodCount => items.Count;
        public void Configure(bool rectangle, float width, float depth, float floor, float captureHeight)
        { rectangular = rectangle; halfWidth = width; halfDepth = depth; bottom = floor; height = captureHeight; }
        void Awake() { grab = GetComponent<XRGrabInteractable>(); previousPosition = transform.position; }
        void OnEnable() { grab.selectEntered.AddListener(PickedUp); grab.selectExited.AddListener(PutDown); }
        void OnDisable() { grab.selectEntered.RemoveListener(PickedUp); grab.selectExited.RemoveListener(PutDown); ResetContents(); }
        void PickedUp(SelectEnterEventArgs args) { foreach (var item in items) Carry(item); }
        void PutDown(SelectExitEventArgs args) => ReleaseAll();
        void OnTriggerEnter(Collider other) => Consider(other);
        void OnTriggerStay(Collider other) => Consider(other);
        void Consider(Collider other)
        {
            var body = other.attachedRigidbody; if (body == null) return;
            var food = body.GetComponent<CookableIngredient>();
            if (food == null || food.IngredientKind != KitchenIngredientKind.Tomato || food.CutForm != KitchenCutForm.Diced) return;
            foreach (var known in items) if (known.food == food) { if (grab.isSelected) Carry(known); return; }
            if (items.Count >= 32) return;
            var colliders = food.GetComponentsInChildren<Collider>(true);
            var item = new Item { food = food, body = body, grab = food.GetComponent<XRGrabInteractable>(),
                colliders = colliders, enabled = new bool[colliders.Length] };
            items.Add(item); if (grab.isSelected) Carry(item);
        }
        bool Inside(Item item)
        {
            if (item.food == null || item.body == null || item.carried || item.body.isKinematic ||
                (item.grab != null && item.grab.isSelected)) return false;
            var p = transform.InverseTransformPoint(item.body.worldCenterOfMass);
            return p.y >= bottom - .01f && p.y <= bottom + height &&
                (rectangular ? Mathf.Abs(p.x) < halfWidth && Mathf.Abs(p.z) < halfDepth :
                    p.x * p.x + p.z * p.z < halfWidth * halfWidth);
        }
        void Carry(Item item)
        {
            if (!Inside(item) || Vector3.Dot(transform.up, Vector3.up) < .5f) return;
            item.parent = item.food.transform.parent; item.gravity = item.body.useGravity;
            item.kinematic = item.body.isKinematic; item.grabbable = item.grab != null && item.grab.enabled;
            if (item.grab != null) item.grab.enabled = false;
            for (var i = 0; i < item.colliders.Length; i++) { item.enabled[i] = item.colliders[i].enabled; item.colliders[i].enabled = false; }
            item.body.velocity = Vector3.zero; item.body.angularVelocity = Vector3.zero;
            item.body.isKinematic = true; item.body.useGravity = false;
            item.food.transform.SetParent(transform, true); item.carried = true; CarriedCount++;
        }
        void FixedUpdate()
        {
            velocity = Vector3.ClampMagnitude((transform.position - previousPosition) / Time.fixedDeltaTime, 2f);
            previousPosition = transform.position;
            if (grab.isSelected && Vector3.Dot(transform.up, Vector3.up) < .45f) ReleaseAll();
        }
        void ReleaseAll()
        {
            foreach (var item in items)
            {
                if (!item.carried || item.food == null) continue;
                item.food.transform.SetParent(item.parent, true);
                item.body.isKinematic = item.kinematic; item.body.useGravity = item.gravity;
                if (!item.kinematic) { item.body.velocity = velocity; item.body.angularVelocity = Vector3.zero; }
                for (var i = 0; i < item.colliders.Length; i++) item.colliders[i].enabled = item.enabled[i];
                if (item.grab != null)
                {
                    item.grab.SetTargetPose(new Pose(item.food.transform.position, item.food.transform.rotation));
                    item.grab.enabled = item.grabbable;
                }
                item.carried = false;
            }
            CarriedCount = 0;
        }
        public void ResetContents() { ReleaseAll(); items.Clear(); velocity = Vector3.zero; previousPosition = transform.position; }
    }
}
