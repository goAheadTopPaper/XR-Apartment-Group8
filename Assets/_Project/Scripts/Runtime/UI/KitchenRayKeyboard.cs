using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace XRApartment.Runtime.UI
{
    /// <summary>Edits the same field as the physical keyboard using tracked UI buttons.</summary>
    internal sealed class KitchenRayKeyboard
    {
        readonly TMP_InputField field;
        readonly RectTransform root;
        readonly TMP_Text preview, count, shiftLabel;
        readonly Material surfaceMaterial, fontMaterial;
        bool shift;

        public bool IsOpen => root.gameObject.activeSelf;

        public KitchenRayKeyboard(string name, RectTransform panel, Transform parent, TMP_InputField field,
            string title, string help, Action done, Shader surfaceShader = null, Shader textShader = null)
        {
            this.field = field;
            root = Rect(name, parent, 0f, 0f, panel.rect.width, panel.rect.height);
            root.gameObject.SetActive(false);
            root.gameObject.AddComponent<Image>().color = new Color(.08f, .12f, .18f);
            var font = TMP_Settings.defaultFontAsset;
            Text("Title", Rect("Heading", root, 0f, 247f, 820f, 50f), title, 34, font);
            preview = Text("Note", Rect("Preview", root, 0f, 168f, 800f, 94f), "", 25, font);
            preview.richText = false;
            preview.overflowMode = TextOverflowModes.Ellipsis;
            count = Text("Count", Rect("CountLabel", root, 343f, 220f, 120f, 26f), "", 18, font);
            var rows = new[] { "1234567890", "QWERTYUIOP", "ASDFGHJKL", "ZXCVBNM,.?" };
            for (var row = 0; row < rows.Length; row++)
                for (var column = 0; column < rows[row].Length; column++)
                {
                    var key = rows[row][column];
                    var x = (column - (rows[row].Length - 1) * .5f) * 76f;
                    Key("Key " + key, key.ToString(), x, 95f - 69f * row, 66f,
                        () => Append(shift ? key : char.ToLowerInvariant(key)));
                }
            shiftLabel = Key("Shift", "Shift", -326f, -183f, 140f, () =>
            {
                shift = !shift;
                Refresh();
            }).GetComponentInChildren<TMP_Text>();
            Key("Space", "Space", -121f, -183f, 246f, () => Append(' '));
            Key("Backspace", "Backspace", 97f, -183f, 164f, Backspace);
            Key("Done", "Done", 290f, -183f, 168f, () => done());
            Text("Help", Rect("HelpLabel", root, 0f, -260f, 820f, 36f), help, 22, font);
            if (surfaceShader != null)
            {
                surfaceMaterial = new Material(surfaceShader);
                foreach (var image in root.GetComponentsInChildren<Image>(true)) image.material = surfaceMaterial;
            }
            if (textShader != null && font != null)
            {
                fontMaterial = new Material(font.material) { shader = textShader, renderQueue = 3100 };
                foreach (var label in root.GetComponentsInChildren<TMP_Text>(true)) label.fontSharedMaterial = fontMaterial;
            }
        }

        public void Show()
        {
            field.DeactivateInputField();
            shift = false;
            Refresh();
            root.SetAsLastSibling();
            root.gameObject.SetActive(true);
        }

        public void Hide() => root.gameObject.SetActive(false);

        public void ReleaseMaterials()
        {
            if (surfaceMaterial != null) UnityEngine.Object.Destroy(surfaceMaterial);
            if (fontMaterial != null) UnityEngine.Object.Destroy(fontMaterial);
        }

        void Append(char key)
        {
            if (field.text.Length >= field.characterLimit) return;
            field.text += key;
            if (char.IsLetter(key)) shift = false;
            Refresh();
        }

        void Backspace()
        {
            var starts = StringInfo.ParseCombiningCharacters(field.text);
            if (starts.Length == 0) return;
            field.text = field.text.Substring(0, starts[starts.Length - 1]);
            Refresh();
        }

        void Refresh()
        {
            if (field.text.Length == 0) KitchenLocale.Set(preview, "Your note appears here.");
            else KitchenLocale.SetVerbatim(preview, field.text);
            count.text = $"{field.text.Length} / {field.characterLimit}";
            KitchenLocale.Set(shiftLabel, shift ? "Shift on" : "Shift");
        }

        Button Key(string name, string title, float x, float y, float width, UnityEngine.Events.UnityAction action) =>
            Button(Rect(name, root, x, y, width, 58f), title, TMP_Settings.defaultFontAsset, action);

        internal static Button Button(RectTransform rect, string title, TMP_FontAsset font, UnityEngine.Events.UnityAction action)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(.14f, .42f, .77f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            Text("Label", rect, title, 22, font);
            button.onClick.AddListener(action);
            return button;
        }

        internal static RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        internal static TextMeshProUGUI Text(string name, RectTransform parent, string value, int size, TMP_FontAsset font)
        {
            var label = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI))
                .GetComponent<TextMeshProUGUI>();
            label.transform.SetParent(parent, false);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(8f, 2f);
            label.rectTransform.offsetMax = new Vector2(-8f, -2f);
            label.font = font;
            label.fontSize = size;
            KitchenLocale.Set(label, value);
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            return label;
        }
    }
}
