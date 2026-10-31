using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;

namespace XRApartment.Runtime
{
    /// <summary>
    /// Keeps XRI's device manipulation, but sends FPS walking through the rig's collision-aware locomotion.
    /// Only the project's Editor simulator prefab uses this component; tracked headset poses are untouched.
    /// </summary>
    [DefaultExecutionOrder(XRInteractionUpdateOrder.k_InteractionSimulator)]
    public sealed class KitchenInteractionSimulator : XRInteractionSimulator
    {
        readonly XROriginMovement walking = new XROriginMovement();
        Camera cachedCamera;
        XRBodyTransformer bodyTransformer;
        CharacterController character;
        KitchenLocomotionSettings locomotion;
        bool warnedAboutRig;

        protected override void ProcessPoseInput()
        {
            if (!manipulatingFPS || Time.time <= 1f)
            {
                base.ProcessPoseInput();
                return;
            }

            var camera = Camera.main;
            if (cachedCamera != camera || bodyTransformer == null || character == null)
            {
                cachedCamera = camera;
                var origin = camera != null ? camera.GetComponentInParent<XROrigin>() : null;
                bodyTransformer = origin != null ? origin.GetComponentInChildren<XRBodyTransformer>() : null;
                character = origin != null ? origin.Origin.GetComponent<CharacterController>() : null;
                locomotion = origin != null ? origin.GetComponent<KitchenLocomotionSettings>() : null;
            }

            var speeds = new Vector3(translateXSpeed, translateYSpeed, translateZSpeed);
            var axes = new Vector3(translateXInput.ReadValue(), translateYInput.ReadValue(), translateZInput.ReadValue());
            if (toggleMouseInput.ReadIsPerformed())
            {
                // XRI 3.5 on Unity 2022 uses platform-specific scroll deltas (20 on Mac, 120 on Windows).
                var divisor = Application.platform == RuntimePlatform.OSXEditor ||
                    Application.platform == RuntimePlatform.OSXPlayer ? 20f : 120f;
                axes.z += mouseScrollInput.ReadValue().y / divisor;
            }

            var motion = Vector3.zero;
            if (camera != null)
            {
                var up = camera.transform.parent != null ? camera.transform.parent.up : Vector3.up;
                var forward = camera.transform.forward;
                if (Mathf.Approximately(Mathf.Abs(Vector3.Dot(forward, up)), 1f)) forward = -camera.transform.up;
                forward = Vector3.ProjectOnPlane(forward, up);
                motion = Quaternion.LookRotation(forward, up) * Vector3.Scale(axes, speeds) *
                    (bodyTranslateMultiplier * Time.deltaTime);
            }

            // Let the package handle rotation, point-and-click, tracking, and controller/hand modes.
            // Suppressing only FPS translation avoids moving simulated head poses around the capsule.
            try
            {
                translateXSpeed = translateYSpeed = translateZSpeed = 0f;
                base.ProcessPoseInput();
            }
            finally
            {
                translateXSpeed = speeds.x;
                translateYSpeed = speeds.y;
                translateZSpeed = speeds.z;
            }

            if (motion == Vector3.zero || (locomotion != null && locomotion.MenuOpen)) return;
            if (bodyTransformer == null || !bodyTransformer.isActiveAndEnabled ||
                character == null || !character.enabled || bodyTransformer.constrainedBodyManipulator == null)
            {
                if (!warnedAboutRig)
                {
                    Debug.LogWarning("Kitchen simulator walking requires an active XR Body Transformer and CharacterController.", this);
                    warnedAboutRig = true;
                }
                return;
            }

            walking.motion = motion;
            bodyTransformer.QueueTransformation(walking);
        }
    }
}
