using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XRApartment.Data.ReviewV2;

namespace XRApartment.Runtime.ReviewV2.UI
{
    public sealed class ReviewNotificationView : MonoBehaviour
    {
        public enum MessageSource { Unspecified, SessionStatus, Device, SessionSave, ObjectRecovery, Marker, SafetyPreview, TestPreview }
        public enum Outcome { Information, Accepted, Pending, Succeeded, Failed, Unconfirmed, Unsupported }
        public enum Severity { ImportantError, CriticalError, SafetyPreview }

        public readonly struct MessageIdentity
        {
            public readonly string SessionId;
            public readonly MessageSource Source;
            // An operation request ID or a marker ID combined with its save-attempt ID.
            public readonly string MessageId;

            public MessageIdentity(string sessionId, MessageSource source, string messageId = null)
            {
                SessionId = sessionId;
                Source = source;
                MessageId = messageId;
            }
        }

        public const int ToastQueueCapacity = 4;
        const int RecentIdentityCapacity = 64;
        const float DefaultToastSeconds = 3f;

        [SerializeField] ReviewV2UiSessionAdapter session;
        [SerializeField] GameObject persistentStatus, toastContainer, importantPanel, actionArea;
        [SerializeField] TMP_Text statusText, statusDescription, toastText, severityText, importantTitle, importantMessage;
        [SerializeField] Button dismissButton, optionalActionButton;
        [SerializeField] GameObject confirmationPanel;

        readonly struct Toast
        {
            public readonly string Text;
            public readonly float Seconds;
            public readonly int Priority;

            public Toast(string text, float seconds, int priority)
            {
                Text = text;
                Seconds = seconds;
                Priority = priority;
            }
        }

        readonly List<Toast> queue = new List<Toast>(ToastQueueCapacity);
        readonly Queue<(MessageSource, string, int)> recent = new Queue<(MessageSource, string, int)>();
        readonly HashSet<(MessageSource, string, int)> remembered = new HashSet<(MessageSource, string, int)>();
        IReviewV2UiSession boundSession, subscribedSession;
        bool hasBindingOverride, hasToast;
        string sessionId;
        float toastTimeRemaining;
        Severity? currentSeverity;
        bool allowDismiss;

        IReviewV2UiSession Session => hasBindingOverride ? boundSession : session;
        public int QueuedToastCount => queue.Count;

        void OnEnable()
        {
            if (dismissButton != null) dismissButton.onClick.AddListener(OnDismiss);
            if (optionalActionButton != null) optionalActionButton.interactable = false;
            ClearNotifications();
            Subscribe();
            RefreshFromSession();
        }

        void OnDisable()
        {
            if (dismissButton != null) dismissButton.onClick.RemoveListener(OnDismiss);
            Unsubscribe();
            sessionId = null;
            ClearNotifications();
            SetVisible(persistentStatus, false);
        }

        public void BindSession(IReviewV2UiSession source)
        {
            if (!ReferenceEquals(Session, source))
            {
                sessionId = null;
                ClearNotifications();
            }

            hasBindingOverride = true;
            boundSession = source;
            if (!isActiveAndEnabled) return;
            Subscribe();
            RefreshFromSession();
        }

        public void RefreshFromSession()
        {
            var source = Session;
            var snapshot = source is ReviewV2UiSessionAdapter adapter && (adapter == null || !adapter.isActiveAndEnabled)
                ? null : source?.Snapshot;
            string currentId = isActiveAndEnabled && snapshot?.Phase == SessionPhase.Experience &&
                !string.IsNullOrWhiteSpace(snapshot.SessionId) ? snapshot.SessionId : null;

            if (!string.Equals(sessionId, currentId, StringComparison.Ordinal))
            {
                ClearNotifications();
                sessionId = currentId;
            }

            bool paused = currentId != null && snapshot.IsPaused;
            SetText(statusText, paused ? "Paused" : "");
            SetText(statusDescription, paused ? "Experience is paused" : "");
            SetVisible(persistentStatus, paused);
        }

        void OnSnapshotChanged(SessionSnapshot _) => RefreshFromSession();

        void Subscribe()
        {
            if (ReferenceEquals(subscribedSession, Session)) return;
            Unsubscribe();
            subscribedSession = Session;
            if (subscribedSession != null) subscribedSession.Changed += OnSnapshotChanged;
        }

        void Unsubscribe()
        {
            if (subscribedSession == null) return;
            subscribedSession.Changed -= OnSnapshotChanged;
            subscribedSession = null;
        }

        public bool ShowToast(MessageIdentity identity, string text, Outcome outcome = Outcome.Information,
            float durationSeconds = DefaultToastSeconds)
        {
            RefreshFromSession();
            if (!CanShow(identity) || string.IsNullOrWhiteSpace(text) || identity.Source == MessageSource.SafetyPreview ||
                !Enum.IsDefined(typeof(Outcome), outcome) || IsDuplicate(identity, (int)outcome)) return false;

            var seconds = float.IsNaN(durationSeconds) ? DefaultToastSeconds : Mathf.Clamp(durationSeconds, 1f, 8f);
            string prefix = outcome == Outcome.Information ? "" : outcome + ": ";
            var toast = new Toast(PreviewPrefix(identity) + prefix + text, seconds, outcome == Outcome.Information ? 0 : 1);
            if (!hasToast && !currentSeverity.HasValue)
            {
                StartToast(toast);
            }
            else
            {
                // Higher-priority statuses can evict the newest item at the lowest priority.
                // Otherwise keep the earlier messages, in arrival order.
                if (queue.Count == ToastQueueCapacity)
                {
                    if (toast.Priority <= queue[queue.Count - 1].Priority) return false;
                    queue.RemoveAt(queue.Count - 1);
                }
                int index = queue.FindIndex(waiting => waiting.Priority < toast.Priority);
                if (index < 0) queue.Add(toast);
                else queue.Insert(index, toast);
            }

            Remember(identity, (int)outcome);
            return true;
        }

