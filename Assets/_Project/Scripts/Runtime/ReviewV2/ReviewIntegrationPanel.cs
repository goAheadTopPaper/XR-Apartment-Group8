using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using XRApartment.Data.ReviewV2;

namespace XRApartment.Runtime.ReviewV2
{
    // Replaceable integration UI. The host is the only session command owner.
    public sealed class ReviewIntegrationPanel : MonoBehaviour
    {
        public ReviewSessionHost host;
        public GameObject panel;
        public TMP_Text status;
        public XRSimpleInteractable resetButton;
        public Button setup, onboarding, review, back, finish, pause, resume, recenter, reset, confirmReset, cancelReset, exit, close, teleport, smooth;
        public Button standing, seated;
        public Button changeScheme, confirmScheme, cancelScheme;
        public Button mark, quickMark, quickReview, quickMenu;
        public GameObject quickBar;
        public TMP_Text quickStatus;
        public ReviewMarkDraftView markDraft;
        public ReviewPlaceholderBoardView reviewBoard;
        public Button brief, quickBrief, briefBack;
        public ReviewActivityBriefView activityBrief;
        [SerializeField] bool openOnStart = true;
        [SerializeField] private bool deferSetupAndOnboardingToT2 = false;
        [SerializeField] bool useT2ExperienceMenu;
        [SerializeField] UI.ReviewGlobalMenuView t2Menu;
        [SerializeField] UI.ReviewV2UiController experienceUi;
        ReviewWorldInteractionGate worldGate;
        double lastToggleTime = double.NegativeInfinity;
        bool T2Visible => useT2ExperienceMenu && experienceUi != null && experienceUi.AnyPanelVisible;
        InputAction toggle;
        Button[] buttons;
        HDAdditionalCameraData menuCamera;
        bool suspendedFxaa, started, restoreOpen;
        bool confirming, briefRequested;
        string schemeConfirmation, confirmationVisit, schemeRequestId;
        bool issuingScheme;
        OperationResult inlineSchemeResult;
        string message = "";
        void Awake()
        {
            worldGate = host.GetComponent<ReviewWorldInteractionGate>();
            ConfigurePresentation();
            setup.onClick.AddListener(() => Send(CommandType.CompleteSetup));
            onboarding.onClick.AddListener(() => Send(CommandType.CompleteOnboarding));
            review.onClick.AddListener(() => Send(CommandType.Review));
            back.onClick.AddListener(() => Send(CommandType.Continue));
            finish.onClick.AddListener(() => Send(CommandType.Finish));
            pause.onClick.AddListener(() => Send(CommandType.Pause));
            resume.onClick.AddListener(() => Send(CommandType.Resume));
            recenter.onClick.AddListener(() => Send(CommandType.Recenter));
            standing.onClick.AddListener(() => Send(CommandType.CalibrateStanding));
            seated.onClick.AddListener(() => Send(CommandType.CalibrateSeated));
            exit.onClick.AddListener(() => Send(CommandType.SafeExit));
            reset.onClick.AddListener(AskReset);
            confirmReset.onClick.AddListener(() => { confirming = false; Send(CommandType.ResetWorld); });
            cancelReset.onClick.AddListener(() => { confirming = false; Render(host.Snapshot); });
            changeScheme.onClick.AddListener(AskScheme);
            confirmScheme.onClick.AddListener(ConfirmScheme);
            cancelScheme.onClick.AddListener(() => { schemeConfirmation = null; Render(host.Snapshot); });
            close.onClick.AddListener(() => SetOpen(false));
            teleport.onClick.AddListener(host.locomotion.SelectTeleport);
            smooth.onClick.AddListener(host.locomotion.SelectSmooth);
            if (mark != null) mark.onClick.AddListener(OpenMark);
            if (quickMark != null) quickMark.onClick.AddListener(OpenMark);
            if (quickReview != null) quickReview.onClick.AddListener(() => Send(CommandType.Review));
            if (quickMenu != null) quickMenu.onClick.AddListener(() => SetOpen(true));
            if (brief != null) brief.onClick.AddListener(OpenBrief);
            if (quickBrief != null) quickBrief.onClick.AddListener(OpenBrief);
            if (briefBack != null) briefBack.onClick.AddListener(() => SetOpen(false));
            if (markDraft != null) markDraft.Closed += DraftClosed;
            toggle = new InputAction("V2 menu", InputActionType.Button);
            toggle.AddBinding("<Keyboard>/u"); toggle.AddBinding("<XRController>{LeftHand}/secondaryButton");
            toggle.AddBinding("<XRController>{LeftHand}/menuButton"); toggle.performed += Toggle;
        }
        void Start()
        {
            started = true;
            var camera = host.locomotion.Origin.Camera;
            panel.GetComponent<Canvas>().worldCamera = camera;
            if (quickBar != null)
            {
                quickBar.GetComponent<Canvas>().worldCamera = camera;
                quickBar.transform.SetParent(camera.transform, false);
                quickBar.transform.localPosition = new Vector3(0, -.22f, .9f);
                quickBar.transform.localRotation = Quaternion.identity;
            }
            SetOpen(openOnStart);
        }
        void OnEnable()
        {
            if (experienceUi != null) experienceUi.VisibilityChanged += ExperienceVisibilityChanged;
            toggle.Enable(); host.Changed += Render; host.OperationCompleted += Completed;
            host.locomotion.Changed += MovementChanged;
            if (resetButton != null) resetButton.selectEntered.AddListener(ResetSelected);
            if (started) SetOpen(restoreOpen);
        }
        void OnDisable()
        {
            if (experienceUi != null) experienceUi.VisibilityChanged -= ExperienceVisibilityChanged;
            if (worldGate != null) worldGate.SetExternalMenuBlocked(false);
            restoreOpen = panel.activeSelf;
            if (markDraft != null) markDraft.Invalidate();
            briefRequested = false;
            if (activityBrief != null) activityBrief.gameObject.SetActive(false);
            panel.SetActive(false);
            if (quickBar != null) quickBar.SetActive(false);
            toggle.Disable(); host.Changed -= Render; host.OperationCompleted -= Completed;
            host.locomotion.Changed -= MovementChanged;
            RestoreAntialiasing();
            if (resetButton != null) resetButton.selectEntered.RemoveListener(ResetSelected);
            host.locomotion.SetMenuOpen(false);
        }
        void OnDestroy()
        {
            if (markDraft != null) markDraft.Closed -= DraftClosed;
            if (quickBar != null && quickBar.transform.parent != transform) Destroy(quickBar);
            toggle.performed -= Toggle; toggle.Dispose();
        }
        void Toggle(InputAction.CallbackContext context)
        {
            if (context.time - lastToggleTime < .1) return;
            lastToggleTime = context.time;
            ToggleMenuInput();
        }
        public void ToggleMenuInput()
        {
            if (useT2ExperienceMenu && host.Snapshot.Phase == SessionPhase.Experience)
            {
                if (DraftOpen) { SetOpen(false); return; }
                if (confirming || schemeConfirmation != null || t2Menu == null) return;
                if (T2Visible) t2Menu.Close();
                else { HideMenuForT2(); t2Menu.TryOpen(); }
                ApplyMovement(host.Snapshot);
                return;
            }
            SetOpen(!panel.activeSelf);
        }
        void ExperienceVisibilityChanged() => ApplyMovement(host.Snapshot);
        void ResetSelected(SelectEnterEventArgs _) => AskReset();
        void AskReset()
        {
            var s = host.Snapshot;
            if (s.Phase != SessionPhase.Experience || s.IsPaused || s.IsBusy || schemeConfirmation != null || DraftOpen) return;
            confirming = true; SetOpen(true); Render(s);
        }
        static string OtherScheme(string scheme) => scheme == SchemeIds.IntegratedKitchen ? SchemeIds.ExternalRefrigerator :
            scheme == SchemeIds.ExternalRefrigerator ? SchemeIds.IntegratedKitchen : null;
        static string SchemeName(string scheme) => scheme == SchemeIds.IntegratedKitchen ? "Scheme A" :
            scheme == SchemeIds.ExternalRefrigerator ? "Scheme B" : scheme;
        bool CanSwitch(SessionSnapshot s) => s.CanReview && OtherScheme(s.SchemeId) != null &&
            host.schemePort is ISchemePort port && port.Supports(OtherScheme(s.SchemeId));
        void AskScheme()
        {
            var s = host.Snapshot;
            if (!CanSwitch(s) || confirming || DraftOpen) return;
            schemeConfirmation = OtherScheme(s.SchemeId); confirmationVisit = s.SchemeVisitId;
            message = ""; SetOpen(true);
        }
        void ConfirmScheme()
        {
            var s = host.Snapshot;
            if (!CanSwitch(s) || schemeConfirmation != OtherScheme(s.SchemeId) || confirmationVisit != s.SchemeVisitId)
            { schemeConfirmation = null; Render(s); return; }
            var target = schemeConfirmation;
            schemeConfirmation = null; message = ""; inlineSchemeResult = null;
            issuingScheme = true;
            CommandResult result;
            try { result = host.Execute(CommandType.ChangeScheme, target); }
            finally { issuingScheme = false; }
            if (!result.Accepted) message = "Cannot switch: " + result.Reason;
            else
            {
                schemeRequestId = result.RequestId;
                // The real scheme port can complete before Execute returns its request ID.
                if (inlineSchemeResult != null && inlineSchemeResult.RequestId == schemeRequestId)
                    CompleteScheme(inlineSchemeResult);
            }
            inlineSchemeResult = null; Render(host.Snapshot);
        }
        void CompleteScheme(OperationResult result)
        {
            schemeRequestId = null;
            message = result.Succeeded ? "Switched to " + SchemeName(host.Snapshot.SchemeId) + ". Activity progress reset." : "Switch failed: " + result.Message;
            Render(host.Snapshot);
        }
        void Completed(OperationResult result)
        {
            if (result.Command == CommandType.ChangeScheme)
            {
                if (issuingScheme) inlineSchemeResult = result;
                else if (result.RequestId == schemeRequestId) CompleteScheme(result);
                return;
            }
            message = result.Message; Render(host.Snapshot);
        }
        bool DefersToT2(SessionSnapshot snapshot) => deferSetupAndOnboardingToT2 &&
            (snapshot.Phase == SessionPhase.Setup || snapshot.Phase == SessionPhase.Onboarding);

