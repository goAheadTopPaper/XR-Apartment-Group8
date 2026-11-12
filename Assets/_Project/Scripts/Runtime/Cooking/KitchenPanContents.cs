using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using XRApartment.Data;
using Unity.Profiling;

namespace XRApartment.Runtime
{
    /// Captures only released prepared pieces that physically reach the pan.
    /// A bounded, pooled food presentation lets the held spatula fold the food
    /// without weakening the room/utensil collision constraint.
    [RequireComponent(typeof(KitchenFoodVessel))]
    public sealed class KitchenPanContents : MonoBehaviour
    {
        static readonly ProfilerMarker PresentationMarker = new ProfilerMarker("XR Apartment.Cooking Presentation");
        sealed class Captured
        {
            public CookableIngredient food;
            public Rigidbody body;
            public XRGrabInteractable grab;
            public Transform parent;
            public Collider[] colliders;
            public bool[] enabled;
            public bool gravity, kinematic, grabbable, served;
            public Vector3 centroid, serveFrom, serveTo;
            public Quaternion rotation;
            public Quaternion serveFromRotation, serveToRotation;
        }
        [SerializeField] KitchenCookingProfile profile;
        [SerializeField] Transform[] curds;
        [SerializeField] bool texturedEggCurds;
        KitchenFoodVessel vessel;
        XRGrabInteractable panGrab;
        readonly Captured[] captured = new Captured[32];
        Vector3[] curdScales;
        MeshRenderer[][] curdRenderers;
        MaterialPropertyBlock curdTint;
        int count, servedCount;
        float lastHeat = -10, fold, foldPhase, serveTime;
        public float TomatoGrams { get; private set; }
        public float BeefGrams { get; private set; }
        public float OnionGrams { get; private set; }
        public float EggScrambleDistance { get; private set; }
        public float StirFryDistance { get; private set; }
        public float BeefOnionStirDistance { get; private set; }
        public int CapturedCount => count - servedCount;
        public bool BeefOnionPlated => servedCount > 0;
        public float CoagulatedFraction => vessel == null || profile == null ? 0 : Mathf.InverseLerp(
            profile.cookedSeconds * profile.coagulationStartsAt, profile.cookedSeconds, vessel.Batch.Cooking.Dose);
        public bool TomatoCooked
        {
            get
            {
                if (profile == null || TomatoGrams < profile.tomatoGrams * profile.requiredTomatoFraction) return false;
                for (var i = 0; i < count; i++)
                    if (captured[i].food.IngredientKind == KitchenIngredientKind.Tomato &&
                        captured[i].food.Stage != CookingStage.Cooked) return false;
                return true;
            }
        }
        public bool IsBurnt => TomatoEggBurnt || BeefStage == CookingStage.Burnt || OnionStage == CookingStage.Burnt;
        internal bool TomatoEggBurnt
        {
            get
            {
                if (vessel != null && vessel.Stage == CookingStage.Burnt) return true;
                for (var i = 0; i < count; i++)
                    if (captured[i].food.IngredientKind == KitchenIngredientKind.Tomato &&
                        captured[i].food.Stage == CookingStage.Burnt) return true;
                return false;
            }
        }
        public CookingStage BeefStage => IngredientStage(KitchenIngredientKind.Beef, BeefGrams);
        public CookingStage OnionStage => IngredientStage(KitchenIngredientKind.Onion, OnionGrams);

