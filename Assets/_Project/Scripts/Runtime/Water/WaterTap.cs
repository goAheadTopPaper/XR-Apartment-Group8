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
        public bool Running => running;
        public Vector3 OutletPosition => outlet.position;

        void Awake() { if (lever != null) closedRotation = lever.localRotation; }
        void OnEnable() { if (control != null) control.selectEntered.AddListener(Toggle); }
        void OnDisable() { if (control != null) control.selectEntered.RemoveListener(Toggle); if (stream != null) stream.Stop(); }
        void Toggle(SelectEnterEventArgs _) => SetRunning(!running);

        public void SetRunning(bool value)
        {
            running = value;
            if (lever != null) lever.localRotation = closedRotation * Quaternion.Euler(value ? -35f : 0f, 0, 0);
            if (!value && stream != null) stream.Stop();
        }

        void FixedUpdate()
        {
            var world = KitchenWaterSystem.Instance;
            if (!running || world == null || profile == null || stream == null || outlet == null) return;
            stream.Emit(world, outlet.position, Vector3.down * 0.45f, profile.TapMillilitresPerSecond * Time.fixedDeltaTime, null);
        }
    }
}
