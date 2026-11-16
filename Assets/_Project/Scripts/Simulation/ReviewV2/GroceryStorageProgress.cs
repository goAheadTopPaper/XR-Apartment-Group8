using System;
using System.Collections.Generic;
using XRApartment.Data.ReviewV2;

namespace XRApartment.Simulation.ReviewV2
{
    // Cumulative successful placements in one Visit; cooking may subsequently retrieve them.
    public sealed class GroceryStorageProgress
    {
        readonly HashSet<string> eligible, stored = new HashSet<string>();
        readonly int required;
        public GoalState State => stored.Count == 0 ? GoalState.NotStarted :
            stored.Count >= required ? GoalState.Completed : GoalState.InProgress;
        public GroceryStorageProgress(IEnumerable<string> ids, int requiredCount)
        {
            eligible = new HashSet<string>(ids);
            if (eligible.Contains(null) || eligible.Contains("") || requiredCount < 2 || requiredCount > eligible.Count)
                throw new ArgumentException("Grocery activity requires distinct stable IDs and at least two placements");
            required = requiredCount;
        }
        public bool Store(string id) => id != null && eligible.Contains(id) && stored.Add(id);
        public void Reset() => stored.Clear();
    }
}
