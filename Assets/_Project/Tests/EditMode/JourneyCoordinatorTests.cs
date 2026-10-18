using System.Collections.Generic;
using NUnit.Framework;
using XRApartment.Data;
using XRApartment.Simulation;

namespace XRApartment.Tests
{
    public class JourneyCoordinatorTests
    {
        private const string SessionId = "anonymous-test-session";

        [TestCase(2)]
        [TestCase(6)]
        public void Plan_RejectsItemCountOutsideThreeToFiveTotal(int itemCount)
        {
            var plan = Plan();
            var items = new List<TaskItemSpec>();
            for (var i = 0; i < itemCount; i++) items.Add(Item("item-" + i, i != itemCount - 1));
            plan.Task.Items = items;
            var core = new JourneyCoordinator(plan);
            Assert.IsFalse(core.HasValidPlan);
            StringAssert.Contains("3-5", core.PlanError);
        }

        [Test]
        public void Plan_RequiresExistingAndNewItems_AndCopiesInput()
        {
            var plan = Plan();
            var core = new JourneyCoordinator(plan);
            Assert.IsTrue(core.HasValidPlan, core.PlanError);
            ((List<string>)plan.BenchmarkIds)[0] = "B3";
            ((List<TaskItemSpec>)plan.Task.Items)[0].ItemId = "changed";
            CollectionAssert.AreEqual(new[] { "B1", "B2" }, core.Snapshot.BenchmarkOrder);

            var invalid = Plan();
            invalid.Task.Items = new List<TaskItemSpec>
            { Item("old-a", true), Item("old-b", true), Item("old-c", true) };
            Assert.IsFalse(new JourneyCoordinator(invalid).HasValidPlan);
        }

        [Test]
        public void NormalB1B2_FinishesOnlyAfterVerifiedFinalSave()
        {
            var core = ReadyCore();
            CompleteBenchmark(core, "B1");
            SaveFeedback(core, "B1", true);
            Assert.AreEqual(SessionState.Explore, core.Snapshot.State);

            CompleteBenchmark(core, "B2");
            SaveFeedback(core, "B2", true);
            Assert.AreEqual(SessionState.ComparisonSummary, core.Snapshot.State);

            var comparison = core.Submit(Command(JourneyCommandType.ConfirmComparison,
                comparison: new FinalComparison
                { PreferredBenchmarkId = "B2", TradeOffSummary = "B2 keeps a clearer path." }));
            Assert.IsTrue(comparison.Accepted);
            Assert.AreEqual(SessionState.ComparisonSummary, comparison.Snapshot.State);
            Assert.AreEqual(SessionOutcome.Completed, comparison.Snapshot.Outcome);
            Assert.AreEqual(SaveStatus.Pending, comparison.Snapshot.Save);
            Assert.AreEqual(SessionOutcome.Completed, comparison.Action.Record.sessionOutcome);

            var verified = CompleteSave(core, comparison.Action, true);
            Assert.AreEqual(SessionState.Finished, verified.Snapshot.State);
            Assert.AreEqual(SaveStatus.Verified, verified.Snapshot.Save);
        }

        [Test]
        public void FinalSaveFailure_LeavesComparisonOpenForRetry()
        {
            var core = ReadyCore();
            CompleteBenchmark(core, "B1"); SaveFeedback(core, "B1", true);
            CompleteBenchmark(core, "B2"); SaveFeedback(core, "B2", true);
            var comparison = core.Submit(Command(JourneyCommandType.ConfirmComparison,
                comparison: new FinalComparison
                { PreferredBenchmarkId = "B1", TradeOffSummary = "Trade-off noted." }));
            var failed = CompleteSave(core, comparison.Action, false);
            Assert.AreEqual(SessionState.ComparisonSummary, failed.Snapshot.State);
            Assert.AreEqual(SaveStatus.Failed, failed.Snapshot.Save);
            var retry = core.Submit(Command(JourneyCommandType.Retry));
            Assert.Greater(retry.Action.RequestId, comparison.Action.RequestId);
            Assert.AreEqual(SessionState.Finished, CompleteSave(core, retry.Action, true).Snapshot.State);
        }

