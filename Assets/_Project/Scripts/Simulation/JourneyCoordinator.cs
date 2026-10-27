using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using XRApartment.Data;

namespace XRApartment.Simulation
{
    /// <summary>The sole owner of journey state. External work is returned as one action at a time.</summary>
    public sealed class JourneyCoordinator : IJourneyCore
    {
        private enum PendingKind
        {
            None, Show, Start, Reset, Return, Replay, Recenter, RestoreStep,
            FeedbackSave, ComparisonSave, PauseSave, ExitSave
        }

        private readonly SessionPlan plan;
        private readonly SessionRecord record;
        private readonly SessionStateMachine states;
        private readonly Dictionary<string, TaskOutcome> outcomes = new Dictionary<string, TaskOutcome>();
        private readonly HashSet<string> resolved = new HashSet<string>();
        private readonly Dictionary<string, ConflictNotice> conflicts = new Dictionary<string, ConflictNotice>();
        private readonly HashSet<string> opened = new HashSet<string>();
        private readonly HashSet<string> closed = new HashSet<string>();
        private readonly HashSet<string> taken = new HashSet<string>();
        private readonly HashSet<string> placed = new HashSet<string>();
        private readonly HashSet<string> reorganised = new HashSet<string>();
        private readonly HashSet<string> returned = new HashSet<string>();

        private ChallengeTaskTracker tracker;
        private JourneyAction pending;
        private PendingKind pendingKind;
        private JourneyAction failedAction;
        private PendingKind failedKind;
        private JourneyAction retainedFailure;
        private PendingKind retainedFailureKind;
        private SafetyNotice currentSafety;
        private string displayedBenchmarkId;
        private string activeBenchmarkId;
        private string skippedReason;
        public string PendingSkipReason => states.Current == SessionState.FeedbackPending &&
            outcomes.TryGetValue(activeBenchmarkId, out var outcome) && outcome == TaskOutcome.Skipped
                ? skippedReason : null;
        private long activeRunId;
        private long nextRequestId;
        private long nextRunId;
        private SaveStatus saveStatus;
        private bool feedbackRecorded;
        private bool pauseSaveAfterPending;
        private SessionTrigger? triggerOnResume;

        public JourneyCoordinator(SessionPlan source)
        {
            plan = CopyPlan(source);
            PlanError = ValidatePlan(plan);
            HasValidPlan = PlanError == null;
            states = new SessionStateMachine(Math.Max(1, plan == null || plan.BenchmarkIds == null
                ? 1 : plan.BenchmarkIds.Count));
            record = new SessionRecord();
            if (!HasValidPlan) return;

            record.sessionId = plan.SessionId;
            record.modelVersion = plan.ModelVersion;
            foreach (var benchmarkId in plan.BenchmarkIds)
            {
                record.benchmarkOrder.Add(benchmarkId);
                outcomes.Add(benchmarkId, TaskOutcome.Unset);
            }
        }

        public bool HasValidPlan { get; }
        public string PlanError { get; }
        public JourneySnapshot Snapshot => MakeSnapshot();

        public JourneyResult Submit(JourneyCommand command)
        {
            if (!HasValidPlan) return Reject(JourneyErrorCode.InvalidPlan, PlanError);
            if (command == null) return Reject(JourneyErrorCode.InvalidState, "Command is required.");
            if (IsCallback(command.Type)) return CompleteCallback(command);
            if (command.SessionId != plan.SessionId)
                return Reject(JourneyErrorCode.WrongSession, "Command session does not match this journey.");
            if (states.Current == SessionState.Finished || states.Current == SessionState.Exited)
                return Reject(JourneyErrorCode.InvalidState, "The journey has ended.");

            if (command.Type == JourneyCommandType.RequestExit) return Exit(command);
            if (command.Type == JourneyCommandType.Pause) return Pause();
            if (command.Type == JourneyCommandType.Resume) return Resume();
            if (command.Type == JourneyCommandType.ConflictObserved) return ObserveConflict(command.Conflict);
            if (command.Type == JourneyCommandType.SafetyObserved) return ObserveSafety(command.Safety);
            if (command.Type == JourneyCommandType.Retry && states.Current == SessionState.Paused &&
                failedKind == PendingKind.PauseSave) return Retry();
            if (states.Current == SessionState.Paused)
                return Reject(JourneyErrorCode.InvalidState, "Resume before continuing the journey.");
            if (command.Type == JourneyCommandType.Retry) return Retry();
            if (pending != null)
                return Reject(JourneyErrorCode.InvalidState, "A scene or save operation is pending.");
            if (failedAction != null)
                return Reject(JourneyErrorCode.InvalidState, "Retry the failed operation or exit safely.");

            switch (command.Type)
            {
                case JourneyCommandType.CompleteSetup:
                    return AdvancePrerequisite(command, SessionState.Setup, SessionTrigger.CalibrationComplete);
                case JourneyCommandType.AcknowledgeOrientation:
                    return AdvancePrerequisite(command, SessionState.Orientation,
                        SessionTrigger.OrientationAcknowledged);
                case JourneyCommandType.ShowBenchmark:
                    return LoadBenchmark(command.BenchmarkId, false);
                case JourneyCommandType.StartChallenge:
                    return LoadBenchmark(command.BenchmarkId, true);
                case JourneyCommandType.SubmitTaskEvidence:
                    return AcceptEvidence(command.Evidence);
                case JourneyCommandType.ConfirmReorganisation:
                    return ConfirmReorganisation();
                case JourneyCommandType.SkipChallenge:
                    return Skip(command.Reason);
                case JourneyCommandType.SubmitFeedback:
                    return AcceptFeedback(command.Feedback);
                case JourneyCommandType.ConfirmComparison:
                    return ConfirmComparison(command.Comparison);
                case JourneyCommandType.ResetCurrentTask:
                    return SceneChange(JourneyActionType.ResetTask, PendingKind.Reset, null, true);
                case JourneyCommandType.RestoreCurrentStep:
                    return SceneChange(JourneyActionType.RestoreCurrentStep,
                        PendingKind.RestoreStep, null, false);
                case JourneyCommandType.ReturnItemToTray:
                    return SceneChange(JourneyActionType.ReturnItem, PendingKind.Return, command.ItemId, false);
                case JourneyCommandType.ReplayCurrentBenchmark:
                    return SceneChange(JourneyActionType.ReplayBenchmark, PendingKind.Replay, null, true);
                case JourneyCommandType.Recenter:
                    return SceneChange(JourneyActionType.Recenter, PendingKind.Recenter, null, false);
                default:
                    return Reject(JourneyErrorCode.InvalidState, "Unsupported command.");
            }
        }

