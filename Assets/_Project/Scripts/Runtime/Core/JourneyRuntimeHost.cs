using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using XRApartment.Data;
using XRApartment.Simulation;

namespace XRApartment.Runtime.Core
{
    /// <summary>Unity composition root and the only dispatcher of scene and storage callbacks.</summary>
    public sealed class JourneyRuntimeHost : MonoBehaviour
    {
        [Serializable]
        private sealed class ItemBinding
        {
            public string ItemId;
            public bool IsExistingItem;
            public string SourceZoneId;
            public string TemporaryZoneId;
            public string ReturnZoneId;
        }

        [Serializable]
        private sealed class AccessBinding
        {
            public string ObjectId;
            public ReviewRole Role;
        }

        [Header("Session plan")]
        [SerializeField] private string sessionId;
        [SerializeField] private string modelVersion;
        [SerializeField] private string[] benchmarkOrder = { "B1", "B2" };
        [SerializeField] private ItemBinding[] taskItems;
        [SerializeField] private AccessBinding[] accessTargets;
        [SerializeField] private string approachZoneId;
        [SerializeField] private string leaveZoneId;
        [SerializeField, Range(0f, 1f)] private float openProgressThreshold = 0.9f;
        [SerializeField, Range(0f, 1f)] private float closedProgressThreshold = 0.1f;

        [Header("Ports")]
        [SerializeField] private BenchmarkRegistry registry;
        [SerializeField] private RuntimeBenchmarkAdapter sceneAdapter;
        [SerializeField] private MonoBehaviour feedbackAdapter;
        [SerializeField] private MonoBehaviour storageAdapter;
        [SerializeField] private MonoBehaviour viewAdapter;
        [SerializeField] private MonoBehaviour conflictSource;
        [SerializeField] private MonoBehaviour safetySource;
        [SerializeField, Min(0.1f)] private float operationTimeoutSeconds = 10f;

        private readonly Queue<JourneyCommand> callbackQueue = new Queue<JourneyCommand>();
        private SynchronizationContext unityContext;
        private IFeedbackPort feedback;
        private ISessionStore storage;
        private IJourneyView view;
        private IConflictSource conflicts;
        private ISafetySource safety;
        private SessionPlan plan;
        private long dispatchedRequestId;
        private JourneyActionType dispatchedType;
        private float dispatchedAt;
        private bool handling;
        private float lastBlockedNoticeAt = -10f;

        public IJourneyCore Core { get; private set; }
        public string StartupError { get; private set; }

        private void Awake()
        {
            unityContext = SynchronizationContext.Current;
            feedback = feedbackAdapter as IFeedbackPort;
            storage = storageAdapter as ISessionStore;
            view = viewAdapter as IJourneyView;
            conflicts = conflictSource as IConflictSource;
            safety = safetySource as ISafetySource;
            plan = BuildPlan();
            StartupError = ValidateRuntimePlan(plan);
            if (StartupError != null)
            {
                Debug.LogError("Journey startup: " + StartupError, this);
                return;
            }
            var coordinator = new JourneyCoordinator(plan);
            if (!coordinator.HasValidPlan)
            {
                StartupError = coordinator.PlanError;
                Debug.LogError("Journey startup: " + StartupError, this);
                return;
            }
            Core = coordinator;
            sceneAdapter.Configure(plan, Core);
            sceneAdapter.EvidenceObserved += OnEvidence;
            sceneAdapter.InteractionBlocked += OnInteractionBlocked;
            if (conflicts != null) conflicts.ConflictObserved += OnConflict;
            if (safety != null) safety.SafetyObserved += OnSafety;
            view?.Render(Core.Snapshot, JourneyErrorCode.None, string.Empty);
        }

        private void OnDestroy()
        {
            if (sceneAdapter != null)
            {
                sceneAdapter.EvidenceObserved -= OnEvidence;
                sceneAdapter.InteractionBlocked -= OnInteractionBlocked;
            }
            if (conflicts != null) conflicts.ConflictObserved -= OnConflict;
            if (safety != null) safety.SafetyObserved -= OnSafety;
        }

