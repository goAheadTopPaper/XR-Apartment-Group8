using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace XRApartment.Runtime
{
    /// One dose per target per physics step, independent of compound-collider count.
    public sealed class KitchenHeatSource : MonoBehaviour
    {
        [SerializeField] XRSimpleInteractable switchButton;
        [SerializeField] Renderer indicator;
        [SerializeField] AudioSource sizzle;
        [SerializeField] ParticleSystem foodVapour;
        [SerializeField] Transform heatSurface;
        [SerializeField] KitchenBurnerFlame burnerFlame;
        [SerializeField] bool heatWater = true;
        readonly Dictionary<Collider, KitchenFoodVessel> vessels = new Dictionary<Collider, KitchenFoodVessel>();
        readonly Dictionary<Collider, CookableIngredient> foods = new Dictionary<Collider, CookableIngredient>();
        readonly Dictionary<Collider, LiquidContainer> waterVessels = new Dictionary<Collider, LiquidContainer>();
        readonly HashSet<KitchenFoodVessel> heatedVessels = new HashSet<KitchenFoodVessel>();
        readonly HashSet<CookableIngredient> heatedFoods = new HashSet<CookableIngredient>();
        readonly HashSet<LiquidContainer> heatedWater = new HashSet<LiquidContainer>();
        MaterialPropertyBlock tint;
        public bool IsOn { get; private set; }
        public bool IsHeating(KitchenFoodVessel vessel) => IsOn && heatedVessels.Contains(vessel) && InCookingPosition(vessel);
        public bool IsHeatingWater(LiquidContainer vessel) => IsOn && heatedWater.Contains(vessel) && InWaterPosition(vessel);
        public LiquidContainer WaterOnBurner { get; private set; }
        public void EnableWaterHeating() => heatWater = true;
        public void SetHeatSurface(Transform surface) => heatSurface = surface;

        bool InWaterPosition(LiquidContainer vessel)
        {
            if (vessel == null || !vessel.isActiveAndEnabled || vessel.Millilitres <= LiquidContainer.VisibleMillilitres ||
                Vector3.Dot(vessel.transform.up, Vector3.up) < .92f) return false;
            var source = heatSurface == null ? transform.position : heatSurface.position;
            var bottom = vessel.transform.TransformPoint(vessel.InteriorOrigin) - source;
            return bottom.x * bottom.x + bottom.z * bottom.z < .11f * .11f && bottom.y >= -.02f && bottom.y <= .055f;
        }
        bool InCookingPosition(KitchenFoodVessel vessel)
        {
            if (vessel == null || Vector3.Dot(vessel.transform.up, Vector3.up) < .85f) return false;
            var source = heatSurface == null ? transform.position : heatSurface.position;
            var bottom = vessel.transform.TransformPoint(Vector3.up * vessel.InnerBottom) - source;
            return bottom.x * bottom.x + bottom.z * bottom.z < .13f * .13f && bottom.y >= -.025f && bottom.y < .09f;
        }
        public void Configure(XRSimpleInteractable button, Renderer glow) { switchButton = button; indicator = glow; }
        public void ConfigureFeedback(AudioSource sound, ParticleSystem steam) { sizzle = sound; foodVapour = steam; }
        public void ConfigureFlame(KitchenBurnerFlame flame) { burnerFlame = flame; if (burnerFlame != null) burnerFlame.SetLit(IsOn); }
        void Awake()
        {
            if (sizzle == null) return;
            sizzle.volume = .52f;
            sizzle.minDistance = .8f;
            sizzle.rolloffMode = AudioRolloffMode.Linear;
            sizzle.dopplerLevel = 0f;
        }

        void OnEnable() { if (switchButton != null) switchButton.selectEntered.AddListener(Toggle); }
        void OnDisable() { if (switchButton != null) switchButton.selectEntered.RemoveListener(Toggle); vessels.Clear(); foods.Clear(); waterVessels.Clear(); SetOn(false); }
        void Toggle(UnityEngine.XR.Interaction.Toolkit.SelectEnterEventArgs args)
        {
            SetOn(!IsOn);
            KitchenAudio.Play(gameObject.scene, KitchenCue.Stove, transform.position, .7f);
        }
        public void SetOn(bool on)
        {
            IsOn = on;
            if (burnerFlame != null) burnerFlame.SetLit(on);
            if (!on) StopFeedback();
            if (indicator == null) return;
            if (tint == null) tint = new MaterialPropertyBlock();
            tint.SetColor("_BaseColor", on ? new Color(1, .16f, .025f) : new Color(.12f, .08f, .07f));
            tint.SetColor("_EmissiveColor", on ? new Color(2, .12f, .01f) : Color.black);
            indicator.SetPropertyBlock(tint);
        }
        void OnTriggerEnter(Collider other)
        {
            var body = other.attachedRigidbody;
            if (body == null) return;
            var vessel = body.GetComponent<KitchenFoodVessel>();
            if (vessel != null && vessel.CanHeat) vessels[other] = vessel;
            var ingredient = body.GetComponent<CookableIngredient>();
            if (ingredient != null) foods[other] = ingredient;
            if (heatWater && body.TryGetComponent<LiquidContainer>(out var water) && water.CanBoilOnStove)
                waterVessels[other] = water;
        }
        void OnTriggerExit(Collider other) { vessels.Remove(other); foods.Remove(other); waterVessels.Remove(other); }
        void OnTriggerStay(Collider other) => OnTriggerEnter(other);
        void FixedUpdate()
        {
            WaterOnBurner = null;
            heatedWater.Clear();
            foreach (var pair in waterVessels)
                if (ActiveCollider(pair.Key) && InWaterPosition(pair.Value) && heatedWater.Add(pair.Value))
                {
                    if (WaterOnBurner == null) WaterOnBurner = pair.Value;
                    if (IsOn) pair.Value.HeatWater(Time.fixedDeltaTime);
                }
            if (!IsOn) return;
            heatedVessels.Clear(); heatedFoods.Clear();
            foreach (var pair in vessels)
                if (pair.Key != null && pair.Key.enabled && pair.Key.gameObject.activeInHierarchy && pair.Value != null && InCookingPosition(pair.Value) && heatedVessels.Add(pair.Value))
                    pair.Value.Heat(1, Time.fixedDeltaTime);
            foreach (var pair in foods)
                if (pair.Key != null && pair.Key.enabled && pair.Key.gameObject.activeInHierarchy && pair.Value != null && heatedFoods.Add(pair.Value))
                    pair.Value.Heat(1, Time.fixedDeltaTime);
            var solidFood = heatedFoods.Count > 0;
            var burnt = false;
            foreach (var ingredient in heatedFoods) if (ingredient.Stage == XRApartment.Data.CookingStage.Burnt) burnt = true;
            var liquidFood = false;
            foreach (var vessel in heatedVessels)
            {
                if (vessel.Batch.Millilitres > .01f) liquidFood = true;
                var pan = vessel.PanContents;
                if (pan == null || pan.CapturedCount == 0) continue;
                solidFood = true;
                if (pan.IsBurnt) burnt = true;
            }
            var containsFood = liquidFood || solidFood;
            if (sizzle != null)
            {
                if (containsFood && !sizzle.isPlaying && sizzle.clip != null) sizzle.Play();
                if (!containsFood && sizzle.isPlaying) sizzle.Stop();
            }
            if (foodVapour != null)
            {
                var waterHot = WaterOnBurner != null && WaterOnBurner.HasHotSteam;
                var boiling = WaterOnBurner != null && WaterOnBurner.IsBoiling;
                var emission = foodVapour.emission; emission.enabled = solidFood || waterHot;
                if (emission.enabled && !foodVapour.isPlaying) foodVapour.Play();
                var main = foodVapour.main; main.startColor = burnt ? new Color(.17f, .14f, .1f, .28f) :
                    boiling ? new Color(.94f, .96f, 1f, .32f) :
                    waterHot ? new Color(.93f, .95f, 1f, .2f) : new Color(.9f, .9f, .86f, .15f);
            }
        }
        static bool ActiveCollider(Collider collider) => collider != null && collider.enabled && collider.gameObject.activeInHierarchy;
        void StopFeedback()
        {
            if (sizzle != null) sizzle.Stop();
            if (foodVapour != null) { var emission = foodVapour.emission; emission.enabled = false; }
        }
    }
}
