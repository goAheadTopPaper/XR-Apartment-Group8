using System;

namespace XRApartment.Simulation
{
    /// Conserved volume in ml. Visual particles never create or measure water.
    public sealed class WaterVolume
    {
        public float Capacity { get; }
        public float Amount { get; private set; }

        public WaterVolume(float capacity, float initialAmount = 0f)
        {
            if (float.IsNaN(capacity) || float.IsInfinity(capacity) || capacity <= 0f)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            Capacity = capacity;
            Add(initialAmount);
        }

        public float Add(float requested)
        {
            if (!PositiveFinite(requested)) return 0f;
            float accepted = Math.Min(requested, Capacity - Amount);
            Amount += accepted;
            return accepted;
        }

        public float Remove(float requested)
        {
            if (!PositiveFinite(requested)) return 0f;
            float removed = Math.Min(requested, Amount);
            Amount -= removed;
            return removed;
        }

        public float TransferTo(WaterVolume target, float requested)
        {
            if (target == null || ReferenceEquals(this, target) || !PositiveFinite(requested)) return 0f;
            return Remove(target.Add(Math.Min(requested, Amount)));
        }

        static bool PositiveFinite(float value) => value > 0f && !float.IsInfinity(value);
    }
}
