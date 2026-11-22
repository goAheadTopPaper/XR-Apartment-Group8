using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using XRApartment.Data;
using XRApartment.Runtime.Core;
using XRApartment.Simulation;

namespace XRApartment.Runtime.UI
{
    /// <summary>Setup and Orientation presenter; Core owns all journey state.</summary>
    public sealed class JourneyWorldSpaceView : MonoBehaviour, IJourneyView
    {
        [SerializeField] private JourneyRuntimeHost host;
        [SerializeField] private RecoveryMenuView recoveryMenu;
        [SerializeField] private JourneyPhaseFiveView phaseFiveView;
        [SerializeField] private XROrigin xrOrigin;
        [SerializeField] private UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationProvider teleportationProvider;
        [SerializeField] private UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationArea teleportPracticeArea;
        [SerializeField] private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable practiceGrabTarget;
        [SerializeField] private TMP_Text stateText, benchmarkText, progressText, errorText, setupHintText;
        [SerializeField] private TMP_Text pageTitle, pageBody, practiceStatus;
        [SerializeField] private Toggle calibrationConfirmed, practiceConfirmed;
        [SerializeField] private Button completeSetupButton, previousButton, nextButton;
        [SerializeField] private Button standingButton, seatedButton, calibrateButton;
        [SerializeField] private Button lowerEyeButton, raiseEyeButton;
        [SerializeField] private Button selectPracticeButton, resetPracticeButton, acknowledgeButton, exitButton;
        [SerializeField] private Button teleportPracticeButton;
        [SerializeField] private GameObject compactPanel;
        [SerializeField] private TMP_Text compactTitle, compactStatus;
        [SerializeField] private TMP_Text compactInstruction;
        [SerializeField] private Button compactButton, expandButton;
        [SerializeField] private GameObject setupControls, orientationControls;
        [SerializeField, Min(0.8f)] private float headFollowDistance = 1.0f;
        [SerializeField] private float headFollowVerticalOffset = 0f;
        [Header("Explore and Challenge")]
        [SerializeField] private GameObject stageOnePanel, explorePanel, challengePanel, pendingPanel;
        [SerializeField] private TMP_Text exploreDesign, exploreDescription, exploreStatus, exploreError;
        [SerializeField] private TMP_Text challengeBenchmark, challengeStep, challengeInstruction;
        [SerializeField] private TMP_Text challengeItems, challengeStatus, challengeError, pendingStatus;
        [SerializeField] private Button previewB1Button, previewB2Button, startChallengeButton;
        [SerializeField] private Button confirmReorganisationButton, restoreStepButton;
        [SerializeField] private Button exploreExitButton, toolbarExitButton, challengeExitButton, pendingExitButton;
        [SerializeField] private GameObject exploreToolbar;
        [SerializeField] private TMP_Text exploreToolbarDesign, exploreToolbarStatus, exploreToolbarError;
        [SerializeField] private Button beginExploreButton, toolbarB1Button, toolbarB2Button, toolbarChallengeButton;
        [SerializeField] private Button resetTaskButton, restartSessionButton;
        [SerializeField] private GameObject exitedPanel;
        [SerializeField] private TMP_Text exitedStatus;
        [SerializeField] private Button continueSkippedButton;
        [Header("Session controls")]
        [SerializeField] private Button setupPauseButton, setupRetryButton;
        [SerializeField] private Button exploreIntroPauseButton, explorePauseButton;
        [SerializeField] private Button challengePauseButton, pendingPauseButton;
        [SerializeField] private GameObject pausedPanel, finishedPanel, skipPanel;
        [SerializeField] private TMP_Text pausedStatus, finishedStatus, skipError;
        [SerializeField] private Button resumeButton, pausedExitButton, retryButton, finishedRestartButton;
        [SerializeField] private Button openSkipButton, confirmSkipButton, cancelSkipButton;
        [SerializeField] private Button skipDiscomfortButton, skipBlockedButton;
        [SerializeField] private TMP_InputField skipReasonField;
        [SerializeField] private GameObject challengeCompactPanel;
        [SerializeField] private TMP_Text challengeCompactTitle, challengeCompactInstruction;
        [SerializeField] private Button challengeCompactButton, challengeExpandButton, challengeCompactConfirmButton;

        private JourneySnapshot snapshot;
        private int page;
        private bool seated, postureChosen, calibrated, teleported, selected, grabbed, compact;
        private readonly System.Collections.Generic.HashSet<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>
            heldPracticeObjects = new System.Collections.Generic.HashSet<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable[] practiceGrabObjects;
        private bool reset, resetPending, exitUnderstood;
        private float standingEyeHeight = 1.65f;
        private float seatedEyeHeight = 1.20f;
        private Transform eyeHeightOffset;
        private XRBaseInteractor[] reviewInteractors;
        private float floorWorldY;
        private string requestedBenchmark;
        private bool retryExploreLoad;
        private bool exploreStarted;
        private bool retryAvailable, retryPending, skipOpen, challengeCompact;
        private Renderer[] menuHandRenderers;
        private bool[] originalHandVisibility;
        private bool Setup => snapshot != null && snapshot.State == SessionState.Setup;
        private bool Orientation => snapshot != null && snapshot.State == SessionState.Orientation;
        private float DesiredEyeHeight => seated ? seatedEyeHeight : standingEyeHeight;
        internal string CalibratedPosture => calibrated ? (seated ? "Seated" : "Standing") : null;

