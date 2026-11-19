using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using XRApartment.Data;

namespace XRApartment.Runtime
{
    public sealed class WaterTap : MonoBehaviour
    {
        [SerializeField] WaterInteractionProfile profile;
        [SerializeField] Transform outlet;
        [SerializeField] Transform lever;
        [SerializeField] XRSimpleInteractable control;
        [SerializeField] LiquidStream stream;
        [SerializeField] bool running;
        Quaternion closedRotation;
        AudioSource flowSound;
        public bool Running => running;
        public Vector3 OutletPosition => outlet.position;

        void Awake() { if (lever != null) closedRotation = lever.localRotation; }
        void OnEnable()
        {
            if (control != null) control.selectEntered.AddListener(Toggle);
            if (running) PlayFlowSound();
        }

        void OnDisable()
        {
            if (control != null) control.selectEntered.RemoveListener(Toggle);
            StopFlow();
        }

        void Toggle(SelectEnterEventArgs _) => SetRunning(!running);

        public void SetRunning(bool value)
        {
            var changed = running != value;
            running = value;
            if (lever != null) lever.localRotation = closedRotation * Quaternion.Euler(value ? -35f : 0f, 0, 0);
            if (!running) StopFlow();
            else if (changed && isActiveAndEnabled) PlayFlowSound();
        }

        void PlayFlowSound()
        {
            if (outlet == null || profile == null || stream == null) return;
            if (flowSound == null)
            {
                var clip = Resources.Load<AudioClip>("KitchenAudio/SFX_Faucet");
                if (clip == null) return;
                flowSound = outlet.gameObject.AddComponent<AudioSource>();
                flowSound.playOnAwake = false;
                flowSound.clip = clip;
                flowSound.loop = true;
                flowSound.spatialBlend = 1f;
                flowSound.minDistance = .8f;
                flowSound.maxDistance = 5f;
                flowSound.rolloffMode = AudioRolloffMode.Linear;
                flowSound.dopplerLevel = 0f;
                flowSound.volume = .48f;
            }
            flowSound.Play();
        }

        void StopFlow()
        {
            if (stream != null) stream.Stop();
            if (flowSound != null) flowSound.Stop();
        }

        void FixedUpdate()
        {
            if (!running || profile == null || stream == null || outlet == null) return;
            var world = KitchenWaterSystem.InScene(gameObject.scene);
            if (world == null) return;
            stream.Emit(world, outlet.position, Vector3.down * 0.45f, profile.TapMillilitresPerSecond * Time.fixedDeltaTime, null);
        }
    }
}
