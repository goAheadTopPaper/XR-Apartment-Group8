using System;
using UnityEngine;
using XRApartment.Data.ReviewV2;

namespace XRApartment.Runtime.ReviewV2.UI
{
    // Explicit callers only. No command execution or operation-result broadcasting.
    public sealed class ReviewNotificationBridge : MonoBehaviour
    {
        [SerializeField] ReviewV2UiSessionAdapter session;
        [SerializeField] ReviewNotificationView view;
        [SerializeField] Camera viewCamera;
        [SerializeField] ReviewNotificationView.MessageSource[] approvedSources = Array.Empty<ReviewNotificationView.MessageSource>();
        ReviewV2UiSessionAdapter subscribed;
        string placedSession;

        void OnEnable()
        {
            subscribed = session;
            if (subscribed != null) subscribed.Changed += OnSnapshot;
            OnSnapshot(subscribed != null ? subscribed.Snapshot : null);
        }
        void OnDisable()
        {
            if (subscribed != null) subscribed.Changed -= OnSnapshot;
            subscribed = null;
            placedSession = null;
        }
        void OnSnapshot(SessionSnapshot snapshot)
        {
            if (snapshot == null || snapshot.Phase != SessionPhase.Experience)
            { placedSession = null; return; }
            if (placedSession == snapshot.SessionId || viewCamera == null) return;
            // Place once on entering Experience. Never follow the head every frame.
            transform.SetPositionAndRotation(viewCamera.transform.position + viewCamera.transform.forward * .5f,
                viewCamera.transform.rotation);
            placedSession = snapshot.SessionId;
        }
        public bool TryShowToast(ReviewNotificationView.MessageIdentity identity, string text,
            ReviewNotificationView.Outcome outcome = ReviewNotificationView.Outcome.Information,
            float seconds = 3f)
        {
            if (!isActiveAndEnabled || view == null || string.IsNullOrWhiteSpace(identity.MessageId) ||
                identity.Source == ReviewNotificationView.MessageSource.Unspecified ||
                identity.Source == ReviewNotificationView.MessageSource.TestPreview ||
                identity.Source == ReviewNotificationView.MessageSource.SafetyPreview ||
                Array.IndexOf(approvedSources, identity.Source) < 0) return false;
            return view.ShowToast(identity, text, outcome, seconds);
        }
    }
}
