using System;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using XRApartment.Data;
using XRApartment.Simulation;

namespace XRApartment.Runtime
{
    /// Reads actual ingredient, utensil and heat state; no timed auto-completion.
    public sealed class KitchenRecipeController : MonoBehaviour
    {
        [Serializable]
        public sealed class Guide
        {
            public TMP_Text heading, action, detail;
            internal bool IsBound => heading != null && action != null && detail != null;

            internal void Show(string title, string nextAction, string progress, Color accent)
            {
                if (!IsBound) return;
                heading.text = title;
                heading.color = accent;
                action.text = nextAction;
                detail.text = progress;
            }
        }

        static readonly Color Guidance = new Color(.66f, .91f, 1f);
        static readonly Color Complete = new Color(.68f, 1f, .76f);
        static readonly Color Attention = new Color(1f, .77f, .44f);
        [SerializeField] KitchenCookingProfile profile;
        [SerializeField] KitchenFridge fridge;
        [SerializeField] PreparedKitchenProp tomato;
        [SerializeField] PreparedKitchenProp[] slices;
        [SerializeField] KitchenEgg[] eggs;
        [SerializeField] KitchenFoodVessel bowl, pan;
        [SerializeField] KitchenHeatSource stove;
        [SerializeField] TextMesh fridgeHint;
        [SerializeField] Guide worktopGuide, fridgeGuide;
        [SerializeField] XRSimpleInteractable recipeButton;
        [SerializeField] CookableIngredient beef, onion;
        [SerializeField] Transform servingPlate;
        [SerializeField] float servingRadius;
        float nextHint, blockerVisibleUntil;
        Collider recentFridgeBlocker;
        Vector3 fridgeHintPosition;
        Quaternion fridgeHintRotation;
        XRGrabInteractable bowlGrab;
        KitchenPanContents contents;
        KitchenCookingStation station;
        KitchenStoredIngredient beefStock, onionStock;
        public TomatoEggRecipe Recipe { get; } = new TomatoEggRecipe();
        public KitchenRecipeChoice ActiveRecipe { get; private set; }
        public BeefOnionStep BeefStep
        {
            get
            {
                if (contents == null || beefStock == null || onionStock == null) return BeefOnionStep.TakeIngredients;
                if (contents.BeefOnionPlated) return BeefOnionStep.Served;
                if (contents.BeefStage == CookingStage.Burnt || contents.OnionStage == CookingStage.Burnt)
                    return BeefOnionStep.Burnt;
                if (!beefStock.WasRetrieved || !onionStock.WasRetrieved) return BeefOnionStep.TakeIngredients;
                if (!beef.Prepared.IsSplit || !onion.Prepared.IsSplit) return BeefOnionStep.CutIngredients;
                if (contents.BeefGrams < beef.Grams * .75f || contents.OnionGrams < onion.Grams * .75f)
                    return BeefOnionStep.AddToPan;
                if (contents.BeefStage != CookingStage.Cooked || contents.OnionStage != CookingStage.Cooked ||
                    contents.BeefOnionStirDistance < profile.stirFryMetres) return BeefOnionStep.CookAndStir;
                return stove.IsOn ? BeefOnionStep.TurnOffStove : BeefOnionStep.Plate;
            }
        }
        static readonly string[] Hints = {
            "Grip and pull the fridge handle.", "Take a tomato out of the fridge.",
            "Hold the knife and slice the tomato on the board.", "Cut each slice again; move chunks into the prep bowl.",
            "Open the fridge and take an egg.", "Pick up the mixing bowl from the table.",
            "Hold the egg above the bowl; tap the rim or press trigger.",
            "Move the whisk through the egg until evenly mixed.", "Select STOVE to turn on the heat.",
            "Place the frying pan flat on the burner.", "Tilt the bowl and pour the beaten egg into the pan.",
            "Let the egg set, then use the spatula to scramble it.",
            "Tip diced tomato from the board or prep bowl into the pan.",
            "Keep heating and fold tomato and egg with the spatula.",
            "Tomato and egg is ready. Turn off the stove!", "Food burnt. Select RESET ROOM to try again."
        };
        static readonly string[] BeefHints = {
            "Open the fridge and take out beef and an onion.",
            "Cut the beef and onion on the board.",
            "Put the cut beef and onion into the pan.",
            "Set the pan on the lit burner and stir both ingredients.",
            "Turn off the stove before serving.",
            "Hold the pan and tip its edge over the serving plate.",
            "Beef and onion served. Select RESET ROOM to cook again.",
            "Food burnt. Select RESET ROOM to try again."
        };
        public string Instructions => ActiveRecipe == KitchenRecipeChoice.BeefOnion ? BeefInstructions : TomatoInstructions;
        string TomatoInstructions => "TOMATO & EGG\n" +
            (Recipe.Step == TomatoEggStep.Ready || Recipe.Step == TomatoEggStep.Burnt ? "" :
                "Step " + ((int)Recipe.Step + 1) + "/14: ") + Hints[(int)Recipe.Step] +
            "\nEgg " + (pan == null ? 0 : pan.Batch.Millilitres).ToString("0") + " mL | Tomato " +
            (contents == null ? 0 : contents.TomatoGrams).ToString("0") + " g | " +
            (stove != null && stove.IsOn ? "STOVE ON" : "STOVE OFF") +
            (contents != null && contents.BeefGrams > 0f
                ? $"\nBeef {contents.BeefGrams:0} g | {contents.BeefStage}" : "") +
            "\nGrip: hold tools | Trigger: egg / handle / stove | RESET: start again" +
            (recipeButton == null ? "" : "\nRECIPE: switch and restock");
        string BeefInstructions => "BEEF & ONION\n" + BeefHints[(int)BeefStep] +
            (contents == null || contents.BeefOnionPlated ? "" :
                $"\nBeef {contents.BeefGrams:0} g · {contents.BeefStage} | Onion {contents.OnionGrams:0} g · {contents.OnionStage}") +
            "\nRECIPE: switch and restock | RESET: start again";

