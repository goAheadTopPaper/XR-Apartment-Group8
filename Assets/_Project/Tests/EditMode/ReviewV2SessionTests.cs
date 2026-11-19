using System;
using System.Linq;
using NUnit.Framework;
using XRApartment.Data;
using XRApartment.Data.ReviewV2;
using XRApartment.Simulation.ReviewV2;

namespace XRApartment.Tests
{
    public class ReviewV2SessionTests
    {
        [Test] public void SnapshotReusesCurrentStateAndPreservesEarlierState()
        {
            var core = new SessionController();
            var setup = core.Snapshot;
            Assert.AreSame(setup, core.Snapshot);
            Assert.IsFalse(core.Execute(CommandType.Finish).Accepted);
            Assert.AreSame(setup, core.Snapshot);

            SessionSnapshot published = null;
            core.Changed += s => { published = s; Assert.AreSame(s, core.Snapshot); };
            Experience(core);
            var experience = core.Snapshot;
            Assert.AreSame(published, experience);
            Assert.AreEqual(SessionPhase.Experience, experience.Phase);
            Assert.AreEqual(SessionPhase.Setup, setup.Phase);

            core.Execute(CommandType.Pause);
            var paused = core.Snapshot;
            Assert.IsTrue(paused.IsPaused);
            Assert.IsFalse(experience.IsPaused);
            core.Execute(CommandType.Resume);
            Assert.IsFalse(core.Snapshot.IsPaused);

            Assert.IsTrue(core.ObserveGoal(experience.SessionId, experience.SchemeVisitId, "G02", GoalState.Completed, "cooked"));
            var completed = core.Snapshot;
            Assert.AreNotSame(experience, completed);
            Assert.AreSame(published, completed);
            Assert.AreEqual(GoalState.Completed, completed.Goals.Single(g => g.Id == "G02").State);
            Assert.AreEqual(GoalState.NotStarted, experience.Goals.Single(g => g.Id == "G02").State);
            Assert.IsFalse(core.ObserveGoal(experience.SessionId, experience.SchemeVisitId, "G02", GoalState.Completed, "duplicate"));
            Assert.AreSame(completed, core.Snapshot);
        }

