using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using XRApartment.Data;

namespace XRApartment.Runtime.UI
{
    public sealed class KitchenActivityGuide : MonoBehaviour
    {
        enum Step { Calibrate, Practice, Cook, Preview, Feedback, Done }

        [SerializeField] LocomotionSettingsView menu;
        [SerializeField] KitchenLocomotionSettings movement;
        [SerializeField] KitchenRecipeController recipe;
        [SerializeField] KitchenLayoutLab layout;
        [SerializeField] KitchenFeedbackNotes notes;
        [SerializeField] KitchenPauseMenu pauseMenu;
        [SerializeField] GameObject panel, calibration;
        [SerializeField] Button close, advance, standing, seated, lower, raise;
        [SerializeField] TMP_Text title, instruction, detail, status, actionLabel, hint;
        readonly HashSet<XRGrabInteractable> held = new HashSet<XRGrabInteractable>();
        XRGrabInteractable[] practiceItems;
        RectTransform advanceRect;
        Vector2 advancePosition;
        Step step;
        bool useSeated, calibrated, teleported, released, milestoneShown;
        float standingHeight = 1.65f, seatedHeight = 1.20f, nextRefresh;

        float EyeHeight => useSeated ? seatedHeight : standingHeight;
        bool Practiced => teleported && released;
        bool Compared => layout.HasObservedOpening(false) && layout.HasObservedOpening(true);
        bool ReadyForNextStep => step switch
        {
            Step.Practice => Practiced,
            Step.Cook => recipe.TomatoMealFinished,
            Step.Preview => Compared,
            _ => false
        };
        public bool IsFreePlay { get; private set; }
        internal bool AllowsLayout => !isActiveAndEnabled || IsFreePlay || step == Step.Preview;
        internal bool AllowsFeedback => !isActiveAndEnabled || IsFreePlay || step == Step.Feedback;
        internal bool RequiresChoice => isActiveAndEnabled && !IsFreePlay && (step == Step.Calibrate || step == Step.Done);
        internal string StageTitle => KitchenLocale.Source(title);
        internal string StageInstruction => KitchenLocale.Source(instruction);

        void Awake()
        {
            advanceRect = (RectTransform)advance.transform;
            advancePosition = advanceRect.anchoredPosition;
            close.onClick.AddListener(menu.Close);
            advance.onClick.AddListener(Advance);
            standing.onClick.AddListener(() => ChoosePosture(false));
            seated.onClick.AddListener(() => ChoosePosture(true));
            lower.onClick.AddListener(() => AdjustHeight(-.05f));
            raise.onClick.AddListener(() => AdjustHeight(.05f));
            notes.Saved += FeedbackSaved;
        }

        void Start() => Open();

        void OnDestroy()
        {
            StopPractice();
            if (notes != null) notes.Saved -= FeedbackSaved;
        }

        internal void Open()
        {
            milestoneShown |= ReadyForNextStep;
            KitchenLocale.Set(status, "");
            Refresh();
            menu.OpenAuxiliary(panel);
        }

        void ChoosePosture(bool sitting)
        {
            useSeated = sitting;
            calibrated = false;
            Refresh();
        }

        void AdjustHeight(float change)
        {
            if (useSeated) seatedHeight = Mathf.Clamp(seatedHeight + change, .90f, 1.95f);
            else standingHeight = Mathf.Clamp(standingHeight + change, .90f, 1.95f);
            calibrated = false;
            Refresh();
        }