        private void Awake()
        {
            if (xrOrigin != null)
            {
                floorWorldY = xrOrigin.transform.position.y;
                reviewInteractors = xrOrigin.GetComponentsInChildren<XRBaseInteractor>(true);
            }
            if (recoveryMenu != null) recoveryMenu.SetPostureView(this);
            CacheMenuHandRenderers();
            Wire(previousButton, Previous); Wire(nextButton, Next);
            Wire(standingButton, Standing); Wire(seatedButton, Seated); Wire(calibrateButton, Calibrate);
            Wire(lowerEyeButton, LowerEye); Wire(raiseEyeButton, RaiseEye);
            Wire(completeSetupButton, CompleteSetup); Wire(selectPracticeButton, SelectPractice);
            Wire(teleportPracticeButton, PracticeTeleport);
            Wire(compactButton, MinimisePanel); Wire(expandButton, ExpandPanel);
            Wire(resetPracticeButton, ResetPractice); Wire(acknowledgeButton, Acknowledge);
            Wire(exitButton, SafeExit);
            Wire(previewB1Button, PreviewB1); Wire(previewB2Button, PreviewB2);
            Wire(startChallengeButton, StartChallenge);
            Wire(confirmReorganisationButton, ConfirmReorganisation);
            Wire(challengeCompactConfirmButton, ConfirmReorganisation);
            Wire(restoreStepButton, RestoreCurrentStep);
            Wire(exploreExitButton, SafeExit); Wire(toolbarExitButton, SafeExit);
            Wire(challengeExitButton, SafeExit); Wire(pendingExitButton, SafeExit);
            Wire(beginExploreButton, BeginExplore);
            Wire(toolbarB1Button, PreviewB1); Wire(toolbarB2Button, PreviewB2);
            Wire(toolbarChallengeButton, StartChallenge);
            Wire(resetTaskButton, ResetCurrentTask); Wire(restartSessionButton, StartNewSession);
            Wire(continueSkippedButton, ContinueSkippedChallenge);
            Wire(setupPauseButton, PauseJourney); Wire(explorePauseButton, PauseJourney);
            Wire(exploreIntroPauseButton, PauseJourney);
            Wire(setupRetryButton, RetryFailedOperation);
            Wire(challengePauseButton, PauseJourney); Wire(pendingPauseButton, PauseJourney);
            foreach (var menuButton in new[] { setupPauseButton, explorePauseButton,
                exploreIntroPauseButton, challengePauseButton, pendingPauseButton })
                if (menuButton != null)
                    Label(menuButton.GetComponentInChildren<TMP_Text>(true), "Menu");
            Wire(resumeButton, ResumeJourney); Wire(pausedExitButton, SafeExit);
            Wire(retryButton, RetryFailedOperation); Wire(finishedRestartButton, StartNewSession);
            Wire(openSkipButton, OpenSkip); Wire(confirmSkipButton, ConfirmSkip);
            Wire(cancelSkipButton, CloseSkip);
            Wire(skipDiscomfortButton, SkipForDiscomfort); Wire(skipBlockedButton, SkipForBlockedInteraction);
            Wire(challengeCompactButton, CompactChallenge); Wire(challengeExpandButton, ExpandChallenge);
            if (skipReasonField != null) skipReasonField.onValueChanged.AddListener(OnSkipReasonChanged);
            if (teleportationProvider != null) teleportationProvider.locomotionEnded += OnTeleport;
            practiceGrabObjects = FindObjectsOfType<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>(true);
            foreach (var item in practiceGrabObjects)
            {
                item.selectEntered.AddListener(OnGrab);
                item.selectExited.AddListener(OnRelease);
            }
        }

        private void Start()
        {
            if (host != null && host.Core != null)
                Render(host.Core.Snapshot, JourneyErrorCode.None, string.Empty);
            else Error(host == null ? "Journey host is not assigned." : host.StartupError);
        }

        private void LateUpdate()
        {
            if (xrOrigin == null || xrOrigin.Camera == null || snapshot == null) return;
            var cameraTransform = xrOrigin.Camera.transform;
            var flatForward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up);
            if (flatForward.sqrMagnitude < 0.001f) flatForward = Vector3.ProjectOnPlane(cameraTransform.up, Vector3.up);
            if (flatForward.sqrMagnitude < 0.001f) flatForward = Vector3.forward;
            flatForward.Normalize();

            if (snapshot.State == SessionState.Setup || snapshot.State == SessionState.Orientation)
            {
                transform.position = cameraTransform.position + flatForward * headFollowDistance +
                                     Vector3.up * headFollowVerticalOffset;
                // Unity UI graphics are readable from the local -Z side.
                transform.rotation = Quaternion.LookRotation(flatForward, Vector3.up);
            }

            if (explorePanel != null)
            {
                var stageThreeRoot = explorePanel.transform.root;
                stageThreeRoot.position = cameraTransform.position + flatForward * 1.25f;
                stageThreeRoot.rotation = Quaternion.LookRotation(flatForward, Vector3.up);
                if (snapshot.State == SessionState.Explore && exploreStarted && exploreToolbar != null)
                    PositionAtViewportTop(exploreToolbar, xrOrigin.Camera, stageThreeRoot,
                        0.5f, 0.95f);
                if (snapshot.State == SessionState.ChallengeActive)
                    PositionAtViewportTop(challengeCompact ? challengeCompactPanel : challengePanel,
                        xrOrigin.Camera, stageThreeRoot, 0.025f, 0.94f);
            }
        }

        private static void PositionAtViewportTop(GameObject panel, Camera camera,
            Transform canvasRoot, float viewportX, float viewportY)
        {
            if (panel == null || camera == null) return;
            var rect = panel.GetComponent<RectTransform>();
            if (rect == null) return;
            var ray = camera.ViewportPointToRay(new Vector3(viewportX, viewportY));
            var plane = new Plane(canvasRoot.forward, canvasRoot.position);
            if (!plane.Raycast(ray, out var distance)) return;
            var top = ray.GetPoint(distance);
            var halfWidth = rect.rect.width * rect.lossyScale.x * 0.5f;
            var halfHeight = rect.rect.height * rect.lossyScale.y * 0.5f;
            rect.position = top + (viewportX < 0.5f ? canvasRoot.right * halfWidth : Vector3.zero)
                - canvasRoot.up * halfHeight;
        }

