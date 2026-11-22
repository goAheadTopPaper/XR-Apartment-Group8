using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using XRApartment.Data.ReviewV2;
using XRApartment.Runtime.ReviewV2.UI;

namespace XRApartment.Tests
{
    public sealed class ReviewHelpViewTests
    {
        GameObject root, panel, menu, taskBook;
        Button open, back;
        ReviewHelpView view;
        ReviewHelpView.Page[] pages;
        ReviewV2UiController controller;
        FakeSession session;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Help fixture");
            root.SetActive(false);
            panel = Child("Help", root.transform);
            menu = Child("Menu", root.transform);
            taskBook = Child("Task Book", root.transform);
            open = Child("Open", menu.transform).AddComponent<Button>();
            back = Child("Back", panel.transform).AddComponent<Button>();
            taskBook.SetActive(false);
            controller = root.AddComponent<ReviewV2UiController>();
            RegisterPanels(true);
            view = root.AddComponent<ReviewHelpView>();
            pages = new ReviewHelpView.Page[4];
            for (int i = 0; i < pages.Length; i++)
            {
                var page = Child("Page " + i, panel.transform);
                pages[i] = new ReviewHelpView.Page
                {
                    root = page,
                    previous = Child("Previous", page.transform).AddComponent<Button>(),
                    next = Child("Next", page.transform).AddComponent<Button>()
                };
            }

            SetField(view, "uiController", controller);
            SetField(view, "panel", panel);
            SetField(view, "backButton", back);
            SetField(view, "menuHelpButton", open);
            SetField(view, "pages", pages);
            session = new FakeSession { Snapshot = Snapshot() };
            view.BindSession(session);
            root.SetActive(true);
            Lifecycle(view, "OnEnable");
        }

        [TearDown]
        public void TearDown()
        {
            if (view != null && view.enabled) Lifecycle(view, "OnDisable");
            UnityEngine.Object.DestroyImmediate(root);
        }

