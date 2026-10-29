using System;
using XRApartment.Data;

namespace XRApartment.Simulation
{
    /// Conserved egg volume and mixing state; water has a separate runtime system.
    public sealed class EggBatch
    {
        readonly WaterVolume volume;
        public CookingProgress Cooking { get; }
        public float Millilitres => volume.Amount;
        public float Capacity => volume.Capacity;
        public float MixedFraction { get; private set; }
        public bool IsPourable => Cooking.Stage == CookingStage.Raw;
        public CookingStage Stage => Millilitres <= .0001f ? CookingStage.Empty :
            Cooking.Stage != CookingStage.Raw ? Cooking.Stage :
            MixedFraction >= .999f ? CookingStage.Beaten : CookingStage.Raw;

        public EggBatch(float capacity, float cookedAt, float burntAt)
        {
            volume = new WaterVolume(capacity);
            Cooking = new CookingProgress(cookedAt, burntAt);
        }

        public float AddRaw(float requested) => Receive(requested, 0f, 0f);

        float Receive(float requested, float mixed, float dose)
        {
            if (!IsPourable) return 0f;
            var before = Millilitres;
            var accepted = volume.Add(requested);
            if (accepted <= 0) return 0f;
            MixedFraction = (MixedFraction * before + mixed * accepted) / Millilitres;
            Cooking.Restore((Cooking.Dose * before + dose * accepted) / Millilitres);
            return accepted;
        }

        public void Whisk(float distance, float metresPerEgg)
        {
            if (Millilitres <= 0f || !IsPourable || !CookingProgress.PositiveFinite(distance) ||
                !CookingProgress.PositiveFinite(metresPerEgg)) return;
            MixedFraction = Math.Min(1f, MixedFraction + distance / (metresPerEgg * Math.Max(1f, Millilitres / 50f)));
        }

        public void Heat(float intensity, float seconds)
        {
            if (Millilitres > 0f) Cooking.Advance(intensity, seconds);
        }

        public float TransferTo(EggBatch target, float requested)
        {
            if (target == null || ReferenceEquals(this, target) || !IsPourable ||
                !CookingProgress.PositiveFinite(requested)) return 0f;
            var accepted = target.Receive(Math.Min(Millilitres, requested), MixedFraction, Cooking.Dose);
            Remove(accepted);
            return accepted;
        }

        public float Remove(float requested)
        {
            var removed = volume.Remove(requested);
            if (Millilitres <= .0001f) { MixedFraction = 0f; Cooking.Restore(0f); }
            return removed;
        }

        public void Reset() { volume.Remove(Millilitres); MixedFraction = 0f; Cooking.Restore(0f); }
    }
}
