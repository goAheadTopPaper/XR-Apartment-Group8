using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using XRApartment.Data.ReviewV2;

namespace XRApartment.Runtime.ReviewV2
{
    [DefaultExecutionOrder(-200)]
    public sealed class SpatialFeedbackRecorder : MonoBehaviour
    {
        public ReviewSessionHost host;
        public int contextEventLimit = 12;
        readonly Queue<string> recent = new Queue<string>();
        readonly List<SpatialFeedbackRecord> awaitingAfter = new List<SpatialFeedbackRecord>();
        LocalFeedbackStore store;
        SpatialFeedbackDocument document;
        string contextSession, contextVisit;
        long revision, savedRevision = -1;
        Task<bool> loadTask, saveTask;
        public string Error { get; private set; }
        public bool IsLoading { get; private set; }
        public bool IsReady => document != null && Error == null;
        public IReadOnlyList<SpatialFeedbackRecord> Records => RecordsForSession(null);

        internal IReadOnlyList<SpatialFeedbackRecord> RecordsForSession(string session, string scheme = null,
            string visit = null, string model = null)
        {
            if (document == null) return Array.Empty<SpatialFeedbackRecord>();
            var result = new List<SpatialFeedbackRecord>();
            foreach (var r in document.records)
                if ((session == null || r.sessionId == session) && (scheme == null || r.schemeId == scheme) &&
                    (visit == null || r.schemeVisitId == visit) && (model == null || r.modelVersion == model))
                    result.Add(r.Copy());
            return result;
        }
        public event Action RecordsChanged;
        public string FilePath => store?.FilePath;

        void Awake() => _ = ReloadAsync();
        void OnEnable() { if (host != null) { host.Observed += OnFact; host.Changed += OnContext; } }
        void OnDisable() { if (host != null) { host.Observed -= OnFact; host.Changed -= OnContext; } CloseWindow(); }

        public Task<bool> ReloadAsync()
        {
            // Never reload over unsaved in-memory submissions.
            if (document != null) return RetrySaveAsync();
            return IsLoading ? loadTask : (loadTask = LoadAsync());
        }

        async Task<bool> LoadAsync()
        {
            IsLoading = true;
            try
            {
                store = new LocalFeedbackStore();
                var source = store;
                document = await Task.Run(source.Load);
                Error = null;
            }
            catch (Exception ex) { Error = ex.Message; }
            finally { IsLoading = false; }
            NotifyRecordsChanged();
            return IsReady;
        }

