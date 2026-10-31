using System;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;

namespace XRApartment.Runtime
{
    public enum KitchenMovementMode { Teleport, Smooth }

    /// <summary>Local player preference; has no ownership of review sessions or task state.</summary>
    [RequireComponent(typeof(XROrigin))]
    [DefaultExecutionOrder(100)]
    public sealed class KitchenLocomotionSettings : MonoBehaviour
    {
        public const string PreferenceKey = "XRApartment.Locomotion.Mode";
        [SerializeField] ContinuousMoveProvider moveProvider;
        [SerializeField] KitchenTeleportationProvider teleportProvider;
        [SerializeField] SnapTurnProvider snapTurnProvider;
        [SerializeField] XRRayInteractor leftTeleportRay, rightTeleportRay;
        // Persistent calls target the existing Starter Assets managers' public property setters.
        // This keeps Runtime independent of the replaceable Samples assembly.
        [SerializeField] UnityEvent<bool> leftSmoothInput = new UnityEvent<bool>();
        [SerializeField] UnityEvent<bool> rightSmoothInput = new UnityEvent<bool>();

        bool started;
        public XROrigin Origin { get; private set; }
        public CharacterController Character { get; private set; }
        public KitchenMovementMode Mode { get; private set; }
        public bool MenuOpen { get; private set; }
        public ContinuousMoveProvider MoveProvider => moveProvider;
        public KitchenTeleportationProvider TeleportProvider => teleportProvider;
        public event Action Changed;

        void Awake()
        {
            Origin = GetComponent<XROrigin>();
            Character = Origin.Origin.GetComponent<CharacterController>();
        }

        void Start()
        {
            started = true;
            ReloadPreference();
        }

        void OnEnable() { if (started) Apply(); }

        public void ReloadPreference()
        {
            var saved = PlayerPrefs.GetInt(PreferenceKey, (int)KitchenMovementMode.Teleport);
            SetMode(saved == (int)KitchenMovementMode.Smooth ? KitchenMovementMode.Smooth : KitchenMovementMode.Teleport, false);
        }

        public void SelectTeleport() => SetMode(KitchenMovementMode.Teleport);
        public void SelectSmooth() => SetMode(KitchenMovementMode.Smooth);

        public void SetMode(KitchenMovementMode mode, bool persist = true)
        {
            if (mode != KitchenMovementMode.Teleport && mode != KitchenMovementMode.Smooth)
                throw new ArgumentOutOfRangeException(nameof(mode));
            CancelAim();
            Mode = mode;
            Apply();
            if (persist)
            {
                PlayerPrefs.SetInt(PreferenceKey, (int)mode);
                PlayerPrefs.Save();
            }
        }

        public void SetMenuOpen(bool open)
        {
            if (MenuOpen == open) return;
            if (open) CancelAim();
            MenuOpen = open;
            Apply();
        }

        void CancelAim()
        {
            if (teleportProvider != null) teleportProvider.CancelPending();
            if (leftTeleportRay != null) leftTeleportRay.gameObject.SetActive(false);
            if (rightTeleportRay != null) rightTeleportRay.gameObject.SetActive(false);
        }

        void Apply()
        {
            var smooth = Mode == KitchenMovementMode.Smooth;
            leftSmoothInput.Invoke(smooth);
            // The right stick retains snap turn and teleport in both modes.
            rightSmoothInput.Invoke(false);
            if (moveProvider != null) moveProvider.enabled = smooth && !MenuOpen;
            if (snapTurnProvider != null) snapTurnProvider.enabled = !MenuOpen;
            if (teleportProvider != null) teleportProvider.enabled = !MenuOpen;
            Changed?.Invoke();
        }
    }
}
