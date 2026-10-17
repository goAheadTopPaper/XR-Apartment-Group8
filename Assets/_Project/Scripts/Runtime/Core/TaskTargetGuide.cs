using System.Collections.Generic;
using UnityEngine;
using XRApartment.Simulation;

namespace XRApartment.Runtime.Core
{
    /// <summary>Replaceable, non-colliding Play guidance driven by the Core snapshot.</summary>
    public sealed class TaskTargetGuide : MonoBehaviour
    {
        [SerializeField] private JourneyRuntimeHost host;
        [SerializeField] private TaskEvidenceBridge evidenceBridge;
        [SerializeField] private Color colour = new Color(0.18f, 1f, 0.28f, 1f);
        [SerializeField] private Material arrowMaterial;

        private readonly List<Transform> arrows = new List<Transform>();
        private readonly List<string> visibleTargets = new List<string>();
        private readonly Dictionary<string, TaskEvidenceIdentity> identities =
            new Dictionary<string, TaskEvidenceIdentity>();
        private readonly Dictionary<string, Renderer[]> zoneHints =
            new Dictionary<string, Renderer[]>();
        private Material material;
        private Mesh mesh;
        private bool ownsMaterial;
        private string benchmarkId;
        private long runId;

        public IReadOnlyList<string> VisibleTargets => visibleTargets;

        private void Awake()
        {
            material = arrowMaterial;
            if (material == null)
            {
                material = new Material(Shader.Find("Unlit/Color"));
                material.color = colour;
                ownsMaterial = true;
            }
            mesh = new Mesh { name = "Downward guide triangle" };
            mesh.vertices = new[]
            {
                new Vector3(0f, -0.065f, 0f),
                new Vector3(-0.055f, 0.05f, 0f),
                new Vector3(0.055f, 0.05f, 0f)
            };
            mesh.triangles = new[] { 0, 2, 1 };
            mesh.RecalculateNormals();
            foreach (var id in new[]
            {
                "ZONE_Approach", "ZONE_Leave", "ZONE_FreezerSource",
                "ZONE_Temporary", "ZONE_FreshDestination"
            })
            {
                var zone = transform.Find(id);
                if (zone != null)
                    zoneHints[id] = zone.GetComponentsInChildren<Renderer>(true);
            }
            HideZoneHints();
        }

        private void OnDestroy()
        {
            if (ownsMaterial && material != null) Destroy(material);
            if (mesh != null) Destroy(mesh);
        }