        public bool ShowImportantMessage(MessageIdentity identity, string title, string message,
            Severity severity = Severity.ImportantError, bool canDismiss = false)
        {
            RefreshFromSession();
            int key = 100 + (int)severity;
            if (!CanShow(identity) || string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(message) ||
                !Enum.IsDefined(typeof(Severity), severity) ||
                (severity == Severity.SafetyPreview) != (identity.Source == MessageSource.SafetyPreview) ||
                (currentSeverity.HasValue && severity < currentSeverity.Value) || IsDuplicate(identity, key)) return false;

            currentSeverity = severity;
            allowDismiss = canDismiss;
            SetText(severityText, severity == Severity.SafetyPreview ? "SAFETY\nPREVIEW" :
                severity == Severity.CriticalError ? "CRITICAL\nERROR" : "SYSTEM\nERROR");
            if (severityText != null) severityText.color = severity == Severity.SafetyPreview
                ? new Color32(255, 208, 122, 255) : new Color32(255, 182, 192, 255);
            SetText(importantTitle, PreviewPrefix(identity) + title);
            SetText(importantMessage, message);
            SetVisible(importantPanel, true);
            SetVisible(toastContainer, false);
            RefreshDismissAction();
            Remember(identity, key);
            return true;
        }

        public bool DismissImportantMessage()
        {
            RefreshFromSession();
            if (!currentSeverity.HasValue || !allowDismiss || ConfirmationOpen) return false;
            HideImportant();
            if (hasToast) SetVisible(toastContainer, true);
            else ShowNextToast();
            return true;
        }

        public void ClearNotifications()
        {
            queue.Clear();
            recent.Clear();
            remembered.Clear();
            hasToast = false;
            toastTimeRemaining = 0;
            SetText(toastText, "");
            SetVisible(toastContainer, false);
            HideImportant();
        }

        void Update()
        {
            // The existing adapter can lose its source without publishing Changed(null).
            RefreshFromSession();
            RefreshDismissAction();
            AdvanceToast(Time.unscaledDeltaTime);
        }

        void AdvanceToast(float unscaledDeltaTime)
        {
            if (!hasToast || currentSeverity.HasValue) return;
            toastTimeRemaining -= unscaledDeltaTime;
            if (toastTimeRemaining > 0) return;
            hasToast = false;
            SetText(toastText, "");
            SetVisible(toastContainer, false);
            ShowNextToast();
        }

        void ShowNextToast()
        {
            if (queue.Count == 0) return;
            var next = queue[0];
            queue.RemoveAt(0);
            StartToast(next);
        }

        void StartToast(Toast toast)
        {
            hasToast = true;
            toastTimeRemaining = toast.Seconds;
            SetText(toastText, toast.Text);
            SetVisible(toastContainer, true);
        }

        bool ConfirmationOpen => confirmationPanel != null && confirmationPanel.activeInHierarchy;

        void OnDismiss()
        {
            if (dismissButton != null && dismissButton.isActiveAndEnabled && dismissButton.IsInteractable() &&
                EventSystem.current != null && EventSystem.current.currentSelectedGameObject == dismissButton.gameObject)
                DismissImportantMessage();
        }

        void RefreshDismissAction()
        {
            bool show = currentSeverity.HasValue && allowDismiss && !ConfirmationOpen;
            if (!show) ClearDismissFocus();
            SetVisible(actionArea, show);
        }

        void HideImportant()
        {
            currentSeverity = null;
            allowDismiss = false;
            RefreshDismissAction();
            SetText(severityText, "");
            SetText(importantTitle, "");
            SetText(importantMessage, "");
            SetVisible(importantPanel, false);
        }

        void ClearDismissFocus()
        {
            var system = EventSystem.current;
            if (dismissButton != null && system != null && !system.alreadySelecting &&
                system.currentSelectedGameObject == dismissButton.gameObject)
                system.SetSelectedGameObject(null);
        }

        bool CanShow(MessageIdentity identity) =>
            sessionId != null && string.Equals(sessionId, identity.SessionId, StringComparison.Ordinal) &&
            identity.Source != MessageSource.Unspecified && Enum.IsDefined(typeof(MessageSource), identity.Source);

        static string PreviewPrefix(MessageIdentity identity) =>
            identity.Source == MessageSource.SafetyPreview || identity.Source == MessageSource.TestPreview
                ? "PREVIEW — " : "";

        bool IsDuplicate(MessageIdentity identity, int kind) => !string.IsNullOrWhiteSpace(identity.MessageId) &&
            remembered.Contains((identity.Source, identity.MessageId, kind));

        void Remember(MessageIdentity identity, int kind)
        {
            if (string.IsNullOrWhiteSpace(identity.MessageId)) return;
            if (recent.Count == RecentIdentityCapacity) remembered.Remove(recent.Dequeue());
            var key = (identity.Source, identity.MessageId, kind);
            recent.Enqueue(key);
            remembered.Add(key);
        }

        static void SetVisible(GameObject region, bool visible)
        {
            if (region != null && region.activeSelf != visible) region.SetActive(visible);
        }

        static void SetText(TMP_Text label, string value)
        {
            if (label != null && label.text != value) label.text = value;
        }
    }
}
