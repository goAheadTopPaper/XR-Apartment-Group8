using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using XRApartment.Data;
using XRApartment.Simulation;

namespace XRApartment.Runtime.Core
{
    /// <summary>Observes physical rig, access and item state; it never advances the journey itself.</summary>
    public sealed class TaskEvidenceBridge : MonoBehaviour
    {
        [SerializeField] private Transform rigHead;
        [SerializeField, Min(0.05f)] private float placementDwellSeconds = 0.2f;

        private readonly Dictionary<string, TaskEvidenceIdentity> identities =
            new Dictionary<string, TaskEvidenceIdentity>();
        private readonly Dictionary<string, bool> sourceWasInside = new Dictionary<string, bool>();
        private readonly Dictionary<string, float> previousProgress = new Dictionary<string, float>();
        private readonly Dictionary<string, float> dwell = new Dictionary<string, float>();
        private readonly HashSet<string> taken = new HashSet<string>();
        private readonly HashSet<string> placed = new HashSet<string>();
        private readonly HashSet<string> emitted = new HashSet<string>();
        private readonly Dictionary<string, XRSelectFilterDelegate> grabGuards =
            new Dictionary<string, XRSelectFilterDelegate>();
        private readonly Dictionary<string, Collider> itemColliders =
            new Dictionary<string, Collider>();

        private IJourneyCore core;
        private TaskConfig task;
        private string sessionId;
        private string benchmarkId;
        private long runId;
        private float sessionStartedAt = -1f;

        public event Action<TaskEvidence> EvidenceObserved;
        public event Action<string> InteractionBlocked;
        public string LastError { get; private set; }

        public bool HasEmitted(ChallengeStep step, string itemOrTargetId) =>
            emitted.Contains(step + ":" + itemOrTargetId);

        public bool Bind(SessionPlan plan, IJourneyCore journey, string benchmark, long run)
        {
            Unbind();
            if (plan == null || journey == null || plan.Task == null || rigHead == null)
            {
                LastError = "Plan, journey or XR head is missing.";
                return false;
            }
            foreach (var identity in FindObjectsByType<TaskEvidenceIdentity>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (identity == null || string.IsNullOrWhiteSpace(identity.StableId)) continue;
                if (identities.ContainsKey(identity.StableId))
                {
                    LastError = "Duplicate task identity: " + identity.StableId;
                    return false;
                }
                identities.Add(identity.StableId, identity);
            }
            foreach (var access in plan.Task.AccessTargets)
            {
                TaskEvidenceIdentity identity;
                if (!identities.TryGetValue(access.ObjectId, out identity) ||
                    identity.Role != access.Role || !identity.TryGetAccessProgress(out var progress))
                {
                    LastError = "Missing moving access target: " + access.ObjectId;
                    return false;
                }
                previousProgress[access.ObjectId] = progress;
            }
            if (!HasZone(plan.Task.ApproachZoneId, ReviewRole.CirculationZone) ||
                !HasZone(plan.Task.LeaveZoneId, ReviewRole.CirculationZone))
            {
                LastError = "Approach or leave zone is missing.";
                return false;
            }
            foreach (var item in plan.Task.Items)
            {
                TaskEvidenceIdentity identity;
                if (!identities.TryGetValue(item.ItemId, out identity) || !identity.IsTaskItem ||
                    identity.Grab == null ||
                    !HasZone(item.ReturnZoneId, ReviewRole.StorageZone) ||
                    (item.IsExistingItem &&
                     (!HasZone(item.SourceZoneId, ReviewRole.StorageZone) ||
                      !HasZone(item.TemporaryZoneId, ReviewRole.CounterSurface))))
                {
                    LastError = "Task item or zone is missing: " + item.ItemId;
                    return false;
                }
                if (item.IsExistingItem)
                    sourceWasInside[item.ItemId] = Inside(item.SourceZoneId, identity.transform.position);
            }
            task = plan.Task;
            core = journey;
            sessionId = plan.SessionId;
            benchmarkId = benchmark;
            runId = run;
            foreach (var item in task.Items)
            {
                var itemId = item.ItemId;
                foreach (var collider in identities[itemId].GetComponentsInChildren<Collider>())
                    if (collider.enabled && !collider.isTrigger)
                    {
                        itemColliders[itemId] = collider;
                        break;
                    }
                var guard = new XRSelectFilterDelegate((_, __) => CanGrabTaskItem(itemId));
                identities[itemId].Grab.selectFilters.Add(guard);
                grabGuards.Add(itemId, guard);
            }
            if (sessionStartedAt < 0f) sessionStartedAt = Time.realtimeSinceStartup;
            LastError = null;
            return true;
        }

