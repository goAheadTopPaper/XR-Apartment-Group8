using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;

namespace XRApartment.Runtime.UI
{
    /// <summary>Small independent movement preference panel, usable with tracked UI rays.</summary>
    public sealed class LocomotionSettingsView : MonoBehaviour
    {
        [SerializeField] KitchenLocomotionSettings settings;
        [SerializeField] GameObject panel;
        [SerializeField] TMP_Text current, instructions;
        [SerializeField] Button teleport, smooth, close;
        [SerializeField] Image blink;
        [SerializeField, Min(.1f)] float headDistance = .8f;
        [SerializeField, Min(.02f)] float blinkSeconds = .12f;
        InputAction toggle;
        float blinkRemaining;
        public bool IsOpen => panel != null && panel.activeSelf;

        void Awake()
        {
            teleport.onClick.AddListener(ChooseTeleport);
            smooth.onClick.AddListener(ChooseSmooth);
            close.onClick.AddListener(Close);
            toggle = new InputAction("Movement settings", InputActionType.Button);
            toggle.AddBinding("<XRController>{LeftHand}/secondaryButton");
            toggle.AddBinding("<XRController>{LeftHand}/menuButton");
            toggle.AddBinding("<Keyboard>/f8");
            toggle.performed += OnToggle;
            panel.SetActive(false);
            if (blink != null) blink.gameObject.SetActive(false);
        }

        void OnEnable()
        {
            settings.Changed += Refresh;
            settings.TeleportProvider.locomotionStarted += OnTeleport;
            toggle.Enable();
            Refresh();
        }

        void OnDisable()
        {
            toggle.Disable();
            settings.Changed -= Refresh;
            settings.TeleportProvider.locomotionStarted -= OnTeleport;
            Close();
            if (blink != null) blink.gameObject.SetActive(false);
            blinkRemaining = 0;
        }

        void OnDestroy()
        {
            toggle.performed -= OnToggle;
            toggle.Dispose();
            teleport.onClick.RemoveListener(ChooseTeleport);
            smooth.onClick.RemoveListener(ChooseSmooth);
            close.onClick.RemoveListener(Close);
        }

        void ChooseTeleport() => settings.SelectTeleport();
        void ChooseSmooth() => settings.SelectSmooth();
        void OnToggle(InputAction.CallbackContext _) { if (IsOpen) Close(); else Open(); }

        public void Open()
        {
            var camera = settings.Origin.Camera.transform;
            var forward = Vector3.ProjectOnPlane(camera.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .01f) forward = Vector3.forward;
            panel.transform.SetPositionAndRotation(camera.position + forward * headDistance, Quaternion.LookRotation(forward));
            panel.SetActive(true);
            settings.SetMenuOpen(true);
            Refresh();
        }

        public void Close()
        {
            panel.SetActive(false);
            settings.SetMenuOpen(false);
        }

        void Refresh()
        {
            if (current == null) return;
            var continuous = settings.Mode == KitchenMovementMode.Smooth;
            current.text = continuous ? "Current: Continuous" : "Current: Teleport";
            instructions.text = continuous
                ? "Left stick: walk where you look.\nRight stick: snap turn / teleport."
                : "Push either stick forward to aim.\nRelease to teleport. Right stick: snap turn.";
            teleport.interactable = continuous;
            smooth.interactable = !continuous;
        }

        void OnTeleport(LocomotionProvider _)
        {
            if (blink == null) return;
            blinkRemaining = blinkSeconds;
            blink.color = Color.black;
            blink.gameObject.SetActive(true);
        }

        void LateUpdate()
        {
            if (blinkRemaining <= 0 || blink == null) return;
            blinkRemaining -= Time.unscaledDeltaTime;
            var colour = Color.black;
            colour.a = Mathf.Clamp01(blinkRemaining / blinkSeconds);
            blink.color = colour;
            if (blinkRemaining <= 0) blink.gameObject.SetActive(false);
        }
    }
}
