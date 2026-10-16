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

        WaterVolume water;
        float radius, height, bottom;
        Vector3 lastPosition, lastVelocity;
        Vector2 slosh, sloshVelocity;
        public float Millilitres => water == null ? 0 : water.Amount;
        public float CapacityMillilitres => water == null ? 0 : water.Capacity;
        public bool IsDrain => drain;
        public float Radius => radius;
        public float Bottom => bottom;
        public float Height => height;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ClearRegistry() => Active.Clear();

        void OnEnable() { if (!Active.Contains(this)) Active.Add(this); }
        void OnDisable() { Active.Remove(this); if (stream != null) stream.Stop(); }

        void Start() => Initialize();

        void Initialize()
        {
            if (water != null || profile == null) return;
            radius = profile.Length(parameterPrefix + "_inner_radius");
            bottom = profile.Length(parameterPrefix + "_bottom_y");
            height = profile.Length(parameterPrefix + "_height");
            water = new WaterVolume(Mathf.PI * radius * radius * height * 1000000f);
            water.Add(water.Capacity * initialFill);
            lastPosition = transform.position;
        }

        public float Receive(float millilitres)
        {
            Initialize();
            return water == null ? 0f : water.Add(millilitres);
        }

        public void ResetWater()
        {
            Initialize();
            if (water == null) return;
            water.Remove(water.Amount);
            water.Add(water.Capacity * initialFill);
            slosh = sloshVelocity = Vector2.zero;
            lastPosition = transform.position;
            lastVelocity = Vector3.zero;
        }

        void FixedUpdate()
        {
            Initialize();
            if (water == null) return;
            float dt = Time.fixedDeltaTime;
            Vector3 velocity = (transform.position - lastPosition) / dt;
            var acceleration = Vector3.ClampMagnitude((velocity - lastVelocity) / dt, 15f);
            var localAcceleration = transform.InverseTransformDirection(acceleration);
            float frequency = profile.SloshFrequency;
            sloshVelocity += (-slosh * frequency * frequency - 2f * profile.SloshDamping * frequency * sloshVelocity
                - new Vector2(localAcceleration.x, localAcceleration.z) * 0.45f) * dt;
            slosh = Vector2.ClampMagnitude(slosh + sloshVelocity * dt, profile.MaximumSloshSlope);
            lastVelocity = velocity;
            lastPosition = transform.position;
            var world = KitchenWaterSystem.Instance;
            if (world == null) return;
            if (drain)
            {
                world.RecordDrain(water.Remove(profile.TapMillilitresPerSecond * 2f * dt));
                return;
            }
            var up = transform.InverseTransformDirection(Vector3.up);
            float retained = CylindricalLiquid.RetainedFraction(radius, height, up.x, up.y, up.z);
            float excess = Mathf.Max(0f, water.Amount - retained * water.Capacity);
            float amount = Mathf.Min(excess, water.Capacity / profile.EmptyingSeconds * dt);
            if (amount <= 0.0001f) { if (stream != null) stream.Stop(); return; }
            var radial = new Vector3(-up.x, 0, -up.z).normalized;
            if (radial.sqrMagnitude < 0.1f) radial = Vector3.right;
            Vector3 lip = transform.TransformPoint(radial * radius + Vector3.up * (bottom + height));
            Vector3 launch = Vector3.ClampMagnitude(velocity, 1.5f) + transform.TransformDirection(radial) * 0.18f + Vector3.down * 0.15f;
            if (stream != null && stream.Emit(world, lip, launch, amount, this)) water.Remove(amount);
        }

        void LateUpdate()
        {
            if (water == null || surface == null) return;
            var normal = transform.InverseTransformDirection(Vector3.up);
            normal = (normal + new Vector3(slosh.x, 0, slosh.y)).normalized;
            float fraction = water.Amount / water.Capacity;
            float offset = CylindricalLiquid.SurfaceOffset(radius, height, normal.x, normal.y, normal.z, fraction);
            surface.SetPlane(radius, height, normal, offset, water.Amount > 0.05f);
        }

        internal bool CrossesMouth(Vector3 from, Vector3 to, out float fraction)
        {
            fraction = 0;
            if (water == null || Vector3.Dot(transform.up, Vector3.up) <= 0f) return false;
            var a = transform.InverseTransformPoint(from);
            var b = transform.InverseTransformPoint(to);
            float rim = bottom + height;
            if (a.y < rim || b.y > rim || a.y <= b.y) return false;
            fraction = (a.y - rim) / (a.y - b.y);
            var p = Vector3.Lerp(a, b, fraction);
            return p.x * p.x + p.z * p.z < radius * radius;
        }
    }
}
