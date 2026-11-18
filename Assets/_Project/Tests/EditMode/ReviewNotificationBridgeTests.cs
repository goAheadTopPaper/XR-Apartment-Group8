using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using XRApartment.Data.ReviewV2;
using XRApartment.Runtime.ReviewV2.UI;

namespace XRApartment.Tests
{
    public class ReviewNotificationBridgeTests
    {
        GameObject root;
        ReviewNotificationBridge bridge;
        [SetUp] public void SetUp()
        {
            root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/UI/PREF_UI_ReviewV2_Notifications.prefab"));
            root.SetActive(false);
            var view = root.GetComponent<ReviewNotificationView>();
            view.BindSession(new Session());
            bridge = root.AddComponent<ReviewNotificationBridge>();
            Field("view", view);
            root.SetActive(true);
        }
        [TearDown] public void TearDown() => UnityEngine.Object.DestroyImmediate(root);
        void Field(string name, object value) => typeof(ReviewNotificationBridge)
            .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(bridge, value);
        void Approve(ReviewNotificationView.MessageSource source) => Field("approvedSources", new[] { source });
        [Test] public void DefaultConfigurationDoesNotBroadcastAnyMessage()
        {
            Assert.False(bridge.TryShowToast(new ReviewNotificationView.MessageIdentity("test", ReviewNotificationView.MessageSource.SessionStatus, "event"), "Status"));
        }
        [Test] public void ApprovedMessagesStillRequireReliableIdentity()
        {
            Approve(ReviewNotificationView.MessageSource.SessionStatus);
            Assert.False(bridge.TryShowToast(new ReviewNotificationView.MessageIdentity("test", ReviewNotificationView.MessageSource.SessionStatus), "Status"));
        }
        [Test] public void ExplicitMessageRetainsViewSessionAndDeduplicationRules()
        {
            Approve(ReviewNotificationView.MessageSource.SessionStatus);
            var id = new ReviewNotificationView.MessageIdentity("test", ReviewNotificationView.MessageSource.SessionStatus, "event");
            Assert.True(bridge.TryShowToast(id, "Status"));
            Assert.False(bridge.TryShowToast(id, "Status"));
            Assert.False(bridge.TryShowToast(new ReviewNotificationView.MessageIdentity("other", id.Source, "event"), "Status"));
        }
        [TestCase(ReviewNotificationView.MessageSource.SafetyPreview)]
        [TestCase(ReviewNotificationView.MessageSource.TestPreview)]
        public void PreviewCannotBecomeAProductionBridgeMessage(ReviewNotificationView.MessageSource source)
        {
            Approve(source);
            Assert.False(bridge.TryShowToast(new ReviewNotificationView.MessageIdentity("test", source, "event"), "Preview"));
        }
        sealed class Session : IReviewV2UiSession
        {
            public SessionSnapshot Snapshot => new SessionSnapshot("test", SessionPhase.Experience, "A", "visit", "model",
                Array.Empty<GoalSnapshot>(), false, false, true, SaveStatus.NotConfigured);
            public event Action<SessionSnapshot> Changed { add { } remove { } }
            public event Action<OperationResult> OperationCompleted { add { } remove { } }
            public CommandResult Execute(CommandType command) => throw new InvalidOperationException("Notifications must not send commands");
        }
    }
}
