using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using XRApartment.Data;
using XRApartment.Runtime.Core;
using XRApartment.Simulation;

namespace XRApartment.Runtime.Feedback
{
    /// <summary>
    /// Two-page world-space feedback form for one active benchmark. The host remains
    /// responsible for validation and all Core state transitions.
    /// </summary>
    public sealed class FeedbackFormView : MonoBehaviour, IJourneyView
    {
        [SerializeField] private JourneyRuntimeHost host;
        [SerializeField] private MonoBehaviour fallbackView;
        [SerializeField] private float distanceFromOrigin = 1.6f;
        [SerializeField] private Vector2 canvasSize = new Vector2(1200f, 1120f);

        private IJourneyView fallback;
        private Canvas canvas;
        private TMP_Text heading;
        private TMP_Text pageLabel;
        private TMP_Text validationLabel;
        private TMP_Text outcomeLabel;
        private Button submitButton;
        private readonly Dictionary<string, Toggle> choiceToggles = new Dictionary<string, Toggle>();
        private readonly Dictionary<string, Image> choiceRows = new Dictionary<string, Image>();
        private readonly Dictionary<ExperienceTag, Toggle> experienceToggles = new Dictionary<ExperienceTag, Toggle>();
        private readonly Dictionary<Renderer, bool> handRendererStates = new Dictionary<Renderer, bool>();
        private FeedbackDraft draft;
        private string draftSessionId;
        private string draftBenchmarkId;
        private TaskOutcome draftOutcome;
        private int page;
        private JourneySnapshot snapshot;
        private bool handsHidden;

        private void Awake()
        {
            fallback = fallbackView as IJourneyView;
            BuildCanvas();
            SetVisible(false);
        }

        public void Render(JourneySnapshot current, JourneyErrorCode errorCode, string message)
        {
            snapshot = current;
            fallback?.Render(current, errorCode, message);
            bool pending = current != null && current.State == SessionState.FeedbackPending;
            SetVisible(pending);
            if (!pending) return;

            var outcome = TaskOutcome.Unset;
            var hasOutcome = current.BenchmarkOutcomes != null && current.ActiveBenchmarkId != null &&
                current.BenchmarkOutcomes.TryGetValue(current.ActiveBenchmarkId, out outcome);
            if (!hasOutcome) return;
            if (draft == null || draftSessionId != current.SessionId ||
                draftBenchmarkId != current.ActiveBenchmarkId || draftOutcome != outcome)
            {
                draftSessionId = current.SessionId;
                draftBenchmarkId = current.ActiveBenchmarkId;
                draftOutcome = outcome;
                draft = new FeedbackDraft
                {
                    SessionId = draftSessionId,
                    BenchmarkId = draftBenchmarkId
                };
                page = 0;
            }

            heading.text = "Kitchen design feedback · " + current.ActiveBenchmarkId;
            var pendingSkipReason = host == null
                ? null : (host.Core as JourneyCoordinator)?.PendingSkipReason;
            outcomeLabel.text = outcome == TaskOutcome.Completed
                ? "Task completed. Share your assessment of this design."
                : outcome == TaskOutcome.Skipped
                    ? string.IsNullOrWhiteSpace(pendingSkipReason)
                        ? "Task skipped. The stored reason is unavailable; submission is blocked."
                        : "Task skipped · Reason recorded: " + pendingSkipReason
                    : "Task outcome is not available.";
            submitButton.interactable = outcome == TaskOutcome.Completed ||
                (outcome == TaskOutcome.Skipped && !string.IsNullOrWhiteSpace(pendingSkipReason));
            RestoreControls();
            UpdatePageVisibility();
            if (errorCode == JourneyErrorCode.InvalidFeedback) ShowContractErrors(message);
            else validationLabel.text = string.Empty;
        }