        public void RequestReviewFromT2()
        {
            if (!isActiveAndEnabled || host.Snapshot.Phase != SessionPhase.Experience) return;
            if (DraftOpen && !markDraft.RequestClose()) return;
            Send(CommandType.Review);
        }
        public bool PrepareT2DestructiveAction(out string reason)
        {
            reason = "";
            if (markDraft == null)
            { reason = "Marker draft protection is unavailable. Operation blocked."; return false; }
            if (markDraft.IsOpen || markDraft.HasDraft ||
                (markDraft.positionCapture != null && markDraft.positionCapture.IsCapturing))
            {
                if (T2Visible) t2Menu.Close();
                if (markDraft.IsOpen) SetMenuVisible(true);
                markDraft.RequestClose();
                reason = "Resolve the Mark draft, then request this operation again.";
                return false;
            }
            var recorder = host != null ? host.GetComponent<SpatialFeedbackRecorder>() : null;
            if (recorder == null || !recorder.IsReady)
            { reason = "Marker storage is unavailable. Check Review before proceeding."; return false; }
            foreach (var record in recorder.Records)
                if (record.saveStatus != XRApartment.Data.ReviewV2.FeedbackSaveStatus.Verified)
                { reason = "An unverified marker is retained in memory. Retry saving in Review before proceeding."; return false; }
            return true;
        }
        void Send(CommandType command)
        {
            if ((command == CommandType.CompleteSetup || command == CommandType.CompleteOnboarding) &&
                deferSetupAndOnboardingToT2 && DefersToT2(host.Snapshot)) return;

            var result = host.Execute(command); message = result.Accepted ? "" : result.Reason;
            if (result.Accepted && command == CommandType.Review) SetMenuVisible(true);
            if (result.Accepted && (command == CommandType.Recenter || command == CommandType.CalibrateStanding || command == CommandType.CalibrateSeated)) SetOpen(true);
            if (result.Accepted && (command == CommandType.CompleteOnboarding || command == CommandType.Continue || command == CommandType.Resume)) SetOpen(false);
            Render(host.Snapshot);
        }
        public void SetOpen(bool open)
        {
            if (open && useT2ExperienceMenu && host.Snapshot.Phase == SessionPhase.Experience)
            { if (!DraftOpen && t2Menu != null) t2Menu.TryOpen(); return; }
            if (open && deferSetupAndOnboardingToT2 && DefersToT2(host.Snapshot)) return;
            if (DraftOpen && !markDraft.RequestClose()) return;
            if (!open) briefRequested = false;
            SetMenuVisible(open);
        }
        public void OpenBrief()
        {
            var s = host.Snapshot;
            if (!isActiveAndEnabled || !s.CanReview || confirming || schemeConfirmation != null || DraftOpen || activityBrief == null) return;
            briefRequested = true; message = ""; SetMenuVisible(true);
        }
        bool DraftOpen => markDraft != null && markDraft.IsOpen;
        public void OpenMark()
        {
            var s = host.Snapshot;
            if (!isActiveAndEnabled || !s.CanReview || confirming || schemeConfirmation != null || markDraft == null || DraftOpen) return;
            if (T2Visible) t2Menu.Close();
            briefRequested = false; message = "";
            if (useT2ExperienceMenu) { markDraft.Open(s); SetMenuVisible(true); }
            else { SetMenuVisible(true); markDraft.Open(s); }
            Render(s);
        }
        void DraftClosed() => SetMenuVisible(false);
        void SetMenuVisible(bool open)
        {
            if (open && deferSetupAndOnboardingToT2 && DefersToT2(host.Snapshot)) return;
            if (!open) schemeConfirmation = null;
            panel.SetActive(open);
            if (open)
            {
                var camera = host.locomotion.Origin.Camera.transform;
                panel.transform.SetPositionAndRotation(camera.position + camera.forward * .9f, camera.rotation);
                if (useT2ExperienceMenu && host.Snapshot.Phase == SessionPhase.Review)
                {
                    var forward = Vector3.ProjectOnPlane(camera.forward, Vector3.up).normalized;
                    if (forward.sqrMagnitude < .01f) forward = Vector3.forward;
                    panel.transform.SetPositionAndRotation(camera.position + forward * .9f + Vector3.up * .19f,
                        Quaternion.LookRotation(forward, Vector3.up));
                }
                menuCamera = camera.GetComponent<HDAdditionalCameraData>();
                // FXAA smears the thin world-space TMP glyphs. Limit the bypass to this menu;
                // retain every other camera/quality setting and restore FXAA when it closes.
                if (menuCamera != null && menuCamera.antialiasing == HDAdditionalCameraData.AntialiasingMode.FastApproximateAntialiasing)
                {
                    menuCamera.antialiasing = HDAdditionalCameraData.AntialiasingMode.None;
                    suspendedFxaa = true;
                }
            }
            else RestoreAntialiasing();
            Render(host.Snapshot);
            ApplyMovement(host.Snapshot);
        }
        void RestoreAntialiasing()
        {
            if (suspendedFxaa && menuCamera != null && menuCamera.antialiasing == HDAdditionalCameraData.AntialiasingMode.None)
                menuCamera.antialiasing = HDAdditionalCameraData.AntialiasingMode.FastApproximateAntialiasing;
            suspendedFxaa = false;
        }
        void MovementChanged() => Render(host.Snapshot);