        public void Configure(KitchenCookingProfile settings, KitchenFridge storage, PreparedKitchenProp whole,
            PreparedKitchenProp[] cutSlices, KitchenEgg[] rawEggs, KitchenFoodVessel mixing,
            KitchenFoodVessel frying, KitchenHeatSource heat)
        { profile = settings; fridge = storage; tomato = whole; slices = cutSlices; eggs = rawEggs; bowl = mixing; pan = frying; stove = heat; }
        public void SetFridgeHint(TextMesh label) => fridgeHint = label;
        public void ConfigureGuides(Guide worktop, Guide storage)
        {
            worktopGuide = worktop;
            fridgeGuide = storage;
        }
        public void ConfigureBeefOnion(XRSimpleInteractable button, CookableIngredient rawBeef,
            CookableIngredient rawOnion, Transform plate, float plateRadius)
        {
            recipeButton = button;
            beef = rawBeef;
            onion = rawOnion;
            servingPlate = plate;
            servingRadius = plateRadius;
        }
        void Awake()
        {
            bowlGrab = bowl.GetComponent<XRGrabInteractable>();
            contents = pan.GetComponent<KitchenPanContents>();
            station = GetComponent<KitchenCookingStation>();
            if (beef != null) beefStock = beef.GetComponent<KitchenStoredIngredient>();
            if (onion != null) onionStock = onion.GetComponent<KitchenStoredIngredient>();
            var display = FridgeDisplay;
            if (display == null || fridge == null) return;
            fridgeHintPosition = fridge.transform.InverseTransformPoint(display.position);
            fridgeHintRotation = Quaternion.Inverse(fridge.transform.rotation) * display.rotation;
        }
        void OnEnable()
        {
            if (fridge != null) fridge.Retrieved += Retrieved;
            if (recipeButton != null) recipeButton.selectEntered.AddListener(ChangeRecipe);
        }
        void OnDisable()
        {
            if (fridge != null) fridge.Retrieved -= Retrieved;
            if (recipeButton != null) recipeButton.selectEntered.RemoveListener(ChangeRecipe);
        }
        void ChangeRecipe(SelectEnterEventArgs args)
        {
            if (station.IsResetting || beefStock == null || onionStock == null) return;
            ActiveRecipe = ActiveRecipe == KitchenRecipeChoice.TomatoEgg ? KitchenRecipeChoice.BeefOnion : KitchenRecipeChoice.TomatoEgg;
            station.ResetStation();
        }
        internal void UseFridge(KitchenFridge storage)
        {
            if (fridge != storage)
            {
                if (isActiveAndEnabled && fridge != null) fridge.Retrieved -= Retrieved;
                fridge = storage;
                recentFridgeBlocker = null;
                if (isActiveAndEnabled) fridge.Retrieved += Retrieved;
            }
            var display = FridgeDisplay;
            if (display != null)
                display.SetPositionAndRotation(fridge.transform.TransformPoint(fridgeHintPosition),
                    fridge.transform.rotation * fridgeHintRotation);
        }

