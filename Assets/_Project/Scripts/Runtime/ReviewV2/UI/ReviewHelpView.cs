using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XRApartment.Data.ReviewV2;

namespace XRApartment.Runtime.ReviewV2.UI
{
    public sealed class ReviewHelpView : MonoBehaviour
    {
        const string HelpPanelId = "help";
        const string GlobalMenuPanelId = "global_menu";

        [Serializable]
        public sealed class Page
        {
            public GameObject root;
            [NonSerialized] public UnityEngine.Events.UnityAction onPrevious;
            [NonSerialized] public UnityEngine.Events.UnityAction onNext;
            public Button previous;
            public Button next;
        }

        [SerializeField] ReviewV2UiSessionAdapter session;
        [SerializeField] ReviewV2UiController uiController;
        [SerializeField] GameObject panel;
        [SerializeField] Button backButton;
        [SerializeField] Button menuHelpButton;
        [SerializeField] Page[] pages = Array.Empty<Page>();

        IReviewV2UiSession boundSession;
        IReviewV2UiSession subscribedSession;
        bool hasSessionOverride;
        string openedSessionId;
        int pageIndex;

        IReviewV2UiSession Session => hasSessionOverride ? boundSession : session;
        public bool IsOpen => isActiveAndEnabled && panel != null && panel.activeInHierarchy;

        void OnEnable()
        {
            HideAndReset();
            if (backButton != null) backButton.onClick.AddListener(Close);
            if (menuHelpButton != null) menuHelpButton.onClick.AddListener(OpenFromMenu);
            foreach (var page in pages)
            {
                page.onPrevious = () => Navigate(page, -1);
                page.onNext = () => Navigate(page, 1);
                if (page.previous != null) page.previous.onClick.AddListener(page.onPrevious);
                if (page.next != null) page.next.onClick.AddListener(page.onNext);
            }
            Subscribe();
        }

        void OnDisable()
        {
            Unsubscribe();
            if (backButton != null) backButton.onClick.RemoveListener(Close);
            if (menuHelpButton != null) menuHelpButton.onClick.RemoveListener(OpenFromMenu);
            foreach (var page in pages)
            {
                if (page.previous != null) page.previous.onClick.RemoveListener(page.onPrevious);
                if (page.next != null) page.next.onClick.RemoveListener(page.onNext);
            }
            HideAndReset();
        }

        void Update()
        {
            // The adapter can lose its source without publishing Changed(null).
            if (openedSessionId != null) ValidateOpenSession(ReadSnapshot());
        }

        public void BindSession(IReviewV2UiSession uiSession)
        {
            if (!ReferenceEquals(Session, uiSession)) HideAndReset();
            hasSessionOverride = true;
            boundSession = uiSession;
            if (!isActiveAndEnabled) return;

            Subscribe();
            ValidateOpenSession(ReadSnapshot());
        }

        public bool TryOpen()
        {
            if (!isActiveAndEnabled) return false;
            var snapshot = ReadSnapshot();
            ValidateOpenSession(snapshot);
            if (!CanOpen(snapshot) || panel == null || pages.Length == 0 || uiController == null) return false;
            if (IsOpen) return true;

            ShowPage(0);
            if (!uiController.ShowPanel(HelpPanelId) || !IsOpen) return false;
            openedSessionId = snapshot.SessionId;
            FocusPage();
            return true;
        }

        public void OpenFromMenu() => TryOpen();

        public void Close()
        {
            var snapshot = ReadSnapshot();
            bool returnToMenu = IsOpen && IsSameExperience(snapshot);
            HideAndReset();
            if (returnToMenu && uiController != null && uiController.ShowPanel(GlobalMenuPanelId))
                Focus(menuHelpButton);
        }

        void Navigate(Page sourcePage, int offset)
        {
            ValidateOpenSession(ReadSnapshot());
            if (!IsOpen || !ReferenceEquals(pages[pageIndex], sourcePage)) return;
            int index = pageIndex + offset;
            if (index < 0 || index >= pages.Length) return;
            ShowPage(index);
            FocusPage();
        }

        void ShowPage(int index)
        {
            pageIndex = index;
            for (int i = 0; i < pages.Length; i++)
            {
                var page = pages[i];
                if (page.root != null) page.root.SetActive(i == index);
                if (page.previous != null) page.previous.interactable = i > 0;
                if (page.next != null) page.next.interactable = i < pages.Length - 1;
            }
        }

        void FocusPage()
        {
            var page = pages[pageIndex];
            Focus(page.next != null && page.next.IsInteractable() ? page.next : page.previous);
        }

        static void Focus(Button button)
        {
            if (button != null && button.gameObject.activeInHierarchy && button.IsInteractable() &&
                EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(button.gameObject);
        }

        void HideAndReset()
        {
            var eventSystem = EventSystem.current;
            if (panel != null)
            {
                var selected = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
                if (selected != null && selected.transform.IsChildOf(panel.transform))
                    eventSystem.SetSelectedGameObject(null);
                panel.SetActive(false);
            }

            openedSessionId = null;
            ShowPage(0);
        }

        void Subscribe()
        {
            var source = Session;
            if (ReferenceEquals(source, subscribedSession)) return;
            Unsubscribe();
            subscribedSession = source;
            if (subscribedSession != null) subscribedSession.Changed += ValidateOpenSession;
        }

        void Unsubscribe()
        {
            if (subscribedSession != null) subscribedSession.Changed -= ValidateOpenSession;
            subscribedSession = null;
        }

        SessionSnapshot ReadSnapshot()
        {
            var source = Session;
            if (source is ReviewV2UiSessionAdapter adapter && (adapter == null || !adapter.isActiveAndEnabled))
                return null;

            return source?.Snapshot;
        }

        static bool CanOpen(SessionSnapshot snapshot) =>
            snapshot?.Phase == SessionPhase.Experience && !string.IsNullOrEmpty(snapshot.SessionId);

        bool IsSameExperience(SessionSnapshot snapshot) =>
            CanOpen(snapshot) && string.Equals(openedSessionId, snapshot.SessionId, StringComparison.Ordinal);

        void ValidateOpenSession(SessionSnapshot snapshot)
        {
            if (openedSessionId != null && (!IsOpen || !IsSameExperience(snapshot))) HideAndReset();
        }
    }
}