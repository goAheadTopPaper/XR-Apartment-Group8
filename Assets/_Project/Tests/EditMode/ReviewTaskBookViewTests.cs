using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using XRApartment.Data.ReviewV2;
using XRApartment.Runtime.ReviewV2.UI;

namespace XRApartment.Tests
{
    public sealed class ReviewTaskBookViewTests
    {
        GameObject root;
        GameObject menu;
        GameObject book;
        ReviewTaskBookView view;
        ReviewTaskBookView.GoalCard[] cards;
        FakeSession session;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Task Book fixture");
            root.SetActive(false);
            menu = Child("Menu", root.transform);
            book = Child("Book", root.transform);
            book.SetActive(false);
            var controller = root.AddComponent<ReviewV2UiController>();
            SetField(controller, "panels", new[]
            {
                new ReviewV2UiController.PanelRegistration { id = "global_menu", panel = menu },
                new ReviewV2UiController.PanelRegistration { id = "task_book", panel = book }
            });
            view = root.AddComponent<ReviewTaskBookView>();
            SetField(view, "uiController", controller);
            cards = new ReviewTaskBookView.GoalCard[5];
            for (int i = 0; i < cards.Length; i++)
            {
                var id = "G0" + (i + 1);
                var card = Child(id, book.transform);
                cards[i] = new ReviewTaskBookView.GoalCard
                {
                    id = id,
                    title = Child("Title", card.transform).AddComponent<TextMeshProUGUI>(),
                    status = Child("Status", card.transform).AddComponent<TextMeshProUGUI>()
                };
            }
            SetField(view, "cards", cards);
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
        [TestCase(SessionPhase.Onboarding)]
        [TestCase(SessionPhase.Review)]
        [TestCase(SessionPhase.Finished)]
        [TestCase(SessionPhase.Exited)]
        public void NonExperiencePhaseCannotOpen(SessionPhase phase)
        {
            Enable(Snapshot(phase));
            Assert.IsFalse(view.CanOpen);
            Assert.IsFalse(view.TryOpenFromMenu());
            Assert.IsFalse(book.activeSelf);
            Assert.IsTrue(menu.activeSelf);
            AssertNoCommands(phase);
        }

        [Test]
        public void MissingSnapshotPreventsOpeningUntilExperienceArrives()
        {
            Enable(null);
            Assert.IsFalse(view.CanOpen);
            Assert.IsFalse(view.TryOpenFromMenu());
            Assert.IsFalse(book.activeSelf);
            session.Publish(Snapshot(SessionPhase.Experience, Goal("G02", GoalState.Completed)));
            Assert.IsTrue(view.CanOpen);
            Assert.IsFalse(book.activeSelf);
            Assert.AreEqual("Completed", Status(1));
        }

        [Test]
        public void ExistingExperienceIsRenderedImmediatelyButDoesNotOpenBook()
        {
            Enable(Snapshot(SessionPhase.Experience, Goal("G02", GoalState.Completed)));
            Assert.IsTrue(view.CanOpen);
            Assert.IsFalse(book.activeSelf);
            Assert.AreEqual("Completed", Status(1));
            Assert.AreEqual("Goal G01", cards[0].title.text);
            AssertNoCommands(SessionPhase.Experience);
        }

        [TestCase(false, GoalState.NotStarted, "Unavailable")]
        [TestCase(false, GoalState.InProgress, "Unavailable")]
        [TestCase(false, GoalState.Completed, "Unavailable")]
        [TestCase(true, GoalState.NotStarted, "Not Started")]
        [TestCase(true, GoalState.InProgress, "In Progress")]
        [TestCase(true, GoalState.Completed, "Completed")]
        public void MapsAvailabilityBeforeGoalState(bool available, GoalState state, string expected)
        {
            Enable(Snapshot(SessionPhase.Experience, new GoalSnapshot("G02", state, available)));
            Assert.AreEqual(expected, Status(1));
            AssertNoCommands(SessionPhase.Experience);
        }

        [Test]
        public void MatchesStableIdAcrossReorderedUnknownAndMissingGoals()
        {
            Enable(Snapshot(SessionPhase.Experience, Goal("G02", GoalState.Completed), Goal("G01", GoalState.InProgress)));
            Assert.AreEqual("In Progress", Status(0));
            Assert.AreEqual("Completed", Status(1));
            session.Publish(Snapshot(SessionPhase.Experience, Goal("G99", GoalState.Completed), Goal("G01", GoalState.InProgress)));
            Assert.AreEqual("In Progress", Status(0));
            Assert.AreEqual("Unavailable", Status(1));
            Assert.AreEqual("Unavailable", Status(4));
        }