        [Test]
        public void SafeExit_ImmediatelyAbortsAndOldSceneCallbackIsStale()
        {
            var core = ReadyCore();
            var start = core.Submit(Command(JourneyCommandType.StartChallenge, "B1"));
            var exit = core.Submit(Command(JourneyCommandType.RequestExit, reason: "comfort"));
            Assert.IsTrue(exit.Accepted);
            Assert.AreEqual(SessionState.Exited, exit.Snapshot.State);
            Assert.AreEqual(SessionOutcome.Aborted, exit.Snapshot.Outcome);
            Assert.AreEqual(SessionOutcome.Aborted, exit.Action.Record.sessionOutcome);
            Assert.AreEqual(JourneyActionType.SaveRecord, exit.Action.Type);

            var late = CompleteScene(core, start.Action, "B1", true);
            Assert.IsFalse(late.Accepted);
            Assert.AreEqual(JourneyErrorCode.StaleResult, late.ErrorCode);
            Assert.AreEqual(SessionState.Exited, CompleteSave(core, exit.Action, true).Snapshot.State);
        }

        [Test]
        public void SkippedBenchmarks_ProduceIncompleteOutcomeWithoutPreference()
        {
            var core = ReadyCore();
            SkipAndSave(core, "B1");
            SkipAndSave(core, "B2");
            Assert.AreEqual(SessionState.ComparisonSummary, core.Snapshot.State);
            var comparison = core.Submit(Command(JourneyCommandType.ConfirmComparison,
                comparison: new FinalComparison()));
            Assert.IsTrue(comparison.Accepted);
            Assert.AreEqual(SessionOutcome.Incomplete, comparison.Snapshot.Outcome);
            Assert.AreEqual(TaskOutcome.Skipped,
                comparison.Action.Record.FindFeedback("B1").taskOutcome);
            Assert.AreEqual("comfort stop",
                comparison.Action.Record.FindFeedback("B1").outcomeReason);
            Assert.AreEqual(SessionOutcome.Incomplete,
                CompleteSave(core, comparison.Action, true).Snapshot.Outcome);
        }

        [Test]
        public void PendingSkipReason_IsAvailableOnlyWhileSkippedFeedbackIsPending()
        {
            var core = ReadyCore();
            Start(core, "B1");
            var skipped = core.Submit(Command(JourneyCommandType.SkipChallenge, reason: "  comfort stop  "));

            Assert.IsTrue(skipped.Accepted, skipped.Message);
            Assert.AreEqual("comfort stop", core.PendingSkipReason);

            core.Submit(Command(JourneyCommandType.RequestExit, reason: "participant ended session"));
            Assert.IsNull(core.PendingSkipReason);
        }

        [Test]
        public void ValidFeedbackTurnsConflictAmberBeforeFailedSave_AndSafetyStaysSeparate()
        {
            var core = ReadyCore();
            CompleteBenchmark(core, "B1");
            var conflict = core.Submit(Command(JourneyCommandType.ConflictObserved,
                conflict: new ConflictNotice
                {
                    IssueId = "door-clearance", SessionId = SessionId,
                    BenchmarkId = "B1", ModelVersion = "model-1",
                    ObjectId = "door-main", Role = ReviewRole.FridgeDoor,
                    AssumptionKey = "fridge_clearance", Measurement = 0.2f
                }));
            Assert.IsFalse(conflict.Snapshot.Conflicts[0].FeedbackRecorded);
            var safety = core.Submit(Command(JourneyCommandType.SafetyObserved,
                safety: new SafetyNotice
                { SafetyEventId = "boundary", SessionId = SessionId, IsActive = true }));
            Assert.NotNull(safety.Snapshot.CurrentSafety);
            Assert.IsFalse(safety.Snapshot.Conflicts[0].FeedbackRecorded);

            var submitted = core.Submit(Command(JourneyCommandType.SubmitFeedback,
                feedback: ValidFeedback("B1")));
            Assert.IsTrue(submitted.Snapshot.Conflicts[0].FeedbackRecorded);
            Assert.IsTrue(submitted.Action.Record.conflicts[0].feedbackRecorded);
            var failed = CompleteSave(core, submitted.Action, false);
            Assert.AreEqual(SessionState.FeedbackPending, failed.Snapshot.State);
            Assert.AreEqual(SaveStatus.Failed, failed.Snapshot.Save);
            Assert.IsTrue(failed.Snapshot.Conflicts[0].FeedbackRecorded);
        }

