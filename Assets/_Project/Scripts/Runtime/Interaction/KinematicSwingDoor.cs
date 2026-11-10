using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;
using XRApartment.Simulation;
using XRApartment.Data;

namespace XRApartment.Runtime
{
    /// Moves a fixed hinge through the same collision sweep for hand pulls and authored poses.
    /// Unconfigured doors retain their select-to-toggle behavior.
    [RequireComponent(typeof(XRSimpleInteractable))]
    public class KinematicSwingDoor : MonoBehaviour
    {
        [SerializeField] private float openAngleDegrees = 110f;
        [Tooltip("-1: panel extends +x from the hinge and opens toward the viewer; +1: mirrored hinge")]
        [SerializeField] private float swingSign = -1f;
        [SerializeField, Min(0.05f)] private float openDurationSeconds = 0.8f;
        [SerializeField] private bool startOpen = false;
        [SerializeField] private BoxCollider sweepProbe;
        [SerializeField] private BoxCollider handleSweepProbe;
        [SerializeField] private HandInteractionProfile collisionProfile;
        [SerializeField, Range(.1f, 5f)] private float sweepStepDegrees = .5f;
        [SerializeField] private Transform pullHandle;

        private XRSimpleInteractable interactable;
        private bool openTarget;
        private bool observeRequestedOpening;
        private double progress; // 0 closed .. 1 open
        private HeldPropSweep sweep;
        private HeldPropSweep handleSweep;
        private Transform pullHand;
        private HapticImpulsePlayer pullHaptics;
        private Vector3 handOffset, initialRadial;
        private float initialAngle, nextContactFeedback;
        private bool keepPulledAngle;

        public bool IsOpen => openTarget;
        public double Progress => progress;
        public Transform PullHandle => pullHandle;
        internal Collider OpeningBlocker { get; private set; }

        private void Awake()
        {
            interactable = GetComponent<XRSimpleInteractable>();
            openTarget = startOpen;
            progress = startOpen ? 1.0 : 0.0;
            if (sweepProbe != null && collisionProfile != null)
            {
                sweep = new HeldPropSweep(sweepProbe, transform, collisionProfile.ContactLayers);
                if (handleSweepProbe != null)
                    handleSweep = new HeldPropSweep(handleSweepProbe, transform, collisionProfile.ContactLayers);
            }
            Apply();
        }

        private void OnEnable()
        {
            interactable.selectEntered.AddListener(OnSelected);
            interactable.selectExited.AddListener(OnReleased);
        }

        private void OnDisable()
        {
            interactable.selectEntered.RemoveListener(OnSelected);
            interactable.selectExited.RemoveListener(OnReleased);
            pullHand = null;
            pullHaptics = null;
        }

        public void ConfigurePullHandle(Transform handle) => pullHandle = handle;

        private void OnSelected(SelectEnterEventArgs args)
        {
            if (pullHandle == null) { Toggle(); return; }
            pullHand = args.interactorObject.GetAttachTransform(interactable);
            if (pullHand == null) return;
            pullHaptics = args.interactorObject.transform.GetComponentInParent<HapticImpulsePlayer>();
            handOffset = pullHand.InverseTransformPoint(pullHandle.position);
            initialRadial = Radial(pullHandle.position);
            initialAngle = Angle;
            keepPulledAngle = true;
        }

        private void OnReleased(SelectExitEventArgs args)
        {
            if (pullHand == null) return;
            pullHand = null;
            pullHaptics = null;
            if (!args.isCanceled && Angle > 0f && Angle < 2.5f) SetOpen(false);
        }

        private float Angle => Mathf.Abs(Mathf.DeltaAngle(0f, transform.localEulerAngles.y));
        private Vector3 Radial(Vector3 worldPoint) => Vector3.ProjectOnPlane(worldPoint - transform.position,
            transform.parent == null ? Vector3.up : transform.parent.up);