        public void Bind(ReviewSessionHost sessionHost)
        {
            if (host != null) { host.Observed -= OnFact; host.Changed -= OnContext; }
            host = sessionHost; CloseWindow();
            if (isActiveAndEnabled && host != null) { host.Observed += OnFact; host.Changed += OnContext; }
        }
        void CloseWindow() { recent.Clear(); awaitingAfter.Clear(); contextSession = contextVisit = null; }
        void OnContext(SessionSnapshot s)
        {
            if (s.IsPaused || s.IsBusy || (s.Phase != SessionPhase.Experience && s.Phase != SessionPhase.Review) || s.SessionId != contextSession || s.SchemeVisitId != contextVisit) CloseWindow();
        }
        void EnsureContext(SessionSnapshot s)
        {
            if (s.SessionId != contextSession || s.SchemeVisitId != contextVisit) CloseWindow();
            contextSession = s.SessionId; contextVisit = s.SchemeVisitId;
        }
        void OnFact(InteractionEvent fact)
        {
            if (document == null || host == null || !host.CanObserve) return;
            var s = host.Snapshot;
            if (fact.SessionId != s.SessionId || fact.SchemeVisitId != s.SchemeVisitId) return;
            EnsureContext(s);
            recent.Enqueue(fact.EventId); while (recent.Count > Mathf.Max(1, contextEventLimit)) recent.Dequeue();
            if (awaitingAfter.Count == 0) return;
            foreach (var record in awaitingAfter)
            {
                record.afterEventId = fact.EventId;
                record.saveStatus = FeedbackSaveStatus.Pending;
            }
            awaitingAfter.Clear();
            revision++;
            _ = RetrySaveAsync();
            NotifyRecordsChanged();
        }
        public SpatialFeedbackRecord AddMarker(FeedbackType type, Vector3 point, string objectId = null, string anchorId = null)
            => AddMarker(type, point, "", "", objectId, anchorId, null);
        public SpatialFeedbackRecord AddMarker(FeedbackType type, Vector3 point, string explanation, string importance, string objectId, string anchorId, string zoneId)
        {
            if (!IsReady) throw new InvalidOperationException(Error ?? "Feedback is unavailable");
            if (host == null || !host.CanObserve) throw new InvalidOperationException("Feedback is available during Experience or Review.");
            var s = host.Snapshot; EnsureContext(s);
            var t = !string.IsNullOrEmpty(objectId) ? host.Resolve(objectId) : null;
            if (t == null && !string.IsNullOrEmpty(anchorId)) t = host.Resolve(anchorId);
            var p = t != null ? t.InverseTransformPoint(point) : point;
            var r = new SpatialFeedbackRecord { sessionId = s.SessionId, schemeId = s.SchemeId, schemeVisitId = s.SchemeVisitId,
                modelVersion = s.ModelVersion, markerId = Guid.NewGuid().ToString("N"), timestampUtcMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                objectId = objectId, anchorId = anchorId, zoneId = zoneId, hasObjectLocalPosition = t != null,
                localX = p.x, localY = p.y, localZ = p.z, worldX = point.x, worldY = point.y, worldZ = point.z, type = type,
                beforeEventId = recent.LastOrDefault(), explanation = explanation ?? "", importance = importance ?? "", saveStatus = FeedbackSaveStatus.Pending };
            document.records.Add(r); awaitingAfter.Add(r); revision++;
            _ = RetrySaveAsync();
            NotifyRecordsChanged();
            return r.Copy();
        }
        public Task<bool> AddExplanationAsync(string id, string explanation, string importance)
        {
            var r = document?.records.Find(x => x.markerId == id);
            if (r == null) return Task.FromResult(false);
            r.explanation = explanation ?? ""; r.importance = importance ?? "";
            r.saveStatus = FeedbackSaveStatus.Pending; revision++;
            var written = RetrySaveAsync();
            NotifyRecordsChanged();
            return written;
        }
        public async Task<bool> RetrySaveAsync()
        {
            if (IsLoading && !await loadTask) return false;
            if (document == null) return false; // A corrupt file is never overwritten by a retry.
            if (saveTask != null && !saveTask.IsCompleted) return await saveTask;
            if (savedRevision == revision) return true;
            saveTask = PersistAsync();
            return await saveTask;
        }
        async Task<bool> PersistAsync()
        {
            while (savedRevision != revision)
            {
                long target = revision;
                var payload = new SpatialFeedbackDocument { schemaVersion = document.schemaVersion };
                foreach (var r in document.records)
                {
                    var copy = r.Copy(); copy.saveStatus = FeedbackSaveStatus.Verified;
                    payload.records.Add(copy);
                }
                try
                {
                    var destination = store;
                    await LocalFeedbackStore.WriteAsync(() => destination.Save(payload));
                }
                catch (Exception ex)
                {
                    Error = ex.Message;
                    foreach (var r in document.records)
                        if (r.saveStatus != FeedbackSaveStatus.Verified) r.saveStatus = FeedbackSaveStatus.Failed;
                    NotifyRecordsChanged();
                    return false;
                }
                savedRevision = target;
                // Requests made during the write share one task and coalesce into the next snapshot.
                if (savedRevision != revision) continue;
                Error = null;
                foreach (var r in document.records) r.saveStatus = FeedbackSaveStatus.Verified;
                NotifyRecordsChanged();
            }
            return true;
        }
        void NotifyRecordsChanged() { if (this != null) RecordsChanged?.Invoke(); }
        public string ExportJson() => store.ExportJson();
    }
}
