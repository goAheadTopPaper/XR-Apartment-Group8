using System;
using System.Collections.Generic;
using System.Linq;
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
        readonly HashSet<string> awaitingAfter = new HashSet<string>();
        LocalFeedbackStore store;
        SpatialFeedbackDocument document;
        string contextSession, contextVisit;
        bool loaded;
        public string Error { get; private set; }
        public bool IsReady => loaded && Error == null;
        public IReadOnlyList<SpatialFeedbackRecord> Records => document == null ? Array.Empty<SpatialFeedbackRecord>() : document.records.Select(Clone).ToArray();
        public event Action RecordsChanged;
        public string FilePath => store?.FilePath;
        void Awake() => Reload();
        void OnEnable() { if (host != null) { host.Observed += OnFact; host.Changed += OnContext; } }
        void OnDisable() { if (host != null) { host.Observed -= OnFact; host.Changed -= OnContext; } CloseWindow(); }
        public bool Reload()
        {
            // Never discard in-memory failed submissions when retrying a write.
            if (loaded && Error != null) return RetrySave();
            try { store = new LocalFeedbackStore(); document = store.Load(); loaded = true; Error = null; }
            catch (Exception ex) { loaded = false; Error = ex.Message; }
            RecordsChanged?.Invoke(); return IsReady;
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
            if (!IsReady || host == null || !host.CanObserve) return;
            var s = host.Snapshot;
            if (fact.SessionId != s.SessionId || fact.SchemeVisitId != s.SchemeVisitId) return;
            EnsureContext(s); bool changed = false;
            foreach (var r in document.records)
                if (awaitingAfter.Contains(r.markerId) && r.sessionId == fact.SessionId && r.schemeVisitId == fact.SchemeVisitId)
                { r.afterEventId = fact.EventId; awaitingAfter.Remove(r.markerId); changed = true; }
            recent.Enqueue(fact.EventId); while (recent.Count > Mathf.Max(1, contextEventLimit)) recent.Dequeue();
            if (changed) { Persist(); RecordsChanged?.Invoke(); }
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
            document.records.Add(r); awaitingAfter.Add(r.markerId); Persist(); RecordsChanged?.Invoke(); return Clone(r);
        }
        public bool AddExplanation(string id, string explanation, string importance)
        {
            var r = document?.records.Find(x => x.markerId == id); if (r == null) return false;
            r.explanation = explanation ?? ""; r.importance = importance ?? "";
            var ok = Persist(); RecordsChanged?.Invoke(); return ok;
        }
        public bool RetrySave() { var ok = Persist(); RecordsChanged?.Invoke(); return ok; }
        bool Persist()
        {
            if (!loaded || document == null || store == null) return false;
            try { foreach (var r in document.records) r.saveStatus = FeedbackSaveStatus.Verified; store.Save(document); Error = null; return true; }
            catch (Exception ex) { foreach (var r in document.records) r.saveStatus = FeedbackSaveStatus.Failed; Error = ex.Message; return false; }
        }
        public string ExportJson() => store.ExportJson();
        static SpatialFeedbackRecord Clone(SpatialFeedbackRecord r) => JsonUtility.FromJson<SpatialFeedbackRecord>(JsonUtility.ToJson(r));
    }
}