        [Test]
        public void InvalidFeedback_ReportsFieldErrorWithoutAdvancingOrSaving()
        {
            var core = ReadyCore();
            CompleteBenchmark(core, "B1");
            var result = core.Submit(Command(JourneyCommandType.SubmitFeedback,
                feedback: new FeedbackValidationResult
                {
                    SessionId = SessionId, BenchmarkId = "B1", IsValid = false,
                    FieldErrors = new Dictionary<string, string>
                    { { "importance", "Select importance." } }
                }));
            Assert.IsFalse(result.Accepted);
            Assert.AreEqual(JourneyErrorCode.InvalidFeedback, result.ErrorCode);
            StringAssert.Contains("importance", result.Message);
            Assert.AreEqual(SessionState.FeedbackPending, result.Snapshot.State);
            Assert.IsNull(result.Action);
        }

        [Test]
        public void Evidence_RejectsOldRunWrongStepAndDuplicateItem()
        {
            var core = ReadyCore();
            var start = Start(core, "B1");
            var old = Evidence(core, ChallengeStep.Approach, zoneId: "approach",
                role: ReviewRole.CirculationZone);
            old.RunId = start.RunId - 1;
            Assert.AreEqual(JourneyErrorCode.InvalidEvidence,
                core.Submit(Command(JourneyCommandType.SubmitTaskEvidence, evidence: old)).ErrorCode);
            Assert.AreEqual(JourneyErrorCode.InvalidEvidence,
                core.Submit(Command(JourneyCommandType.SubmitTaskEvidence,
                    evidence: Evidence(core, ChallengeStep.Open, objectId: "door-main",
                        role: ReviewRole.FridgeDoor, measurement: 1f))).ErrorCode);
            SubmitEvidence(core, Evidence(core, ChallengeStep.Approach, zoneId: "approach",
                role: ReviewRole.CirculationZone));
            SubmitEvidence(core, Evidence(core, ChallengeStep.Open, objectId: "door-main",
                role: ReviewRole.FridgeDoor, measurement: 1f));
            var first = Evidence(core, ChallengeStep.TakeOut, itemId: "old-1", zoneId: "source");
            SubmitEvidence(core, first);
            Assert.AreEqual(JourneyErrorCode.InvalidEvidence,
                core.Submit(Command(JourneyCommandType.SubmitTaskEvidence, evidence: first)).ErrorCode);
        }

        [Test]
        public void FiveItemPlan_TakesOutOnlyExistingItemsAndReturnsAllFive()
        {
            var plan = Plan();
            plan.Task.Items = new List<TaskItemSpec>
            {
                Item("old-1", true), Item("old-2", true), Item("old-3", true),
                Item("new-1", false), Item("new-2", false)
            };
            var core = ReadyCore(plan);
            Start(core, "B1");
            SubmitEvidence(core, Evidence(core, ChallengeStep.Approach, zoneId: "approach",
                role: ReviewRole.CirculationZone));
            SubmitEvidence(core, Evidence(core, ChallengeStep.Open, objectId: "door-main",
                role: ReviewRole.FridgeDoor, measurement: 1f));
            foreach (var id in new[] { "old-1", "old-2", "old-3" })
                SubmitEvidence(core, Evidence(core, ChallengeStep.TakeOut, itemId: id, zoneId: "source"));
            Assert.AreEqual(ChallengeStep.TemporaryPlacement, core.Snapshot.CurrentStep);
            foreach (var id in new[] { "old-1", "old-2", "old-3" })
                SubmitEvidence(core, Evidence(core, ChallengeStep.TemporaryPlacement,
                    itemId: id, zoneId: "counter", role: ReviewRole.CounterSurface, released: true));
            SubmitEvidence(core, Evidence(core, ChallengeStep.Reorganise,
                itemId: "old-1", zoneId: "storage", released: true));
            core.Submit(Command(JourneyCommandType.ConfirmReorganisation));
            Assert.AreEqual(5, core.Snapshot.TotalItemCount);
            SubmitEvidence(core, Evidence(core, ChallengeStep.PutBack,
                itemId: "old-1", zoneId: "storage", released: true));
            var returnedToTray = core.Submit(new JourneyCommand
            {
                Type = JourneyCommandType.ReturnItemToTray,
                SessionId = SessionId,
                ItemId = "old-1"
            });
            Assert.AreEqual(0,
                CompleteScene(core, returnedToTray.Action, "B1", true).Snapshot.CompletedItemCount);
            foreach (var id in new[] { "old-1", "old-2", "old-3", "new-1", "new-2" })
                SubmitEvidence(core, Evidence(core, ChallengeStep.PutBack,
                    itemId: id, zoneId: "storage", released: true));
            Assert.AreEqual(ChallengeStep.Close, core.Snapshot.CurrentStep);
        }