        private JourneyResult AdvancePrerequisite(JourneyCommand command, SessionState expected,
            SessionTrigger trigger)
        {
            if (states.Current != expected || !command.PrerequisitesMet)
                return Reject(JourneyErrorCode.InvalidState, "Required setup or orientation is incomplete.");
            states.TryAdvance(trigger);
            return Accept();
        }

        private JourneyResult LoadBenchmark(string benchmarkId, bool challenge)
        {
            if (states.Current != SessionState.Explore)
                return Reject(JourneyErrorCode.InvalidState, "Benchmarks can be loaded from Explore only.");
            if (string.IsNullOrWhiteSpace(benchmarkId) || !outcomes.ContainsKey(benchmarkId))
                return Reject(JourneyErrorCode.WrongBenchmark, "Benchmark is not in this session plan.");
            if (challenge && benchmarkId != NextUnresolved())
                return Reject(JourneyErrorCode.WrongBenchmark, "Challenge order or result does not match the plan.");
            var action = NewAction(JourneyActionType.LoadBenchmark, benchmarkId, null,
                challenge ? ++nextRunId : 0);
            return Dispatch(action, challenge ? PendingKind.Start : PendingKind.Show);
        }

        private JourneyResult SceneChange(JourneyActionType type, PendingKind kind, string itemId, bool newRun)
        {
            if (kind == PendingKind.Recenter)
            {
                if (states.Current == SessionState.Setup)
                    return Reject(JourneyErrorCode.InvalidState, "Recenter is unavailable before setup.");
            }
            else if (states.Current != SessionState.ChallengeActive)
                return Reject(JourneyErrorCode.InvalidState, "This task action requires an active challenge.");
            if (kind == PendingKind.Return && FindItem(itemId) == null)
                return Reject(JourneyErrorCode.InvalidEvidence, "The item is not in the task plan.");
            if (kind == PendingKind.Return && (tracker == null ||
                tracker.CurrentStep != ChallengeStep.PutBack))
                return Reject(JourneyErrorCode.InvalidState,
                    "Return to tray is available during PutBack before the task closes.");
            var action = NewAction(type, activeBenchmarkId ?? displayedBenchmarkId, itemId,
                newRun ? ++nextRunId : activeRunId);
            if (kind == PendingKind.RestoreStep) action.Step = tracker?.CurrentStep;
            return Dispatch(action, kind);
        }

