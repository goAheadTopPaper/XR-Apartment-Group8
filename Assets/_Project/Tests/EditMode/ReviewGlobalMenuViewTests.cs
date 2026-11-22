using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XRApartment.Data.ReviewV2;
using XRApartment.Runtime.ReviewV2.UI;

namespace XRApartment.Tests
{
    public sealed class ReviewGlobalMenuViewTests
    {
        GameObject root, panel, book, help, modal;
        CanvasGroup body;
        ReviewV2UiController controller;
        ReviewGlobalMenuView view;
        ReviewTaskBookView taskBook;
        TMP_Text scheme, status, pauseLabel, feedback;
        Button reset, confirm, cancel, exit, review, pause, recenter, schemeButton, taskBookButton, helpButton, close, opener;
        EventSystem fixtureEvents;
        FakeSession session;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Global Menu fixture");
            root.SetActive(false);
            fixtureEvents = Child("EventSystem", root.transform).AddComponent<EventSystem>();
            panel = Child("Menu", root.transform);
            book = Child("Task Book", root.transform);
            help = Child("Help", root.transform);
            modal = Child("Modal", panel.transform);
            body = Child("Body", panel.transform).AddComponent<CanvasGroup>();
            opener = MakeButton(root.transform, "Opener");
            reset = MakeButton(body.transform, "Reset");
            exit = MakeButton(body.transform, "Exit");
            review = MakeButton(body.transform, "Review");
            pause = MakeButton(body.transform, "Pause");
            recenter = MakeButton(body.transform, "Recenter");
            schemeButton = MakeButton(body.transform, "Scheme");
            taskBookButton = MakeButton(body.transform, "Task Book");
            helpButton = MakeButton(body.transform, "Help");
            close = MakeButton(body.transform, "Close");
            confirm = MakeButton(modal.transform, "Confirm");
            cancel = MakeButton(modal.transform, "Cancel");
            scheme = MakeLabel("Scheme text");
            status = MakeLabel("Status text");
            pauseLabel = MakeLabel("Pause text");
            feedback = MakeLabel("Feedback");
            controller = root.AddComponent<ReviewV2UiController>();
            Field(controller, "panels", new[]
            {
                new ReviewV2UiController.PanelRegistration { id = "global_menu", panel = panel },
                new ReviewV2UiController.PanelRegistration { id = "task_book", panel = book },
                new ReviewV2UiController.PanelRegistration { id = "help", panel = help }
            });
            Call(controller, "Awake");
            session = new FakeSession { Snapshot = Snapshot() };
            taskBook = root.AddComponent<ReviewTaskBookView>();
            Field(taskBook, "uiController", controller);
            view = root.AddComponent<ReviewGlobalMenuView>();
            Field(view, "panel", panel);
            Field(view, "taskBookPanel", book);
            Field(view, "helpPanel", help);
            Field(view, "confirmationPanel", modal);
            Field(view, "uiController", controller);
            Field(view, "taskBook", taskBook);
            Field(view, "menuBody", body);
            Field(view, "resetWorldButton", reset);
            Field(view, "safeExitButton", exit);
            Field(view, "reviewButton", review);
            Field(view, "recenterButton", recenter);
            Field(view, "pauseResumeButton", pause);
            Field(view, "switchSchemeButton", schemeButton);
            Field(view, "taskBookButton", taskBookButton);
            Field(view, "helpButton", helpButton);
            Field(view, "closeButton", close);
            Field(view, "menuOpener", opener);
            Field(view, "confirmButton", confirm);
            Field(view, "cancelButton", cancel);
            Field(view, "currentSchemeLabel", scheme);
            Field(view, "sessionStatusLabel", status);
            Field(view, "pauseResumeLabel", pauseLabel);
            Field(view, "feedbackText", feedback);
            view.BindSession(session);
            root.SetActive(true);
            Call(fixtureEvents, "OnEnable");
            EventSystem.current = fixtureEvents;
            taskBook.BindSession(session);
            Call(view, "OnEnable");
        }

        [TearDown]
        public void TearDown()
        {
            if (view != null && view.enabled) Call(view, "OnDisable");
            if (taskBook != null) taskBook.BindSession(null);
            if (fixtureEvents != null) Call(fixtureEvents, "OnDisable");
            UnityEngine.Object.DestroyImmediate(root);
        }

