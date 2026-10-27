using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Unity.XR.CoreUtils;
using XRApartment.Runtime.Core;
using XRApartment.Simulation;

namespace XRApartment.Runtime.UI
{
    /// <summary>World space recovery controls. JourneyRuntimeHost remains the sole command path.</summary>
    public sealed class RecoveryMenuView : MonoBehaviour
    {
        [SerializeField] private JourneyRuntimeHost host;
        [SerializeField] private XROrigin xrOrigin;
        [SerializeField] private GameObject panel, exitConfirmation;
        [SerializeField] private TMP_Text status, progress, operation, saveWarning;
        [SerializeField] private Button pause, resume, restoreStep, resetTask, returnItem;
        [SerializeField] private Button replayBenchmark, recenter, retry, skip, safeExit;
        [SerializeField] private Button discomfortReason, blockedReason;
        [SerializeField] private Button confirmExit, cancelExit, close;
        [SerializeField] private Button chooseItem;
        [SerializeField] private TMP_Text selectedItem;
        [SerializeField] private TMP_InputField skipReason;
        [SerializeField, Min(0.5f)] private float headDistance = 0.65f;

        private InputAction toggleAction;
        private JourneySnapshot snapshot;
        private bool open, exitPending, retryAvailable, retryPending;
        private string operationMessage = "";
        private string pendingLabel;
        private IReadOnlyList<string> itemIds;
        private int itemIndex;

        private void Awake()
        {
            Wire(pause, () => Send(JourneyCommandType.Pause));
            Wire(resume, () => Send(JourneyCommandType.Resume));
            Wire(restoreStep, () => Send(JourneyCommandType.RestoreCurrentStep));
            Wire(resetTask, () => Send(JourneyCommandType.ResetCurrentTask));
            Wire(returnItem, ReturnSelectedItem);
            Wire(chooseItem, ChooseNextItem);
            Wire(replayBenchmark, () => Send(JourneyCommandType.ReplayCurrentBenchmark));
            Wire(recenter, () => Send(JourneyCommandType.Recenter));
            Wire(retry, () => Send(JourneyCommandType.Retry));
            Wire(skip, SkipChallenge);
            Wire(discomfortReason, () => SetSkipReason("Discomfort during the task"));
            Wire(blockedReason, () => SetSkipReason("Unable to complete an interaction"));
            Wire(safeExit, RequestExitConfirmation);
            Wire(confirmExit, ConfirmExit);
            Wire(cancelExit, CancelExit);
            Wire(close, Close);
            if (skipReason != null) skipReason.onValueChanged.AddListener(_ => RefreshSkipButton());
            toggleAction = new InputAction("Recovery menu", InputActionType.Button);
            toggleAction.AddBinding("<XRController>{RightHand}/secondaryButton");
            toggleAction.AddBinding("<Keyboard>/escape");
            toggleAction.performed += ToggleFromController;
            toggleAction.Enable();
            if (panel != null) panel.SetActive(false);
        }

        private void Start()
        {
            if (host != null)
            {
                itemIds = host.TaskItemIds;
                RefreshSelectedItem();
                if (host.Core != null) Render(host.Core.Snapshot, JourneyErrorCode.None, "");
            }
        }

        private void OnDestroy()
        {
            if (toggleAction == null) return;
            toggleAction.performed -= ToggleFromController;
            toggleAction.Disable();
            toggleAction.Dispose();
        }

