using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using XRApartment.Simulation;

namespace XRApartment.Runtime
{
    /// Kinematic fridge drawer (FR-04): selecting the front slides it between closed and a
    /// fixed open distance along the appliance's local +Z (out toward the reviewer). Same
    /// reproducibility reasoning as KinematicSwingDoor: deterministic transform motion,
    /// no rigidbody physics. Attach to the drawer root that carries the front panel.
    [RequireComponent(typeof(XRSimpleInteractable))]
    public class KinematicLinearDrawer : MonoBehaviour
    {
        [SerializeField, Min(0.05f)] private float openDistanceMetres = 0.35f;
        [SerializeField, Min(0.05f)] private float openDurationSeconds = 0.6f;
        [SerializeField] private bool startOpen = false;

        private XRSimpleInteractable interactable;
        private Vector3 closedLocalPosition;
        private bool openTarget;
        private double progress; // 0 closed .. 1 open

        public bool IsOpen => openTarget;
        public double Progress => progress;

        private void Awake()
        {
            interactable = GetComponent<XRSimpleInteractable>();
            closedLocalPosition = transform.localPosition;
            openTarget = startOpen;
            progress = startOpen ? 1.0 : 0.0;
            Apply();
        }

        private void OnEnable()
        {
            interactable.selectEntered.AddListener(_ => Toggle());
        }

        private void OnDisable()
        {
            interactable.selectEntered.RemoveAllListeners();
        }

        public void Toggle() => SetOpen(!openTarget);

        public void SetOpen(bool open)
        {
            openTarget = open;
        }

        private void Update()
        {
            var target = openTarget ? 1.0 : 0.0;
            if (progress == target) return;

            var delta = openDurationSeconds <= 0f ? 1.0 : Time.deltaTime / openDurationSeconds;
            progress = MotionKinematics.StepToward(progress, target, delta);
            Apply();
        }

        private void Apply()
        {
            var distance = (float)MotionKinematics.SlideDistance(progress, openDistanceMetres);
            transform.localPosition = closedLocalPosition + Vector3.forward * distance;
        }
    }
}