        CookingStage IngredientStage(KitchenIngredientKind kind, float grams)
        {
            if (grams <= 0f) return CookingStage.Empty;
            var stage = CookingStage.Cooked;
            for (var i = 0; i < count; i++)
            {
                var entry = captured[i];
                if (entry.served || entry.food.IngredientKind != kind) continue;
                var pieceStage = entry.food.Stage;
                if (pieceStage == CookingStage.Burnt) return CookingStage.Burnt;
                if (pieceStage != CookingStage.Cooked) stage = CookingStage.Raw;
            }
            return stage;
        }
        public void Configure(KitchenCookingProfile settings, Transform[] eggCurds, bool textured = false)
        { profile = settings; curds = eggCurds; texturedEggCurds = textured; }
        void Awake()
        {
            vessel = GetComponent<KitchenFoodVessel>();
            panGrab = GetComponent<XRGrabInteractable>();
            curdScales = new Vector3[curds.Length];
            curdRenderers = new MeshRenderer[curds.Length][]; curdTint = new MaterialPropertyBlock();
            for (var i = 0; i < curds.Length; i++) { curdScales[i] = curds[i].localScale; curds[i].gameObject.SetActive(false); }
            for (var i = 0; i < curds.Length; i++) curdRenderers[i] = curds[i].GetComponentsInChildren<MeshRenderer>(true);
        }
        void OnTriggerEnter(Collider other) => Receive(other);
        void OnTriggerStay(Collider other) => Receive(other);
        void Receive(Collider other)
        {
            if (other.attachedRigidbody == null) return;
            var food = other.attachedRigidbody.GetComponent<CookableIngredient>();
            if (food != null) TryReceive(food);
        }
        public bool TryReceive(CookableIngredient food)
        {
            if (food == null || !food.ReadyForPan || food.Grams <= 0 || count == captured.Length) return false;
            if (servedCount > 0 && food.IngredientKind != KitchenIngredientKind.Tomato) return false;
            for (var i = 0; i < count; i++) if (captured[i].food == food) return false;
            var body = food.GetComponent<Rigidbody>(); var grab = food.GetComponent<XRGrabInteractable>();
            if (body == null || body.isKinematic || (grab != null && grab.isSelected)) return false;
            var p = transform.InverseTransformPoint(body.worldCenterOfMass);
            if (p.x * p.x + p.z * p.z > vessel.InnerRadius * vessel.InnerRadius * .9f ||
                p.y < vessel.InnerBottom - .015f || p.y > vessel.InnerBottom + vessel.InnerHeight + .035f) return false;
            var entry = new Captured { food = food, body = body, grab = grab, parent = food.transform.parent,
                gravity = body.useGravity, kinematic = body.isKinematic, grabbable = grab != null && grab.enabled,
                colliders = food.GetComponentsInChildren<Collider>(true), centroid = body.centerOfMass,
                rotation = food.PresentationUp.sqrMagnitude > .01f ?
                    Quaternion.FromToRotation(food.PresentationUp, Vector3.up) :
                    Quaternion.Inverse(transform.rotation) * food.transform.rotation };
            entry.enabled = new bool[entry.colliders.Length];
            for (var i = 0; i < entry.colliders.Length; i++) { entry.enabled[i] = entry.colliders[i].enabled; entry.colliders[i].enabled = false; }
            if (grab != null) grab.enabled = false;
            if (!body.isKinematic) { body.velocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
            body.isKinematic = true; body.useGravity = false;
            food.transform.SetParent(transform, true);
            captured[count++] = entry;
            if (food.IngredientKind == KitchenIngredientKind.Tomato) TomatoGrams += food.Grams;
            else if (food.IngredientKind == KitchenIngredientKind.Beef) BeefGrams += food.Grams;
            else OnionGrams += food.Grams;
            if (food.IngredientKind != KitchenIngredientKind.Tomato) BeefOnionStirDistance = 0f;
            return true;
        }

        internal bool TryPlateBeefOnion(Transform plate, float plateRadius)
        {
            if (servedCount > 0 || plate == null || !plate.gameObject.activeInHierarchy ||
                panGrab == null || !panGrab.isSelected || Vector3.Dot(plate.up, Vector3.up) < .85f ||
                BeefStage != CookingStage.Cooked || OnionStage != CookingStage.Cooked) return false;
            var tilt = Vector3.Dot(transform.up, Vector3.up);
            if (tilt > .7071f || tilt < -.35f) return false;
            var downhill = Vector3.ProjectOnPlane(Vector3.down, transform.up).normalized;
            var lip = transform.position + downhill * vessel.InnerRadius +
                transform.up * (vessel.InnerBottom + vessel.InnerHeight);
            var target = plate.InverseTransformPoint(lip);
            if (target.y < .018f || target.y > .22f || target.x * target.x + target.z * target.z > plateRadius * plateRadius)
                return false;

            serveTime = 0f;
            for (var index = 0; index < count; index++)
            {
                var entry = captured[index];
                if (entry.food.IngredientKind == KitchenIngredientKind.Tomato) continue;
                entry.served = true;
                entry.food.transform.SetParent(plate, true);
                entry.serveFrom = entry.food.transform.localPosition;
                entry.serveFromRotation = entry.food.transform.localRotation;
                var angle = servedCount * 2.39996f;
                var radius = plateRadius * (.26f + .14f * (servedCount % 3));
                entry.serveToRotation = Quaternion.Euler(0, angle * Mathf.Rad2Deg + 35f, 0) * entry.rotation;
                entry.serveTo = new Vector3(Mathf.Cos(angle) * radius, .026f + .008f * (servedCount % 2),
                    Mathf.Sin(angle) * radius) - entry.serveToRotation * entry.centroid;
                servedCount++;
            }
            BeefGrams = OnionGrams = 0f;
            return true;
        }
        public void Heat(float intensity, float seconds)
        {
            lastHeat = Time.time;
            for (var i = 0; i < count; i++)
                if (!captured[i].served) captured[i].food.Heat(intensity, seconds);
        }
        public bool ContainsTip(Vector3 point)
        {
            var p = transform.InverseTransformPoint(point);
            return p.x * p.x + p.z * p.z < vessel.InnerRadius * vessel.InnerRadius &&
                p.y > vessel.InnerBottom - .008f && p.y < vessel.InnerBottom + vessel.InnerHeight + .025f;
        }
        public void Stir(float distance)
        {
            if (profile == null || !ContainsFood || distance <= 0 || float.IsInfinity(distance) || float.IsNaN(distance)) return;
            distance = Mathf.Min(distance, profile.maxSpatulaMetresPerStep);
            fold = .4f; foldPhase += distance * 14f;
            if (Time.time - lastHeat > .25f) return;
            if (BeefGrams > 0f && OnionGrams > 0f) BeefOnionStirDistance += distance;
            if (CoagulatedFraction >= .7f && vessel.Batch.MixedFraction >= .99f) EggScrambleDistance += distance;
            if (TomatoGrams >= profile.tomatoGrams * profile.requiredTomatoFraction &&
                EggScrambleDistance >= profile.scrambleMetres) StirFryDistance += distance;
        }
        bool ContainsFood => vessel != null && (vessel.Batch.Millilitres > .01f || CapturedCount > 0);
        void LateUpdate()
        {
            using (PresentationMarker.Auto()) UpdatePresentation();
        }
        void UpdatePresentation()
        {
            if (vessel == null || profile == null) return;
            var animateServe = servedCount > 0 && serveTime < 1f;
            if (animateServe) serveTime = Mathf.Min(1f, serveTime + Time.deltaTime / .24f);
            fold = Mathf.Max(0, fold - Time.deltaTime);
            var coagulated = CoagulatedFraction;
            var eggPresent = vessel.Batch.Millilitres > .01f;
            vessel.ShowSolidEgg(coagulated > .98f && eggPresent);
            for (var i = 0; i < curds.Length; i++)
            {
                var visible = eggPresent && coagulated > (i % 4) * .035f + .025f;
                curds[i].gameObject.SetActive(visible);
                if (!visible) continue;
                var angle = i * 2.39996f + foldPhase * .35f;
                var radius = vessel.InnerRadius * (.18f + .14f * (i % 4));
                var lift = fold > 0 ? Mathf.Abs(Mathf.Sin(fold * 10 + i)) * .018f * fold / .4f : 0;
                curds[i].localPosition = new Vector3(Mathf.Cos(angle) * radius, vessel.InnerBottom + .004f + lift, Mathf.Sin(angle) * radius);
                curds[i].localRotation = Quaternion.Euler(fold > 0 ? Mathf.Sin(foldPhase + i) * 35 : 0, angle * Mathf.Rad2Deg, 0);
                curds[i].localScale = curdScales[i] * Mathf.Lerp(.08f, 1, coagulated);
                curdTint.SetColor("_BaseColor", vessel.Stage == CookingStage.Burnt ? profile.burntTint :
                    texturedEggCurds ? Color.white : profile.cookedEgg);
                foreach (var renderer in curdRenderers[i]) renderer.SetPropertyBlock(curdTint);
            }
            for (var i = 0; i < count; i++)
            {
                var entry = captured[i];
                if (entry.served)
                {
                    if (animateServe)
                    {
                        var t = Mathf.SmoothStep(0f, 1f, serveTime);
                        entry.food.transform.SetLocalPositionAndRotation(
                            Vector3.Lerp(entry.serveFrom, entry.serveTo, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * .022f),
                            Quaternion.Slerp(entry.serveFromRotation, entry.serveToRotation, t));
                    }
                    continue;
                }
                var angle = i * 2.39996f + foldPhase * .38f;
                var radius = vessel.InnerRadius * (.24f + .13f * (i % 4));
                var centre = new Vector3(Mathf.Cos(angle) * radius, vessel.InnerBottom + .018f +
                    (fold > 0 ? Mathf.Abs(Mathf.Sin(fold * 10 + i)) * .018f : 0), Mathf.Sin(angle) * radius);
                var rotation = transform.rotation * Quaternion.Euler(0, angle * Mathf.Rad2Deg, fold > 0 ? Mathf.Sin(foldPhase + i) * 30 : 0) * entry.rotation;
                entry.body.transform.SetPositionAndRotation(transform.TransformPoint(centre) - rotation * entry.centroid, rotation);
            }
        }
        public void ResetContents()
        {
            for (var i = 0; i < count; i++)
            {
                var entry = captured[i]; entry.food.transform.SetParent(entry.parent, true);
                entry.body.isKinematic = entry.kinematic; entry.body.useGravity = entry.gravity;
                for (var j = 0; j < entry.colliders.Length; j++) entry.colliders[j].enabled = entry.enabled[j];
                if (entry.grab != null)
                {
                    entry.grab.SetTargetPose(new Pose(entry.food.transform.position, entry.food.transform.rotation));
                    entry.grab.enabled = entry.grabbable;
                }
                captured[i] = null;
            }
            count = servedCount = 0;
            BeefGrams = OnionGrams = TomatoGrams = EggScrambleDistance = StirFryDistance =
                BeefOnionStirDistance = fold = foldPhase = serveTime = 0;
            lastHeat = -10;
            if (vessel != null) vessel.ShowSolidEgg(false);
            if (curds != null) foreach (var curd in curds) curd.gameObject.SetActive(false);
        }
    }
}