        public void Unbind()
        {
            foreach (var pair in grabGuards)
                if (identities.TryGetValue(pair.Key, out var identity) && identity?.Grab != null)
                    identity.Grab.selectFilters.Remove(pair.Value);
            grabGuards.Clear();
            itemColliders.Clear();
            core = null;
            task = null;
            identities.Clear();
            sourceWasInside.Clear();
            previousProgress.Clear();
            dwell.Clear();
            taken.Clear();
            placed.Clear();
            emitted.Clear();
        }

        private bool CanGrabTaskItem(string itemId)
        {
            if (core == null) return true;
            var snapshot = core.Snapshot;
            if (snapshot.State != SessionState.ChallengeActive) return true;
            if (!snapshot.IsBusy && snapshot.CurrentStep != ChallengeStep.Approach &&
                snapshot.CurrentStep != ChallengeStep.Open) return true;
            InteractionBlocked?.Invoke(snapshot.IsBusy
                ? "Wait for the current scene action before grabbing task items."
                : "Open both fridge doors before grabbing task items (" + itemId + ").");
            return false;
        }

        public void ResetStepObservation(ChallengeStep step)
        {
            var prefix = step + ":";
            emitted.RemoveWhere(key => key.StartsWith(prefix, StringComparison.Ordinal));
            dwell.Clear();
            if (step == ChallengeStep.TakeOut) taken.Clear();
            if (step == ChallengeStep.TemporaryPlacement) placed.Clear();
            foreach (var item in task.Items)
                if (item.IsExistingItem && identities.TryGetValue(item.ItemId, out var identity))
                    sourceWasInside[item.ItemId] = Inside(item.SourceZoneId, identity.transform.position);
            if (step == ChallengeStep.TakeOut)
                foreach (var item in task.Items)
                    if (item.IsExistingItem)
                        Debug.Log("[DEBUG-b01] reset " + item.ItemId + " inside=" + sourceWasInside[item.ItemId] +
                            " position=" + identities[item.ItemId].transform.position +
                            " bodyPosition=" + identities[item.ItemId].Body.position +
                            " zone=" + identities[item.SourceZoneId].Sensor.bounds);
            foreach (var access in task.AccessTargets)
                if (identities.TryGetValue(access.ObjectId, out var identity) &&
                    identity.TryGetAccessProgress(out var progress))
                    previousProgress[access.ObjectId] = progress;
        }

