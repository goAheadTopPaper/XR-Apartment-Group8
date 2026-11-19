using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XRApartment.Data.ReviewV2;

namespace XRApartment.Runtime.ReviewV2.UI
{
    public sealed class ReviewGlobalMenuView : MonoBehaviour
    {
        const string MenuId = "global_menu";
        const float ReceiptTimeoutSeconds = 12f;

        [SerializeField] ReviewV2UiSessionAdapter session;
        [SerializeField] ReviewV2UiController uiController;
        [SerializeField] ReviewTaskBookView taskBook;
        [SerializeField] ReviewIntegrationPanel reviewIntegration;
        [SerializeField] GameObject panel, taskBookPanel, helpPanel, confirmationPanel;
        [SerializeField] CanvasGroup menuBody;
        [SerializeField] Button taskBookButton, helpButton, reviewButton, switchSchemeButton;
        [SerializeField] Button pauseResumeButton, recenterButton, resetWorldButton, safeExitButton;
        [SerializeField] Button closeButton, confirmButton, cancelButton, menuOpener;
        [SerializeField] TMP_Text currentSchemeLabel, sessionStatusLabel, feedbackText;
        [SerializeField] TMP_Text pauseResumeLabel, pauseResumeDescription;
        [SerializeField] TMP_Text confirmationTitle, confirmationMessage, confirmLabel;

        IReviewV2UiSession boundSession, subscribedSession;
        bool hasOverride, dispatching, collectingReset, menuWasVisible;
        string sessionId, pendingRequestId;
        float receiptDeadline, pauseDeadline;
        bool? requestedPause;
        CommandType? confirmation;
        Button childReturnFocus;
        readonly List<OperationResult> earlyReceipts = new List<OperationResult>();

        IReviewV2UiSession Session => hasOverride ? boundSession : session;
        public bool IsOpen => isActiveAndEnabled && Visible(panel);

        void OnEnable()
        {
            ConnectButtons(true);
            CloseGroup(false);
            Subscribe();
            OnSnapshot(ReadSnapshot());
        }

        void OnDisable()
        {
            ConnectButtons(false);
            Unsubscribe();
            ResetSession();
        }

        public void BindSession(IReviewV2UiSession source)
        {
            if (!ReferenceEquals(Session, source)) ResetSession();
            hasOverride = true;
            boundSession = source;
            if (!isActiveAndEnabled) return;
            Subscribe();
            OnSnapshot(ReadSnapshot());
        }

        public bool TryOpen()
        {
            if (!isActiveAndEnabled) return false;
            var snapshot = ReadSnapshot();
            OnSnapshot(snapshot);
            if (!InExperience(snapshot) || panel == null || uiController == null) return false;
            if (IsOpen) return true;
            childReturnFocus = null;
            if (!uiController.ShowPanel(MenuId) || !IsOpen) return false;
            SyncVisibility();
            return true;
        }

        public void OpenFromInput() => TryOpen();

        public void Close() => CloseGroup(true);

        void Update()
        {
            var snapshot = ReadSnapshot();
            if (Identity(snapshot) != sessionId || (!InExperience(snapshot) && AnyPanelVisible()))
                OnSnapshot(snapshot);

            if (pendingRequestId != null && Time.unscaledTime >= receiptDeadline)
            {
                pendingRequestId = null;
                SetFeedback("Unconfirmed: reset completion was not reported. Check the world before retrying.");
                Render(snapshot);
            }

            if (requestedPause.HasValue && Time.unscaledTime >= pauseDeadline)
            {
                requestedPause = null;
                SetFeedback("Unconfirmed: the pause state did not change.");
                Render(snapshot);
            }

            SyncVisibility();
        }

        void Subscribe()
        {
            if (ReferenceEquals(subscribedSession, Session)) return;
            Unsubscribe();
            subscribedSession = Session;
            if (subscribedSession == null) return;
            subscribedSession.Changed += OnSnapshot;
            subscribedSession.OperationCompleted += OnOperationCompleted;
        }

