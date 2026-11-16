using System;
using System.Collections.Generic;
using XRApartment.Data.ReviewV2;
using V2SaveStatus = XRApartment.Data.ReviewV2.SaveStatus;

namespace XRApartment.Simulation.ReviewV2
{
    public sealed class SessionController : ISessionReadModel, IReviewSessionCommands
    {
        readonly string sessionId = Guid.NewGuid().ToString("N");
        readonly GoalTracker goals;
        readonly ISchemePort schemes;
        readonly IWorldPort world;
        readonly ISessionSavePort storage;
        readonly bool mock;
        string modelVersion;
        readonly List<string> schemeHistory = new List<string>();
        string schemeId, visitId = Guid.NewGuid().ToString("N"), pending;
        SessionPhase phase;
        bool paused;
        V2SaveStatus saveStatus;
        public event Action<SessionSnapshot> Changed;
        public event Action<OperationResult> OperationCompleted;
        public SessionSnapshot Snapshot => new SessionSnapshot(sessionId, phase, schemeId, visitId,
            modelVersion, goals.Snapshot, paused, pending != null, mock, saveStatus, schemeHistory);
        public SessionController(IWorldPort world = null, ISchemePort schemes = null, ISessionSavePort storage = null, bool isMock = false)
            : this(false, world, schemes, storage, isMock) { }
        public SessionController(bool groceriesAvailable, IWorldPort world = null, ISchemePort schemes = null, ISessionSavePort storage = null, bool isMock = false)
            : this(groceriesAvailable, world, schemes, storage, isMock,
                groceriesAvailable ? "cooking-m1-groceries-v1" : "cooking-m0-hdrp-e319aaf") { }
        public SessionController(bool groceriesAvailable, IWorldPort world, ISchemePort schemes, ISessionSavePort storage, bool isMock, string modelVersion)
            : this(groceriesAvailable, world, schemes, storage, isMock, modelVersion, isMock ? "MOCK_A" : SchemeIds.Baseline) { }
        public SessionController(bool groceriesAvailable, IWorldPort world, ISchemePort schemes, ISessionSavePort storage, bool isMock, string modelVersion, string initialSchemeId)
        { this.world = world; this.schemes = schemes; this.storage = storage; mock = isMock;
            this.modelVersion = string.IsNullOrWhiteSpace(modelVersion) ? "UNVERSIONED" : modelVersion;
            if (string.IsNullOrWhiteSpace(initialSchemeId)) throw new ArgumentException("Initial scheme identity is required", nameof(initialSchemeId));
            schemeId = initialSchemeId; schemeHistory.Add(schemeId); goals = new GoalTracker(isMock, groceriesAvailable); }
        bool Terminal => phase == SessionPhase.Finished || phase == SessionPhase.Exited;
        void Publish() => Changed?.Invoke(Snapshot);
        public CommandResult Execute(CommandType command, string targetSchemeId = null)
        {
            if (Terminal) return new CommandResult(false, "SessionEnded");
            if (command == CommandType.SafeExit)
            { pending = null; paused = false; phase = SessionPhase.Exited;
                if (saveStatus == V2SaveStatus.Pending) saveStatus = V2SaveStatus.Failed; Publish(); return new CommandResult(true); }
            if (pending != null) return new CommandResult(false, "OperationPending");
            if (command == CommandType.Pause && !paused) { paused = true; Publish(); return new CommandResult(true); }
            if (command == CommandType.Resume && paused) { paused = false; Publish(); return new CommandResult(true); }
            if (command == CommandType.Recenter) return world?.Recenter() ?? new CommandResult(false, "XRUnavailable");
            if (command == CommandType.CalibrateStanding || command == CommandType.CalibrateSeated)
                return world is ICalibrationPort calibration ? calibration.Calibrate(command == CommandType.CalibrateSeated) : new CommandResult(false, "CalibrationUnavailable");
            if (paused) return new CommandResult(false, "Paused");
            switch (command)
            {
                case CommandType.CompleteSetup: if (phase != SessionPhase.Setup) break; return Transition(SessionPhase.Onboarding);
                case CommandType.CompleteOnboarding: if (phase != SessionPhase.Onboarding) break; return Transition(SessionPhase.Experience);
                case CommandType.Review: if (phase != SessionPhase.Experience) break; return Transition(SessionPhase.Review);
                case CommandType.Continue: if (phase != SessionPhase.Review) break; return Transition(SessionPhase.Experience);
                case CommandType.Finish:
                    if (phase != SessionPhase.Review) break;
                    if (storage != null) return Begin(command, (id, done) => storage.Save(id, Snapshot, done));
                    return Transition(SessionPhase.Finished);
                case CommandType.ResetWorld:
                    if (phase != SessionPhase.Experience) break;
                    if (world == null) return new CommandResult(false, "WorldUnavailable");
                    return Begin(command, world.Reset);
                case CommandType.ChangeScheme:
                    if (phase != SessionPhase.Experience) break;
                    if (string.IsNullOrWhiteSpace(targetSchemeId) || targetSchemeId == schemeId) return new CommandResult(false, "InvalidScheme");
                    if (schemes == null || !schemes.Supports(targetSchemeId)) return new CommandResult(false, "NotSupported");
                    var version = schemes is ISchemeModelCatalog catalog ? catalog.ModelVersionFor(targetSchemeId) : modelVersion;
                    if (string.IsNullOrWhiteSpace(version)) return new CommandResult(false, "ModelVersionUnavailable");
                    return Begin(command, (id, done) => SaveThenChange(id, targetSchemeId, done), targetSchemeId, version);
            }
            return new CommandResult(false, "InvalidPhase");
        }
        CommandResult Transition(SessionPhase next) { phase = next; Publish(); return new CommandResult(true); }
        void SaveThenChange(string id, string target, Action<string, bool, string> done)
        {
            if (storage == null) { schemes.Change(id, target, done); return; }
            saveStatus = V2SaveStatus.Pending; Publish();
            bool received = false;
            try { storage.Save(id, Snapshot, (receipt, verified, message) =>
            {
                if (receipt != id || pending != id || Terminal || received) return;
                received = true;
                if (!verified || mock)
                { saveStatus = V2SaveStatus.Failed; done(id, false, message); return; }
                saveStatus = V2SaveStatus.Verified;
                try { schemes.Change(id, target, done); }
                catch (Exception ex) { done(id, false, ex.Message); }
            }); }
            catch
            {
                if (pending == id) saveStatus = V2SaveStatus.Failed;
                throw;
            }
        }
        CommandResult Begin(CommandType command, Action<string, Action<string, bool, string>> action, string target = null, string targetVersion = null)
        {
            var id = pending = Guid.NewGuid().ToString("N");
            if (command == CommandType.Finish) saveStatus = V2SaveStatus.Pending;
            Publish();
            try { action(id, (receipt, success, message) => Complete(receipt, command, success, message, target, targetVersion)); }
            catch (Exception ex) { Complete(id, command, false, ex.Message, target, targetVersion); }
            return new CommandResult(true, "Accepted; observe OperationCompleted", id);
        }
        void Complete(string id, CommandType command, bool success, string message, string target, string targetVersion)
        {
            if (id != pending || Terminal) return;
            pending = null;
            if (command == CommandType.Finish)
            {
                success &= !mock;
                saveStatus = success ? V2SaveStatus.Verified : V2SaveStatus.Failed;
                if (success) phase = SessionPhase.Finished;
            }
            else if (success)
            { if (target != null) { schemeId = target; modelVersion = targetVersion; schemeHistory.Add(target); } visitId = Guid.NewGuid().ToString("N"); goals.Reset(); }
            Publish(); OperationCompleted?.Invoke(new OperationResult(id, command, success, message));
        }
        public bool ObserveGoal(string session, string visit, string goalId, GoalState state, string evidenceId, long? updatedAtMilliseconds = null)
        {
            if (Terminal || paused || pending != null || (phase != SessionPhase.Experience && phase != SessionPhase.Review) || session != sessionId || visit != visitId) return false;
            if (!goals.Observe(goalId, state, evidenceId, updatedAtMilliseconds)) return false;
            Publish(); return true;
        }
    }
}
