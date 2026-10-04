using System;

namespace XRApartment.Data
{
    public enum CabinetStrategy
    {
        Proud,
        FlushOriented
    }

    public enum FridgeArchetype
    {
        F2,
        F3,
        F4
    }

    [Flags]
    public enum SessionMode
    {
        None = 0,
        Explore = 1,
        Challenge = 2,
        Both = Explore | Challenge
    }
}
