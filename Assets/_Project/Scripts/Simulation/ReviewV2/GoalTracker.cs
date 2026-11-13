using System.Collections.Generic;
using XRApartment.Data.ReviewV2;

namespace XRApartment.Simulation.ReviewV2
{
    public sealed class GoalTracker
    {
        readonly Dictionary<string, GoalSnapshot> goals = new Dictionary<string, GoalSnapshot>();
        public GoalTracker(bool allAvailable = false)
        { for (int i = 1; i <= 5; i++) { var id = "G0" + i; goals.Add(id, new GoalSnapshot(id, GoalState.NotStarted, allAvailable || i == 2)); } }
        public IEnumerable<GoalSnapshot> Snapshot => new List<GoalSnapshot>(goals.Values);
        public bool Observe(string id, GoalState state, string evidenceId)
        {
            if (!goals.TryGetValue(id, out var old) || !old.Available || string.IsNullOrWhiteSpace(evidenceId) ||
                state <= old.State || state > GoalState.Completed) return false;
            goals[id] = new GoalSnapshot(id, state, true, evidenceId); return true;
        }
        public void Reset()
        { foreach (var id in new List<string>(goals.Keys)) goals[id] = new GoalSnapshot(id, GoalState.NotStarted, goals[id].Available); }
    }
}