        [Test]
        public void ResetSupersedesRunAndRequiresFreshPhysicalEvidence()
        {
            var core = ReadyCore();
            Start(core, "B1");
            var old = Evidence(core, ChallengeStep.Approach, zoneId: "approach",
                role: ReviewRole.CirculationZone);
            SubmitEvidence(core, old);
            var reset = core.Submit(Command(JourneyCommandType.ResetCurrentTask));
            Assert.Greater(reset.Action.RunId, old.RunId);
            var completed = CompleteScene(core, reset.Action, "B1", true);
            Assert.AreEqual(ChallengeStep.Approach, completed.Snapshot.CurrentStep);
            Assert.AreEqual(reset.Action.RunId, completed.Snapshot.ActiveRunId);
            Assert.AreEqual(JourneyErrorCode.InvalidEvidence,
                core.Submit(Command(JourneyCommandType.SubmitTaskEvidence, evidence: old)).ErrorCode);
        }

        [Test]
        public void PauseDuringLoad_SavesThenResumesIntoChallenge()
        {
            var core = ReadyCore();
            var start = core.Submit(Command(JourneyCommandType.StartChallenge, "B1"));
            var pause = core.Submit(Command(JourneyCommandType.Pause));
            Assert.AreEqual(SessionState.Paused, pause.Snapshot.State);
            Assert.IsNull(pause.Action);
            var loaded = CompleteScene(core, start.Action, "B1", true);
            Assert.AreEqual(SessionState.Paused, loaded.Snapshot.State);
            Assert.AreEqual(JourneyActionType.SaveRecord, loaded.Action.Type);
            Assert.AreEqual(SessionState.Paused, CompleteSave(core, loaded.Action, true).Snapshot.State);
            var resumed = core.Submit(Command(JourneyCommandType.Resume));
            Assert.AreEqual(SessionState.ChallengeActive, resumed.Snapshot.State);
            Assert.AreEqual("B1", resumed.Snapshot.ActiveBenchmarkId);
        }

        [Test]
        public void PauseAfterFeedbackSaveFailure_PreservesTheOriginalRetry()
        {
            var core = ReadyCore();
            CompleteBenchmark(core, "B1");
            var feedback = core.Submit(Command(JourneyCommandType.SubmitFeedback,
                feedback: ValidFeedback("B1")));
            CompleteSave(core, feedback.Action, false);

            var paused = core.Submit(Command(JourneyCommandType.Pause));
            Assert.AreEqual(JourneyActionType.SaveRecord, paused.Action.Type);
            CompleteSave(core, paused.Action, true);
            Assert.AreEqual(SessionState.FeedbackPending,
                core.Submit(Command(JourneyCommandType.Resume)).Snapshot.State);
            var retry = core.Submit(Command(JourneyCommandType.Retry));
            Assert.IsTrue(retry.Accepted);
            Assert.Greater(retry.Action.RequestId, feedback.Action.RequestId);
            Assert.AreEqual(SessionState.Explore, CompleteSave(core, retry.Action, true).Snapshot.State);
        }

        [Test]
        public void SceneFailureWhilePaused_StillAttemptsPauseSaveAndKeepsSceneRetry()
        {
            var core = ReadyCore();
            var start = core.Submit(Command(JourneyCommandType.StartChallenge, "B1"));
            core.Submit(Command(JourneyCommandType.Pause));
            var failed = CompleteScene(core, start.Action, "B1", false);
            Assert.AreEqual(JourneyErrorCode.OperationFailed, failed.ErrorCode);
            Assert.AreEqual(JourneyActionType.SaveRecord, failed.Action.Type);
            CompleteSave(core, failed.Action, true);
            Assert.AreEqual(SessionState.Explore,
                core.Submit(Command(JourneyCommandType.Resume)).Snapshot.State);
            var retry = core.Submit(Command(JourneyCommandType.Retry));
            Assert.IsTrue(retry.Accepted);
            Assert.Greater(retry.Action.RunId, start.Action.RunId);
        }