        void Advance()
        {
            KitchenLocale.Set(status, "");
            var previous = step;
            switch (step)
            {
                case Step.Calibrate:
                    if (!calibrated) ApplyCalibration();
                    else if (IsFreePlay)
                    {
                        step = Step.Done;
                        menu.Close();
                    }
                    else BeginPractice();
                    break;
                case Step.Practice:
                    if (Practiced && recipe.SelectRecipe(KitchenRecipeChoice.TomatoEgg))
                    {
                        StopPractice();
                        step = Step.Cook;
                        menu.Close();
                    }
                    break;
                case Step.Cook:
                    if (recipe.ActiveRecipe != KitchenRecipeChoice.TomatoEgg)
                    {
                        if (recipe.SelectRecipe(KitchenRecipeChoice.TomatoEgg)) menu.Close();
                    }
                    else if (recipe.TomatoMealFinished) step = Step.Preview;
                    break;
                case Step.Preview:
                    if (Compared) step = Step.Feedback;
                    else menu.OpenLayout();
                    break;
                case Step.Feedback:
                    notes.OpenOrSpawn();
                    break;
                case Step.Done:
                    if (IsFreePlay)
                    {
                        step = Step.Calibrate;
                        calibrated = false;
                        menu.OpenAuxiliary(panel, recenter: true);
                    }
                    else
                    {
                        IsFreePlay = true;
                        menu.Close();
                    }
                    break;
            }
            if (step != previous) milestoneShown = false;
            Refresh();
        }

        void ApplyCalibration()
        {
            var holding = movement.GetComponentsInChildren<XRBaseInteractor>()
                .Any(interactor => interactor.interactablesSelected.Any(item => item is XRGrabInteractable));
            if (holding)
            {
                KitchenLocale.Set(status, "Put down the item you are holding before changing the view.");
                return;
            }
            calibrated = movement.CalibrateEyeHeight(EyeHeight);
            if (calibrated) menu.OpenAuxiliary(panel, recenter: true);
            else KitchenLocale.Set(status, "Eye height calibration needs a valid tracking offset on the XR rig.");
        }

        void BeginPractice()
        {
            step = Step.Practice;
            teleported = released = false;
            practiceItems = FindObjectsByType<XRGrabInteractable>(FindObjectsSortMode.None)
                .Where(item => item.gameObject.scene == gameObject.scene && !item.TryGetComponent<KitchenFeedbackNote>(out _))
                .ToArray();
            foreach (var item in practiceItems)
            {
                item.selectEntered.AddListener(Grabbed);
                item.selectExited.AddListener(Released);
            }
            movement.TeleportProvider.locomotionEnded += Teleported;
            menu.Close();
        }

        void StopPractice()
        {
            if (practiceItems == null) return;
            if (movement != null && movement.TeleportProvider != null)
                movement.TeleportProvider.locomotionEnded -= Teleported;
            foreach (var item in practiceItems)
            {
                if (item == null) continue;
                item.selectEntered.RemoveListener(Grabbed);
                item.selectExited.RemoveListener(Released);
            }
            practiceItems = null;
            held.Clear();
        }

        void Teleported(LocomotionProvider _) => teleported = true;
        void Grabbed(SelectEnterEventArgs args) => held.Add((XRGrabInteractable)args.interactableObject);
        void Released(SelectExitEventArgs args)
        {
            if (held.Remove((XRGrabInteractable)args.interactableObject) && !args.isCanceled) released = true;
        }

        void FeedbackSaved()
        {
            if (!isActiveAndEnabled || step != Step.Feedback) return;
            step = Step.Done;
            Open();
        }

        void LateUpdate()
        {
            if (!menu.IsOpen && (RequiresChoice || (!milestoneShown && ReadyForNextStep && !pauseMenu.HasSelection)))
                Open();
            if (menu.IsOpen) hint.transform.parent.gameObject.SetActive(false);
            if (Time.unscaledTime >= nextRefresh)
            {
                nextRefresh = Time.unscaledTime + .15f;
                Refresh();
                RefreshHint();
            }
            if (!hint.gameObject.activeInHierarchy) return;
            var head = movement.Origin.Camera.transform;
            hint.transform.parent.SetPositionAndRotation(head.TransformPoint(0f, -.26f, 1.2f), head.rotation);
        }

