using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using XRApartment.Data;

namespace XRApartment.Runtime.UI
{
    public sealed class KitchenActivityGuide : MonoBehaviour
    {
        enum Step { AwaitingLanguage, ToKitchen, Practice, Cook, Preview, Feedback, Done }

        const float EntryArrivalRadius = .38f;
        const float EntryArrowLookAhead = 3.4f;
        const string EntryControls = "Right stick to teleport. Click left stick to crouch or stand.";
        const string ExploreKitchen = "Try the fridge, sink, chopping board or stove. Grip and release a kitchen tool.";
        const string FridgeShortcut = "Right B / F9 to choose a full-size or compact fridge.";
        const string GripPractice = "Grip a kitchen tool, then release it.";

        [SerializeField] LocomotionSettingsView menu;
        [SerializeField] KitchenLocomotionSettings movement;
        [SerializeField] KitchenRecipeController recipe;
        [SerializeField] KitchenLayoutLab layout;
        [SerializeField] KitchenFeedbackNotes notes;
        [SerializeField] KitchenPauseMenu pauseMenu;
        [SerializeField] GameObject panel;
        [SerializeField] Button close, advance;
        [SerializeField] TMP_Text title, instruction, detail, actionLabel, hint;
        [SerializeField] Vector3[] entryRoute = System.Array.Empty<Vector3>();
        [SerializeField] LineRenderer entryArrow;
        readonly HashSet<XRGrabInteractable> held = new HashSet<XRGrabInteractable>();
        XRGrabInteractable[] practiceItems;
        InputAction crouchAction;
        RectTransform advanceRect;
        Vector2 advancePosition;
        Step step;
        int entryWaypoint;
        bool started, movementPracticed, released, milestoneShown;
        float nextRefresh;

        bool HasEntryRoute => entryRoute.Length > 1;
        bool Practiced => movementPracticed && released;
        bool Compared => layout.HasObservedOpening(false) && layout.HasObservedOpening(true);
        bool ReadyForNextStep => step switch
        {
            Step.Practice => Practiced,
            Step.Cook => recipe.TomatoMealFinished,
            Step.Preview => Compared,
            _ => false
        };
        public bool IsFreePlay { get; private set; }
        internal bool CanChooseFridgeBeforeCooking => isActiveAndEnabled && step == Step.Practice && movementPracticed;
        internal bool AllowsLayout => !isActiveAndEnabled || IsFreePlay || CanChooseFridgeBeforeCooking || step == Step.Preview;
        internal bool AllowsFeedback => !isActiveAndEnabled || step != Step.AwaitingLanguage;
        internal bool RequiresChoice => isActiveAndEnabled && !IsFreePlay && step == Step.Done;
        internal string StageTitle => KitchenLocale.Source(title);
        internal string StageInstruction => KitchenLocale.Source(instruction);

        void Awake()
        {
            advanceRect = (RectTransform)advance.transform;
            advancePosition = advanceRect.anchoredPosition;
            close.onClick.AddListener(menu.Close);
            advance.onClick.AddListener(Advance);
            notes.Saved += FeedbackSaved;
            crouchAction = new InputAction("Kitchen crouch", InputActionType.Button, "<XRController>{LeftHand}/{Primary2DAxisClick}");
            crouchAction.performed += Crouch;
        }

        void Start()
        {
            started = true;
            ShowStartupLanguage();
        }

        void OnEnable()
        {
            crouchAction.Enable();
            if (started && step == Step.AwaitingLanguage) ShowStartupLanguage();
        }

        void ShowStartupLanguage() => LanguageSelectionView.ShowForStartup(menu, movement.Origin.Camera, BeginEntry);

        void OnDisable()
        {
            crouchAction?.Disable();
            LanguageSelectionView.CancelStartup(menu);
            if (entryArrow != null) entryArrow.enabled = false;
            if (hint != null) hint.transform.parent.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            crouchAction?.Dispose();
            StopPractice();
            if (notes != null) notes.Saved -= FeedbackSaved;
        }

        internal void Open()
        {
            if (step == Step.AwaitingLanguage) return;
            milestoneShown |= ReadyForNextStep;
            Refresh();
            menu.OpenAuxiliary(panel);
        }

        void BeginEntry()
        {
            movement.AlignViewHeight(false);
            if (HasEntryRoute)
            {
                step = Step.ToKitchen;
                entryWaypoint = 1;
            }
            else BeginPractice();
            Refresh();
            RefreshHint();
        }

