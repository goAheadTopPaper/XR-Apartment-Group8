using System;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XRApartment.Data.ReviewV2;
using XRApartment.Runtime.ReviewV2.UI;
using static XRApartment.Runtime.ReviewV2.UI.ReviewNotificationView;

namespace XRApartment.Tests
{
    public sealed class ReviewNotificationViewTests
    {
        GameObject root, toast, status, important, actions, confirmation;
        TMP_Text toastText, title, severity;
        Button dismiss, optional, cancel;
        EventSystem eventSystem;
        ReviewNotificationView view;
        FakeSession session;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Notifications fixture");
            root.SetActive(false);
            eventSystem = New("EventSystem", root.transform).AddComponent<EventSystem>();
            toast = New("Toast", root.transform);
            status = New("Paused", root.transform);
            important = New("Important", root.transform);
            actions = New("Actions", important.transform);
            confirmation = New("Confirmation", root.transform);
            confirmation.SetActive(false);
            dismiss = New("Dismiss", actions.transform).AddComponent<Button>();
            optional = New("Optional", actions.transform).AddComponent<Button>();
            cancel = New("Cancel", confirmation.transform).AddComponent<Button>();
            toastText = toast.AddComponent<TextMeshProUGUI>();
            title = New("Title", important.transform).AddComponent<TextMeshProUGUI>();
            severity = New("Severity", important.transform).AddComponent<TextMeshProUGUI>();
            view = root.AddComponent<ReviewNotificationView>();
            Field(view, "persistentStatus", status);
            Field(view, "toastContainer", toast);
            Field(view, "importantPanel", important);
            Field(view, "actionArea", actions);
            Field(view, "toastText", toastText);
            Field(view, "importantTitle", title);
            Field(view, "severityText", severity);
            Field(view, "dismissButton", dismiss);
            Field(view, "optionalActionButton", optional);
            Field(view, "confirmationPanel", confirmation);
            root.SetActive(true);
            Call(eventSystem, "OnEnable");
            EventSystem.current = eventSystem;
            Call(view, "OnEnable");
            session = new FakeSession { Snapshot = Snapshot() };
        }

        [TearDown]
        public void TearDown()
        {
            if (view != null && view.enabled) Call(view, "OnDisable");
            if (eventSystem != null) Call(eventSystem, "OnDisable");
            UnityEngine.Object.DestroyImmediate(root);
        }