        private void LateUpdate()
        {
            visibleTargets.Clear();
            if (host == null || host.Core == null || evidenceBridge == null)
            {
                HideZoneHints();
                ShowCount(0);
                return;
            }
            var snapshot = host.Core.Snapshot;
            if (snapshot.State != SessionState.ChallengeActive || snapshot.IsBusy ||
                !snapshot.CurrentStep.HasValue)
            {
                HideZoneHints();
                ShowCount(0);
                return;
            }
            if (benchmarkId != snapshot.ActiveBenchmarkId || runId != snapshot.ActiveRunId)
            {
                benchmarkId = snapshot.ActiveBenchmarkId;
                runId = snapshot.ActiveRunId;
                identities.Clear();
                foreach (var identity in FindObjectsByType<TaskEvidenceIdentity>(
                    FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    if (identity != null && !string.IsNullOrEmpty(identity.StableId))
                        identities[identity.StableId] = identity;
            }

            UpdateZoneHints(snapshot.CurrentStep.Value);

            var positions = new List<Vector3>();
            switch (snapshot.CurrentStep.Value)
            {
                case ChallengeStep.Approach:
                    AddZone("ZONE_Approach", positions);
                    break;
                case ChallengeStep.Open:
                    AddNextAccess(ChallengeStep.Open, positions);
                    break;
                case ChallengeStep.TakeOut:
                    AddNextItem(ChallengeStep.TakeOut, positions);
                    break;
                case ChallengeStep.TemporaryPlacement:
                    AddNextItemOrZone(ChallengeStep.TemporaryPlacement,
                        "ZONE_Temporary", positions);
                    break;
                case ChallengeStep.Reorganise:
                    AddNextItemOrZone(ChallengeStep.Reorganise,
                        "ZONE_FreshDestination", positions);
                    break;
                case ChallengeStep.PutBack:
                    AddNextItemOrZone(ChallengeStep.PutBack,
                        "ZONE_FreshDestination", positions);
                    break;
                case ChallengeStep.Close:
                    AddNextAccess(ChallengeStep.Close, positions);
                    break;
                case ChallengeStep.Leave:
                    AddZone("ZONE_Leave", positions);
                    break;
            }
            ShowCount(positions.Count);
            var camera = Camera.main;
            for (var i = 0; i < positions.Count; i++)
            {
                var arrow = arrows[i];
                arrow.position = positions[i] + Vector3.up *
                    (0.025f * Mathf.Sin(Time.time * 4.5f));
                if (camera != null)
                    arrow.rotation = Quaternion.LookRotation(
                        camera.transform.position - arrow.position, Vector3.up);
            }
        }

        private void UpdateZoneHints(ChallengeStep step)
        {
            HideZoneHints();
            switch (step)
            {
                case ChallengeStep.Approach:
                    ShowZoneHint("ZONE_Approach");
                    break;
                case ChallengeStep.TakeOut:
                    ShowZoneHint("ZONE_FreezerSource");
                    break;
                case ChallengeStep.TemporaryPlacement:
                    ShowZoneHint("ZONE_Temporary");
                    break;
                case ChallengeStep.Reorganise:
                    if (evidenceBridge.HasEmitted(step, "ITEM_Tomato") ||
                        evidenceBridge.HasEmitted(step, "ITEM_Onion")) break;
                    ShowZoneHint(AnyTargetItemHeld(step)
                        ? "ZONE_FreshDestination" : "ZONE_Temporary");
                    break;
                case ChallengeStep.PutBack:
                    ShowZoneHint("ZONE_FreshDestination");
                    break;
                case ChallengeStep.Leave:
                    ShowZoneHint("ZONE_Leave");
                    break;
            }
        }

        private bool AnyTargetItemHeld(ChallengeStep step)
        {
            var ids = step == ChallengeStep.PutBack
                ? new[] { "ITEM_Tomato", "ITEM_Onion", "ITEM_CerealBox" }
                : new[] { "ITEM_Tomato", "ITEM_Onion" };
            foreach (var id in ids)
                if (!evidenceBridge.HasEmitted(step, id) &&
                    identities.TryGetValue(id, out var item) &&
                    item.Grab != null && item.Grab.isSelected)
                    return true;
            return false;
        }

        private void HideZoneHints()
        {
            foreach (var hints in zoneHints.Values)
                foreach (var hint in hints) hint.enabled = false;
        }

        private void ShowZoneHint(string id)
        {
            if (!zoneHints.TryGetValue(id, out var hints)) return;
            foreach (var hint in hints) hint.enabled = true;
        }

        private void AddNextAccess(ChallengeStep step, List<Vector3> positions)
        {
            var ids = step == ChallengeStep.Open
                ? new[] { "ACCESS_FridgeFreezerDoor", "ACCESS_FridgeFreshDoor" }
                : new[] { "ACCESS_FridgeFreshDoor", "ACCESS_FridgeFreezerDoor" };
            foreach (var id in ids)
            {
                if (evidenceBridge.HasEmitted(step, id) || !identities.TryGetValue(id, out var access))
                    continue;
                Transform handle = null;
                foreach (var child in access.GetComponentsInChildren<Transform>())
                    if (child.name == "Handle") { handle = child; break; }
                positions.Add((handle != null ? handle.position : access.transform.position) +
                    Vector3.up * 0.13f);
                visibleTargets.Add(id);
                return;
            }
        }

        private void AddNextItem(ChallengeStep step, List<Vector3> positions)
        {
            var ids = step == ChallengeStep.PutBack
                ? new[] { "ITEM_Tomato", "ITEM_Onion", "ITEM_CerealBox" }
                : new[] { "ITEM_Tomato", "ITEM_Onion" };
            foreach (var id in ids)
            {
                if (evidenceBridge.HasEmitted(step, id) || !identities.TryGetValue(id, out var item))
                    continue;
                AddItem(id, item, positions);
                return;
            }
        }

        private void AddItem(string id, TaskEvidenceIdentity item, List<Vector3> positions)
        {
            var renderer = item.GetComponentInChildren<Renderer>();
            positions.Add(new Vector3(item.transform.position.x,
                renderer != null ? renderer.bounds.max.y + 0.12f : item.transform.position.y + 0.2f,
                item.transform.position.z));
            visibleTargets.Add(id);
        }

        private void AddNextItemOrZone(ChallengeStep step, string zoneId,
            List<Vector3> positions)
        {
            var ids = step == ChallengeStep.PutBack
                ? new[] { "ITEM_Tomato", "ITEM_Onion", "ITEM_CerealBox" }
                : new[] { "ITEM_Tomato", "ITEM_Onion" };
            if (step == ChallengeStep.Reorganise &&
                (evidenceBridge.HasEmitted(step, "ITEM_Tomato") ||
                 evidenceBridge.HasEmitted(step, "ITEM_Onion"))) return;
            foreach (var id in ids)
                if (!evidenceBridge.HasEmitted(step, id) &&
                    identities.TryGetValue(id, out var held) &&
                    held.Grab != null && held.Grab.isSelected)
                {
                    AddZone(zoneId, positions);
                    return;
                }
            foreach (var id in ids)
            {
                if (evidenceBridge.HasEmitted(step, id) || !identities.TryGetValue(id, out var item))
                    continue;
                AddItem(id, item, positions);
                return;
            }
        }

        private void AddZone(string id, List<Vector3> positions)
        {
            if (!identities.TryGetValue(id, out var zone)) return;
            var sensors = zone.GetComponents<Collider>();
            foreach (var sensor in sensors)
            {
                if (!sensor.enabled || !sensor.isTrigger) continue;
                var centre = sensor.bounds.center;
                positions.Add(centre + Vector3.up *
                    (id == "ZONE_FreshDestination" ? 0.08f :
                        id == "ZONE_Temporary" ? 0.13f : -sensor.bounds.extents.y + 0.22f));
                visibleTargets.Add(id);
            }
        }

        private void ShowCount(int count)
        {
            while (arrows.Count < count)
            {
                var arrow = new GameObject("GUIDE_Green_DownTriangle");
                arrow.transform.SetParent(transform, false);
                arrow.AddComponent<MeshFilter>().sharedMesh = mesh;
                arrow.AddComponent<MeshRenderer>().sharedMaterial = material;
                arrows.Add(arrow.transform);
            }
            for (var i = 0; i < arrows.Count; i++)
                arrows[i].gameObject.SetActive(i < count);
        }
    }
}
