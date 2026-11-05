using System;
using System.Collections.Generic;
using UnityEngine;
using XRApartment.Data;
using XRApartment.Simulation;

namespace XRApartment.Runtime.Core
{
    /// <summary>Swaps the benchmark slot while leaving the kitchen, XR rig and UI intact.</summary>
    public sealed class RuntimeBenchmarkAdapter : MonoBehaviour, IReviewScenePort
    {
        private const float RestClearance = .004f;

        [Serializable]
        private sealed class BenchmarkBinding
        {
            public string BenchmarkId;
            public GameObject Prefab;
        }

        private sealed class ItemPose
        {
            public TaskEvidenceIdentity Identity;
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Scale;
            public Bounds? PhysicalBounds;
        }

        [SerializeField] private Transform benchmarkSlot;
        [SerializeField] private BenchmarkBinding[] benchmarks;
        [SerializeField] private TaskEvidenceBridge evidenceBridge;
        [SerializeField] private Transform xrOrigin;
        [SerializeField] private Transform xrHead;
        [SerializeField] private Transform recenterAnchor;

        private readonly Dictionary<string, ItemPose> baseline = new Dictionary<string, ItemPose>();
        private readonly Dictionary<string, ItemPose> preparation = new Dictionary<string, ItemPose>();
        private SessionPlan plan;
        private IJourneyCore core;
        private GameObject currentInstance;
        private string currentBenchmarkId;
        private bool challengePlacementActive;

        public event Action<TaskEvidence> EvidenceObserved;
        public event Action<string> InteractionBlocked;
        public string CurrentBenchmarkId => currentBenchmarkId;
        internal GameObject CurrentInstance => currentInstance;

        private void Awake()
        {
            if (evidenceBridge != null)
            {
                evidenceBridge.EvidenceObserved += ForwardEvidence;
                evidenceBridge.InteractionBlocked += ForwardBlocked;
            }
            CaptureBaseline();
            if (benchmarkSlot == null) return;
            var marker = benchmarkSlot.GetComponentInChildren<BenchmarkSlotMarker>(true);
            if (marker != null)
            {
                currentInstance = marker.gameObject;
                currentBenchmarkId = marker.BenchmarkId;
            }
        }

        private void OnDestroy()
        {
            if (evidenceBridge != null)
            {
                evidenceBridge.EvidenceObserved -= ForwardEvidence;
                evidenceBridge.InteractionBlocked -= ForwardBlocked;
            }
        }

        private void ForwardEvidence(TaskEvidence evidence) => EvidenceObserved?.Invoke(evidence);
        private void ForwardBlocked(string message) => InteractionBlocked?.Invoke(message);

        public void Configure(SessionPlan sessionPlan, IJourneyCore journey)
        {
            plan = sessionPlan;
            core = journey;
            CapturePreparationPoses();
            RestorePreparation();
        }

        /// <summary>Restore the shared preparation items only after a saved challenge returns to Explore.</summary>
        public void SyncExplorePlacement(JourneySnapshot snapshot)
        {
            if (challengePlacementActive && snapshot != null &&
                snapshot.State == SessionState.Explore &&
                string.IsNullOrEmpty(snapshot.ActiveBenchmarkId))
            {
                RestorePreparation();
                challengePlacementActive = false;
            }
        }

        public bool HasBinding(string benchmarkId)
        {
            return FindBinding(benchmarkId) != null && benchmarkSlot != null && evidenceBridge != null;
        }

