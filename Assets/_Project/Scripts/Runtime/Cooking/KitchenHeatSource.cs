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
        readonly Dictionary<Collider, KitchenFoodVessel> vessels = new Dictionary<Collider, KitchenFoodVessel>();
        readonly Dictionary<Collider, CookableIngredient> foods = new Dictionary<Collider, CookableIngredient>();
        readonly HashSet<KitchenFoodVessel> heatedVessels = new HashSet<KitchenFoodVessel>();
        readonly HashSet<CookableIngredient> heatedFoods = new HashSet<CookableIngredient>();
        MaterialPropertyBlock tint;
        public bool IsOn { get; private set; }
        public bool IsHeating(KitchenFoodVessel vessel) => IsOn && heatedVessels.Contains(vessel) && InCookingPosition(vessel);
        public void SetHeatSurface(Transform surface) => heatSurface = surface;
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
        void OnEnable() { if (switchButton != null) switchButton.selectEntered.AddListener(Toggle); }
        void OnDisable() { if (switchButton != null) switchButton.selectEntered.RemoveListener(Toggle); vessels.Clear(); foods.Clear(); SetOn(false); }
        void Toggle(UnityEngine.XR.Interaction.Toolkit.SelectEnterEventArgs args) => SetOn(!IsOn);
        public void SetOn(bool on)
        {
            IsOn = on;
            if (burnerFlame != null) burnerFlame.SetLit(on);
            if (!on) StopFeedback();
            if (indicator == null) return;
            if (tint == null) tint = new MaterialPropertyBlock();
            tint.SetColor("_Color", on ? new Color(1, .16f, .025f) : new Color(.12f, .08f, .07f));
            tint.SetColor("_EmissionColor", on ? new Color(2, .12f, .01f) : Color.black);
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
        }
        void OnTriggerExit(Collider other) { vessels.Remove(other); foods.Remove(other); }
        void OnTriggerStay(Collider other) => OnTriggerEnter(other);
        void FixedUpdate()
        {
            if (!IsOn) return;
            heatedVessels.Clear(); heatedFoods.Clear();
            foreach (var pair in vessels)
                if (pair.Key != null && pair.Key.enabled && pair.Key.gameObject.activeInHierarchy && pair.Value != null && InCookingPosition(pair.Value) && heatedVessels.Add(pair.Value))
                    pair.Value.Heat(1, Time.fixedDeltaTime);
            foreach (var pair in foods)
                if (pair.Key != null && pair.Key.enabled && pair.Key.gameObject.activeInHierarchy && pair.Value != null && heatedFoods.Add(pair.Value))
                    pair.Value.Heat(1, Time.fixedDeltaTime);
            var containsFood = heatedFoods.Count > 0;
            foreach (var vessel in heatedVessels) if (vessel.Batch.Millilitres > .01f) containsFood = true;
            if (sizzle != null)
            {
                if (containsFood && !sizzle.isPlaying && sizzle.clip != null) sizzle.Play();
                if (!containsFood && sizzle.isPlaying) sizzle.Stop();
            }
            if (foodVapour != null)
            {
                var emission = foodVapour.emission; emission.enabled = heatedFoods.Count > 0;
                var burnt = false;
                foreach (var ingredient in heatedFoods) if (ingredient.Stage == XRApartment.Data.CookingStage.Burnt) burnt = true;
                var main = foodVapour.main; main.startColor = burnt ? new Color(.17f, .14f, .1f, .28f) : new Color(.9f, .9f, .86f, .15f);
            }
        }
        void StopFeedback()
        {
            if (sizzle != null) sizzle.Stop();
            if (foodVapour != null) { var emission = foodVapour.emission; emission.enabled = false; }
        }
    }
}