        [Test]
        public void WrongLoadedBenchmark_FailsWithoutStartingAndRetryUsesNewRun()
        {
            var core = ReadyCore();
            var start = core.Submit(Command(JourneyCommandType.StartChallenge, "B1"));
            var wrong = CompleteScene(core, start.Action, "B2", true);
            Assert.AreEqual(JourneyErrorCode.OperationFailed, wrong.ErrorCode);
            Assert.AreEqual(SessionState.Explore, wrong.Snapshot.State);
            var retry = core.Submit(Command(JourneyCommandType.Retry));
            Assert.Greater(retry.Action.RequestId, start.Action.RequestId);
            Assert.Greater(retry.Action.RunId, start.Action.RunId);
            Assert.AreEqual(SessionState.ChallengeActive,
                CompleteScene(core, retry.Action, "B1", true).Snapshot.State);
        }

        [Test]
        public void FailedPreviewRetryRemainsAnExplorePreview()
        {
            var core = ReadyCore();
            var show = core.Submit(Command(JourneyCommandType.ShowBenchmark, "B1"));
            Assert.AreEqual(0, show.Action.RunId);
            Assert.AreEqual(JourneyErrorCode.OperationFailed,
                CompleteScene(core, show.Action, "B1", false).ErrorCode);
            var retry = core.Submit(Command(JourneyCommandType.Retry));
            Assert.AreEqual(0, retry.Action.RunId);
            Assert.AreEqual(SessionState.Explore,
                CompleteScene(core, retry.Action, "B1", true).Snapshot.State);
        }

        [Test]
        public void RestoreTakeOutClearsOnlyItsPartialProgressAndKeepsTheRun()
        {
            var core = ReadyCore();
            Start(core, "B1");
            SubmitEvidence(core, Evidence(core, ChallengeStep.Approach, zoneId: "approach",
                role: ReviewRole.CirculationZone));
            SubmitEvidence(core, Evidence(core, ChallengeStep.Open, objectId: "door-main",
                role: ReviewRole.FridgeDoor, measurement: 1f));
            SubmitEvidence(core, Evidence(core, ChallengeStep.TakeOut,
                itemId: "old-1", zoneId: "source"));
            Assert.AreEqual(1, core.Snapshot.CompletedItemCount);
            var run = core.Snapshot.ActiveRunId;
            var restore = core.Submit(Command(JourneyCommandType.RestoreCurrentStep));
            Assert.IsTrue(restore.Accepted, restore.Message);
            Assert.AreEqual(JourneyActionType.RestoreCurrentStep, restore.Action.Type);
            Assert.AreEqual(ChallengeStep.TakeOut, restore.Action.Step);
            Assert.AreEqual(run, restore.Action.RunId);
            Assert.AreEqual(1, core.Snapshot.CompletedItemCount,
                "A scene failure must not erase accepted evidence");
            var result = CompleteScene(core, restore.Action, "B1", true);
            Assert.AreEqual(ChallengeStep.TakeOut, result.Snapshot.CurrentStep);
            Assert.AreEqual(0, result.Snapshot.CompletedItemCount);
            Assert.AreEqual(run, result.Snapshot.ActiveRunId);
            SubmitEvidence(core, Evidence(core, ChallengeStep.TakeOut,
                itemId: "old-1", zoneId: "source"));
            SubmitEvidence(core, Evidence(core, ChallengeStep.TakeOut,
                itemId: "old-2", zoneId: "source"));
            Assert.AreEqual(ChallengeStep.TemporaryPlacement, core.Snapshot.CurrentStep);
        }

        [Test]
        public void FailedStepRestoreKeepsEvidenceAndRetryKeepsStepAndRun()
        {
            var core = ReadyCore();
            Start(core, "B1");
            SubmitEvidence(core, Evidence(core, ChallengeStep.Approach, zoneId: "approach",
                role: ReviewRole.CirculationZone));
            SubmitEvidence(core, Evidence(core, ChallengeStep.Open, objectId: "door-main",
                role: ReviewRole.FridgeDoor, measurement: 1f));
            SubmitEvidence(core, Evidence(core, ChallengeStep.TakeOut,
                itemId: "old-1", zoneId: "source"));
            var run = core.Snapshot.ActiveRunId;
            var restore = core.Submit(Command(JourneyCommandType.RestoreCurrentStep));
            Assert.AreEqual(JourneyErrorCode.OperationFailed,
                CompleteScene(core, restore.Action, "B1", false).ErrorCode);
            Assert.AreEqual(1, core.Snapshot.CompletedItemCount);
            var retry = core.Submit(Command(JourneyCommandType.Retry));
            Assert.IsTrue(retry.Accepted, retry.Message);
            Assert.AreEqual(ChallengeStep.TakeOut, retry.Action.Step);
            Assert.AreEqual(run, retry.Action.RunId);
            Assert.AreEqual(0, CompleteScene(core, retry.Action, "B1", true)
                .Snapshot.CompletedItemCount);
        }

