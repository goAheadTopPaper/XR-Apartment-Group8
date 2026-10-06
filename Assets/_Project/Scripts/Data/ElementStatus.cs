namespace XRApartment.Data
{
    /// PRD section 5 scope states and UX-01: the reviewer must be able to tell these apart.
    public enum ElementStatus
    {
        Fixed,
        Reviewable,
        Simplified,
        Assumption,
        Deferred
    }
}