        private void OnDestroy()
        {
            RestoreMenuHandRenderers();
            Unwire(previousButton, Previous); Unwire(nextButton, Next);
            Unwire(standingButton, Standing); Unwire(seatedButton, Seated); Unwire(calibrateButton, Calibrate);
            Unwire(lowerEyeButton, LowerEye); Unwire(raiseEyeButton, RaiseEye);
            Unwire(completeSetupButton, CompleteSetup); Unwire(selectPracticeButton, SelectPractice);
            Unwire(teleportPracticeButton, PracticeTeleport);
            Unwire(compactButton, MinimisePanel); Unwire(expandButton, ExpandPanel);
            Unwire(resetPracticeButton, ResetPractice); Unwire(acknowledgeButton, Acknowledge);
            Unwire(exitButton, SafeExit);
            Unwire(previewB1Button, PreviewB1); Unwire(previewB2Button, PreviewB2);
            Unwire(startChallengeButton, StartChallenge);
            Unwire(confirmReorganisationButton, ConfirmReorganisation);
            Unwire(challengeCompactConfirmButton, ConfirmReorganisation);
            Unwire(restoreStepButton, RestoreCurrentStep);
            Unwire(exploreExitButton, SafeExit); Unwire(toolbarExitButton, SafeExit);
            Unwire(challengeExitButton, SafeExit); Unwire(pendingExitButton, SafeExit);
            Unwire(beginExploreButton, BeginExplore);
            Unwire(toolbarB1Button, PreviewB1); Unwire(toolbarB2Button, PreviewB2);
            Unwire(toolbarChallengeButton, StartChallenge);
            Unwire(resetTaskButton, ResetCurrentTask); Unwire(restartSessionButton, StartNewSession);
            Unwire(continueSkippedButton, ContinueSkippedChallenge);
            Unwire(setupPauseButton, PauseJourney); Unwire(explorePauseButton, PauseJourney);
            Unwire(exploreIntroPauseButton, PauseJourney);
            Unwire(setupRetryButton, RetryFailedOperation);
            Unwire(challengePauseButton, PauseJourney); Unwire(pendingPauseButton, PauseJourney);
            Unwire(resumeButton, ResumeJourney); Unwire(pausedExitButton, SafeExit);
            Unwire(retryButton, RetryFailedOperation); Unwire(finishedRestartButton, StartNewSession);
            Unwire(openSkipButton, OpenSkip); Unwire(confirmSkipButton, ConfirmSkip);
            Unwire(cancelSkipButton, CloseSkip);
            Unwire(skipDiscomfortButton, SkipForDiscomfort); Unwire(skipBlockedButton, SkipForBlockedInteraction);
            Unwire(challengeCompactButton, CompactChallenge); Unwire(challengeExpandButton, ExpandChallenge);
            if (skipReasonField != null) skipReasonField.onValueChanged.RemoveListener(OnSkipReasonChanged);
            if (teleportationProvider != null) teleportationProvider.locomotionEnded -= OnTeleport;
            if (practiceGrabObjects != null)
                foreach (var item in practiceGrabObjects)
                    if (item != null)
                    {
                        item.selectEntered.RemoveListener(OnGrab);
                        item.selectExited.RemoveListener(OnRelease);
                    }
        }

        public void Render(JourneySnapshot current, JourneyErrorCode code, string message)
        {
            if (recoveryMenu != null) recoveryMenu.Render(current, code, message);
            if (phaseFiveView != null) phaseFiveView.Render(current, code, message);
            var previousState = snapshot == null ? SessionState.Setup : snapshot.State;
            snapshot = current;
            if (current == null)
            {
                Label(stateText, "Journey unavailable");
                Error(string.IsNullOrEmpty(message) ? "Waiting for journey state." : message);
                Visible(false, false);
                ShowStage(false, false, false);
                return;
            }
            if (previousState != current.State) page = 0;
            if (previousState == SessionState.ChallengeActive && current.State != SessionState.ChallengeActive)
                skipOpen = false;
            if (code == JourneyErrorCode.OperationFailed || current.Save == SaveStatus.Failed)
                retryAvailable = true;
            if (retryPending && !current.IsBusy)
            {
                retryPending = false;
                if (code == JourneyErrorCode.None && current.Save != SaveStatus.Failed)
                    retryAvailable = false;
            }
            if (previousState != current.State && current.State == SessionState.Explore &&
                previousState != SessionState.Paused)
                exploreStarted = previousState == SessionState.FeedbackPending;
            if (previousState != current.State && current.State == SessionState.ChallengeActive &&
                previousState != SessionState.Paused) challengeCompact = false;
            UpdateMenuHandVisibility();
            if (resetPending && current.State == SessionState.Orientation && !current.IsBusy)
            {
                resetPending = false;
                if (code == JourneyErrorCode.None)
                {
                    reset = ApplyEyeHeight();
                    if (!reset)
                    {
                        code = JourneyErrorCode.OperationFailed;
                        message = "Reset completed but eye height could not be restored.";
                    }
                }
            }
            Label(stateText, "Journey: " + current.State);
            var id = !string.IsNullOrEmpty(current.ActiveBenchmarkId)
                ? current.ActiveBenchmarkId : current.DisplayedBenchmarkId;
            Label(benchmarkText, "Design: " + (string.IsNullOrEmpty(id) ? "None" : id));
            Label(progressText, current.CurrentStep.HasValue
                ? "Task: " + ((int)current.CurrentStep.Value + 1) + "/8 | Items " +
                  current.CompletedItemCount + "/" + current.TotalItemCount
                : "Task: not started");
            var setupAlert = code == JourneyErrorCode.None ? string.Empty : code + ": " + message;
            if (current.CurrentSafety != null)
                setupAlert = "SAFETY: " + current.CurrentSafety.Message + "  " + setupAlert;
            else if (current.Save == SaveStatus.Failed)
                setupAlert = "Save failed. Retry the operation.  " + setupAlert;
            if (current.Conflicts != null && current.Conflicts.Count > 0)
                setupAlert += " Design notices: " + current.Conflicts.Count + ".";
            if (phaseFiveView != null && (current.State == SessionState.FeedbackPending ||
                current.State == SessionState.ComparisonSummary || current.State == SessionState.Finished))
                setupAlert = string.Empty;
            Error(setupAlert);
            Visible(Setup, Orientation);
            RenderExploreChallenge(current, code, message);
            RenderSessionControls(current, code, message);
            Refresh();
        }