        void Unsubscribe()
        {
            if (subscribedSession == null) return;
            subscribedSession.Changed -= OnSnapshot;
            subscribedSession.OperationCompleted -= OnOperationCompleted;
            subscribedSession = null;
        }

        SessionSnapshot ReadSnapshot()
        {
            var source = Session;
            if (source is ReviewV2UiSessionAdapter adapter && (adapter == null || !adapter.isActiveAndEnabled))
                return null;
            return source?.Snapshot;
        }

        static string Identity(SessionSnapshot snapshot) =>
            string.IsNullOrEmpty(snapshot?.SessionId) ? null : snapshot.SessionId;

        static bool InExperience(SessionSnapshot snapshot) =>
            Identity(snapshot) != null && snapshot.Phase == SessionPhase.Experience;

        void OnSnapshot(SessionSnapshot snapshot)
        {
            var id = Identity(snapshot);
            if (id != sessionId || !InExperience(snapshot)) ResetSession();
            sessionId = id;
            if (requestedPause.HasValue && requestedPause.Value == snapshot.IsPaused)
            {
                requestedPause = null;
                SetFeedback("");
                if (!snapshot.IsPaused) CloseGroup(true);
            }
            Render(snapshot);
            SyncVisibility();
        }

        void ResetSession()
        {
            sessionId = null;
            pendingRequestId = null;
            requestedPause = null;
            collectingReset = false;
            earlyReceipts.Clear();
            CloseGroup(false);
            SetFeedback("");
        }

        void Render(SessionSnapshot snapshot)
        {
            bool valid = Identity(snapshot) != null;
            bool available = InExperience(snapshot);
            bool paused = valid && snapshot.IsPaused;
            bool busy = valid && snapshot.IsBusy;
            bool pending = pendingRequestId != null || collectingReset;
            bool ready = available && !busy && !dispatching && !pending;

            SetText(currentSchemeLabel, "Current scheme: " + (valid ? snapshot.SchemeId : "—"));
            SetText(sessionStatusLabel, valid ? snapshot.Phase + (paused ? " · Paused" : "") +
                (busy ? " · Busy" : "") + (snapshot.IsMock ? " · Mock" : "") : "No session");
            SetText(pauseResumeLabel, paused ? "Resume" : "Pause");
            SetText(pauseResumeDescription, paused ? "Return to experience interactions" :
                "Temporarily stop experience interactions");
            SetInteractable(taskBookButton, available && !busy);
            SetInteractable(helpButton, available && !busy);
            SetInteractable(reviewButton, ready && !paused);
            SetInteractable(switchSchemeButton, false);
            SetInteractable(pauseResumeButton, ready && !requestedPause.HasValue);
            SetInteractable(recenterButton, ready);
            SetInteractable(resetWorldButton, ready && !paused);
            SetInteractable(safeExitButton, available && !dispatching);
        }

        void OpenTaskBook()
        {
            if (CanUseMenu() && taskBook != null && taskBook.TryOpenFromMenu())
            {
                childReturnFocus = taskBookButton;
                SyncVisibility();
            }
        }

        void Review()
        {
            if (!CanUseMenu()) return;
            if (reviewIntegration != null) reviewIntegration.RequestReviewFromT2();
            else Send(CommandType.Review);
        }
        void Recenter() => Send(CommandType.Recenter);
        void PauseOrResume()
        {
            if (!requestedPause.HasValue)
                Send(ReadSnapshot()?.IsPaused == true ? CommandType.Resume : CommandType.Pause);
        }
        void AskReset() => AskConfirmation(CommandType.ResetWorld);
        void AskExit() => AskConfirmation(CommandType.SafeExit);

        void AskConfirmation(CommandType action)
        {
            if (!CanUseMenu() || !AllowCommand(ReadSnapshot(), action)) return;
            if (!CheckDraftProtection()) return;
            bool reset = action == CommandType.ResetWorld;
            confirmation = action;
            SetText(confirmationTitle, reset ? "Reset current world?" : "Leave the experience?");
            SetText(confirmationMessage, reset
                ? "World objects in the current scheme will be restored, and activity progress will reset. Saved markers should remain."
                : "Safe Exit ends the experience. This action does not save the session.");
            SetText(confirmLabel, reset ? "Confirm Reset" : "Confirm Exit");
            SetModal(true);
            Focus(cancelButton);
        }

