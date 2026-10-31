using UnityEngine;

namespace XRApartment.Runtime
{
    /// Bounded ballistic volume parcels plus cosmetic streams/splashes. No SPH solver.
    [DefaultExecutionOrder(100)]
    public sealed class KitchenWaterSystem : MonoBehaviour
    {
        const int ParcelCapacity = 256;
        [SerializeField] ParticleSystem splashes;
        struct Parcel
        {
            public bool active;
            public Vector3 position, velocity;
            public float amount, age;
            public LiquidContainer source;
        }
        readonly Parcel[] parcels = new Parcel[ParcelCapacity];
        readonly RaycastHit[] hits = new RaycastHit[64];
        public static KitchenWaterSystem Instance { get; private set; }
        public float InjectedMillilitres { get; private set; }
        public float DrainedMillilitres { get; private set; }
        public float SpilledMillilitres { get; private set; }
        public int ActiveParcels { get; private set; }
        public float InFlightMillilitres
        {
            get { float total = 0; for (int i = 0; i < parcels.Length; i++) if (parcels[i].active) total += parcels[i].amount; return total; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ClearInstance() => Instance = null;

        void Awake()
        {
            Instance = this;
            var camera = Camera.main;
            if (camera != null) camera.depthTextureMode |= DepthTextureMode.Depth;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        public bool Emit(Vector3 position, Vector3 velocity, float millilitres, LiquidContainer source)
        {
            if (millilitres <= 0f || float.IsNaN(millilitres) || float.IsInfinity(millilitres)) return false;
            for (int i = 0; i < parcels.Length; i++)
            {
                if (parcels[i].active) continue;
                parcels[i] = new Parcel { active = true, position = position, velocity = velocity, amount = millilitres, source = source };
                ActiveParcels++;
                if (source == null) InjectedMillilitres += millilitres;
                return true;
            }
            return false; // Callers retain their volume when the fixed pool is full.
        }

        public void RecordDrain(float millilitres) => DrainedMillilitres += millilitres;

        public void ResetWater()
        {
            ClearFlow();
            foreach (var container in LiquidContainer.Active) if (container != null) container.ResetWater();
        }

        public void ClearFlow()
        {
            for (int i = 0; i < parcels.Length; i++) parcels[i] = default;
            ActiveParcels = 0;
            InjectedMillilitres = DrainedMillilitres = SpilledMillilitres = 0;
            if (splashes != null) splashes.Clear();
        }

        void FixedUpdate() => Simulate(Time.fixedDeltaTime);

        public void Simulate(float dt)
        {
            if (dt <= 0f) return;
            Vector3 gravity = Physics.gravity;
            for (int i = 0; i < parcels.Length; i++)
            {
                if (!parcels[i].active) continue;
                var p = parcels[i];
                Vector3 next = p.position + p.velocity * dt + gravity * (0.5f * dt * dt);
                if (Trace(p.position, next, p.source, out var point, out var normal, out var receiver))
                {
                    float accepted = receiver == null ? 0f : receiver.Receive(p.amount);
                    SpilledMillilitres += p.amount - accepted;
                    Splash(point, normal);
                    p.active = false;
                }
                else
                {
                    p.position = next;
                    p.velocity += gravity * dt;
                    p.age += dt;
                    if (p.age > 2f) { SpilledMillilitres += p.amount; p.active = false; }
                }
                if (!p.active) ActiveParcels--;
                parcels[i] = p;
            }
        }

        public bool Trace(Vector3 from, Vector3 to, LiquidContainer source, out Vector3 point, out Vector3 normal, out LiquidContainer receiver)
        {
            Vector3 delta = to - from;
            float length = delta.magnitude;
            point = to;
            normal = Vector3.up;
            receiver = null;
            if (length < 0.000001f) return false;
            float closest = 1.0001f;
            int count = Physics.RaycastNonAlloc(from, delta / length, hits, length, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (source != null && hit.transform.IsChildOf(source.transform)) continue;
                float t = hit.distance / length;
                if (t >= closest) continue;
                closest = t;
                point = hit.point;
                normal = hit.normal;
                receiver = null;
            }
            for (int i = 0; i < LiquidContainer.Active.Count; i++)
            {
                var container = LiquidContainer.Active[i];
                if (container == null || container == source || !container.CrossesMouth(from, to, out var t) || t >= closest) continue;
                closest = t;
                point = Vector3.Lerp(from, to, t);
                normal = container.transform.up;
                receiver = container;
            }
            return closest <= 1f;
        }

        void Splash(Vector3 point, Vector3 normal)
        {
            if (splashes == null) return;
            for (int i = 0; i < 4; i++)
            {
                var parameters = new ParticleSystem.EmitParams
                {
                    position = point,
                    velocity = normal * Random.Range(0.2f, 0.5f) + Random.insideUnitSphere * 0.12f
                };
                splashes.Emit(parameters, 1);
            }
        }

        void LateUpdate()
        {
            float t = Time.time * 0.008f;
            Shader.SetGlobalVector("_NvWatersMovement", new Vector4(t * 0.5f, t * 0.3f, t, -t * 0.6f));
        }
    }
}
