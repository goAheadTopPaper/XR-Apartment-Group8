namespace XRApartment.Data
{
    public enum CombinationDecision
    {
        Unset,
        Accept,
        NeedsRevision,
        Reject
    }

    public enum FeedbackCategory
    {
        Unset,
        DoorClearance,
        Circulation,
        StorageUsability,
        OperationalSpace,
        Other
    }

    public enum EverydayImpact
    {
        Unset,
        Inconvenient,
        ProlongsTask,
        BlocksCirculation,
        CannotComplete,
        Other
    }

    public enum Importance
    {
        Unset,
        Low,
        Medium,
        High
    }

    /// FR-06 boundary: a preset prompt must not be recorded as an autonomous discovery.
    public enum DiscoveryMode
    {
        Unset,
        PresetPrompt,
        HumanObserved,
        FacilitatorIntervention
    }

    public enum TaskOutcome
    {
        Unset,
        Completed,
        Skipped,
        Aborted
    }

    public enum MarkerCategory
    {
        Unset,
        FridgeDoor,
        FridgeDrawer,
        CounterSurface,
        StorageZone,
        CirculationZone
    }
}
