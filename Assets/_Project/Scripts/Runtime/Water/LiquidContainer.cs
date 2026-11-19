using System.Collections.Generic;
using UnityEngine;
using XRApartment.Data;
using XRApartment.Simulation;

namespace XRApartment.Runtime
{
    [DisallowMultipleComponent]
    public sealed class LiquidContainer : MonoBehaviour
    {
        internal static readonly List<LiquidContainer> Active = new List<LiquidContainer>();
        [SerializeField] WaterInteractionProfile profile;
        [SerializeField] string parameterPrefix = "water_cup";
        [SerializeField, Range(0, 1)] float initialFill = 0.6f;
        [SerializeField] bool drain;
        [SerializeField] LiquidSurface surface;
        [SerializeField] LiquidStream stream;
        [SerializeField] bool fittedCavity;
        [SerializeField] VesselLiquidGeometry.Section[] sections;
        [SerializeField] bool spillWhenDisabled;
        [SerializeField] ParticleSystem steam;

        WaterVolume water;
        VesselLiquidGeometry geometry;
        VesselLiquidGeometry previousSurfaceGeometry;
        KitchenFoodVessel foodVessel;
        Vector3 interiorOrigin;
        Vector3 lastPosition, lastVelocity;
        Vector3 previousSurfaceNormal;
        Vector2 slosh, sloshVelocity;
        float previousSurfaceFraction, previousSurfaceOffset;
        const float AmbientCelsius = 20f;
        const float SteamStartCelsius = 45f;
        float lastHeat = -10f;
        public float TemperatureCelsius { get; private set; } = AmbientCelsius;
        public bool IsBoiling => Millilitres > VisibleMillilitres && TemperatureCelsius >= 99.9f;
        public bool HasHotSteam => Millilitres > VisibleMillilitres && TemperatureCelsius >= SteamStartCelsius;
        public void ConfigureSteam(ParticleSystem vapour) => steam = vapour;
        internal bool CanBoilOnStove => !drain && (parameterPrefix == "water_cooking_stockpot" ||
            (foodVessel != null && foodVessel.CanHeat));
        public float Millilitres => water == null ? 0 : water.Amount;
        public float CapacityMillilitres { get { Initialize(); return water == null ? 0f : water.Capacity; } }
        public bool IsDrain => drain;
        public float Radius => Geometry?.RadiusX ?? 0f;
        public float Bottom => InteriorOrigin.y;
        public float Height => Geometry?.Height ?? 0f;
        internal VesselLiquidGeometry Geometry { get { Initialize(); return geometry; } }
        internal Vector3 InteriorOrigin { get { Initialize(); return interiorOrigin; } }
        internal const float VisibleMillilitres = .05f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ClearRegistry() => Active.Clear();

        void OnEnable()
        {
            Active.Add(this);
            lastPosition = transform.position;
            lastVelocity = Vector3.zero;
        }

        void OnDisable()
        {
            Active.Remove(this);
            if (stream != null) stream.Stop();
            StopSteam();
            if (spillWhenDisabled && water != null)
            {
                var spilled = water.Remove(water.Amount);
                KitchenWaterSystem.InScene(gameObject.scene)?.RecordSpill(spilled);
            }
        }

        void Start() => Initialize();

        void Initialize()
        {
            if (water != null || profile == null) return;
            var radius = profile.Length(parameterPrefix + "_inner_radius");
            var radiusZ = fittedCavity ? profile.Length(parameterPrefix + "_depth_radius") : radius;
            var height = profile.Length(parameterPrefix + "_height");
            interiorOrigin = new Vector3(fittedCavity ? profile.Length(parameterPrefix + "_center_x") : 0f,
                profile.Length(parameterPrefix + "_bottom_y"), fittedCavity ? profile.Length(parameterPrefix + "_center_z") : 0f);
            geometry = new VesselLiquidGeometry(radius, radiusZ, height, sections);
            var scale = Mathf.Abs(transform.localToWorldMatrix.determinant);
            water = new WaterVolume(geometry.CapacityMillilitres * scale);
            water.Add(water.Capacity * initialFill);
            foodVessel = GetComponent<KitchenFoodVessel>();
            lastPosition = transform.position;
        }

        public float Receive(float millilitres)
        {
            Initialize();
            if (water == null || !isActiveAndEnabled || (foodVessel != null && foodVessel.HasEgg)) return 0f;
            var before = water.Amount;
            var accepted = water.Add(millilitres);
            if (accepted > 0f) TemperatureCelsius = Mathf.Lerp(TemperatureCelsius, AmbientCelsius, accepted / (before + accepted));
            return accepted;
        }

        internal void HeatWater(float seconds)
        {
            if (!CanBoilOnStove || Millilitres <= VisibleMillilitres) return;
            lastHeat = Time.time;
            // Responsive sandbox timing, with more water taking longer. No evaporation.
            var degreesPerSecond = Mathf.Clamp(2400f / Millilitres, 5f, 16f);
            TemperatureCelsius = Mathf.MoveTowards(TemperatureCelsius, 100f, degreesPerSecond * seconds);
        }

        public void ResetWater()
        {
            Initialize();
            if (stream != null) stream.Stop();
            StopSteam();
            if (water == null) return;
            water.Remove(water.Amount);
            water.Add(water.Capacity * initialFill);
            TemperatureCelsius = AmbientCelsius;
            lastHeat = -10f;
            previousSurfaceGeometry = null;
            slosh = sloshVelocity = Vector2.zero;
            lastPosition = transform.position;
            lastVelocity = Vector3.zero;
        }

