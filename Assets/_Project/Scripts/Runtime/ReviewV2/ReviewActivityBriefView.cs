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
            "G01  将今天买回来的食材放好", "G02  做好一份西红柿炒蛋",
            "G03  将做好的菜端上餐桌", "G04  加热一份剩菜", "G05  准备一杯热水"
        };

        public void ConfigurePresentation()
        {
            introduction.text = "体验待讨论的厨房设计，感受日常使用是否顺手。\n活动顺序自由，可以同时尝试或中途换一项。\n随时可 Mark 或进入 Review，无需完成活动。";
            foreach (var text in GetComponentsInChildren<TMP_Text>(true))
            {
                text.fontSize = 28; text.enableAutoSizing = false;
                text.alignment = TextAlignmentOptions.TopLeft;
                text.overflowMode = TextOverflowModes.Truncate;
                text.raycastTarget = false;
            }
            for (int i = 0; i < Titles.Length; i++) goalTitles[i].text = Titles[i];
        }

        public void Present(SessionSnapshot snapshot)
        {
            for (int i = 0; i < Titles.Length; i++)
            {
                GoalSnapshot goal = null;
                foreach (var candidate in snapshot.Goals)
                    if (candidate.Id == "G0" + (i + 1)) { goal = candidate; break; }
                bool available = goal != null && goal.Available;
                goalStates[i].text = !available ? "未接入" : goal.State == GoalState.Completed ? "已完成" :
                    goal.State == GoalState.InProgress ? "进行中" : "尚未开始";
                goalStates[i].color = !available ? new Color(1, .81f, .44f) :
                    goal.State == GoalState.Completed ? new Color(.5f, 1, .72f) : Color.white;
            }
        }
    }
}
