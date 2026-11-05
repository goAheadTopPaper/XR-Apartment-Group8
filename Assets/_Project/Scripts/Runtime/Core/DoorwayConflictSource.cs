using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using XRApartment.Data;
using XRApartment.Simulation;

namespace XRApartment.Runtime.Core
{
    /// <summary>Reports and marks measured opening limits on registered, fixed cabinet parts.</summary>
    [RequireComponent(typeof(JourneyRuntimeHost), typeof(RuntimeBenchmarkAdapter), typeof(TaskEvidenceBridge))]
    public sealed class DoorwayConflictSource : MonoBehaviour, IConflictSource
    {
        private const string IssuePrefix = "door-sweep/";
        private static readonly int[] BoxEdges = { 0, 1, 3, 2, 0, 4, 5, 1, 5, 7, 3, 7, 6, 2, 6, 4 };
        private static readonly Color Yellow = new Color(1f, .89f, .22f);
        private static readonly Color Amber = new Color(1f, .58f, .12f);
        [SerializeField] private ModelAssumptions assumptions;

        private readonly HashSet<string> reported = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, ReviewDoorObstacle> obstacles =
            new Dictionary<string, ReviewDoorObstacle>(StringComparer.Ordinal);
        private readonly Vector3[] points = new Vector3[BoxEdges.Length];
        private JourneyRuntimeHost host;
        private RuntimeBenchmarkAdapter adapter;
        private TaskEvidenceBridge evidence;
        private JourneySnapshot snapshot;
        private GameObject instance;
        private KinematicSwingDoor[] doors = Array.Empty<KinematicSwingDoor>();
        private Collider lastBlocker;
        private GameObject marker;
        private LineRenderer outline;
        private TMP_Text caption;
        private Material lineMaterial;

        public event Action<ConflictNotice> ConflictObserved;

        private void OnEnable()
        {
            host = GetComponent<JourneyRuntimeHost>();
            adapter = GetComponent<RuntimeBenchmarkAdapter>();
            evidence = GetComponent<TaskEvidenceBridge>();
            host.SnapshotUpdated += Render;
            if (host.Core != null) Render(host.Core.Snapshot);
        }

        private void OnDisable()
        {
            host.SnapshotUpdated -= Render;
            if (marker != null) marker.SetActive(false);
            lastBlocker = null;
        }

        private void OnDestroy()
        {
            if (lineMaterial != null) Destroy(lineMaterial);
        }

        private void LateUpdate()
        {
            if (snapshot == null) return;
            if (snapshot.IsBusy || (snapshot.State != SessionState.Explore &&
                snapshot.State != SessionState.ChallengeActive))
            {
                lastBlocker = null;
                FaceCaption();
                return;
            }
            if (instance != adapter.CurrentInstance) Render(snapshot);
            Collider blocker = null;
            foreach (var door in doors)
            {
                if (door == null || door.OpeningBlocker == null) continue;
                blocker = door.OpeningBlocker;
                if (blocker != lastBlocker) Observe(door, blocker);
                break;
            }
            lastBlocker = blocker;
            FaceCaption();
        }

        private void Observe(KinematicSwingDoor door, Collider blocker)
        {
            if (instance == null || assumptions == null || blocker.attachedRigidbody != null ||
                !blocker.TryGetComponent<ReviewDoorObstacle>(out var obstacle) ||
                !obstacle.isActiveAndEnabled || string.IsNullOrWhiteSpace(obstacle.StableId) ||
                string.IsNullOrWhiteSpace(obstacle.DisplayName) ||
                !obstacle.transform.IsChildOf(instance.transform) ||
                !assumptions.TryGetLength(obstacle.AssumptionKey, out var depth) || depth <= 0f ||
                !door.TryGetComponent<TaskEvidenceIdentity>(out var identity) || identity.Role != ReviewRole.FridgeDoor)
                return;
            var benchmark = snapshot.State == SessionState.ChallengeActive
                ? snapshot.ActiveBenchmarkId : snapshot.DisplayedBenchmarkId;
            if (string.IsNullOrEmpty(benchmark) || benchmark != adapter.CurrentBenchmarkId) return;

            var issueId = IssuePrefix + identity.StableId + "/" + obstacle.StableId;
            if (!reported.Add(benchmark + "/" + issueId)) return;
            var angle = Mathf.Abs(Mathf.DeltaAngle(0f, door.transform.localEulerAngles.y));
            ConflictObserved?.Invoke(new ConflictNotice
            {
                IssueId = issueId,
                SessionId = snapshot.SessionId,
                BenchmarkId = benchmark,
                ModelVersion = assumptions.ModelVersion,
                ObjectId = identity.StableId,
                Role = identity.Role,
                AssumptionKey = obstacle.AssumptionKey,
                Measurement = angle,
                OccurredAtSessionSeconds = evidence.SessionSeconds,
                Message = $"Opening blocked at {angle:0}° by {obstacle.DisplayName}."
            });
        }

