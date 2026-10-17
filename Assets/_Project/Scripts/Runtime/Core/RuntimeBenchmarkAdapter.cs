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
                        else xrOrigin.position += recenterAnchor.position - xrHead.position;
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
            var items = new List<ItemPose>();
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
                items.Add(pose);
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
            var destinations = new Dictionary<string, ItemPose>();
            var existingIndex = 0;
            var freshIndex = 0;
            foreach (var spec in plan.Task.Items)
            {
                var pose = baseline[spec.ItemId];
                var placement = pose;
                if (spec.IsExistingItem)
                {
                    if (step == ChallengeStep.Reorganise || step == ChallengeStep.PutBack)
                    {
                        if (!TryZonePose(spec.TemporaryZoneId, pose, existingIndex,
                            out placement, out error)) return false;
                    }
                    else if (step == ChallengeStep.Close || step == ChallengeStep.Leave)
                    {
                        if (!TryZonePose(spec.ReturnZoneId, pose, freshIndex++,
                            out placement, out error)) return false;
                    }
                    existingIndex++;
                }
                else if (step == ChallengeStep.Close || step == ChallengeStep.Leave)
                {
                    if (!TryZonePose(spec.ReturnZoneId, pose, freshIndex++,
                        out placement, out error)) return false;
                }
                destinations.Add(spec.ItemId, placement);
            }
            foreach (var pose in destinations.Values) RestorePose(pose);
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

        private static bool TryZonePose(string zoneId, ItemPose baselinePose, int index,
            out ItemPose result, out string error)
        {
            result = null;
            error = null;
            var zone = FindIdentity(zoneId);
            if (zone == null || zone.Sensor == null)
            {
                error = "Step recovery zone is missing: " + zoneId;
                return false;
            }
            var bounds = zone.Sensor.bounds;
            var offset = (index % 3 - 1) * Mathf.Min(.07f, bounds.extents.x * .45f);
            result = new ItemPose
            {
                Identity = baselinePose.Identity,
                Position = bounds.center + new Vector3(offset, 0f, 0f),
                Rotation = baselinePose.Rotation,
                Scale = baselinePose.Scale
            };
            return true;
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
            return new ItemPose
            {
                Identity = identity,
                Position = identity.transform.position,
                Rotation = identity.transform.rotation,
                Scale = identity.transform.localScale
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
