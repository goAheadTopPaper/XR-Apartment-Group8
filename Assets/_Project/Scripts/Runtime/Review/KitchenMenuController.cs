using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;

namespace XRApartment.Runtime
{
    /// In-room demo menu: return-to-start, pause, smooth-locomotion toggle. Buttons are
    /// resolved by child name so the editor builder and this controller stay decoupled.
    /// Pause zeroes simulation time (kinematic doors, physics) while XRI events keep the
    /// menu itself operable; labels always reflect the current state. The smooth-move
    /// provider belongs to the Starter Assets sample, so it is looked up by type name
    /// instead of a compile-time reference — Samples are replaced wholesale on upgrades.
    [RequireComponent(typeof(Canvas))]
    public class KitchenMenuController : MonoBehaviour
    {
        const string SmoothMoveTypeName = "DynamicMoveProvider";

        [SerializeField] private string resetButtonName = "BTN_Reset";
        [SerializeField] private string pauseButtonName = "BTN_Pause";
        [SerializeField] private string locomotionButtonName = "BTN_Locomotion";

        private XROrigin rig;
        private Vector3 startPosition;
        private Quaternion startRotation;
        private Behaviour smoothMove;
        private Button pauseButton;
        private Button locomotionButton;

        private void Awake()
        {
            rig = Object.FindFirstObjectByType<XROrigin>();
            if (rig != null)
            {
                startPosition = rig.transform.position;
                startRotation = rig.transform.rotation;
                foreach (var behaviour in rig.GetComponentsInChildren<Behaviour>(true))
                {
                    if (behaviour != null && behaviour.GetType().Name == SmoothMoveTypeName)
                    {
                        smoothMove = behaviour;
                        break;
                    }
                }
            }

            WireButton(resetButtonName, ResetToStart);
            pauseButton = WireButton(pauseButtonName, TogglePause);
            locomotionButton = WireButton(locomotionButtonName, ToggleSmoothLocomotion);
            RefreshLabels();
        }

        private Button WireButton(string childName, UnityAction action)
        {
            var child = transform.Find(childName);
            if (child == null) return null;
            var button = child.GetComponent<Button>();
            if (button != null) button.onClick.AddListener(action);
            return button;
        }

        public void ResetToStart()
        {
            if (rig == null) return;
            rig.transform.SetPositionAndRotation(startPosition, startRotation);
        }

        public void TogglePause()
        {
            Time.timeScale = Time.timeScale == 0f ? 1f : 0f;
            RefreshLabels();
        }

        public void ToggleSmoothLocomotion()
        {
            if (smoothMove != null) smoothMove.enabled = !smoothMove.enabled;
            RefreshLabels();
        }

        private void RefreshLabels()
        {
            SetLabel(pauseButton, Time.timeScale == 0f ? "继续" : "暂停");
            SetLabel(locomotionButton, smoothMove == null
                ? "平滑移动：不可用"
                : smoothMove.enabled ? "平滑移动：开" : "平滑移动：关");
        }

        private static void SetLabel(Button button, string text)
        {
            if (button == null) return;
            var label = button.GetComponentInChildren<Text>(true);
            if (label != null) label.text = text;
        }
    }
}
