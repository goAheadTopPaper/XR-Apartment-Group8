using System;
using System.Collections.Generic;
using XRApartment.Data;

namespace XRApartment.Simulation
{
    public enum SaveStatus { None, Pending, Verified, Failed }
    public enum JourneyActionType { LoadBenchmark, ResetTask, ReturnItem, ReplayBenchmark, Recenter, SaveRecord, RestoreCurrentStep }
    public enum JourneyCommandType
    {
        CompleteSetup, AcknowledgeOrientation, ShowBenchmark, StartChallenge,
        SubmitTaskEvidence, ConfirmReorganisation, SkipChallenge, SubmitFeedback,
        ConfirmComparison, Pause, Resume, ResetCurrentTask, ReturnItemToTray, ReplayCurrentBenchmark,
        Recenter, Retry, RequestExit, SceneCompleted, SaveCompleted, RestoreCurrentStep,
        ConflictObserved, SafetyObserved
    }
    public enum JourneyErrorCode
    {
        None, InvalidPlan, WrongSession, InvalidState, WrongBenchmark,
        InvalidEvidence, InvalidFeedback, OperationFailed, StaleResult
    }

    public sealed class TaskItemSpec
    {
        public string ItemId { get; set; }
        public bool IsExistingItem { get; set; }
        public string SourceZoneId { get; set; }
        public string TemporaryZoneId { get; set; }
        public string ReturnZoneId { get; set; }
    }
    public sealed class TaskAccessSpec
    {
        public string ObjectId { get; set; }
        public ReviewRole Role { get; set; }
    }
    public sealed class TaskConfig
    {
        public IReadOnlyList<TaskItemSpec> Items { get; set; }
        public IReadOnlyList<TaskAccessSpec> AccessTargets { get; set; }
        public string ApproachZoneId { get; set; }
        public string LeaveZoneId { get; set; }
        public float OpenProgressThreshold { get; set; }
        public float ClosedProgressThreshold { get; set; }
    }
    public sealed class SessionPlan
    {
        public string SessionId { get; set; }
        public string ModelVersion { get; set; }
        public IReadOnlyList<string> BenchmarkIds { get; set; }
        public TaskConfig Task { get; set; }
    }
    public sealed class TaskEvidence
    {
        public string SessionId { get; set; }
        public string BenchmarkId { get; set; }
        public long RunId { get; set; }
        public ChallengeStep Step { get; set; }
        public ReviewRole Role { get; set; }
        public string ObjectId { get; set; }
        public string ItemId { get; set; }
        public string ZoneId { get; set; }
        public float OccurredAtSessionSeconds { get; set; }
        public float Measurement { get; set; }
        public bool Released { get; set; }
    }
    public sealed class FeedbackRequest
    {
        public string SessionId { get; set; }
        public string BenchmarkId { get; set; }
        public TaskOutcome TaskOutcome { get; set; }
        public string OutcomeReason { get; set; }
    }
    public sealed class FeedbackDraft
    {
        public string SessionId { get; set; }
        public string BenchmarkId { get; set; }
        public CombinationDecision Decision { get; set; }
        public FeedbackCategory Category { get; set; }
        public Importance Importance { get; set; }
        public string ShortExplanation { get; set; }
        public string SkipReason { get; set; }
        public ExperienceTag Experience { get; set; }
        public EverydayImpact EverydayImpact { get; set; }
        public DiscoveryMode DiscoveryMode { get; set; }
    }
    public sealed class FeedbackValidationResult
    {
        public string SessionId { get; set; }
        public string BenchmarkId { get; set; }
        public bool IsValid { get; set; }
        public IReadOnlyDictionary<string, string> FieldErrors { get; set; }
        public BenchmarkFeedback Feedback { get; set; }
    }
    public sealed class FinalComparison
    {
        public string PreferredBenchmarkId { get; set; }
        public string TradeOffSummary { get; set; }
    }
    public sealed class ConflictNotice
    {
        public string IssueId { get; set; }
        public string SessionId { get; set; }
        public string BenchmarkId { get; set; }
        public string ModelVersion { get; set; }
        public string ObjectId { get; set; }
        public ReviewRole Role { get; set; }
        public string AssumptionKey { get; set; }
        public float Measurement { get; set; }
        public float OccurredAtSessionSeconds { get; set; }
        public string Message { get; set; }
        public bool FeedbackRecorded { get; set; }
    }
    public sealed class SafetyNotice
    {
        public string SafetyEventId { get; set; }
        public string SessionId { get; set; }
        public string RiskType { get; set; }
        public bool IsActive { get; set; }
        public float OccurredAtSessionSeconds { get; set; }
        public string Message { get; set; }
        public string SuggestedAction { get; set; }
    }
    public sealed class SceneActionResult
    {
        public long RequestId { get; set; }
        public bool Succeeded { get; set; }
        public string ActualBenchmarkId { get; set; }
        public JourneyErrorCode ErrorCode { get; set; }
        public string Message { get; set; }
    }
    public sealed class SaveResult
    {
        public long RequestId { get; set; }
        public bool Succeeded { get; set; }
        public bool ReadBackVerified { get; set; }
        public string Location { get; set; }
        public JourneyErrorCode ErrorCode { get; set; }
        public string Message { get; set; }
    }
    public sealed class ReadResult
    {
        public bool Succeeded { get; set; }
        public SessionRecord Record { get; set; }
        public JourneyErrorCode ErrorCode { get; set; }
        public string Message { get; set; }
    }
    public sealed class JourneyAction
    {
        public long RequestId { get; set; }
        public long RunId { get; set; }
        public JourneyActionType Type { get; set; }
        public string SessionId { get; set; }
        public string BenchmarkId { get; set; }
        public string ItemId { get; set; }
        public ChallengeStep? Step { get; set; }
        public SessionRecord Record { get; set; }
    }
    public sealed class JourneySnapshot
    {
        public string SessionId { get; set; }
        public SessionState State { get; set; }
        public SessionOutcome Outcome { get; set; }
        public SaveStatus Save { get; set; }
        public string DisplayedBenchmarkId { get; set; }
        public string ActiveBenchmarkId { get; set; }
        public long ActiveRunId { get; set; }
        public ChallengeStep? CurrentStep { get; set; }
        public int CompletedItemCount { get; set; }
        public int TotalItemCount { get; set; }
        public bool IsBusy { get; set; }
        public IReadOnlyList<string> BenchmarkOrder { get; set; }
        public IReadOnlyDictionary<string, TaskOutcome> BenchmarkOutcomes { get; set; }
        public IReadOnlyList<BenchmarkFeedback> Feedback { get; set; }
        public IReadOnlyList<ConflictNotice> Conflicts { get; set; }
        public SafetyNotice CurrentSafety { get; set; }
    }
    public sealed class JourneyCommand
    {
        public JourneyCommandType Type { get; set; }
        public string SessionId { get; set; }
        public string BenchmarkId { get; set; }
        public string ItemId { get; set; }
        public string Reason { get; set; }
        public bool PrerequisitesMet { get; set; }
        public TaskEvidence Evidence { get; set; }
        public FeedbackValidationResult Feedback { get; set; }
        public FinalComparison Comparison { get; set; }
        public SceneActionResult SceneResult { get; set; }
        public SaveResult SaveResult { get; set; }
        public ConflictNotice Conflict { get; set; }
        public SafetyNotice Safety { get; set; }
    }
    public sealed class JourneyResult
    {
        public bool Accepted { get; set; }
        public JourneyErrorCode ErrorCode { get; set; }
        public string Message { get; set; }
        public JourneySnapshot Snapshot { get; set; }
        public JourneyAction Action { get; set; }
    }

    public interface IJourneyCore
    {
        JourneySnapshot Snapshot { get; }
        JourneyResult Submit(JourneyCommand command);
    }
    public interface IReviewScenePort
    {
        event Action<TaskEvidence> EvidenceObserved;
        void Execute(JourneyAction action, Action<SceneActionResult> completed);
    }
    public interface IFeedbackPort
    {
        FeedbackValidationResult Validate(FeedbackRequest request, FeedbackDraft draft);
    }
    public interface ISessionStore
    {
        void Save(long requestId, SessionRecord record, Action<SaveResult> completed);
        void TryRead(string sessionId, Action<ReadResult> completed);
    }
    public interface IJourneyView
    {
        void Render(JourneySnapshot snapshot, JourneyErrorCode errorCode, string message);
    }
    public interface IConflictSource
    {
        event Action<ConflictNotice> ConflictObserved;
    }
    public interface ISafetySource
    {
        event Action<SafetyNotice> SafetyObserved;
    }
}