        public bool ValidatePlanWiring(SessionPlan sessionPlan, BenchmarkRegistry registry,
            out string error)
        {
            error = null;
            if (sessionPlan == null || sessionPlan.Task == null || registry == null ||
                benchmarkSlot == null || evidenceBridge == null)
            {
                error = "Scene adapter, slot or task plan is missing.";
                return false;
            }
            var sceneIdentities = new Dictionary<string, TaskEvidenceIdentity>();
            foreach (var identity in FindObjectsByType<TaskEvidenceIdentity>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (identity == null || string.IsNullOrWhiteSpace(identity.StableId) ||
                    identity.transform.IsChildOf(benchmarkSlot)) continue;
                if (sceneIdentities.ContainsKey(identity.StableId))
                {
                    error = "Duplicate scene task ID: " + identity.StableId;
                    return false;
                }
                sceneIdentities.Add(identity.StableId, identity);
            }
            foreach (var item in sessionPlan.Task.Items)
            {
                TaskEvidenceIdentity identity;
                if (!sceneIdentities.TryGetValue(item.ItemId, out identity) ||
                    !identity.IsTaskItem || identity.Grab == null)
                {
                    error = "Shared task item is missing: " + item.ItemId;
                    return false;
                }
            }
            if (FindPreparationPlate() == null)
            {
                error = "Preparation plate is missing from SET_Preparation.";
                return false;
            }
            foreach (var benchmarkId in sessionPlan.BenchmarkIds)
            {
                var binding = FindBinding(benchmarkId);
                if (binding == null)
                {
                    error = "Benchmark prefab is missing: " + benchmarkId;
                    return false;
                }
                var definition = registry.Find(benchmarkId);
                var marker = binding.Prefab.GetComponentInChildren<BenchmarkSlotMarker>(true);
                if (definition == null || marker == null || marker.BenchmarkId != benchmarkId ||
                    marker.Cabinet != definition.Cabinet || marker.Fridge != definition.Fridge)
                {
                    error = "Benchmark prefab marker does not match the registry: " + benchmarkId;
                    return false;
                }
                var combined = new Dictionary<string, TaskEvidenceIdentity>(sceneIdentities);
                foreach (var identity in binding.Prefab.GetComponentsInChildren<TaskEvidenceIdentity>(true))
                {
                    if (identity == null || string.IsNullOrWhiteSpace(identity.StableId)) continue;
                    if (combined.ContainsKey(identity.StableId))
                    {
                        error = "Duplicate task ID in " + benchmarkId + ": " + identity.StableId;
                        return false;
                    }
                    combined.Add(identity.StableId, identity);
                }
                foreach (var target in sessionPlan.Task.AccessTargets)
                {
                    TaskEvidenceIdentity identity;
                    if (!combined.TryGetValue(target.ObjectId, out identity) ||
                        identity.Role != target.Role || !identity.TryGetAccessProgress(out _))
                    {
                        error = "Benchmark " + benchmarkId + " has no moving access target: " +
                            target.ObjectId;
                        return false;
                    }
                }
                if (!HasSceneZone(combined, sessionPlan.Task.ApproachZoneId,
                        ReviewRole.CirculationZone) ||
                    !HasSceneZone(combined, sessionPlan.Task.LeaveZoneId,
                        ReviewRole.CirculationZone))
                {
                    error = "Benchmark " + benchmarkId + " has no approach or leave zone sensor.";
                    return false;
                }
                foreach (var item in sessionPlan.Task.Items)
                {
                    if (!HasSceneZone(combined, item.ReturnZoneId, ReviewRole.StorageZone) ||
                        (item.IsExistingItem &&
                         (!HasSceneZone(combined, item.SourceZoneId, ReviewRole.StorageZone) ||
                          !HasSceneZone(combined, item.TemporaryZoneId,
                              ReviewRole.CounterSurface))))
                    {
                        error = "Benchmark " + benchmarkId + " has no zone for item: " + item.ItemId;
                        return false;
                    }
                }
            }
            return true;
        }

        private static bool HasSceneZone(Dictionary<string, TaskEvidenceIdentity> identities,
            string id, ReviewRole role)
        {
            TaskEvidenceIdentity identity;
            return !string.IsNullOrWhiteSpace(id) && identities.TryGetValue(id, out identity) &&
                identity.Role == role && identity.Sensor != null;
        }

        public void Execute(JourneyAction action, Action<SceneActionResult> completed)
        {
            if (completed == null) return;
            if (action == null || plan == null || core == null)
            {
                completed(Failure(action, "Scene adapter has no action or session plan."));
                return;
            }
            SceneActionResult response;
            try
            {
                string error = null;
                switch (action.Type)
                {
                    case JourneyActionType.LoadBenchmark:
                    case JourneyActionType.ResetTask:
                    case JourneyActionType.ReplayBenchmark:
                        Load(action.BenchmarkId, action.RunId, out error);
                        break;
                    case JourneyActionType.ReturnItem:
                        ReturnItem(action.ItemId, out error);
                        break;
                    case JourneyActionType.RestoreCurrentStep:
                        RestoreStep(action.Step, out error);
                        break;
                    case JourneyActionType.Recenter:
                        if (xrOrigin == null || xrHead == null || recenterAnchor == null)
                            error = "XR rig or recenter anchor is missing.";
                        else
                        {
                            var offset = recenterAnchor.position - xrHead.position;
                            offset.y = 0f;
                            xrOrigin.position += offset;
                        }
                        break;
                    default:
                        error = "Unsupported scene action.";
                        break;
                }
                response = error == null ? new SceneActionResult
                {
                    RequestId = action.RequestId,
                    Succeeded = true,
                    ActualBenchmarkId = currentBenchmarkId,
                    ErrorCode = JourneyErrorCode.None
                } : Failure(action, error);
            }
            catch (Exception exception)
            {
                response = Failure(action, "Scene action exception: " + exception.Message);
            }
            completed(response);
        }

