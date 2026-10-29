using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using XRApartment.Data;

namespace XRApartment.Runtime
{
    [RequireComponent(typeof(PreparedKitchenProp), typeof(XRGrabInteractable))]
    public sealed class KitchenEgg : MonoBehaviour
    {
        [SerializeField] KitchenCookingProfile profile;
        [SerializeField] LineRenderer fallingContents;
        PreparedKitchenProp shell;
        XRGrabInteractable grab;
        CollisionConstrainedGrabTransformer guard;
        Vector3 previousRequest;
        bool hadSample;
        KitchenFoodVessel dropReceiver;
        float dropUntil;
        public float LastReceivedMillilitres { get; private set; }
        public float LastSpilledMillilitres { get; private set; }

        public void Configure(KitchenCookingProfile settings) => profile = settings;
        public void ConfigureDropVisual(LineRenderer stream) => fallingContents = stream;
        void Awake()
        {
            shell = GetComponent<PreparedKitchenProp>(); grab = GetComponent<XRGrabInteractable>();
            guard = GetComponent<CollisionConstrainedGrabTransformer>();
        }
        void OnEnable() { shell.Split += ReleaseContents; grab.activated.AddListener(Activate); hadSample = false; }
        void OnDisable() { shell.Split -= ReleaseContents; grab.activated.RemoveListener(Activate); if (fallingContents != null) fallingContents.enabled = false; }

        void Activate(ActivateEventArgs args)
        {
            if (profile != null && grab.isSelected && KitchenFoodVessel.ReceiverBelow(transform.position, profile.receiverHeight) != null)
                shell.TryCrack();
        }

        void LateUpdate()
        {
            if (fallingContents != null)
            {
                fallingContents.enabled = Time.time < dropUntil;
                if (fallingContents.enabled)
                {
                    var end = dropReceiver != null ? dropReceiver.transform.TransformPoint(Vector3.up * (dropReceiver.InnerBottom + .01f)) : transform.position + Vector3.down * .3f;
                    fallingContents.positionCount = 12;
                    for (var i = 0; i < 12; i++) fallingContents.SetPosition(i, Vector3.Lerp(transform.position, end, i * i / 121f));
                    fallingContents.widthMultiplier = .007f * Mathf.Clamp01((dropUntil - Time.time) / .2f);
                }
            }
            if (profile == null || guard == null || !grab.isSelected || shell.IsSplit) { hadSample = false; return; }
            var request = guard.RequestedPose.position;
            var speed = hadSample ? Vector3.Distance(request, previousRequest) / Mathf.Max(.001f, Time.deltaTime) : 0;
            previousRequest = request; hadSample = true;
            if (guard.IsBlocked && speed >= profile.eggKnockSpeed &&
                KitchenFoodVessel.ReceiverBelow(transform.position, profile.receiverHeight) != null) shell.TryCrack();
        }

        void ReleaseContents(PreparedKitchenProp prop)
        {
            if (profile == null) return;
            var receiver = KitchenFoodVessel.ReceiverBelow(transform.position, profile.receiverHeight);
            LastReceivedMillilitres = receiver == null ? 0 : receiver.ReceiveEgg(profile.eggMillilitres);
            LastSpilledMillilitres = profile.eggMillilitres - LastReceivedMillilitres;
            dropReceiver = receiver; dropUntil = Time.time + .6f;
            // A single-hand crack gesture opens the two halves to either side;
            // dropping both closed convex halves into the bowl blocks the whisk.
            if (receiver != null)
            {
                var side = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
                if (side.sqrMagnitude < .1f) side = Vector3.right;
                for (var i = 0; i < shell.Pieces.Count; i++)
                    shell.Pieces[i].velocity = side * (i % 2 == 0 ? -.7f : .7f) + Vector3.up * .15f;
            }
        }

        public void ResetEgg()
        {
            LastReceivedMillilitres = LastSpilledMillilitres = 0; hadSample = false; dropUntil = 0; dropReceiver = null;
            if (fallingContents != null) fallingContents.enabled = false;
        }
    }
}
