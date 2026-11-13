using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.XR;
using XRApartment.Data;
using XRApartment.Data.ReviewV2;
using XRApartment.Simulation.ReviewV2;

namespace XRApartment.Runtime.ReviewV2
{
    [DefaultExecutionOrder(-100)]
    public sealed class ReviewSessionHost : MonoBehaviour, ISessionReadModel, IReviewSessionCommands, IInteractionEventSource, IWorldPort, ICalibrationPort
    {
        public KitchenCookingStation station;
        public KitchenRoomReset roomReset;
        public KitchenRecipeController recipe;
        public KitchenLocomotionSettings locomotion;
        public ReviewSceneIdentity[] identities;
        SessionController core;
        readonly Stopwatch clock = new Stopwatch();
        bool cooked, resetInFlight;
        float previousTimeScale = 1;
        bool ownsFreeze;
        public SessionSnapshot Snapshot => core.Snapshot;
        public event Action<SessionSnapshot> Changed;
        public event Action<InteractionEvent> Observed;
        public event Action<OperationResult> OperationCompleted;
        public bool CanObserve
        {
            get
            {
                if (core == null) return false;
                var s = Snapshot;
                return (s.Phase == SessionPhase.Experience || s.Phase == SessionPhase.Review) && !s.IsPaused &&
                    !s.IsBusy && !resetInFlight && !station.IsResetting && !roomReset.IsResetting;
            }
        }
        void Awake()
        {
            core = new SessionController(this);
            core.Changed += OnChanged;
            core.OperationCompleted += OnOperation;
            clock.Start();
        }
        void OnDestroy()
        {
            core.Changed -= OnChanged; core.OperationCompleted -= OnOperation;
            if (ownsFreeze) Time.timeScale = previousTimeScale;
        }
        void OnOperation(OperationResult result) => OperationCompleted?.Invoke(result);
        void OnChanged(SessionSnapshot snapshot)
        {
            bool freeze = snapshot.IsPaused || snapshot.Phase == SessionPhase.Finished || snapshot.Phase == SessionPhase.Exited;
            if (freeze && !ownsFreeze) { previousTimeScale = Time.timeScale; Time.timeScale = 0; ownsFreeze = true; }
            else if (!freeze && ownsFreeze) { Time.timeScale = previousTimeScale; ownsFreeze = false; }
            Changed?.Invoke(snapshot);
        }
        public CommandResult Execute(CommandType command, string targetSchemeId = null) => core.Execute(command, targetSchemeId);
        public Transform Resolve(string stableId)
        {
            foreach (var item in identities) if (item != null && item.Id == stableId) return item.transform;
            return null;
        }
        internal void Observe(InteractionKind kind, string objectId)
        {
            if (!CanObserve || Resolve(objectId) == null) return;
            var fact = new InteractionEvent(Snapshot, kind, objectId, clock.ElapsedMilliseconds);
            if (kind == InteractionKind.DishCooked)
                core.ObserveGoal(fact.SessionId, fact.SchemeVisitId, "G02", GoalState.Completed, fact.EventId, fact.ElapsedMilliseconds);
            Observed?.Invoke(fact);
        }
        void LateUpdate()
        {
            if (recipe == null || !CanObserve) return;
            bool ready = recipe.ActiveRecipe == KitchenRecipeChoice.TomatoEgg && recipe.Recipe.Step == TomatoEggStep.Ready;
            if (ready && !cooked) Observe(InteractionKind.DishCooked, "COOKING_STATION");
            cooked = ready;
        }
        public void Reset(string requestId, Action<string, bool, string> completed)
        {
            if (station == null || roomReset == null || !roomReset.isActiveAndEnabled)
            { completed(requestId, false, "WorldUnavailable"); return; }
            StartCoroutine(ResetWorld(requestId, completed));
        }
        IEnumerator ResetWorld(string id, Action<string, bool, string> completed)
        {
            resetInFlight = true;
            string error = null;
            try { roomReset.ResetRoom(); }
            catch (Exception ex) { error = ex.Message; }
            if (error != null) { resetInFlight = false; completed(id, false, error); yield break; }
            float deadline = Time.realtimeSinceStartup + 10f;
            yield return null;
            while (station.IsResetting || roomReset.IsResetting)
            {
                if (Time.realtimeSinceStartup > deadline)
                { resetInFlight = false; completed(id, false, "World restoration timed out; inspect scene before retrying"); yield break; }
                yield return null;
            }
            cooked = false; resetInFlight = false;
            completed(id, true, "World reset completed; feedback remains owned by its original visit");
        }
        public CommandResult Recenter()
        {
            var inputs = new List<XRInputSubsystem>(); SubsystemManager.GetInstances(inputs);
            foreach (var input in inputs) if (input.running && input.TryRecenter()) return new CommandResult(true);
            return new CommandResult(false, "XR runtime did not support recenter; use the headset system calibration");
        }
        public CommandResult Calibrate(bool seated) => locomotion != null && locomotion.CalibrateEyeHeight(seated ? 1.2f : 1.65f)
            ? new CommandResult(true) : new CommandResult(false, "CalibrationUnavailable");
    }
}
