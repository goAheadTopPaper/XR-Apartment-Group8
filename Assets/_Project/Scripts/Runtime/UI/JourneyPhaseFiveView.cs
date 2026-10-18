using System.Collections.Generic;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.UI;
using XRApartment.Data;
using XRApartment.Runtime.Core;
using XRApartment.Simulation;

namespace XRApartment.Runtime.UI
{
    // E1 owns the form, its field validation and the draft editor. The shell only keeps a copy
    // while panels are closed, paused, or a save is retried.
    public interface IFeedbackInputView
    {
        void Open(FeedbackRequest request, FeedbackDraft draft);
        FeedbackDraft CaptureDraft();
        void ShowFieldErrors(IReadOnlyDictionary<string, string> errors);
    }

    public interface IComparisonInputView
    {
        void Open(JourneySnapshot snapshot, FinalComparison draft);
        FinalComparison Capture();
        void ShowError(string message);
    }

    /// <summary>Phase-five shell. All state changes go through JourneyRuntimeHost.</summary>
    public sealed class JourneyPhaseFiveView : MonoBehaviour
    {
        [SerializeField] private JourneyRuntimeHost host;
        [SerializeField] private RecoveryMenuView recoveryMenu;
        [SerializeField] private XROrigin xrOrigin;
        [SerializeField] private MonoBehaviour feedbackInput;
        [SerializeField] private MonoBehaviour comparisonInput;
        [SerializeField] private RectTransform feedbackInputSlot, comparisonInputSlot;
        [SerializeField] private GameObject feedbackPanel, comparisonPanel, finishedPanel, noticePanel;
        [SerializeField] private TMP_Text feedbackTitle, feedbackOutcome, feedbackStatus;
        [SerializeField] private TMP_Text comparisonTitle, b1Summary, b2Summary, comparisonStatus;
        [SerializeField] private TMP_Text finishedStatus, saveStatus, conflictStatus, safetyStatus;
        [SerializeField] private Button feedbackOpen, feedbackCancel, feedbackSubmit;
        [SerializeField] private Button skippedContinueButton;
        [SerializeField] private Button comparisonConfirm, saveRetry, safetyPause, safetyExit;
        [SerializeField, Min(0.5f)] private float headDistance = 0.8f;

        private readonly Dictionary<string, FeedbackDraft> drafts = new Dictionary<string, FeedbackDraft>();
        private JourneySnapshot snapshot;
        private FinalComparison comparisonDraft = new FinalComparison();
        private string lastError = "";
        private string activeDraftKey;
        private bool feedbackOpenNow;

        private IFeedbackInputView FeedbackInput => feedbackInput as IFeedbackInputView;
        private IComparisonInputView ComparisonInput => comparisonInput as IComparisonInputView;

        private void Awake()
        {
            Wire(feedbackOpen, OpenFeedback);
            Wire(feedbackCancel, CancelFeedback);
            Wire(feedbackSubmit, SubmitFeedback);
            Wire(skippedContinueButton, SubmitSkippedFeedback);
            Wire(comparisonConfirm, ConfirmComparison);
            Wire(saveRetry, RetrySave);
            Wire(safetyPause, PauseForSafety);
            Wire(safetyExit, SafeExit);
            if (feedbackInput == null && feedbackInputSlot != null)
                foreach (var component in feedbackInputSlot.GetComponentsInChildren<MonoBehaviour>(true))
                    if (component is IFeedbackInputView) { feedbackInput = component; break; }
            if (comparisonInput == null && comparisonInputSlot != null)
                foreach (var component in comparisonInputSlot.GetComponentsInChildren<MonoBehaviour>(true))
                    if (component is IComparisonInputView) { comparisonInput = component; break; }
            if (FeedbackInput != null && feedbackInputSlot != null &&
                feedbackInputSlot.Find("Dependency") != null)
                feedbackInputSlot.Find("Dependency").gameObject.SetActive(false);
            if (ComparisonInput != null && comparisonInputSlot != null &&
                comparisonInputSlot.Find("Dependency") != null)
                comparisonInputSlot.Find("Dependency").gameObject.SetActive(false);
            SetActive(feedbackPanel, false);
            SetActive(comparisonPanel, false);
            SetActive(finishedPanel, false);
            SetActive(noticePanel, false);
        }

        private void OnDestroy()
        {
            Unwire(feedbackOpen, OpenFeedback);
            Unwire(feedbackCancel, CancelFeedback);
            Unwire(feedbackSubmit, SubmitFeedback);
            Unwire(skippedContinueButton, SubmitSkippedFeedback);
            Unwire(comparisonConfirm, ConfirmComparison);
            Unwire(saveRetry, RetrySave);
            Unwire(safetyPause, PauseForSafety);
            Unwire(safetyExit, SafeExit);
        }

