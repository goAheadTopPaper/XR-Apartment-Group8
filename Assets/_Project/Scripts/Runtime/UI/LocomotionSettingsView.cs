using System.Collections.Generic;
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
        [SerializeField] Button language;
        [SerializeField] Button standingHeight, seatedHeight;
        [SerializeField] KitchenLayoutMenu layout;
        [SerializeField] KitchenPauseMenu pauseMenu;
        [SerializeField] Image blink;
        [SerializeField, Min(.1f)] float headDistance = .8f;
        [SerializeField, Min(.02f)] float blinkSeconds = .12f;
        InputAction toggle, layoutToggle;
        KitchenMenuHoldIndicator menuHoldIndicator;
        GameObject auxiliaryPanel;
        Pose panelPose;
        readonly RaycastHit[] panelHits = new RaycastHit[48];
        readonly Dictionary<RectTransform, Vector3> panelScales = new Dictionary<RectTransform, Vector3>();
        Vector3 lastLayoutEyePosition;
        bool layoutFromPauseMenu;
        double menuPressedAt = -1;
        bool menuHoldHandled;
        float blinkRemaining;
        public bool IsOpen => panel.activeSelf || (layout != null && layout.gameObject.activeSelf) ||
            (auxiliaryPanel != null && auxiliaryPanel.activeSelf);
        internal bool ReturnsToPauseFromLayout => layoutFromPauseMenu;
        const string CookingScene = "SCN_Kitchen_Cooking";
        const string ReviewScene = "SCN_Kitchen_Review";
        const double MenuHoldSeconds = .55;

        void Awake()
        {
            if (language != null)
            {
                language.onClick.AddListener(OpenLanguage);
                KitchenLocale.Set(language.GetComponentInChildren<TMP_Text>(true), "Language");
            }
            teleport.onClick.AddListener(ChooseTeleport);
            smooth.onClick.AddListener(ChooseSmooth);
            close.onClick.AddListener(CloseMovementSettings);
            if (enterReview != null) enterReview.onClick.AddListener(EnterReview);
            if (standingHeight != null)
            {
                standingHeight.onClick.AddListener(AlignStandingHeight);
                KitchenLocale.SetLabel(standingHeight, "Align standing height");
            }
            if (seatedHeight != null)
            {
                seatedHeight.onClick.AddListener(AlignSeatedHeight);
                KitchenLocale.SetLabel(seatedHeight, "Align seated height");
            }
            toggle = new InputAction("Kitchen menu", InputActionType.Button);
            toggle.AddBinding("<XRController>{LeftHand}/menuButton");
            toggle.AddBinding("<XRController>{LeftHand}/secondaryButton");
            toggle.AddBinding("<Keyboard>/f8");
            if (pauseMenu == null) toggle.performed += OnToggle;
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
            CancelMenuHold();
            layoutToggle?.Disable();
            settings.Changed -= Refresh;
            settings.TeleportProvider.locomotionStarted -= OnTeleport;
            Close();
            if (blink != null) blink.gameObject.SetActive(false);
            blinkRemaining = 0;
        }

        void OnDestroy()
        {
            if (language != null) language.onClick.RemoveListener(OpenLanguage);
            toggle.performed -= OnToggle;
            toggle.Dispose();
            if (layoutToggle != null)
            {
                layoutToggle.performed -= OnLayoutToggle;
                layoutToggle.Dispose();
            }
            teleport.onClick.RemoveListener(ChooseTeleport);
            smooth.onClick.RemoveListener(ChooseSmooth);
            close.onClick.RemoveListener(CloseMovementSettings);
            if (enterReview != null) enterReview.onClick.RemoveListener(EnterReview);
            if (standingHeight != null) standingHeight.onClick.RemoveListener(AlignStandingHeight);
            if (seatedHeight != null) seatedHeight.onClick.RemoveListener(AlignSeatedHeight);
        }

        void OpenLanguage() => LanguageSelectionView.Show();
        void AlignStandingHeight() => AlignViewHeight(false);
        void AlignSeatedHeight() => AlignViewHeight(true);

        void AlignViewHeight(bool seated)
        {
            if (settings.AlignViewHeight(seated)) PlaceInView(panel.transform, true);
        }
        void ChooseTeleport() => settings.SelectTeleport();
        void ChooseSmooth() => settings.SelectSmooth();
        void OnToggle(InputAction.CallbackContext _)
        {
            if (IsOpen) Close();
            else Open();
        }

        void Update()
        {
            if (pauseMenu == null) return;
            if (LanguageSelectionView.IsVisible || !toggle.IsPressed())
            {
                CancelMenuHold();
                return;
            }
            if (menuHoldHandled) return;
            var now = Time.unscaledTimeAsDouble;
            if (menuPressedAt < 0) menuPressedAt = now;
            var progress = (now - menuPressedAt) / MenuHoldSeconds;
            var head = settings.Origin.Camera.transform;
            if (progress < 1.0)
            {
                if (menuHoldIndicator == null) menuHoldIndicator = KitchenMenuHoldIndicator.Create(transform, pauseMenu.SurfaceMaterial);
                menuHoldIndicator.Show(head, (float)progress);
                return;
            }
            menuHoldHandled = true;
            menuHoldIndicator?.Hide();
            pauseMenu.Toggle();
            KitchenAudio.Play(gameObject.scene, KitchenCue.Menu, head.position, .75f);
        }

        void CancelMenuHold()
        {
            menuPressedAt = -1;
            menuHoldHandled = false;
            menuHoldIndicator?.Hide();
        }

        void OnLayoutToggle(InputAction.CallbackContext _)
        {
            if (layout.gameObject.activeSelf) CloseLayout();
            else OpenLayout();
        }

        internal void OpenLayout(bool fromPauseMenu = false)
        {
            if (layout == null || (pauseMenu != null && !pauseMenu.AllowsLayout)) return;
            layoutFromPauseMenu = fromPauseMenu;
            ShowPanel(layout.gameObject);
        }

        internal void CloseLayout()
        {
            if (layoutFromPauseMenu && pauseMenu != null) pauseMenu.Open();
            else Close();
        }

        internal void RefreshLayoutPlacement()
        {
            if (layout != null && layout.isActiveAndEnabled) PlaceInView(layout.transform, false);
        }

        void CloseMovementSettings()
        {
            if (pauseMenu != null) pauseMenu.Open();
            else Close();
        }

        void EnterReview()
        {
            if (!enterReview.isActiveAndEnabled || !enterReview.interactable) return;
            enterReview.interactable = false;
            Close();
            SceneManager.LoadScene(ReviewScene, LoadSceneMode.Single);
        }

        public void Open()
        {
            ShowPanel(panel);
            Refresh();
        }

        internal void OpenAuxiliary(GameObject target, bool recenter = false) => ShowPanel(target, recenter);

        public void Close() => ShowPanel(null);

        void ShowPanel(GameObject target, bool recenter = false)
        {
            if (auxiliaryPanel != null && auxiliaryPanel != target) auxiliaryPanel.SetActive(false);
            if (panel != target) panel.SetActive(false);
            var layoutPanel = layout == null ? null : layout.gameObject;
            if (layoutPanel != null && layoutPanel != target) layoutPanel.SetActive(false);
            auxiliaryPanel = target == panel || target == layoutPanel ? null : target;
            if (target != null)
            {
                PlaceInView(target.transform, recenter);
                target.SetActive(true);
            }
            settings.SetMenuOpen(target != null);
            if (pauseMenu != null) pauseMenu.SetPaused(target != null);
        }

        void PlaceInView(Transform target, bool recenter)
        {
            var view = settings.Origin.Camera;
            var camera = view.transform;
            if (pauseMenu != null)
            {
                if (!pauseMenu.IsPaused || recenter)
                {
                    var up = Mathf.Abs(Vector3.Dot(camera.forward, Vector3.up)) > .98f ? camera.up : Vector3.up;
                    panelPose = new Pose(camera.position + camera.forward * headDistance,
                        Quaternion.LookRotation(camera.forward, up));
                }
                if (target is RectTransform rect)
                {
                    if (!panelScales.TryGetValue(rect, out var scale)) panelScales.Add(rect, scale = rect.localScale);
                    KitchenPanelPlacement.Place(rect, view, settings.transform, panelPose, scale, panelHits);
                }
                else target.SetPositionAndRotation(panelPose.position, panelPose.rotation);
                lastLayoutEyePosition = camera.position;
                return;
            }
            var forward = Vector3.ProjectOnPlane(camera.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .01f) forward = Vector3.forward;
            target.SetPositionAndRotation(camera.position + forward * headDistance, Quaternion.LookRotation(forward));
        }

        void Refresh()
        {
            if (enterReview != null)
                enterReview.gameObject.SetActive(gameObject.scene.name == CookingScene &&
                    Application.CanStreamedLevelBeLoaded(ReviewScene) && (pauseMenu == null || pauseMenu.AllowsFreePlay));
            if (current == null) return;
            var continuous = settings.Mode == KitchenMovementMode.Smooth;
            KitchenLocale.Set(current, continuous ? "Current: Continuous" : "Current: Teleport");
            var help = continuous
                ? "Left stick: walk where you look.\nRight stick: snap turn / teleport."
                : "Push either stick forward to aim.\nRelease to teleport. Right stick: snap turn.";
            KitchenLocale.Set(instructions, help);
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
            if (layout != null && layout.isActiveAndEnabled &&
                (settings.Origin.Camera.transform.position - lastLayoutEyePosition).sqrMagnitude > .0016f)
                RefreshLayoutPlacement();
            if (blinkRemaining <= 0 || blink == null) return;
            blinkRemaining -= Time.unscaledDeltaTime;
            var colour = Color.black;
            colour.a = Mathf.Clamp01(blinkRemaining / blinkSeconds);
            blink.color = colour;
            if (blinkRemaining <= 0) blink.gameObject.SetActive(false);
        }
    }
}
