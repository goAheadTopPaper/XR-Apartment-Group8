using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using XRApartment.Data.ReviewV2;

namespace XRApartment.Runtime.ReviewV2
{
    // Replaceable M0 UI. The host is the only session command owner.
    public sealed class ReviewIntegrationPanel : MonoBehaviour
    {
        public ReviewSessionHost host;
        public GameObject panel;
        public TMP_Text status;
        public XRSimpleInteractable resetButton;
        public Button setup, onboarding, review, back, finish, pause, resume, recenter, reset, confirmReset, cancelReset, exit, close, teleport, smooth;
        InputAction toggle;
        bool confirming;
        string message = "";
        void Awake()
        {
            setup.onClick.AddListener(() => Send(CommandType.CompleteSetup));
            onboarding.onClick.AddListener(() => Send(CommandType.CompleteOnboarding));
            review.onClick.AddListener(() => Send(CommandType.Review));
            back.onClick.AddListener(() => Send(CommandType.Continue));
            finish.onClick.AddListener(() => Send(CommandType.Finish));
            pause.onClick.AddListener(() => Send(CommandType.Pause));
            resume.onClick.AddListener(() => Send(CommandType.Resume));
            recenter.onClick.AddListener(() => Send(CommandType.Recenter));
            exit.onClick.AddListener(() => Send(CommandType.SafeExit));
            reset.onClick.AddListener(AskReset);
            confirmReset.onClick.AddListener(() => { confirming = false; Send(CommandType.ResetWorld); });
            cancelReset.onClick.AddListener(() => { confirming = false; Render(host.Snapshot); });
            close.onClick.AddListener(() => SetOpen(false));
            teleport.onClick.AddListener(host.locomotion.SelectTeleport);
            smooth.onClick.AddListener(host.locomotion.SelectSmooth);
            toggle = new InputAction("V2 menu", InputActionType.Button);
            toggle.AddBinding("<Keyboard>/f8"); toggle.AddBinding("<XRController>{LeftHand}/secondaryButton");
            toggle.AddBinding("<XRController>{LeftHand}/menuButton"); toggle.performed += Toggle;
        }
        void Start() { SetOpen(true); Render(host.Snapshot); }
        void OnEnable()
        {
            toggle.Enable(); host.Changed += Render; host.OperationCompleted += Completed;
            if (resetButton != null) resetButton.selectEntered.AddListener(ResetSelected);
        }
        void OnDisable()
        {
            toggle.Disable(); host.Changed -= Render; host.OperationCompleted -= Completed;
            if (resetButton != null) resetButton.selectEntered.RemoveListener(ResetSelected);
            host.locomotion.SetMenuOpen(false);
        }
        void OnDestroy() { toggle.performed -= Toggle; toggle.Dispose(); }
        void Toggle(InputAction.CallbackContext _) => SetOpen(!panel.activeSelf);
        void ResetSelected(SelectEnterEventArgs _) => AskReset();
        void AskReset() { confirming = true; SetOpen(true); Render(host.Snapshot); }
        void Completed(OperationResult result) { message = result.Message; Render(host.Snapshot); }
        void Send(CommandType command)
        {
            var result = host.Execute(command); message = result.Accepted ? "" : result.Reason;
            if (result.Accepted && (command == CommandType.CompleteOnboarding || command == CommandType.Continue || command == CommandType.Resume)) SetOpen(false);
            Render(host.Snapshot);
        }
        public void SetOpen(bool open)
        {
            panel.SetActive(open);
            if (open)
            {
                var camera = host.locomotion.Origin.Camera.transform;
                var forward = Vector3.ProjectOnPlane(camera.forward, Vector3.up).normalized;
                if (forward.sqrMagnitude < .01f) forward = Vector3.forward;
                panel.transform.SetPositionAndRotation(camera.position + forward * 1.1f, Quaternion.LookRotation(forward));
            }
            ApplyMovement(host.Snapshot);
        }
        void ApplyMovement(SessionSnapshot snapshot) => host.locomotion.SetMenuOpen(panel.activeSelf || snapshot.IsPaused ||
            snapshot.IsBusy || snapshot.Phase == SessionPhase.Finished || snapshot.Phase == SessionPhase.Exited);
        void Render(SessionSnapshot s)
        {
            status.text = "REVIEW V2 / M0\n" + s.Phase + " | " + s.SchemeId + "\n" +
                "存储未接入，数据未保存 / Storage not connected; not saved\n" +
                "F8 / Left Y: menu. Explore, grab, teleport. Review at any time.\n" +
                (confirming ? "Reset objects and activity progress? Feedback keeps its original visit.\n" : "") + message;
            bool ready = !s.IsPaused && !s.IsBusy;
            setup.interactable = ready && s.Phase == SessionPhase.Setup;
            onboarding.interactable = ready && s.Phase == SessionPhase.Onboarding;
            review.interactable = s.CanReview;
            back.interactable = finish.interactable = ready && s.Phase == SessionPhase.Review;
            reset.interactable = ready && s.Phase == SessionPhase.Experience;
            confirmReset.gameObject.SetActive(confirming); cancelReset.gameObject.SetActive(confirming);
            confirmReset.interactable = reset.interactable;
            bool terminal = s.Phase == SessionPhase.Finished || s.Phase == SessionPhase.Exited;
            pause.interactable = ready && !terminal; resume.interactable = s.IsPaused && !s.IsBusy;
            exit.interactable = !terminal;
            ApplyMovement(s);
        }
    }
}
