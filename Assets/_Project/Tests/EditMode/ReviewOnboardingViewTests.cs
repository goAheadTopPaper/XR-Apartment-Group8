using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using XRApartment.Data.ReviewV2;
using XRApartment.Runtime.ReviewV2.UI;

namespace XRApartment.Tests
{
    public sealed class ReviewOnboardingViewTests
    {
        GameObject root, panel, taskBook;
        ReviewOnboardingView view;
        ReviewOnboardingView.Page[] pages;
        FakeSession session;
        Button practice;
        TMP_Text hint;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Onboarding fixture");
            root.SetActive(false);
            panel = Child("Onboarding", root.transform);
            panel.SetActive(false);
            taskBook = Child("Task Book sentinel", root.transform);
            taskBook.SetActive(false);
            var controller = root.AddComponent<ReviewV2UiController>();
            SetField(controller, "panels", new[]
            {
                new ReviewV2UiController.PanelRegistration { id = "onboarding", panel = panel },
                new ReviewV2UiController.PanelRegistration { id = "task_book", panel = taskBook }
            });
            view = root.AddComponent<ReviewOnboardingView>();
            SetField(view, "uiController", controller);
            pages = new ReviewOnboardingView.Page[4];
            for (var i = 0; i < pages.Length; i++)
            {
                var page = Child("Page " + i, panel.transform);
                pages[i] = new ReviewOnboardingView.Page
                {
                    root = page,
                    previous = Child("Previous", page.transform).AddComponent<Button>(),
                    next = i < 3 ? Child("Next", page.transform).AddComponent<Button>() : null,
                    replay = Child("Replay", page.transform).AddComponent<Button>(),
                    skip = Child("Skip", page.transform).AddComponent<Button>(),
                    continueToExperience = i == 3 ? Child("Continue", page.transform).AddComponent<Button>() : null,
                    feedback = Child("Feedback", page.transform).AddComponent<TextMeshProUGUI>()
                };
                page.SetActive(i == 0);
            }
            practice = Child("Practice", pages[0].root.transform).AddComponent<Button>();
            hint = Child("Practice hint", pages[0].root.transform).AddComponent<TextMeshProUGUI>();
            SetField(view, "pages", pages);
            SetField(view, "teleportPracticeButton", practice);
            SetField(view, "teleportPracticeHint", hint);
            session = new FakeSession();
            view.BindSession(session);
            Lifecycle(controller, "Awake");
        }

        [TearDown]
        public void TearDown()
        {
            if (root == null) return;
            if (root.activeSelf) Disable();
            UnityEngine.Object.DestroyImmediate(root);
        }

        [TestCase(SessionPhase.Setup)]
        [TestCase(SessionPhase.Experience)]
        [TestCase(SessionPhase.Review)]
        [TestCase(SessionPhase.Finished)]
        [TestCase(SessionPhase.Exited)]
        public void OtherPhasesStayHiddenAndCannotSubmit(SessionPhase phase)
        {
            Enable(Snapshot(phase));
            Assert.IsFalse(panel.activeSelf);
            view.Skip();
            view.Continue();
            Assert.IsEmpty(session.Commands);
        }

        [Test]
        public void NoSnapshotIsHiddenUntilOnboardingArrives()
        {
            Enable(null);
            Assert.IsFalse(panel.activeSelf);
            session.Publish(Snapshot(SessionPhase.Setup));
            Assert.IsFalse(panel.activeSelf);
            session.Publish(Snapshot(SessionPhase.Onboarding));
            Assert.IsTrue(panel.activeSelf);
            AssertPage(0);
            Assert.IsEmpty(session.Commands);
            session.Publish(null);
            Assert.IsFalse(panel.activeSelf);
        }

        [Test]
        public void ExistingOnboardingAppearsOnEnable()
        {
            Enable(Snapshot(SessionPhase.Onboarding));
            Assert.IsTrue(panel.activeSelf);
            AssertPage(0);
            Assert.IsFalse(pages[0].previous.interactable);
        }

