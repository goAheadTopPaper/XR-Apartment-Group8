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
using XRApartment.Runtime.ReviewV2;
using XRApartment.Runtime.ReviewV2.UI;
using XRApartment.Simulation.ReviewV2;

namespace XRApartment.Tests
{
    public sealed class ReviewSetupViewTests
    {
        GameObject root;
        GameObject setupPanel;
        ReviewSetupView view;
        Button seated, standing, begin, safeExit, close;
        Toggle safety;
        TMP_Text feedback;
        FakeUiSession fake;
        ReviewV2UiSessionAdapter adapter;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("ReviewSetupViewTestRoot");
            root.SetActive(false);
            var controller = root.AddComponent<ReviewV2UiController>();
            setupPanel = new GameObject("SetupPanel");
            setupPanel.transform.SetParent(root.transform, false);
            SetField(controller, "panels", new[] { new ReviewV2UiController.PanelRegistration { id = "setup", panel = setupPanel } });

            seated = CreateButton("Seated");
            standing = CreateButton("Standing");
            begin = CreateButton("Begin");
            safeExit = CreateButton("SafeExit");
            close = CreateButton("Close");
            safety = new GameObject("Safety").AddComponent<Toggle>();
            safety.transform.SetParent(root.transform, false);
            safety.SetIsOnWithoutNotify(false);
            feedback = CreateText("Feedback");
            var selected = CreateText("Selected");

            view = root.AddComponent<ReviewSetupView>();
            SetField(view, "uiController", controller);
            SetField(view, "seatedButton", seated);
            SetField(view, "standingButton", standing);
            SetField(view, "beginButton", begin);
            SetField(view, "safeExitButton", safeExit);
            SetField(view, "closeButton", close);
            SetField(view, "safetyAcknowledgement", safety);
            SetField(view, "feedbackText", feedback);
            SetField(view, "selectedPostureText", selected);

            // Ordinary MonoBehaviours do not receive runtime lifecycle callbacks in this EditMode fixture.
            InvokeLifecycle(controller, "Awake");
        }

        void StartWithFake()
        {
            fake = new FakeUiSession();
            view.BindSession(fake);
            root.SetActive(true);
            InvokeLifecycle(view, "OnEnable");
        }

        [TearDown]
        public void TearDown()
        {
            if (root == null) return;
            if (root.activeSelf) DisableView();
            if (adapter != null) InvokeLifecycle(adapter, "OnDisable");
            UnityEngine.Object.DestroyImmediate(root);
        }

        [Test]
        public void SetupPhase_ShowsSetupPanel()
        {
            StartWithFake();
            Assert.AreEqual(0, fake.CommandCallCount);
            Assert.IsNull(fake.LastCommand);
            CollectionAssert.IsEmpty(fake.CommandsReceived);
            Assert.IsTrue(setupPanel.activeSelf);
            Assert.IsTrue(seated.interactable);
            Assert.IsTrue(standing.interactable);
        }

        [TestCase(false, CommandType.CalibrateStanding)]
        [TestCase(true, CommandType.CalibrateSeated)]
        public void PostureChoice_UsesMatchingCalibrationCommand(bool chooseSeated, CommandType expected)
        {
            StartWithFake();
            (chooseSeated ? seated : standing).onClick.Invoke();
            Assert.AreEqual(1, fake.CommandCallCount);
            Assert.AreEqual(expected, fake.LastCommand);
            CollectionAssert.AreEqual(new[] { expected }, fake.CommandsReceived);
            StringAssert.Contains("view selected", feedback.text);
        }

        [Test]
        public void BeginAfterSafetyAndPosture_UsesCompleteSetupCommand()
        {
            StartWithFake();
            standing.onClick.Invoke();
            Assert.AreEqual(1, fake.CommandCallCount);
            Assert.IsFalse(begin.interactable);
            safety.isOn = true;
            Assert.IsTrue(begin.interactable);
            begin.onClick.Invoke();
            Assert.AreEqual(2, fake.CommandCallCount);
            Assert.AreEqual(CommandType.CompleteSetup, fake.LastCommand);
            CollectionAssert.AreEqual(new[] { CommandType.CalibrateStanding, CommandType.CompleteSetup }, fake.CommandsReceived);
            Assert.AreEqual(SessionPhase.Onboarding, fake.Snapshot.Phase);
            Assert.IsFalse(setupPanel.activeSelf);
        }

