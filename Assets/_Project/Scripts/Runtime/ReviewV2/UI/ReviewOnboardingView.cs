using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using XRApartment.Data.ReviewV2;

namespace XRApartment.Runtime.ReviewV2.UI
{
    public sealed class ReviewOnboardingView : MonoBehaviour
    {
        const string PanelId = "onboarding";

        [Serializable]
        public sealed class Page
        {
            public GameObject root;
            public Button previous;
            public Button next;
            public Button replay;
            public Button skip;
            public Button continueToExperience;
            public TMP_Text feedback;
        }

        [SerializeField] ReviewV2UiSessionAdapter session;
        [SerializeField] ReviewV2UiController uiController;
        [SerializeField] Page[] pages = Array.Empty<Page>();
        [SerializeField] Button teleportPracticeButton;
        [SerializeField] TMP_Text teleportPracticeHint;

        IReviewV2UiSession boundSession;
        IReviewV2UiSession subscribedSession;
        IReviewV2UiSession Session => boundSession ?? session;
        string displayedSessionId;
        bool showingOnboarding;
        bool submitting;
        bool awaitingPhase;
        int pageIndex;

        void OnEnable()
        {
            foreach (var page in pages)
            {
                if (page.previous != null) page.previous.onClick.AddListener(Previous);
                if (page.next != null) page.next.onClick.AddListener(Next);
                if (page.replay != null) page.replay.onClick.AddListener(Replay);
                if (page.skip != null) page.skip.onClick.AddListener(Skip);
                if (page.continueToExperience != null) page.continueToExperience.onClick.AddListener(Continue);
            }
            if (teleportPracticeButton != null) teleportPracticeButton.onClick.AddListener(ShowTeleportPractice);
            Connect();
        }

        void OnDisable()
        {
            foreach (var page in pages)
            {
                if (page.previous != null) page.previous.onClick.RemoveListener(Previous);
                if (page.next != null) page.next.onClick.RemoveListener(Next);
                if (page.replay != null) page.replay.onClick.RemoveListener(Replay);
                if (page.skip != null) page.skip.onClick.RemoveListener(Skip);
                if (page.continueToExperience != null) page.continueToExperience.onClick.RemoveListener(Continue);
            }
            if (teleportPracticeButton != null) teleportPracticeButton.onClick.RemoveListener(ShowTeleportPractice);
            Disconnect();
            Render(null);
        }

        public void BindSession(IReviewV2UiSession uiSession)
        {
            boundSession = uiSession;
            if (isActiveAndEnabled) Connect();
        }

        void Connect()
        {
            var source = Session;
            if (!ReferenceEquals(source, subscribedSession))
            {
                Disconnect();
                subscribedSession = source;
                if (subscribedSession != null) subscribedSession.Changed += Render;
            }
            Render(source?.Snapshot);
        }

        void Disconnect()
        {
            if (subscribedSession != null) subscribedSession.Changed -= Render;
            subscribedSession = null;
        }

        void Render(SessionSnapshot snapshot)
        {
            bool isOnboarding = snapshot?.Phase == SessionPhase.Onboarding;
            bool entered = isOnboarding && (!showingOnboarding || displayedSessionId != snapshot.SessionId);
            showingOnboarding = isOnboarding;
            displayedSessionId = snapshot?.SessionId;
            if (!isOnboarding || entered) awaitingPhase = false;
            if (entered) ResetPresentation();
            if (uiController != null) uiController.SetPanelVisible(PanelId, isOnboarding);
        }

        void Previous()
        {
            if (showingOnboarding && pageIndex > 0) ShowPage(pageIndex - 1);
        }

        void Next()
        {
            if (showingOnboarding && pageIndex + 1 < pages.Length) ShowPage(pageIndex + 1);
        }

        public void Replay()
        {
            if (showingOnboarding) ResetPresentation();
        }

        void ResetPresentation()
        {
            SetFeedback(string.Empty);
            KitchenLocale.Set(teleportPracticeHint, "Optional: try a teleport.");
            ShowPage(0);
        }

        void ShowPage(int index)
        {
            pageIndex = index;
            for (int i = 0; i < pages.Length; i++)
            {
                var page = pages[i];
                if (page.root != null) page.root.SetActive(i == index);
                if (page.previous != null) page.previous.interactable = i > 0;
            }
        }

        void ShowTeleportPractice()
        {
            if (showingOnboarding && teleportPracticeHint != null)
                KitchenLocale.Set(teleportPracticeHint, "Teleport not connected.\nPractice is optional.");
        }

        public void Skip() => Complete();

        public void Continue()
        {
            if (pages.Length > 0 && pageIndex == pages.Length - 1) Complete();
        }

        void Complete()
        {
            var source = Session;
            if (!isActiveAndEnabled || submitting || awaitingPhase ||
                source?.Snapshot?.Phase != SessionPhase.Onboarding) return;

            submitting = true;
            try
            {
                var result = source.Execute(CommandType.CompleteOnboarding);
                // The current core can publish Experience during Execute, before returning the result.
                var snapshot = source.Snapshot;
                Render(snapshot);
                awaitingPhase = result.Accepted && snapshot?.Phase == SessionPhase.Onboarding;
                SetFeedback(result.Accepted ? string.Empty : result.Reason);
            }
            finally
            {
                submitting = false;
            }
        }

        void SetFeedback(string message)
        {
            foreach (var page in pages)
                KitchenLocale.Set(page.feedback, message ?? string.Empty);
        }
    }
}
