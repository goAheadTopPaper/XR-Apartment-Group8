using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

namespace XRApartment.Runtime
{
    /// Attach to the blade trigger, with an explicitly authored tip and grab reference.
    public sealed class PreparedKitchenKnife : MonoBehaviour
    {
        [SerializeField] XRGrabInteractable grab;
        [SerializeField] Transform tip;
        Vector3 previous;
        float downward, pressure, lastContact;
        PreparedKitchenProp contact;
        float nextBoardSound;
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

        void OnEnable()
        {
            if (tip != null) previousActual = previous = tip.position;
            downward = pressure = 0f;
            contact = null;
            wasHeld = strokeUsed = false;
        }

        void FixedUpdate()
        {
            if (tip == null) return;
            var held = grab != null && grab.isSelected;
            var requested = held && guard != null ? guard.RequestedPose.position + guard.RequestedPose.rotation * tipLocal : tip.position;
            downward = held && wasHeld ? Mathf.Clamp(Mathf.Max(previous.y - requested.y,
                previousActual.y - tip.position.y), 0f, .016f) : 0f;
            if (!held || (strokeUsed && tip.position.y >= cutHeight + liftToRearm))
            {
                strokeUsed = false;
                pressure = 0f;
                contact = null;
            }
            previous = requested; previousActual = tip.position; wasHeld = held;
        }

        void OnTriggerEnter(Collider other) => Cut(other);
        void OnTriggerStay(Collider other) => Cut(other);

        void Cut(Collider other)
        {
            if (grab == null || !grab.isSelected || strokeUsed || downward < .00015f ||
                other.attachedRigidbody == null) return;
            var prop = other.attachedRigidbody.GetComponent<PreparedKitchenProp>();
            if (prop == null)
            {
                if (Time.fixedTime >= nextBoardSound && other.attachedRigidbody.GetComponent<KitchenPrepTray>() != null)
                {
                    nextBoardSound = Time.fixedTime + .22f;
                    KitchenAudio.Play(gameObject.scene, KitchenCue.Board, tip.position, .38f);
                }
                return;
            }
            if (prop.Kind != PreparedPropKind.Slice || prop.IsSplit || lastContact == Time.fixedTime) return;
            var hand = grab.interactorsSelecting[0].transform.GetComponentInParent<HapticImpulsePlayer>();
            if (contact != prop || Time.fixedTime - lastContact > Time.fixedDeltaTime * 1.5f)
            {
                pressure = 0f;
                contact = prop;
                hand?.SendHapticImpulse(.07f, .025f);
            }
            lastContact = Time.fixedTime;
            // The accepted blade must touch the food while the hand advances.
            // The small compression distance lets a slow, steady press cut too.
            pressure += downward;
            if (pressure < .007f || !prop.TryPressCut()) return;
            strokeUsed = true;
            cutHeight = tip.position.y;
            pressure = 0f;
            KitchenAudio.Play(gameObject.scene, KitchenCue.Cut, tip.position, .75f);
            hand?.SendHapticImpulse(.23f, .043f);
        }
    }
}