        [Test]
        public void AuthoredPrefabStartsHiddenAndHasOnlyDismissRaycastTarget()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/UI/PREF_UI_ReviewV2_Notifications.prefab");
            Assert.IsNotNull(prefab.GetComponent<ReviewNotificationView>());
            Assert.IsTrue(prefab.activeSelf);
            Assert.IsFalse(prefab.transform.Find("ToastContainer").gameObject.activeSelf);
            Assert.IsFalse(prefab.transform.Find("PersistentStatus").gameObject.activeSelf);
            Assert.IsFalse(prefab.transform.Find("ImportantMessagePanel").gameObject.activeSelf);
            Assert.IsFalse(prefab.transform.Find("ImportantMessagePanel/ActionArea").gameObject.activeSelf);
            foreach (var graphic in prefab.GetComponentsInChildren<Graphic>(true))
                if (graphic.raycastTarget) Assert.AreEqual("DismissButton", graphic.name);
            foreach (var button in prefab.GetComponentsInChildren<Button>(true))
                Assert.Zero(button.onClick.GetPersistentEventCount());
        }

        [Test]
        public void NoSessionOrUnspecifiedSourceCannotShowMessages()
        {
            Assert.IsFalse(view.ShowToast(Id("1"), "No session"));
            AssertHidden();
            view.BindSession(session);
            Assert.IsFalse(view.ShowToast(Id("none", MessageSource.Unspecified), "No source"));
            Assert.IsFalse(view.ShowToast(new MessageIdentity("old", MessageSource.Device), "Old request"));
        }

        [Test]
        public void PauseUsesSnapshotAndDoesNotIssueCommands()
        {
            view.BindSession(session);
            Assert.IsFalse(status.activeSelf);
            session.Publish(Snapshot(paused: true));
            Assert.IsTrue(status.activeSelf);
            session.Publish(Snapshot());
            Assert.IsFalse(status.activeSelf);
            Assert.Zero(session.CommandCount);
            Assert.Zero(session.OperationSubscriptions);
        }

        [TestCase(SessionPhase.Finished)]
        [TestCase(SessionPhase.Exited)]
        [TestCase(SessionPhase.Review)]
        public void InactivePhaseClearsEverythingAndIgnoresOldMessages(SessionPhase phase)
        {
            view.BindSession(session);
            session.Publish(Snapshot(paused: true));
            view.ShowToast(Id("1"), "Active");
            view.ShowImportantMessage(Id("important"), "Information", "Details");
            view.ShowToast(Id("2"), "Queued");
            session.Publish(Snapshot(phase, paused: true));
            AssertHidden();
            Assert.IsFalse(view.ShowToast(Id("3"), "Late"));
        }

        [Test]
        public void NewSessionClearsQueueHistoryAndRefreshesPause()
        {
            view.BindSession(session);
            view.ShowToast(Id("1"), "Old active");
            view.ShowToast(Id("2"), "Old queued");
            view.ShowImportantMessage(Id("important"), "Old", "Details");
            session.Publish(Snapshot(id: "replacement", paused: true));
            Assert.IsTrue(status.activeSelf);
            Assert.IsFalse(important.activeSelf || toast.activeSelf);
            Assert.Zero(view.QueuedToastCount);
            Assert.IsFalse(view.ShowToast(Id("1"), "Late old"));
            Assert.IsTrue(view.ShowToast(new MessageIdentity("replacement", MessageSource.Device, "1"), "New"));
            Assert.AreEqual("New", toastText.text);
        }

        [Test]
        public void SilentAdapterUnbindClearsViewAtNextRefresh()
        {
            var adapter = New("Adapter", root.transform).AddComponent<ReviewV2UiSessionAdapter>();
            adapter.BindMock();
            Assert.IsTrue(adapter.Execute(CommandType.CompleteSetup).Accepted);
            Assert.IsTrue(adapter.Execute(CommandType.CompleteOnboarding).Accepted);
            Assert.IsTrue(adapter.Execute(CommandType.Pause).Accepted);
            view.BindSession(adapter);
            Assert.IsTrue(status.activeSelf);
            adapter.Bind(null);
            Call(view, "Update");
            AssertHidden();
        }

        [Test]
        public void EnableDisableDoesNotRestoreMessagesOrDuplicateSubscription()
        {
            view.BindSession(session);
            view.BindSession(session);
            Assert.AreEqual(1, session.Subscribers);
            session.Publish(Snapshot(paused: true));
            view.ShowToast(Id("1"), "Old");
            Call(view, "OnDisable");
            view.enabled = false;
            Assert.Zero(session.Subscribers);
            AssertHidden();
            view.enabled = true;
            Call(view, "OnEnable");
            Assert.AreEqual(1, session.Subscribers);
            Assert.IsTrue(status.activeSelf);
            Assert.IsFalse(toast.activeSelf);
            session.Publish(null);
            AssertHidden();
        }

        [Test]
        public void ToastExpiresWithUnscaledTimeWhilePausedAndKeepsFocus()
        {
            view.BindSession(session);
            session.Publish(Snapshot(paused: true));
            confirmation.SetActive(true);
            eventSystem.SetSelectedGameObject(cancel.gameObject);
            Assert.IsTrue(view.ShowToast(Id("one"), "Request", Outcome.Accepted, 1f));
            Assert.AreEqual("Accepted: Request", toastText.text);
            float previousScale = Time.timeScale;
            try
            {
                Time.timeScale = 0;
                Advance(1.1f);
                Assert.IsFalse(toast.activeSelf);
                Assert.IsTrue(status.activeSelf);
                Assert.AreEqual(0, Time.timeScale);
            }
            finally { Time.timeScale = previousScale; }
            Assert.AreSame(cancel.gameObject, eventSystem.currentSelectedGameObject);
        }

        [Test]
        public void OutcomeLabelsDistinguishAllStates()
        {
            view.BindSession(session);
            foreach (Outcome state in Enum.GetValues(typeof(Outcome)))
            {
                Assert.IsTrue(view.ShowToast(Id("same-request"), "details", state));
                Assert.AreEqual(state == Outcome.Information ? "details" : state + ": details", toastText.text);
                Advance(9f);
            }
        }

        [Test]
        public void DurationsAreBoundedAndAnonymousTextDoesNotDeduplicate()
        {
            view.BindSession(session);
            Assert.IsTrue(view.ShowToast(Id(null), "Same", durationSeconds: -3f));
            Assert.IsTrue(view.ShowToast(Id(null), "Same", durationSeconds: float.PositiveInfinity));
            Advance(0.5f);
            Assert.AreEqual(1, view.QueuedToastCount);
            Advance(0.5f);
            Assert.Zero(view.QueuedToastCount);
            Advance(8.01f);
            Assert.IsFalse(toast.activeSelf);
        }

        [Test]
        public void StableIdentityDeduplicatesBySourceAndRequest()
        {
            view.BindSession(session);
            Assert.IsTrue(view.ShowToast(Id("one"), "Same"));
            Assert.IsFalse(view.ShowToast(Id("one"), "Edited text"));
            Assert.IsTrue(view.ShowToast(Id("one", MessageSource.Marker), "Same"));
            Assert.IsTrue(view.ShowToast(Id("two"), "Same"));
            Assert.AreEqual(2, view.QueuedToastCount);
        }

        [Test]
        public void QueueIsBoundedAndPriorityEvictsNewestLowPriority()
        {
            view.BindSession(session);
            view.ShowToast(Id("current"), "Current");
            for (int i = 0; i < ToastQueueCapacity; i++)
                Assert.IsTrue(view.ShowToast(Id("id-" + i), "Item " + i));
            Assert.IsFalse(view.ShowToast(Id("overflow"), "Dropped"));
            Assert.IsTrue(view.ShowToast(Id("priority"), "Priority", Outcome.Failed));
            Assert.AreEqual(ToastQueueCapacity, view.QueuedToastCount);
            Advance(9f);
            Assert.AreEqual("Failed: Priority", toastText.text);
            for (int i = 0; i < ToastQueueCapacity - 1; i++)
            {
                Advance(9f);
                Assert.AreEqual("Item " + i, toastText.text);
            }
            Advance(9f);
            Assert.IsFalse(toast.activeSelf);
        }

        [Test]
        public void ImportantMessageStaysAndResumesToastAfterExplicitDismiss()
        {
            view.BindSession(session);
            view.ShowToast(Id("toast"), "Toast");
            Assert.IsTrue(view.ShowImportantMessage(Id("error"), "Error", "Details", canDismiss: true));
            Assert.IsFalse(toast.activeSelf);
            Advance(20f);
            session.Publish(Snapshot(paused: true));
            Assert.IsTrue(important.activeSelf);
            Assert.IsFalse(optional.IsInteractable());
            dismiss.onClick.Invoke();
            Assert.IsTrue(important.activeSelf);
            eventSystem.SetSelectedGameObject(dismiss.gameObject);
            ExecuteEvents.Execute(dismiss.gameObject, new BaseEventData(eventSystem), ExecuteEvents.submitHandler);
            Assert.IsFalse(important.activeSelf);
            Assert.IsTrue(toast.activeSelf && status.activeSelf);
            Assert.IsNull(eventSystem.currentSelectedGameObject);
        }

        [TestCase("Reset")]
        [TestCase("Safe Exit")]
        public void ConfirmationKeepsFocusAndDisablesDismiss(string scenario)
        {
            view.BindSession(session);
            cancel.gameObject.name = scenario + " cancel";
            confirmation.SetActive(true);
            eventSystem.SetSelectedGameObject(cancel.gameObject);
            view.ShowImportantMessage(Id("important"), "Error", "Details", canDismiss: true);
            view.ShowToast(Id("toast"), "Information");
            Assert.IsFalse(actions.activeSelf);
            ExecuteEvents.Execute(dismiss.gameObject, new BaseEventData(eventSystem), ExecuteEvents.submitHandler);
            Assert.IsTrue(important.activeSelf);
            Assert.IsFalse(view.DismissImportantMessage());
            Assert.AreSame(cancel.gameObject, eventSystem.currentSelectedGameObject);
            view.ClearNotifications();
            Assert.AreSame(cancel.gameObject, eventSystem.currentSelectedGameObject);
        }

        [Test]
        public void SafetyPreviewIsExplicitAndImportantErrorCannotReplaceIt()
        {
            view.BindSession(session);
            Assert.IsFalse(view.ShowImportantMessage(Id("1"), "Alert", "Details", Severity.SafetyPreview));
            Assert.IsTrue(view.ShowImportantMessage(Id("1", MessageSource.SafetyPreview), "Alert", "Details", Severity.SafetyPreview));
            StringAssert.StartsWith("PREVIEW", title.text);
            StringAssert.Contains("SAFETY", severity.text);
            Assert.IsFalse(view.ShowImportantMessage(Id("2"), "Error", "Details", Severity.CriticalError));
            Assert.IsFalse(view.DismissImportantMessage());
            Assert.IsFalse(session.Snapshot.IsPaused);
            Assert.Zero(session.CommandCount);
        }

        void AssertHidden()
        {
            Assert.IsFalse(toast.activeSelf || status.activeSelf || important.activeSelf || actions.activeSelf);
            Assert.Zero(view.QueuedToastCount);
        }

        static MessageIdentity Id(string id, MessageSource source = MessageSource.Device) => new MessageIdentity("session", source, id);
        void Advance(float seconds) => Call(view, "AdvanceToast", seconds);

        static SessionSnapshot Snapshot(SessionPhase phase = SessionPhase.Experience, string id = "session", bool paused = false) =>
            new SessionSnapshot(id, phase, SchemeIds.Baseline, "visit", "model", Array.Empty<GoalSnapshot>(),
                paused, false, true, SaveStatus.NotConfigured);

        static GameObject New(string name, Transform parent)
        {
            var child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            return child;
        }

        static void Field(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

        static void Call(object target, string method, params object[] args) =>
            target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, args);

        sealed class FakeSession : IReviewV2UiSession
        {
            public SessionSnapshot Snapshot { get; set; }
            public event Action<SessionSnapshot> Changed;
            public int Subscribers => Changed?.GetInvocationList().Length ?? 0;
            public int OperationSubscriptions { get; private set; }
            public event Action<OperationResult> OperationCompleted { add { OperationSubscriptions++; } remove { OperationSubscriptions--; } }
            public int CommandCount { get; private set; }

            public CommandResult Execute(CommandType command)
            {
                CommandCount++;
                return new CommandResult(false, "Notifications must not issue commands.");
            }

            public void Publish(SessionSnapshot snapshot)
            {
                Snapshot = snapshot;
                Changed?.Invoke(snapshot);
            }
        }
    }
}
