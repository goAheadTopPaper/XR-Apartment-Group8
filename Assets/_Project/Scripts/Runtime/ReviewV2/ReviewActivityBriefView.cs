using TMPro;
using UnityEngine;
using XRApartment.Data.ReviewV2;

namespace XRApartment.Runtime.ReviewV2
{
    // Read-only activity guidance. Goal evidence and progression remain owned by the host.
    public sealed class ReviewActivityBriefView : MonoBehaviour
    {
        public TMP_Text introduction;
        public TMP_Text[] goalTitles, goalStates;
        static readonly string[] Titles =
        {
            "G01  Put away today's groceries", "G02  Cook a tomato and egg stir-fry",
            "G03  Serve the dish at the dining table", "G04  Reheat a leftover dish", "G05  Prepare a cup of hot water"
        };

        public void ConfigurePresentation()
        {
            KitchenLocale.Set(introduction, "Experience the kitchen design under discussion and notice whether everyday tasks feel easy.\nActivities are free to reorder, overlap or skip.\nYou can Mark or enter Review at any time.");
            foreach (var text in GetComponentsInChildren<TMP_Text>(true))
            {
                text.fontSize = 28; text.enableAutoSizing = false;
                text.alignment = TextAlignmentOptions.TopLeft;
                text.overflowMode = TextOverflowModes.Truncate;
                text.raycastTarget = false;
            }
            for (int i = 0; i < Titles.Length; i++) KitchenLocale.Set(goalTitles[i], Titles[i]);
        }

        public void Present(SessionSnapshot snapshot)
        {
            for (int i = 0; i < Titles.Length; i++)
            {
                GoalSnapshot goal = null;
                foreach (var candidate in snapshot.Goals)
                    if (candidate.Id == "G0" + (i + 1)) { goal = candidate; break; }
                bool available = goal != null && goal.Available;
                var state = !available ? "Unavailable" : goal.State == GoalState.Completed ? "Completed" :
                    goal.State == GoalState.InProgress ? "In Progress" : "Not Started";
                KitchenLocale.Set(goalStates[i], state);
                goalStates[i].color = !available ? new Color(1, .81f, .44f) :
                    goal.State == GoalState.Completed ? new Color(.5f, 1, .72f) : Color.white;
            }
        }
    }
}