        private void BuildCanvas()
        {
            var canvasObject = new GameObject("FeedbackFormCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(TrackedDeviceGraphicRaycaster), typeof(GraphicRaycaster));
            var viewer = Camera.main != null ? Camera.main.transform : transform;
            canvasObject.transform.SetParent(viewer, false);
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            // The legacy InputField still uses a dynamic Unity font. Keep its raster
            // resolution high enough for the enlarged world-space XR canvas; other labels use SDF.
            canvasObject.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 96f;
            canvasObject.GetComponent<RectTransform>().sizeDelta = canvasSize;
            canvasObject.transform.localPosition = new Vector3(0f, 0f, distanceFromOrigin);
            canvasObject.transform.localRotation = Quaternion.identity;
            canvasObject.transform.localScale = Vector3.one * 0.0015f;

            var background = Panel(canvasObject.transform, "WarmPanel", new Color(0.96f, 0.91f, 0.82f, 0.98f));
            Stretch(background.rectTransform, 0f, 0f, 0f, 0f);
            var root = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup));
            root.transform.SetParent(background.transform, false);
            Stretch(root.GetComponent<RectTransform>(), 34f, 28f, 34f, 28f);
            var layout = root.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            heading = AddText(root.transform, "Kitchen design feedback", 42, FontStyles.Bold, 62);
            outcomeLabel = AddText(root.transform, string.Empty, 29, FontStyles.Normal, 48);
            pageLabel = AddText(root.transform, string.Empty, 27, FontStyles.Bold, 42);
            AddText(root.transform, "Choose the response that best reflects your experience.", 27, FontStyles.Normal, 44);

            var pageOne = Container(root.transform, "PageOne");
            pageOne.AddComponent<LayoutElement>().preferredHeight = 460f;
            AddChoiceGroup(pageOne.transform, "Decision", "decision", new[] { "Accept", "NeedsRevision", "Reject" });
            AddChoiceGroup(pageOne.transform, "Main category", "category", new[] { "DoorClearance", "Circulation", "StorageUsability", "OperationalSpace", "Other" });
            AddChoiceGroup(pageOne.transform, "Importance", "importance", new[] { "Low", "Medium", "High" });

            var pageTwo = Container(root.transform, "PageTwo");
            pageTwo.AddComponent<LayoutElement>().preferredHeight = 540f;
            AddText(pageTwo.transform, "Experience tags · select any that apply", 29, FontStyles.Bold, 44);
            AddExperienceGroup(pageTwo.transform);
            AddChoiceGroup(pageTwo.transform, "Everyday impact (optional)", "everydayImpact", new[] { "Inconvenient", "ProlongsTask", "BlocksCirculation", "CannotComplete", "Other" });
            AddText(pageTwo.transform, "Share any additional thoughts verbally with the facilitator.", 27, FontStyles.Normal, 44);

            validationLabel = AddText(root.transform, string.Empty, 27, FontStyles.Bold, 56);
            var buttons = new GameObject("Navigation", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            buttons.transform.SetParent(root.transform, false);
            var buttonLayout = buttons.GetComponent<HorizontalLayoutGroup>();
            buttonLayout.spacing = 16f;
            buttonLayout.childControlWidth = true;
            buttonLayout.childForceExpandWidth = true;
            AddButton(buttons.transform, "Back", () => { page = 0; UpdatePageVisibility(); }, 72);
            AddButton(buttons.transform, "Continue", Continue, 72);
            submitButton = AddButton(buttons.transform, "Submit feedback", Submit, 72);
        }