        private JourneyResult AcceptEvidence(TaskEvidence evidence)
        {
            if (states.Current != SessionState.ChallengeActive || tracker == null)
                return Reject(JourneyErrorCode.InvalidState, "There is no active challenge.");
            if (evidence == null || evidence.SessionId != plan.SessionId ||
                evidence.BenchmarkId != activeBenchmarkId || evidence.RunId != activeRunId ||
                evidence.Step != tracker.CurrentStep || !Finite(evidence.OccurredAtSessionSeconds) ||
                evidence.OccurredAtSessionSeconds < 0f || !Finite(evidence.Measurement))
                return Reject(JourneyErrorCode.InvalidEvidence, "Evidence identity, run or step is invalid.");

            var task = plan.Task;
            switch (evidence.Step)
            {
                case ChallengeStep.Approach:
                    if (evidence.Role != ReviewRole.CirculationZone || evidence.ZoneId != task.ApproachZoneId)
                        return InvalidEvidence("XR Rig has not reached the approach zone.");
                    tracker.Advance();
                    break;
                case ChallengeStep.Open:
                    if (!AccessEvidence(evidence, true, opened))
                        return InvalidEvidence("A registered access target has not physically opened.");
                    if (opened.Count == task.AccessTargets.Count) tracker.Advance();
                    break;
                case ChallengeStep.TakeOut:
                    var oldItem = FindItem(evidence.ItemId);
                    if (oldItem == null || !oldItem.IsExistingItem ||
                        evidence.Role != ReviewRole.StorageZone || evidence.ZoneId != oldItem.SourceZoneId ||
                        !taken.Add(oldItem.ItemId))
                        return InvalidEvidence("An existing item must leave its source zone once.");
                    if (taken.Count == ExistingItemCount()) tracker.Advance();
                    break;
                case ChallengeStep.TemporaryPlacement:
                    var temporaryItem = FindItem(evidence.ItemId);
                    if (temporaryItem == null || !temporaryItem.IsExistingItem ||
                        !taken.Contains(temporaryItem.ItemId) || evidence.Role != ReviewRole.CounterSurface ||
                        evidence.ZoneId != temporaryItem.TemporaryZoneId || !evidence.Released ||
                        !placed.Add(temporaryItem.ItemId))
                        return InvalidEvidence("An existing item must be released in its temporary zone once.");
                    if (placed.Count == ExistingItemCount()) tracker.Advance();
                    break;
                case ChallengeStep.Reorganise:
                    var movedItem = FindItem(evidence.ItemId);
                    if (movedItem == null || !movedItem.IsExistingItem ||
                        !placed.Contains(movedItem.ItemId) || evidence.Role != ReviewRole.StorageZone ||
                        evidence.ZoneId != movedItem.ReturnZoneId || !evidence.Released ||
                        !reorganised.Add(movedItem.ItemId))
                        return InvalidEvidence("A placed item must be moved into registered storage.");
                    break;
                case ChallengeStep.PutBack:
                    var returnItem = FindItem(evidence.ItemId);
                    if (returnItem == null || evidence.Role != ReviewRole.StorageZone ||
                        evidence.ZoneId != returnItem.ReturnZoneId || !evidence.Released ||
                        (returnItem.IsExistingItem && !placed.Contains(returnItem.ItemId)) ||
                        !returned.Add(returnItem.ItemId))
                        return InvalidEvidence("Each task item must be released in its return zone once.");
                    if (returned.Count == task.Items.Count) tracker.Advance();
                    break;
                case ChallengeStep.Close:
                    if (!AccessEvidence(evidence, false, closed))
                        return InvalidEvidence("A registered access target has not physically closed.");
                    if (closed.Count == task.AccessTargets.Count) tracker.Advance();
                    break;
                case ChallengeStep.Leave:
                    if (evidence.Role != ReviewRole.CirculationZone || evidence.ZoneId != task.LeaveZoneId)
                        return InvalidEvidence("XR Rig has not reached the leave zone.");
                    tracker.Advance();
                    outcomes[activeBenchmarkId] = TaskOutcome.Completed;
                    states.TryAdvance(SessionTrigger.ChallengeTaskFinished);
                    break;
                default:
                    return InvalidEvidence("Unknown challenge step.");
            }
            record.totalSeconds = Math.Max(record.totalSeconds, evidence.OccurredAtSessionSeconds);
            return Accept();
        }

        private bool AccessEvidence(TaskEvidence evidence, bool opening, HashSet<string> counted)
        {
            TaskAccessSpec target = null;
            foreach (var item in plan.Task.AccessTargets)
                if (item.ObjectId == evidence.ObjectId) { target = item; break; }
            if (target == null || target.Role != evidence.Role || counted.Contains(target.ObjectId)) return false;
            if (evidence.Measurement < 0f || evidence.Measurement > 1f) return false;
            if (opening ? evidence.Measurement < plan.Task.OpenProgressThreshold :
                evidence.Measurement > plan.Task.ClosedProgressThreshold) return false;
            return counted.Add(target.ObjectId);
        }

        private JourneyResult ConfirmReorganisation()
        {
            if (states.Current != SessionState.ChallengeActive || tracker == null ||
                tracker.CurrentStep != ChallengeStep.Reorganise || reorganised.Count == 0)
                return Reject(JourneyErrorCode.InvalidEvidence,
                    "Move and release an existing task item in the storage zone before confirming; new-purchase items do not count.");
            tracker.Advance();
            return Accept();
        }

        private JourneyResult Skip(string reason)
        {
            if (states.Current != SessionState.ChallengeActive || string.IsNullOrWhiteSpace(reason))
                return Reject(JourneyErrorCode.InvalidState, "An active challenge and skip reason are required.");
            skippedReason = reason.Trim();
            outcomes[activeBenchmarkId] = TaskOutcome.Skipped;
            states.TryAdvance(SessionTrigger.ChallengeTaskSkipped);
            record.comfortOrAbort.Add(new TimedEvent
            {
                type = "challenge_skipped",
                combinationId = activeBenchmarkId,
                sessionSeconds = record.totalSeconds
            });
            return Accept();
        }

        private JourneyResult AcceptFeedback(FeedbackValidationResult result)
        {
            if (states.Current != SessionState.FeedbackPending || feedbackRecorded)
                return Reject(JourneyErrorCode.InvalidState, "Feedback is not pending for this benchmark.");
            if (result == null || result.SessionId != plan.SessionId ||
                result.BenchmarkId != activeBenchmarkId || !result.IsValid || result.Feedback == null)
                return Reject(JourneyErrorCode.InvalidFeedback, FeedbackError(result));
            var feedback = CopyFeedback(result.Feedback);
            if (!string.IsNullOrEmpty(feedback.combinationId) && feedback.combinationId != activeBenchmarkId)
                return Reject(JourneyErrorCode.InvalidFeedback, "Feedback benchmark differs from the active one.");
            feedback.combinationId = activeBenchmarkId;
            feedback.taskOutcome = outcomes[activeBenchmarkId];
            feedback.outcomeReason = feedback.taskOutcome == TaskOutcome.Skipped ? skippedReason : null;
            if (feedback.taskOutcome == TaskOutcome.Completed &&
                (feedback.decision == CombinationDecision.Unset || feedback.category == FeedbackCategory.Unset ||
                 feedback.importance == Importance.Unset))
                return Reject(JourneyErrorCode.InvalidFeedback,
                    "Decision, category and importance are required.");
            record.benchmarkFeedback.Add(feedback);
            feedbackRecorded = true;
            foreach (var pair in conflicts)
            {
                if (pair.Value.BenchmarkId == activeBenchmarkId) pair.Value.FeedbackRecorded = true;
            }
            SyncConflictRecords();
            return Dispatch(NewSaveAction(), PendingKind.FeedbackSave);
        }