        private void Update()
        {
            if (Core == null || dispatchedRequestId == 0 ||
                Time.realtimeSinceStartup - dispatchedAt < operationTimeoutSeconds) return;
            var requestId = dispatchedRequestId;
            dispatchedRequestId = 0;
            Enqueue(dispatchedType == JourneyActionType.SaveRecord
                ? new JourneyCommand
                {
                    Type = JourneyCommandType.SaveCompleted,
                    SaveResult = new SaveResult
                    {
                        RequestId = requestId, Succeeded = false,
                        ErrorCode = JourneyErrorCode.OperationFailed,
                        Message = "Storage callback timed out."
                    }
                }
                : new JourneyCommand
                {
                    Type = JourneyCommandType.SceneCompleted,
                    SceneResult = new SceneActionResult
                    {
                        RequestId = requestId, Succeeded = false,
                        ErrorCode = JourneyErrorCode.OperationFailed,
                        Message = "Scene callback timed out."
                    }
                });
        }

        public JourneyResult Submit(JourneyCommand command)
        {
            if (Core == null) return new JourneyResult
            {
                Accepted = false,
                ErrorCode = JourneyErrorCode.InvalidPlan,
                Message = StartupError ?? "Journey is not configured."
            };
            if (command == null) command = new JourneyCommand();
            if (string.IsNullOrEmpty(command.SessionId)) command.SessionId = plan.SessionId;
            handling = true;
            JourneyResult result;
            try { result = Process(command); }
            finally { handling = false; }
            DrainCallbacks();
            return result;
        }

        public JourneyResult SubmitFeedback(FeedbackDraft draft)
        {
            if (Core == null || feedback == null) return new JourneyResult
            {
                Accepted = false, ErrorCode = JourneyErrorCode.InvalidPlan,
                Message = "Feedback port is not configured.", Snapshot = Core?.Snapshot
            };
            var snapshot = Core.Snapshot;
            if (snapshot.State != SessionState.FeedbackPending ||
                string.IsNullOrEmpty(snapshot.ActiveBenchmarkId))
                return Submit(new JourneyCommand { Type = JourneyCommandType.SubmitFeedback });
            if (draft == null) draft = new FeedbackDraft();
            draft.SessionId = snapshot.SessionId;
            draft.BenchmarkId = snapshot.ActiveBenchmarkId;
            var request = new FeedbackRequest
            {
                SessionId = snapshot.SessionId, BenchmarkId = snapshot.ActiveBenchmarkId,
                TaskOutcome = snapshot.BenchmarkOutcomes[snapshot.ActiveBenchmarkId],
                OutcomeReason = draft.SkipReason
            };
            FeedbackValidationResult validation;
            try { validation = feedback.Validate(request, draft); }
            catch (Exception exception)
            {
                return Submit(new JourneyCommand
                {
                    Type = JourneyCommandType.SubmitFeedback,
                    Feedback = new FeedbackValidationResult
                    {
                        SessionId = snapshot.SessionId, BenchmarkId = snapshot.ActiveBenchmarkId,
                        IsValid = false,
                        FieldErrors = new Dictionary<string, string>
                        { { "port", exception.Message } }
                    }
                });
            }
            return Submit(new JourneyCommand
            { Type = JourneyCommandType.SubmitFeedback, Feedback = validation });
        }

        private JourneyResult Process(JourneyCommand command)
        {
            var result = Core.Submit(command);
            sceneAdapter?.SyncExplorePlacement(result.Snapshot);
            view?.Render(result.Snapshot, result.ErrorCode, result.Message);
            if (result.Action != null) Dispatch(result.Action);
            return result;
        }

        private void Dispatch(JourneyAction action)
        {
            dispatchedRequestId = action.RequestId;
            dispatchedType = action.Type;
            dispatchedAt = Time.realtimeSinceStartup;
            try
            {
                if (action.Type == JourneyActionType.SaveRecord)
                {
                    if (storage == null) throw new InvalidOperationException("Storage port is missing.");
                    storage.Save(action.RequestId, action.Record, result => PostCallback(new JourneyCommand
                    {
                        Type = JourneyCommandType.SaveCompleted, SaveResult = result
                    }));
                }
                else
                {
                    if (sceneAdapter == null) throw new InvalidOperationException("Scene port is missing.");
                    sceneAdapter.Execute(action, result => PostCallback(new JourneyCommand
                    {
                        Type = JourneyCommandType.SceneCompleted, SceneResult = result
                    }));
                }
            }
            catch (Exception exception)
            {
                Enqueue(action.Type == JourneyActionType.SaveRecord
                    ? new JourneyCommand
                    {
                        Type = JourneyCommandType.SaveCompleted,
                        SaveResult = new SaveResult
                        {
                            RequestId = action.RequestId, ErrorCode = JourneyErrorCode.OperationFailed,
                            Message = exception.Message
                        }
                    }
                    : new JourneyCommand
                    {
                        Type = JourneyCommandType.SceneCompleted,
                        SceneResult = new SceneActionResult
                        {
                            RequestId = action.RequestId, ErrorCode = JourneyErrorCode.OperationFailed,
                            Message = exception.Message
                        }
                    });
            }
        }