        private bool Load(string benchmarkId, long runId, out string error)
        {
            error = null;
            var binding = FindBinding(benchmarkId);
            if (binding == null || benchmarkSlot == null || evidenceBridge == null)
            {
                error = "Benchmark prefab, slot or evidence bridge is missing: " + benchmarkId;
                return false;
            }
            var previous = currentInstance;
            var previousId = currentBenchmarkId;
            var previousPoses = CaptureCurrentPoses();
            var previousPlacement = challengePlacementActive;
            GameObject replacement = null;
            try
            {
                if (previous != null) previous.SetActive(false);
                replacement = Instantiate(binding.Prefab, benchmarkSlot, false);
                replacement.name = binding.Prefab.name;
                replacement.SetActive(true);
                var marker = replacement.GetComponentInChildren<BenchmarkSlotMarker>(true);
                if (marker == null || marker.BenchmarkId != benchmarkId)
                    throw new InvalidOperationException("Loaded prefab reports a different benchmark ID.");
                if (runId == 0) RestorePreparation();
                else RestoreBaseline();
                if (!evidenceBridge.Bind(plan, core, benchmarkId, runId))
                    throw new InvalidOperationException(evidenceBridge.LastError);

                currentInstance = replacement;
                currentBenchmarkId = marker.BenchmarkId;
                challengePlacementActive = runId != 0;
                if (previous != null) Destroy(previous);
                return true;
            }
            catch (Exception exception)
            {
                if (replacement != null)
                {
                    replacement.SetActive(false);
                    Destroy(replacement);
                }
                RestorePoses(previousPoses);
                if (previous != null) previous.SetActive(true);
                currentInstance = previous;
                currentBenchmarkId = previousId;
                challengePlacementActive = previousPlacement;
                if (previous != null && !string.IsNullOrEmpty(previousId))
                    evidenceBridge.Bind(plan, core, previousId, core.Snapshot.ActiveRunId);
                else evidenceBridge.Unbind();
                error = "Benchmark load failed and previous scene was restored: " + exception.Message;
                return false;
            }
        }

        private bool ReturnItem(string itemId, out string error)
        {
            error = null;
            ItemPose pose;
            if (string.IsNullOrWhiteSpace(itemId) || !baseline.TryGetValue(itemId, out pose))
            {
                error = "Unknown task item: " + itemId;
                return false;
            }
            if (pose.Identity.Grab != null && pose.Identity.Grab.isSelected)
            {
                error = "Release the item before returning it to the tray.";
                return false;
            }
            RestorePose(pose);
            evidenceBridge?.ItemReturnedToTray(itemId);
            return true;
        }