        public void ItemReturnedToTray(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId)) return;
            emitted.Remove(ChallengeStep.PutBack + ":" + itemId);
            dwell.Remove(ChallengeStep.PutBack + ":" + itemId);
        }

        private bool HasZone(string id, ReviewRole role)
        {
            TaskEvidenceIdentity identity;
            return !string.IsNullOrWhiteSpace(id) && identities.TryGetValue(id, out identity) &&
                identity.Role == role && identity.Sensor != null;
        }

        private bool Inside(string zoneId, Vector3 point)
        {
            TaskEvidenceIdentity zone;
            return identities.TryGetValue(zoneId, out zone) && zone.ContainsPoint(point);
        }

        private void Update()
        {
            if (core == null || task == null) return;
            var snapshot = core.Snapshot;
            var active = snapshot.State == SessionState.ChallengeActive && !snapshot.IsBusy &&
                snapshot.ActiveBenchmarkId == benchmarkId && snapshot.ActiveRunId == runId &&
                snapshot.CurrentStep.HasValue;

            foreach (var access in task.AccessTargets)
            {
                TaskEvidenceIdentity identity;
                float progress;
                if (!identities.TryGetValue(access.ObjectId, out identity) ||
                    !identity.TryGetAccessProgress(out progress)) continue;
                float previous;
                previousProgress.TryGetValue(access.ObjectId, out previous);
                if (active && snapshot.CurrentStep == ChallengeStep.Open &&
                    previous < task.OpenProgressThreshold && progress >= task.OpenProgressThreshold)
                    Emit(ChallengeStep.Open, access.ObjectId, access.Role,
                        access.ObjectId, null, progress, false);
                if (active && snapshot.CurrentStep == ChallengeStep.Close &&
                    previous > task.ClosedProgressThreshold && progress <= task.ClosedProgressThreshold)
                    Emit(ChallengeStep.Close, access.ObjectId, access.Role,
                        access.ObjectId, null, progress, false);
                previousProgress[access.ObjectId] = progress;
            }

            foreach (var item in task.Items)
            {
                if (!item.IsExistingItem) continue;
                var identity = identities[item.ItemId];
                var inside = Inside(item.SourceZoneId, identity.transform.position);
                bool wasInside;
                if (active && snapshot.CurrentStep == ChallengeStep.TakeOut &&
                    sourceWasInside.TryGetValue(item.ItemId, out wasInside) && wasInside != inside)
                    Debug.Log("[DEBUG-b01] edge " + item.ItemId + " was=" + wasInside + " now=" + inside +
                        " position=" + identity.transform.position + " zone=" + identities[item.SourceZoneId].Sensor.bounds +
                        " bodyPosition=" + identity.Body.position + " kinetic=" + identity.Body.isKinematic + " selected=" + identity.Grab.isSelected +
                        " parent=" + identity.transform.parent.name + " run=" + runId);
                if (active && snapshot.CurrentStep == ChallengeStep.TakeOut &&
                    sourceWasInside.TryGetValue(item.ItemId, out wasInside) && wasInside && !inside)
                {
                    taken.Add(item.ItemId);
                    Emit(ChallengeStep.TakeOut, item.ItemId, ReviewRole.StorageZone,
                        null, item.SourceZoneId, 0f, false);
                }
                sourceWasInside[item.ItemId] = inside;
            }

            if (!active) return;

            switch (snapshot.CurrentStep.Value)
            {
                case ChallengeStep.Approach:
                    if (Inside(task.ApproachZoneId, rigHead.position))
                        Emit(ChallengeStep.Approach, task.ApproachZoneId,
                            ReviewRole.CirculationZone, null, null, 0f, false);
                    break;
                case ChallengeStep.TemporaryPlacement:
                    foreach (var item in task.Items)
                    {
                        if (!item.IsExistingItem || !taken.Contains(item.ItemId)) continue;
                        var identity = identities[item.ItemId];
                        if (Inside(item.TemporaryZoneId, identity.transform.position) &&
                            !identity.Grab.isSelected)
                        {
                            float seconds;
                            dwell.TryGetValue(item.ItemId, out seconds);
                            seconds += Time.deltaTime;
                            dwell[item.ItemId] = seconds;
                            if (seconds >= placementDwellSeconds)
                            {
                                placed.Add(item.ItemId);
                                Emit(ChallengeStep.TemporaryPlacement, item.ItemId,
                                    ReviewRole.CounterSurface, null, item.TemporaryZoneId, 0f, true);
                            }
                        }
                        else dwell[item.ItemId] = 0f;
                    }
                    break;
                case ChallengeStep.Reorganise:
                    foreach (var item in task.Items)
                    {
                        if (!item.IsExistingItem || !placed.Contains(item.ItemId)) continue;
                        var identity = identities[item.ItemId];
                        ObserveStoragePlacement(item, identity, ChallengeStep.Reorganise);
                    }
                    break;
                case ChallengeStep.PutBack:
                    foreach (var item in task.Items)
                    {
                        var identity = identities[item.ItemId];
                        ObserveStoragePlacement(item, identity, ChallengeStep.PutBack);
                    }
                    break;
                case ChallengeStep.Leave:
                    if (!Inside(task.ApproachZoneId, rigHead.position) &&
                        Inside(task.LeaveZoneId, rigHead.position))
                        Emit(ChallengeStep.Leave, task.LeaveZoneId, ReviewRole.CirculationZone,
                            null, task.LeaveZoneId, 0f, false);
                    break;
            }
        }

        private void ObserveStoragePlacement(TaskItemSpec item, TaskEvidenceIdentity identity,
            ChallengeStep step)
        {
            var key = step + ":" + item.ItemId;
            if (Inside(item.ReturnZoneId, PhysicalItemCenter(identity)) && !identity.Grab.isSelected)
            {
                float seconds;
                dwell.TryGetValue(key, out seconds);
                seconds += Time.deltaTime;
                dwell[key] = seconds;
                if (seconds >= placementDwellSeconds)
                    Emit(step, item.ItemId, ReviewRole.StorageZone,
                        null, item.ReturnZoneId, 0f, true);
            }
            else dwell[key] = 0f;
        }

        private Vector3 PhysicalItemCenter(TaskEvidenceIdentity identity)
        {
            if (itemColliders.TryGetValue(identity.StableId, out var collider) && collider != null &&
                collider.enabled) return collider.bounds.center;
            return identity.transform.position;
        }

        private void Emit(ChallengeStep step, string key, ReviewRole role,
            string objectId, string zoneId, float measurement, bool released)
        {
            var emissionKey = step + ":" + key;
            if (!emitted.Add(emissionKey)) return;
            EvidenceObserved?.Invoke(new TaskEvidence
            {
                SessionId = sessionId, BenchmarkId = benchmarkId, RunId = runId,
                Step = step, Role = role, ObjectId = objectId,
                ItemId = role == ReviewRole.CirculationZone || objectId != null ? null : key,
                ZoneId = zoneId ?? (role == ReviewRole.CirculationZone ? key : null),
                OccurredAtSessionSeconds = Time.realtimeSinceStartup - sessionStartedAt,
                Measurement = measurement, Released = released
            });
        }
    }
}
