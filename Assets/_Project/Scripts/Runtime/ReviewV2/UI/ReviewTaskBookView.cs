using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using XRApartment.Data.ReviewV2;

namespace XRApartment.Runtime.ReviewV2.UI
{
    public sealed class ReviewTaskBookView : MonoBehaviour
    {
        const string TaskBookPanelId = "task_book";
        const string GlobalMenuPanelId = "global_menu";

        [Serializable]
        public sealed class GoalCard
        {
            public string id;
            public string displayTitle;
            public TMP_Text title;
            public TMP_Text status;
            public Image accent;
            public Image statusBackground;

            public void Render(GoalSnapshot goal)
            {
                KitchenLocale.Set(title, string.IsNullOrWhiteSpace(displayTitle) ? "Goal " + id : displayTitle);

                if (goal == null || !goal.Available)
                {
                    SetStatus("Unavailable", new Color32(180, 191, 219, 255), new Color32(52, 62, 83, 255));
                    return;
                }

                switch (goal.State)
                {
                    case GoalState.NotStarted:
                        SetStatus("Not Started", new Color32(176, 203, 227, 255), new Color32(40, 61, 80, 255));
                        break;
                    case GoalState.InProgress:
                        SetStatus("In Progress", new Color32(143, 200, 255, 255), new Color32(29, 65, 105, 255));
                        break;
                    case GoalState.Completed:
                        SetStatus("Completed", new Color32(126, 238, 210, 255), new Color32(16, 80, 71, 255));
                        break;
                    default:
                        SetStatus("Unavailable", new Color32(180, 191, 219, 255), new Color32(52, 62, 83, 255));
                        break;
                }
            }

            void SetStatus(string label, Color foreground, Color background)
            {
                if (status != null)
                {
                    KitchenLocale.Set(status, label);
                    status.color = foreground;
                }
                if (accent != null) accent.color = foreground;
                if (statusBackground != null) statusBackground.color = background;
            }
        }

        [SerializeField] ReviewV2UiSessionAdapter session;
        [SerializeField] ReviewV2UiController uiController;
        [SerializeField] TMP_Text sessionLabel;
        [SerializeField] GoalCard[] cards = Array.Empty<GoalCard>();

        IReviewV2UiSession boundSession;
        IReviewV2UiSession subscribedSession;

        IReviewV2UiSession Session => boundSession ?? session;
        public bool CanOpen => isActiveAndEnabled && Session?.Snapshot?.Phase == SessionPhase.Experience;

        void OnEnable()
        {
            Subscribe();
            RefreshFromSnapshot();
        }

        void OnDisable()
        {
            Unsubscribe();
            HideTaskBook();
        }

        public void BindSession(IReviewV2UiSession uiSession)
        {
            Unsubscribe();
            boundSession = uiSession;
            if (!isActiveAndEnabled) return;
            Subscribe();
            RefreshFromSnapshot();
        }

        public bool TryOpenFromMenu()
        {
            if (!isActiveAndEnabled) return false;
            var snapshot = Session?.Snapshot;
            Render(snapshot);
            return snapshot?.Phase == SessionPhase.Experience &&
                   uiController != null && uiController.ShowPanel(TaskBookPanelId);
        }

        // Unity Button events require a void callback.
        public void OpenFromMenu() => TryOpenFromMenu();

        public void Close()
        {
            HideTaskBook();
            if (CanOpen && uiController != null) uiController.ShowPanel(GlobalMenuPanelId);
        }

        public void RefreshFromSnapshot() => Render(Session?.Snapshot);

        void Subscribe()
        {
            var current = Session;
            if (ReferenceEquals(subscribedSession, current)) return;
            Unsubscribe();
            subscribedSession = current;
            if (subscribedSession != null) subscribedSession.Changed += Render;
        }

        void Unsubscribe()
        {
            if (subscribedSession != null) subscribedSession.Changed -= Render;
            subscribedSession = null;
        }

        void Render(SessionSnapshot snapshot)
        {
            if (snapshot?.Phase != SessionPhase.Experience) HideTaskBook();
            if (sessionLabel != null)
            {
                bool isMock = snapshot?.IsMock == true;
                KitchenLocale.Set(sessionLabel, isMock ? "Mock session" : string.Empty);
                sessionLabel.gameObject.SetActive(isMock);
            }

            foreach (var card in cards)
            {
                if (card == null || string.IsNullOrEmpty(card.id)) continue;
                GoalSnapshot match = null;
                if (snapshot != null)
                {
                    foreach (var goal in snapshot.Goals)
                    {
                        if (goal == null || !string.Equals(goal.Id, card.id, StringComparison.Ordinal)) continue;
                        match = goal;
                        break;
                    }
                }
                card.Render(match);
            }
        }

        void HideTaskBook()
        {
            if (uiController != null) uiController.SetPanelVisible(TaskBookPanelId, false);
        }
    }
}