        [Test]
        public void RejectedSetupCommand_ShowsReasonAndKeepsSetupOpen()
        {
            StartWithFake();
            standing.onClick.Invoke();
            safety.isOn = true;
            fake.RejectNext = "TestRejected";
            begin.onClick.Invoke();
            Assert.AreEqual(2, fake.CommandCallCount);
            Assert.AreEqual(CommandType.CompleteSetup, fake.LastCommand);
            CollectionAssert.AreEqual(new[] { CommandType.CalibrateStanding, CommandType.CompleteSetup }, fake.CommandsReceived);
            Assert.IsNull(fake.RejectNext, "The fake must consume the configured rejection.");
            Assert.AreEqual(SessionPhase.Setup, fake.Snapshot.Phase);
            Assert.IsTrue(setupPanel.activeSelf);
            Assert.IsFalse(string.IsNullOrEmpty(feedback.text), "The view must write rejection feedback.");
            StringAssert.Contains("TestRejected", feedback.text);
        }

        [Test]
        public void OperationCompleted_ShowsAsyncResult()
        {
            StartWithFake();
            fake.RaiseOperation(new OperationResult("r1", CommandType.ResetWorld, false, "Mock failure"));
            StringAssert.Contains("Mock failure", feedback.text);
        }

        [Test]
        public void SafeExit_UsesExistingSafeExitCommand()
        {
            StartWithFake();
            safeExit.onClick.Invoke();
            Assert.AreEqual(1, fake.CommandCallCount);
            Assert.AreEqual(CommandType.SafeExit, fake.LastCommand);
            CollectionAssert.AreEqual(new[] { CommandType.SafeExit }, fake.CommandsReceived);
            Assert.AreEqual(SessionPhase.Exited, fake.Snapshot.Phase);
            Assert.IsFalse(setupPanel.activeSelf);
        }

        [Test]
        public void SetupPanel_CanCloseAndReopen()
        {
            StartWithFake();
            var controller = root.GetComponent<ReviewV2UiController>();
            Assert.IsTrue(setupPanel.activeInHierarchy);
            close.onClick.Invoke();
            Assert.IsFalse(setupPanel.activeSelf);
            Assert.IsTrue(view.isActiveAndEnabled);
            Assert.AreEqual(1, fake.ChangedSubscribers);
            Assert.AreEqual(1, fake.OperationSubscribers);
            Assert.IsTrue(controller.ShowPanel("setup"));
            Assert.IsTrue(setupPanel.activeInHierarchy);
            close.onClick.Invoke();
            Assert.IsFalse(setupPanel.activeSelf);
            view.OpenSetup();
            Assert.IsTrue(setupPanel.activeInHierarchy);
            Assert.AreEqual(1, fake.ChangedSubscribers);
            Assert.AreEqual(1, fake.OperationSubscribers);
        }

        [Test]
        public void SessionEvents_AreSubscribedAndUnsubscribedWithView()
        {
            StartWithFake();
            Assert.AreEqual(1, fake.ChangedSubscribers);
            Assert.AreEqual(1, fake.OperationSubscribers);
            var previousFeedback = feedback.text;
            DisableView();
            Assert.AreEqual(0, fake.ChangedSubscribers);
            Assert.AreEqual(0, fake.OperationSubscribers);
            var result = new OperationResult("r1", CommandType.ResetWorld, false, "Lifecycle feedback");
            fake.RaiseOperation(result);
            Assert.AreEqual(previousFeedback, feedback.text);
            root.SetActive(true);
            InvokeLifecycle(view, "OnEnable");
            Assert.AreEqual(1, fake.ChangedSubscribers);
            Assert.AreEqual(1, fake.OperationSubscribers);
            fake.RaiseOperation(result);
            StringAssert.Contains("Lifecycle feedback", feedback.text);
            standing.onClick.Invoke();
            Assert.AreEqual(1, fake.CommandCallCount, "Re-enabling must register the button listener exactly once.");
        }

        [Test]
        public void MockAdapter_IdentifiesPostureSelectionAsMockOnly()
        {
            StartWithFake();
            var adapterObject = new GameObject("MockAdapter");
            try
            {
                var adapter = adapterObject.AddComponent<ReviewV2UiSessionAdapter>();
                adapter.BindMock();
                view.BindSession(adapter);
                standing.onClick.Invoke();
                Assert.IsTrue(adapter.Snapshot.IsMock);
                Assert.IsFalse(string.IsNullOrEmpty(feedback.text), "The view must write mock posture feedback.");
                StringAssert.Contains("Mock only", feedback.text);
                StringAssert.Contains("no hardware calibration was performed", feedback.text);
            }
            finally
            {
                view.BindSession(fake);
                UnityEngine.Object.DestroyImmediate(adapterObject);
            }
        }