        private void RenderExploreChallenge(JourneySnapshot current, JourneyErrorCode code, string message)
        {
            bool explore = current.State == SessionState.Explore;
            bool challenge = current.State == SessionState.ChallengeActive;
            bool pending = current.State == SessionState.FeedbackPending;
            ShowStage(explore, challenge, pending);
            if (!current.IsBusy) requestedBenchmark = null;
            var displayed = current.DisplayedBenchmarkId;
            var next = NextUnresolved(current);
            if (explore)
            {
                if (code == JourneyErrorCode.OperationFailed) retryExploreLoad = true;
                else if (code == JourneyErrorCode.None && !current.IsBusy) retryExploreLoad = false;
                var design = host?.FindBenchmark(displayed);
                Label(exploreDesign, string.IsNullOrEmpty(displayed)
                    ? "Choose a design to preview" : "Current design  |  " + displayed);
                Label(exploreDescription,
                    "Both designs use the same large single-door F2 fridge. Preview the cabinet arrangements, then try the same eight kitchen actions in each design.");
                Label(exploreStatus, "Start Explore to open the design choices.");
                Label(exploreError, code == JourneyErrorCode.None ? "" : code + ": " + message);
                Label(exploreToolbarDesign, design == null ? "Explore" : "Preview " + design.Id);
                Label(exploreToolbarStatus, current.IsBusy ? "Loading design. Please wait." :
                    ExploreGuidance(design, next));
                Label(exploreToolbarError, code == JourneyErrorCode.None ? "" : code + ": " + message);
                if (previewB1Button != null) previewB1Button.interactable =
                    !current.IsBusy && !retryExploreLoad && HasBenchmark(current, "B1");
                if (previewB2Button != null) previewB2Button.interactable =
                    !current.IsBusy && !retryExploreLoad && HasBenchmark(current, "B2");
                if (toolbarB1Button != null) toolbarB1Button.interactable =
                    !current.IsBusy && !retryExploreLoad && HasBenchmark(current, "B1");
                if (toolbarB2Button != null) toolbarB2Button.interactable =
                    !current.IsBusy && !retryExploreLoad && HasBenchmark(current, "B2");
                if (startChallengeButton != null) startChallengeButton.interactable =
                    !current.IsBusy && (retryExploreLoad ||
                    (!string.IsNullOrEmpty(displayed) && displayed == next));
                if (startChallengeButton != null)
                    Label(startChallengeButton.GetComponentInChildren<TMP_Text>(true),
                        retryExploreLoad ? "Retry load" : "Start Challenge");
                if (toolbarChallengeButton != null)
                {
                    toolbarChallengeButton.interactable = startChallengeButton != null && startChallengeButton.interactable;
                    Label(toolbarChallengeButton.GetComponentInChildren<TMP_Text>(true),
                        retryExploreLoad ? "Retry load" : "Challenge");
                }
            }
            if (challenge)
            {
                Label(challengeBenchmark, "Challenge  |  " + current.ActiveBenchmarkId);
                if (current.CurrentStep.HasValue)
                {
                    var step = current.CurrentStep.Value;
                    Label(challengeStep, "STEP " + ((int)step + 1) + " / 8  " + StepTitle(step));
                    Label(challengeInstruction, StepInstruction(step));
                    Label(challengeCompactTitle, current.ActiveBenchmarkId + "  |  Step " +
                        ((int)step + 1) + "/8  " + StepTitle(step));
                    bool reorganising = step == ChallengeStep.Reorganise;
                    bool moved = reorganising && current.CompletedItemCount > 0;
                    Label(challengeCompactInstruction, moved
                        ? "Item moved. Select Confirm to continue." : StepInstruction(step));
                    Label(challengeItems, current.TotalItemCount > 0
                        ? "Items " + current.CompletedItemCount + " / " + current.TotalItemCount
                        : "Follow the green guide.");
                    SetReorganisationButton(confirmReorganisationButton, reorganising, moved && !current.IsBusy);
                    SetReorganisationButton(challengeCompactConfirmButton, reorganising, moved && !current.IsBusy);
                    Label(challengeStatus, current.IsBusy ? "Updating scene..." :
                        reorganising && !moved
                            ? "Move and release an existing item in the marked storage zone."
                            : reorganising
                                ? "Item moved. Confirm your reorganisation."
                                : "Progress advances after the real interaction is accepted.");
                }
                Label(challengeError, code == JourneyErrorCode.None ? "" : code + ": " + message);
                if (restoreStepButton != null) restoreStepButton.interactable = !current.IsBusy;
                if (resetTaskButton != null) resetTaskButton.interactable = !current.IsBusy;
            }
            if (pending)
            {
                var skipped = current.BenchmarkOutcomes != null &&
                    !string.IsNullOrEmpty(current.ActiveBenchmarkId) &&
                    current.BenchmarkOutcomes.TryGetValue(current.ActiveBenchmarkId, out var outcome) &&
                    outcome == TaskOutcome.Skipped;
                Label(pendingStatus, skipped
                    ? current.ActiveBenchmarkId + " Challenge skipped. Save the recorded reason to continue."
                    : current.ActiveBenchmarkId + " task complete. Feedback is pending for this design.");
                if (continueSkippedButton != null)
                {
                    continueSkippedButton.gameObject.SetActive(skipped);
                    continueSkippedButton.interactable = !current.IsBusy;
                }
            }
            if (current.State == SessionState.Exited)
            {
                Label(exitedStatus, current.Save == SaveStatus.Pending
                    ? "Session exited. Final save is still pending."
                    : current.Save == SaveStatus.Verified
                        ? "Session exited. Final save verified. Start a new session to return to Setup."
                        : "Session exited. Final save was not verified. Start a new session only if you accept that risk.");
                if (restartSessionButton != null)
                    restartSessionButton.interactable = current.Save != SaveStatus.Pending;
            }
        }

