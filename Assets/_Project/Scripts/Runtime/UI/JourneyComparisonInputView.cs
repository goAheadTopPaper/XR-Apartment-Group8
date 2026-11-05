using System.Globalization;
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
        private Button openKeyboardButton;
        private RectTransform keyboard;
        private CanvasGroup comparisonGroup;
        private TMP_Text keyboardPreview;
        private TMP_Text keyboardCount;
        private TMP_Text shiftLabel;
        private TMP_Text error;
        private Shader keyboardSurfaceShader;
        private Shader keyboardTextShader;
        private Material keyboardSurface;
        private Material keyboardFont;
        private string preferred;
        private string skipped;
        private bool shift;

        internal void ConfigureKeyboardShaders(Shader surface, Shader text)
        {
            keyboardSurfaceShader = surface;
            keyboardTextShader = text;
        }

        public void Open(JourneySnapshot snapshot, FinalComparison draft)
        {
            BuildIfNeeded();
            CloseKeyboard();
            bool b1Completed = Completed(snapshot, "B1");
            bool b2Completed = Completed(snapshot, "B2");
            skipped = b1Completed && !b2Completed ? "B2" : b2Completed && !b1Completed ? "B1" : null;
            b1Button.interactable = b1Completed;
            b2Button.interactable = b2Completed;
            openKeyboardButton.interactable = b1Completed || b2Completed;
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
                TradeOffSummary = tradeOff.text
            };
        }

        public void ShowError(string message)
        {
            BuildIfNeeded();
            error.text = message ?? string.Empty;
        }

        private void OnDisable() => CloseKeyboard();

        private void OnDestroy()
        {
            if (keyboardSurface != null) Destroy(keyboardSurface);
            if (keyboardFont != null) Destroy(keyboardFont);
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
            b1Button = Choice("Prefer B1", -285f, font, () => { preferred = "B1"; RefreshSelection(); });
            b2Button = Choice("Prefer B2", 285f, font, () => { preferred = "B2"; RefreshSelection(); });
            openKeyboardButton = Choice("Write in VR", 0f, font, OpenKeyboard);

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
            tradeOff.richText = false;
            value.richText = false;
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

        private void OpenKeyboard()
        {
            if (keyboard == null) BuildKeyboard();
            tradeOff.DeactivateInputField();
            shift = false;
            RefreshKeyboard();
            keyboard.SetAsLastSibling();
            comparisonGroup.alpha = 0f;
            comparisonGroup.interactable = false;
            comparisonGroup.blocksRaycasts = false;
            keyboard.gameObject.SetActive(true);
            error.text = string.Empty;
        }

        internal void CloseKeyboard()
        {
            if (keyboard == null || !keyboard.gameObject.activeSelf) return;
            keyboard.gameObject.SetActive(false);
            comparisonGroup.alpha = 1f;
            comparisonGroup.interactable = true;
            comparisonGroup.blocksRaycasts = true;
        }

        private void BuildKeyboard()
        {
            var panel = (RectTransform)transform.parent;
            comparisonGroup = panel.gameObject.AddComponent<CanvasGroup>();
            // Center the keys on the viewer while the comparison panel is hidden.
            keyboard = Rect("TradeOffKeyboard", 0f, 0f, panel.rect.width, panel.rect.height, panel.parent);
            keyboard.gameObject.SetActive(false);
            var surface = keyboard.gameObject.AddComponent<Image>();
            surface.color = new Color(.08f, .12f, .18f);
            var font = TMP_Settings.defaultFontAsset;
            Text("Title", Rect("Heading", 0f, 247f, 820f, 50f, keyboard), "Write your main trade-off", 34, font);
            keyboardPreview = Text("Note", Rect("Preview", 0f, 168f, 800f, 94f, keyboard), "", 25, font);
            keyboardPreview.richText = false;
            keyboardCount = Text("Count", Rect("CountLabel", 343f, 220f, 120f, 26f, keyboard), "", 18, font);
            var rows = new[] { "1234567890", "QWERTYUIOP", "ASDFGHJKL", "ZXCVBNM,.?" };
            for (int row = 0; row < rows.Length; row++)
                for (int column = 0; column < rows[row].Length; column++)
                {
                    char key = rows[row][column];
                    float x = (column - (rows[row].Length - 1) * .5f) * 76f;
                    Key("Key " + key, key.ToString(), x, 95f - 69f * row, 66f, font,
                        () => Append(shift ? key : char.ToLowerInvariant(key)));
                }
            shiftLabel = Key("Shift", "Shift", -326f, -183f, 140f, font, () =>
            {
                shift = !shift;
                RefreshKeyboard();
            }).GetComponentInChildren<TMP_Text>();
            Key("Space", "Space", -121f, -183f, 246f, font, () => Append(' '));
            Key("Backspace", "Backspace", 97f, -183f, 164f, font, Backspace);
            Key("Done", "Done", 290f, -183f, 168f, font, CloseKeyboard);
            Text("Help", Rect("HelpLabel", 0f, -260f, 820f, 36f, keyboard),
                "Use the controller ray and trigger. Done returns to the comparison.", 22, font);
            if (keyboardSurfaceShader != null)
            {
                keyboardSurface = new Material(keyboardSurfaceShader);
                foreach (var image in keyboard.GetComponentsInChildren<Image>(true)) image.material = keyboardSurface;
            }
            if (keyboardTextShader != null && font != null)
            {
                keyboardFont = new Material(font.material) { shader = keyboardTextShader, renderQueue = 3100 };
                foreach (var label in keyboard.GetComponentsInChildren<TMP_Text>(true)) label.fontSharedMaterial = keyboardFont;
            }
        }

        private void Append(char key)
        {
            if (tradeOff.text.Length >= tradeOff.characterLimit) return;
            tradeOff.text += key;
            if (char.IsLetter(key)) shift = false;
            RefreshKeyboard();
        }

        private void Backspace()
        {
            var starts = StringInfo.ParseCombiningCharacters(tradeOff.text);
            if (starts.Length == 0) return;
            tradeOff.text = tradeOff.text.Substring(0, starts[starts.Length - 1]);
            RefreshKeyboard();
        }

        private void RefreshKeyboard()
        {
            keyboardPreview.text = tradeOff.text.Length == 0 ? "Your note appears here." : tradeOff.text;
            keyboardCount.text = $"{tradeOff.text.Length} / {tradeOff.characterLimit}";
            shiftLabel.text = shift ? "Shift on" : "Shift";
        }

        private Button Key(string name, string title, float x, float y, float width, TMP_FontAsset font,
            UnityEngine.Events.UnityAction action) =>
            AddButton(Rect(name, x, y, width, 58f, keyboard), title, font, action);

        private Button Choice(string title, float x, TMP_FontAsset font, UnityEngine.Events.UnityAction action)
            => AddButton(Rect(title, x, 48f, 225f, 48f), title, font, action);

        private static Button AddButton(RectTransform rect, string title, TMP_FontAsset font,
            UnityEngine.Events.UnityAction action)
        {
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
