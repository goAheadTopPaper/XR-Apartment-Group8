using UnityEngine;

namespace XRApartment.Runtime
{
    /// Gas jets follow the burner switch, independently of food/steam feedback.
    public sealed class KitchenBurnerFlame : MonoBehaviour
    {
        [SerializeField] ParticleSystem jets;
        [SerializeField] Light burnerLight;
        public bool IsLit { get; private set; }
        public int ParticleCount => jets == null ? 0 : jets.particleCount;

        public void Configure(ParticleSystem particles, Light light)
        {
            jets = particles; burnerLight = light; SetLit(false);
        }

        void Awake() => SetLit(false);
        void OnDisable() => SetLit(false);

        public void SetLit(bool lit)
        {
            IsLit = lit && isActiveAndEnabled;
            if (jets != null)
            {
                if (IsLit && !jets.isPlaying) jets.Play(true);
                else if (!IsLit) jets.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            if (burnerLight != null) burnerLight.enabled = IsLit;
        }
    }
}