        [Test]
        public void SnapshotAndSaveAction_DoNotExposeMutableCoreRecord()
        {
            var core = ReadyCore();
            CompleteBenchmark(core, "B1");
            var feedback = core.Submit(Command(JourneyCommandType.SubmitFeedback,
                feedback: ValidFeedback("B1")));
            feedback.Action.Record.benchmarkFeedback[0].shortExplanation = "changed outside";
            Assert.AreNotEqual("changed outside", core.Snapshot.Feedback[0].shortExplanation);
            var snapshot = core.Snapshot;
            snapshot.Feedback[0].shortExplanation = "changed snapshot";
            Assert.AreNotEqual("changed snapshot", core.Snapshot.Feedback[0].shortExplanation);
        }

        private static JourneyCoordinator ReadyCore(SessionPlan plan = null)
        {
            var core = new JourneyCoordinator(plan ?? Plan());
            Assert.IsTrue(core.HasValidPlan, core.PlanError);
            Assert.IsTrue(core.Submit(Command(JourneyCommandType.CompleteSetup, prerequisites: true)).Accepted);
            Assert.IsTrue(core.Submit(Command(JourneyCommandType.AcknowledgeOrientation,
                prerequisites: true)).Accepted);
            return core;
        }

        private static JourneyAction Start(JourneyCoordinator core, string benchmarkId)
        {
            var start = core.Submit(Command(JourneyCommandType.StartChallenge, benchmarkId));
            Assert.IsTrue(start.Accepted, start.Message);
            Assert.AreEqual(SessionState.ChallengeActive,
                CompleteScene(core, start.Action, benchmarkId, true).Snapshot.State);
            return start.Action;
        }

        private static void CompleteBenchmark(JourneyCoordinator core, string benchmarkId)
        {
            Start(core, benchmarkId);
            SubmitEvidence(core, Evidence(core, ChallengeStep.Approach, zoneId: "approach",
                role: ReviewRole.CirculationZone));
            SubmitEvidence(core, Evidence(core, ChallengeStep.Open, objectId: "door-main",
                role: ReviewRole.FridgeDoor, measurement: 1f));
            foreach (var id in new[] { "old-1", "old-2" })
                SubmitEvidence(core, Evidence(core, ChallengeStep.TakeOut, itemId: id, zoneId: "source"));
            foreach (var id in new[] { "old-1", "old-2" })
                SubmitEvidence(core, Evidence(core, ChallengeStep.TemporaryPlacement,
                    itemId: id, zoneId: "counter", role: ReviewRole.CounterSurface, released: true));
            SubmitEvidence(core, Evidence(core, ChallengeStep.Reorganise, itemId: "old-1",
                zoneId: "storage", released: true));
            Assert.IsTrue(core.Submit(Command(JourneyCommandType.ConfirmReorganisation)).Accepted);
            foreach (var id in new[] { "old-1", "old-2", "new-1" })
                SubmitEvidence(core, Evidence(core, ChallengeStep.PutBack, itemId: id,
                    zoneId: "storage", released: true));
            SubmitEvidence(core, Evidence(core, ChallengeStep.Close, objectId: "door-main",
                role: ReviewRole.FridgeDoor, measurement: 0f));
            SubmitEvidence(core, Evidence(core, ChallengeStep.Leave, zoneId: "leave",
                role: ReviewRole.CirculationZone));
            Assert.AreEqual(SessionState.FeedbackPending, core.Snapshot.State);
            Assert.AreEqual(TaskOutcome.Completed, core.Snapshot.BenchmarkOutcomes[benchmarkId]);
        }

        private static void SaveFeedback(JourneyCoordinator core, string benchmarkId, bool success)
        {
            var feedback = core.Submit(Command(JourneyCommandType.SubmitFeedback,
                feedback: ValidFeedback(benchmarkId)));
            Assert.IsTrue(feedback.Accepted, feedback.Message);
            CompleteSave(core, feedback.Action, success);
        }

