using UnityEngine;

namespace XRApartment.Runtime
{
    [RequireComponent(typeof(LineRenderer))]
    public sealed class LiquidStream : MonoBehaviour
    {
        const int Samples = 28;
        const float SampleTime = .035f;
        readonly Vector3[] points = new Vector3[Samples];
        LineRenderer line;
        void Awake()
        {
            line = GetComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.enabled = false;
        }

        public bool Emit(KitchenWaterSystem world, Vector3 origin, Vector3 velocity, float millilitres, LiquidContainer source)
        {
            if (!world.Emit(origin, velocity, millilitres, source)) { Stop(); return false; }
            if (line == null) return true;
            line.enabled = true;
            float rate = millilitres * 0.000001f / Mathf.Max(0.001f, Time.fixedDeltaTime);
            float width = 2f * Mathf.Sqrt(rate / (Mathf.PI * Mathf.Max(0.4f, velocity.magnitude)));
            line.startWidth = Mathf.Clamp(width, 0.002f, 0.014f);
            line.endWidth = line.startWidth * 0.6f;

            var gravity = Physics.gravity;
            // The renderer keeps its original resolution; trace two segments at once only when
            // the chord deviates from the parabola by at most 6.1 mm. Water volume uses its own solver.
            int stride = gravity.magnitude * (4f * SampleTime * SampleTime) / 8f <= .0061f ? 2 : 1;
            int count = Samples;
            points[0] = origin;
            for (int i = 1; i < Samples; ++i)
            {
                float time = i * SampleTime;
                points[i] = origin + velocity * time + gravity * (.5f * time * time);
            }
            for (int from = 0; from < Samples - 1; from += stride)
            {
                int to = Mathf.Min(Samples - 1, from + stride);
                if (!world.Trace(points[from], points[to], source, out var hit, out _, out _)) continue;
                // Refine only the hit span so the endpoint still follows a full-resolution hit.
                for (int i = from + 1; i <= to; ++i)
                {
                    if (!world.Trace(points[i - 1], points[i], source, out var exact, out _, out _)) continue;
                    hit = exact; to = i; break;
                }
                points[to] = hit;
                count = to + 1;
                break;
            }
            line.positionCount = count;
            line.SetPositions(points);
            return true;
        }

        public void Stop() { if (line != null) line.enabled = false; }
        void OnDisable() => Stop();
    }
}
