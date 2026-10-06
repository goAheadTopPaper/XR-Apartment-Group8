using System;
using System.Collections.Generic;

namespace XRApartment.Simulation
{
    /// Plain-C# session flow (PRD 9.2 / 8.5). Deliberately free of UnityEngine types so the
    /// whole journey can be exercised by edit-mode tests without a headset, a scene or XR input.
    public class SessionStateMachine
    {
        private static readonly Dictionary<KeyValuePair<SessionState, SessionTrigger>, SessionState> Transitions =
            new Dictionary<KeyValuePair<SessionState, SessionTrigger>, SessionState>
            {
                { Key(SessionState.Setup, SessionTrigger.CalibrationComplete), SessionState.Orientation },
                { Key(SessionState.Orientation, SessionTrigger.OrientationAcknowledged), SessionState.Explore },
                { Key(SessionState.Explore, SessionTrigger.ChallengeRequested), SessionState.ChallengeActive },
                { Key(SessionState.ChallengeActive, SessionTrigger.ChallengeTaskFinished), SessionState.FeedbackPending },
                { Key(SessionState.ChallengeActive, SessionTrigger.ChallengeTaskSkipped), SessionState.FeedbackPending },
                { Key(SessionState.FeedbackPending, SessionTrigger.FeedbackSubmitted), SessionState.Explore },
                { Key(SessionState.Explore, SessionTrigger.PreferenceConfirmed), SessionState.ComparisonSummary },
                { Key(SessionState.ComparisonSummary, SessionTrigger.PreferenceConfirmed), SessionState.Finished }
            };

        private readonly int expectedBenchmarks;
        private readonly List<string> transitionLog = new List<string>();

        public SessionStateMachine(int expectedBenchmarks)
        {
            if (expectedBenchmarks < 1) throw new ArgumentOutOfRangeException(nameof(expectedBenchmarks));
            this.expectedBenchmarks = expectedBenchmarks;
            ResolvedBenchmarks = 0;
        }

        public SessionState Current { get; private set; } = SessionState.Setup;
        public SessionState StateBeforePause { get; private set; } = SessionState.Setup;
        public int ResolvedBenchmarks { get; private set; }
        public int ExpectedBenchmarks => expectedBenchmarks;
        public bool AllBenchmarksResolved => ResolvedBenchmarks >= expectedBenchmarks;
        public IReadOnlyList<string> TransitionLog => transitionLog;
        public string LastRejection { get; private set; } = string.Empty;

        public bool TryAdvance(SessionTrigger trigger)
        {
            LastRejection = string.Empty;

            if (trigger == SessionTrigger.ExitRequested)
            {
                if (Current == SessionState.Finished)
                {
                    LastRejection = "session already finished";
                    return false;
                }
                return Move(SessionState.Finished, trigger);
            }

            if (trigger == SessionTrigger.PauseRequested)
            {
                if (Current == SessionState.Paused || Current == SessionState.Finished)
                {
                    LastRejection = "cannot pause from " + Current;
                    return false;
                }
                StateBeforePause = Current;
                return Move(SessionState.Paused, trigger);
            }

            if (trigger == SessionTrigger.ResumeRequested)
            {
                if (Current != SessionState.Paused)
                {
                    LastRejection = "not paused";
                    return false;
                }
                return Move(StateBeforePause, trigger);
            }

            if (Current == SessionState.Paused || Current == SessionState.Finished)
            {
                LastRejection = "session is " + Current;
                return false;
            }

            if (trigger == SessionTrigger.PreferenceConfirmed && Current == SessionState.Explore
                && !AllBenchmarksResolved)
            {
                LastRejection = "only " + ResolvedBenchmarks + " of " + expectedBenchmarks +
                    " benchmarks resolved";
                return false;
            }

            if (trigger == SessionTrigger.ChallengeRequested && AllBenchmarksResolved)
            {
                LastRejection = "all benchmarks already resolved";
                return false;
            }

            SessionState next;
            if (!Transitions.TryGetValue(Key(Current, trigger), out next))
            {
                LastRejection = "no transition for " + Current + " + " + trigger;
                return false;
            }

            if (trigger == SessionTrigger.FeedbackSubmitted) ResolvedBenchmarks++;
            return Move(next, trigger);
        }

        private bool Move(SessionState target, SessionTrigger trigger)
        {
            Current = target;
            transitionLog.Add(trigger + " -> " + target);
            return true;
        }

        private static KeyValuePair<SessionState, SessionTrigger> Key(SessionState state, SessionTrigger trigger)
        {
            return new KeyValuePair<SessionState, SessionTrigger>(state, trigger);
        }
    }
}