        // Also used by authoring to upgrade the existing Prefab without replacing its objects or references.
        public void ConfigurePresentation()
        {
            buttons = new[] { setup, onboarding, review, back, finish, resume, pause, teleport, smooth,
                standing, seated, recenter, mark, brief, briefBack, changeScheme, reset, confirmReset, cancelReset, confirmScheme, cancelScheme, exit, close };
            ((RectTransform)panel.transform).sizeDelta = new Vector2(960, 620);
            panel.transform.localScale = Vector3.one * .001f;
            panel.GetComponent<UnityEngine.UI.Image>().color = new Color(.025f, .045f, .065f, 1);
            foreach (var text in panel.GetComponentsInChildren<TMP_Text>(true))
            {
                text.fontSize = 32; text.enableAutoSizing = false;
                text.color = Color.white; text.raycastTarget = false;
                text.overflowMode = TextOverflowModes.Truncate;
            }
            status.fontSize = 30; status.alignment = TextAlignmentOptions.TopLeft;
            status.richText = true;
            SetLabel(setup, "Confirm Setup"); SetLabel(onboarding, "Start Experience");
            SetLabel(review, "Review"); SetLabel(back, "Continue Exploring");
            SetLabel(finish, "Finish"); SetLabel(exit, "Safe Exit");
            SetLabel(pause, "Pause & Settings"); SetLabel(resume, "Resume");
            SetLabel(teleport, "Teleport"); SetLabel(smooth, "Smooth Movement");
            SetLabel(standing, "Standing"); SetLabel(seated, "Seated");
            SetLabel(recenter, "Recenter"); SetLabel(reset, "Reset World");
            SetLabel(confirmReset, "Confirm Reset"); SetLabel(cancelReset, "Cancel");
            SetLabel(close, "Close Menu");
            SetLabel(changeScheme, "Switch Scheme"); SetLabel(confirmScheme, "Confirm Switch"); SetLabel(cancelScheme, "Cancel");
            SetLabel(mark, "Mark");
            SetLabel(brief, "Activity Book"); SetLabel(briefBack, "Back to Experience");
            foreach (var button in buttons) Show(button, button == setup || button == teleport || button == smooth ||
                button == standing || button == seated || button == recenter || button == exit || button == close);
            if (markDraft != null) markDraft.ConfigurePresentation();
            if (reviewBoard != null) reviewBoard.ConfigurePresentation();
            if (activityBrief != null) activityBrief.ConfigurePresentation();
            if (quickBrief != null)
            {
                var shortcuts = new[] { quickBrief, quickMark, quickReview, quickMenu };
                for (int i = 0; i < shortcuts.Length; i++)
                {
                    var rect = (RectTransform)shortcuts[i].transform;
                    rect.sizeDelta = new Vector2(206, 64); rect.anchoredPosition = new Vector2(-339 + i * 226, -54);
                    var label = shortcuts[i].GetComponentInChildren<TMP_Text>(true);
                    label.fontSize = 28; label.rectTransform.sizeDelta = new Vector2(190, 56);
                }
            }
            if (quickStatus != null) quickStatus.rectTransform.sizeDelta = new Vector2(884, 44);
            KitchenLocale.Set(status, "<size=40><b>SETUP</b></size>\nChoose a movement mode and calibrate seated or standing.\nConfirm when ready.\nU / Left Y: Menu\n<color=#FFCF70>Storage unavailable; data not saved.</color>");
            LayoutButtons();
        }
        static void SetLabel(Button button, string text)
        {
            if (button == null) return;
            var label = button.GetComponentInChildren<TMP_Text>(true); KitchenLocale.Set(label, text);
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(12, 4); label.rectTransform.offsetMax = new Vector2(-12, -4);
            var colors = button.colors;
            colors.normalColor = Color.white; colors.highlightedColor = new Color(.65f, .88f, 1);
            colors.selectedColor = colors.highlightedColor; colors.disabledColor = new Color(.5f, .5f, .5f);
            button.colors = colors;
        }
        void LayoutButtons()
        {
            int count = 0;
            foreach (var button in buttons) if (button != null && button.gameObject.activeSelf) count++;
            bool board = reviewBoard != null && (reviewBoard.gameObject.activeSelf ||
                (useT2ExperienceMenu && host.Snapshot.Phase == SessionPhase.Review && !DraftOpen));
            bool briefVisible = activityBrief != null && activityBrief.gameObject.activeSelf;
            float briefNotice = briefVisible && !string.IsNullOrEmpty(message) ? 64 : 0;
            float height = DraftOpen ? 662 : (briefVisible ? 620 + briefNotice : board ? 550 : 290) + ((count + 1) / 2) * 82;
            ((RectTransform)panel.transform).sizeDelta = new Vector2(960, height);
            var header = status.rectTransform;
            header.anchorMin = header.anchorMax = new Vector2(.5f, 1); header.pivot = new Vector2(.5f, 1);
            header.sizeDelta = new Vector2(884, DraftOpen || briefVisible ? 120 + briefNotice : 238); header.anchoredPosition = new Vector2(0, -24);
            if (briefVisible) ((RectTransform)activityBrief.transform).anchoredPosition = new Vector2(0, -156 - briefNotice);
            int index = 0;
            foreach (var button in buttons)
            {
                if (button == null || !button.gameObject.activeSelf) continue;
                var rect = (RectTransform)button.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1); rect.pivot = new Vector2(.5f, .5f);
                rect.sizeDelta = new Vector2(430, 68);
                rect.anchoredPosition = new Vector2(index % 2 == 0 ? -227 : 227, -(briefVisible ? 630 + briefNotice : board ? 560 : 300) - (index / 2) * 82);
                button.GetComponent<UnityEngine.UI.Image>().color = button == setup || button == onboarding || button == back || button == resume || button == confirmReset || button == confirmScheme
                    ? new Color(.055f, .36f, .46f) : new Color(.10f, .20f, .28f);
                index++;
            }
        }
        static void Show(Button button, bool visible) { if (button != null) button.gameObject.SetActive(visible); }
        void ApplyMovement(SessionSnapshot snapshot)
        {
            // The small shortcut bar is also UI: keep its thin text out of FXAA while visible.
            bool uiVisible = panel.activeSelf || (quickBar != null && quickBar.activeInHierarchy);
            if (started && uiVisible)
            {
                if (menuCamera == null) menuCamera = host.locomotion.Origin.Camera.GetComponent<HDAdditionalCameraData>();
                if (menuCamera != null && menuCamera.antialiasing == HDAdditionalCameraData.AntialiasingMode.FastApproximateAntialiasing)
                { menuCamera.antialiasing = HDAdditionalCameraData.AntialiasingMode.None; suspendedFxaa = true; }
            }
            else if (!uiVisible) RestoreAntialiasing();
            if (worldGate != null) worldGate.SetExternalMenuBlocked(useT2ExperienceMenu && (T2Visible || panel.activeSelf));
            host.locomotion.SetMenuOpen(T2Visible || panel.activeSelf || snapshot.IsPaused || snapshot.IsBusy ||
                snapshot.Phase == SessionPhase.Finished || snapshot.Phase == SessionPhase.Exited);
        }
        void HideMenuForT2()
        {
            var eventSystem = EventSystem.current;
            var selected = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
            if (selected != null && (selected == panel || selected.transform.IsChildOf(panel.transform)))
                eventSystem.SetSelectedGameObject(null);

            // The draft lives under the panel. Hide its parent without discarding the draft.
            if (DraftOpen) markDraft.positionCapture?.Cancel();
            panel.SetActive(false);
            confirming = briefRequested = false;
            schemeConfirmation = null;
            message = "";
            RestoreAntialiasing();
        }