        private JourneyResult ConfirmComparison(FinalComparison comparison)
        {
            if (states.Current != SessionState.ComparisonSummary || resolved.Count != plan.BenchmarkIds.Count)
                return Reject(JourneyErrorCode.InvalidState, "All planned benchmarks must be saved first.");
            if (comparison == null)
                return Reject(JourneyErrorCode.InvalidFeedback, "Final comparison is required.");
            var anyCompleted = false;
            var anySkipped = false;
            foreach (var outcome in outcomes.Values)
            {
                anyCompleted |= outcome == TaskOutcome.Completed;
                anySkipped |= outcome == TaskOutcome.Skipped;
            }
            if (anyCompleted && (string.IsNullOrWhiteSpace(comparison.PreferredBenchmarkId) ||
                !outcomes.ContainsKey(comparison.PreferredBenchmarkId) ||
                outcomes[comparison.PreferredBenchmarkId] != TaskOutcome.Completed ||
                string.IsNullOrWhiteSpace(comparison.TradeOffSummary)))
                return Reject(JourneyErrorCode.InvalidFeedback,
                    "Choose a completed benchmark and describe the trade-off.");
            if (!anyCompleted && !string.IsNullOrWhiteSpace(comparison.PreferredBenchmarkId))
                return Reject(JourneyErrorCode.InvalidFeedback, "All benchmarks were skipped; no preference is eligible.");
            record.finalPreferenceCombinationId = comparison.PreferredBenchmarkId;
            record.finalTradeOffSummary = comparison.TradeOffSummary;
            record.sessionOutcome = anySkipped ? SessionOutcome.Incomplete : SessionOutcome.Completed;
            return Dispatch(NewSaveAction(), PendingKind.ComparisonSave);
        }

        private JourneyResult Pause()
        {
            if (!states.TryAdvance(SessionTrigger.PauseRequested))
                return Reject(JourneyErrorCode.InvalidState, states.LastRejection);
            record.assistanceEvents.Add(new TimedEvent
            {
                type = "pause", combinationId = activeBenchmarkId, sessionSeconds = record.totalSeconds
            });
            if (pending != null)
            {
                pauseSaveAfterPending = true;
                return Accept();
            }
            if (failedAction != null)
            {
                retainedFailure = failedAction;
                retainedFailureKind = failedKind;
                failedAction = null;
                failedKind = PendingKind.None;
            }
            return Dispatch(NewSaveAction(), PendingKind.PauseSave);
        }

        private JourneyResult Resume()
        {
            if (states.Current != SessionState.Paused)
                return Reject(JourneyErrorCode.InvalidState, "The journey is not paused.");
            if (pending != null)
                return Reject(JourneyErrorCode.InvalidState, "Wait for the pending scene or save result.");
            if (failedKind == PendingKind.PauseSave && retainedFailure != null)
                RestoreRetainedFailure();
            states.TryAdvance(SessionTrigger.ResumeRequested);
            if (triggerOnResume.HasValue)
            {
                states.TryAdvance(triggerOnResume.Value);
                if (triggerOnResume == SessionTrigger.FeedbackSubmitted && states.AllBenchmarksResolved)
                    states.TryAdvance(SessionTrigger.PreferenceConfirmed);
                triggerOnResume = null;
            }
            pauseSaveAfterPending = false;
            return Accept();
        }

        private JourneyResult Exit(JourneyCommand command)
        {
            pending = null;
            pendingKind = PendingKind.None;
            failedAction = null;
            failedKind = PendingKind.None;
            retainedFailure = null;
            retainedFailureKind = PendingKind.None;
            pauseSaveAfterPending = false;
            triggerOnResume = null;
            states.TryAdvance(SessionTrigger.ExitRequested);
            record.sessionOutcome = SessionOutcome.Aborted;
            record.comfortOrAbort.Add(new TimedEvent
            {
                type = "exit", combinationId = activeBenchmarkId, sessionSeconds = record.totalSeconds
            });
            return Dispatch(NewSaveAction(), PendingKind.ExitSave);
        }

        private JourneyResult Retry()
        {
            if (pending != null || failedAction == null)
                return Reject(JourneyErrorCode.InvalidState, "There is no failed operation to retry.");
            var retry = NewAction(failedAction.Type, failedAction.BenchmarkId, failedAction.ItemId,
                (failedAction.Type == JourneyActionType.LoadBenchmark &&
                 failedKind != PendingKind.Show) ||
                failedAction.Type == JourneyActionType.ResetTask ||
                failedAction.Type == JourneyActionType.ReplayBenchmark
                    ? ++nextRunId : failedAction.RunId);
            retry.Step = failedAction.Step;
            if (retry.Type == JourneyActionType.SaveRecord) retry.Record = CopyRecord(record);
            return Dispatch(retry, failedKind);
        }