        void FixedUpdate()
        {
            Initialize();
            if (water == null) return;
            float dt = Time.fixedDeltaTime;
            if (water.Amount <= VisibleMillilitres) TemperatureCelsius = AmbientCelsius;
            else if (Time.time - lastHeat > .1f)
                TemperatureCelsius = Mathf.MoveTowards(TemperatureCelsius, AmbientCelsius, .85f * dt);
            Vector3 velocity = (transform.position - lastPosition) / dt;
            var acceleration = Vector3.ClampMagnitude((velocity - lastVelocity) / dt, 15f);
            var localAcceleration = transform.InverseTransformDirection(acceleration);
            float frequency = profile.SloshFrequency;
            sloshVelocity += (-slosh * frequency * frequency - 2f * profile.SloshDamping * frequency * sloshVelocity
                - new Vector2(localAcceleration.x, localAcceleration.z) * 0.45f) * dt;
            slosh = Vector2.ClampMagnitude(slosh + sloshVelocity * dt, profile.MaximumSloshSlope);
            lastVelocity = velocity;
            lastPosition = transform.position;
            var world = KitchenWaterSystem.InScene(gameObject.scene);
            if (world == null || water.Amount <= 0f)
            {
                if (stream != null) stream.Stop();
                return;
            }
            if (drain)
            {
                world.RecordDrain(water.Remove(profile.TapMillilitresPerSecond * 2f * dt));
                return;
            }
            var up = LocalGravityNormal(transform);
            float retained = geometry.RetainedFraction(up.x, up.y, up.z);
            float excess = Mathf.Max(0f, water.Amount - retained * water.Capacity);
            float amount = Mathf.Min(excess, water.Capacity / profile.EmptyingSeconds * dt);
            if (amount <= 0.0001f) { if (stream != null) stream.Stop(); return; }
            var rim = LowestRim(geometry, up);
            var radial = Vector3.ProjectOnPlane(rim, Vector3.up).normalized;
            Vector3 lip = transform.TransformPoint(interiorOrigin + rim);
            Vector3 launch = Vector3.ClampMagnitude(velocity, 1.5f) + transform.TransformDirection(radial) * 0.18f + Vector3.down * 0.15f;
            if (stream != null && stream.Emit(world, lip, launch, amount, this)) water.Remove(amount);
        }

        void LateUpdate()
        {
            UpdateSteam();
            if (water == null || surface == null) return;
            if (water.Amount <= VisibleMillilitres)
            {
                surface.SetPlane(geometry, Vector3.up, 0f, false);
                return;
            }
            var normal = LocalGravityNormal(transform);
            normal = (normal + new Vector3(slosh.x, 0, slosh.y)).normalized;
            float fraction = water.Amount / water.Capacity;
            // The mesh caches its plane, but finding the offset also iterates over the cavity.
            if (geometry != previousSurfaceGeometry || !normal.Equals(previousSurfaceNormal) || fraction != previousSurfaceFraction)
            {
                previousSurfaceOffset = geometry.SurfaceOffset(normal.x, normal.y, normal.z, fraction);
                previousSurfaceGeometry = geometry;
                previousSurfaceNormal = normal;
                previousSurfaceFraction = fraction;
            }
            surface.SetPlane(geometry, normal, previousSurfaceOffset, true);
        }

        void UpdateSteam()
        {
            if (steam == null) return;
            var emitting = HasHotSteam;
            var emission = steam.emission;
            emission.enabled = emitting;
            if (!emitting) return;
            if (!steam.isPlaying) steam.Play();
            var intensity = Mathf.InverseLerp(SteamStartCelsius, 100f, TemperatureCelsius);
            emission.rateOverTime = Mathf.Lerp(6f, 18f, intensity);
            var main = steam.main;
            main.startColor = new Color(.94f, .96f, 1f, Mathf.Lerp(.16f, .34f, intensity));
        }

        void StopSteam()
        {
            if (steam == null) return;
            var emission = steam.emission;
            emission.enabled = false;
            steam.Clear();
        }

        internal bool CrossesMouth(Vector3 from, Vector3 to, out float fraction)
            => ProjectMouth(from, to, out fraction) && fraction <= 1f;

        // A solid grab proxy may cover the opening of a visually hollow vessel.
        // Only paths aimed through that vessel's measured mouth bypass its own proxy.
        internal bool ApproachesOpening(Vector3 from, Vector3 to) => fittedCavity && isActiveAndEnabled &&
            ProjectMouth(from, to, out _);

        bool ProjectMouth(Vector3 from, Vector3 to, out float fraction)
        {
            fraction = 0;
            Initialize();
            if (geometry == null || Vector3.Dot(transform.up, Vector3.up) <= 0f) return false;
            var a = transform.InverseTransformPoint(from) - interiorOrigin;
            var b = transform.InverseTransformPoint(to) - interiorOrigin;
            var rim = geometry.Height;
            if (a.y < rim || a.y <= b.y) return false;
            fraction = (a.y - rim) / (a.y - b.y);
            var p = Vector3.LerpUnclamped(a, b, fraction);
            return geometry.ContainsMouth(p.x, p.z);
        }

        internal static Vector3 LocalGravityNormal(Transform frame) =>
            frame.localToWorldMatrix.transpose.MultiplyVector(Vector3.up).normalized;

        static Vector3 LowestRim(VesselLiquidGeometry cavity, Vector3 up)
        {
            var direction = new Vector2(-up.x * cavity.RadiusX, -up.z * cavity.RadiusZ).normalized;
            if (direction == Vector2.zero) direction = Vector2.right;
            return new Vector3(direction.x * cavity.RadiusX, cavity.Height, direction.y * cavity.RadiusZ);
        }
    }
}
