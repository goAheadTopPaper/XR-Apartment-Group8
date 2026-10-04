namespace XRApartment.Simulation
{
    /// PRD 9.2 system states. Challenge.Active and Feedback.Pending are per benchmark and
    /// repeat for every combination in the rotated order.
    public enum SessionState
    {
        Setup,
        Orientation,
        Explore,
        ChallengeActive,
        FeedbackPending,
        ComparisonSummary,
        Paused,
        Finished
    }

    public enum SessionTrigger
    {
        CalibrationComplete,
        OrientationAcknowledged,
        ChallengeRequested,
        ChallengeTaskFinished,
        ChallengeTaskSkipped,
        FeedbackSubmitted,
        PreferenceConfirmed,
        PauseRequested,
        ResumeRequested,
        ExitRequested
    }
}
