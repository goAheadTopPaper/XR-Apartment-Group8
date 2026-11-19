using System.Collections.Generic;
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
        sealed class Marker
        {
            public Transform View, Anchor;
            public Vector3 LocalPoint, WorldPoint;
        }
        readonly Dictionary<string, Marker> markers = new Dictionary<string, Marker>();
        SessionSnapshot context;

        void OnEnable() { recorder.RecordsChanged += Refresh; host.Changed += Changed; }
        void Start() => Refresh();
        void OnDisable() { recorder.RecordsChanged -= Refresh; host.Changed -= Changed; Clear(); context = null; }
        void Changed(SessionSnapshot snapshot)
        {
            if (context == null || IsVisible(context) != IsVisible(snapshot) || context.SchemeVisitId != snapshot.SchemeVisitId)
                Refresh();
        }
        static bool IsVisible(SessionSnapshot s) => s.Phase == SessionPhase.Experience || s.Phase == SessionPhase.Review;
        void Clear()
        {
            foreach (var marker in markers.Values)
                if (marker.View != null) { marker.View.gameObject.SetActive(false); Destroy(marker.View.gameObject); }
            markers.Clear();
        }
        void Refresh()
        {
            var snapshot = host.Snapshot;
            if (!IsVisible(snapshot) || markerPrefab == null || context?.SchemeVisitId != snapshot.SchemeVisitId) Clear();
            context = snapshot;
            if (!IsVisible(snapshot) || markerPrefab == null) return;
            // The recorder never deletes a marker within a visit. Changes to note/status do not change its pose or icon.
            foreach (var record in recorder.RecordsForSession(snapshot.SessionId, snapshot.SchemeId, snapshot.SchemeVisitId, snapshot.ModelVersion))
            {
                if (markers.ContainsKey(record.markerId)) continue;
                var instance = Instantiate(markerPrefab, transform);
                instance.name = "Feedback " + record.markerId;
                var text = instance.GetComponentInChildren<TMP_Text>();
                text.text = record.type == FeedbackType.Positive ? "+" : record.type == FeedbackType.Concern ? "!" : "?";
                text.color = record.type == FeedbackType.Positive ? new Color(.25f, 1, .55f) : record.type == FeedbackType.Concern ? new Color(1, .65f, .2f) : new Color(.3f, .8f, 1);
                markers.Add(record.markerId, new Marker { View = instance.transform,
                    Anchor = record.hasObjectLocalPosition ? host.Resolve(record.objectId) ?? host.Resolve(record.anchorId) : null,
                    LocalPoint = new Vector3(record.localX, record.localY, record.localZ),
                    WorldPoint = new Vector3(record.worldX, record.worldY, record.worldZ) });
            }
            Update();
        }
        void Update()
        {
            if (markers.Count == 0) return;
            var cameraPosition = host.locomotion.Origin.Camera.transform.position;
            foreach (var marker in markers.Values)
            {
                if (marker.View == null) continue;
                var point = marker.Anchor != null ? marker.Anchor.TransformPoint(marker.LocalPoint) : marker.WorldPoint;
                var delta = point - cameraPosition;
                if (delta.sqrMagnitude < .000001f) continue;
                marker.View.SetPositionAndRotation(point - delta.normalized * .025f, Quaternion.LookRotation(delta, Vector3.up));
            }
        }
    }
}