        private void PostCallback(JourneyCommand command)
        {
            if (unityContext != null) unityContext.Post(_ => Enqueue(command), null);
            else Enqueue(command);
        }

        private void Enqueue(JourneyCommand command)
        {
            callbackQueue.Enqueue(command);
            if (!handling) DrainCallbacks();
        }

        private void DrainCallbacks()
        {
            if (handling || Core == null) return;
            handling = true;
            try
            {
                while (callbackQueue.Count > 0)
                {
                    var command = callbackQueue.Dequeue();
                    var requestId = command.Type == JourneyCommandType.SaveCompleted
                        ? command.SaveResult?.RequestId : command.SceneResult?.RequestId;
                    if (requestId == dispatchedRequestId) dispatchedRequestId = 0;
                    Process(command);
                }
            }
            finally { handling = false; }
        }

        private void OnEvidence(TaskEvidence evidence) => Enqueue(new JourneyCommand
        {
            Type = JourneyCommandType.SubmitTaskEvidence,
            SessionId = evidence.SessionId, Evidence = evidence
        });

        private void OnInteractionBlocked(string detail)
        {
            if (Core == null || Time.realtimeSinceStartup - lastBlockedNoticeAt < 1f) return;
            lastBlockedNoticeAt = Time.realtimeSinceStartup;
            view?.Render(Core.Snapshot, JourneyErrorCode.InvalidState, detail);
        }

        private void OnConflict(ConflictNotice notice) => Enqueue(new JourneyCommand
        {
            Type = JourneyCommandType.ConflictObserved,
            SessionId = notice.SessionId, Conflict = notice
        });

        private void OnSafety(SafetyNotice notice) => Enqueue(new JourneyCommand
        {
            Type = JourneyCommandType.SafetyObserved,
            SessionId = notice.SessionId, Safety = notice
        });

        private SessionPlan BuildPlan()
        {
            var items = new List<TaskItemSpec>();
            if (taskItems != null)
                foreach (var item in taskItems)
                    if (item != null) items.Add(new TaskItemSpec
                    {
                        ItemId = item.ItemId, IsExistingItem = item.IsExistingItem,
                        SourceZoneId = item.SourceZoneId,
                        TemporaryZoneId = item.TemporaryZoneId,
                        ReturnZoneId = item.ReturnZoneId
                    });
            var access = new List<TaskAccessSpec>();
            if (accessTargets != null)
                foreach (var target in accessTargets)
                    if (target != null) access.Add(new TaskAccessSpec
                    { ObjectId = target.ObjectId, Role = target.Role });
            return new SessionPlan
            {
                SessionId = string.IsNullOrWhiteSpace(sessionId)
                    ? Guid.NewGuid().ToString("N") : sessionId.Trim(),
                ModelVersion = modelVersion,
                BenchmarkIds = benchmarkOrder == null ? null : new List<string>(benchmarkOrder),
                Task = new TaskConfig
                {
                    Items = items, AccessTargets = access,
                    ApproachZoneId = approachZoneId, LeaveZoneId = leaveZoneId,
                    OpenProgressThreshold = openProgressThreshold,
                    ClosedProgressThreshold = closedProgressThreshold
                }
            };
        }

        private string ValidateRuntimePlan(SessionPlan sessionPlan)
        {
            if (registry == null || sceneAdapter == null)
                return "Benchmark registry or runtime scene adapter is missing.";
            if (sessionPlan.BenchmarkIds == null) return "Benchmark order is missing.";
            foreach (var id in sessionPlan.BenchmarkIds)
            {
                var definition = registry.Find(id);
                if (definition == null || !definition.Allows(SessionMode.Challenge))
                    return "Registry has no Challenge benchmark: " + id;
                if (!sceneAdapter.HasBinding(id)) return "Runtime prefab binding is missing: " + id;
            }
            if (!sceneAdapter.ValidatePlanWiring(sessionPlan, registry, out var wiringError))
                return wiringError;
            return null;
        }
    }
}
