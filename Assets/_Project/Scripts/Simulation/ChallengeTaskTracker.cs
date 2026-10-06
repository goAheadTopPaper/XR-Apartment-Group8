using System;
using System.Collections.Generic;

namespace XRApartment.Simulation
{
    /// PRD 8.4 standard action chain. The same eight steps run for every benchmark; only the
    /// geometry they resolve against changes (FR-03 / D07).
    public enum ChallengeStep
    {
        Approach,
        Open,
        TakeOut,
        TemporaryPlacement,
        Reorganise,
        PutBack,
        Close,
        Leave
    }

    public class ChallengeTaskTracker
    {
        public const int MinItemCount = 3;
        public const int MaxItemCount = 5;

        private static readonly ChallengeStep[] Chain =
        {
            ChallengeStep.Approach,
            ChallengeStep.Open,
            ChallengeStep.TakeOut,
            ChallengeStep.TemporaryPlacement,
            ChallengeStep.Reorganise,
            ChallengeStep.PutBack,
            ChallengeStep.Close,
            ChallengeStep.Leave
        };

        private int cursor;

        public ChallengeTaskTracker(int itemCount)
        {
            if (!IsValidItemCount(itemCount))
                throw new ArgumentOutOfRangeException(nameof(itemCount),
                    "each benchmark uses an equivalent 3-5 item load");
            ItemCount = itemCount;
        }

        public static IReadOnlyList<ChallengeStep> StandardChain => Chain;

        public int ItemCount { get; private set; }
        public ChallengeStep? CurrentStep => cursor < Chain.Length ? Chain[cursor] : (ChallengeStep?)null;
        public int CompletedStepCount => cursor;
        public bool IsComplete => cursor >= Chain.Length;

        public static bool IsValidItemCount(int itemCount)
        {
            return itemCount >= MinItemCount && itemCount <= MaxItemCount;
        }

        public bool Advance()
        {
            if (IsComplete) return false;
            cursor++;
            return true;
        }

        /// The chain is fixed: advancing out of order would make benchmark results incomparable.
        public bool TryAdvanceTo(ChallengeStep step)
        {
            if (IsComplete || CurrentStep.Value != step) return false;
            cursor++;
            return true;
        }

        public void Reset() => cursor = 0;
    }
}