        [Test]
        public void ParallelGoalsAndDirectCompletionDoNotChangeNavigationOrPhase()
        {
            var g1 = Goal("G01", GoalState.InProgress);
            var g3 = Goal("G03", GoalState.InProgress);
            Enable(Snapshot(SessionPhase.Experience, g1, Goal("G02", GoalState.NotStarted), g3));
            Assert.AreEqual("In Progress", Status(0));
            Assert.AreEqual("Not Started", Status(1));
            Assert.AreEqual("In Progress", Status(2));
            session.Publish(Snapshot(SessionPhase.Experience, g1, Goal("G02", GoalState.Completed), g3));
            Assert.AreEqual("Completed", Status(1));
            Assert.IsFalse(book.activeSelf);
            Assert.IsTrue(view.TryOpenFromMenu());
            session.Publish(Snapshot(SessionPhase.Experience, g1, Goal("G02", GoalState.Completed), Goal("G03", GoalState.Completed)));
            Assert.IsTrue(book.activeSelf);
            Assert.IsFalse(menu.activeSelf);
            AssertNoCommands(SessionPhase.Experience);
        }

        [Test]
        public void OpenAndCloseKeepPanelsMutuallyExclusive()
        {
            Enable(Snapshot(SessionPhase.Experience));
            Assert.IsTrue(view.TryOpenFromMenu());
            Assert.IsTrue(book.activeSelf);
            Assert.IsFalse(menu.activeSelf);
            view.Close();
            Assert.IsFalse(book.activeSelf);
            Assert.IsTrue(menu.activeSelf);
            AssertNoCommands(SessionPhase.Experience);
        }

        [Test]
        public void LeavingExperienceClosesBookWithoutReturningToMenuOrReopening()
        {
            Enable(Snapshot(SessionPhase.Experience));
            Assert.IsTrue(view.TryOpenFromMenu());
            session.Publish(Snapshot(SessionPhase.Review));
            Assert.IsFalse(book.activeSelf);
            Assert.IsFalse(menu.activeSelf);
            view.Close();
            Assert.IsFalse(menu.activeSelf);
            session.Publish(Snapshot(SessionPhase.Experience));
            Assert.IsFalse(book.activeSelf);
            Assert.IsFalse(menu.activeSelf);
            AssertNoCommands(SessionPhase.Experience);
        }

        [Test]
        public void InvalidSnapshotClosesBookAndKeepsMenuClosed()
        {
            Enable(Snapshot(SessionPhase.Experience));
            Assert.IsTrue(view.TryOpenFromMenu());
            session.Publish(null);
            view.Close();
            Assert.IsFalse(view.CanOpen);
            Assert.IsFalse(book.activeSelf);
            Assert.IsFalse(menu.activeSelf);
            Assert.IsEmpty(session.Commands);
        }

        [Test]
        public void ReopeningReadsCurrentSnapshotEvenWithoutAnEvent()
        {
            Enable(Snapshot(SessionPhase.Experience, Goal("G02", GoalState.NotStarted)));
            Assert.IsTrue(view.TryOpenFromMenu());
            view.Close();
            session.Publish(Snapshot(SessionPhase.Experience, Goal("G02", GoalState.InProgress)));
            Assert.IsFalse(book.activeSelf);
            Assert.AreEqual("In Progress", Status(1));
            session.SetSnapshot(Snapshot(SessionPhase.Experience, Goal("G02", GoalState.Completed)));
            Assert.IsTrue(view.TryOpenFromMenu());
            Assert.AreEqual("Completed", Status(1));
            AssertNoCommands(SessionPhase.Experience);
        }

        [Test]
        public void NewVisitClearsOldGoalStateAndDoesNotChangePage()
        {
            Enable(Snapshot(SessionPhase.Experience, Goal("G01", GoalState.Completed), Goal("G02", GoalState.Completed)));
            Assert.IsTrue(view.TryOpenFromMenu());
            session.Publish(new SessionSnapshot("session", SessionPhase.Experience, SchemeIds.Baseline, "visit-2",
                "model", new[] { Goal("G02", GoalState.NotStarted) }, false, false, false, SaveStatus.NotConfigured));
            Assert.AreEqual("Unavailable", Status(0));
            Assert.AreEqual("Not Started", Status(1));
            Assert.IsTrue(book.activeSelf);
            AssertNoCommands(SessionPhase.Experience);
        }