        void Advance()
        {
            var previous = step;
            switch (step)
            {
                case Step.ToKitchen:
                    menu.Close();
                    break;
                case Step.Practice:
                    if (!Practiced)
                    {
                        if (CanChooseFridgeBeforeCooking) menu.OpenLayout();
                    }
                    else if (recipe.SelectRecipe(KitchenRecipeChoice.TomatoEgg))
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
                    IsFreePlay = true;
                    menu.Close();
                    break;
            }
            if (step != previous) milestoneShown = false;
            Refresh();
        }

        void Crouch(InputAction.CallbackContext _)
        {
            if (step != Step.AwaitingLanguage && !menu.IsOpen && !LanguageSelectionView.IsVisible)
                movement.ToggleCrouch();
        }

        void UpdateEntryRoute()
        {
            if (entryArrow != null) entryArrow.enabled = step == Step.ToKitchen && !menu.IsOpen;
            if (step != Step.ToKitchen || menu.IsOpen) return;

            var feet = movement.Origin.Camera.transform.position;
            feet.y = entryRoute[0].y;
            var arrivalSquared = EntryArrivalRadius * EntryArrivalRadius;
            if ((feet - entryRoute[entryRoute.Length - 1]).sqrMagnitude <= arrivalSquared)
            {
                if (entryArrow != null) entryArrow.enabled = false;
                BeginPractice(movedToKitchen: true);
                return;
            }
            if (entryWaypoint < entryRoute.Length - 1 && (feet - entryRoute[entryWaypoint]).sqrMagnitude <= arrivalSquared)
                entryWaypoint++;

            if (entryArrow == null) return;
            var start = entryRoute[entryWaypoint - 1];
            var segment = entryRoute[entryWaypoint] - start;
            var direction = segment.normalized;
            var ahead = Mathf.Clamp(Vector3.Dot(feet - start, direction) + EntryArrowLookAhead, 0f, segment.magnitude);
            entryArrow.transform.SetPositionAndRotation(start + direction * ahead + Vector3.up * .025f,
                Quaternion.LookRotation(direction));
        }

        string EntryInstruction() => entryWaypoint switch
        {
            1 => "Follow the arrow straight past the sofa.",
            2 => "Turn left and follow the arrow past the dining table.",
            _ => "Turn right through the kitchen opening and go to the worktop."
        };

        void BeginPractice(bool movedToKitchen = false)
        {
            step = Step.Practice;
            movementPracticed = movedToKitchen;
            released = false;
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

        void Teleported(LocomotionProvider _) => movementPracticed = true;
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
            UpdateEntryRoute();
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
                message = "Grip the yellow note and throw it at an object, or release it against the surface.";
            else
                switch (step)
                {
                    case Step.ToKitchen:
                        message = entryWaypoint == 1 ? EntryInstruction() + "\n" + EntryControls : EntryInstruction();
                        break;
                    case Step.Practice:
                        message = Practiced ? "Practice complete. Hold Menu and open Kitchen Guide to continue."
                            : CanChooseFridgeBeforeCooking ? FridgeShortcut + "\n" + GripPractice : PracticeInstruction();
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

        string PracticeInstruction() => !movementPracticed
            ? "Push the right stick forward to aim at the floor, then release to teleport."
            : HasEntryRoute ? ExploreKitchen : GripPractice;

        string PreviewInstruction() => !layout.HasObservedOpening(layout.CompactSelected)
            ? "Grip and pull the current fridge handle until the door stops."
            : layout.CompactSelected
                ? "Hold Menu and open Layout Lab. Choose Full size, resume and try the door."
                : "Hold Menu and open Layout Lab. Choose Compact, resume and try the door.";

        void Refresh()
        {
            close.gameObject.SetActive(!RequiresChoice);
            advanceRect.anchoredPosition = RequiresChoice ? new Vector2(0f, advancePosition.y) : advancePosition;
            switch (step)
            {
                case Step.ToKitchen:
                    Show("Find the kitchen", EntryInstruction(), EntryControls, "Resume");
                    break;
                case Step.Practice:
                    Show("Practice controls", Practiced ? "Practice complete. Start the recipe when ready." : PracticeInstruction(),
                        Practiced ? FridgeShortcut : (HasEntryRoute ? "Kitchen reached" : movementPracticed ? "Teleport done" : "Teleport pending") + " | " +
                        (released ? "Grab and release done" : "Grab and release pending"),
                        Practiced ? "Start tomato and egg" : "Choose a fridge", Practiced || CanChooseFridgeBeforeCooking);
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
                    Show("Leave feedback", "Grip the yellow note and throw it at an object, or release it against the surface.",
                        "Write your feedback and save it to the local document.", "Feedback note");
                    break;
                case Step.Done:
                    if (IsFreePlay)
                        Show("Free play", "Explore the kitchen at your own pace.", "", "Resume");
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