        private JourneyResult ObserveConflict(ConflictNotice notice)
        {
            if (notice == null || notice.SessionId != plan.SessionId ||
                !outcomes.ContainsKey(notice.BenchmarkId) || notice.ModelVersion != plan.ModelVersion ||
                string.IsNullOrWhiteSpace(notice.IssueId) || string.IsNullOrWhiteSpace(notice.ObjectId) ||
                string.IsNullOrWhiteSpace(notice.AssumptionKey) || !Finite(notice.Measurement) ||
                !Finite(notice.OccurredAtSessionSeconds) || notice.OccurredAtSessionSeconds < 0f)
                return Reject(JourneyErrorCode.InvalidEvidence, "Conflict identity or measurement is invalid.");
            var key = notice.BenchmarkId + "\u001f" + notice.IssueId;
            if (conflicts.ContainsKey(key)) return Accept();
            var copy = CopyConflict(notice);
            copy.FeedbackRecorded = HasFeedback(notice.BenchmarkId);
            conflicts.Add(key, copy);
            record.totalSeconds = Math.Max(record.totalSeconds, Math.Max(0f, notice.OccurredAtSessionSeconds));
            SyncConflictRecords();
            return Accept();
        }

        private JourneyResult ObserveSafety(SafetyNotice notice)
        {
            if (notice == null || notice.SessionId != plan.SessionId ||
                string.IsNullOrWhiteSpace(notice.SafetyEventId))
                return Reject(JourneyErrorCode.InvalidEvidence, "Safety notice identity is invalid.");
            if (notice.IsActive) currentSafety = CopySafety(notice);
            else if (currentSafety != null && currentSafety.SafetyEventId == notice.SafetyEventId)
                currentSafety = null;
            return Accept();
        }

        private JourneyResult CompleteCallback(JourneyCommand command)
        {
            var scene = command.Type == JourneyCommandType.SceneCompleted;
            var requestId = scene ? command.SceneResult == null ? 0 : command.SceneResult.RequestId
                : command.SaveResult == null ? 0 : command.SaveResult.RequestId;
            if (pending == null || pending.RequestId != requestId ||
                (pending.Type == JourneyActionType.SaveRecord) == scene)
                return Reject(JourneyErrorCode.StaleResult, "The callback does not match the current request.");
            var action = pending;
            var kind = pendingKind;
            pending = null;
            pendingKind = PendingKind.None;
            return scene ? CompleteScene(command.SceneResult, action, kind)
                : CompleteSave(command.SaveResult, action, kind);
        }

        private JourneyResult CompleteScene(SceneActionResult result, JourneyAction action, PendingKind kind)
        {
            var expected = action.BenchmarkId;
            if (!result.Succeeded || (!string.IsNullOrEmpty(expected) && result.ActualBenchmarkId != expected))
                return Fail(action, kind, !string.IsNullOrWhiteSpace(result.Message)
                    ? result.Message : "Scene action failed or loaded the wrong benchmark.");
            failedAction = null;
            failedKind = PendingKind.None;
            switch (kind)
            {
                case PendingKind.Show:
                    displayedBenchmarkId = expected;
                    break;
                case PendingKind.Start:
                    displayedBenchmarkId = expected;
                    activeBenchmarkId = expected;
                    activeRunId = action.RunId;
                    ResetTaskProgress();
                    AdvanceOrDefer(SessionTrigger.ChallengeRequested);
                    break;
                case PendingKind.Reset:
                case PendingKind.Replay:
                    activeRunId = action.RunId;
                    ResetTaskProgress();
                    record.assistanceEvents.Add(new TimedEvent
                    {
                        type = kind == PendingKind.Reset ? "task_reset" : "benchmark_replay",
                        combinationId = activeBenchmarkId, sessionSeconds = record.totalSeconds
                    });
                    break;
                case PendingKind.Return:
                    returned.Remove(action.ItemId);
                    record.assistanceEvents.Add(new TimedEvent
                    {
                        type = "return_item", combinationId = activeBenchmarkId,
                        sessionSeconds = record.totalSeconds
                    });
                    break;
                case PendingKind.RestoreStep:
                    ClearCurrentStepProgress(action.Step);
                    record.assistanceEvents.Add(new TimedEvent
                    {
                        type = "step_restore", combinationId = activeBenchmarkId,
                        sessionSeconds = record.totalSeconds
                    });
                    break;
                case PendingKind.Recenter:
                    record.assistanceEvents.Add(new TimedEvent
                    {
                        type = "recenter", combinationId = activeBenchmarkId,
                        sessionSeconds = record.totalSeconds
                    });
                    break;
            }
            return SaveAfterPendingPauseOrAccept();
        }