        [Test]
        public void PaginationAndReplayOnlyChangePresentation()
        {
            var snapshot = Snapshot(SessionPhase.Onboarding);
            Enable(snapshot);
            practice.onClick.Invoke();
            StringAssert.Contains("not connected", hint.text);
            pages[0].next.onClick.Invoke();
            AssertPage(1);
            pages[1].previous.onClick.Invoke();
            AssertPage(0);
            pages[0].next.onClick.Invoke();
            pages[1].next.onClick.Invoke();
            AssertPage(2);
            pages[2].replay.onClick.Invoke();
            AssertPage(0);
            Assert.AreEqual("Optional: try a teleport.", hint.text);
            Assert.AreSame(snapshot, session.Snapshot);
            Assert.IsEmpty(session.Commands);
        }

        [Test]
        public void SamePhaseGoalUpdatePreservesCurrentPage()
        {
            Enable(Snapshot(SessionPhase.Onboarding));
            pages[0].next.onClick.Invoke();
            pages[1].next.onClick.Invoke();
            var updated = Snapshot(SessionPhase.Onboarding, GoalState.Completed);
            session.Publish(updated);
            AssertPage(2);
            Assert.AreSame(updated.Goals[0], session.Snapshot.Goals[0]);
            Assert.IsEmpty(session.Commands);
        }

        [Test]
        public void SkipOnFirstPageNeedsNoPracticeAndPublishesExperienceSynchronously()
        {
            Enable(Snapshot(SessionPhase.Onboarding));
            Assert.IsTrue(pages[0].skip.IsInteractable());
            pages[0].skip.onClick.Invoke();
            AssertCompletion();
        }

        [Test]
        public void ContinueIsOnlyOnLastPageAndSubmitsOnce()
        {
            Enable(Snapshot(SessionPhase.Onboarding));
            view.Continue();
            Assert.IsEmpty(session.Commands);
            pages[0].next.onClick.Invoke();
            pages[1].next.onClick.Invoke();
            pages[2].next.onClick.Invoke();
            AssertPage(3);
            pages[3].continueToExperience.onClick.Invoke();
            AssertCompletion();
            view.Continue();
            Assert.AreEqual(1, session.Commands.Count);
        }

        [TestCase("InvalidPhase")]
        [TestCase("Paused")]
        [TestCase("OperationPending")]
        [TestCase("SessionEnded")]
        [TestCase("Provider failure")]
        public void RejectionShowsActualReasonAndKeepsOnboardingOpen(string reason)
        {
            Enable(Snapshot(SessionPhase.Onboarding));
            session.Reply = new CommandResult(false, reason);
            pages[0].skip.onClick.Invoke();
            Assert.AreEqual(reason, pages[0].feedback.text);
            Assert.IsTrue(panel.activeSelf);
            AssertPage(0);
            CollectionAssert.AreEqual(new[] { CommandType.CompleteOnboarding }, session.Commands);
        }

        [Test]
        public void AcceptedWithoutPhaseUpdateWaitsAndPreventsDuplicateSubmission()
        {
            Enable(Snapshot(SessionPhase.Onboarding));
            session.PublishExperienceDuringExecute = false;
            view.Skip();
            view.Skip();
            Assert.IsTrue(panel.activeSelf);
            CollectionAssert.AreEqual(new[] { CommandType.CompleteOnboarding }, session.Commands);
            session.Publish(Snapshot(SessionPhase.Experience));
            Assert.IsFalse(panel.activeSelf);
        }