        private void AddChoiceGroup(Transform parent, string title, string key, string[] options)
        {
            AddText(parent, title, 29, FontStyles.Bold, 44);
            var row = new GameObject(key + "Options", typeof(RectTransform), typeof(GridLayoutGroup), typeof(LayoutElement));
            row.transform.SetParent(parent, false);
            var rowImage = row.AddComponent<Image>();
            rowImage.color = Color.clear;
            rowImage.raycastTarget = false;
            choiceRows[key] = rowImage;
            var group = row.AddComponent<ToggleGroup>();
            int columns = options.Length <= 3 ? options.Length : 3;
            var grid = row.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(220f, 64f);
            grid.spacing = new Vector2(10f, 8f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;
            row.GetComponent<LayoutElement>().preferredHeight = Mathf.Ceil(options.Length / (float)columns) * 72f;
            foreach (string option in options)
            {
                var toggle = AddToggle(row.transform, Friendly(option), 64);
                toggle.group = group;
                choiceToggles[key + "." + option] = toggle;
                toggle.onValueChanged.AddListener(_ =>
                {
                    SetChoiceError(key, false);
                    validationLabel.text = string.Empty;
                    CaptureControls();
                });
            }
        }

        private void AddExperienceGroup(Transform parent)
        {
            var row = new GameObject("ExperienceOptions", typeof(RectTransform), typeof(GridLayoutGroup), typeof(LayoutElement));
            row.transform.SetParent(parent, false);
            var grid = row.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(220f, 64f);
            grid.spacing = new Vector2(10f, 8f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            row.GetComponent<LayoutElement>().preferredHeight = 220f;
            foreach (ExperienceTag tag in new[] { ExperienceTag.DoorBlocked, ExperienceTag.NeedsToStepBack,
                ExperienceTag.HardToReach, ExperienceTag.NotEnoughTempSpace, ExperienceTag.HardToTurn, ExperienceTag.Other })
            {
                var toggle = AddToggle(row.transform, Friendly(tag.ToString()), 64);
                experienceToggles[tag] = toggle;
                toggle.onValueChanged.AddListener(_ => CaptureControls());
            }
        }

        private void RestoreControls()
        {
            SetChoice("decision", draft.Decision.ToString());
            SetChoice("category", draft.Category.ToString());
            SetChoice("importance", draft.Importance.ToString());
            SetChoice("everydayImpact", draft.EverydayImpact.ToString());
            foreach (var item in experienceToggles)
                item.Value.SetIsOnWithoutNotify((draft.Experience & item.Key) != 0);
            foreach (var toggle in choiceToggles.Values)
                toggle.GetComponent<Image>().color = toggle.isOn
                    ? new Color(0.72f, 0.48f, 0.29f)
                    : new Color(0.91f, 0.84f, 0.71f);
            foreach (var toggle in experienceToggles.Values)
                toggle.GetComponent<Image>().color = toggle.isOn
                    ? new Color(0.72f, 0.48f, 0.29f)
                    : new Color(0.91f, 0.84f, 0.71f);
        }

        private void CaptureControls()
        {
            if (draft == null) return;
            draft.Decision = ReadChoice("decision", draft.Decision);
            draft.Category = ReadChoice("category", draft.Category);
            draft.Importance = ReadChoice("importance", draft.Importance);
            draft.EverydayImpact = ReadChoice("everydayImpact", draft.EverydayImpact);
            draft.Experience = ExperienceTag.None;
            foreach (var item in experienceToggles)
                if (item.Value.isOn) draft.Experience |= item.Key;
        }

        private void Submit()
        {
            if (host == null || snapshot == null || draft == null) return;
            if (draftOutcome != TaskOutcome.Completed && draftOutcome != TaskOutcome.Skipped)
            {
                validationLabel.text = "Feedback is unavailable because the task outcome is not Completed or Skipped.";
                return;
            }
            CaptureControls();
            var missing = new List<string>();
            if (draftOutcome == TaskOutcome.Completed)
            {
                if (draft.Decision == CombinationDecision.Unset) missing.Add("Decision");
                if (draft.Category == FeedbackCategory.Unset) missing.Add("Category");
                if (draft.Importance == Importance.Unset) missing.Add("Importance");
            }
            if (missing.Count > 0)
            {
                page = 0;
                UpdatePageVisibility();
                SetChoiceError("decision", draft.Decision == CombinationDecision.Unset);
                SetChoiceError("category", draft.Category == FeedbackCategory.Unset);
                SetChoiceError("importance", draft.Importance == Importance.Unset);
                validationLabel.text = "Complete required fields: " + string.Join(", ", missing) + ".";
                return;
            }
            var result = host.SubmitFeedback(draft);
            if (result != null && !result.Accepted)
                validationLabel.text = string.IsNullOrEmpty(result.Message) ? "Feedback was not accepted." : result.Message;
        }

        private void ShowContractErrors(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                validationLabel.text = "Feedback was not accepted. Review the required fields.";
                return;
            }
            var fields = new List<string>();
            foreach (var key in new[] { "decision", "category", "importance", "shortExplanation", "experience", "everydayImpact", "identity", "taskOutcome", "outcomeReason" })
                if (message.IndexOf(key + "=", StringComparison.OrdinalIgnoreCase) >= 0)
                    fields.Add(key == "shortExplanation" ? "Short explanation" : Friendly(key));
            SetChoiceError("decision", fields.Contains("Decision"));
            SetChoiceError("category", fields.Contains("Category"));
            SetChoiceError("importance", fields.Contains("Importance"));
            if (fields.Contains("Short explanation")) page = 1;
            UpdatePageVisibility();
            validationLabel.text = fields.Contains("Short explanation")
                ? "The current feedback contract still requires a written explanation. Verbal comments are not recorded; submission is blocked until the requirement is updated."
                : fields.Count == 0 ? message : "Please review: " + string.Join(", ", fields) + ".";
        }

        private void Continue()
        {
            CaptureControls();
            var missing = new List<string>();
            if (draftOutcome == TaskOutcome.Completed)
            {
                if (draft.Decision == CombinationDecision.Unset) missing.Add("Decision");
                if (draft.Category == FeedbackCategory.Unset) missing.Add("Category");
                if (draft.Importance == Importance.Unset) missing.Add("Importance");
            }
            if (missing.Count > 0)
            {
                SetChoiceError("decision", draft.Decision == CombinationDecision.Unset);
                SetChoiceError("category", draft.Category == FeedbackCategory.Unset);
                SetChoiceError("importance", draft.Importance == Importance.Unset);
                validationLabel.text = "Choose: " + string.Join(", ", missing) + ".";
                return;
            }
            validationLabel.text = string.Empty;
            page = 1;
            UpdatePageVisibility();
        }

        private void UpdatePageVisibility()
        {
            if (canvas == null) return;
            var one = canvas.transform.Find("WarmPanel/Content/PageOne");
            var two = canvas.transform.Find("WarmPanel/Content/PageTwo");
            if (one != null) one.gameObject.SetActive(page == 0);
            if (two != null) two.gameObject.SetActive(page == 1);
            if (pageLabel != null) pageLabel.text = page == 0 ? "STEP 1 OF 2 · OVERALL JUDGEMENT" : "STEP 2 OF 2 · EXPERIENCE";
            var nav = canvas.transform.Find("WarmPanel/Content/Navigation");
            if (nav != null)
            {
                nav.GetChild(0).gameObject.SetActive(page == 1);
                nav.GetChild(1).gameObject.SetActive(page == 0);
                nav.GetChild(2).gameObject.SetActive(page == 1);
            }
        }

        private void SetChoice(string key, string value)
        {
            foreach (var item in choiceToggles)
                if (item.Key.StartsWith(key + ".", StringComparison.Ordinal))
                    item.Value.SetIsOnWithoutNotify(item.Key == key + "." + value);
        }

        private void SetChoiceError(string key, bool hasError)
        {
            if (choiceRows.TryGetValue(key, out var image))
                image.color = hasError ? new Color(0.96f, 0.78f, 0.70f, 0.55f) : Color.clear;
        }

        private T ReadChoice<T>(string key, T current) where T : struct
        {
            foreach (var item in choiceToggles)
                if (item.Key.StartsWith(key + ".", StringComparison.Ordinal) && item.Value.isOn &&
                    Enum.TryParse(item.Key.Substring(key.Length + 1), out T parsed)) return parsed;
            return current;
        }

        private static GameObject Container(Transform parent, string name)
        {
            var result = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup));
            result.transform.SetParent(parent, false);
            var layout = result.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            return result;
        }