        private static void SkipAndSave(JourneyCoordinator core, string benchmarkId)
        {
            Start(core, benchmarkId);
            Assert.IsTrue(core.Submit(Command(JourneyCommandType.SkipChallenge,
                reason: "comfort stop")).Accepted);
            var feedback = core.Submit(Command(JourneyCommandType.SubmitFeedback,
                feedback: new FeedbackValidationResult
                {
                    SessionId = SessionId, BenchmarkId = benchmarkId, IsValid = true,
                    Feedback = new BenchmarkFeedback()
                }));
            Assert.IsTrue(feedback.Accepted, feedback.Message);
            CompleteSave(core, feedback.Action, true);
        }

        private static void SubmitEvidence(JourneyCoordinator core, TaskEvidence evidence)
        {
            var result = core.Submit(Command(JourneyCommandType.SubmitTaskEvidence, evidence: evidence));
            Assert.IsTrue(result.Accepted, result.Message);
        }

        private static TaskEvidence Evidence(JourneyCoordinator core, ChallengeStep step,
            string objectId = null, string itemId = null, string zoneId = null,
            ReviewRole role = ReviewRole.StorageZone, float measurement = 0f, bool released = false)
        {
            return new TaskEvidence
            {
                SessionId = SessionId, BenchmarkId = core.Snapshot.ActiveBenchmarkId,
                RunId = core.Snapshot.ActiveRunId, Step = step,
                Role = role, ObjectId = objectId, ItemId = itemId, ZoneId = zoneId,
                OccurredAtSessionSeconds = 1f, Measurement = measurement, Released = released
            };
        }

        private static FeedbackValidationResult ValidFeedback(string benchmarkId)
        {
            return new FeedbackValidationResult
            {
                SessionId = SessionId, BenchmarkId = benchmarkId, IsValid = true,
                Feedback = new BenchmarkFeedback
                {
                    combinationId = benchmarkId,
                    decision = CombinationDecision.NeedsRevision,
                    category = FeedbackCategory.DoorClearance,
                    importance = Importance.High,
                    shortExplanation = "Door sweep reduces circulation."
                }
            };
        }

        private static JourneyResult CompleteScene(JourneyCoordinator core, JourneyAction action,
            string actualBenchmarkId, bool success)
        {
            return core.Submit(new JourneyCommand
            {
                Type = JourneyCommandType.SceneCompleted,
                SceneResult = new SceneActionResult
                { RequestId = action.RequestId, Succeeded = success, ActualBenchmarkId = actualBenchmarkId }
            });
        }

        private static JourneyResult CompleteSave(JourneyCoordinator core, JourneyAction action, bool success)
        {
            Assert.NotNull(action);
            return core.Submit(new JourneyCommand
            {
                Type = JourneyCommandType.SaveCompleted,
                SaveResult = new SaveResult
                { RequestId = action.RequestId, Succeeded = success, ReadBackVerified = success }
            });
        }

        private static JourneyCommand Command(JourneyCommandType type, string benchmarkId = null,
            string reason = null, bool prerequisites = false, TaskEvidence evidence = null,
            FeedbackValidationResult feedback = null, FinalComparison comparison = null,
            ConflictNotice conflict = null, SafetyNotice safety = null)
        {
            return new JourneyCommand
            {
                Type = type, SessionId = SessionId, BenchmarkId = benchmarkId,
                Reason = reason, PrerequisitesMet = prerequisites, Evidence = evidence,
                Feedback = feedback, Comparison = comparison, Conflict = conflict, Safety = safety
            };
        }

        private static SessionPlan Plan()
        {
            return new SessionPlan
            {
                SessionId = SessionId, ModelVersion = "model-1",
                BenchmarkIds = new List<string> { "B1", "B2" },
                Task = new TaskConfig
                {
                    Items = new List<TaskItemSpec>
                    { Item("old-1", true), Item("old-2", true), Item("new-1", false) },
                    AccessTargets = new List<TaskAccessSpec>
                    { new TaskAccessSpec { ObjectId = "door-main", Role = ReviewRole.FridgeDoor } },
                    ApproachZoneId = "approach", LeaveZoneId = "leave",
                    OpenProgressThreshold = 0.9f, ClosedProgressThreshold = 0.1f
                }
            };
        }

        private static TaskItemSpec Item(string id, bool existing)
        {
            return new TaskItemSpec
            {
                ItemId = id, IsExistingItem = existing,
                SourceZoneId = existing ? "source" : null,
                TemporaryZoneId = existing ? "counter" : null,
                ReturnZoneId = "storage"
            };
        }
    }
}