        private bool RestoreStep(ChallengeStep? requestedStep, out string error)
        {
            error = null;
            var snapshot = core.Snapshot;
            if (!requestedStep.HasValue || snapshot.State != SessionState.ChallengeActive ||
                snapshot.CurrentStep != requestedStep || snapshot.ActiveBenchmarkId != currentBenchmarkId)
            {
                error = "Restore request no longer matches the active step.";
                return false;
            }
            var step = requestedStep.Value;
            foreach (var spec in plan.Task.Items)
            {
                if (!baseline.TryGetValue(spec.ItemId, out var pose) || pose.Identity == null)
                {
                    error = "Task item is missing: " + spec.ItemId;
                    return false;
                }
                if (pose.Identity.Grab != null && pose.Identity.Grab.isSelected)
                {
                    error = "Release all task items before restoring this step.";
                    return false;
                }
            }
            var access = new List<TaskEvidenceIdentity>();
            foreach (var target in plan.Task.AccessTargets)
            {
                var identity = FindIdentity(target.ObjectId);
                if (identity == null || (identity.GetComponent<KinematicSwingDoor>() == null &&
                    identity.GetComponent<KinematicLinearDrawer>() == null))
                {
                    error = "Access target is missing: " + target.ObjectId;
                    return false;
                }
                access.Add(identity);
            }
            var destinations = new List<ItemPose>();
            var byZone = new Dictionary<string, List<ItemPose>>(StringComparer.Ordinal);
            var restoreToStorage = step == ChallengeStep.Close || step == ChallengeStep.Leave;
            var restoreToCounter = step == ChallengeStep.Reorganise || step == ChallengeStep.PutBack;
            foreach (var spec in plan.Task.Items)
            {
                var pose = baseline[spec.ItemId];
                if (!restoreToStorage && !(restoreToCounter && spec.IsExistingItem))
                {
                    destinations.Add(pose);
                    continue;
                }
                var zoneId = restoreToStorage ? spec.ReturnZoneId : spec.TemporaryZoneId;
                if (!byZone.TryGetValue(zoneId, out var placements))
                {
                    placements = new List<ItemPose>();
                    byZone.Add(zoneId, placements);
                }
                placements.Add(pose);
            }
            Physics.SyncTransforms();
            foreach (var zone in byZone)
                if (!TryPlanZonePlacements(zone.Key, zone.Value, destinations, out error)) return false;
            foreach (var pose in destinations) RestorePose(pose);
            var shouldOpen = step >= ChallengeStep.TakeOut && step <= ChallengeStep.Close;
            foreach (var identity in access)
            {
                var swing = identity.GetComponent<KinematicSwingDoor>();
                if (swing != null) swing.SetOpenImmediate(shouldOpen);
                var drawer = identity.GetComponent<KinematicLinearDrawer>();
                if (drawer != null) drawer.SetOpenImmediate(shouldOpen);
            }
            Physics.SyncTransforms();
            evidenceBridge.ResetStepObservation(step);
            return true;
        }

        private static bool TryPlanZonePlacements(string zoneId, List<ItemPose> items,
            List<ItemPose> destinations, out string error)
        {
            error = null;
            var zone = FindIdentity(zoneId);
            var sensors = new List<Collider>();
            if (zone != null)
                foreach (var sensor in zone.GetComponents<Collider>())
                    if (sensor.enabled && sensor.isTrigger) sensors.Add(sensor);
            if (sensors.Count == 0)
            {
                error = "Step recovery zone is missing: " + zoneId;
                return false;
            }

            for (var index = 0; index < items.Count; index++)
            {
                var sensorIndex = index % sensors.Count;
                var slot = index / sensors.Count;
                var slotCount = (items.Count - 1 - sensorIndex) / sensors.Count + 1;
                var sensor = sensors[sensorIndex];
                var volume = sensor.bounds;
                var source = items[index];
                var width = volume.size.x / slotCount;
                var centre = volume.center;
                centre.x = volume.min.x + width * (slot + .5f);
                if (!source.PhysicalBounds.HasValue || source.PhysicalBounds.Value.size.x + RestClearance > width ||
                    source.PhysicalBounds.Value.size.z + RestClearance > volume.size.z ||
                    !TryFindSupport(centre, volume.min.y, out var supportY))
                {
                    error = "No supported recovery position for " + source.Identity.StableId + " in " + zoneId + ".";
                    return false;
                }

                var physical = source.PhysicalBounds.Value;
                centre.y = supportY + physical.extents.y + RestClearance;
                if (centre.y + physical.extents.y > volume.max.y ||
                    (sensor.ClosestPoint(centre) - centre).sqrMagnitude > .000001f)
                {
                    error = "The recovery position does not fit " + source.Identity.StableId + " in " + zoneId + ".";
                    return false;
                }
                destinations.Add(new ItemPose
                {
                    Identity = source.Identity,
                    Position = source.Position + (centre - physical.center),
                    Rotation = source.Rotation,
                    Scale = source.Scale
                });
            }
            return true;
        }

