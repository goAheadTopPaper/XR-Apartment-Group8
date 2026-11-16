using System;
using System.Linq;
using NUnit.Framework;
using XRApartment.Data.ReviewV2;
using XRApartment.Simulation.ReviewV2;

namespace XRApartment.Tests
{
    public sealed class ReviewV2SchemeContextTests
    {
        sealed class LayoutPort : ISchemePort, ISchemeModelCatalog
        {
            public string Id, VersionB = "B-v1";
            public Action<string, bool, string> Done;
            public bool Supports(string id) => id == SchemeIds.IntegratedKitchen || id == SchemeIds.ExternalRefrigerator;
            public string ModelVersionFor(string id) => id == SchemeIds.IntegratedKitchen ? "A-v1" : VersionB;
            public void Change(string id, string scheme, Action<string, bool, string> done) { Id = id; Done = done; }
        }
        static SessionController Start(LayoutPort port)
        {
            var core = new SessionController(true, null, port, null, false, "A-v1", SchemeIds.IntegratedKitchen);
            core.Execute(CommandType.CompleteSetup); core.Execute(CommandType.CompleteOnboarding); return core;
        }
        [Test] public void SuccessfulRoundTripCreatesFreshVisitsAndPreservesCapturedContext()
        {
            var port = new LayoutPort(); var core = Start(port); var a = core.Snapshot;
            core.ObserveGoal(a.SessionId, a.SchemeVisitId, "G02", GoalState.InProgress, "cut", 10);
            var fact = new InteractionEvent(core.Snapshot, InteractionKind.TomatoCut, "COOKING_STATION", 10);
            core.Execute(CommandType.ChangeScheme, SchemeIds.ExternalRefrigerator);
            Assert.AreEqual(a.SchemeVisitId, core.Snapshot.SchemeVisitId);
            Assert.AreEqual("OperationPending", core.Execute(CommandType.ChangeScheme, SchemeIds.ExternalRefrigerator).Reason);
            port.VersionB = "changed-after-request"; port.Done(port.Id, true, "loaded"); var b = core.Snapshot;
            Assert.AreEqual("B-v1", b.ModelVersion, "Identity must be captured before loading starts");
            Assert.AreEqual(a.SessionId, b.SessionId); Assert.AreNotEqual(a.SchemeVisitId, b.SchemeVisitId);
            Assert.IsTrue(b.Goals.All(g => g.State == GoalState.NotStarted));
            Assert.IsFalse(core.ObserveGoal(a.SessionId, a.SchemeVisitId, "G02", GoalState.Completed, "late"));
            core.Execute(CommandType.ChangeScheme, SchemeIds.IntegratedKitchen); port.Done(port.Id, true, "loaded");
            Assert.AreEqual("A-v1", core.Snapshot.ModelVersion); Assert.AreNotEqual(a.SchemeVisitId, core.Snapshot.SchemeVisitId);
            CollectionAssert.AreEqual(new[] { SchemeIds.IntegratedKitchen, SchemeIds.ExternalRefrigerator, SchemeIds.IntegratedKitchen }, core.Snapshot.SchemeHistory);
            Assert.AreEqual(SchemeIds.IntegratedKitchen, fact.SchemeId); Assert.AreEqual("A-v1", fact.ModelVersion);
            Assert.AreEqual(a.SchemeVisitId, fact.SchemeVisitId);
        }
        [Test] public void FailureAndWrongReceiptPreserveWorldContextAndGoals()
        {
            var port = new LayoutPort(); var core = Start(port); var a = core.Snapshot;
            core.ObserveGoal(a.SessionId, a.SchemeVisitId, "G01", GoalState.InProgress, "stored");
            core.Execute(CommandType.ChangeScheme, SchemeIds.ExternalRefrigerator);
            port.Done("stale", true, ""); Assert.IsTrue(core.Snapshot.IsBusy);
            port.Done(port.Id, false, "destination occupied");
            Assert.AreEqual(a.SchemeVisitId, core.Snapshot.SchemeVisitId); Assert.AreEqual("A-v1", core.Snapshot.ModelVersion);
            Assert.AreEqual(GoalState.InProgress, core.Snapshot.Goals.Single(g => g.Id == "G01").State);
            Assert.AreEqual(1, core.Snapshot.SchemeHistory.Count);
            port.Done(port.Id, true, "duplicate"); Assert.AreEqual(a.SchemeVisitId, core.Snapshot.SchemeVisitId);
        }
        [Test] public void MissingVersionRejectsBeforePortMutatesWorld()
        {
            var port = new LayoutPort { VersionB = " " }; var core = Start(port);
            Assert.AreEqual("ModelVersionUnavailable", core.Execute(CommandType.ChangeScheme, SchemeIds.ExternalRefrigerator).Reason);
            Assert.IsNull(port.Done); Assert.IsFalse(core.Snapshot.IsBusy);
        }
        [Test] public void ReviewAndPauseNeverInvokeLayoutPort()
        {
            var port = new LayoutPort(); var core = Start(port); var a = core.Snapshot;
            core.Execute(CommandType.Review); Assert.IsFalse(core.Execute(CommandType.ChangeScheme, SchemeIds.ExternalRefrigerator).Accepted);
            core.Execute(CommandType.Continue); core.Execute(CommandType.Pause);
            Assert.IsFalse(core.Execute(CommandType.ChangeScheme, SchemeIds.ExternalRefrigerator).Accepted);
            core.Execute(CommandType.Resume); Assert.IsNull(port.Done); Assert.AreEqual(a.SchemeVisitId, core.Snapshot.SchemeVisitId);
        }
        sealed class SavePort : ISessionSavePort
        {
            public string Id;
            public SessionSnapshot Captured;
            public Action<string, bool, string> Done;
            public void Save(string id, SessionSnapshot snapshot, Action<string, bool, string> done)
            { Id = id; Captured = snapshot; Done = done; }
        }
        [Test] public void ConfiguredSaveMustVerifyBeforeChangingAndFailureCanRetry()
        {
            var layout = new LayoutPort(); var save = new SavePort();
            var core = new SessionController(true, null, layout, save, false, "A-v1", SchemeIds.IntegratedKitchen);
            core.Execute(CommandType.CompleteSetup); core.Execute(CommandType.CompleteOnboarding); var a = core.Snapshot;
            core.Execute(CommandType.ChangeScheme, SchemeIds.ExternalRefrigerator);
            Assert.IsNull(layout.Done); Assert.AreEqual(a.SchemeVisitId, save.Captured.SchemeVisitId);
            save.Done(save.Id, false, "disk full"); Assert.AreEqual(a.SchemeVisitId, core.Snapshot.SchemeVisitId);
            Assert.AreEqual(SaveStatus.Failed, core.Snapshot.SaveStatus); Assert.IsNull(layout.Done);
            core.Execute(CommandType.ChangeScheme, SchemeIds.ExternalRefrigerator);
            save.Done("old", true, ""); Assert.IsNull(layout.Done);
            save.Done(save.Id, true, "verified test receipt"); Assert.IsNotNull(layout.Done);
            Assert.AreEqual(a.SchemeVisitId, core.Snapshot.SchemeVisitId);
            layout.Done(layout.Id, true, "loaded"); Assert.AreEqual(SchemeIds.ExternalRefrigerator, core.Snapshot.SchemeId);
            var b = core.Snapshot.SchemeVisitId;
            save.Done(save.Id, true, "duplicate"); Assert.AreEqual(b, core.Snapshot.SchemeVisitId);
            Assert.AreEqual(a.SchemeId, save.Captured.SchemeId);
        }
    }
}