        private static TMP_FontAsset uiFont;
        private static Material uiFontMaterial;

        private static TMP_Text AddText(Transform parent, string value, int size, FontStyles style, float height)
        {
            var obj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            obj.transform.SetParent(parent, false);
            var text = obj.GetComponent<TextMeshProUGUI>();
            text.text = value;
            if (uiFont == null)
                uiFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (uiFont == null) uiFont = TMP_Settings.defaultFontAsset;
            text.font = uiFont;
            if (uiFont != null && uiFontMaterial == null && uiFont.material != null)
            {
                // A modest positive SDF sharpness keeps small XR text edges defined
                // without changing the shared project font material.
                uiFontMaterial = new Material(uiFont.material);
                if (uiFontMaterial.HasProperty(ShaderUtilities.ID_Sharpness))
                    uiFontMaterial.SetFloat(ShaderUtilities.ID_Sharpness, 0.5f);
            }
            if (uiFontMaterial != null) text.fontSharedMaterial = uiFontMaterial;
            text.fontSize = Mathf.RoundToInt(size * 1.22f);
            text.fontStyle = style;
            text.color = new Color(0.22f, 0.20f, 0.17f);
            text.alignment = TextAlignmentOptions.MidlineLeft;
            // Labels are visual only. Let pointer hits reach the parent Toggle or
            // Button instead of making the glyph mesh a separate raycast target.
            text.raycastTarget = false;
            obj.GetComponent<LayoutElement>().preferredHeight = height * 1.15f;
            return text;
        }

