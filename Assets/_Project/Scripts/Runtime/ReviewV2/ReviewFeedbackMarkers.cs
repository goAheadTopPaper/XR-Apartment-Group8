using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using XRApartment.Data.ReviewV2;
namespace XRApartment.Runtime.ReviewV2
{
    public sealed class ReviewFeedbackMarkers : MonoBehaviour
    {
        public ReviewSessionHost host;
        public SpatialFeedbackRecorder recorder;
        public GameObject markerPrefab;
        IReadOnlyList<SpatialFeedbackRecord> visible = new SpatialFeedbackRecord[0];
        readonly Dictionary<string, GameObject> markers = new Dictionary<string, GameObject>();
        void OnEnable() { recorder.RecordsChanged += Rebuild; host.Changed += Changed; }
        void Start() => Rebuild();
        void OnDisable() { recorder.RecordsChanged -= Rebuild; host.Changed -= Changed; Clear(); }
        void Changed(SessionSnapshot s) => Rebuild();
        void Clear() { foreach (var m in markers.Values) if (m != null) { m.SetActive(false); Destroy(m); } markers.Clear(); }
        void Rebuild()
        {
            Clear(); if (!recorder.IsReady || markerPrefab == null) return;
            var s = host.Snapshot;
            if (s.Phase != SessionPhase.Experience && s.Phase != SessionPhase.Review) return;
            visible = recorder.Records.Where(r => r.sessionId == s.SessionId && r.schemeVisitId == s.SchemeVisitId && r.modelVersion == s.ModelVersion).ToArray();
            foreach (var r in visible)
            {
                var marker = Instantiate(markerPrefab, transform); marker.name = "Feedback " + r.markerId;
                marker.GetComponentInChildren<TMP_Text>().text = r.type == FeedbackType.Positive ? "+" : r.type == FeedbackType.Concern ? "!" : "?";
                marker.GetComponentInChildren<TMP_Text>().color = r.type == FeedbackType.Positive ? new Color(.25f, 1, .55f) : r.type == FeedbackType.Concern ? new Color(1, .65f, .2f) : new Color(.3f, .8f, 1);
                markers.Add(r.markerId, marker);
            }
            Update();
        }
        void Update()
        {
            var camera = host.locomotion.Origin.Camera.transform;
            foreach (var r in visible)
            {
                if (!markers.TryGetValue(r.markerId, out var marker) || marker == null) continue;
                var t = host.Resolve(r.objectId ?? r.anchorId);
                var point = r.hasObjectLocalPosition && t != null ? t.TransformPoint(new Vector3(r.localX, r.localY, r.localZ)) : new Vector3(r.worldX, r.worldY, r.worldZ);
                marker.transform.position = point + (camera.position - point).normalized * .025f;
                marker.transform.rotation = Quaternion.LookRotation(marker.transform.position - camera.position, Vector3.up);
            }
        }
    }
}
