using System.Collections.Generic;
using UnityEngine;
using XRApartment.Data;
using XRApartment.Simulation;

namespace XRApartment.Runtime
{
    /// Finite-volume egg liquid. A gravity plane and a short ballistic stream are visual approximations.
    public sealed class KitchenFoodVessel : MonoBehaviour
    {
        static readonly List<KitchenFoodVessel> Active = new List<KitchenFoodVessel>();
        [SerializeField] KitchenCookingProfile profile;
        [SerializeField] float radius = .065f, bottom = .01f, height = .05f;
        [SerializeField] bool canHeat;
        [SerializeField] LiquidSurface surface;
        [SerializeField] Transform[] yolks;
        [SerializeField] LineRenderer stream;
        [SerializeField] ParticleSystem vapour;
        MaterialPropertyBlock colour;
        readonly Vector3[] arc = new Vector3[25];
        MeshRenderer surfaceRenderer;
        EggBatch batch;
        float lastHeat = -10f;
        Vector3 previousPosition;
        KitchenPanContents panContents;
        bool solidEggVisible;
        public EggBatch Batch { get { Initialize(); return batch; } }
        public CookingStage Stage => Batch == null ? CookingStage.Empty : Batch.Stage;
        public float SpilledMillilitres { get; private set; }
        public bool CanHeat => canHeat;
        public float InnerRadius => radius;
        public float InnerBottom => bottom;
        public float InnerHeight => height;
        public KitchenPanContents PanContents => panContents;
        public void ShowSolidEgg(bool show) => solidEggVisible = show;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRegistry() => Active.Clear();

        public void Configure(KitchenCookingProfile settings, float innerRadius, float innerBottom,
            float innerHeight, bool heated, LiquidSurface liquid, Transform[] rawYolks,
            LineRenderer pouring, ParticleSystem steam)
        {
            profile = settings; radius = innerRadius; bottom = innerBottom; height = innerHeight;
            canHeat = heated; surface = liquid; yolks = rawYolks; stream = pouring; vapour = steam;
        }

        void Initialize()
        {
            if (batch != null || profile == null) return;
            batch = new EggBatch(Mathf.PI * radius * radius * height * 1000000f,
                profile.cookedSeconds, profile.burntSeconds);
            colour = new MaterialPropertyBlock();
            if (surface != null) surfaceRenderer = surface.GetComponent<MeshRenderer>();
            previousPosition = transform.position;
            panContents = GetComponent<KitchenPanContents>();
        }

        void OnEnable() { if (!Active.Contains(this)) Active.Add(this); }
        void OnDisable() { Active.Remove(this); if (stream != null) stream.enabled = false; }
        void Start() => Initialize();

        public float ReceiveEgg(float amount)
        {
            Initialize();
            var accepted = batch == null ? 0 : batch.AddRaw(amount);
            SpilledMillilitres += Mathf.Max(0, amount - accepted);
            return accepted;
        }

        public bool ContainsStirTip(Vector3 point)
        {
            Initialize();
            if (batch == null || batch.Millilitres < .01f || !batch.IsPourable) return false;
            var p = transform.InverseTransformPoint(point);
            var depth = height * batch.Millilitres / batch.Capacity;
            return p.x * p.x + p.z * p.z < radius * radius &&
                p.y >= bottom - .008f && p.y <= bottom + Mathf.Max(.02f, depth + .02f);
        }

        public void Stir(float distance)
        {
            Initialize();
            if (batch != null) batch.Whisk(Mathf.Min(distance, profile.maxWhiskMetresPerStep), profile.whiskMetres);
        }

        public void Heat(float intensity, float seconds)
        {
            Initialize();
            if (!canHeat || batch == null) return;
            batch.Heat(intensity, seconds);
            if (panContents != null) panContents.Heat(intensity, seconds);
            if (batch.Millilitres > .01f && intensity > 0) lastHeat = Time.time;
        }

        public static KitchenFoodVessel ReceiverBelow(Vector3 egg, float maximumDrop)
        {
            KitchenFoodVessel result = null;
            var closest = maximumDrop;
            foreach (var vessel in Active)
            {
                vessel.Initialize();
                if (vessel.batch == null || !vessel.batch.IsPourable ||
                    Vector3.Dot(vessel.transform.up, Vector3.up) < .7f) continue;
                var local = vessel.transform.InverseTransformPoint(egg);
                var drop = local.y - vessel.bottom - vessel.height;
                if (drop < -.015f || drop > closest || local.x * local.x + local.z * local.z > vessel.radius * vessel.radius) continue;
                closest = Mathf.Max(0, drop); result = vessel;
            }
            return result;
        }

        public static KitchenFoodVessel StirAt(Vector3 tip)
        {
            foreach (var vessel in Active) if (vessel.ContainsStirTip(tip)) return vessel;
            return null;
        }