        private static Toggle AddToggle(Transform parent, string label, float height)
        {
            var obj = new GameObject(label, typeof(RectTransform), typeof(Toggle), typeof(Image), typeof(LayoutElement));
            obj.transform.SetParent(parent, false);
            obj.GetComponent<Image>().color = new Color(0.91f, 0.84f, 0.71f);
            obj.GetComponent<LayoutElement>().preferredHeight = height;
            var toggle = obj.GetComponent<Toggle>();
            toggle.targetGraphic = obj.GetComponent<Image>();
            var background = obj.GetComponent<Image>();
            toggle.onValueChanged.AddListener(on => background.color = on
                ? new Color(0.72f, 0.48f, 0.29f)
                : new Color(0.91f, 0.84f, 0.71f));
            var text = AddText(obj.transform, label, 27, FontStyles.Normal, height);
            text.alignment = TextAlignmentOptions.Center;
            Stretch(text.rectTransform, 4f, 2f, 4f, 2f);
            return toggle;
        }

        private static Button AddButton(Transform parent, string label, UnityEngine.Events.UnityAction action, float height)
        {
            var obj = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            obj.transform.SetParent(parent, false);
            obj.GetComponent<Image>().color = new Color(0.60f, 0.37f, 0.22f);
            obj.GetComponent<LayoutElement>().preferredHeight = height;
            var text = AddText(obj.transform, label, 28, FontStyles.Bold, height);
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            Stretch(text.rectTransform, 4f, 2f, 4f, 2f);
            var button = obj.GetComponent<Button>();
            button.onClick.AddListener(action);
            return button;
        }

        private static Image Panel(Transform parent, string name, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            var image = obj.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static void Stretch(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static string Friendly(string value)
        {
            switch (value)
            {
                case "NeedsRevision": return "Revise";
                case "DoorClearance": return "Door clearance";
                case "StorageUsability": return "Storage use";
                case "OperationalSpace": return "Working space";
                case "ProlongsTask": return "Takes longer";
                case "BlocksCirculation": return "Blocks circulation";
                case "CannotComplete": return "Cannot complete";
                case "decision": return "Decision";
                case "category": return "Category";
                case "importance": return "Importance";
                case "shortExplanation": return "Short explanation";
                case "everydayImpact": return "Everyday impact";
                case "taskOutcome": return "Task outcome";
                case "outcomeReason": return "Outcome reason";
                case "identity": return "Request identity";
                case "DoorBlocked": return "Door blocked";
                case "NeedsToStepBack": return "Step back";
                case "NotEnoughTempSpace": return "Tight temp space";
                case "HardToReach": return "Hard to reach";
                case "HardToTurn": return "Hard to turn";
                default: return value;
            }
        }

        private void SetVisible(bool visible)
        {
            if (canvas != null) canvas.gameObject.SetActive(visible);
            SetHandRenderersHidden(visible);
        }

        private void SetHandRenderersHidden(bool hide)
        {
            if (handsHidden == hide) return;

            if (hide)
            {
                handRendererStates.Clear();
                foreach (var sceneTransform in FindObjectsOfType<Transform>(true))
                {
                    if (sceneTransform.name != "PREF_Hand_Left" && sceneTransform.name != "PREF_Hand_Right")
                        continue;

                    foreach (var handRenderer in sceneTransform.GetComponentsInChildren<Renderer>(true))
                    {
                        if (handRenderer == null || handRendererStates.ContainsKey(handRenderer)) continue;
                        handRendererStates.Add(handRenderer, handRenderer.enabled);
                        handRenderer.enabled = false;
                    }
                }
                handsHidden = true;
                return;
            }

            foreach (var item in handRendererStates)
                if (item.Key != null) item.Key.enabled = item.Value;
            handRendererStates.Clear();
            handsHidden = false;
        }
    }
}