        private JourneyResult CompleteSave(SaveResult result, JourneyAction action, PendingKind kind)
        {
            if (!result.Succeeded || !result.ReadBackVerified)
            {
                saveStatus = SaveStatus.Failed;
                return Fail(action, kind, !string.IsNullOrWhiteSpace(result.Message)
                    ? result.Message : "Local record was not written and read back successfully.");
            }
            saveStatus = SaveStatus.Verified;
            if (kind == PendingKind.PauseSave && retainedFailure != null)
                RestoreRetainedFailure();
            else
            {
                failedAction = null;
                failedKind = PendingKind.None;
            }
            if (kind == PendingKind.FeedbackSave)
            {
                resolved.Add(activeBenchmarkId);
                AdvanceOrDefer(SessionTrigger.FeedbackSubmitted);
                if (states.Current != SessionState.Paused && states.AllBenchmarksResolved)
                    states.TryAdvance(SessionTrigger.PreferenceConfirmed);
                activeBenchmarkId = null;
                tracker = null;
                feedbackRecorded = false;
                skippedReason = null;
            }
            else if (kind == PendingKind.ComparisonSave)
                AdvanceOrDefer(SessionTrigger.PreferenceConfirmed);
            return SaveAfterPendingPauseOrAccept();
        }

        private void AdvanceOrDefer(SessionTrigger trigger)
        {
            if (states.Current == SessionState.Paused) triggerOnResume = trigger;
            else states.TryAdvance(trigger);
        }

        private JourneyResult SaveAfterPendingPauseOrAccept()
        {
            if (states.Current == SessionState.Paused && pauseSaveAfterPending)
            {
                pauseSaveAfterPending = false;
                return Dispatch(NewSaveAction(), PendingKind.PauseSave);
            }
            return Accept();
        }

        private JourneyResult Fail(JourneyAction action, PendingKind kind, string fallbackMessage)
        {
            failedAction = action;
            failedKind = kind;
            if (states.Current == SessionState.Paused && pauseSaveAfterPending)
            {
                retainedFailure = action;
                retainedFailureKind = kind;
                failedAction = null;
                failedKind = PendingKind.None;
                pauseSaveAfterPending = false;
                var pauseResult = Dispatch(NewSaveAction(), PendingKind.PauseSave);
                pauseResult.ErrorCode = JourneyErrorCode.OperationFailed;
                pauseResult.Message = fallbackMessage;
                return pauseResult;
            }
            pauseSaveAfterPending = false;
            return new JourneyResult
            {
                Accepted = true,
                ErrorCode = JourneyErrorCode.OperationFailed,
                Message = fallbackMessage,
                Snapshot = MakeSnapshot()
            };
        }

        private void RestoreRetainedFailure()
        {
            failedAction = retainedFailure;
            failedKind = retainedFailureKind;
            retainedFailure = null;
            retainedFailureKind = PendingKind.None;
            if (failedKind == PendingKind.FeedbackSave || failedKind == PendingKind.ComparisonSave ||
                failedKind == PendingKind.PauseSave)
                saveStatus = SaveStatus.Failed;
        }

        private JourneyAction NewAction(JourneyActionType type, string benchmarkId, string itemId, long runId)
        {
            return new JourneyAction
            {
                RequestId = ++nextRequestId, RunId = runId, Type = type,
                SessionId = plan.SessionId, BenchmarkId = benchmarkId, ItemId = itemId
            };
        }

        private JourneyAction NewSaveAction()
        {
            var action = NewAction(JourneyActionType.SaveRecord, activeBenchmarkId, null, activeRunId);
            action.Record = CopyRecord(record);
            return action;
        }

        private JourneyResult Dispatch(JourneyAction action, PendingKind kind)
        {
            pending = action;
            pendingKind = kind;
            if (action.Type == JourneyActionType.SaveRecord) saveStatus = SaveStatus.Pending;
            return Accept(action);
        }

        private JourneyResult Accept(JourneyAction action = null)
        {
            return new JourneyResult
            {
                Accepted = true, ErrorCode = JourneyErrorCode.None,
                Message = string.Empty, Snapshot = MakeSnapshot(), Action = action
            };
        }

        private JourneyResult Reject(JourneyErrorCode code, string message)
        {
            return new JourneyResult
            {
                Accepted = false, ErrorCode = code, Message = message, Snapshot = MakeSnapshot()
            };
        }

        private JourneyResult InvalidEvidence(string message) => Reject(JourneyErrorCode.InvalidEvidence, message);

        private void ResetTaskProgress()
        {
            tracker = new ChallengeTaskTracker(plan.Task.Items.Count);
            opened.Clear(); closed.Clear(); taken.Clear(); placed.Clear();
            reorganised.Clear(); returned.Clear();
        }

        private void ClearCurrentStepProgress(ChallengeStep? step)
        {
            if (!step.HasValue || tracker == null || tracker.CurrentStep != step) return;
            switch (step.Value)
            {
                case ChallengeStep.Open: opened.Clear(); break;
                case ChallengeStep.TakeOut: taken.Clear(); break;
                case ChallengeStep.TemporaryPlacement: placed.Clear(); break;
                case ChallengeStep.Reorganise: reorganised.Clear(); break;
                case ChallengeStep.PutBack: returned.Clear(); break;
                case ChallengeStep.Close: closed.Clear(); break;
            }
        }

        private string NextUnresolved()
        {
            foreach (var benchmarkId in plan.BenchmarkIds)
                if (!resolved.Contains(benchmarkId)) return benchmarkId;
            return null;
        }

        private TaskItemSpec FindItem(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            foreach (var item in plan.Task.Items) if (item.ItemId == id) return item;
            return null;
        }

        private int ExistingItemCount()
        {
            var count = 0;
            foreach (var item in plan.Task.Items) if (item.IsExistingItem) count++;
            return count;
        }

        private bool HasFeedback(string benchmarkId)
        {
            return record.FindFeedback(benchmarkId) != null;
        }

