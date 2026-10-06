namespace XRApartment.Data
{
    /// Semantic roles used to resolve challenge-task targets, so that B1/B2/B3 can run the
    /// identical action chain (FR-03 / D07) without referencing per-benchmark object names.
    public enum ReviewRole
    {
        FridgeDoor,
        FridgeDrawer,
        CounterSurface,
        StorageZone,
        CirculationZone,
        FeedbackAnchor
    }
}
