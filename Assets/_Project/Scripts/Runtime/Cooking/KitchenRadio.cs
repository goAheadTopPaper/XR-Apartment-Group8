using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace XRApartment.Runtime
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class KitchenRadio : MonoBehaviour
    {
        public const float DefaultVolume = .08f;
        const float VolumeStep = .04f;
        const float MaximumVolume = .32f;
        [SerializeField] XRSimpleInteractable power, volumeDown, volumeUp;
        [SerializeField] Renderer powerLight;
        AudioSource speaker;
        bool powered;

        public void Configure(XRSimpleInteractable powerButton, XRSimpleInteractable quieterButton,
            XRSimpleInteractable louderButton, Renderer lamp)
        {
            power = powerButton;
            volumeDown = quieterButton;
            volumeUp = louderButton;
            powerLight = lamp;
        }

        void Awake() => speaker = GetComponent<AudioSource>();

        void OnEnable()
        {
            power.selectEntered.AddListener(TogglePower);
            volumeDown.selectEntered.AddListener(Quieter);
            volumeUp.selectEntered.AddListener(Louder);
            ResetPlayback();
        }

        void OnDisable()
        {
            power.selectEntered.RemoveListener(TogglePower);
            volumeDown.selectEntered.RemoveListener(Quieter);
            volumeUp.selectEntered.RemoveListener(Louder);
            SetPowered(false);
        }

        internal void ResetPlayback()
        {
            if (speaker == null) return;
            speaker.volume = DefaultVolume;
            SetPowered(true);
        }

        void TogglePower(SelectEnterEventArgs _) => SetPowered(!powered);

        void SetPowered(bool on)
        {
            powered = on && speaker.clip != null;
            powerLight.enabled = powered;
            if (powered) speaker.Play();
            else speaker.Stop();
        }

        void Quieter(SelectEnterEventArgs _) => speaker.volume = Mathf.Max(0f, speaker.volume - VolumeStep);

        void Louder(SelectEnterEventArgs _) => speaker.volume = Mathf.Min(MaximumVolume, speaker.volume + VolumeStep);
    }
}