        void Render(SessionSnapshot s)
        {
            if (useT2ExperienceMenu && s.Phase != SessionPhase.Experience && T2Visible) experienceUi.HideAll();
            bool deferredToT2 = DefersToT2(s);
            if (useT2ExperienceMenu && s.Phase == SessionPhase.Experience && !DraftOpen && panel.activeSelf) HideMenuForT2();
            if (deferredToT2 && panel.activeSelf) HideMenuForT2();
            bool ready = !s.IsPaused && !s.IsBusy;
            bool terminal = s.Phase == SessionPhase.Finished || s.Phase == SessionPhase.Exited;
            if (!deferredToT2 && DraftOpen && (!s.CanReview || !markDraft.Matches(s)))
            {
                markDraft.Invalidate(); message = "Context changed. Draft cleared; not submitted or saved.";
            }
            if (!ready || s.Phase != SessionPhase.Experience) confirming = false;
            if (!ready || s.Phase != SessionPhase.Experience || confirmationVisit != s.SchemeVisitId) schemeConfirmation = null;
            bool confirmation = confirming || schemeConfirmation != null;
            if (!s.CanReview || confirmation) briefRequested = false;
            bool showBrief = !deferredToT2 && activityBrief != null && ready && !DraftOpen &&
                (s.Phase == SessionPhase.Onboarding || (s.Phase == SessionPhase.Experience && briefRequested));
            bool settings = !terminal && !s.IsBusy && !confirmation && (s.Phase == SessionPhase.Setup || s.IsPaused);
            Show(setup, !deferredToT2 && ready && s.Phase == SessionPhase.Setup);
            Show(onboarding, !deferredToT2 && ready && s.Phase == SessionPhase.Onboarding);
            Show(review, s.CanReview && !confirmation);
            Show(mark, s.CanReview && !confirmation && markDraft != null);
            Show(brief, s.CanReview && !confirmation && activityBrief != null);
            Show(briefBack, false);
            Show(back, ready && s.Phase == SessionPhase.Review); Show(finish, back.gameObject.activeSelf);
            Show(pause, ready && s.Phase == SessionPhase.Experience && !confirmation);
            Show(resume, s.IsPaused && !s.IsBusy && !terminal);
            Show(teleport, settings); Show(smooth, settings); Show(standing, settings); Show(seated, settings);
            Show(recenter, settings || (ready && s.Phase == SessionPhase.Onboarding));
            Show(reset, ready && s.Phase == SessionPhase.Experience && !confirmation);
            Show(confirmReset, confirming); Show(cancelReset, confirming);
            Show(changeScheme, s.Phase == SessionPhase.Experience && !s.IsPaused && !confirmation && OtherScheme(s.SchemeId) != null);
            changeScheme.interactable = CanSwitch(s);
            KitchenLocale.SetLabel(changeScheme, "Switch to " + SchemeName(OtherScheme(s.SchemeId)) + " / Switch");
            Show(confirmScheme, schemeConfirmation != null); Show(cancelScheme, schemeConfirmation != null);
            confirmScheme.interactable = schemeConfirmation != null && CanSwitch(s);
            Show(exit, !terminal && !confirmation); Show(close, !confirmation);
            if (showBrief)
            {
                foreach (var button in buttons) Show(button, false);
                Show(onboarding, s.Phase == SessionPhase.Onboarding);
                Show(recenter, s.Phase == SessionPhase.Onboarding);
                Show(exit, s.Phase == SessionPhase.Onboarding);
                Show(close, s.Phase == SessionPhase.Onboarding);
                Show(briefBack, s.Phase == SessionPhase.Experience);
                Show(review, s.CanReview);
            }
            if (DraftOpen) foreach (var button in buttons) Show(button, false);
            if (activityBrief != null)
            {
                activityBrief.gameObject.SetActive(showBrief);
                if (showBrief) activityBrief.Present(s);
            }
            if (reviewBoard != null)
            {
                bool showBoard = s.Phase == SessionPhase.Review && ready && !DraftOpen;
                reviewBoard.gameObject.SetActive(showBoard);
                if (showBoard) reviewBoard.Refresh();
            }
            if (quickBar != null)
            {
                quickBar.SetActive(!useT2ExperienceMenu && isActiveAndEnabled && s.CanReview && !panel.activeSelf);
                KitchenLocale.Set(quickStatus, SchemeName(s.SchemeId) + (host.sessionSavePort != null
                    ? " · Free activity order · Mark and Review" : " · Free activity order · storage pending"));
            }
            string title, hint;
            if (confirming) { title = "RESET"; hint = "World objects and activity progress return to their initial state.\nExisting feedback keeps its original scheme."; }
            else if (schemeConfirmation != null) { title = "SWITCH TO " + SchemeName(schemeConfirmation); hint = "The target scheme starts from its initial state and activity progress resets.\nExisting feedback keeps its original scheme."; }
            else if (s.IsBusy) { title = issuingScheme || schemeRequestId != null ? "SWITCHING" : "PLEASE WAIT"; hint = "Please wait for the current operation to finish."; }
            else if (s.IsPaused) { title = "PAUSED"; hint = "Adjust movement or recenter your view."; }
            else switch (s.Phase)
            {
                case SessionPhase.Setup: title = "SETUP"; hint = "Choose a movement mode and calibrate seated or standing.\nConfirm when ready."; break;
                case SessionPhase.Onboarding: title = "GETTING STARTED"; hint = "Explore the kitchen and grab objects; Mark to record, Review to reflect.\nActivity book, Mark and Review shortcuts are below."; break;
                case SessionPhase.Experience: title = "EXPERIENCE"; hint = "Explore the kitchen freely. Enter Review whenever you like."; break;
                case SessionPhase.Review: title = "REVIEW"; hint = "Organise your feedback. Continue exploring or finish."; break;
                case SessionPhase.Finished: title = "FINISHED"; hint = "This session has ended."; break;
                default: title = "EXITED"; hint = "This session was exited. Unverified data is not guaranteed to be saved."; break;
            }
            string storage = s.SaveStatus == SaveStatus.Verified ? (s.Phase == SessionPhase.Finished ? "Save verified." : "Last save verified.") :
                s.SaveStatus == SaveStatus.Pending ? "Saving; waiting for verification." :
                s.SaveStatus == SaveStatus.Failed ? "Save failed; data not verified." :
                (host.sessionSavePort != null ? "Storage connected; session verified on finish or switch." : "Storage unavailable; data not saved.");
            var feedbackRecorder = host.GetComponent<SpatialFeedbackRecorder>();
            if (feedbackRecorder != null && !feedbackRecorder.IsReady) storage = "Marker read or save failed. Retry in Review.";
            string scheme = SchemeName(s.SchemeId);
            string movement = host.locomotion.Mode == KitchenMovementMode.Teleport ? "Teleport" : "Smooth";
            var source = DraftOpen
                ? "<size=40><b>MARK</b></size>\n" + scheme + (host.sessionSavePort != null ? " · U: Back" : " · Note preview only · U: Back")
                : showBrief
                    ? "<size=40><b>ACTIVITY BRIEF</b></size>\n" + scheme + " · " + storage + " · U: Menu" +
                        (string.IsNullOrEmpty(message) ? "" : "\n<size=28><color=#FFCF70>" + message + "</color></size>")
                    : "<size=40><b>" + title + "</b></size>\n" + hint + "\n" + scheme + " · " + movement + " · U / Left Y: Menu\n" +
                        "<color=#FFCF70>" + storage + "</color>" + (string.IsNullOrEmpty(message) ? "" : "\n<size=28>" + message + "</size>");
            KitchenLocale.Set(status, source);
            setup.interactable = !deferredToT2 && ready && s.Phase == SessionPhase.Setup;
            if (DraftOpen || s.Phase == SessionPhase.Review)
            {
                string englishScheme = s.SchemeId == SchemeIds.IntegratedKitchen ? "Scheme A" :
                    s.SchemeId == SchemeIds.ExternalRefrigerator ? "Scheme B" : s.SchemeId;
                string save = s.SaveStatus == SaveStatus.Verified ? "Last save verified." :
                    s.SaveStatus == SaveStatus.Pending ? "Saving; verification pending." :
                    s.SaveStatus == SaveStatus.Failed ? "Save failed; data not verified." :
                    host.sessionSavePort != null ? "Storage connected; verified on finish or switch." : "Storage unavailable; data not saved.";
                if (feedbackRecorder != null && !feedbackRecorder.IsReady) save = "Marker read or save failed. Retry from Review.";
                var t2Source = DraftOpen ? "<size=40><b>MARK</b></size>\n" + englishScheme + " · U: Back" :
                    "<size=40><b>REVIEW</b></size>\nReview your feedback. Continue exploring or finish.\n" +
                    englishScheme + " · " + (host.locomotion.Mode == KitchenMovementMode.Teleport ? "Teleport" : "Smooth Movement") +
                    " · U: Menu\n<color=#FFCF70>" + save + "</color>";
                if (!string.IsNullOrEmpty(message)) t2Source += "\n<size=28>" + message + "</size>";
                KitchenLocale.Set(status, t2Source);
                SetLabel(finish, "Finish Experience");
            }
            onboarding.interactable = !deferredToT2 && ready && s.Phase == SessionPhase.Onboarding;
            review.interactable = s.CanReview;
            back.interactable = finish.interactable = ready && s.Phase == SessionPhase.Review;
            reset.interactable = ready && s.Phase == SessionPhase.Experience;
            confirmReset.interactable = reset.interactable;
            standing.interactable = seated.interactable = recenter.interactable = !terminal && !s.IsBusy;
            pause.interactable = ready && !terminal; resume.interactable = s.IsPaused && !s.IsBusy;
            exit.interactable = !terminal;
            if (useT2ExperienceMenu)
            {
                foreach (var button in buttons) if (button != back && button != finish) Show(button, false);
                if (s.Phase != SessionPhase.Review || DraftOpen) { Show(back, false); Show(finish, false); }
            }
            LayoutButtons();
            ApplyMovement(s);
        }
    }
}