        private void FollowPull()
        {
            if (Time.deltaTime <= 0f) return;
            var direction = Radial(pullHand.TransformPoint(handOffset));
            if (direction.sqrMagnitude < .0004f) return;
            var up = transform.parent == null ? Vector3.up : transform.parent.up;
            var desired = Mathf.Clamp(initialAngle + Vector3.SignedAngle(initialRadial, direction, up) * swingSign,
                0f, openAngleDegrees);
            var before = Angle;
            if (Mathf.Abs(desired - before) < .01f) return;
            var opening = desired > before;
            openTarget = desired > .01f;
            var speed = 2f * openAngleDegrees / Mathf.Max(.05f, openDurationSeconds);
            var next = Mathf.MoveTowards(before, desired, speed * Time.deltaTime);
            progress = MotionKinematics.InverseSmoothStep(next / openAngleDegrees);
            Apply(observeOpening: opening);
            var after = Angle;
            if ((before <= .5f && after > .5f) || (before > .5f && after <= .5f))
            {
                KitchenAudio.Play(gameObject.scene, after > .5f ? KitchenCue.Unlatch : KitchenCue.Close, pullHandle.position);
                pullHaptics?.SendHapticImpulse(.18f, .05f);
            }
            else if (Mathf.Abs(after - next) > .4f && Time.unscaledTime >= nextContactFeedback)
            {
                nextContactFeedback = Time.unscaledTime + .24f;
                KitchenAudio.Play(gameObject.scene, KitchenCue.Impact, pullHandle.position, .35f);
                pullHaptics?.SendHapticImpulse(.24f, .04f);
            }
        }

        public void Toggle() => SetOpen(!openTarget);

        public void SetOpen(bool open)
        {
            // Target flips immediately; Update eases progress toward it, so re-grabbing
            // mid-swing simply reverses direction instead of snapping.
            if (openTarget != open) OpeningBlocker = null;
            pullHand = null;
            keepPulledAngle = false;
            openTarget = open;
            observeRequestedOpening = open;
        }

        public void SetOpenImmediate(bool open)
        {
            pullHand = null;
            keepPulledAngle = false;
            openTarget = open;
            observeRequestedOpening = false;
            progress = open ? 1.0 : 0.0;
            Apply();
        }

        private void Update()
        {
            if (pullHand != null) { FollowPull(); return; }
            if (keepPulledAngle) return;
            var target = openTarget ? 1.0 : 0.0;
            if (progress == target) return;

            var delta = openDurationSeconds <= 0f ? 1.0 : Time.deltaTime / openDurationSeconds;
            progress = MotionKinematics.StepToward(progress, target, delta);
            Apply(observeOpening: observeRequestedOpening);
        }

        private void Apply(bool observeOpening = false)
        {
            OpeningBlocker = null;
            var angle = (float)MotionKinematics.SwingAngle(progress, openAngleDegrees, swingSign);
            var rotation = Quaternion.Euler(0f, angle, 0f);
            if (sweep == null || !gameObject.activeInHierarchy) { transform.localRotation = rotation; return; }
            var parentRotation = transform.parent == null ? Quaternion.identity : transform.parent.rotation;
            // A thin door close to its frame needs finer angular coverage than
            // a held pot. Subdivide the same conservative sweep, never skip it.
            var step = Mathf.Min(collisionProfile.GrabRotationStepDegrees, sweepStepDegrees);
            var start = new Pose(transform.position, transform.rotation);
            var skin = collisionProfile.Length("grab_contact_skin");
            var iterations = Mathf.Max(collisionProfile.GrabRotationIterations,
                Mathf.CeilToInt(openAngleDegrees / step) + 1);
            var accepted = SweepRotation(sweep, start, parentRotation * rotation, skin, step, iterations,
                out var panelBlocker);
            // The pull projects from the far end of the leaf, not from the hinge.
            Collider handleBlocker = null;
            if (handleSweep != null)
                accepted = SweepRotation(handleSweep, start, accepted, skin, step, iterations, out handleBlocker);
            transform.rotation = accepted;
            var acceptedAngle = Mathf.DeltaAngle(0, transform.localEulerAngles.y);
            if (observeOpening && Mathf.Abs(Mathf.DeltaAngle(acceptedAngle, angle)) > .0001f &&
                !sweep.QuerySaturated && (handleSweep == null || !handleSweep.QuerySaturated))
                OpeningBlocker = handleBlocker ?? panelBlocker;
            // Keep the timeline when unobstructed. Feeding an eased angle back
            // as linear time would ease twice and stall the door near closed.
            if (Quaternion.Angle(transform.rotation, parentRotation * rotation) > .01f)
            {
                progress = MotionKinematics.InverseSmoothStep(acceptedAngle / (openAngleDegrees * swingSign));
            }
        }

        private static Quaternion SweepRotation(HeldPropSweep probe, Pose start, Quaternion rotation,
            float skin, float step, int iterations, out Collider blocker)
        {
            var accepted = probe.Resolve(start, new Pose(start.position, rotation), null, skin, step, iterations);
            // A door has a fixed hinge; overlap recovery must not translate it.
            if ((accepted.position - start.position).sqrMagnitude < .000001f)
            {
                blocker = probe.RotationBlocker;
                return accepted.rotation;
            }
            blocker = probe.RecoveryBlocker;
            return start.rotation;
        }
    }
}