        private void ShowStage(bool explore, bool challenge, bool pending)
        {
            if (stageOnePanel != null) stageOnePanel.SetActive((Setup || Orientation) && !compact);
            if (compactPanel != null) compactPanel.SetActive((Setup || Orientation) && compact);
            if (explorePanel != null) explorePanel.SetActive(explore && !exploreStarted);
            if (exploreToolbar != null) exploreToolbar.SetActive(explore && exploreStarted);
            if (challengePanel != null) challengePanel.SetActive(challenge && !challengeCompact);
            if (challengeCompactPanel != null) challengeCompactPanel.SetActive(challenge && challengeCompact);
            if (pendingPanel != null) pendingPanel.SetActive(pending && phaseFiveView == null);
            if (exitedPanel != null) exitedPanel.SetActive(snapshot != null && snapshot.State == SessionState.Exited);
            if (pausedPanel != null) pausedPanel.SetActive(recoveryMenu == null &&
                snapshot != null && snapshot.State == SessionState.Paused);
            if (finishedPanel != null) finishedPanel.SetActive(phaseFiveView == null &&
                snapshot != null && snapshot.State == SessionState.Finished);
            if (skipPanel != null) skipPanel.SetActive(challenge && skipOpen);
        }

        private void RenderSessionControls(JourneySnapshot current, JourneyErrorCode code, string message)
        {
            bool stageThree = current.State != SessionState.Setup && current.State != SessionState.Orientation;
            Label(pausedStatus, retryAvailable && current.Save != SaveStatus.Failed
                ? "Session paused. Resume before retrying the failed operation."
                : "Session paused. Resume to continue the previous stage.");
            if (resumeButton != null) resumeButton.interactable = !current.IsBusy;
            Label(finishedStatus, current.Save == SaveStatus.Pending
                ? "Session finished. Waiting for the final save."
                : "Session finished. Start a new session to return to Setup.");
            if (finishedRestartButton != null)
                finishedRestartButton.interactable = current.Save != SaveStatus.Pending;
            if (retryButton != null)
            {
                retryButton.gameObject.SetActive(stageThree && retryAvailable &&
                    current.State != SessionState.Exited && current.State != SessionState.Finished);
                retryButton.interactable = !current.IsBusy &&
                    (current.State != SessionState.Paused || current.Save == SaveStatus.Failed);
            }
            if (setupRetryButton != null)
            {
                setupRetryButton.gameObject.SetActive(!stageThree && retryAvailable);
                setupRetryButton.interactable = !current.IsBusy;
            }
            if (confirmSkipButton != null)
                confirmSkipButton.interactable = !current.IsBusy && skipReasonField != null &&
                    !string.IsNullOrWhiteSpace(skipReasonField.text);
            if (openSkipButton != null) openSkipButton.interactable = !current.IsBusy;
            if (skipError != null && (!skipOpen || code == JourneyErrorCode.None))
                Label(skipError, string.Empty);
        }

        private void CacheMenuHandRenderers()
        {
            if (xrOrigin == null) return;
            var candidates = xrOrigin.GetComponentsInChildren<Renderer>(true);
            var hands = new System.Collections.Generic.List<Renderer>();
            foreach (var candidate in candidates)
            {
                var parent = candidate.transform.parent;
                if (candidate.name == "BasicHand" && parent != null && parent.name == "Hand Model")
                    hands.Add(candidate);
            }
            menuHandRenderers = hands.ToArray();
            originalHandVisibility = new bool[menuHandRenderers.Length];
            for (var i = 0; i < menuHandRenderers.Length; i++)
                originalHandVisibility[i] = menuHandRenderers[i].enabled;
        }

        private void UpdateMenuHandVisibility()
        {
            if (menuHandRenderers == null || snapshot == null) return;
            var showHands = snapshot.State != SessionState.Setup &&
                            snapshot.State != SessionState.Orientation &&
                            (snapshot.State != SessionState.Explore || exploreStarted) &&
                            (phaseFiveView != null || snapshot.State != SessionState.FeedbackPending);
            for (var i = 0; i < menuHandRenderers.Length; i++)
                if (menuHandRenderers[i] != null)
                    menuHandRenderers[i].enabled = showHands && originalHandVisibility[i];
        }

        private void RestoreMenuHandRenderers()
        {
            if (menuHandRenderers == null) return;
            for (var i = 0; i < menuHandRenderers.Length; i++)
                if (menuHandRenderers[i] != null)
                    menuHandRenderers[i].enabled = originalHandVisibility[i];
        }

        private static bool HasBenchmark(JourneySnapshot current, string id)
        {
            if (current.BenchmarkOrder == null) return false;
            foreach (var candidate in current.BenchmarkOrder) if (candidate == id) return true;
            return false;
        }

        private static string ExploreGuidance(BenchmarkDefinition design, string next)
        {
            var guide = next == null ? "Both Challenges are complete." : "Next Challenge  |  " + next + ". " +
                (design?.Id == next ? "Select Challenge to begin." : "Preview " + next + " to begin.");
            if (design == null)
                return "Choose B1 or B2. Both previews use the same F2 fridge.\n" + guide;
            var cabinet = design.Cabinet == CabinetStrategy.Proud ? "Proud" : "Flush";
            var description = design.Id + "  |  " + cabinet + " + " + design.Fridge;
            if (!string.IsNullOrWhiteSpace(design.ControlledQuestion))
                description += "\n" + design.ControlledQuestion;
            return description + "\n" + guide;
        }

        private static string NextUnresolved(JourneySnapshot current)
        {
            if (current.BenchmarkOrder == null || current.BenchmarkOutcomes == null) return null;
            foreach (var id in current.BenchmarkOrder)
                if (current.BenchmarkOutcomes.TryGetValue(id, out var outcome) &&
                    outcome == TaskOutcome.Unset) return id;
            return null;
        }

        private static string StepTitle(ChallengeStep step)
        {
            switch (step)
            {
                case ChallengeStep.TakeOut: return "Take out";
                case ChallengeStep.TemporaryPlacement: return "Place on counter";
                case ChallengeStep.PutBack: return "Put back";
                default: return step.ToString();
            }
        }