        [Test]
        public void DisableDetachesAndReenableSubscribesOnceWithLatestSnapshot()
        {
            Enable(Snapshot(SessionPhase.Experience));
            Assert.AreEqual(1, session.ChangedSubscribers);
            view.BindSession(session);
            Assert.AreEqual(1, session.ChangedSubscribers);
            Disable();
            Assert.AreEqual(0, session.ChangedSubscribers);
            session.Publish(Snapshot(SessionPhase.Experience, Goal("G02", GoalState.Completed)));
            Assert.AreEqual("Unavailable", Status(1));
            root.SetActive(true);
            Lifecycle(view, "OnEnable");
            Assert.AreEqual(1, session.ChangedSubscribers);
            Assert.AreEqual("Completed", Status(1));
            Assert.IsFalse(book.activeSelf);
            AssertNoCommands(SessionPhase.Experience);
        }

        [Test]
        public void PrefabStartsClosedAndHasFiveUniqueConfiguredCards()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/PREF_UI_ReviewV2_TaskBook.prefab");
            Assert.IsNotNull(prefab);
            var panel = prefab.transform.Find("TaskBookPanel");
            Assert.IsFalse(panel.gameObject.activeSelf);
            var settings = new SerializedObject(prefab.GetComponent<ReviewTaskBookView>()).FindProperty("cards");
            Assert.AreEqual(5, settings.arraySize);
            var ids = new List<string>();
            for (int i = 0; i < settings.arraySize; i++)
            {
                var card = settings.GetArrayElementAtIndex(i);
                ids.Add(card.FindPropertyRelative("id").stringValue);
                Assert.IsNotNull(card.FindPropertyRelative("title").objectReferenceValue);
                Assert.IsNotNull(card.FindPropertyRelative("status").objectReferenceValue);
                Assert.IsNotNull(card.FindPropertyRelative("accent").objectReferenceValue);
                Assert.IsNotNull(card.FindPropertyRelative("statusBackground").objectReferenceValue);
            }
            CollectionAssert.AreEquivalent(new[] { "G01", "G02", "G03", "G04", "G05" }, ids);
        }

        void Enable(SessionSnapshot initial)
        {
            session.SetSnapshot(initial);
            root.SetActive(true);
            // EditMode does not dispatch runtime MonoBehaviour callbacks.
            Lifecycle(view, "OnEnable");
        }

        void Disable()
        {
            root.SetActive(false);
            Lifecycle(view, "OnDisable");
        }

        string Status(int index) => cards[index].status.text;

        void AssertNoCommands(SessionPhase expected)
        {
            Assert.AreEqual(0, session.ExecuteCount);
            Assert.IsEmpty(session.Commands);
            Assert.AreEqual(expected, session.CurrentPhase);
        }

        static SessionSnapshot Snapshot(SessionPhase phase, params GoalSnapshot[] goals) =>
            new SessionSnapshot("session", phase, SchemeIds.Baseline, "visit-1", "model", goals,
                false, false, false, SaveStatus.NotConfigured);

        static GoalSnapshot Goal(string id, GoalState state) => new GoalSnapshot(id, state, true);

        static GameObject Child(string name, Transform parent)
        {
            var child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            return child;
        }

        static void SetField(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        static void Lifecycle(MonoBehaviour target, string name) =>
            target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);

        public sealed class FakeSession : IReviewV2UiSession
        {
            public SessionSnapshot Snapshot { get; private set; }
            public SessionPhase? CurrentPhase => Snapshot?.Phase;
            public List<CommandType> Commands { get; } = new List<CommandType>();
            public int ExecuteCount => Commands.Count;
            public int ChangedSubscribers => Changed?.GetInvocationList().Length ?? 0;
            public event Action<SessionSnapshot> Changed;
            public event Action<OperationResult> OperationCompleted { add { } remove { } }

            public CommandResult Execute(CommandType command)
            {
                Commands.Add(command);
                return new CommandResult(false, "Unexpected Task Book command");
            }

            public void SetSnapshot(SessionSnapshot snapshot) => Snapshot = snapshot;

            public void Publish(SessionSnapshot snapshot)
            {
                SetSnapshot(snapshot);
                Changed?.Invoke(snapshot);
            }
        }
    }
}