        private void LateUpdate()
        {
            if (!open || xrOrigin == null || xrOrigin.Camera == null) return;
            var camera = xrOrigin.Camera.transform;
            var forward = Vector3.ProjectOnPlane(camera.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            transform.position = camera.position + forward * headDistance;
            transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }

        public void Render(JourneySnapshot current, JourneyErrorCode error, string message)
        {
            snapshot = current;
            if (current == null) return;
            if (error != JourneyErrorCode.None)
            {
                operationMessage = error + ": " + message;
                pendingLabel = null;
                if (error == JourneyErrorCode.OperationFailed) retryAvailable = true;
            }
            else if (retryPending && !current.IsBusy && current.Save != SaveStatus.Failed)
            {
                operationMessage = "Retry completed. Continue from the current state.";
                retryAvailable = false;
                retryPending = false;
                pendingLabel = null;
            }
            else if (!current.IsBusy && pendingLabel != null)
            {
                operationMessage = pendingLabel + " completed.";
                pendingLabel = null;
            }
            if (error == JourneyErrorCode.OperationFailed && !current.IsBusy) retryPending = false;
            if (current.Save == SaveStatus.Failed)
                retryAvailable = true;
            if (current.State == SessionState.Paused || current.Save == SaveStatus.Failed ||
                error == JourneyErrorCode.OperationFailed)
                Open();
            if (current.State == SessionState.Exited || current.State == SessionState.Finished)
            {
                exitPending = false;
                Close();
            }
            if (panel == null) return;
            var challenge = current.State == SessionState.ChallengeActive;
            var paused = current.State == SessionState.Paused;
            var ended = current.State == SessionState.Exited || current.State == SessionState.Finished;
            var busy = current.IsBusy;
            Set(pause, !paused && !ended, !busy);
            Set(resume, paused, !busy);
            Set(restoreStep, challenge, !busy && !retryAvailable);
            Set(resetTask, challenge, !busy && !retryAvailable);
            Set(returnItem, challenge && current.CurrentStep == ChallengeStep.PutBack,
                !busy && !retryAvailable && itemIds != null && itemIds.Count > 0);
            Set(chooseItem, challenge && current.CurrentStep == ChallengeStep.PutBack,
                !busy && itemIds != null && itemIds.Count > 1);
            Set(replayBenchmark, challenge, !busy && !retryAvailable);
            Set(recenter, !ended && current.State != SessionState.Setup, !busy && !retryAvailable && !paused);
            Set(retry, retryAvailable && !ended, !busy && (!paused || current.Save == SaveStatus.Failed));
            Set(skip, challenge, !busy && !retryAvailable && skipReason != null &&
                !string.IsNullOrWhiteSpace(skipReason.text));
            Set(discomfortReason, challenge, !busy && !retryAvailable);
            Set(blockedReason, challenge, !busy && !retryAvailable);
            Set(safeExit, !ended, true);
            if (confirmExit != null) confirmExit.interactable = !ended;
            if (exitConfirmation != null) exitConfirmation.SetActive(exitPending && !ended);
            if (close != null) close.interactable = !paused && !exitPending;
            Label(status, current.State + (busy ? "  |  Busy: please wait" : ""));
            Label(progress, current.CurrentStep.HasValue
                ? current.ActiveBenchmarkId + " | Step " + ((int)current.CurrentStep.Value + 1) + "/8 | " +
                  current.CompletedItemCount + "/" + current.TotalItemCount + " items"
                : "Current progress remains in this session.");
            Label(operation, string.IsNullOrEmpty(operationMessage)
                ? "Restore step keeps earlier progress. Reset task starts at step 1. Replay reloads this design."
                : operationMessage);
            Label(saveWarning, current.Save == SaveStatus.Failed
                ? "Local save failed. Progress remains in memory. Use Retry or Safe Exit; exit does not prove a save."
                : current.State == SessionState.Exited && current.Save != SaveStatus.Verified
                    ? "Session exited. Final save has not been verified."
                    : "");
        }

        public void Open()
        {
            if (snapshot != null && (snapshot.State == SessionState.Exited ||
                snapshot.State == SessionState.Finished)) return;
            open = true;
            if (panel != null) panel.SetActive(true);
            if (snapshot != null) RenderVisibleState();
        }

        public void RequestExitConfirmation()
        {
            Open();
            exitPending = true;
            if (exitConfirmation != null) exitConfirmation.SetActive(true);
        }

        private void RenderVisibleState()
        {
            if (exitConfirmation != null) exitConfirmation.SetActive(exitPending);
        }

        private void Close()
        {
            if (snapshot != null && snapshot.State == SessionState.Paused) return;
            open = false;
            exitPending = false;
            if (panel != null) panel.SetActive(false);
        }

        private void ToggleFromController(InputAction.CallbackContext context)
        {
            if (open) Close(); else Open();
        }

        private void CancelExit()
        {
            exitPending = false;
            if (exitConfirmation != null) exitConfirmation.SetActive(false);
        }

        private void ConfirmExit()
        {
            exitPending = false;
            Send(JourneyCommandType.RequestExit);
        }

        private void ReturnSelectedItem()
        {
            if (host == null || itemIds == null || itemIds.Count == 0) return;
            Submit(new JourneyCommand { Type = JourneyCommandType.ReturnItemToTray,
                ItemId = itemIds[itemIndex] });
        }

        private void ChooseNextItem()
        {
            if (itemIds == null || itemIds.Count == 0) return;
            itemIndex = (itemIndex + 1) % itemIds.Count;
            RefreshSelectedItem();
        }

        private void RefreshSelectedItem()
        {
            Label(selectedItem, itemIds == null || itemIds.Count == 0
                ? "No task item" : "Item: " + itemIds[itemIndex]);
        }

        private void RefreshSkipButton()
        {
            if (snapshot != null && skip != null) skip.interactable =
                snapshot.State == SessionState.ChallengeActive && !snapshot.IsBusy &&
                !retryAvailable && skipReason != null && !string.IsNullOrWhiteSpace(skipReason.text);
        }

        private void SkipChallenge()
        {
            if (skipReason == null || string.IsNullOrWhiteSpace(skipReason.text)) return;
            Submit(new JourneyCommand { Type = JourneyCommandType.SkipChallenge,
                Reason = skipReason.text.Trim() });
        }

        private void SetSkipReason(string reason)
        {
            if (skipReason != null) skipReason.text = reason;
        }

        private void Send(JourneyCommandType type)
        {
            if (type == JourneyCommandType.Retry) retryPending = true;
            Submit(new JourneyCommand { Type = type });
        }

        private void Submit(JourneyCommand command)
        {
            if (host == null) return;
            pendingLabel = command.Type.ToString();
            var result = host.Submit(command);
            if (!result.Accepted)
            {
                pendingLabel = null;
                if (command.Type == JourneyCommandType.Retry) retryPending = false;
                operationMessage = result.ErrorCode + ": " + result.Message;
                Render(result.Snapshot, result.ErrorCode, result.Message);
            }
            else if (command.Type == JourneyCommandType.SkipChallenge && skipReason != null)
                skipReason.text = "";
        }

        private static void Set(Button button, bool visible, bool enabled)
        {
            if (button == null) return;
            button.gameObject.SetActive(visible);
            button.interactable = enabled;
        }
        private static void Label(TMP_Text label, string value)
        { if (label != null) label.text = value; }
        private static void Wire(Button button, UnityEngine.Events.UnityAction action)
        { if (button != null) button.onClick.AddListener(action); }
    }
}
