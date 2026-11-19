using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using XRApartment.Data;

namespace XRApartment.Runtime
{
    /// Animates the hand attached to its tracked controller and signals blocked held props.
    [DisallowMultipleComponent]
    public sealed class ControllerHandPresentation : MonoBehaviour
    {
        static readonly int GripId = Animator.StringToHash("Grip");
        static readonly int TriggerId = Animator.StringToHash("Trigger");
        static readonly int EmissionId = Shader.PropertyToID("_EmissiveColor");

        [SerializeField] HandInteractionProfile profile;
        [SerializeField] NearFarInteractor interactor;
        [SerializeField] Transform solidRoot;
        [SerializeField] Transform ghostRoot;
        [SerializeField] SphereCollider contactProbe;
        [SerializeField] Animator solidAnimator;

        Renderer[] solidRenderers;
        MaterialPropertyBlock properties;
        float grip, trigger;
        int feedbackState = -1;
        CollisionConstrainedGrabTransformer heldProp;
        HapticImpulsePlayer haptics;
        float nextBlockedHaptic;

        public float Grip => grip;
        public float Trigger => trigger;

        void Awake()
        {
            // The solid hand inherits the controller pose from the rig. Disable the older
            // visual-contact components on existing prefabs.
            if (contactProbe != null) contactProbe.enabled = false;
            if (ghostRoot != null) ghostRoot.gameObject.SetActive(false);
            solidRenderers = solidRoot.GetComponentsInChildren<Renderer>(true);
            // Sample the space occupied by the player when a rendered hand reaches across a wall.
            var camera = GetComponentInParent<XROrigin>()?.Camera;
            if (camera != null)
                foreach (var renderer in solidRenderers)
                    if (renderer.probeAnchor == null) renderer.probeAnchor = camera.transform;
            properties = new MaterialPropertyBlock();
            haptics = interactor.GetComponentInParent<HapticImpulsePlayer>();
            interactor.selectEntered.AddListener(OnSelected);
            interactor.selectExited.AddListener(OnReleased);
        }

        void OnDestroy()
        {
            if (interactor == null) return;
            interactor.selectEntered.RemoveListener(OnSelected);
            interactor.selectExited.RemoveListener(OnReleased);
        }

        void OnSelected(SelectEnterEventArgs args)
        {
            heldProp = args.interactableObject.transform.GetComponent<CollisionConstrainedGrabTransformer>();
            nextBlockedHaptic = 0f;
        }

        void OnReleased(SelectExitEventArgs args) => heldProp = null;

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

            var state = holding ? 2 : interactor.hasHover ? 1 : 0;
            if (state == feedbackState) return;
            feedbackState = state;
            properties.SetColor(EmissionId, state == 2 ? profile.HoldingEmission : state == 1 ? profile.HoverEmission : Color.black);
            foreach (var renderer in solidRenderers) renderer.SetPropertyBlock(properties);
        }

        void LateUpdate()
        {
            if (heldProp != null && heldProp.IsBlocked && haptics != null && Time.unscaledTime >= nextBlockedHaptic)
            {
                haptics.SendHapticImpulse(profile.BlockedHapticAmplitude, profile.BlockedHapticDuration);
                nextBlockedHaptic = Time.unscaledTime + profile.BlockedHapticInterval;
            }
        }
    }
}
