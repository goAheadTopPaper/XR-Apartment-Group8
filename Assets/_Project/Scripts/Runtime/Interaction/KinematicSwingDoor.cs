using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using XRApartment.Simulation;

namespace XRApartment.Runtime
{
    /// Kinematic fridge door (FR-04): selecting the door/handle toggles between closed and a
    /// fixed open angle. Motion is a transform rotation driven by MotionKinematics —
    /// deliberately not HingeJoint physics, so all three benchmarks see the same reproducible
    /// motion and the later sweep/clearance checks (FR-04) can evaluate deterministic poses.
    /// Attach to the hinge pivot; the door panel (mesh + collider + handle) hangs below it.
    [RequireComponent(typeof(XRSimpleInteractable))]
    public class KinematicSwingDoor : MonoBehaviour
    {
        [SerializeField] private float openAngleDegrees = 110f;
        [Tooltip("-1: panel extends +x from the hinge and opens toward the viewer; +1: mirrored hinge")]
        [SerializeField] private float swingSign = -1f;
        [SerializeField, Min(0.05f)] private float openDurationSeconds = 0.8f;
        [SerializeField] private bool startOpen = false;

        private XRSimpleInteractable interactable;
        private bool openTarget;
        private double progress; // 0 closed .. 1 open

        public bool IsOpen => openTarget;
        public double Progress => progress;

        private void Awake()
        {
            interactable = GetComponent<XRSimpleInteractable>();
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
            // Target flips immediately; Update eases progress toward it, so re-grabbing
            // mid-swing simply reverses direction instead of snapping.
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
            var angle = (float)MotionKinematics.SwingAngle(progress, openAngleDegrees, swingSign);
            transform.localRotation = Quaternion.Euler(0f, angle, 0f);
        }
    }
}
