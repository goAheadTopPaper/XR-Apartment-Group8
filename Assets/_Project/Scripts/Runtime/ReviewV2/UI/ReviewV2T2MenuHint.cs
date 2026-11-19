using TMPro;
using UnityEngine;
using UnityEngine.XR;
using XRApartment.Data.ReviewV2;

namespace XRApartment.Runtime.ReviewV2.UI
{
    // Presentation only. The existing Integration Panel owns all menu input.
    public sealed class ReviewV2T2MenuHint : MonoBehaviour
    {
        [SerializeField] ReviewV2UiSessionAdapter session;
        [SerializeField] ReviewV2UiController controller;
        [SerializeField] Camera viewCamera;
        [SerializeField] TMP_Text label;
        void LateUpdate()
        {
            var snapshot = session != null ? session.Snapshot : null;
            bool visible = snapshot != null && snapshot.Phase == SessionPhase.Experience &&
                controller != null && !controller.AnyPanelVisible;
            if (label == null) return;
            label.gameObject.SetActive(visible);
            if (!visible || viewCamera == null) return;
            // Both left-hand secondary/menu controls are accepted by the existing action;
            // device-specific button names are intentionally not guessed.
            KitchenLocale.Set(label, XRSettings.isDeviceActive ? "Controller Menu" : "U  Menu");
            label.raycastTarget = false;
            var p = viewCamera.ViewportToWorldPoint(new Vector3(.18f, .10f, .8f));
            transform.SetPositionAndRotation(p, viewCamera.transform.rotation);
        }
        void OnDisable() { if (label != null) label.gameObject.SetActive(false); }
    }
}