        Transform FridgeDisplay => fridgeGuide?.heading != null ? fridgeGuide.heading.transform.parent :
            fridgeHint != null ? fridgeHint.transform : null;
        void Retrieved(KitchenStoredFood food)
        {
            if (ActiveRecipe != KitchenRecipeChoice.TomatoEgg) return;
            if (food == KitchenStoredFood.Tomato) Recipe.Observe(TomatoEggAction.TakeTomato);
            if (food == KitchenStoredFood.Egg) Recipe.Observe(TomatoEggAction.TakeEgg);
        }
        void Update()
        {
            if (profile == null || contents == null) return;
            if (ActiveRecipe == KitchenRecipeChoice.TomatoEgg) ObserveTomatoRecipe();
            else if (BeefStep == BeefOnionStep.Plate) contents.TryPlateBeefOnion(servingPlate, servingRadius);
        }

        void LateUpdate()
        {
            if (profile == null || contents == null) return;
            var now = Time.unscaledTime;
            // Sample after the door has swept this frame, before throttling text updates.
            // Brief gaps in collision contact should not flicker the instruction.
            if (!fridge.Door.IsOpen || fridge.Accessible) recentFridgeBlocker = null;
            else if (fridge.Door.OpeningBlocker != null)
            {
                recentFridgeBlocker = fridge.Door.OpeningBlocker;
                blockerVisibleUntil = now + .4f;
            }
            else if (now >= blockerVisibleUntil) recentFridgeBlocker = null;
            if (now < nextHint) return;
            nextHint = now + .2f;
            if (fridgeHint != null) fridgeHint.text = Instructions;
            if (worktopGuide?.IsBound == true || fridgeGuide?.IsBound == true) RefreshGuides();
        }