        [Test]
        public void AdapterFirst_ViewReadsCurrentSnapshotWhenEnabled()
        {
            AddSerializedMockAdapter();
            root.SetActive(true);
            InvokeLifecycle(adapter, "OnEnable");
            Assert.AreEqual(SessionPhase.Setup, adapter.Snapshot.Phase);

            InvokeLifecycle(view, "OnEnable");

            AssertSetupReady();
        }

        [Test]
        public void ViewFirst_AdapterPublishesInitialSnapshotToExistingSubscriber()
        {
            AddSerializedMockAdapter();
            root.SetActive(true);
            InvokeLifecycle(view, "OnEnable");
            Assert.IsNull(adapter.Snapshot);
            Assert.IsFalse((bool)GetField(view, "phaseInitialized"));
            Assert.IsFalse(seated.IsInteractable());
            Assert.IsFalse(standing.IsInteractable());

            var snapshots = new List<SessionSnapshot>();
            adapter.Changed += snapshots.Add;
            InvokeLifecycle(adapter, "OnEnable");

            Assert.AreEqual(1, snapshots.Count);
            Assert.AreEqual(adapter.Snapshot.SessionId, snapshots[0].SessionId);
            Assert.AreEqual(SessionPhase.Setup, snapshots[0].Phase);
            AssertSetupReady();
        }

        [Test]
        public void RepeatedBinding_ReusesMockAndDeliversOneTransition()
        {
            AddSerializedMockAdapter();
            root.SetActive(true);
            InvokeLifecycle(adapter, "OnEnable");
            InvokeLifecycle(view, "OnEnable");
            var originalMock = GetField(adapter, "mock");
            var snapshots = new List<SessionSnapshot>();
            adapter.Changed += snapshots.Add;

            view.BindSession(adapter);
            view.BindSession(adapter);
            adapter.BindMock();
            adapter.BindMock();

            Assert.AreSame(originalMock, GetField(adapter, "mock"));
            Assert.IsEmpty(snapshots);
            Assert.AreEqual(2, ((Delegate)GetField(adapter, "Changed")).GetInvocationList().Length);
            Assert.AreEqual(1, ((Delegate)GetField(adapter, "OperationCompleted")).GetInvocationList().Length);
            seated.onClick.Invoke();
            safety.isOn = true;
            begin.onClick.Invoke();
            Assert.AreEqual(1, snapshots.Count);
            Assert.AreEqual(SessionPhase.Onboarding, snapshots[0].Phase);
            Assert.IsFalse(string.IsNullOrEmpty(feedback.text));
            StringAssert.Contains("Setup accepted", feedback.text);

            InvokeLifecycle(adapter, "OnDisable");
            ((MockReviewProvider)originalMock).Execute(CommandType.CompleteOnboarding);
            Assert.AreEqual(SessionPhase.Experience, adapter.Snapshot.Phase);
            Assert.AreEqual(1, snapshots.Count, "Disabled adapters must not forward provider updates.");
        }

        [Test]
        public void BindHost_PublishesRealSnapshotAndForwardsCommand()
        {
            AddSerializedMockAdapter();
            var hostObject = new GameObject("Review Session Host");
            hostObject.transform.SetParent(root.transform, false);
            var host = hostObject.AddComponent<ReviewSessionHost>();
            root.SetActive(true);
            InvokeLifecycle(host, "Awake");
            try
            {
                InvokeLifecycle(adapter, "OnEnable");
                InvokeLifecycle(view, "OnEnable");
                var previousMock = (MockReviewProvider)GetField(adapter, "mock");
                var snapshots = new List<SessionSnapshot>();
                adapter.Changed += snapshots.Add;

                adapter.Bind(host);
                adapter.Bind(host);
                Assert.IsNull(GetField(adapter, "mock"));
                Assert.IsFalse(adapter.Snapshot.IsMock);
                Assert.AreEqual(host.Snapshot.SessionId, adapter.Snapshot.SessionId);
                Assert.AreEqual(1, snapshots.Count);
                Assert.IsFalse(snapshots[0].IsMock);
                previousMock.Execute(CommandType.CompleteSetup);
                Assert.AreEqual(1, snapshots.Count, "The previous mock must be detached.");

                Assert.IsTrue(adapter.Execute(CommandType.CompleteSetup).Accepted);
                Assert.AreEqual(SessionPhase.Onboarding, host.Snapshot.Phase);
                Assert.AreEqual(2, snapshots.Count);
                Assert.IsFalse(setupPanel.activeSelf);
            }
            finally
            {
                InvokeLifecycle(adapter, "OnDisable");
                InvokeLifecycle(host, "OnDestroy");
            }
        }

