using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace XRApartment.Runtime
{
    /// In-room demo menu: return-to-start, pause, smooth-locomotion toggle. Buttons are
    /// resolved by child name so the editor builder and this controller stay decoupled.
    /// Pause zeroes simulation time (kinematic doors, physics) while XRI events keep the
    /// menu itself operable; labels always reflect the current state. Movement changes
    /// go through the same preference controller as the player's movement settings panel.
    [RequireComponent(typeof(Canvas))]
    public class KitchenMenuController : MonoBehaviour
    {

        [SerializeField] private string resetButtonName = "BTN_Reset";
        [SerializeField] private string pauseButtonName = "BTN_Pause";
        [SerializeField] private string locomotionButtonName = "BTN_Locomotion";

        private XROrigin rig;
        private Vector3 startPosition;
        private Quaternion startRotation;
        private KitchenLocomotionSettings locomotion;
        private Button pauseButton;
        private Button locomotionButton;

        private void Awake()
        {
            rig = Object.FindFirstObjectByType<XROrigin>();
            if (rig != null)
            {
                startPosition = rig.transform.position;
                startRotation = rig.transform.rotation;
                locomotion = rig.GetComponent<KitchenLocomotionSettings>();
                if (locomotion != null) locomotion.Changed += RefreshLabels;
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
            if (locomotion != null) locomotion.SetMode(locomotion.Mode == KitchenMovementMode.Smooth
                ? KitchenMovementMode.Teleport : KitchenMovementMode.Smooth);
            RefreshLabels();
        }

        private void OnDestroy()
        {
            if (locomotion != null) locomotion.Changed -= RefreshLabels;
        }

        private void RefreshLabels()
        {
            SetLabel(pauseButton, Time.timeScale == 0f ? "继续" : "暂停");
            SetLabel(locomotionButton, locomotion == null
                ? "移动设置：不可用"
                : locomotion.Mode == KitchenMovementMode.Smooth ? "移动：平移" : "移动：瞬移");
        }

        private static void SetLabel(Button button, string text)
        {
            if (button == null) return;
            var label = button.GetComponentInChildren<Text>(true);
            if (label != null) label.text = text;
        }
    }
}
