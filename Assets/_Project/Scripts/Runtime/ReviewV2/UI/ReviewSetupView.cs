using TMPro;
using UnityEngine;
using UnityEngine.UI;
using XRApartment.Data.ReviewV2;

namespace XRApartment.Runtime.ReviewV2.UI
{
    /// <summary>UI-01 only: posture calibration, safety acknowledgement, setup transition and safe exit.</summary>
    public sealed class ReviewSetupView : MonoBehaviour
    {
        const string PanelId = "setup";

        [SerializeField] ReviewV2UiSessionAdapter session;
        [SerializeField] ReviewV2UiController uiController;
        [SerializeField] Button seatedButton;
        [SerializeField] Button standingButton;
        [SerializeField] Toggle safetyAcknowledgement;
        [SerializeField] Button beginButton;
        [SerializeField] Button safeExitButton;
        [SerializeField] Button closeButton;
        [SerializeField] TMP_Text selectedPostureText;
        [SerializeField] TMP_Text feedbackText;
        [SerializeField] GameObject seatedSelection;
        [SerializeField] GameObject standingSelection;

        IReviewV2UiSession activeSession;
        bool phaseInitialized;
        SessionPhase lastPhase;
        bool postureSelected;
        string postureLabel;

        void OnEnable()
        {
            if (seatedButton != null) seatedButton.onClick.AddListener(SelectSeated);
            if (standingButton != null) standingButton.onClick.AddListener(SelectStanding);
            if (beginButton != null) beginButton.onClick.AddListener(BeginExperience);
            if (safeExitButton != null) safeExitButton.onClick.AddListener(SafeExit);
            if (closeButton != null) closeButton.onClick.AddListener(CloseSetup);
            if (safetyAcknowledgement != null) safetyAcknowledgement.onValueChanged.AddListener(OnSafetyChanged);

            activeSession = activeSession ?? session;
            AttachSession();
            Render(activeSession != null ? activeSession.Snapshot : null);
            RefreshControls();
        }

        void OnDisable()
        {
            if (seatedButton != null) seatedButton.onClick.RemoveListener(SelectSeated);
            if (standingButton != null) standingButton.onClick.RemoveListener(SelectStanding);
            if (beginButton != null) beginButton.onClick.RemoveListener(BeginExperience);
            if (safeExitButton != null) safeExitButton.onClick.RemoveListener(SafeExit);
            if (closeButton != null) closeButton.onClick.RemoveListener(CloseSetup);
            if (safetyAcknowledgement != null) safetyAcknowledgement.onValueChanged.RemoveListener(OnSafetyChanged);
            DetachSession();
        }

        public void BindSession(IReviewV2UiSession uiSession)
        {
            DetachSession();
            activeSession = uiSession;
            if (isActiveAndEnabled) AttachSession();
            Render(activeSession != null ? activeSession.Snapshot : null);
        }

        public void OpenSetup()
        {
            if (uiController != null) uiController.ShowPanel(PanelId);
        }

        void SelectSeated() => Calibrate(CommandType.CalibrateSeated, "Seated");
        void SelectStanding() => Calibrate(CommandType.CalibrateStanding, "Standing");

        void Calibrate(CommandType command, string label)
        {
            if (activeSession == null) { SetFeedback("Review session is not assigned."); return; }
            var result = activeSession.Execute(command);
            if (!result.Accepted)
            {
                SetFeedback("Posture calibration was not applied: " + result.Reason);
                RefreshControls();
                return;
            }
            postureSelected = true;
            postureLabel = label;
            RenderPosture();
            bool mock = activeSession.Snapshot != null && activeSession.Snapshot.IsMock;
            SetFeedback(label + " view selected." + (mock
                ? " Mock only; no hardware calibration was performed."
                : " Adjust or stop if you feel discomfort."));
            RefreshControls();
        }

        void BeginExperience()
        {
            if (activeSession == null) { SetFeedback("Review session is not assigned."); return; }
            if (!postureSelected) { SetFeedback("Choose seated or standing view first."); return; }
            if (safetyAcknowledgement == null || !safetyAcknowledgement.isOn)
            { SetFeedback("Confirm the safety guidance before continuing."); return; }

            var result = activeSession.Execute(CommandType.CompleteSetup);
            SetFeedback(result.Accepted
                ? "Setup accepted. Waiting for the session state update."
                : "Setup was not accepted: " + result.Reason);
            RefreshControls();
        }

        void SafeExit()
        {
            if (activeSession == null) { SetFeedback("Review session is not assigned."); return; }
            var result = activeSession.Execute(CommandType.SafeExit);
            SetFeedback(result.Accepted ? "Safe exit requested." : "Safe exit was not accepted: " + result.Reason);
            RefreshControls();
        }

        void CloseSetup()
        {
            if (uiController != null) uiController.SetPanelVisible(PanelId, false);
        }

        void OnSafetyChanged(bool _) => RefreshControls();

        void OnSessionChanged(SessionSnapshot snapshot)
        {
            Render(snapshot);
        }

        void AttachSession()
        {
            if (activeSession == null) return;
            activeSession.Changed += OnSessionChanged;
            activeSession.OperationCompleted += OnOperationCompleted;
        }

        void DetachSession()
        {
            if (activeSession == null) return;
            activeSession.Changed -= OnSessionChanged;
            activeSession.OperationCompleted -= OnOperationCompleted;
        }

        void OnOperationCompleted(OperationResult result)
        {
            SetFeedback(result.Succeeded
                ? result.Command + " completed: " + result.Message
                : result.Command + " failed: " + result.Message);
            Render(activeSession != null ? activeSession.Snapshot : null);
        }

        void Render(SessionSnapshot snapshot)
        {
            if (snapshot == null) return;
            if (!phaseInitialized || snapshot.Phase != lastPhase)
            {
                phaseInitialized = true;
                lastPhase = snapshot.Phase;
                if (uiController != null) uiController.SetPanelVisible(PanelId, snapshot.Phase == SessionPhase.Setup);
            }

            RenderPosture();
            RefreshControls(snapshot);
        }

        void RenderPosture()
        {
            KitchenLocale.Set(selectedPostureText, postureSelected
                ? "Selected experience: " + postureLabel
                : "Choose seated or standing view");
            if (seatedSelection != null) seatedSelection.SetActive(postureSelected && postureLabel == "Seated");
            if (standingSelection != null) standingSelection.SetActive(postureSelected && postureLabel == "Standing");
        }

        void RefreshControls() => RefreshControls(activeSession != null ? activeSession.Snapshot : null);

        void RefreshControls(SessionSnapshot snapshot)
        {
            bool setupPhase = snapshot != null && snapshot.Phase == SessionPhase.Setup;
            bool ready = setupPhase && !snapshot.IsPaused && !snapshot.IsBusy;
            if (seatedButton != null) seatedButton.interactable = ready;
            if (standingButton != null) standingButton.interactable = ready;
            if (safetyAcknowledgement != null) safetyAcknowledgement.interactable = ready;
            if (beginButton != null) beginButton.interactable = ready && postureSelected && safetyAcknowledgement != null && safetyAcknowledgement.isOn;
            if (safeExitButton != null) safeExitButton.interactable = snapshot != null && snapshot.Phase != SessionPhase.Finished && snapshot.Phase != SessionPhase.Exited;
            if (closeButton != null) closeButton.interactable = true;
        }

        void SetFeedback(string message)
        {
            KitchenLocale.Set(feedbackText, message ?? string.Empty);
        }
    }
}