        [Test]
        public void PrefabStartsClosedAndHasNoPreviewEvents()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/UI/PREF_UI_ReviewV2_Help.prefab");
            Assert.IsNotNull(prefab);
            Assert.IsTrue(prefab.activeSelf);
            Assert.IsFalse(prefab.transform.Find("HelpPanel").gameObject.activeSelf);
            Assert.IsNotNull(prefab.GetComponent<ReviewHelpView>());
            foreach (var button in prefab.GetComponentsInChildren<Button>(true))
                Assert.AreEqual(0, button.onClick.GetPersistentEventCount(), button.name);
        }

        [TestCase(SessionPhase.Setup)]
        [TestCase(SessionPhase.Onboarding)]
        [TestCase(SessionPhase.Review)]
        [TestCase(SessionPhase.Finished)]
        [TestCase(SessionPhase.Exited)]
        public void OtherPhasesCannotOpen(SessionPhase phase)
        {
            session.Publish(Snapshot(phase));
            Assert.IsFalse(view.TryOpen());
            Assert.IsFalse(view.IsOpen);
            Assert.IsTrue(menu.activeSelf);
        }

        [Test]
        public void EmptyOrUnidentifiedSnapshotCannotOpen()
        {
            session.Publish(null);
            Assert.IsFalse(view.TryOpen());
            session.Publish(Snapshot(id: ""));
            Assert.IsFalse(view.TryOpen());
        }

        [Test]
        public void ExperienceDoesNotShowHelpUntilRequested()
        {
            Assert.IsFalse(view.IsOpen);
            open.onClick.Invoke();
            Assert.IsTrue(view.IsOpen);
            Assert.IsFalse(menu.activeSelf);
            AssertPage(0);
        }

        [Test]
        public void AllFourPagesNavigateAndRespectBoundaries()
        {
            Assert.IsTrue(view.TryOpen());
            Assert.IsFalse(pages[0].previous.IsInteractable());
            pages[0].previous.onClick.Invoke();
            AssertPage(0);
            for (int i = 0; i < 3; i++)
            {
                pages[i].next.onClick.Invoke();
                AssertPage(i + 1);
            }
            Assert.IsFalse(pages[3].next.IsInteractable());
            pages[3].next.onClick.Invoke();
            AssertPage(3);
            for (int i = 3; i > 0; i--)
            {
                pages[i].previous.onClick.Invoke();
                AssertPage(i - 1);
            }
        }

        [Test]
        public void BackReturnsToMenuAndReopenResetsPage()
        {
            view.TryOpen();
            pages[0].next.onClick.Invoke();
            back.onClick.Invoke();
            Assert.IsFalse(view.IsOpen);
            Assert.IsTrue(menu.activeSelf);
            Assert.IsFalse(taskBook.activeSelf);
            open.onClick.Invoke();
            AssertPage(0);
        }

        [Test]
        public void CloseWithoutRegisteredMenuLeavesPanelsClosed()
        {
            RegisterPanels(false);
            menu.SetActive(false);
            Assert.IsTrue(view.TryOpen());
            view.Close();
            Assert.IsFalse(view.IsOpen);
            Assert.IsFalse(menu.activeSelf);
            Assert.IsFalse(taskBook.activeSelf);
        }

        [Test]
        public void PhaseChangeClosesWithoutReturningToOldMenu()
        {
            view.TryOpen();
            session.Publish(Snapshot(SessionPhase.Review));
            view.Close();
            Assert.IsFalse(view.IsOpen);
            Assert.IsFalse(menu.activeSelf);
            session.Publish(Snapshot());
            Assert.IsFalse(view.IsOpen);
        }

        [Test]
        public void NewSessionClosesEvenIfStillInExperience()
        {
            view.TryOpen();
            session.Publish(Snapshot(id: "another"));
            Assert.IsFalse(view.IsOpen);
            Assert.IsFalse(menu.activeSelf);
            Assert.IsTrue(view.TryOpen());
            AssertPage(0);
        }

        [Test]
        public void NullSnapshotClosesWithOrWithoutNotification()
        {
            view.TryOpen();
            session.Publish(null);
            Assert.IsFalse(view.IsOpen);
            session.Publish(Snapshot());
            view.TryOpen();
            session.Snapshot = null;
            Lifecycle(view, "Update");
            Assert.IsFalse(view.IsOpen);
            Assert.IsFalse(menu.activeSelf);
        }

        [Test]
        public void ConcreteAdapterSilentUnbindClosesHelp()
        {
            var adapter = Child("Adapter", root.transform).AddComponent<ReviewV2UiSessionAdapter>();
            adapter.BindMock();
            Assert.IsTrue(adapter.Execute(CommandType.CompleteSetup).Accepted);
            Assert.IsTrue(adapter.Execute(CommandType.CompleteOnboarding).Accepted);
            view.BindSession(adapter);
            Assert.IsTrue(view.TryOpen());
            adapter.Bind(null);
            Lifecycle(view, "Update");
            Assert.IsFalse(view.IsOpen);
            Assert.IsFalse(menu.activeSelf);
        }

        [Test]
        public void RebindingClosesAndMovesSubscriptionEvenWithSameSessionId()
        {
            view.TryOpen();
            view.BindSession(session);
            Assert.IsTrue(view.IsOpen);
            Assert.AreEqual(1, session.SubscriberCount);
            var replacement = new FakeSession { Snapshot = Snapshot() };
            view.BindSession(replacement);
            Assert.IsFalse(view.IsOpen);
            Assert.AreEqual(0, session.SubscriberCount);
            Assert.AreEqual(1, replacement.SubscriberCount);
            Assert.IsTrue(view.TryOpen());
            view.BindSession(null);
            Assert.IsFalse(view.IsOpen);
            Assert.AreEqual(0, replacement.SubscriberCount);
        }

        [Test]
        public void DisableRetiresListenersAndReenableDoesNotReopen()
        {
            view.TryOpen();
            Lifecycle(view, "OnDisable");
            view.enabled = false;
            Assert.AreEqual(0, session.SubscriberCount);
            Assert.IsFalse(panel.activeSelf);
            view.enabled = true;
            Lifecycle(view, "OnEnable");
            Assert.AreEqual(1, session.SubscriberCount);
            Assert.IsFalse(view.IsOpen);
            Assert.IsTrue(view.TryOpen());
            pages[0].next.onClick.Invoke();
            AssertPage(1);
            pages[1].next.onClick.Invoke();
            AssertPage(2);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void NavigationLeavesFullSnapshotUnchangedAndIssuesNoCommands(bool paused)
        {
            var original = Snapshot(paused: paused);
            session.Publish(original);
            for (int i = 0; i < 3; i++)
            {
                Assert.IsTrue(view.TryOpen());
                pages[0].next.onClick.Invoke();
                view.Close();
            }

            Assert.AreSame(original, session.Snapshot);
            Assert.AreEqual(SessionPhase.Experience, session.Snapshot.Phase);
            Assert.AreEqual("session", session.Snapshot.SessionId);
            Assert.AreEqual("visit", session.Snapshot.SchemeVisitId);
            Assert.AreEqual(GoalState.InProgress, session.Snapshot.Goals[0].State);
            Assert.AreEqual(paused, session.Snapshot.IsPaused);
            Assert.AreEqual(0, session.CommandCount);
        }

        [Test]
        public void ControllerReplacementIsMutuallyExclusiveAndCloseDoesNotStealIt()
        {
            controller.ShowPanel("task_book");
            view.TryOpen();
            Assert.IsFalse(taskBook.activeSelf);
            Assert.IsFalse(menu.activeSelf);
            pages[0].next.onClick.Invoke();
            controller.ShowPanel("task_book");
            Assert.IsFalse(view.IsOpen);
            view.Close();
            Assert.IsTrue(taskBook.activeSelf);
            Assert.IsFalse(menu.activeSelf);
            view.TryOpen();
            Assert.IsFalse(taskBook.activeSelf);
            AssertPage(0);
        }

        [Test]
        public void DuplicateOpenAndStaleButtonsDoNotAdvanceOrRestoreTwice()
        {
            open.onClick.Invoke();
            pages[0].next.onClick.Invoke();
            pages[0].next.onClick.Invoke();
            AssertPage(1);
            Assert.IsTrue(view.TryOpen());
            AssertPage(1);
            back.onClick.Invoke();
            controller.ShowPanel("task_book");
            back.onClick.Invoke();
            Assert.IsTrue(taskBook.activeSelf);
            Assert.AreEqual(0, session.CommandCount);
        }

        void AssertPage(int index)
        {
            for (int i = 0; i < pages.Length; i++)
                Assert.AreEqual(i == index, pages[i].root.activeSelf, "Page " + i);
        }

        void RegisterPanels(bool includeMenu)
        {
            var help = new ReviewV2UiController.PanelRegistration { id = "help", panel = panel };
            var book = new ReviewV2UiController.PanelRegistration { id = "task_book", panel = taskBook };
            var global = new ReviewV2UiController.PanelRegistration { id = "global_menu", panel = menu };
            SetField(controller, "panels", includeMenu ? new[] { help, book, global } : new[] { help, book });
            Lifecycle(controller, "Awake");
        }

        static SessionSnapshot Snapshot(SessionPhase phase = SessionPhase.Experience,
            string id = "session", bool paused = false) =>
            new SessionSnapshot(id, phase, SchemeIds.Baseline, "visit", "model",
                new[] { new GoalSnapshot("G02", GoalState.InProgress, true) },
                paused, false, true, SaveStatus.NotConfigured);

        static GameObject Child(string name, Transform parent)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent);
            return child;
        }

        static void SetField(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        static void Lifecycle(object target, string callback) =>
            target.GetType().GetMethod(callback, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);

        sealed class FakeSession : IReviewV2UiSession
        {
            public SessionSnapshot Snapshot { get; set; }
            public event Action<SessionSnapshot> Changed;
            public event Action<OperationResult> OperationCompleted { add { } remove { } }
            public int SubscriberCount => Changed?.GetInvocationList().Length ?? 0;
            public int CommandCount { get; private set; }

            public CommandResult Execute(CommandType command)
            {
                CommandCount++;
                return new CommandResult(false, "Help must never issue a command");
            }

            public void Publish(SessionSnapshot snapshot)
            {
                Snapshot = snapshot;
                Changed?.Invoke(snapshot);
            }
        }
    }
}