        private void SyncConflictRecords()
        {
            record.conflicts.Clear();
            foreach (var notice in conflicts.Values)
                record.conflicts.Add(new ConflictRecord
                {
                    issueId = notice.IssueId, combinationId = notice.BenchmarkId,
                    modelVersion = notice.ModelVersion, objectId = notice.ObjectId,
                    role = notice.Role, assumptionKey = notice.AssumptionKey,
                    measurement = notice.Measurement,
                    sessionSeconds = notice.OccurredAtSessionSeconds,
                    message = notice.Message, feedbackRecorded = notice.FeedbackRecorded
                });
        }

        private JourneySnapshot MakeSnapshot()
        {
            var order = plan != null && plan.BenchmarkIds != null
                ? new List<string>(plan.BenchmarkIds) : new List<string>();
            var feedback = new List<BenchmarkFeedback>();
            foreach (var entry in record.benchmarkFeedback) feedback.Add(CopyFeedback(entry));
            var notices = new List<ConflictNotice>();
            foreach (var notice in conflicts.Values) notices.Add(CopyConflict(notice));
            var benchmarkOutcomes = new Dictionary<string, TaskOutcome>(outcomes);
            var current = tracker == null ? (ChallengeStep?)null : tracker.CurrentStep;
            var total = current == ChallengeStep.TakeOut || current == ChallengeStep.TemporaryPlacement
                ? ExistingItemCount() : current == ChallengeStep.PutBack ? plan.Task.Items.Count : 0;
            var completed = current == ChallengeStep.TakeOut ? taken.Count
                : current == ChallengeStep.TemporaryPlacement ? placed.Count
                : current == ChallengeStep.PutBack ? returned.Count : 0;
            return new JourneySnapshot
            {
                SessionId = plan == null ? null : plan.SessionId,
                State = states.Current, Outcome = record.sessionOutcome, Save = saveStatus,
                DisplayedBenchmarkId = displayedBenchmarkId, ActiveBenchmarkId = activeBenchmarkId,
                ActiveRunId = activeRunId, CurrentStep = current,
                CompletedItemCount = completed, TotalItemCount = total,
                IsBusy = pending != null,
                BenchmarkOrder = order.AsReadOnly(),
                BenchmarkOutcomes = new ReadOnlyDictionary<string, TaskOutcome>(benchmarkOutcomes),
                Feedback = feedback.AsReadOnly(), Conflicts = notices.AsReadOnly(),
                CurrentSafety = CopySafety(currentSafety)
            };
        }

        private static bool IsCallback(JourneyCommandType type) =>
            type == JourneyCommandType.SceneCompleted || type == JourneyCommandType.SaveCompleted;

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static string FeedbackError(FeedbackValidationResult result)
        {
            if (result == null || result.FieldErrors == null || result.FieldErrors.Count == 0)
                return "Feedback validation failed or identity differs.";
            return "Feedback validation failed: " + string.Join(", ",
                result.FieldErrors.Select(pair => pair.Key + "=" + pair.Value));
        }