        [Test]
        public void PrefabStartsHiddenAndHasOnlyRuntimeBindings()
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/UI/PREF_UI_ReviewV2_GlobalMenu.prefab");
            Assert.IsNotNull(asset.GetComponent<ReviewGlobalMenuView>());
            Assert.IsTrue(asset.activeSelf);
            var menu = asset.transform.Find("GlobalMenuPanel");
            Assert.IsFalse(menu.gameObject.activeSelf);
            Assert.IsFalse(menu.Find("ConfirmationPanel").gameObject.activeSelf);
            foreach (var b in asset.GetComponentsInChildren<Button>(true))
                Assert.Zero(b.onClick.GetPersistentEventCount());
        }

        [TestCase(SessionPhase.Setup)]
        [TestCase(SessionPhase.Onboarding)]
        [TestCase(SessionPhase.Review)]
        [TestCase(SessionPhase.Finished)]
        [TestCase(SessionPhase.Exited)]
        public void NonExperiencePhasesCannotOpen(SessionPhase phase)
        {
            session.Publish(Snapshot(phase));
            Assert.IsFalse(view.TryOpen());
            Assert.IsFalse(view.IsOpen);
        }

        [Test]
        public void InvalidSnapshotClearsSchemeAndExperienceDoesNotAutoOpen()
        {
            Open();
            session.Publish(null);
            Assert.IsFalse(view.IsOpen);
            Assert.AreEqual("Current scheme: —", scheme.text);
            Assert.IsFalse(view.TryOpen());
            session.Publish(Snapshot(id: ""));
            Assert.IsFalse(view.TryOpen());
            session.Publish(Snapshot());
            Assert.IsFalse(view.IsOpen);
            Open();
        }

        [Test]
        public void HeaderReadsSchemePauseAndBusyFromSnapshot()
        {
            Open();
            session.Publish(Snapshot(paused: true, busy: true, schemeId: "MOCK_B"));
            Assert.AreEqual("Current scheme: MOCK_B", scheme.text);
            StringAssert.Contains("Paused · Busy", status.text);
            Assert.AreEqual("Resume", pauseLabel.text);
            Assert.IsFalse(pause.IsInteractable());
            Assert.IsTrue(exit.IsInteractable());
            Assert.IsFalse(schemeButton.IsInteractable());
            schemeButton.onClick.Invoke();
            Assert.IsEmpty(session.Commands);
            Assert.IsEmpty(feedback.text);
        }

        [Test]
        public void TaskBookAndHelpAreExclusiveAndReturnFocusIsVisible()
        {
            Open();
            AssertFocus(taskBookButton);
            taskBookButton.onClick.Invoke();
            Assert.IsTrue(book.activeSelf);
            Assert.IsFalse(view.IsOpen);
            taskBook.Close();
            Call(view, "Update");
            AssertFocus(taskBookButton);
            controller.ShowPanel("help");
            Assert.IsTrue(help.activeSelf);
            Assert.IsFalse(view.IsOpen);
            Assert.IsFalse(book.activeSelf);
            Call(view, "Update");
            controller.ShowPanel("global_menu");
            EventSystem.current.SetSelectedGameObject(helpButton.gameObject);
            Call(view, "Update");
            AssertFocus(helpButton);
            close.onClick.Invoke();
            Assert.IsFalse(book.activeSelf || help.activeSelf || view.IsOpen);
            AssertFocus(opener);
        }

        [Test]
        public void OrdinaryNavigationLeavesSnapshotAndGoalsUntouched()
        {
            var original = session.Snapshot;
            Open();
            taskBookButton.onClick.Invoke();
            taskBook.Close();
            view.Close();
            Assert.AreSame(original, session.Snapshot);
            Assert.AreEqual("visit", session.Snapshot.SchemeVisitId);
            Assert.AreEqual(GoalState.NotStarted, session.Snapshot.Goals[0].State);
            Assert.IsEmpty(session.Commands);
        }

        [Test]
        public void ExternalHideDismissesConfirmationAndClearsItsFocus()
        {
            AskReset();
            controller.ShowPanel("help");
            Assert.IsFalse(view.IsOpen);
            Call(view, "Update");
            Assert.IsFalse(modal.activeSelf);
            Assert.IsNull(EventSystem.current.currentSelectedGameObject);
            confirm.onClick.Invoke();
            Assert.IsEmpty(session.Commands);
        }

        [Test]
        public void NewSessionClosesEntireGroupAndRetiresFeedback()
        {
            Open();
            recenter.onClick.Invoke();
            controller.ShowPanel("help");
            session.Publish(Snapshot(id: "new", schemeId: "MOCK_B"));
            Assert.IsFalse(help.activeSelf || book.activeSelf || view.IsOpen);
            Assert.IsEmpty(feedback.text);
            Assert.AreEqual("Current scheme: MOCK_B", scheme.text);
        }

        [Test]
        public void ConcreteAdapterSilentUnbindClosesEntireGroup()
        {
            var adapter = Child("Adapter", root.transform).AddComponent<ReviewV2UiSessionAdapter>();
            adapter.BindMock();
            Assert.IsTrue(adapter.Execute(CommandType.CompleteSetup).Accepted);
            Assert.IsTrue(adapter.Execute(CommandType.CompleteOnboarding).Accepted);
            view.BindSession(adapter);
            Open();
            controller.ShowPanel("help");
            adapter.Bind(null);
            Call(view, "Update");
            Assert.IsFalse(view.IsOpen || help.activeSelf);
        }

        [Test]
        public void EnableDisableAndBindingDoNotDuplicateListenersOrReopen()
        {
            Open();
            int before = session.ChangeSubscribers;
            Assert.AreEqual(1, session.OperationSubscribers);
            view.BindSession(session);
            Assert.AreEqual(before, session.ChangeSubscribers);
            Call(view, "OnDisable");
            view.enabled = false;
            Assert.AreEqual(before - 1, session.ChangeSubscribers);
            Assert.Zero(session.OperationSubscribers);
            view.enabled = true;
            Call(view, "OnEnable");
            Assert.AreEqual(before, session.ChangeSubscribers);
            Assert.AreEqual(1, session.OperationSubscribers);
            Assert.IsFalse(view.IsOpen);
            Open();
            recenter.onClick.Invoke();
            CollectionAssert.AreEqual(new[] { CommandType.Recenter }, session.Commands);
        }

        [Test]
        public void ResetRequiresConfirmationAndCancelRestoresFocus()
        {
            AskReset();
            Assert.IsFalse(body.interactable || body.blocksRaycasts);
            Assert.IsFalse(taskBookButton.IsInteractable());
            AssertFocus(cancel);
            Assert.IsEmpty(session.Commands);
            cancel.onClick.Invoke();
            Assert.IsFalse(modal.activeSelf);
            Assert.IsTrue(body.interactable && body.blocksRaycasts);
            AssertFocus(reset);
            Assert.IsEmpty(session.Commands);
        }

        [Test]
        public void ResetRechecksBusyBeforeConfirm()
        {
            AskReset();
            session.Publish(Snapshot(busy: true));
            confirm.onClick.Invoke();
            Assert.IsEmpty(session.Commands);
            StringAssert.Contains("OperationPending", feedback.text);
        }

        [Test]
        public void ResetConfirmSubmitsOnceAndShowsReasonOnRejection()
        {
            session.Handler = _ => new CommandResult(false, "WorldUnavailable");
            AskReset();
            confirm.onClick.Invoke();
            confirm.onClick.Invoke();
            CollectionAssert.AreEqual(new[] { CommandType.ResetWorld }, session.Commands);
            Assert.AreEqual("Rejected: WorldUnavailable", feedback.text);
        }

        [TestCase(true, "Succeeded: restored")]
        [TestCase(false, "Failed: restored")]
        public void ResetUsesOnlyMatchingAsyncReceipt(bool succeeded, string expected)
        {
            BeginReset();
            StringAssert.StartsWith("Pending:", feedback.text);
            Assert.IsFalse(reset.IsInteractable());
            AssertFocus(taskBookButton);
            session.Complete(new OperationResult("old", CommandType.ResetWorld, true, "stale"));
            session.Complete(new OperationResult("request", CommandType.Finish, true, "wrong command"));
            StringAssert.StartsWith("Pending:", feedback.text);
            session.Complete(new OperationResult("request", CommandType.ResetWorld, succeeded, "restored"));
            Assert.AreEqual(expected, feedback.text);
            Assert.IsTrue(reset.IsInteractable());
        }

        [Test]
        public void ResetMatchingSynchronousReceiptWinsAfterExecuteReturns()
        {
            session.Handler = _ =>
            {
                session.Complete(new OperationResult("request", CommandType.ResetWorld, true, "restored"));
                session.Complete(new OperationResult("old", CommandType.ResetWorld, false, "stale"));
                return new CommandResult(true, requestId: "request");
            };
            AskReset();
            confirm.onClick.Invoke();
            Assert.AreEqual("Succeeded: restored", feedback.text);
        }

        [Test]
        public void SnapshotChangeAloneDoesNotDeclareResetSuccessAndTimeoutIsUnconfirmed()
        {
            BeginReset();
            session.Publish(Snapshot(visit: "new-visit"));
            StringAssert.StartsWith("Pending:", feedback.text);
            Field(view, "receiptDeadline", -1f);
            Call(view, "Update");
            StringAssert.StartsWith("Unconfirmed:", feedback.text);
            session.Complete(new OperationResult("request", CommandType.ResetWorld, true, "late"));
            StringAssert.StartsWith("Unconfirmed:", feedback.text);
        }

        [Test]
        public void OldReceiptCannotPolluteReplacementSession()
        {
            BeginReset();
            var replacement = new FakeSession { Snapshot = Snapshot(id: "next") };
            view.BindSession(replacement);
            Assert.IsFalse(view.IsOpen);
            session.Complete(new OperationResult("request", CommandType.ResetWorld, true, "old"));
            replacement.Complete(new OperationResult("request", CommandType.ResetWorld, true, "wrong source"));
            Assert.IsEmpty(feedback.text);
        }

        [Test]
        public void ClosedMenuRemainsClosedWhenReceiptArrives()
        {
            BeginReset();
            view.Close();
            session.Complete(new OperationResult("request", CommandType.ResetWorld, true, "late"));
            Assert.IsFalse(view.IsOpen || book.activeSelf || help.activeSelf);
        }

        [Test]
        public void PauseAndResumeUseCommandsAndOnlySnapshotChangesLabel()
        {
            session.Handler = _ => new CommandResult(true);
            Open();
            pause.onClick.Invoke();
            Assert.AreEqual("Pause", pauseLabel.text);
            pause.onClick.Invoke();
            CollectionAssert.AreEqual(new[] { CommandType.Pause }, session.Commands);
            session.Publish(Snapshot(paused: true));
            Assert.AreEqual("Resume", pauseLabel.text);
            session.Handler = _ => { session.Publish(Snapshot()); return new CommandResult(true); };
            pause.onClick.Invoke();
            CollectionAssert.AreEqual(new[] { CommandType.Pause, CommandType.Resume }, session.Commands);
            Assert.IsFalse(view.IsOpen);
        }

        [Test]
        public void ReviewAtZeroCompletedGoalsClosesTheGroup()
        {
            session.Handler = _ => { session.Publish(Snapshot(SessionPhase.Review)); return new CommandResult(true); };
            Open();
            review.onClick.Invoke();
            CollectionAssert.AreEqual(new[] { CommandType.Review }, session.Commands);
            Assert.IsFalse(view.IsOpen || book.activeSelf || help.activeSelf);
        }

        [Test]
        public void RecenterDisplaysUnsupportedReasonWithoutChangingSnapshot()
        {
            var original = session.Snapshot;
            session.Handler = _ => new CommandResult(false, "MockHasNoXR");
            Open();
            recenter.onClick.Invoke();
            CollectionAssert.AreEqual(new[] { CommandType.Recenter }, session.Commands);
            Assert.AreEqual("Unsupported: MockHasNoXR", feedback.text);
            Assert.AreSame(original, session.Snapshot);
        }

        [Test]
        public void BusySafeExitCanCancelThenConfirmAndIgnoresLateReset()
        {
            BeginReset();
            session.Publish(Snapshot(busy: true));
            Assert.IsTrue(exit.IsInteractable());
            exit.onClick.Invoke();
            AssertFocus(cancel);
            cancel.onClick.Invoke();
            AssertFocus(exit);
            CollectionAssert.AreEqual(new[] { CommandType.ResetWorld }, session.Commands);
            session.Handler = _ => { session.Publish(Snapshot(SessionPhase.Exited)); return new CommandResult(true); };
            exit.onClick.Invoke();
            confirm.onClick.Invoke();
            CollectionAssert.AreEqual(new[] { CommandType.ResetWorld, CommandType.SafeExit }, session.Commands);
            session.Complete(new OperationResult("request", CommandType.ResetWorld, true, "late"));
            Assert.IsFalse(view.IsOpen || help.activeSelf || book.activeSelf);
            Assert.IsEmpty(feedback.text);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void UnavailableDraftProtectionBlocksOpeningConfirmation(bool resetAction)
        {
            var holder = Child("Unavailable T3 bridge", root.transform);
            holder.SetActive(false);
            var bridge = holder.AddComponent<XRApartment.Runtime.ReviewV2.ReviewIntegrationPanel>();
            Field(view, "reviewIntegration", bridge);
            Open();
            (resetAction ? reset : exit).onClick.Invoke();
            Assert.IsFalse(modal.activeSelf);
            Assert.IsEmpty(session.Commands);
            StringAssert.Contains("protection is unavailable", feedback.text);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void DraftProtectionIsRecheckedAtFinalConfirm(bool resetAction)
        {
            Open();
            (resetAction ? reset : exit).onClick.Invoke();
            Assert.IsTrue(modal.activeSelf);
            var holder = Child("Late unavailable T3 bridge", root.transform);
            holder.SetActive(false);
            Field(view, "reviewIntegration", holder.AddComponent<XRApartment.Runtime.ReviewV2.ReviewIntegrationPanel>());
            confirm.onClick.Invoke();
            Assert.IsFalse(modal.activeSelf);
            Assert.IsEmpty(session.Commands);
        }

        void Open() => Assert.IsTrue(view.TryOpen());

        void AskReset()
        {
            Open();
            reset.onClick.Invoke();
            Assert.IsTrue(modal.activeSelf);
        }

        void BeginReset()
        {
            session.Handler = _ => new CommandResult(true, requestId: "request");
            AskReset();
            confirm.onClick.Invoke();
        }

        static void AssertFocus(Button button) =>
            Assert.AreSame(button.gameObject, EventSystem.current.currentSelectedGameObject);

        TMP_Text MakeLabel(string name) => Child(name, body.transform).AddComponent<TextMeshProUGUI>();
        static Button MakeButton(Transform parent, string name) => Child(name, parent).AddComponent<Button>();

        static GameObject Child(string name, Transform parent)
        {
            var result = new GameObject(name, typeof(RectTransform));
            result.transform.SetParent(parent);
            return result;
        }

        static SessionSnapshot Snapshot(SessionPhase phase = SessionPhase.Experience, string id = "session",
            bool paused = false, bool busy = false, string schemeId = "MOCK_A", string visit = "visit") =>
            new SessionSnapshot(id, phase, schemeId, visit, "model",
                new[] { new GoalSnapshot("G02", GoalState.NotStarted, true) },
                paused, busy, true, SaveStatus.NotConfigured);

        static void Field(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        static void Call(object target, string method) =>
            target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);

        sealed class FakeSession : IReviewV2UiSession
        {
            public SessionSnapshot Snapshot { get; set; }
            public event Action<SessionSnapshot> Changed;
            public event Action<OperationResult> OperationCompleted;
            public int ChangeSubscribers => Changed?.GetInvocationList().Length ?? 0;
            public int OperationSubscribers => OperationCompleted?.GetInvocationList().Length ?? 0;
            public List<CommandType> Commands { get; } = new List<CommandType>();
            public Func<CommandType, CommandResult> Handler { get; set; }

            public CommandResult Execute(CommandType command)
            {
                Commands.Add(command);
                return Handler?.Invoke(command) ?? new CommandResult(false, "Unsupported");
            }

            public void Publish(SessionSnapshot snapshot)
            {
                Snapshot = snapshot;
                Changed?.Invoke(snapshot);
            }

            public void Complete(OperationResult result) => OperationCompleted?.Invoke(result);
        }
    }
}