        [Test]
        public void SerializedMockStartup_FirstSeatedSubmitWorksWithoutRebinding()
        {
            AddSerializedMockAdapter();
            root.SetActive(true);
            InvokeLifecycle(view, "OnEnable");
            InvokeLifecycle(adapter, "OnEnable");

            AssertSetupReady();
            var eventData = new BaseEventData(EventSystem.current);
            Assert.IsTrue(ExecuteEvents.Execute(seated.gameObject, eventData, ExecuteEvents.submitHandler));
            Assert.IsTrue(adapter.Snapshot.IsMock);
            Assert.IsFalse(string.IsNullOrEmpty(feedback.text));
            StringAssert.Contains("Seated view selected", feedback.text);
            StringAssert.Contains("no hardware calibration was performed", feedback.text);
        }

        void AddSerializedMockAdapter()
        {
            adapter = root.AddComponent<ReviewV2UiSessionAdapter>();
            SetField(adapter, "useMockMode", true);
            SetField(view, "session", adapter);
        }

        void AssertSetupReady()
        {
            Assert.IsTrue((bool)GetField(view, "phaseInitialized"));
            Assert.IsTrue(setupPanel.activeInHierarchy);
            Assert.IsTrue(seated.IsInteractable());
            Assert.IsTrue(standing.IsInteractable());
            Assert.IsTrue(safety.IsInteractable());
            Assert.IsFalse(begin.IsInteractable());
        }

        static object GetField(object target, string field)
        {
            var info = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(info, "Missing test field " + field);
            return info.GetValue(target);
        }

        void DisableView()
        {
            root.SetActive(false);
            InvokeLifecycle(view, "OnDisable");
        }

        static void InvokeLifecycle(MonoBehaviour component, string callback)
        {
            var method = component.GetType().GetMethod(callback, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "Missing lifecycle callback " + callback);
            method.Invoke(component, null);
        }

        Button CreateButton(string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(root.transform, false);
            return go.GetComponent<Button>();
        }

        TMP_Text CreateText(string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(root.transform, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/Art/UI/FNT_ReviewV2Integration.asset");
            return text;
        }

        static void SetField(object target, string field, object value)
        {
            var info = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(info, "Missing serialized test field " + field);
            info.SetValue(target, value);
        }

        sealed class FakeUiSession : IReviewV2UiSession
        {
            readonly MockReviewProvider mock = new MockReviewProvider();
            Action<SessionSnapshot> changed;
            Action<OperationResult> operation;
            public int ChangedSubscribers => changed?.GetInvocationList().Length ?? 0;
            public int OperationSubscribers => operation?.GetInvocationList().Length ?? 0;
            public string RejectNext;
            readonly List<CommandType> commandsReceived = new List<CommandType>();
            public int CommandCallCount => commandsReceived.Count;
            public CommandType? LastCommand => commandsReceived.Count == 0 ? (CommandType?)null : commandsReceived[commandsReceived.Count - 1];
            public IReadOnlyList<CommandType> CommandsReceived => commandsReceived;
            public SessionSnapshot Snapshot => mock.Snapshot;
            public event Action<SessionSnapshot> Changed
            {
                add { changed += value; mock.Changed += value; }
                remove { changed -= value; mock.Changed -= value; }
            }
            public event Action<OperationResult> OperationCompleted
            {
                add { operation += value; }
                remove { operation -= value; }
            }
            public CommandResult Execute(CommandType command)
            {
                commandsReceived.Add(command);
                if (!string.IsNullOrEmpty(RejectNext))
                {
                    var reason = RejectNext;
                    RejectNext = null;
                    return new CommandResult(false, reason);
                }
                if (command == CommandType.CalibrateSeated || command == CommandType.CalibrateStanding)
                    return new CommandResult(true);
                return mock.Execute(command);
            }
            public void RaiseOperation(OperationResult result) => operation?.Invoke(result);
        }
    }
}