        [Test]
        public void DisableAndReenableDoesNotDuplicateSubscriptionOrButtonListeners()
        {
            Enable(Snapshot(SessionPhase.Onboarding));
            Assert.AreEqual(1, session.Subscribers);
            view.BindSession(session);
            Assert.AreEqual(1, session.Subscribers);
            Disable();
            Assert.AreEqual(0, session.Subscribers);
            Assert.IsFalse(panel.activeSelf);
            session.Publish(Snapshot(SessionPhase.Onboarding));
            Assert.IsFalse(panel.activeSelf);
            root.SetActive(true);
            Lifecycle(view, "OnEnable");
            Assert.AreEqual(1, session.Subscribers);
            pages[0].next.onClick.Invoke();
            AssertPage(1);
            pages[1].skip.onClick.Invoke();
            AssertCompletion();
        }

        [Test]
        public void AuthoredPrefabIsClosedAndHasNoStaticButtonBypass()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/PREF_UI_ReviewV2_Onboarding.prefab");
            Assert.IsNotNull(prefab);
            var prefabPanel = prefab.transform.Find("OnboardingPanel");
            Assert.IsFalse(prefabPanel.gameObject.activeSelf);
            Assert.AreEqual(4, prefabPanel.Find("Pages").childCount);
            Assert.IsNotNull(prefab.GetComponent<ReviewOnboardingView>());
            foreach (var button in prefabPanel.GetComponentsInChildren<Button>(true))
                Assert.AreEqual(0, button.onClick.GetPersistentEventCount(), button.name);
        }

        void Enable(SessionSnapshot initial)
        {
            session.Publish(initial);
            root.SetActive(true);
            Lifecycle(view, "OnEnable");
        }

        void Disable()
        {
            root.SetActive(false);
            Lifecycle(view, "OnDisable");
        }

        void AssertPage(int active)
        {
            for (var i = 0; i < pages.Length; i++) Assert.AreEqual(i == active, pages[i].root.activeSelf);
        }

        void AssertCompletion()
        {
            CollectionAssert.AreEqual(new[] { CommandType.CompleteOnboarding }, session.Commands);
            Assert.AreEqual(SessionPhase.Experience, session.Snapshot.Phase);
            Assert.AreEqual("visit", session.Snapshot.SchemeVisitId);
            Assert.AreEqual(GoalState.NotStarted, session.Snapshot.Goals[0].State);
            Assert.IsFalse(panel.activeSelf);
            Assert.IsFalse(taskBook.activeSelf);
        }

        static SessionSnapshot Snapshot(SessionPhase phase, GoalState state = GoalState.NotStarted) =>
            new SessionSnapshot("session", phase, SchemeIds.Baseline, "visit", "model",
                new[] { new GoalSnapshot("G02", state, true) }, false, false, true, SaveStatus.NotConfigured);

        static GameObject Child(string name, Transform parent)
        {
            var child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            return child;
        }

        static void SetField(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        // Ordinary MonoBehaviours do not run their runtime callbacks in EditMode.
        static void Lifecycle(MonoBehaviour target, string name) =>
            target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);

        sealed class FakeSession : IReviewV2UiSession
        {
            public SessionSnapshot Snapshot { get; private set; }
            public List<CommandType> Commands { get; } = new List<CommandType>();
            public int Subscribers => Changed?.GetInvocationList().Length ?? 0;
            public CommandResult Reply = new CommandResult(true);
            public bool PublishExperienceDuringExecute = true;
            public event Action<SessionSnapshot> Changed;
            public event Action<OperationResult> OperationCompleted { add { } remove { } }

            public void Publish(SessionSnapshot snapshot)
            {
                Snapshot = snapshot;
                Changed?.Invoke(snapshot);
            }

            public CommandResult Execute(CommandType command)
            {
                Commands.Add(command);
                if (Reply.Accepted && PublishExperienceDuringExecute)
                    Publish(new SessionSnapshot(Snapshot.SessionId, SessionPhase.Experience, Snapshot.SchemeId,
                        Snapshot.SchemeVisitId, Snapshot.ModelVersion, Snapshot.Goals,
                        false, false, Snapshot.IsMock, Snapshot.SaveStatus));
                return Reply;
            }
        }
    }
}