        private void LateUpdate()
        {
            if (snapshot == null || xrOrigin == null || xrOrigin.Camera == null) return;
            var camera = xrOrigin.Camera.transform;
            var forward = Vector3.ProjectOnPlane(camera.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            transform.position = camera.position + forward * headDistance;
            transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }

        public void Render(JourneySnapshot current, JourneyErrorCode code, string message)
        {
            var previousState = snapshot == null ? SessionState.Setup : snapshot.State;
            if (snapshot != null && current != null &&
                snapshot.State == SessionState.FeedbackPending && current.State != SessionState.FeedbackPending)
                CaptureFeedback();
            if (snapshot != null && current != null &&
                snapshot.State == SessionState.ComparisonSummary && current.State != SessionState.ComparisonSummary)
                CaptureComparison();
            snapshot = current;
            if (current == null) return;
            if (current.State == SessionState.ComparisonSummary &&
                previousState != SessionState.ComparisonSummary && ComparisonInput != null)
                ComparisonInput.Open(current, comparisonDraft);
            if (code != JourneyErrorCode.None) lastError = code + ": " + message;
            else if (current.Save != SaveStatus.Failed) lastError = "";

            var pending = current.State == SessionState.FeedbackPending;
            var comparing = current.State == SessionState.ComparisonSummary;
            var finished = current.State == SessionState.Finished && current.Save == SaveStatus.Verified;
            if (!pending) feedbackOpenNow = false;
            SetActive(feedbackPanel, pending);
            SetActive(comparisonPanel, comparing);
            SetActive(finishedPanel, finished);
            RenderFeedback(current, pending);
            RenderComparison(current, comparing);
            Label(finishedStatus, current.Outcome == SessionOutcome.Completed
                ? "<size=38><b>Review complete</b></size>\n\nThank you for reviewing both kitchen designs.\nYour feedback has been saved locally and verified."
                : "<size=38><b>Session complete</b></size>\n\nYour session record has been saved locally and verified.");
            RenderNotices(current);
        }

        private void RenderFeedback(JourneySnapshot current, bool pending)
        {
            if (!pending) return;
            var id = current.ActiveBenchmarkId;
            var outcome = current.BenchmarkOutcomes != null && id != null &&
                current.BenchmarkOutcomes.TryGetValue(id, out var value) ? value : TaskOutcome.Unset;
            Label(feedbackTitle, DesignName(id) + " | Feedback");
            Label(feedbackOutcome, "Task result: " + outcome +
                (outcome == TaskOutcome.Skipped ? " | reason: " + (host?.CurrentFeedbackRequest?.OutcomeReason ?? "recorded") :
                 " | Decision, Category, Importance and short explanation required"));
            Label(feedbackStatus, outcome == TaskOutcome.Skipped
                ? current.Save == SaveStatus.Failed
                    ? "Skip feedback is retained. Local save failed; use Retry. " + lastError
                    : current.Save == SaveStatus.Pending
                        ? "Skip feedback submitted. Waiting for local read back verification."
                        : "No rating fields are required for a skipped task. Submit its recorded reason to continue."
                : FeedbackInput == null
                ? "E1 Feedback form and validator pending integration. This shell cannot submit in its place."
                : current.Save == SaveStatus.Failed
                    ? "Feedback is held in this session. Local save failed; use Retry without submitting again. " + lastError
                    : current.Save == SaveStatus.Pending
                        ? "Feedback submitted. Waiting for local read back verification."
                        : feedbackOpenNow ? lastError : "Open feedback to review or edit this design's draft.");
            SetActive(feedbackOpen, outcome != TaskOutcome.Skipped && FeedbackInput != null &&
                !feedbackOpenNow && current.Save != SaveStatus.Pending);
            SetActive(feedbackCancel, outcome != TaskOutcome.Skipped && FeedbackInput != null && feedbackOpenNow);
            SetActive(feedbackSubmit, outcome != TaskOutcome.Skipped && FeedbackInput != null && feedbackOpenNow);
            SetActive(skippedContinueButton, outcome == TaskOutcome.Skipped);
            if (skippedContinueButton != null)
                skippedContinueButton.interactable = !current.IsBusy && current.Save != SaveStatus.Pending &&
                    current.Save != SaveStatus.Failed;
            if (feedbackSubmit != null) feedbackSubmit.interactable = !current.IsBusy &&
                current.Save != SaveStatus.Pending && current.Save != SaveStatus.Failed;
            if (feedbackInputSlot != null) feedbackInputSlot.gameObject.SetActive(feedbackOpenNow);
        }

        private void RenderComparison(JourneySnapshot current, bool comparing)
        {
            if (!comparing) return;
            Label(comparisonTitle, "Final comparison | B1 and B2");
            Label(b1Summary, Summary(current, "B1"));
            Label(b2Summary, Summary(current, "B2"));
            Label(comparisonStatus, ComparisonInput == null
                ? "E1 preference and trade-off input pending integration."
                : current.Save == SaveStatus.Failed
                    ? "Final save failed; use Retry. The comparison remains in memory. " + lastError
                    : current.Save == SaveStatus.Pending
                        ? "Saving final comparison; Finished is locked until read back verification."
                        : lastError);
            if (comparisonInputSlot != null) comparisonInputSlot.gameObject.SetActive(ComparisonInput != null);
            if (comparisonConfirm != null) comparisonConfirm.interactable = ComparisonInput != null &&
                !current.IsBusy && current.Save != SaveStatus.Failed;
        }

        private static string Summary(JourneySnapshot current, string id)
        {
            var outcome = current.BenchmarkOutcomes != null &&
                current.BenchmarkOutcomes.TryGetValue(id, out var value) ? value : TaskOutcome.Unset;
            BenchmarkFeedback feedback = null;
            if (current.Feedback != null)
                foreach (var item in current.Feedback)
                    if (item != null && item.combinationId == id) { feedback = item; break; }
            return DesignName(id) + " | " + outcome + "\n" + (feedback == null ? "No feedback recorded" :
                outcome == TaskOutcome.Skipped ? "Skipped: " + feedback.outcomeReason :
                feedback.decision + " / " + feedback.category + " / " + feedback.importance +
                "\n" + feedback.shortExplanation);
        }

        private static string DesignName(string id)
        {
            switch (id)
            {
                case "B1": return "B1 Proud + F2";
                case "B2": return "B2 Flush + F2";
                default: return id ?? "Unknown design";
            }
        }

        private void RenderNotices(JourneySnapshot current)
        {
            var safety = current.CurrentSafety;
            var hasSafety = safety != null && safety.IsActive;
            ConflictNotice conflict = null;
            if (current.Conflicts != null)
                foreach (var item in current.Conflicts)
                    if (item != null && (item.BenchmarkId == current.ActiveBenchmarkId ||
                        item.BenchmarkId == current.DisplayedBenchmarkId))
                    { conflict = item; break; }
            var showSave = current.Save != SaveStatus.None;
            SetActive(noticePanel, showSave || conflict != null || hasSafety);
            if (noticePanel != null && noticePanel.TryGetComponent<Image>(out var noticeBackground))
                noticeBackground.enabled = conflict != null || hasSafety;
            Label(saveStatus, "Local save: " + current.Save +
                (current.Save == SaveStatus.Failed ? " | " + lastError :
                 current.Save == SaveStatus.Verified ? " | verified" : ""));
            SetActive(saveStatus, showSave);
            if (saveStatus != null) saveStatus.color = current.Save == SaveStatus.Failed
                ? new Color(1f, .7f, .48f) : new Color(.72f, .79f, .84f);
            Label(conflictStatus, conflict == null ? "" : conflict.FeedbackRecorded
                ? "AMBER | Design issue feedback recorded in this session; issue unresolved. " + conflict.Message
                : "YELLOW | Design geometry notice. " + conflict.Message);
            SetActive(conflictStatus, conflict != null);
            if (conflictStatus != null) conflictStatus.color = conflict != null && conflict.FeedbackRecorded
                ? new Color(1f, .58f, .16f) : new Color(1f, .9f, .22f);
            Label(safetyStatus, hasSafety ? "RED | SAFETY | " + safety.Message +
                " | " + safety.SuggestedAction : "");
            SetActive(safetyStatus, hasSafety);
            SetActive(safetyPause, hasSafety);
            SetActive(safetyExit, hasSafety);
            SetActive(saveRetry, current.Save == SaveStatus.Failed &&
                current.State != SessionState.Exited && current.State != SessionState.Finished);
            if (saveRetry != null) saveRetry.interactable = !current.IsBusy;
        }

        private void OpenFeedback()
        {
            if (snapshot == null || snapshot.State != SessionState.FeedbackPending || FeedbackInput == null) return;
            var key = snapshot.SessionId + "/" + snapshot.ActiveBenchmarkId;
            activeDraftKey = key;
            drafts.TryGetValue(key, out var draft);
            var request = host != null ? host.CurrentFeedbackRequest : null;
            if (request == null) return;
            FeedbackInput.Open(request, CopyDraft(draft));
            feedbackOpenNow = true;
            Render(snapshot, JourneyErrorCode.None, "");
        }

        private void CancelFeedback()
        {
            CaptureFeedback();
            feedbackOpenNow = false;
            Render(snapshot, JourneyErrorCode.None, "");
        }

        private void CaptureFeedback()
        {
            if (!feedbackOpenNow || FeedbackInput == null || string.IsNullOrEmpty(activeDraftKey)) return;
            var draft = FeedbackInput.CaptureDraft();
            if (draft != null) drafts[activeDraftKey] = CopyDraft(draft);
        }

        private static FeedbackDraft CopyDraft(FeedbackDraft value)
        {
            if (value == null) return null;
            return new FeedbackDraft
            {
                SessionId = value.SessionId, BenchmarkId = value.BenchmarkId,
                Decision = value.Decision, Category = value.Category,
                Importance = value.Importance, ShortExplanation = value.ShortExplanation,
                SkipReason = value.SkipReason, Experience = value.Experience,
                EverydayImpact = value.EverydayImpact, DiscoveryMode = value.DiscoveryMode
            };
        }

        private void SubmitFeedback()
        {
            if (host == null || snapshot == null || !feedbackOpenNow || FeedbackInput == null ||
                snapshot.State != SessionState.FeedbackPending || snapshot.IsBusy ||
                snapshot.Save == SaveStatus.Failed) return;
            CaptureFeedback();
            drafts.TryGetValue(activeDraftKey, out var draft);
            var result = host.SubmitFeedback(draft);
            if (result.ErrorCode == JourneyErrorCode.InvalidFeedback)
            {
                FeedbackInput.ShowFieldErrors(host.LastFeedbackValidation?.FieldErrors);
            }
        }

        private void SubmitSkippedFeedback()
        {
            if (host == null || snapshot == null || snapshot.State != SessionState.FeedbackPending ||
                snapshot.IsBusy || snapshot.Save == SaveStatus.Pending || snapshot.Save == SaveStatus.Failed ||
                string.IsNullOrEmpty(snapshot.ActiveBenchmarkId) || snapshot.BenchmarkOutcomes == null ||
                !snapshot.BenchmarkOutcomes.TryGetValue(snapshot.ActiveBenchmarkId, out var outcome) ||
                outcome != TaskOutcome.Skipped) return;
            // The stored skip reason is supplied by Host; the configured feedback port still
            // validates it, and Core still owns progression and saving.
            host.SubmitFeedback(new FeedbackDraft());
        }

        private void CaptureComparison()
        {
            if (ComparisonInput == null) return;
            var value = ComparisonInput.Capture();
            if (value != null) comparisonDraft = value;
        }

        private void ConfirmComparison()
        {
            if (host == null || snapshot == null || ComparisonInput == null ||
                snapshot.State != SessionState.ComparisonSummary || snapshot.IsBusy ||
                snapshot.Save == SaveStatus.Failed) return;
            CaptureComparison();
            var preference = comparisonDraft.PreferredBenchmarkId;
            var hasCompleted = false;
            if (snapshot.BenchmarkOutcomes != null)
                foreach (var pair in snapshot.BenchmarkOutcomes)
                    if (pair.Value == TaskOutcome.Completed) hasCompleted = true;
            if (hasCompleted && (string.IsNullOrWhiteSpace(preference) ||
                !snapshot.BenchmarkOutcomes.TryGetValue(preference, out var outcome) ||
                outcome != TaskOutcome.Completed ||
                string.IsNullOrWhiteSpace(comparisonDraft.TradeOffSummary)))
            {
                ComparisonInput.ShowError("Choose a completed design and describe its main trade-off.");
                return;
            }
            if (!hasCompleted && !string.IsNullOrWhiteSpace(preference))
            {
                ComparisonInput.ShowError("Both tasks were skipped; leave preference empty.");
                return;
            }
            var result = host.Submit(new JourneyCommand
            { Type = JourneyCommandType.ConfirmComparison, Comparison = comparisonDraft });
            if (!result.Accepted) ComparisonInput.ShowError(result.Message);
        }

        private void RetrySave()
        {
            if (host != null && snapshot != null && snapshot.Save == SaveStatus.Failed && !snapshot.IsBusy)
                host.Submit(new JourneyCommand { Type = JourneyCommandType.Retry });
        }

        private void PauseForSafety()
        {
            if (host != null && snapshot != null && snapshot.State != SessionState.Paused)
                host.Submit(new JourneyCommand { Type = JourneyCommandType.Pause });
            if (recoveryMenu != null) recoveryMenu.Open();
        }

        private void SafeExit()
        {
            if (recoveryMenu != null) recoveryMenu.RequestExitConfirmation();
        }

        private static void Label(TMP_Text target, string value)
        { if (target != null) target.text = value ?? ""; }
        private static void SetActive(Component target, bool active)
        { if (target != null) target.gameObject.SetActive(active); }
        private static void SetActive(GameObject target, bool active)
        { if (target != null) target.SetActive(active); }
        private static void Wire(Button button, UnityEngine.Events.UnityAction action)
        { if (button != null) button.onClick.AddListener(action); }
        private static void Unwire(Button button, UnityEngine.Events.UnityAction action)
        { if (button != null) button.onClick.RemoveListener(action); }
    }
}