        private static string StepInstruction(ChallengeStep step)
        {
            switch (step)
            {
                case ChallengeStep.Approach: return "Move your headset into the GREEN approach zone.";
                case ChallengeStep.Open: return "Fully open the fridge door.";
                case ChallengeStep.TakeOut: return "Remove TOMATO and ONION from the upper source shelf.";
                case ChallengeStep.TemporaryPlacement: return "Release TOMATO and ONION on the PURPLE counter highlight.";
                case ChallengeStep.Reorganise: return "Move TOMATO or ONION from the purple counter to a lower fridge shelf. Release it, then confirm.";
                case ChallengeStep.PutBack: return "Release all THREE items onto ANY lower shelf. Include the CEREAL BOX.";
                case ChallengeStep.Close: return "Fully close the fridge door.";
                case ChallengeStep.Leave: return "Exit GREEN, then move your headset into the BLUE zone.";
                default: return "Follow the target guide.";
            }
        }

        private static string TeleportPracticeInstruction()
        {
#if UNITY_EDITOR
            return "Aim a controller at the open floor ahead. In the PC simulator, hold I for the right thumbstick " +
                   "(Shift+I for the left), point the teleport arc at the floor, then release I to land. " +
                   "Keep your physical play area clear.";
#else
            return "Aim at the open floor ahead. Push a controller thumbstick forward to show the teleport arc, " +
                   "then release it to land. Check your physical play area before moving.";
#endif
        }

        private void PreviewB1() => Preview("B1");
        private void PreviewB2() => Preview("B2");
        private void Preview(string id)
        {
            if (snapshot == null || snapshot.State != SessionState.Explore || snapshot.IsBusy ||
                !HasBenchmark(snapshot, id) || host == null) return;
            requestedBenchmark = id;
            host.Submit(new JourneyCommand { Type = JourneyCommandType.ShowBenchmark, BenchmarkId = id });
        }

        private void StartChallenge()
        {
            if (retryExploreLoad && snapshot != null && snapshot.State == SessionState.Explore &&
                !snapshot.IsBusy && host != null)
            {
                host.Submit(new JourneyCommand { Type = JourneyCommandType.Retry });
                return;
            }
            if (snapshot == null || snapshot.State != SessionState.Explore || snapshot.IsBusy ||
                string.IsNullOrEmpty(snapshot.DisplayedBenchmarkId) ||
                snapshot.DisplayedBenchmarkId != NextUnresolved(snapshot) || host == null) return;
            requestedBenchmark = snapshot.DisplayedBenchmarkId;
            host.Submit(new JourneyCommand
            { Type = JourneyCommandType.StartChallenge, BenchmarkId = requestedBenchmark });
        }

        private void ConfirmReorganisation()
        {
            if (snapshot == null || snapshot.State != SessionState.ChallengeActive ||
                snapshot.CurrentStep != ChallengeStep.Reorganise || snapshot.CompletedItemCount == 0 ||
                snapshot.IsBusy || host == null) return;
            host.Submit(new JourneyCommand { Type = JourneyCommandType.ConfirmReorganisation });
        }

        private static void SetReorganisationButton(Button button, bool visible, bool interactable)
        {
            if (button == null) return;
            button.gameObject.SetActive(visible);
            button.interactable = interactable;
        }