        private void Render(JourneySnapshot current)
        {
            snapshot = current;
            if (instance != adapter.CurrentInstance)
            {
                instance = adapter.CurrentInstance;
                lastBlocker = null;
                obstacles.Clear();
                doors = instance == null ? Array.Empty<KinematicSwingDoor>() :
                    instance.GetComponentsInChildren<KinematicSwingDoor>();
                if (instance != null)
                    foreach (var obstacle in instance.GetComponentsInChildren<ReviewDoorObstacle>())
                        if (!string.IsNullOrWhiteSpace(obstacle.StableId)) obstacles[obstacle.StableId] = obstacle;
            }

            if (marker != null) marker.SetActive(false);
            if (current == null || current.IsBusy || current.CurrentSafety != null ||
                current.State == SessionState.Paused || current.State == SessionState.Exited ||
                current.State == SessionState.Finished || current.Conflicts == null) return;

            for (var index = current.Conflicts.Count - 1; index >= 0; index--)
            {
                var notice = current.Conflicts[index];
                if (notice.BenchmarkId != adapter.CurrentBenchmarkId ||
                    !notice.IssueId.StartsWith(IssuePrefix, StringComparison.Ordinal) ||
                    !obstacles.TryGetValue(notice.IssueId.Substring(notice.IssueId.LastIndexOf('/') + 1),
                        out var obstacle) || obstacle == null || !obstacle.isActiveAndEnabled)
                    continue;
                Mark(obstacle.GetComponent<BoxCollider>(), notice);
                return;
            }
        }

        private void Mark(BoxCollider obstacle, ConflictNotice notice)
        {
            EnsureMarker();
            if (marker == null) return;
            var half = obstacle.size * .5f + Vector3.one * .003f;
            for (var index = 0; index < BoxEdges.Length; index++)
            {
                var bits = BoxEdges[index];
                points[index] = obstacle.transform.TransformPoint(obstacle.center + new Vector3(
                    (bits & 1) == 0 ? -half.x : half.x,
                    (bits & 2) == 0 ? -half.y : half.y,
                    (bits & 4) == 0 ? -half.z : half.z));
            }
            var colour = notice.FeedbackRecorded ? Amber : Yellow;
            outline.startColor = outline.endColor = colour;
            outline.SetPositions(points);
            var bounds = obstacle.bounds;
            caption.transform.position = new Vector3(bounds.center.x, bounds.max.y + .12f, bounds.center.z);
            caption.color = colour;
            caption.text = "! " + notice.Message +
                (notice.FeedbackRecorded ? "\nFeedback recorded" : "\nDesign conflict");
            marker.SetActive(true);
            FaceCaption();
        }

        private void FaceCaption()
        {
            if (marker == null || !marker.activeSelf) return;
            var camera = Camera.main;
            if (camera == null) return;
            var facing = caption.transform.position - camera.transform.position;
            if (facing.sqrMagnitude > .001f) caption.transform.rotation = Quaternion.LookRotation(facing);
        }

        private void EnsureMarker()
        {
            if (marker != null) return;
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) return;
            marker = new GameObject("ReviewDesignConflict");
            marker.transform.SetParent(transform, false);
            lineMaterial = new Material(shader);
            outline = marker.AddComponent<LineRenderer>();
            outline.sharedMaterial = lineMaterial;
            outline.shadowCastingMode = ShadowCastingMode.Off;
            outline.receiveShadows = false;
            outline.positionCount = points.Length;
            outline.widthMultiplier = .008f;

            var label = new GameObject("Design conflict caption");
            label.transform.SetParent(marker.transform, false);
            label.transform.localScale = Vector3.one * .15f;
            caption = label.AddComponent<TextMeshPro>();
            caption.rectTransform.sizeDelta = new Vector2(6f, 2f);
            caption.fontSize = 4f;
            caption.alignment = TextAlignmentOptions.Center;
            caption.richText = false;
        }
    }
}
