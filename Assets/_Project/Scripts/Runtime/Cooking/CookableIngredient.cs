using UnityEngine;
using XRApartment.Data;
using XRApartment.Simulation;

namespace XRApartment.Runtime
{
    public sealed class CookableIngredient : MonoBehaviour
    {
        [SerializeField] KitchenCookingProfile profile;
        [SerializeField] CookableIngredient[] pieces;
        [SerializeField] KitchenIngredientKind ingredientKind;
        [SerializeField] KitchenCutForm cutForm;
        [SerializeField] float grams;
        [SerializeField] Vector3 presentationUp;
        PreparedKitchenProp prepared;
        MeshRenderer[] visuals;
        Color[][] originalColours;
        CookingProgress progress;
        MaterialPropertyBlock tint;
        public CookingStage Stage { get { Initialize(); return progress == null ? CookingStage.Raw : progress.Stage; } }
        public float HeatDose { get { Initialize(); return progress == null ? 0 : progress.Dose; } }
        public KitchenIngredientKind IngredientKind => ingredientKind;
        public KitchenCutForm CutForm => cutForm;
        public float Grams => grams;
        public Vector3 PresentationUp => presentationUp;
        public PreparedKitchenProp Prepared { get { Initialize(); return prepared; } }
        public void ConfigureIdentity(KitchenIngredientKind kind, KitchenCutForm form, float massGrams)
        { ingredientKind = kind; cutForm = form; grams = massGrams; }
        public void ConfigurePresentationUp(Vector3 normal) => presentationUp = normal.normalized;
        public void Configure(KitchenCookingProfile settings, CookableIngredient[] derived) { profile = settings; pieces = derived; }
        void Initialize()
        {
            if (progress != null || profile == null) return;
            progress = new CookingProgress(profile.cookedSeconds, profile.burntSeconds);
            tint = new MaterialPropertyBlock();
            if (ingredientKind == KitchenIngredientKind.Tomato)
            {
                var body = GetComponent<Rigidbody>();
                if (body != null)
                {
                    body.drag = profile.foodLinearDamping; body.angularDrag = profile.foodAngularDamping;
                    body.maxDepenetrationVelocity = .35f; body.solverIterations = 10; body.solverVelocityIterations = 4;
                    foreach (var collider in GetComponentsInChildren<Collider>(true))
                        if (collider.GetComponentInParent<Rigidbody>(true) == body && collider.name != "COL_GrabSweepProbe")
                            collider.contactOffset = profile.foodContactOffset;
                }
            }
            prepared = GetComponent<PreparedKitchenProp>();
            var root = transform.Find("Intact");
            visuals = (root == null ? transform : root).GetComponentsInChildren<MeshRenderer>(true);
            originalColours = new Color[visuals.Length][];
            for (var i = 0; i < visuals.Length; i++)
            {
                var materials = visuals[i].sharedMaterials;
                originalColours[i] = new Color[materials.Length];
                for (var j = 0; j < materials.Length; j++) originalColours[i][j] = materials[j] != null ? materials[j].color : Color.white;
            }
        }
        void Awake() => Initialize();
        void OnEnable() { Initialize(); if (prepared != null) prepared.Split += InheritCooking; }
        void OnDisable() { if (prepared != null) prepared.Split -= InheritCooking; }
        void InheritCooking(PreparedKitchenProp prop)
        {
            if (pieces == null || progress == null) return;
            foreach (var piece in pieces) piece.Restore(progress.Dose);
        }
        public void Heat(float intensity, float seconds)
        {
            Initialize(); if (progress == null || (prepared != null && prepared.IsSplit)) return;
            progress.Advance(intensity, seconds); ApplyTint();
        }
        void Restore(float dose) { Initialize(); if (progress == null) return; progress.Restore(dose); ApplyTint(); }
        void ApplyTint()
        {
            var colour = progress.Stage == CookingStage.Burnt ? profile.burntTint :
                progress.Stage == CookingStage.Cooked ? profile.cookedFoodTint : Color.white;
            for (var i = 0; i < visuals.Length; i++)
                if (!visuals[i].name.StartsWith("COL_"))
                    for (var j = 0; j < originalColours[i].Length; j++)
                    {
                        tint.SetColor("_Color", originalColours[i][j] * colour);
                        visuals[i].SetPropertyBlock(tint, j);
                    }
        }
        public void ResetCooking() => Restore(0);
    }
}
