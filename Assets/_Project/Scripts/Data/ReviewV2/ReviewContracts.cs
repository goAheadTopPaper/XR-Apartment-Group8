using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace XRApartment.Data.ReviewV2
{
    public enum SessionPhase { Setup, Onboarding, Experience, Review, Finished, Exited }
    public enum GoalState { NotStarted, InProgress, Completed }
    public enum SaveStatus { NotConfigured, Pending, Failed, Verified }
    public enum CommandType { CompleteSetup, CompleteOnboarding, Review, Continue, Finish, Pause, Resume, Recenter, ChangeScheme, ResetWorld, SafeExit, CalibrateStanding, CalibrateSeated }
    public static class SchemeIds
    {
        public const string Baseline = "COOKING_BASELINE";
        public const string IntegratedKitchen = "SCHEME_A";
        public const string ExternalRefrigerator = "SCHEME_B";
    }
    public enum InteractionKind { ObjectGrabbed, ObjectReleased, DishCooked }
    public sealed class CommandResult
    {
        public bool Accepted { get; }
        public string Reason { get; }
        public string RequestId { get; }
        public CommandResult(bool accepted, string reason = "", string requestId = null)
        { Accepted = accepted; Reason = reason; RequestId = requestId; }
    }
    public sealed class GoalSnapshot
    {
        public string Id { get; }
        public GoalState State { get; }
        public bool Available { get; }
        public string EvidenceId { get; }
        public long? UpdatedAtMilliseconds { get; }
        public GoalSnapshot(string id, GoalState state, bool available, string evidenceId = null, long? updatedAtMilliseconds = null)
        { Id = id; State = state; Available = available; EvidenceId = evidenceId; UpdatedAtMilliseconds = updatedAtMilliseconds; }
    }
    public sealed class SessionSnapshot
    {
        public string SessionId { get; }
        public SessionPhase Phase { get; }
        public string SchemeId { get; }
        public string SchemeVisitId { get; }
        public string ModelVersion { get; }
        public IReadOnlyList<GoalSnapshot> Goals { get; }
        public IReadOnlyList<string> SchemeHistory { get; }
        public bool IsPaused { get; }
        public bool IsBusy { get; }
        public bool IsMock { get; }
        public SaveStatus SaveStatus { get; }
        public bool CanReview => Phase == SessionPhase.Experience && !IsPaused && !IsBusy;
        public SessionSnapshot(string sessionId, SessionPhase phase, string schemeId, string visitId,
            string modelVersion, IEnumerable<GoalSnapshot> goals, bool paused, bool busy, bool mock, SaveStatus saveStatus,
            IEnumerable<string> schemeHistory = null)
        {
            SessionId = sessionId; Phase = phase; SchemeId = schemeId; SchemeVisitId = visitId;
            ModelVersion = modelVersion; Goals = new ReadOnlyCollection<GoalSnapshot>(new List<GoalSnapshot>(goals));
            IsPaused = paused; IsBusy = busy; IsMock = mock; SaveStatus = saveStatus;
            SchemeHistory = new ReadOnlyCollection<string>(new List<string>(schemeHistory ?? new[] { schemeId }));
        }
    }
    public sealed class InteractionEvent
    {
        public const string SchemaVersion = "review-v2-m0-1";
        public string EventId { get; }
        public string SessionId { get; }
        public string SchemeId { get; }
        public string SchemeVisitId { get; }
        public string ModelVersion { get; }
        public long ElapsedMilliseconds { get; }
        public InteractionKind Kind { get; }
        public string ObjectId { get; }
        public string ZoneId { get; }
        public string AnchorId { get; }
        public bool IsMock { get; }
        public InteractionEvent(SessionSnapshot context, InteractionKind kind, string objectId, long elapsed,
            string zoneId = null, string anchorId = null)
        {
            EventId = Guid.NewGuid().ToString("N"); SessionId = context.SessionId; SchemeId = context.SchemeId;
            SchemeVisitId = context.SchemeVisitId; ModelVersion = context.ModelVersion; IsMock = context.IsMock;
            Kind = kind; ObjectId = objectId; ElapsedMilliseconds = elapsed; ZoneId = zoneId; AnchorId = anchorId;
        }
    }
    public interface ISessionReadModel { SessionSnapshot Snapshot { get; } event Action<SessionSnapshot> Changed; }
    public interface IReviewSessionCommands { CommandResult Execute(CommandType command, string targetSchemeId = null); }
    public interface IInteractionEventSource { event Action<InteractionEvent> Observed; }
    // Ports must leave the old world intact on rejection/failure. Complete only after world restoration.
    public interface ISchemePort
    {
        bool Supports(string schemeId);
        void Change(string requestId, string schemeId, Action<string, bool, string> completed);
    }
    public interface IWorldPort
    {
        void Reset(string requestId, Action<string, bool, string> completed);
        CommandResult Recenter();
    }
    public interface ICalibrationPort { CommandResult Calibrate(bool seated); }
    // Verified means durable write AND read-back confirmation. Mock providers must never return verified=true.
    public interface ISessionSavePort
    {
        void Save(string requestId, SessionSnapshot snapshot, Action<string, bool, string> completed);
    }
    public sealed class OperationResult
    {
        public string RequestId { get; }
        public CommandType Command { get; }
        public bool Succeeded { get; }
        public string Message { get; }
        public OperationResult(string id, CommandType command, bool success, string message)
        { RequestId = id; Command = command; Succeeded = success; Message = message; }
    }
}