        void FixedUpdate()
        {
            Initialize();
            if (batch == null) return;
            var velocity = Vector3.ClampMagnitude((transform.position - previousPosition) / Time.fixedDeltaTime, 1.5f);
            previousPosition = transform.position;
            if (stream != null) stream.enabled = false;
            if (!batch.IsPourable || batch.Millilitres <= .01f) return;
            var up = transform.InverseTransformDirection(Vector3.up);
            var retained = CylindricalLiquid.RetainedFraction(radius, height, up.x, up.y, up.z);
            var amount = Mathf.Min(Mathf.Max(0, batch.Millilitres - retained * batch.Capacity),
                batch.Capacity / profile.emptyingSeconds * Time.fixedDeltaTime);
            if (amount < .001f) return;
            var radial = new Vector3(-up.x, 0, -up.z).normalized;
            if (radial.sqrMagnitude < .1f) radial = Vector3.right;
            var origin = transform.TransformPoint(radial * radius + Vector3.up * (bottom + height));
            var launch = velocity + transform.TransformDirection(radial) * .12f + Vector3.down * .1f;
            KitchenFoodVessel receiver = null;
            arc[0] = origin;
            var count = 1;
            for (var i = 1; i < arc.Length; i++)
            {
                var t = i * .025f;
                var point = origin + launch * t + Physics.gravity * (.5f * t * t);
                var earliest = 2f;
                foreach (var candidate in Active)
                {
                    if (candidate == this || !candidate.CrossesMouth(arc[i - 1], point, out var fraction) || fraction >= earliest) continue;
                    receiver = candidate; earliest = fraction;
                }
                arc[i] = receiver == null ? point : Vector3.Lerp(arc[i - 1], point, earliest);
                count++;
                if (receiver != null) break;
            }
            var accepted = receiver == null ? 0 : batch.TransferTo(receiver.Batch, amount);
            var spilled = batch.Remove(amount - accepted);
            SpilledMillilitres += spilled;
            if (stream != null)
            {
                stream.enabled = true; stream.positionCount = count;
                for (var i = 0; i < count; i++) stream.SetPosition(i, arc[i]);
            }
        }

        bool CrossesMouth(Vector3 from, Vector3 to, out float fraction)
        {
            Initialize(); fraction = 0;
            if (batch == null || !batch.IsPourable || Vector3.Dot(transform.up, Vector3.up) <= 0) return false;
            var a = transform.InverseTransformPoint(from); var b = transform.InverseTransformPoint(to);
            var rim = bottom + height;
            if (a.y < rim || b.y > rim || a.y <= b.y) return false;
            fraction = (a.y - rim) / (a.y - b.y);
            var point = Vector3.Lerp(a, b, fraction);
            return point.x * point.x + point.z * point.z < radius * radius;
        }

        void LateUpdate()
        {
            Initialize();
            if (batch == null || surface == null) return;
            var normal = batch.IsPourable ? transform.InverseTransformDirection(Vector3.up) : Vector3.up;
            var offset = CylindricalLiquid.SurfaceOffset(radius, height, normal.x, normal.y, normal.z, batch.Millilitres / batch.Capacity);
            surface.SetPlane(radius, height, normal, offset, batch.Millilitres > .01f && !solidEggVisible);
            var rawTint = Color.Lerp(profile.rawAlbumen, profile.beatenEgg, batch.MixedFraction);
            var coagulated = Mathf.InverseLerp(profile.cookedSeconds * profile.coagulationStartsAt,
                profile.cookedSeconds, batch.Cooking.Dose);
            var tint = batch.Stage == CookingStage.Burnt ? profile.burntTint : Color.Lerp(rawTint, profile.cookedEgg, coagulated);
            colour.SetColor("_Color", tint);
            if (surfaceRenderer != null) surfaceRenderer.SetPropertyBlock(colour);
            if (yolks != null)
                for (var i = 0; i < yolks.Length; i++)
                {
                    var visible = batch.Millilitres > i * profile.eggMillilitres && batch.MixedFraction < .98f && !solidEggVisible &&
                        batch.Stage != CookingStage.Burnt && normal.y > .85f;
                    yolks[i].gameObject.SetActive(visible);
                    if (!visible) continue;
                    var angle = i * 2.4f;
                    yolks[i].localPosition = new Vector3(Mathf.Cos(angle) * radius * .3f,
                        bottom + offset / Mathf.Max(.1f, normal.y) + .001f, Mathf.Sin(angle) * radius * .3f);
                    var scale = Mathf.Lerp(1, .05f, batch.MixedFraction);
                    yolks[i].localScale = new Vector3(.023f, .007f, .023f) * scale;
                }
            if (vapour != null)
            {
                var emission = vapour.emission;
                emission.enabled = Time.time - lastHeat < .15f && batch.Millilitres > .01f;
                var main = vapour.main;
                main.startColor = batch.Stage == CookingStage.Burnt ? profile.smoke : profile.steam;
            }
        }

        public void ResetFood()
        {
            Initialize(); if (batch != null) batch.Reset(); SpilledMillilitres = 0;
            previousPosition = transform.position; lastHeat = -10f;
            solidEggVisible = false;
            if (stream != null) stream.enabled = false;
            if (vapour != null) vapour.Clear();
        }
    }
}
