using System.Collections.Generic;
using System.Linq;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace XRApartment.Runtime
{
    /// Extends the cooking reset to the authored room in the same scene.
    /// Player tracking, other loaded scenes and the formal journey are outside this scope.
    [DefaultExecutionOrder(200)]
    public sealed class KitchenRoomReset : MonoBehaviour
    {
        [SerializeField] KitchenCookingStation station;
        sealed class Actor
        {
            public Transform transform, parent;
            public Vector3 position, scale;
            public Quaternion rotation;
            public Rigidbody body;
            public bool active, kinematic, gravity;
            public RigidbodyInterpolation interpolation;
        }
        Actor[] actors;
        XRGrabInteractable[] grabs;
        bool[] enabledGrabs, throwGrabs;
        WaterTap[] taps;
        KitchenWaterSystem[] waterWorlds;
        LiquidContainer[] containers;
        LiquidStream[] streams;
        PreparedKitchenProp[] prepared;
        CookableIngredient[] ingredients;
        KitchenEgg[] eggs;
        KinematicSwingDoor[] doors;
        KinematicLinearDrawer[] drawers;
        bool[] openDoors, openDrawers;
        ParticleSystem[] effects;
        AudioSource[] audioEffects;
        int restoreFrame = -1;
        public int LoosePropCount => actors == null ? 0 : actors.Length;
        public bool IsResetting => restoreFrame >= 0;

        public void Configure(KitchenCookingStation cookingStation) => station = cookingStation;

        T[] InScene<T>() where T : Component
        {
            var found = new List<T>();
            foreach (var root in gameObject.scene.GetRootGameObjects()) found.AddRange(root.GetComponentsInChildren<T>(true));
            return found.Where(c => c.GetComponentInParent<XROrigin>(true) == null).ToArray();
        }
        bool OutsideStation(Component component) => !component.transform.IsChildOf(station.transform);

        void Awake()
        {
            if (station == null || station.gameObject.scene != gameObject.scene) { enabled = false; return; }
            grabs = InScene<XRGrabInteractable>().Where(OutsideStation).ToArray();
            enabledGrabs = grabs.Select(g => g.enabled).ToArray();
            throwGrabs = grabs.Select(g => g.throwOnDetach).ToArray();
            actors = grabs.Where(g => g.transform.parent == null || g.transform.parent.GetComponentInParent<XRGrabInteractable>(true) == null)
                .Select(g =>
                {
                    var t = g.transform; var body = g.GetComponent<Rigidbody>();
                    return new Actor { transform = t, parent = t.parent, position = t.localPosition, rotation = t.localRotation,
                        scale = t.localScale, active = g.gameObject.activeSelf, body = body,
                        kinematic = body != null && body.isKinematic, gravity = body != null && body.useGravity,
                        interpolation = body == null ? RigidbodyInterpolation.None : body.interpolation };
                }).ToArray();
            taps = InScene<WaterTap>(); waterWorlds = InScene<KitchenWaterSystem>(); containers = InScene<LiquidContainer>(); streams = InScene<LiquidStream>();
            prepared = InScene<PreparedKitchenProp>().Where(OutsideStation).ToArray();
            ingredients = InScene<CookableIngredient>().Where(OutsideStation).ToArray(); eggs = InScene<KitchenEgg>().Where(OutsideStation).ToArray();
            doors = InScene<KinematicSwingDoor>().Where(OutsideStation).ToArray(); openDoors = doors.Select(d => d.IsOpen).ToArray();
            drawers = InScene<KinematicLinearDrawer>().Where(OutsideStation).ToArray(); openDrawers = drawers.Select(d => d.IsOpen).ToArray();
            effects = InScene<ParticleSystem>(); audioEffects = InScene<AudioSource>();
        }
        void OnEnable() { if (station != null) station.ResetStarting += ResetRemainder; }
        void OnDisable()
        {
            if (station != null) station.ResetStarting -= ResetRemainder;
            RestoreInteraction();
        }
        public void ResetRoom() { if (station != null && isActiveAndEnabled) station.ResetStation(); }

        void ResetRemainder()
        {
            restoreFrame = Time.frameCount + 2;
            foreach (var grab in grabs) if (grab != null) grab.enabled = false;
            foreach (var grab in grabs) if (grab != null) grab.throwOnDetach = false;
            foreach (var prop in prepared) if (prop != null) prop.ResetProp();
            foreach (var food in ingredients) if (food != null) food.ResetCooking();
            foreach (var egg in eggs) if (egg != null) egg.ResetEgg();
            foreach (var actor in actors)
            {
                if (actor.transform == null) continue;
                if (actor.body != null) actor.body.interpolation = RigidbodyInterpolation.None;
                actor.transform.SetParent(actor.parent, false);
                actor.transform.SetLocalPositionAndRotation(actor.position, actor.rotation); actor.transform.localScale = actor.scale;
                actor.transform.gameObject.SetActive(actor.active);
                SynchronizeBody(actor);
            }
            foreach (var grab in grabs) if (grab != null) grab.SetTargetPose(new Pose(grab.transform.position, grab.transform.rotation));
            foreach (var tap in taps) if (tap != null) tap.SetRunning(false);
            foreach (var world in waterWorlds) if (world != null) world.ClearFlow();
            foreach (var container in containers) if (container != null) container.ResetWater();
            foreach (var stream in streams) if (stream != null) stream.Stop();
            foreach (var effect in effects) if (effect != null) effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            foreach (var sound in audioEffects) if (sound != null) sound.Stop();
            Physics.SyncTransforms();
        }
        static void SynchronizeBody(Actor actor)
        {
            var body = actor.body; if (body == null) return;
            body.isKinematic = actor.kinematic; body.useGravity = actor.gravity;
            body.position = actor.transform.position; body.rotation = actor.transform.rotation;
            if (!body.isKinematic) { body.velocity = Vector3.zero; body.angularVelocity = Vector3.zero; body.Sleep(); }
        }
        void RestoreInteraction()
        {
            if (restoreFrame < 0) return;
            foreach (var actor in actors) if (actor.transform != null && actor.body != null)
            {
                actor.transform.SetLocalPositionAndRotation(actor.position, actor.rotation);
                actor.transform.localScale = actor.scale;
                SynchronizeBody(actor); actor.body.interpolation = actor.interpolation;
            }
            Physics.SyncTransforms();
            for (var i = 0; i < doors.Length; i++) if (doors[i] != null) doors[i].SetOpenImmediate(openDoors[i]);
            for (var i = 0; i < drawers.Length; i++) if (drawers[i] != null) drawers[i].SetOpenImmediate(openDrawers[i]);
            for (var i = 0; i < grabs.Length; i++) if (grabs[i] != null)
            { grabs[i].throwOnDetach = throwGrabs[i]; grabs[i].enabled = enabledGrabs[i]; }
            restoreFrame = -1;
        }
        void LateUpdate() { if (restoreFrame >= 0 && Time.frameCount >= restoreFrame) RestoreInteraction(); }
    }
}
