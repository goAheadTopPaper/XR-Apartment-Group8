using System;
using XRApartment.Data;

namespace XRApartment.Simulation
{
    /// Game-time exposure, not a temperature or food-safety calculation.
    public sealed class CookingProgress
    {
        public float CookedAt { get; }
        public float BurntAt { get; }
        public float Dose { get; private set; }
        public CookingStage Stage => Dose >= BurntAt ? CookingStage.Burnt :
            Dose >= CookedAt ? CookingStage.Cooked : CookingStage.Raw;

        public CookingProgress(float cookedAt, float burntAt)
        {
            if (!PositiveFinite(cookedAt) || !PositiveFinite(burntAt) || burntAt <= cookedAt)
                throw new ArgumentOutOfRangeException(nameof(burntAt));
            CookedAt = cookedAt;
            BurntAt = burntAt;
        }

        public void Advance(float intensity, float seconds)
        {
            if (!PositiveFinite(intensity) || !PositiveFinite(seconds)) return;
            Dose = Math.Min(BurntAt, Dose + Math.Min(1f, intensity) * seconds);
        }

        public void Restore(float dose)
        {
            Dose = PositiveFinite(dose) ? Math.Min(BurntAt, dose) : 0f;
        }

        public static bool PositiveFinite(float value) => value > 0f && !float.IsInfinity(value);
    }
}