        void RefreshGuides()
        {
            var beefRecipe = ActiveRecipe == KitchenRecipeChoice.BeefOnion;
            var dish = beefRecipe ? "BEEF & ONION" : "TOMATO & EGG";
            var tomatoStep = Recipe.Step;
            var beefStep = BeefStep;
            var done = beefRecipe ? beefStep == BeefOnionStep.Served : tomatoStep == TomatoEggStep.Ready;
            var burnt = beefRecipe ? beefStep == BeefOnionStep.Burnt : tomatoStep == TomatoEggStep.Burnt;
            var step = beefRecipe ? (int)beefStep : (int)tomatoStep;
            var total = beefRecipe ? (int)BeefOnionStep.Served : (int)TomatoEggStep.Ready;
            var caption = done ? (beefRecipe ? "SERVED" : "READY") :
                burnt ? "BURNT" : $"STEP {step + 1} / {total}";
            var ingredient = NeededIngredient(beefRecipe, tomatoStep, beefStep);
            var blocker = recentFridgeBlocker;
            var blockedForRecipe = ingredient != null && blocker != null;
            var recovery = blocker == null ? null : blocker.GetComponentInParent<KitchenLayoutLab>() != null
                ? "Try compact fridge in Layout Lab (right B / F9)."
                : "Check the swing path before opening the door again.";
            const string blockedAction = "The fridge door is blocked.";
            var action = beefRecipe ? BeefHints[step] : Hints[step];
            if (beefRecipe && ingredient != null) action = "Take " + ingredient + " from the fridge.";
            if (done && !beefRecipe && !stove.IsOn)
                action = "Tomato and egg is ready. Select RESET ROOM to cook again.";
            var detail = blockedForRecipe ? recovery : beefRecipe ? BeefProgress(beefStep) : TomatoProgress(tomatoStep);
            var accent = burnt || blockedForRecipe ? Attention : done ? Complete : Guidance;
            worktopGuide?.Show(dish + "  /  " + caption, blockedForRecipe ? blockedAction : action, detail, accent);

            var fridgeAction = blocker != null ? blockedAction :
                ingredient == null ? "Continue at the worktop." :
                fridge.Accessible ? "Grip to take " + ingredient + "." :
                fridge.Door.IsOpen ? "Pull the handle farther." :
                "Grip and pull the fridge handle.";
            var fridgeDetail = recovery ?? (ingredient == null ? "RESET ROOM restocks the fridge." :
                "For this recipe: " + ingredient + ".");
            fridgeGuide?.Show("FRIDGE  /  " + dish, fridgeAction, fridgeDetail, blocker != null ? Attention : Guidance);
        }

        string NeededIngredient(bool beefRecipe, TomatoEggStep tomatoStep, BeefOnionStep beefStep)
        {
            if (beefRecipe)
            {
                if (beefStep != BeefOnionStep.TakeIngredients || beefStock == null || onionStock == null) return null;
                if (beefStock.WasRetrieved) return "an onion";
                return onionStock.WasRetrieved ? "beef" : "beef and an onion";
            }
            switch (tomatoStep)
            {
                case TomatoEggStep.OpenFridge:
                case TomatoEggStep.TakeTomato: return "a tomato";
                case TomatoEggStep.TakeEgg: return "an egg";
                default: return null;
            }
        }

        string TomatoProgress(TomatoEggStep step)
        {
            if (step == TomatoEggStep.DiceTomato)
            {
                var diced = 0;
                foreach (var slice in slices) if (slice.IsSplit) diced++;
                return $"{diced} / {slices.Length} slices diced";
            }
            if (step == TomatoEggStep.CrackEgg || step == TomatoEggStep.BeatEgg)
                return $"Mixing bowl  {bowl.Batch.Millilitres:0} mL egg  /  {bowl.Batch.MixedFraction:P0} mixed";
            if (step >= TomatoEggStep.TurnOnStove && step <= TomatoEggStep.Ready)
                return $"Pan  {pan.Batch.Millilitres:0} mL egg  /  {contents.TomatoGrams:0} g tomato  /  {StoveStatus}";
            return "Grip to hold  /  Trigger to use  /  RECIPE to switch";
        }

        string BeefProgress(BeefOnionStep step)
        {
            if (step == BeefOnionStep.Served) return "Grip the plate to carry your dish.";
            if (step == BeefOnionStep.Burnt) return StoveStatus;
            return $"Beef  {contents.BeefGrams:0} g  {contents.BeefStage}  /  " +
                $"Onion  {contents.OnionGrams:0} g  {contents.OnionStage}  /  {StoveStatus}";
        }

        string StoveStatus => stove.IsOn ? "Stove on" : "Stove off";
        void ObserveTomatoRecipe()
        {
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
            Recipe.ObserveCooking(pan.Stage == CookingStage.Cooked, contents.TomatoCooked, contents.TomatoEggBurnt);
        }
        public void ResetRecipe() => Recipe.Reset();
    }
}
