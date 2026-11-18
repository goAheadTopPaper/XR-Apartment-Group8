using System;
using System.Linq;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using XRApartment.Data.ReviewV2;
namespace XRApartment.Runtime.ReviewV2
{
    public sealed class ReviewFeedbackCapture : MonoBehaviour
    {
        public ReviewSessionHost host;
        public ReviewIntegrationPanel panel;
        public NearFarInteractor rightRay;
        bool armed, completing;
        int releasedFrame;
        public bool IsCapturing { get; private set; }
        public event Action<bool> CaptureChanged;
        public void Begin()
        {
            if (!host.Snapshot.CanReview || !panel.markDraft.IsOpen) return;
            if (rightRay == null) rightRay = host.locomotion.Origin.GetComponentsInChildren<NearFarInteractor>(true)
                .FirstOrDefault(r => r.name.IndexOf("Right", StringComparison.OrdinalIgnoreCase) >= 0 || r.transform.parent.name.IndexOf("Right", StringComparison.OrdinalIgnoreCase) >= 0);
            if (rightRay == null) { panel.markDraft.ShowMessage("Right-hand ray unavailable. Check the simulator."); return; }
            armed = false; completing = false; releasedFrame = 0; IsCapturing = true;
            panel.panel.GetComponent<Canvas>().enabled = false;
            panel.markDraft.note.DeactivateInputField();
            SetCaptureHint(true); CaptureChanged?.Invoke(true);
        }
        public void Cancel()
        {
            if (!IsCapturing) return;
            IsCapturing = false;
            panel.panel.GetComponent<Canvas>().enabled = true;
            SetCaptureHint(false);
            CaptureChanged?.Invoke(false);
        }
        void SetCaptureHint(bool show)
        {
            if (panel.quickBar == null) return;
            foreach (var b in new[] { panel.quickBrief, panel.quickMark, panel.quickReview, panel.quickMenu }) if (b != null) b.gameObject.SetActive(!show);
            panel.quickBar.SetActive(show);
            if (show) panel.quickStatus.text = "Point at the kitchen. Release, then press the right trigger to confirm. U: Cancel";
        }
        void Update()
        {
            if (!IsCapturing) return;
            if (!host.Snapshot.CanReview || !panel.markDraft.Matches(host.Snapshot)) { Cancel(); return; }
            bool pressed = rightRay.uiPressInput.ReadIsPerformed();
            if (completing)
            {
                // Keep the canvas hidden through the confirmation release. Otherwise that release
                // clicks the capture button beneath the pointer and immediately starts capture again.
                if (pressed) return;
                if (releasedFrame == 0) { releasedFrame = Time.frameCount; return; }
                if (Time.frameCount > releasedFrame) Cancel();
                return;
            }
            if (!pressed) { armed = true; return; }
            if (!armed) return;
            armed = false;
            var origin = rightRay.curveOrigin;
            if (origin == null || !Physics.Raycast(origin.position, origin.forward, out var hit, 15f, ~((1 << 2) | (1 << 5)), QueryTriggerInteraction.Ignore) || hit.collider == null ||
                hit.collider.gameObject.scene != host.station.gameObject.scene) return;
            var identity = hit.collider.GetComponentInParent<ReviewSceneIdentity>();
            string id = identity != null && host.Resolve(identity.Id) == identity.transform ? identity.Id : null;
            panel.markDraft.SetCapturedPoint(hit.point, id);
            completing = true;
        }
        void OnDisable() => Cancel();
    }
}
