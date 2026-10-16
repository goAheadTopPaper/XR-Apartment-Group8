using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using XRApartment.Data;

namespace XRApartment.Runtime
{
    /// Adapts Mzh's Grip/Trigger animation and tracked-hand ghost to the existing XRI rig.
    /// Only the rendered palm stops at surfaces; tracked inputs, UI and task logic stay live.
    [DisallowMultipleComponent]
    public sealed class ControllerHandPresentation : MonoBehaviour
    {
        static readonly int GripId = Animator.StringToHash("Grip");
        static readonly int TriggerId = Animator.StringToHash("Trigger");
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] HandInteractionProfile profile;
        [SerializeField] NearFarInteractor interactor;
        [SerializeField] Transform solidRoot;
        [SerializeField] Transform ghostRoot;
        [SerializeField] SphereCollider contactProbe;
        [SerializeField] Animator solidAnimator;
        [SerializeField] Animator ghostAnimator;

        Transform tracked;
        Transform rigRoot;
        Renderer[] solidRenderers;
        Renderer[] ghostRenderers;
        MaterialPropertyBlock properties;
        KinematicPalmSolver solver;
        Vector3 palmPosition;
        Vector3 lastRigPosition;
        Quaternion lastRigRotation;
        float radius, skin, ghostShow, ghostHide, teleportDistance;
        float grip, trigger;
        int feedbackState = -1;

        public float Grip => grip;
        public float Trigger => trigger;
        public bool GhostVisible { get; private set; }
        public Vector3 PalmPosition => palmPosition;
        public float TrackingOffset => Vector3.Distance(palmPosition, tracked.position);

        void Awake()
        {
            tracked = transform.parent;
            rigRoot = transform.root;
            contactProbe.enabled = false;
            contactProbe.center = Vector3.zero;
            radius = profile.Length("hand_palm_radius");
            skin = profile.Length("hand_contact_skin");
            ghostShow = profile.Length("hand_ghost_show_distance");
            ghostHide = profile.Length("hand_ghost_hide_distance");
            teleportDistance = profile.Length("hand_teleport_reset_distance");
            solidRenderers = solidRoot.GetComponentsInChildren<Renderer>(true);
            ghostRenderers = ghostRoot.GetComponentsInChildren<Renderer>(true);
            properties = new MaterialPropertyBlock();
            solver = new KinematicPalmSolver(contactProbe, rigRoot, profile.ContactLayers);
            palmPosition = tracked.position;
            lastRigPosition = rigRoot.position;
            lastRigRotation = rigRoot.rotation;
            SetGhost(false);
        }

        void Update()
        {
            var holding = interactor.hasSelection;
            var targetGrip = Mathf.Clamp01(interactor.selectInput.ReadValue());
            if (holding) targetGrip = Mathf.Max(targetGrip, profile.HoldingGrip);
            else if (interactor.hasHover) targetGrip = Mathf.Max(targetGrip, profile.HoverGrip);
            var step = profile.FingerResponse * Time.unscaledDeltaTime;
            grip = Mathf.MoveTowards(grip, targetGrip, step);
            trigger = Mathf.MoveTowards(trigger, Mathf.Clamp01(interactor.activateInput.ReadValue()), step);
            solidAnimator.SetFloat(GripId, grip);
            solidAnimator.SetFloat(TriggerId, trigger);
            ghostAnimator.SetFloat(GripId, grip);
            ghostAnimator.SetFloat(TriggerId, trigger);

            var state = holding ? 2 : interactor.hasHover ? 1 : 0;
            if (state == feedbackState) return;
            feedbackState = state;
            properties.SetColor(EmissionId, state == 2 ? profile.HoldingEmission : state == 1 ? profile.HoverEmission : Color.black);
            foreach (var renderer in solidRenderers) renderer.SetPropertyBlock(properties);
        }

        void LateUpdate()
        {
            var teleported = Vector3.Distance(rigRoot.position, lastRigPosition) > teleportDistance ||
                             Quaternion.Angle(rigRoot.rotation, lastRigRotation) > 30f;
            var from = teleported ? tracked.position : palmPosition;
            var held = interactor.hasSelection ? interactor.firstInteractableSelected.transform : null;
            palmPosition = solver.Resolve(from, tracked.position, radius, skin, held);
            solidRoot.SetPositionAndRotation(palmPosition, tracked.rotation);
            ghostRoot.SetPositionAndRotation(tracked.position, tracked.rotation);
            var offset = TrackingOffset;
            if (!GhostVisible && offset > ghostShow) SetGhost(true);
            else if (GhostVisible && offset < ghostHide) SetGhost(false);
            lastRigPosition = rigRoot.position;
            lastRigRotation = rigRoot.rotation;
        }

        void SetGhost(bool visible)
        {
            GhostVisible = visible;
            foreach (var renderer in ghostRenderers) renderer.enabled = visible;
        }
    }
}
