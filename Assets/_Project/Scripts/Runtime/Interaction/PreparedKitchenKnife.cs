using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace XRApartment.Runtime
{
    /// Attach to the blade trigger, with an explicitly authored tip and grab reference.
    public sealed class PreparedKitchenKnife : MonoBehaviour
    {
        [SerializeField] XRGrabInteractable grab;
        [SerializeField] Transform tip;
        Vector3 previous;
        float speed;
        CollisionConstrainedGrabTransformer guard;
        Vector3 tipLocal;
        Vector3 previousActual;
        bool wasHeld;
        [SerializeField, Min(.005f)] float liftToRearm = .035f;
        bool strokeUsed;
        float cutHeight;

        public void Configure(XRGrabInteractable interactable, Transform bladeTip) { grab = interactable; tip = bladeTip; }

        void Awake()
        {
            if (grab != null) guard = grab.GetComponent<CollisionConstrainedGrabTransformer>();
            if (grab != null && tip != null) tipLocal = grab.transform.InverseTransformPoint(tip.position);
        }

        void OnEnable() { if (tip != null) previousActual = previous = tip.position; speed = 0f; wasHeld = false; }

        void FixedUpdate()
        {
            if (tip == null) return;
            var held = grab != null && grab.isSelected;
            var requested = held && guard != null ? guard.RequestedPose.position + guard.RequestedPose.rotation * tipLocal : tip.position;
            speed = held && wasHeld ? Mathf.Max(0f, Mathf.Max(previous.y - requested.y,
                previousActual.y - tip.position.y)) / Time.fixedDeltaTime : 0f;
            if (!held || (strokeUsed && tip.position.y >= cutHeight + liftToRearm)) strokeUsed = false;
            previous = requested; previousActual = tip.position; wasHeld = held;
        }

        void OnTriggerEnter(Collider other) => Cut(other);
        void OnTriggerStay(Collider other) => Cut(other);

        void Cut(Collider other)
        {
            if (grab == null || !grab.isSelected || strokeUsed || other.attachedRigidbody == null) return;
            // Intent supplies cutting speed when the solid blade is stopped at
            // contact. The accepted blade trigger must still touch the food;
            // the tracked ghost can never cut a remote item.
            var prop = other.attachedRigidbody.GetComponent<PreparedKitchenProp>();
            if (prop != null && prop.TryCut(speed)) { strokeUsed = true; cutHeight = tip.position.y; }
        }
    }
}
