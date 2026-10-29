using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using XRApartment.Simulation;
using XRApartment.Data;

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
        [SerializeField] private BoxCollider sweepProbe;
        [SerializeField] private HandInteractionProfile collisionProfile;
        [SerializeField, Range(.1f, 5f)] private float sweepStepDegrees = .5f;

        private XRSimpleInteractable interactable;
        private bool openTarget;
        private double progress; // 0 closed .. 1 open
        private HeldPropSweep sweep;

        public bool IsOpen => openTarget;
        public double Progress => progress;

        private void Awake()
        {
            interactable = GetComponent<XRSimpleInteractable>();
            openTarget = startOpen;
            progress = startOpen ? 1.0 : 0.0;
            if (sweepProbe != null && collisionProfile != null)
                sweep = new HeldPropSweep(sweepProbe, transform, collisionProfile.ContactLayers);
            Apply();
        }

        private void OnEnable()
        {
            interactable.selectEntered.AddListener(OnSelected);
        }

        private void OnDisable()
        {
            interactable.selectEntered.RemoveListener(OnSelected);
        }

        private void OnSelected(SelectEnterEventArgs _) => Toggle();

        public void Toggle() => SetOpen(!openTarget);

        public void SetOpen(bool open)
        {
            // Target flips immediately; Update eases progress toward it, so re-grabbing
            // mid-swing simply reverses direction instead of snapping.
            openTarget = open;
        }

        public void SetOpenImmediate(bool open)
        {
            openTarget = open;
            progress = open ? 1.0 : 0.0;
            Apply();
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
            var rotation = Quaternion.Euler(0f, angle, 0f);
            if (sweep == null) { transform.localRotation = rotation; return; }
            var parentRotation = transform.parent == null ? Quaternion.identity : transform.parent.rotation;
            // A thin door close to its frame needs finer angular coverage than
            // a held pot. Subdivide the same conservative sweep, never skip it.
            var step = Mathf.Min(collisionProfile.GrabRotationStepDegrees, sweepStepDegrees);
            var accepted = sweep.Resolve(new Pose(transform.position, transform.rotation),
                new Pose(transform.position, parentRotation * rotation), null,
                collisionProfile.Length("grab_contact_skin"), step,
                Mathf.Max(collisionProfile.GrabRotationIterations, Mathf.CeilToInt(openAngleDegrees / step) + 1));
            // A door has a fixed hinge; overlap recovery must not translate it.
            if ((accepted.position - transform.position).sqrMagnitude < .000001f)
                transform.rotation = accepted.rotation;
            // Keep the timeline when unobstructed. Feeding an eased angle back
            // as linear time would ease twice and stall the door near closed.
            if (Quaternion.Angle(transform.rotation, parentRotation * rotation) > .01f)
            {
                var acceptedAngle = Mathf.DeltaAngle(0, transform.localEulerAngles.y);
                progress = MotionKinematics.InverseSmoothStep(acceptedAngle / (openAngleDegrees * swingSign));
            }
        }
    }
}