        private static bool TryFindSupport(Vector3 centre, float zoneBottom, out float height)
        {
            height = float.NegativeInfinity;
            foreach (var hit in Physics.RaycastAll(centre, Vector3.down,
                centre.y - zoneBottom + RestClearance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                if (hit.collider.attachedRigidbody == null && hit.normal.y > .9f)
                    height = Mathf.Max(height, hit.point.y);
            return !float.IsNegativeInfinity(height);
        }

        private static TaskEvidenceIdentity FindIdentity(string stableId)
        {
            foreach (var identity in FindObjectsByType<TaskEvidenceIdentity>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (identity.StableId == stableId) return identity;
            return null;
        }

        private BenchmarkBinding FindBinding(string benchmarkId)
        {
            if (benchmarks == null) return null;
            foreach (var binding in benchmarks)
                if (binding != null && binding.BenchmarkId == benchmarkId && binding.Prefab != null)
                    return binding;
            return null;
        }

        private void CaptureBaseline()
        {
            baseline.Clear();
            foreach (var identity in FindObjectsByType<TaskEvidenceIdentity>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (identity == null || !identity.IsTaskItem ||
                    string.IsNullOrWhiteSpace(identity.StableId) ||
                    (benchmarkSlot != null && identity.transform.IsChildOf(benchmarkSlot))) continue;
                if (!baseline.ContainsKey(identity.StableId))
                    baseline.Add(identity.StableId, CapturePose(identity));
            }
        }

        private static Transform FindPreparationPlate()
        {
            var preparationSet = GameObject.Find("SET_Preparation");
            return preparationSet == null ? null :
                preparationSet.transform.Find("PREF_Prop_Plate");
        }

        private void CapturePreparationPoses()
        {
            preparation.Clear();
            var plate = FindPreparationPlate();
            if (plate == null || plan?.Task?.Items == null) return;
            var existing = new List<ItemPose>();
            foreach (var item in plan.Task.Items)
            {
                if (!item.IsExistingItem) continue;
                if (baseline.TryGetValue(item.ItemId, out var pose)) existing.Add(pose);
            }
            for (var index = 0; index < existing.Count; index++)
            {
                var source = existing[index];
                preparation.Add(source.Identity.StableId, new ItemPose
                {
                    Identity = source.Identity,
                    Position = plate.TransformPoint(new Vector3(
                        (index - (existing.Count - 1) * .5f) * .105f, .013f, 0f)),
                    Rotation = source.Rotation,
                    Scale = source.Scale
                });
            }
        }

        private void RestorePreparation()
        {
            foreach (var pose in preparation.Values) RestorePose(pose);
            Physics.SyncTransforms();
        }

        private Dictionary<string, ItemPose> CaptureCurrentPoses()
        {
            var poses = new Dictionary<string, ItemPose>();
            foreach (var pair in baseline)
                if (pair.Value.Identity != null)
                    poses.Add(pair.Key, CapturePose(pair.Value.Identity));
            return poses;
        }

        private static ItemPose CapturePose(TaskEvidenceIdentity identity)
        {
            Bounds? physicalBounds = null;
            if (identity.Grab != null)
                foreach (var collider in identity.Grab.colliders)
                    if (collider != null && collider.enabled && !collider.isTrigger)
                    {
                        var bounds = physicalBounds ?? collider.bounds;
                        bounds.Encapsulate(collider.bounds);
                        physicalBounds = bounds;
                    }
            return new ItemPose
            {
                Identity = identity,
                Position = identity.transform.position,
                Rotation = identity.transform.rotation,
                Scale = identity.transform.localScale,
                PhysicalBounds = physicalBounds
            };
        }

        private void RestoreBaseline()
        {
            foreach (var pose in baseline.Values) RestorePose(pose);
            Physics.SyncTransforms();
        }

        private static void RestorePoses(Dictionary<string, ItemPose> poses)
        {
            foreach (var pose in poses.Values) RestorePose(pose);
        }

        private static void RestorePose(ItemPose pose)
        {
            if (pose.Identity == null) return;
            var body = pose.Identity.Body;
            if (body != null && !body.isKinematic)
            {
                body.velocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            pose.Identity.transform.SetPositionAndRotation(pose.Position, pose.Rotation);
            pose.Identity.transform.localScale = pose.Scale;
            if (pose.Identity.Grab != null) pose.Identity.Grab.SetTargetPose(new Pose(pose.Position, pose.Rotation));
        }

        private SceneActionResult Failure(JourneyAction action, string message)
        {
            return new SceneActionResult
            {
                RequestId = action == null ? 0 : action.RequestId,
                Succeeded = false,
                ActualBenchmarkId = currentBenchmarkId,
                ErrorCode = JourneyErrorCode.OperationFailed,
                Message = message
            };
        }
    }
}