        private static string ValidatePlan(SessionPlan p)
        {
            if (p == null || string.IsNullOrWhiteSpace(p.SessionId) ||
                string.IsNullOrWhiteSpace(p.ModelVersion)) return "Session and model IDs are required.";
            if (p.BenchmarkIds == null || p.BenchmarkIds.Count != 2 ||
                !p.BenchmarkIds.Contains("B1") || !p.BenchmarkIds.Contains("B2") ||
                p.BenchmarkIds[0] == p.BenchmarkIds[1])
                return "P0 requires B1 and B2 exactly once in the chosen order.";
            if (p.Task == null || p.Task.Items == null ||
                !ChallengeTaskTracker.IsValidItemCount(p.Task.Items.Count))
                return "Challenge requires 3-5 total task items.";
            if (string.IsNullOrWhiteSpace(p.Task.ApproachZoneId) ||
                string.IsNullOrWhiteSpace(p.Task.LeaveZoneId) ||
                p.Task.ApproachZoneId == p.Task.LeaveZoneId)
                return "Distinct approach and leave zone IDs are required.";
            if (!Finite(p.Task.OpenProgressThreshold) || !Finite(p.Task.ClosedProgressThreshold) ||
                p.Task.ClosedProgressThreshold < 0f || p.Task.OpenProgressThreshold > 1f ||
                p.Task.OpenProgressThreshold <= p.Task.ClosedProgressThreshold)
                return "Access progress thresholds must be ordered within 0-1.";
            var ids = new HashSet<string>();
            var hasOld = false;
            var hasNew = false;
            foreach (var item in p.Task.Items)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.ItemId) || !ids.Add(item.ItemId) ||
                    string.IsNullOrWhiteSpace(item.ReturnZoneId) ||
                    (item.IsExistingItem && (string.IsNullOrWhiteSpace(item.SourceZoneId) ||
                        string.IsNullOrWhiteSpace(item.TemporaryZoneId))))
                    return "Task items need unique IDs and registered source, temporary and return zones.";
                hasOld |= item.IsExistingItem;
                hasNew |= !item.IsExistingItem;
            }
            if (!hasOld || !hasNew) return "Task requires at least one existing and one new item.";
            if (p.Task.AccessTargets == null || p.Task.AccessTargets.Count == 0)
                return "At least one access target is required.";
            ids.Clear();
            foreach (var access in p.Task.AccessTargets)
            {
                if (access == null || string.IsNullOrWhiteSpace(access.ObjectId) ||
                    !ids.Add(access.ObjectId) ||
                    (access.Role != ReviewRole.FridgeDoor && access.Role != ReviewRole.FridgeDrawer))
                    return "Access targets need unique door or drawer IDs.";
            }
            return null;
        }

        private static SessionPlan CopyPlan(SessionPlan source)
        {
            if (source == null) return null;
            var copy = new SessionPlan
            {
                SessionId = source.SessionId, ModelVersion = source.ModelVersion,
                BenchmarkIds = source.BenchmarkIds == null ? null : new List<string>(source.BenchmarkIds)
            };
            if (source.Task == null) return copy;
            var task = source.Task;
            var items = new List<TaskItemSpec>();
            if (task.Items != null)
                foreach (var item in task.Items)
                    items.Add(item == null ? null : new TaskItemSpec
                    {
                        ItemId = item.ItemId, IsExistingItem = item.IsExistingItem,
                        SourceZoneId = item.SourceZoneId, TemporaryZoneId = item.TemporaryZoneId,
                        ReturnZoneId = item.ReturnZoneId
                    });
            var access = new List<TaskAccessSpec>();
            if (task.AccessTargets != null)
                foreach (var target in task.AccessTargets)
                    access.Add(target == null ? null : new TaskAccessSpec
                    { ObjectId = target.ObjectId, Role = target.Role });
            copy.Task = new TaskConfig
            {
                Items = task.Items == null ? null : items,
                AccessTargets = task.AccessTargets == null ? null : access,
                ApproachZoneId = task.ApproachZoneId, LeaveZoneId = task.LeaveZoneId,
                OpenProgressThreshold = task.OpenProgressThreshold,
                ClosedProgressThreshold = task.ClosedProgressThreshold
            };
            return copy;
        }

        private static BenchmarkFeedback CopyFeedback(BenchmarkFeedback f)
        {
            if (f == null) return null;
            return new BenchmarkFeedback
            {
                combinationId = f.combinationId, decision = f.decision, category = f.category,
                experience = f.experience, everydayImpact = f.everydayImpact,
                importance = f.importance, discoveryMode = f.discoveryMode,
                taskOutcome = f.taskOutcome, shortExplanation = f.shortExplanation,
                outcomeReason = f.outcomeReason
            };
        }

        private static ConflictNotice CopyConflict(ConflictNotice n)
        {
            if (n == null) return null;
            return new ConflictNotice
            {
                IssueId = n.IssueId, SessionId = n.SessionId, BenchmarkId = n.BenchmarkId,
                ModelVersion = n.ModelVersion, ObjectId = n.ObjectId, Role = n.Role,
                AssumptionKey = n.AssumptionKey, Measurement = n.Measurement,
                OccurredAtSessionSeconds = n.OccurredAtSessionSeconds, Message = n.Message,
                FeedbackRecorded = n.FeedbackRecorded
            };
        }

        private static SafetyNotice CopySafety(SafetyNotice n)
        {
            if (n == null) return null;
            return new SafetyNotice
            {
                SafetyEventId = n.SafetyEventId, SessionId = n.SessionId,
                RiskType = n.RiskType, IsActive = n.IsActive,
                OccurredAtSessionSeconds = n.OccurredAtSessionSeconds,
                Message = n.Message, SuggestedAction = n.SuggestedAction
            };
        }

        private static TimedEvent CopyEvent(TimedEvent e)
        {
            return e == null ? null : new TimedEvent
            { type = e.type, combinationId = e.combinationId, sessionSeconds = e.sessionSeconds };
        }

        private static SessionRecord CopyRecord(SessionRecord source)
        {
            var copy = new SessionRecord
            {
                sessionId = source.sessionId, modelVersion = source.modelVersion,
                finalPreferenceCombinationId = source.finalPreferenceCombinationId,
                finalTradeOffSummary = source.finalTradeOffSummary,
                sessionOutcome = source.sessionOutcome, totalSeconds = source.totalSeconds
            };
            copy.benchmarkOrder.AddRange(source.benchmarkOrder);
            foreach (var feedback in source.benchmarkFeedback)
                copy.benchmarkFeedback.Add(CopyFeedback(feedback));
            foreach (var marker in source.markers)
                copy.markers.Add(marker == null ? null : new MarkerRecord
                {
                    markerId = marker.markerId, combinationId = marker.combinationId,
                    category = marker.category, targetName = marker.targetName,
                    x = marker.x, y = marker.y, z = marker.z,
                    everydayImpactNote = marker.everydayImpactNote
                });
            foreach (var e in source.promptEvents) copy.promptEvents.Add(CopyEvent(e));
            foreach (var e in source.assistanceEvents) copy.assistanceEvents.Add(CopyEvent(e));
            foreach (var e in source.comfortOrAbort) copy.comfortOrAbort.Add(CopyEvent(e));
            foreach (var c in source.conflicts)
                copy.conflicts.Add(c == null ? null : new ConflictRecord
                {
                    issueId = c.issueId, combinationId = c.combinationId,
                    modelVersion = c.modelVersion, objectId = c.objectId,
                    role = c.role, assumptionKey = c.assumptionKey, measurement = c.measurement,
                    sessionSeconds = c.sessionSeconds, message = c.message,
                    feedbackRecorded = c.feedbackRecorded
                });
            return copy;
        }
    }
}