        void Cancel()
        {
            if (confirmation.HasValue) DismissConfirmation(true);
        }

        void Confirm()
        {
            if (!IsOpen || !confirmation.HasValue) return;
            var action = confirmation.Value;
            DismissConfirmation(true);
            if (!CheckDraftProtection()) return;
            Send(action);
        }
        bool CheckDraftProtection()
        {
            if (reviewIntegration == null) return true; // Independent UI fixtures have no T3 scene bridge.
            string reason;
            if (reviewIntegration.PrepareT2DestructiveAction(out reason)) return true;
            SetFeedback(reason);
            return false;
        }

        void DismissConfirmation(bool restoreFocus)
        {
            var action = confirmation;
            confirmation = null;
            ClearFocusIn(confirmationPanel);
            SetModal(false);
            if (restoreFocus && action.HasValue)
                Focus(action == CommandType.ResetWorld ? resetWorldButton : safeExitButton);
        }

        void SetModal(bool show)
        {
            if (confirmationPanel != null) confirmationPanel.SetActive(show);
            if (menuBody == null) return;
            menuBody.interactable = !show;
            menuBody.blocksRaycasts = !show;
        }

        bool CanUseMenu() => IsOpen && !confirmation.HasValue && !dispatching;

        bool AllowCommand(SessionSnapshot snapshot, CommandType command) =>
            InExperience(snapshot) && Identity(snapshot) == sessionId &&
            (command == CommandType.SafeExit ||
             (!snapshot.IsBusy && pendingRequestId == null &&
              (!(command == CommandType.Review || command == CommandType.ResetWorld) || !snapshot.IsPaused)));

        void Send(CommandType command)
        {
            if (!CanUseMenu()) return;
            var source = subscribedSession;
            var before = ReadSnapshot();
            if (source == null || !AllowCommand(before, command))
            {
                if (!InExperience(before) || Identity(before) != sessionId) OnSnapshot(before);
                else SetFeedback(command == CommandType.SafeExit ? "Rejected: InvalidPhase" :
                    before.IsBusy || pendingRequestId != null ? "Rejected: OperationPending" : "Rejected: Paused");
                return;
            }

            bool reset = command == CommandType.ResetWorld;
            dispatching = true;
            collectingReset = reset;
            earlyReceipts.Clear();
            if (reset) SetFeedback("Pending: requesting world reset…");
            Render(before);
            try
            {
                var result = source.Execute(command);
                var after = ReadSnapshot();
                if (!ReferenceEquals(source, subscribedSession) || Identity(before) != Identity(after) ||
                    !InExperience(after)) return;

                if (!result.Accepted)
                {
                    string prefix = command == CommandType.Recenter ? "Recenter declined: " : "Rejected: ";
                    if (command == CommandType.Recenter && (result.Reason == "MockHasNoXR" ||
                        result.Reason == "XRUnavailable" || result.Reason?.StartsWith("XR runtime did not support", StringComparison.Ordinal) == true))
                        prefix = "Unsupported: ";
                    SetFeedback(prefix + result.Reason);
                    return;
                }

                if (reset)
                {
                    if (string.IsNullOrEmpty(result.RequestId))
                    {
                        SetFeedback("Unconfirmed: reset was accepted without a request ID.");
                        return;
                    }

                    pendingRequestId = result.RequestId;
                    receiptDeadline = Time.unscaledTime + ReceiptTimeoutSeconds;
                    SetFeedback("Pending: awaiting world reset completion…");
                    collectingReset = false;
                    foreach (var receipt in earlyReceipts)
                    {
                        if (receipt.RequestId != pendingRequestId) continue;
                        OnOperationCompleted(receipt);
                        break;
                    }
                }
                else if (command == CommandType.Pause || command == CommandType.Resume)
                {
                    bool pause = command == CommandType.Pause;
                    if (after.IsPaused != pause)
                    {
                        requestedPause = pause;
                        pauseDeadline = Time.unscaledTime + ReceiptTimeoutSeconds;
                        SetFeedback("Pending: waiting for the session pause state…");
                    }
                    else
                    {
                        SetFeedback("");
                        if (!pause) CloseGroup(true);
                    }
                }
                else if (command == CommandType.Recenter) SetFeedback("Succeeded: view recentered.");
                else SetFeedback("");
            }
            finally
            {
                dispatching = false;
                collectingReset = false;
                earlyReceipts.Clear();
                OnSnapshot(ReadSnapshot());
            }
        }

