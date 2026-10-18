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
        public string shortExplanation;
        public string outcomeReason;

        /// FR-07 acceptance: completed tasks require the four judgement fields; skipped tasks require a reason.
        public bool HasAllRequiredFields()
        {
            using (var missing = MissingRequiredFields().GetEnumerator())
                return !missing.MoveNext();
        }

        public IEnumerable<string> MissingRequiredFields()
        {
            if (string.IsNullOrWhiteSpace(combinationId)) yield return "combinationId";
            if (taskOutcome == TaskOutcome.Completed)
            {
                if (decision == CombinationDecision.Unset) yield return "decision";
                if (category == FeedbackCategory.Unset) yield return "category";
                if (importance == Importance.Unset) yield return "importance";
                if (string.IsNullOrWhiteSpace(shortExplanation)) yield return "shortExplanation";
            }
            if (taskOutcome != TaskOutcome.Completed && taskOutcome != TaskOutcome.Skipped)
                yield return "taskOutcome";
            if (taskOutcome == TaskOutcome.Skipped && string.IsNullOrWhiteSpace(outcomeReason))
                yield return "outcomeReason";
        }
    }

    [Serializable]
    public class ConflictRecord
    {
        public string issueId;
        public string combinationId;
        public string modelVersion;
        public string objectId;
        public ReviewRole role;
        public string assumptionKey;
        public float measurement;
        public float sessionSeconds;
        public string message;
        public bool feedbackRecorded;
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
        public List<ConflictRecord> conflicts = new List<ConflictRecord>();
        public string finalPreferenceCombinationId;
        public string finalTradeOffSummary;
        public SessionOutcome sessionOutcome = SessionOutcome.None;
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

        /// All planned benchmarks must be resolved; all-skipped sessions have no preference.
        public bool IsComplete(int expectedBenchmarkCount)
        {
            if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(modelVersion)) return false;
            if (expectedBenchmarkCount <= 0 || benchmarkFeedback == null ||
                benchmarkFeedback.Count != expectedBenchmarkCount) return false;
            var completedCount = 0;
            var preferenceIsCompleted = false;
            for (int i = 0; i < benchmarkFeedback.Count; i++)
            {
                if (benchmarkFeedback[i] == null || !benchmarkFeedback[i].HasAllRequiredFields()) return false;
                if (benchmarkFeedback[i].taskOutcome == TaskOutcome.Completed)
                {
                    completedCount++;
                    if (benchmarkFeedback[i].combinationId == finalPreferenceCombinationId)
                        preferenceIsCompleted = true;
                }
            }
            if (completedCount == 0) return sessionOutcome == SessionOutcome.Incomplete;
            return preferenceIsCompleted && !string.IsNullOrWhiteSpace(finalTradeOffSummary) &&
                sessionOutcome == (completedCount == expectedBenchmarkCount
                    ? SessionOutcome.Completed : SessionOutcome.Incomplete);
        }
    }
}
