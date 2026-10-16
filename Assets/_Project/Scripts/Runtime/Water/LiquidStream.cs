using UnityEngine;

namespace XRApartment.Runtime
{
    [RequireComponent(typeof(LineRenderer))]
    public sealed class LiquidStream : MonoBehaviour
    {
        const int Samples = 28;
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
            line.positionCount = Samples;
            line.SetPosition(0, origin);
            Vector3 previous = origin;
            float rate = millilitres * 0.000001f / Mathf.Max(0.001f, Time.fixedDeltaTime);
            float width = 2f * Mathf.Sqrt(rate / (Mathf.PI * Mathf.Max(0.4f, velocity.magnitude)));
            line.startWidth = Mathf.Clamp(width, 0.002f, 0.014f);
            line.endWidth = line.startWidth * 0.6f;
            for (int i = 1; i < Samples; i++)
            {
                float time = i * 0.035f;
                var next = origin + velocity * time + Physics.gravity * (0.5f * time * time);
                if (world.Trace(previous, next, source, out var point, out _, out _))
                {
                    line.SetPosition(i, point);
                    line.positionCount = i + 1;
                    return true;
                }
                line.SetPosition(i, next);
                previous = next;
            }
            return true;
        }

        public void Stop() { if (line != null) line.enabled = false; }
        void OnDisable() => Stop();
    }
}
