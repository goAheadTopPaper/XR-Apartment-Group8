using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
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
        [SerializeField] Button enterReview;
        [SerializeField] KitchenLayoutMenu layout;
        [SerializeField] Image blink;
        [SerializeField, Min(.1f)] float headDistance = .8f;
        [SerializeField, Min(.02f)] float blinkSeconds = .12f;
        InputAction toggle, layoutToggle;
        float blinkRemaining;
        public bool IsOpen => panel.activeSelf || (layout != null && layout.gameObject.activeSelf);
        const string CookingScene = "SCN_Kitchen_Cooking";
        const string ReviewScene = "SCN_Kitchen_Review";

        void Awake()
        {
            teleport.onClick.AddListener(ChooseTeleport);
            smooth.onClick.AddListener(ChooseSmooth);
            close.onClick.AddListener(Close);
            if (enterReview != null)
            {
                enterReview.gameObject.SetActive(gameObject.scene.name == CookingScene &&
                    Application.CanStreamedLevelBeLoaded(ReviewScene));
                enterReview.onClick.AddListener(EnterReview);
            }
            toggle = new InputAction("Movement settings", InputActionType.Button);
            toggle.AddBinding("<XRController>{LeftHand}/secondaryButton");
            toggle.AddBinding("<XRController>{LeftHand}/menuButton");
            toggle.AddBinding("<Keyboard>/f8");
            toggle.performed += OnToggle;
            panel.SetActive(false);
            if (layout != null)
            {
                layoutToggle = new InputAction("Layout experiments", InputActionType.Button);
                layoutToggle.AddBinding("<XRController>{RightHand}/secondaryButton");
                layoutToggle.AddBinding("<Keyboard>/f9");
                layoutToggle.performed += OnLayoutToggle;
                layout.gameObject.SetActive(false);
            }
            if (blink != null) blink.gameObject.SetActive(false);
        }

        void OnEnable()
        {
            settings.Changed += Refresh;
            settings.TeleportProvider.locomotionStarted += OnTeleport;
            toggle.Enable();
            layoutToggle?.Enable();
            Refresh();
        }

        void OnDisable()
        {
            toggle.Disable();
            layoutToggle?.Disable();
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
            if (layoutToggle != null)
            {
                layoutToggle.performed -= OnLayoutToggle;
                layoutToggle.Dispose();
            }
            teleport.onClick.RemoveListener(ChooseTeleport);
            smooth.onClick.RemoveListener(ChooseSmooth);
            close.onClick.RemoveListener(Close);
            if (enterReview != null) enterReview.onClick.RemoveListener(EnterReview);
        }

        void ChooseTeleport() => settings.SelectTeleport();
        void ChooseSmooth() => settings.SelectSmooth();
        void OnToggle(InputAction.CallbackContext _) { if (IsOpen) Close(); else Open(); }
        void OnLayoutToggle(InputAction.CallbackContext _)
        {
            if (layout.gameObject.activeSelf) Close();
            else
            {
                panel.SetActive(false);
                PlaceInView(layout.transform);
                layout.gameObject.SetActive(true);
                settings.SetMenuOpen(true);
            }
        }

        void EnterReview()
        {
            if (!enterReview.isActiveAndEnabled || !enterReview.interactable) return;
            enterReview.interactable = false;
            SceneManager.LoadScene(ReviewScene, LoadSceneMode.Single);
            Time.timeScale = 1f;
        }

        public void Open()
        {
            if (layout != null) layout.gameObject.SetActive(false);
            PlaceInView(panel.transform);
            panel.SetActive(true);
            settings.SetMenuOpen(true);
            Refresh();
        }

        public void Close()
        {
            panel.SetActive(false);
            if (layout != null) layout.gameObject.SetActive(false);
            settings.SetMenuOpen(false);
        }

        void PlaceInView(Transform target)
        {
            var camera = settings.Origin.Camera.transform;
            var forward = Vector3.ProjectOnPlane(camera.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .01f) forward = Vector3.forward;
            target.SetPositionAndRotation(camera.position + forward * headDistance, Quaternion.LookRotation(forward));
        }

        void Refresh()
        {
            if (current == null) return;
            var continuous = settings.Mode == KitchenMovementMode.Smooth;
            current.text = continuous ? "Current: Continuous" : "Current: Teleport";
            instructions.text = continuous
                ? "Left stick: walk where you look.\nRight stick: snap turn / teleport."
                : "Push either stick forward to aim.\nRelease to teleport. Right stick: snap turn.";
            if (layout != null) instructions.text += "\nLayout lab: Right B / F9";
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
