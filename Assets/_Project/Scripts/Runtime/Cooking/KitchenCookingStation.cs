using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using XRApartment.Data;

namespace XRApartment.Runtime
{
    /// Local sandbox reset and status. It does not emit formal review or feedback events.
    public sealed class KitchenCookingStation : MonoBehaviour
    {
        [SerializeField] Transform[] resetTargets;
        [SerializeField] XRSimpleInteractable resetButton;
        [SerializeField] KitchenFoodVessel bowl, pan;
        [SerializeField] KitchenHeatSource heat;
        [SerializeField] TextMesh status;
        [SerializeField] KitchenRecipeController recipe;
        [SerializeField] KitchenFridge fridge;
        KitchenStoredIngredient[] stored;
        KitchenPanContents panContents;
        KitchenPrepTray[] trays;
        bool[] stockedTargets;
        Vector3[] positions;
        Quaternion[] rotations;
        PreparedKitchenProp[] prepared;
        CookableIngredient[] ingredients;
        KitchenEgg[] eggs;
        Rigidbody[] bodies;
        XRGrabInteractable[] grabs;
        Rigidbody[] targetBodies;
        bool[] resetThrow;
        RigidbodyInterpolation[] resetInterpolation;
        int restoreThrowFrame = -1;
        float nextLabel;
        public event Action ResetStarting;
        public bool IsResetting => restoreThrowFrame >= 0;
        public KitchenFridge ActiveFridge => fridge;
        public void Configure(Transform[] targets, XRSimpleInteractable reset, KitchenFoodVessel mixing,
            KitchenFoodVessel frying, KitchenHeatSource stove, TextMesh label)
        { resetTargets = targets; resetButton = reset; bowl = mixing; pan = frying; heat = stove; status = label; }
        public void ConfigureRecipe(KitchenRecipeController flow, KitchenFridge storage) { recipe = flow; fridge = storage; }
        internal void UseFridge(KitchenFridge storage)
        {
            fridge = storage;
            foreach (var item in stored) item.UseFridge(storage);
            if (recipe != null) recipe.UseFridge(storage);
        }
        void Awake()
        {
            stockedTargets = new bool[resetTargets.Length];
            positions = new Vector3[resetTargets.Length]; rotations = new Quaternion[resetTargets.Length];
            for (var i = 0; i < resetTargets.Length; i++)
            {
                var target = resetTargets[i];
                stockedTargets[i] = target.TryGetComponent<KitchenStoredIngredient>(out var stock) && stock.Fridge == fridge && fridge != null;
                var frame = stockedTargets[i] ? fridge.transform : null;
                positions[i] = frame == null ? target.position : frame.InverseTransformPoint(target.position);
                rotations[i] = frame == null ? target.rotation : Quaternion.Inverse(frame.rotation) * target.rotation;
            }
            prepared = GetComponentsInChildren<PreparedKitchenProp>(true);
            ingredients = GetComponentsInChildren<CookableIngredient>(true);
            eggs = GetComponentsInChildren<KitchenEgg>(true);
            bodies = GetComponentsInChildren<Rigidbody>(true);
            grabs = GetComponentsInChildren<XRGrabInteractable>(true);
            targetBodies = new Rigidbody[resetTargets.Length];
            for (var i = 0; i < resetTargets.Length; i++) targetBodies[i] = resetTargets[i].GetComponent<Rigidbody>();
            resetThrow = new bool[grabs.Length];
            resetInterpolation = new RigidbodyInterpolation[bodies.Length];
            stored = GetComponentsInChildren<KitchenStoredIngredient>(true);
            panContents = pan.GetComponent<KitchenPanContents>();
            trays = GetComponentsInChildren<KitchenPrepTray>(true);
        }
        void OnEnable() { if (resetButton != null) resetButton.selectEntered.AddListener(ResetSelected); }
        void OnDisable() { if (resetButton != null) resetButton.selectEntered.RemoveListener(ResetSelected); }
        void ResetSelected(UnityEngine.XR.Interaction.Toolkit.SelectEnterEventArgs args) => ResetStation();
        public void ResetStation()
        {
            ResetStarting?.Invoke();
            var pending = restoreThrowFrame >= 0;
            for (var i = 0; i < grabs.Length; i++)
                if (grabs[i] != null)
                {
                    if (restoreThrowFrame < 0) resetThrow[i] = grabs[i].throwOnDetach;
                }
            restoreThrowFrame = Time.frameCount + 2;
            // Disabled interactables stop receiving XRI's Late phase. Drain any
            // pending release while throwing is suppressed, including a release
            // that occurred earlier in this frame, before restoring the pose.
            foreach (var grab in grabs) if (grab != null) grab.throwOnDetach = false;
            foreach (var grab in grabs) if (grab != null)
            {
                grab.enabled = false;
                grab.ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase.Late);
            }
            for (var i = 0; i < bodies.Length; i++)
                if (bodies[i] != null)
                {
                    if (!pending) resetInterpolation[i] = bodies[i].interpolation;
                    bodies[i].interpolation = RigidbodyInterpolation.None;
                }
            foreach (var tray in trays) tray.ResetContents();
            if (panContents != null) panContents.ResetContents();
            foreach (var item in prepared) item.ResetProp();
            foreach (var item in ingredients) item.ResetCooking();
            foreach (var item in eggs) item.ResetEgg();
            foreach (var body in bodies)
                if (body != null && !body.isKinematic) { body.velocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
            for (var i = 0; i < resetTargets.Length; i++)
            {
                var frame = stockedTargets[i] ? fridge.transform : null;
                var position = frame == null ? positions[i] : frame.TransformPoint(positions[i]);
                var rotation = frame == null ? rotations[i] : frame.rotation * rotations[i];
                resetTargets[i].SetPositionAndRotation(position, rotation);
                var body = targetBodies[i]; if (body == null) continue;
                body.position = position; body.rotation = rotation;
                if (!body.isKinematic) { body.velocity = Vector3.zero; body.angularVelocity = Vector3.zero; body.Sleep(); }
            }
            foreach (var body in bodies)
                if (body != null) { body.position = body.transform.position; body.rotation = body.transform.rotation; }
            foreach (var grab in grabs)
                if (grab != null) grab.SetTargetPose(new Pose(grab.transform.position, grab.transform.rotation));
            bowl.ResetFood(); pan.ResetFood(); heat.SetOn(false);
            foreach (var item in stored) item.ResetStored();
            // Door sweeps must see the restored poses, not the old carried-food
            // broadphase positions from before this atomic reset.
            Physics.SyncTransforms();
            if (fridge != null) fridge.ResetFridge();
            if (recipe != null) recipe.ResetRecipe();
            Physics.SyncTransforms();
            // XRI detaches late in the frame. Restore interaction only after its
            // pending throw and Rigidbody interpolation history have drained.
            foreach (var grab in grabs) if (grab != null) grab.enabled = false;
        }
        void LateUpdate()
        {
            if (restoreThrowFrame < 0 || Time.frameCount < restoreThrowFrame) return;
            for (var i = 0; i < grabs.Length; i++) if (grabs[i] != null) grabs[i].throwOnDetach = resetThrow[i];
            for (var i = 0; i < bodies.Length; i++) if (bodies[i] != null) bodies[i].interpolation = resetInterpolation[i];
            foreach (var grab in grabs) if (grab != null) grab.enabled = true;
            restoreThrowFrame = -1;
        }
        void Update()
        {
            if (status == null || Time.unscaledTime < nextLabel) return;
            nextLabel = Time.unscaledTime + .2f;
            if (recipe != null) { status.text = recipe.Instructions; return; }
            status.text = "COOKING WORKBENCH\n" +
                "Egg: hold above bowl + trigger / tap rim\nWhisk: move tip in egg | Pour: tilt bowl over pan\n" +
                "Stove: select switch | Knife: grip + cut downward\n" +
                "Bowl " + bowl.Batch.Millilitres.ToString("0") + " mL / " + bowl.Stage +
                "    Pan " + pan.Batch.Millilitres.ToString("0") + " mL / " + pan.Stage +
                "\nStove " + (heat.IsOn ? "ON" : "OFF") + " | 12s cooked, 32s burnt\n" +
                "Select RESET to restore ingredients and clear contents";
        }
    }
}
