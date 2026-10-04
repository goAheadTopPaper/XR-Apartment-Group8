using System;
using System.Collections.Generic;

namespace XRApartment.Data
{
    [Flags]
    public enum ExperienceTag
    {
        None = 0,
        DoorBlocked = 1,
        NeedsToStepBack = 2,
        HardToReach = 4,
        NotEnoughTempSpace = 8,
        HardToTurn = 16,
        Other = 32
    }

    [Serializable]
    public class BenchmarkFeedback
    {
        public string combinationId;
        public CombinationDecision decision = CombinationDecision.Unset;
        public FeedbackCategory category = FeedbackCategory.Unset;
        public ExperienceTag experience = ExperienceTag.None;
        public EverydayImpact everydayImpact = EverydayImpact.Unset;
        public Importance importance = Importance.Unset;
        public DiscoveryMode discoveryMode = DiscoveryMode.Unset;
        public TaskOutcome taskOutcome = TaskOutcome.Unset;
        public string outcomeReason;

        /// FR-07 acceptance: every combination-level judgement field must be present.
        public bool HasAllRequiredFields()
        {
            return !string.IsNullOrEmpty(combinationId)
                && decision != CombinationDecision.Unset
                && category != FeedbackCategory.Unset
                && experience != ExperienceTag.None
                && everydayImpact != EverydayImpact.Unset
                && importance != Importance.Unset
                && discoveryMode != DiscoveryMode.Unset
                && taskOutcome != TaskOutcome.Unset;
        }

        public IEnumerable<string> MissingRequiredFields()
        {
            if (string.IsNullOrEmpty(combinationId)) yield return "combinationId";
            if (decision == CombinationDecision.Unset) yield return "decision";
            if (category == FeedbackCategory.Unset) yield return "category";
            if (experience == ExperienceTag.None) yield return "experience";
            if (everydayImpact == EverydayImpact.Unset) yield return "everydayImpact";
            if (importance == Importance.Unset) yield return "importance";
            if (discoveryMode == DiscoveryMode.Unset) yield return "discoveryMode";
            if (taskOutcome == TaskOutcome.Unset) yield return "taskOutcome";
            if (taskOutcome != TaskOutcome.Completed && string.IsNullOrEmpty(outcomeReason))
                yield return "outcomeReason";
        }
    }

    [Serializable]
    public class MarkerRecord
    {
        public string markerId;
        public string combinationId;
        public MarkerCategory category = MarkerCategory.Unset;
        public string targetName;
        public float x;
        public float y;
        public float z;
        public string everydayImpactNote;
    }

    [Serializable]
    public class TimedEvent
    {
        public string type;
        public string combinationId;
        public float sessionSeconds;
    }

    /// PRD 11.3 local session record. Anonymous by construction: no name, account,
    /// audio or video field exists anywhere in this type.
    [Serializable]
    public class SessionRecord
    {
        public string sessionId;
        public string modelVersion;
        public List<string> benchmarkOrder = new List<string>();
        public List<BenchmarkFeedback> benchmarkFeedback = new List<BenchmarkFeedback>();
        public List<MarkerRecord> markers = new List<MarkerRecord>();
        public List<TimedEvent> promptEvents = new List<TimedEvent>();
        public List<TimedEvent> assistanceEvents = new List<TimedEvent>();
        public List<TimedEvent> comfortOrAbort = new List<TimedEvent>();
        public string finalPreferenceCombinationId;
        public string finalTradeOffSummary;
        public float totalSeconds;

        public BenchmarkFeedback FindFeedback(string combinationId)
        {
            for (int i = 0; i < benchmarkFeedback.Count; i++)
            {
                if (benchmarkFeedback[i] != null && benchmarkFeedback[i].combinationId == combinationId)
                    return benchmarkFeedback[i];
            }
            return null;
        }

        /// Session Definition of Done (PRD 8.5): all benchmarks judged, preference chosen,
        /// and the record stamped with the model version it was collected against.
        public bool IsComplete(int expectedBenchmarkCount)
        {
            if (string.IsNullOrEmpty(sessionId) || string.IsNullOrEmpty(modelVersion)) return false;
            if (string.IsNullOrEmpty(finalPreferenceCombinationId)) return false;
            if (benchmarkFeedback.Count < expectedBenchmarkCount) return false;
            for (int i = 0; i < benchmarkFeedback.Count; i++)
            {
                if (benchmarkFeedback[i] == null || !benchmarkFeedback[i].HasAllRequiredFields()) return false;
            }
            return true;
        }
    }
}