        private void RestoreCurrentStep()
        {
            if (snapshot == null || snapshot.State != SessionState.ChallengeActive ||
                snapshot.IsBusy || host == null) return;
            host.Submit(new JourneyCommand { Type = JourneyCommandType.RestoreCurrentStep });
        }
        private void ResetCurrentTask()
        {
            if (snapshot == null || snapshot.State != SessionState.ChallengeActive ||
                snapshot.IsBusy || host == null) return;
            host.Submit(new JourneyCommand { Type = JourneyCommandType.ResetCurrentTask });
        }
        private void PauseJourney()
        {
            if (recoveryMenu != null) { recoveryMenu.Open(); return; }
        }
        private void ResumeJourney()
        {
            if (snapshot == null || snapshot.State != SessionState.Paused || host == null) return;
            host.Submit(new JourneyCommand { Type = JourneyCommandType.Resume });
        }
        private void RetryFailedOperation()
        {
            if (snapshot == null || !retryAvailable || snapshot.IsBusy || host == null) return;
            retryPending = true;
            var result = host.Submit(new JourneyCommand { Type = JourneyCommandType.Retry });
            if (!result.Accepted) retryPending = false;
        }
        private void OpenSkip()
        {
            if (recoveryMenu != null) { recoveryMenu.Open(); return; }
            if (snapshot == null || snapshot.State != SessionState.ChallengeActive || snapshot.IsBusy) return;
            skipOpen = true;
            ShowStage(false, true, false);
        }
        private void CompactChallenge()
        {
            if (snapshot == null || snapshot.State != SessionState.ChallengeActive) return;
            challengeCompact = true;
            ShowStage(false, true, false);
        }
        private void ExpandChallenge()
        {
            challengeCompact = false;
            ShowStage(false, snapshot != null && snapshot.State == SessionState.ChallengeActive, false);
        }
        private void CloseSkip()
        {
            skipOpen = false;
            ShowStage(false, snapshot != null && snapshot.State == SessionState.ChallengeActive, false);
        }
        private void SkipForDiscomfort() { SetSkipReason("Discomfort during the task"); }
        private void SkipForBlockedInteraction() { SetSkipReason("Unable to complete an interaction"); }
        private void SetSkipReason(string reason)
        {
            if (skipReasonField != null) skipReasonField.text = reason;
        }
        private void OnSkipReasonChanged(string reason)
        {
            if (confirmSkipButton != null)
                confirmSkipButton.interactable = snapshot != null && !snapshot.IsBusy &&
                    !string.IsNullOrWhiteSpace(reason);
        }
        private void ConfirmSkip()
        {
            if (snapshot == null || snapshot.State != SessionState.ChallengeActive ||
                snapshot.IsBusy || host == null || skipReasonField == null) return;
            var reason = skipReasonField.text.Trim();
            if (string.IsNullOrEmpty(reason))
            {
                Label(skipError, "Choose or enter a reason before skipping.");
                return;
            }
            var result = host.Submit(new JourneyCommand
            { Type = JourneyCommandType.SkipChallenge, Reason = reason });
            if (result.Accepted) skipOpen = false;
            else Label(skipError, result.ErrorCode + ": " + result.Message);
        }
        private void ContinueSkippedChallenge()
        {
            if (snapshot == null || snapshot.State != SessionState.FeedbackPending ||
                snapshot.IsBusy || host == null || snapshot.BenchmarkOutcomes == null ||
                string.IsNullOrEmpty(snapshot.ActiveBenchmarkId) ||
                !snapshot.BenchmarkOutcomes.TryGetValue(snapshot.ActiveBenchmarkId, out var outcome) ||
                outcome != TaskOutcome.Skipped) return;
            host.SubmitFeedback(new FeedbackDraft());
        }
        private void StartNewSession()
        {
            if (snapshot == null || (snapshot.State != SessionState.Exited &&
                snapshot.State != SessionState.Finished) ||
                snapshot.Save == SaveStatus.Pending) return;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        private void Refresh()
        {
            if (!Setup && !Orientation) return;
            int last = Setup ? 2 : 4;
            page = Mathf.Clamp(page, 0, last);
            if (Setup)
            {
                string[] titles = { "1 / 3  Kitchen design review", "2 / 3  Design boundaries",
                    "3 / 3  Posture and eye height" };
                string[] bodies = {
                    "Explore one representative kitchen. Compare B1 Proud and B2 Flush refrigerator layouts through the same everyday task. This is a prototype, not a finished home or compliance assessment. Stay inside your physical play area. Stop if uncomfortable.",
                    "Fixed: layouts and tasks are preset. Reviewable: judge access and everyday use, then give feedback. Simplified: objects and interactions cover a limited kitchen. Assumption: dimensions and clearances are prototype assumptions, not verified client facts. No free editing or real weight feedback.",
                    "Choose standing or seated, then calibrate your virtual eye height. Keep your feet or chair inside the play area. Recalibrate if the view feels wrong."
                };
                Label(pageTitle, titles[page]); Label(pageBody, bodies[page]);
                Label(setupHintText, (postureChosen ? (seated ? "Seated" : "Standing") : "Choose posture") +
                    " | Eye " + DesiredEyeHeight.ToString("0.00") + " m" +
                    (calibrated ? " | Ready" : " | Calibrate"));
            }
            else
            {
                string[] titles = { "1 / 5  Teleport", "2 / 5  Controller selection",
                    "3 / 5  Grab", "4 / 5  Reset view", "5 / 5  Exit and confirm" };
                string[] bodies = {
                    TeleportPracticeInstruction(),
                    "Aim the controller ray at Select practice and press the UI trigger. Use the same ray to choose review controls.",
                    "Aim the ray and hold grip to grab a distant object. Tilt that hand's stick back to pull or forward to push. Release an object on the counter to finish practice.",
                    "Press Reset view to recenter through the journey scene adapter. Recheck your physical play area after reset.",
                    "Safe exit is available throughout the guide. It ends this session. Use it if unsafe or unwell. Confirm this before entering Explore."
                };
                Label(pageTitle, titles[page]); Label(pageBody, bodies[page]);
                Label(practiceStatus, "Teleport " + Mark(teleported) + "  Select " + Mark(selected) +
                    "  Grab " + Mark(grabbed) + "  Reset " + Mark(reset) + "  Exit " + Mark(exitUnderstood));
            }
            if (previousButton != null) previousButton.interactable = page > 0;
            if (nextButton != null) nextButton.interactable = page < last && PageComplete();
            Label(compactTitle, KitchenLocale.Source(pageTitle));
            Label(compactInstruction, KitchenLocale.Source(pageBody));
            Label(compactStatus, Orientation ? "Teleport " + Mark(teleported) + "  Select " + Mark(selected) +
                "  Grab " + Mark(grabbed) + "  Reset " + Mark(reset) + "  Exit " + Mark(exitUnderstood) :
                KitchenLocale.Source(pageBody));
            Show(standingButton, Setup && page == 2); Show(seatedButton, Setup && page == 2);
            Show(lowerEyeButton, Setup && page == 2); Show(raiseEyeButton, Setup && page == 2);
            Show(calibrateButton, Setup && page == 2); Show(completeSetupButton, Setup && page == 2);
            Show(selectPracticeButton, Orientation && page == 1);
            Show(teleportPracticeButton, Orientation && page == 0 && Application.isEditor);
            Show(resetPracticeButton, Orientation && page == 3);
            Show(acknowledgeButton, Orientation && page == 4);
            if (completeSetupButton != null) completeSetupButton.interactable =
                postureChosen && calibrated && !snapshot.IsBusy;
            if (acknowledgeButton != null) acknowledgeButton.interactable =
                teleported && selected && grabbed && reset && !snapshot.IsBusy;
        }

        private bool PageComplete()
        {
            if (Setup) return page != 2 || calibrated;
            switch (page)
            {
                case 0: return teleported;
                case 1: return selected;
                case 2: return grabbed;
                case 3: return reset;
                default: return exitUnderstood;
            }
        }
        private void Previous() { if (page > 0) { page--; Refresh(); } }
        private void Next() { if (PageComplete()) { page++; Refresh(); } }
        private void Standing() => ChooseSetupPosture(false);
        private void Seated() => ChooseSetupPosture(true);
        private void ChooseSetupPosture(bool useSeated)
        {
            seated = useSeated;
            postureChosen = true;
            calibrated = false;
            Refresh();
        }
        private void LowerEye() { AdjustEye(-0.05f); }
        private void RaiseEye() { AdjustEye(0.05f); }
        private void AdjustEye(float step)
        {
            if (!Setup || !postureChosen) return;
            var height = Mathf.Clamp(DesiredEyeHeight + step, 0.90f, 1.95f);
            if (seated) seatedEyeHeight = height;
            else standingEyeHeight = height;
            calibrated = false;
            Refresh();
        }

        private void Calibrate()
        {
            if (!Setup || !postureChosen)
            { Error("Choose standing or seated before calibration."); return; }
            if (xrOrigin == null || xrOrigin.Camera == null)
            { Error("XR Origin and tracked camera are required for calibration."); return; }
            if (!ApplyEyeHeight())
            { Error("Eye height calibration needs a valid tracking offset on the XR rig."); return; }
            calibrated = true;
            if (calibrationConfirmed != null) calibrationConfirmed.isOn = true;
            Error(string.Empty); Refresh();
        }

        internal string CalibrateReviewPosture(bool useSeated)
        {
            if (reviewInteractors != null)
                foreach (var interactor in reviewInteractors)
                    if (interactor != null && interactor.hasSelection)
                        foreach (var selectedObject in interactor.interactablesSelected)
                            if (selectedObject is XRGrabInteractable)
                                return "Put down the item you are holding before changing the view.";

            var previousPosture = seated;
            seated = useSeated;
            if (!ApplyEyeHeight())
            {
                seated = previousPosture;
                return "Eye height calibration needs a valid tracking offset on the XR rig.";
            }

            postureChosen = calibrated = true;
            if (calibrationConfirmed != null) calibrationConfirmed.isOn = true;
            Refresh();
            return CalibratedPosture + " view calibrated. Your review progress is kept.";
        }

        private bool ApplyEyeHeight() => KitchenLocomotionSettings.CalibrateEyeHeight(xrOrigin,
            teleportationProvider as KitchenTeleportationProvider, DesiredEyeHeight, ref eyeHeightOffset);
        private void CompleteSetup()
        {
            if (!Setup || !postureChosen || !calibrated || host == null)
            { Error("Choose a posture and calibrate eye height first."); return; }
            host.Submit(new JourneyCommand { Type = JourneyCommandType.CompleteSetup, PrerequisitesMet = true });
        }
        private void OnTeleport(UnityEngine.XR.Interaction.Toolkit.Locomotion.LocomotionProvider provider)
        { if (Orientation) { teleported = true; Refresh(); } }
        private void PracticeTeleport()
        {
            if (!Orientation || page != 0 || !Application.isEditor || teleportationProvider == null ||
                teleportPracticeArea == null) return;
            var areaCollider = teleportPracticeArea.GetComponent<Collider>();
            if (areaCollider == null) { Error("Teleport practice area is missing its collider."); return; }
            var destination = areaCollider.bounds.center;
            destination.y = floorWorldY;
            var request = new UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportRequest
            {
                destinationPosition = destination,
                destinationRotation = xrOrigin != null ? xrOrigin.transform.rotation : Quaternion.identity,
                matchOrientation = UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.MatchOrientation.None,
                requestTime = Time.time
            };
            if (!teleportationProvider.QueueTeleportRequest(request))
                Error("Teleport practice could not start. Try the controller thumbstick.");
        }
        private void SelectPractice()
        { if (Orientation) { selected = true; Refresh(); } }
        private void MinimisePanel() { compact = true; ShowStage(false, false, false); }
        private void ExpandPanel() { compact = false; ShowStage(false, false, false); }
        private void BeginExplore()
        {
            if (Orientation || snapshot == null || snapshot.State != SessionState.Explore) return;
            exploreStarted = true;
            ShowStage(true, false, false);
            UpdateMenuHandVisibility();
        }
        private void OnGrab(SelectEnterEventArgs args)
        {
            if (Orientation && args.interactableObject is UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable item)
                heldPracticeObjects.Add(item);
        }
        private void OnRelease(SelectExitEventArgs args)
        {
            if (!(args.interactableObject is UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable item) ||
                !heldPracticeObjects.Remove(item) || !Orientation) return;
            grabbed = true;
            Refresh();
        }
        private void ResetPractice()
        {
            if (!Orientation || host == null) return;
            resetPending = true;
            var result = host.Submit(new JourneyCommand { Type = JourneyCommandType.Recenter });
            if (!result.Accepted) resetPending = false;
            Refresh();
        }
        private void Acknowledge()
        {
            if (!Orientation || host == null) return;
            exitUnderstood = true;
            if (practiceConfirmed != null) practiceConfirmed.isOn = true;
            Refresh();
            if (!teleported || !selected || !grabbed || !reset)
            { Error("Complete Teleport, Select, Grab and Reset practice first."); return; }
            host.Submit(new JourneyCommand
            { Type = JourneyCommandType.AcknowledgeOrientation, PrerequisitesMet = true });
        }
        private void SafeExit()
        { if (recoveryMenu != null) recoveryMenu.RequestExitConfirmation(); }
        private void Visible(bool setup, bool orientation)
        {
            if (setupControls != null) setupControls.SetActive(setup);
            if (orientationControls != null) orientationControls.SetActive(orientation);
            var guiding = setup || orientation;
            if (pageTitle != null) pageTitle.gameObject.SetActive(guiding);
            if (pageBody != null) pageBody.gameObject.SetActive(guiding);
            if (previousButton != null) previousButton.gameObject.SetActive(guiding);
            if (nextButton != null) nextButton.gameObject.SetActive(guiding);
            if (exitButton != null) exitButton.gameObject.SetActive(guiding);
        }
        private void Error(string message)
        {
            Label(errorText, message);
            if (errorText != null) errorText.gameObject.SetActive(!string.IsNullOrEmpty(message));
        }
        private static string Mark(bool done) => done ? "[done]" : "[ ]";
        private static void Label(TMP_Text text, string value) { if (text != null) KitchenLocale.Set(text, value); }
        private static void Show(Button button, bool visible)
        { if (button != null) button.gameObject.SetActive(visible); }
        private static void Wire(Button button, UnityEngine.Events.UnityAction action)
        { if (button != null) button.onClick.AddListener(action); }
        private static void Unwire(Button button, UnityEngine.Events.UnityAction action)
        { if (button != null) button.onClick.RemoveListener(action); }
    }
}
