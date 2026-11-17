using TMPro;
using UnityEngine;
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
            toggle.AddBinding("<Keyboard>/f8"); toggle.AddBinding("<XRController>{LeftHand}/secondaryButton");
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
            SetOpen(true);
        }
        void OnEnable()
        {
            toggle.Enable(); host.Changed += Render; host.OperationCompleted += Completed;
            host.locomotion.Changed += MovementChanged;
            if (resetButton != null) resetButton.selectEntered.AddListener(ResetSelected);
            if (started) SetOpen(restoreOpen);
        }
        void OnDisable()
        {
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
        void Toggle(InputAction.CallbackContext _) => SetOpen(!panel.activeSelf);
        void ResetSelected(SelectEnterEventArgs _) => AskReset();
        void AskReset()
        {
            var s = host.Snapshot;
            if (s.Phase != SessionPhase.Experience || s.IsPaused || s.IsBusy || schemeConfirmation != null || DraftOpen) return;
            confirming = true; SetOpen(true); Render(s);
        }
        static string OtherScheme(string scheme) => scheme == SchemeIds.IntegratedKitchen ? SchemeIds.ExternalRefrigerator :
            scheme == SchemeIds.ExternalRefrigerator ? SchemeIds.IntegratedKitchen : null;
        static string SchemeName(string scheme) => scheme == SchemeIds.IntegratedKitchen ? "方案 A" :
            scheme == SchemeIds.ExternalRefrigerator ? "方案 B" : scheme;
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
            if (!result.Accepted) message = "无法切换：" + result.Reason;
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
            message = result.Succeeded ? "已切换至" + SchemeName(host.Snapshot.SchemeId) + "，活动进度已重置。" : "切换失败：" + result.Message;
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
        void Send(CommandType command)
        {
            var result = host.Execute(command); message = result.Accepted ? "" : result.Reason;
            if (result.Accepted && command == CommandType.Review) SetMenuVisible(true);
            if (result.Accepted && (command == CommandType.Recenter || command == CommandType.CalibrateStanding || command == CommandType.CalibrateSeated)) SetOpen(true);
            if (result.Accepted && (command == CommandType.CompleteOnboarding || command == CommandType.Continue || command == CommandType.Resume)) SetOpen(false);
            Render(host.Snapshot);
        }
        public void SetOpen(bool open)
        {
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
            briefRequested = false; message = ""; SetMenuVisible(true); markDraft.Open(s); Render(s);
        }
        void DraftClosed() => SetMenuVisible(false);
        void SetMenuVisible(bool open)
        {
            if (!open) schemeConfirmation = null;
            panel.SetActive(open);
            if (open)
            {
                var camera = host.locomotion.Origin.Camera.transform;
                panel.transform.SetPositionAndRotation(camera.position + camera.forward * .9f, camera.rotation);
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
            SetLabel(setup, "确认设置 / Next"); SetLabel(onboarding, "开始体验 / Start");
            SetLabel(review, "进入回顾 / Review"); SetLabel(back, "继续体验 / Continue");
            SetLabel(finish, "结束体验 / Finish"); SetLabel(exit, "中止会话 / Exit");
            SetLabel(pause, "暂停与设置 / Pause"); SetLabel(resume, "恢复体验 / Resume");
            SetLabel(teleport, "传送移动 / Teleport"); SetLabel(smooth, "连续移动 / Smooth");
            SetLabel(standing, "站姿校准 / Standing"); SetLabel(seated, "坐姿校准 / Seated");
            SetLabel(recenter, "重新居中 / Recenter"); SetLabel(reset, "重置世界 / Reset");
            SetLabel(confirmReset, "确认重置 / Confirm"); SetLabel(cancelReset, "取消 / Cancel");
            SetLabel(close, "关闭菜单 / Close");
            SetLabel(changeScheme, "切换方案 / Switch"); SetLabel(confirmScheme, "确认切换 / Confirm"); SetLabel(cancelScheme, "取消 / Cancel");
            SetLabel(mark, "记录标记 / Mark");
            SetLabel(brief, "任务书 / Activities"); SetLabel(briefBack, "返回体验 / Back");
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
            status.text = "<size=40><b>初始设置 / SETUP</b></size>\n选择移动方式，校准站姿或坐姿。\n完成后选择“确认设置”。\nF8 / 左手 Y：菜单\n<color=#FFCF70>存储未接入，数据未保存</color>";
            LayoutButtons();
        }
        static void SetLabel(Button button, string text)
        {
            if (button == null) return;
            var label = button.GetComponentInChildren<TMP_Text>(true); label.text = text;
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
            bool board = reviewBoard != null && reviewBoard.gameObject.activeSelf;
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
            host.locomotion.SetMenuOpen(panel.activeSelf || snapshot.IsPaused || snapshot.IsBusy ||
                snapshot.Phase == SessionPhase.Finished || snapshot.Phase == SessionPhase.Exited);
        }
        void Render(SessionSnapshot s)
        {
            bool ready = !s.IsPaused && !s.IsBusy;
            bool terminal = s.Phase == SessionPhase.Finished || s.Phase == SessionPhase.Exited;
            if (DraftOpen && (!s.CanReview || !markDraft.Matches(s)))
            {
                markDraft.Invalidate(); message = "上下文已变化，草稿已清除；未提交、未保存。";
            }
            if (!ready || s.Phase != SessionPhase.Experience) confirming = false;
            if (!ready || s.Phase != SessionPhase.Experience || confirmationVisit != s.SchemeVisitId) schemeConfirmation = null;
            bool confirmation = confirming || schemeConfirmation != null;
            if (!s.CanReview || confirmation) briefRequested = false;
            bool showBrief = activityBrief != null && ready && !DraftOpen &&
                (s.Phase == SessionPhase.Onboarding || (s.Phase == SessionPhase.Experience && briefRequested));
            bool settings = !terminal && !s.IsBusy && !confirmation && (s.Phase == SessionPhase.Setup || s.IsPaused);
            Show(setup, ready && s.Phase == SessionPhase.Setup);
            Show(onboarding, ready && s.Phase == SessionPhase.Onboarding);
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
            changeScheme.GetComponentInChildren<TMP_Text>(true).text = "切换到" + SchemeName(OtherScheme(s.SchemeId)) + " / Switch";
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
                if (showBoard) reviewBoard.ShowUnavailable();
            }
            if (quickBar != null)
            {
                quickBar.SetActive(isActiveAndEnabled && s.CanReview && !panel.activeSelf);
                quickStatus.text = SchemeName(s.SchemeId) + " · 活动顺序自由 · 标记与存储待接入";
            }
            string title, hint;
            if (confirming) { title = "确认重置 / RESET"; hint = "物品和活动进度将恢复初始状态。\n已有反馈保留原方案归属。"; }
            else if (schemeConfirmation != null) { title = "切换到" + SchemeName(schemeConfirmation) + " / SWITCH"; hint = "目标方案从初始状态开始，活动进度重置。\n已有反馈保留原方案归属。"; }
            else if (s.IsBusy) { title = issuingScheme || schemeRequestId != null ? "正在切换方案 / SWITCHING" : "正在处理 / PLEASE WAIT"; hint = "请等待当前操作完成。"; }
            else if (s.IsPaused) { title = "已暂停 / PAUSED"; hint = "可调整移动方式或校准视角。"; }
            else switch (s.Phase)
            {
                case SessionPhase.Setup: title = "初始设置 / SETUP"; hint = "选择移动方式，校准站姿或坐姿。\n完成后选择“确认设置”。"; break;
                case SessionPhase.Onboarding: title = "操作引导 / GETTING STARTED"; hint = "探索厨房并抓取物品；Mark 记录，Review 回顾。\n体验下方有快捷入口；标记服务待接入。"; break;
                case SessionPhase.Experience: title = "体验菜单 / EXPERIENCE"; hint = "自由探索厨房，随时进入回顾。"; break;
                case SessionPhase.Review: title = "体验回顾 / REVIEW"; hint = "整理体验反馈；可以继续体验或结束。"; break;
                case SessionPhase.Finished: title = "体验结束 / FINISHED"; hint = "本次体验已结束。"; break;
                default: title = "会话已中止 / EXITED"; hint = "本次会话已中止，未验证的数据不保证保存。"; break;
            }
            string storage = s.SaveStatus == SaveStatus.Verified ? "保存已验证" : s.SaveStatus == SaveStatus.Pending ? "正在保存，请等待验证" :
                s.SaveStatus == SaveStatus.Failed ? "保存失败，数据未验证" : "存储未接入，数据未保存";
            string scheme = SchemeName(s.SchemeId);
            string movement = host.locomotion.Mode == KitchenMovementMode.Teleport ? "传送移动" : "连续移动";
            status.text = DraftOpen ? "<size=40><b>记录标记 / MARK</b></size>\n" + scheme + " · 备注仅试填 · F8：返回" :
                showBrief ? "<size=40><b>任务书 / ACTIVITY BRIEF</b></size>\n" + scheme + " · " + storage + " · F8：菜单" +
                    (string.IsNullOrEmpty(message) ? "" : "\n<size=28><color=#FFCF70>" + message + "</color></size>") :
                "<size=40><b>" + title + "</b></size>\n" + hint + "\n" + scheme + " · " + movement + " · F8 / 左手 Y：菜单\n" +
                "<color=#FFCF70>" + storage + "</color>" + (string.IsNullOrEmpty(message) ? "" : "\n<size=28>" + message + "</size>");
            setup.interactable = ready && s.Phase == SessionPhase.Setup;
            onboarding.interactable = ready && s.Phase == SessionPhase.Onboarding;
            review.interactable = s.CanReview;
            back.interactable = finish.interactable = ready && s.Phase == SessionPhase.Review;
            reset.interactable = ready && s.Phase == SessionPhase.Experience;
            confirmReset.interactable = reset.interactable;
            standing.interactable = seated.interactable = recenter.interactable = !terminal && !s.IsBusy;
            pause.interactable = ready && !terminal; resume.interactable = s.IsPaused && !s.IsBusy;
            exit.interactable = !terminal;
            LayoutButtons();
            ApplyMovement(s);
        }
    }
}