        [Test] public void ExplicitModelVersionSurvivesUnavailableGoalsReviewAndReset()
        {
            var port = new PendingPort();
            var core = new SessionController(false, port, null, null, false, "cooking-m1-groceries-v1");
            Experience(core); var before = core.Snapshot;
            Assert.IsFalse(before.Goals.Single(g => g.Id == "G01").Available);
            core.Execute(CommandType.Review); core.Execute(CommandType.Continue);
            Assert.AreEqual(before.ModelVersion, core.Snapshot.ModelVersion);
            Assert.AreEqual(before.SchemeVisitId, core.Snapshot.SchemeVisitId);
            core.Execute(CommandType.ResetWorld); port.Complete(port.Id, true, "restored");
            Assert.AreEqual(before.ModelVersion, core.Snapshot.ModelVersion);
            Assert.AreNotEqual(before.SchemeVisitId, core.Snapshot.SchemeVisitId);
        }
        [Test] public void V2RecipePolicyDoesNotInventFridgeEvidenceOrChangeLegacyDefault()
        {
            var actions = (TomatoEggAction)((1 << 14) - 1) & ~TomatoEggAction.OpenFridge;
            var legacy = new XRApartment.Simulation.TomatoEggRecipe();
            var review = new XRApartment.Simulation.TomatoEggRecipe(false);
            legacy.Observe(actions); review.Observe(actions);
            legacy.ObserveCooking(true, true, false); review.ObserveCooking(true, true, false);
            Assert.IsFalse(legacy.Ready); Assert.IsTrue(review.Ready);
            Assert.AreEqual(TomatoEggAction.None, review.Actions & TomatoEggAction.OpenFridge);
            review.Reset(); Assert.AreEqual(TomatoEggStep.TakeTomato, review.Step);
            Assert.AreEqual(TomatoEggStep.OpenFridge, new XRApartment.Simulation.TomatoEggRecipe().Step);
        }
        [Test] public void GroceryPlacementsAreDistinctAndCanCoexistWithCooking()
        {
            var storage = new GroceryStorageProgress(new[] { "tomato", "egg", "other" }, 2);
            Assert.IsFalse(storage.Store("unknown")); Assert.AreEqual(GoalState.NotStarted, storage.State);
            Assert.IsTrue(storage.Store("egg")); Assert.AreEqual(GoalState.InProgress, storage.State);
            Assert.IsFalse(storage.Store("egg")); Assert.AreEqual(GoalState.InProgress, storage.State);
            var core = new SessionController(groceriesAvailable: true); Experience(core); var s = core.Snapshot;
            Assert.AreEqual("cooking-m1-groceries-v1", s.ModelVersion);
            Assert.IsTrue(core.ObserveGoal(s.SessionId, s.SchemeVisitId, "G01", storage.State, "stored-egg"));
            Assert.IsTrue(core.ObserveGoal(s.SessionId, s.SchemeVisitId, "G02", GoalState.InProgress, "cut"));
            Assert.AreEqual(2, core.Snapshot.Goals.Count(g => g.State == GoalState.InProgress));
            Assert.IsTrue(storage.Store("tomato")); Assert.AreEqual(GoalState.Completed, storage.State);
            Assert.AreEqual(SessionPhase.Experience, core.Snapshot.Phase);
            storage.Reset(); Assert.AreEqual(GoalState.NotStarted, storage.State);
        }
        [Test] public void OnlyNewRealPreparationFlagsCanStartG02()
        {
            var preliminary = TomatoEggAction.OpenFridge | TomatoEggAction.TakeTomato |
                TomatoEggAction.TakeEgg | TomatoEggAction.TakeBowl | TomatoEggAction.TurnOnStove;
            Assert.AreEqual(TomatoEggAction.None, CookingPreparationEvidence.NewProcessingActions(TomatoEggAction.None, preliminary));
            Assert.AreEqual(TomatoEggAction.SliceTomato, CookingPreparationEvidence.NewProcessingActions(preliminary,
                preliminary | TomatoEggAction.SliceTomato));
            Assert.AreEqual(TomatoEggAction.CrackEgg, CookingPreparationEvidence.NewProcessingActions(preliminary,
                preliminary | TomatoEggAction.CrackEgg));
            Assert.AreEqual(TomatoEggAction.None, CookingPreparationEvidence.NewProcessingActions(
                preliminary | TomatoEggAction.CrackEgg, preliminary | TomatoEggAction.CrackEgg));
            Assert.AreEqual(TomatoEggAction.None, CookingPreparationEvidence.NewProcessingActions(
                preliminary | TomatoEggAction.CrackEgg, TomatoEggAction.None));
            Assert.IsTrue(CookingPreparationEvidence.HasNewTomatoCut(0, 1));
            Assert.IsTrue(CookingPreparationEvidence.HasNewTomatoCut(1, 2));
            Assert.IsFalse(CookingPreparationEvidence.HasNewTomatoCut(2, 2));
            Assert.IsFalse(CookingPreparationEvidence.HasNewTomatoCut(2, 0));
        }
        static void Experience(IReviewSessionCommands core)
        { Assert.IsTrue(core.Execute(CommandType.CompleteSetup).Accepted); Assert.IsTrue(core.Execute(CommandType.CompleteOnboarding).Accepted); }
        [Test] public void ZeroGoalsCanReviewReturnAndFinishWithoutClaimingSave()
        {
            var core = new SessionController(); Experience(core);
            string visit = core.Snapshot.SchemeVisitId;
            Assert.IsTrue(core.Execute(CommandType.Review).Accepted);
            Assert.IsTrue(core.Execute(CommandType.Continue).Accepted);
            Assert.AreEqual(visit, core.Snapshot.SchemeVisitId);
            core.Execute(CommandType.Review); Assert.IsTrue(core.Execute(CommandType.Finish).Accepted);
            Assert.AreEqual(SessionPhase.Finished, core.Snapshot.Phase);
            Assert.AreEqual(SaveStatus.NotConfigured, core.Snapshot.SaveStatus);
            Assert.IsTrue(core.Snapshot.Goals.All(g => g.State == GoalState.NotStarted));
        }
        [Test] public void GoalsCoexistAndSnapshotsAreIndependent()
        {
            var mock = new MockReviewProvider(); Experience(mock); var before = mock.Snapshot;
            Assert.IsTrue(mock.SetGoal("G01", GoalState.InProgress)); Assert.IsTrue(mock.SetGoal("G02", GoalState.InProgress));
            Assert.AreEqual(2, mock.Snapshot.Goals.Count(g => g.State == GoalState.InProgress));
            Assert.IsTrue(before.Goals.All(g => g.State == GoalState.NotStarted));
            Assert.IsFalse(mock.SetGoal("G01", GoalState.NotStarted));
        }
        [Test] public void CookingCompletionCannotAdvancePhase()
        {
            var core = new SessionController(); Experience(core); var s = core.Snapshot;
            Assert.IsTrue(core.ObserveGoal(s.SessionId, s.SchemeVisitId, "G02", GoalState.Completed, "real-evidence"));
            Assert.AreEqual(SessionPhase.Experience, core.Snapshot.Phase);
            Assert.IsFalse(core.ObserveGoal(s.SessionId, s.SchemeVisitId, "G04", GoalState.Completed, "invented"));
        }
        [Test] public void CookingMayCompleteWhileReviewingWithoutFinishingSession()
        {
            var core = new SessionController(); Experience(core); core.Execute(CommandType.Review); var s = core.Snapshot;
            Assert.IsTrue(core.ObserveGoal(s.SessionId, s.SchemeVisitId, "G02", GoalState.Completed, "ready-during-review"));
            Assert.AreEqual(SessionPhase.Review, core.Snapshot.Phase);
        }
        [Test] public void AmbiguousGrabDoesNotAdvanceMockGoals()
        {
            var mock = new MockReviewProvider(); Experience(mock); InteractionEvent fact = null;
            mock.Observed += e => fact = e; mock.EmitGrab();
            Assert.IsTrue(fact.IsMock); Assert.AreEqual(mock.Snapshot.SchemeVisitId, fact.SchemeVisitId);
            Assert.IsTrue(mock.Snapshot.Goals.All(g => g.State == GoalState.NotStarted));
        }
        [Test] public void InvalidCommandsPauseAndTerminalAreGuarded()
        {
            var core = new SessionController(); Assert.IsFalse(core.Execute(CommandType.Finish).Accepted);
            Experience(core); string visit = core.Snapshot.SchemeVisitId;
            core.Execute(CommandType.Pause); Assert.IsFalse(core.Execute(CommandType.Review).Accepted);
            core.Execute(CommandType.Resume); Assert.AreEqual(visit, core.Snapshot.SchemeVisitId);
            core.Execute(CommandType.SafeExit); Assert.AreEqual(SessionPhase.Exited, core.Snapshot.Phase);
            Assert.IsFalse(core.Execute(CommandType.CompleteSetup).Accepted);
        }
        [Test] public void UnsupportedSchemePreservesContext()
        {
            var core = new SessionController(); Experience(core); var before = core.Snapshot;
            Assert.AreEqual("NotSupported", core.Execute(CommandType.ChangeScheme, "SCHEME_B").Reason);
            Assert.AreEqual(before.SchemeVisitId, core.Snapshot.SchemeVisitId);
        }
        [Test] public void FailedMockSwitchPreservesGoalsAndSuccessCreatesNewVisit()
        {
            var mock = new MockReviewProvider(); Experience(mock); mock.SetGoal("G02", GoalState.InProgress);
            var old = mock.Snapshot; mock.FailNextOperation = true; mock.Execute(CommandType.ChangeScheme, "MOCK_B");
            Assert.AreEqual(old.SchemeVisitId, mock.Snapshot.SchemeVisitId);
            Assert.AreEqual(GoalState.InProgress, mock.Snapshot.Goals.Single(g => g.Id == "G02").State);
            mock.Execute(CommandType.ChangeScheme, "MOCK_B"); Assert.AreNotEqual(old.SchemeVisitId, mock.Snapshot.SchemeVisitId);
            Assert.IsTrue(mock.Snapshot.Goals.All(g => g.State == GoalState.NotStarted)); Assert.IsTrue(mock.Snapshot.IsMock);
        }
        sealed class PendingPort : IWorldPort, ISessionSavePort
        {
            public string Id; public Action<string, bool, string> Complete;
            public void Reset(string id, Action<string, bool, string> complete) { Id = id; Complete = complete; }
            public CommandResult Recenter() => new CommandResult(true);
            public void Save(string id, SessionSnapshot snapshot, Action<string, bool, string> complete) => Reset(id, complete);
        }
        [Test] public void ResetRejectsConcurrentCommandsAndOldVisitEvidence()
        {
            var port = new PendingPort(); var core = new SessionController(port); Experience(core); var old = core.Snapshot;
            core.Execute(CommandType.ResetWorld); Assert.IsTrue(core.Snapshot.IsBusy);
            Assert.IsFalse(core.Execute(CommandType.Review).Accepted);
            port.Complete("wrong", true, ""); Assert.IsTrue(core.Snapshot.IsBusy);
            port.Complete(port.Id, true, ""); var visit = core.Snapshot.SchemeVisitId;
            port.Complete(port.Id, true, ""); Assert.AreEqual(visit, core.Snapshot.SchemeVisitId);
            Assert.IsFalse(core.ObserveGoal(old.SessionId, old.SchemeVisitId, "G02", GoalState.Completed, "late"));
        }
        [Test] public void StorageFailureAndRetryRequireMatchingVerifiedReceipt()
        {
            var port = new PendingPort(); var core = new SessionController(storage: port); Experience(core); core.Execute(CommandType.Review);
            core.Execute(CommandType.Finish); port.Complete(port.Id, false, "disk failed");
            Assert.AreEqual(SessionPhase.Review, core.Snapshot.Phase); Assert.AreEqual(SaveStatus.Failed, core.Snapshot.SaveStatus);
            core.Execute(CommandType.Finish); port.Complete(port.Id, true, "read back verified");
            Assert.AreEqual(SessionPhase.Finished, core.Snapshot.Phase); Assert.AreEqual(SaveStatus.Verified, core.Snapshot.SaveStatus);
        }
        [Test] public void SafeExitInvalidatesPendingSave()
        {
            var port = new PendingPort(); var core = new SessionController(storage: port); Experience(core); core.Execute(CommandType.Review);
            core.Execute(CommandType.Finish); core.Execute(CommandType.SafeExit); port.Complete(port.Id, true, "late");
            Assert.AreEqual(SessionPhase.Exited, core.Snapshot.Phase); Assert.AreNotEqual(SaveStatus.Verified, core.Snapshot.SaveStatus);
        }
        [Test] public void MockCannotClaimVerifiedSave()
        {
            var port = new PendingPort(); var core = new SessionController(storage: port, isMock: true); Experience(core);
            core.Execute(CommandType.Review); core.Execute(CommandType.Finish); port.Complete(port.Id, true, "mock");
            Assert.AreEqual(SaveStatus.Failed, core.Snapshot.SaveStatus); Assert.AreEqual(SessionPhase.Review, core.Snapshot.Phase);
        }
        [Test] public void GoalEvidenceMustBeKnownAvailableAndNonempty()
        {
            var goals = new GoalTracker(); Assert.IsFalse(goals.Observe("G02", GoalState.InProgress, null));
            Assert.IsFalse(goals.Observe("missing", GoalState.Completed, "e")); Assert.IsFalse(goals.Observe("G05", GoalState.Completed, "e"));
        }
        [Test] public void SchemeHistoryAndEvidenceTimeAreImmutableSnapshots()
        {
            var mock = new MockReviewProvider(); Experience(mock);
            mock.SetGoal("G02", GoalState.InProgress); var before = mock.Snapshot;
            Assert.That(before.Goals.Single(g => g.Id == "G02").UpdatedAtMilliseconds, Is.GreaterThan(0));
            mock.Execute(CommandType.ChangeScheme, "MOCK_B");
            CollectionAssert.AreEqual(new[] { "MOCK_A" }, before.SchemeHistory);
            CollectionAssert.AreEqual(new[] { "MOCK_A", "MOCK_B" }, mock.Snapshot.SchemeHistory);
            mock.Execute(CommandType.ResetWorld);
            Assert.AreEqual(2, mock.Snapshot.SchemeHistory.Count, "World reset is not a scheme visit to a different layout");
        }
        [Test] public void MissingCalibrationPortRejectsWithoutAdvancingPhase()
        {
            var core = new SessionController();
            Assert.AreEqual("CalibrationUnavailable", core.Execute(CommandType.CalibrateStanding).Reason);
            Assert.AreEqual(SessionPhase.Setup, core.Snapshot.Phase);
        }
    }
}