        void OnOperationCompleted(OperationResult result)
        {
            var snapshot = ReadSnapshot();
            if (result == null || result.Command != CommandType.ResetWorld || !InExperience(snapshot) ||
                Identity(snapshot) != sessionId) return;

            if (collectingReset)
            {
                earlyReceipts.Add(result);
                return;
            }

            if (pendingRequestId == null || result.RequestId != pendingRequestId) return;
            pendingRequestId = null;
            SetFeedback((result.Succeeded ? "Succeeded: " : "Failed: ") + result.Message);
            Render(snapshot);
        }

        bool AnyPanelVisible() => Visible(panel) || Visible(taskBookPanel) || Visible(helpPanel);
        static bool Visible(GameObject target) => target != null && target.activeInHierarchy;

        void CloseGroup(bool restoreFocus)
        {
            bool wasVisible = AnyPanelVisible();
            DismissConfirmation(false);
            ClearFocusIn(panel);
            ClearFocusIn(taskBookPanel);
            ClearFocusIn(helpPanel);
            if (uiController != null) uiController.HideAll();
            if (panel != null) panel.SetActive(false);
            childReturnFocus = null;
            menuWasVisible = false;
            if (restoreFocus && wasVisible) Focus(menuOpener);
        }

        void SyncVisibility()
        {
            if (IsOpen)
            {
                var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
                var selectable = selected != null ? selected.GetComponent<Selectable>() : null;
                if (selected == null || !selected.activeInHierarchy || !selected.transform.IsChildOf(panel.transform) ||
                    (selectable != null && !selectable.IsInteractable()))
                {
                    if (confirmation.HasValue) Focus(cancelButton);
                    else if (!Focus(childReturnFocus != null ? childReturnFocus : taskBookButton)) Focus(closeButton);
                }
                if (!menuWasVisible) childReturnFocus = null;
            }
            else
            {
                if (confirmation.HasValue) DismissConfirmation(false);
                ClearFocusIn(panel);
            }

            menuWasVisible = IsOpen;
        }

        static void ClearFocusIn(GameObject scope)
        {
            var system = EventSystem.current;
            if (scope != null && system != null && system.currentSelectedGameObject != null &&
                system.currentSelectedGameObject.transform.IsChildOf(scope.transform))
                system.SetSelectedGameObject(null);
        }

        static bool Focus(Button button)
        {
            if (button == null || !button.gameObject.activeInHierarchy || !button.IsInteractable() || EventSystem.current == null)
                return false;
            EventSystem.current.SetSelectedGameObject(button.gameObject);
            return true;
        }

        void SetFeedback(string message) => SetText(feedbackText, message);
        static void SetText(TMP_Text label, string message)
        {
            KitchenLocale.Set(label, message);
        }

        static void SetInteractable(Button button, bool value)
        {
            if (button != null) button.interactable = value;
        }

        void ConnectButtons(bool add)
        {
            Listen(taskBookButton, OpenTaskBook, add);
            Listen(reviewButton, Review, add);
            Listen(pauseResumeButton, PauseOrResume, add);
            Listen(recenterButton, Recenter, add);
            Listen(resetWorldButton, AskReset, add);
            Listen(safeExitButton, AskExit, add);
            Listen(closeButton, Close, add);
            Listen(confirmButton, Confirm, add);
            Listen(cancelButton, Cancel, add);
        }

        static void Listen(Button button, UnityEngine.Events.UnityAction callback, bool add)
        {
            if (button == null) return;
            if (add) button.onClick.AddListener(callback);
            else button.onClick.RemoveListener(callback);
        }
    }
}
