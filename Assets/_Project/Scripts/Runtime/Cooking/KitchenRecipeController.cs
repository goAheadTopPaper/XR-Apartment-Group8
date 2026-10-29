using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using XRApartment.Data;
using XRApartment.Simulation;

namespace XRApartment.Runtime
{
    /// Reads actual ingredient, utensil and heat state; no timed auto-completion.
    public sealed class KitchenRecipeController : MonoBehaviour
    {
        [SerializeField] KitchenCookingProfile profile;
        [SerializeField] KitchenFridge fridge;
        [SerializeField] PreparedKitchenProp tomato;
        [SerializeField] PreparedKitchenProp[] slices;
        [SerializeField] KitchenEgg[] eggs;
        [SerializeField] KitchenFoodVessel bowl, pan;
        [SerializeField] KitchenHeatSource stove;
        [SerializeField] TextMesh fridgeHint;
        float nextHint;
        XRGrabInteractable bowlGrab;
        KitchenPanContents contents;
        public TomatoEggRecipe Recipe { get; } = new TomatoEggRecipe();
        static readonly string[] Hints = {
            "Open the fridge using its handle.", "Take a tomato out of the fridge.",
            "Hold the knife and slice the tomato on the board.", "Cut each slice again; move chunks into the prep bowl.",
            "Open the fridge and take an egg.", "Pick up the mixing bowl from the table.",
            "Hold the egg above the bowl; tap the rim or press trigger.",
            "Move the whisk through the egg until evenly mixed.", "Select STOVE to turn on the heat.",
            "Place the frying pan flat on the burner.", "Tilt the bowl and pour the beaten egg into the pan.",
            "Let the egg set, then use the spatula to scramble it.",
            "Tip diced tomato from the board or prep bowl into the pan.",
            "Keep heating and fold tomato and egg with the spatula.",
            "Tomato and egg is ready. Turn off the stove!", "Food burnt. Select RESET to try again."
        };
        public string Instructions => "TOMATO & EGG\n" +
            (Recipe.Step == TomatoEggStep.Ready || Recipe.Step == TomatoEggStep.Burnt ? "" :
                "Step " + ((int)Recipe.Step + 1) + "/14: ") + Hints[(int)Recipe.Step] +
            "\nEgg " + (pan == null ? 0 : pan.Batch.Millilitres).ToString("0") + " mL | Tomato " +
            (contents == null ? 0 : contents.TomatoGrams).ToString("0") + " g | " +
            (stove != null && stove.IsOn ? "STOVE ON" : "STOVE OFF") +
            "\nGrip: hold tools | Trigger: egg / handle / stove | RESET: start again";

        public void Configure(KitchenCookingProfile settings, KitchenFridge storage, PreparedKitchenProp whole,
            PreparedKitchenProp[] cutSlices, KitchenEgg[] rawEggs, KitchenFoodVessel mixing,
            KitchenFoodVessel frying, KitchenHeatSource heat)
        { profile = settings; fridge = storage; tomato = whole; slices = cutSlices; eggs = rawEggs; bowl = mixing; pan = frying; stove = heat; }
        public void SetFridgeHint(TextMesh label) => fridgeHint = label;
        void Awake() { bowlGrab = bowl.GetComponent<XRGrabInteractable>(); contents = pan.GetComponent<KitchenPanContents>(); }
        void OnEnable() { if (fridge != null) fridge.Retrieved += Retrieved; }
        void OnDisable() { if (fridge != null) fridge.Retrieved -= Retrieved; }
        void Retrieved(KitchenStoredFood food)
        {
            if (food == KitchenStoredFood.Tomato) Recipe.Observe(TomatoEggAction.TakeTomato);
            if (food == KitchenStoredFood.Egg) Recipe.Observe(TomatoEggAction.TakeEgg);
        }
        void Update()
        {
            if (profile == null || contents == null) return;
            if (fridge.Accessible) Recipe.Observe(TomatoEggAction.OpenFridge);
            if (tomato.IsSplit) Recipe.Observe(TomatoEggAction.SliceTomato);
            var diced = slices.Length > 0;
            foreach (var slice in slices) if (!slice.IsSplit) diced = false;
            if (diced) Recipe.Observe(TomatoEggAction.DiceTomato);
            if (bowlGrab.isSelected) Recipe.Observe(TomatoEggAction.TakeBowl);
            foreach (var egg in eggs)
                if (egg.LastReceivedMillilitres >= profile.minimumEggMillilitres) Recipe.Observe(TomatoEggAction.CrackEgg);
            if (bowl.Batch.Millilitres >= profile.minimumEggMillilitres && bowl.Batch.MixedFraction >= .99f)
                Recipe.Observe(TomatoEggAction.BeatEgg);
            if (stove.IsOn) Recipe.Observe(TomatoEggAction.TurnOnStove);
            if (stove.IsHeating(pan)) Recipe.Observe(TomatoEggAction.PlacePan);
            if (pan.Batch.Millilitres >= profile.minimumEggMillilitres && pan.Batch.MixedFraction >= .99f)
                Recipe.Observe(TomatoEggAction.PourEgg);
            if (contents.EggScrambleDistance >= profile.scrambleMetres) Recipe.Observe(TomatoEggAction.ScrambleEgg);
            if (contents.TomatoGrams >= profile.tomatoGrams * profile.requiredTomatoFraction) Recipe.Observe(TomatoEggAction.AddTomato);
            if (contents.StirFryDistance >= profile.stirFryMetres) Recipe.Observe(TomatoEggAction.StirFry);
            Recipe.ObserveCooking(pan.Stage == CookingStage.Cooked, contents.TomatoCooked, contents.IsBurnt);
            if (fridgeHint != null && Time.unscaledTime >= nextHint)
            { fridgeHint.text = Instructions; nextHint = Time.unscaledTime + .2f; }
        }
        public void ResetRecipe() => Recipe.Reset();
    }
}