        void RefreshHint()
        {
            string message = null;
            if (notes.SpawnBlocked)
                message = "Look toward open space and try the feedback note again.";
            else if (notes.HasLooseNote)
                message = "Grip the yellow note. Release it close to the surface you want to review.";
            else
                switch (step)
                {
                    case Step.Practice:
                        message = Practiced ? "Practice complete. Hold Menu and open Kitchen Guide to continue." : PracticeInstruction();
                        break;
                    case Step.Cook:
                        if (recipe.TomatoMealFinished) message = "Meal complete. Hold Menu and open Kitchen Guide to continue.";
                        break;
                    case Step.Preview:
                        message = Compared ? "Fridges compared. Hold Menu and open Kitchen Guide to continue." : PreviewInstruction();
                        break;
                    case Step.Feedback:
                        message = "Hold Menu and choose Feedback note.";
                        break;
                }
            hint.transform.parent.gameObject.SetActive(message != null && !menu.IsOpen);
            KitchenLocale.Set(hint, message);
        }

        string PracticeInstruction() => !teleported
            ? "Push the right stick forward to aim at the floor, then release to teleport."
            : "Grip a kitchen tool, then release it.";

        string PreviewInstruction() => !layout.HasObservedOpening(layout.CompactSelected)
            ? "Grip and pull the current fridge handle until the door stops."
            : layout.CompactSelected
                ? "Hold Menu and open Layout Lab. Choose Full size, resume and try the door."
                : "Hold Menu and open Layout Lab. Choose Compact, resume and try the door.";

        void Refresh()
        {
            calibration.SetActive(step == Step.Calibrate);
            close.gameObject.SetActive(!RequiresChoice);
            advanceRect.anchoredPosition = RequiresChoice ? new Vector2(0f, advancePosition.y) : advancePosition;
            switch (step)
            {
                case Step.Calibrate:
                    standing.interactable = useSeated;
                    seated.interactable = !useSeated;
                    Show("Calibrate your view", "Choose a posture and adjust the eye height.",
                        $"Eye height: {EyeHeight:0.00} m",
                        !calibrated ? "Calibrate" : IsFreePlay ? "Return to free play" : "Start practice");
                    break;
                case Step.Practice:
                    Show("Practice controls", Practiced ? "Practice complete. Start the recipe when ready." : PracticeInstruction(),
                        (teleported ? "Teleport done" : "Teleport pending") + " | " +
                        (released ? "Grab and release done" : "Grab and release pending"), "Start tomato and egg", Practiced);
                    break;
                case Step.Cook:
                    var tomato = recipe.ActiveRecipe == KitchenRecipeChoice.TomatoEgg;
                    Show("Cook tomato and egg",
                        !tomato ? "Select tomato and egg to continue the guide." :
                        recipe.TomatoMealFinished ? "Meal complete and stove off." : recipe.TomatoStepHint,
                        "Follow the step-by-step instructions at the worktop.",
                        tomato ? "Preview fridges" : "Select tomato and egg", !tomato || recipe.TomatoMealFinished);
                    break;
                case Step.Preview:
                    Show("Compare the fridges", Compared ? "You tried both sizes in the same layout." : PreviewInstruction(),
                        "Full size " + layout.ObservedSwing(false) + " | Compact size " + layout.ObservedSwing(true),
                        Compared ? "Leave feedback" : "Open Layout Lab");
                    break;
                case Step.Feedback:
                    Show("Leave feedback", "Grip the yellow note. Release it close to the surface you want to review.",
                        "Write your feedback and save it to the local document.", "Feedback note");
                    break;
                case Step.Done:
                    if (IsFreePlay)
                        Show("Free play", "Explore the kitchen at your own pace.", "", "Calibrate again");
                    else
                        Show("Kitchen guide complete", "Your feedback was saved. You can now explore the kitchen freely.",
                            "Hold left Menu / F8 to pause.", "Start free play");
                    break;
            }
        }

        void Show(string heading, string instructions, string progress, string action, bool enabled = true)
        {
            KitchenLocale.Set(title, heading);
            KitchenLocale.Set(instruction, instructions);
            KitchenLocale.Set(detail, progress);
            KitchenLocale.Set(actionLabel, action);
            advance.interactable = enabled;
        }
    }
}
