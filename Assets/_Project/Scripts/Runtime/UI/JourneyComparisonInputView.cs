using TMPro;
using UnityEngine;
using UnityEngine.UI;
using XRApartment.Data;
using XRApartment.Simulation;

namespace XRApartment.Runtime.UI
{
    /// <summary>World-space preference and trade-off input for the final comparison.</summary>
    public sealed class JourneyComparisonInputView : MonoBehaviour, IComparisonInputView
    {
        private Button b1Button;
        private Button b2Button;
        private TMP_InputField tradeOff;
        private RectTransform tradeOffRect;
        private RectTransform viewportRect;
        private Button skipContextButton;
        private TMP_Text error;
        private string preferred;
        private string skipped;

        public void Open(JourneySnapshot snapshot, FinalComparison draft)
        {
            BuildIfNeeded();
            bool b1Completed = Completed(snapshot, "B1");
            bool b2Completed = Completed(snapshot, "B2");
            skipped = b1Completed && !b2Completed ? "B2" : b2Completed && !b1Completed ? "B1" : null;
            b1Button.interactable = b1Completed;
            b2Button.interactable = b2Completed;
            preferred = draft?.PreferredBenchmarkId;
            if (!Completed(snapshot, preferred))
                preferred = b1Completed && !b2Completed ? "B1" : b2Completed && !b1Completed ? "B2" : null;
            tradeOff.text = draft?.TradeOffSummary ?? string.Empty;
            skipContextButton.gameObject.SetActive(skipped != null);
            tradeOffRect.anchoredPosition = new Vector2(skipped == null ? 0f : -100f, -26f);
            tradeOffRect.sizeDelta = new Vector2(skipped == null ? 760f : 560f, 54f);
            viewportRect.sizeDelta = new Vector2(skipped == null ? 730f : 530f, 48f);
            error.text = string.Empty;
            RefreshSelection();
        }

        public FinalComparison Capture()
        {
            BuildIfNeeded();
            return new FinalComparison
            {
                PreferredBenchmarkId = preferred,
                TradeOffSummary = tradeOff.text?.Trim()
            };
        }

        public void ShowError(string message)
        {
            BuildIfNeeded();
            error.text = message ?? string.Empty;
        }

        private static bool Completed(JourneySnapshot snapshot, string id)
        {
            return !string.IsNullOrEmpty(id) && snapshot?.BenchmarkOutcomes != null &&
                snapshot.BenchmarkOutcomes.TryGetValue(id, out var outcome) &&
                outcome == TaskOutcome.Completed;
        }

        private void RefreshSelection()
        {
            b1Button.GetComponent<Image>().color = preferred == "B1"
                ? new Color(.20f, .55f, .32f) : new Color(.14f, .42f, .77f);
            b2Button.GetComponent<Image>().color = preferred == "B2"
                ? new Color(.20f, .55f, .32f) : new Color(.14f, .42f, .77f);
        }

        private void BuildIfNeeded()
        {
            if (tradeOff != null) return;
            var font = TMP_Settings.defaultFontAsset;
            b1Button = Choice("Prefer B1", -180f, font, () => { preferred = "B1"; RefreshSelection(); });
            b2Button = Choice("Prefer B2", 180f, font, () => { preferred = "B2"; RefreshSelection(); });

            var field = Rect("TradeOff", 0f, -26f, 760f, 54f);
            tradeOffRect = field;
            var background = field.gameObject.AddComponent<Image>();
            background.color = new Color(.18f, .23f, .31f, 1f);
            tradeOff = field.gameObject.AddComponent<TMP_InputField>();
            tradeOff.targetGraphic = background;
            var viewport = Rect("Viewport", 0f, 0f, 730f, 48f, field);
            viewportRect = viewport;
            var value = Text("Text", viewport, string.Empty, 22, font);
            var placeholder = Text("Placeholder", viewport, "Main trade-off (required)", 19, font);
            placeholder.color = new Color(.70f, .74f, .80f);
            tradeOff.textViewport = viewport;
            tradeOff.textComponent = value;
            tradeOff.placeholder = placeholder;
            tradeOff.characterLimit = 180;
            tradeOff.lineType = TMP_InputField.LineType.SingleLine;
            skipContextButton = Choice("Use skip context", 290f, font, () =>
            {
                if (skipped == null || preferred == null) return;
                tradeOff.text = preferred + " was assessed; " + skipped +
                    " was skipped, so a direct comparison is limited.";
                error.text = string.Empty;
            });
            skipContextButton.GetComponentInChildren<TMP_Text>().fontSize = 17;
            skipContextButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(310f, -26f);
            skipContextButton.GetComponent<RectTransform>().sizeDelta = new Vector2(240f, 80f);
            error = Text("Error", Rect("Error", 0f, -73f, 760f, 30f), string.Empty, 18, font);
            error.color = new Color(1f, .68f, .58f);
        }

        private Button Choice(string title, float x, TMP_FontAsset font, UnityEngine.Events.UnityAction action)
        {
            var rect = Rect(title, x, 48f, 260f, 48f);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(.14f, .42f, .77f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            Text("Label", rect, title, 22, font);
            button.onClick.AddListener(action);
            return button;
        }

        private RectTransform Rect(string title, float x, float y, float width, float height,
            Transform parent = null)
        {
            var rect = new GameObject(title, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent == null ? transform : parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        private static TextMeshProUGUI Text(string title, RectTransform parent, string value,
            int size, TMP_FontAsset font)
        {
            var label = new GameObject(title, typeof(RectTransform), typeof(TextMeshProUGUI))
                .GetComponent<TextMeshProUGUI>();
            label.transform.SetParent(parent, false);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(8f, 2f);
            label.rectTransform.offsetMax = new Vector2(-8f, -2f);
            label.font = font;
            label.fontSize = size;
            label.text = value;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            return label;
        }
    }
}
