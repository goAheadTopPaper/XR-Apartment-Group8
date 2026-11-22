using System;
using System.Collections.Generic;
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
        readonly Button[] candidateKeys;
        readonly TMP_Text[] candidateLabels;
        readonly Button previousCandidates, nextCandidates, separator;
        readonly TMP_Text compositionLabel, languageLabel;
        readonly KitchenPinyinCandidates dictionary;
        IReadOnlyList<string> suggestions = Array.Empty<string>();
        string composition = "";
        int page;
        bool shift, pinyin, languageInitialized;
        const int PageSize = 8;

        public bool IsOpen => root.gameObject.activeSelf;
        internal bool HasComposition => composition.Length != 0;
        internal RectTransform Region => root;

        public KitchenRayKeyboard(string name, RectTransform panel, Transform parent, TMP_InputField field,
            string title, string help, Action done, Shader surfaceShader = null, Shader textShader = null,
            Action openNativeInput = null, Action cancel = null)
        {
            this.field = field;
            var docked = openNativeInput != null;
            if (docked) dictionary = KitchenPinyinCandidates.Load();
            root = Rect(name, parent, 0f, docked ? -185f : 0f, panel.rect.width, docked ? 360f : panel.rect.height);
            root.gameObject.SetActive(false);
            root.gameObject.AddComponent<Image>().color = new Color(.08f, .12f, .18f);
            var font = TMP_Settings.defaultFontAsset;
            if (!docked)
            {
                Text("Title", Rect("Heading", root, 0f, 247f, 820f, 50f), title, 34, font);
                preview = Text("Note", Rect("Preview", root, 0f, 168f, 800f, 94f), "", 25, font);
                preview.richText = false;
                preview.overflowMode = TextOverflowModes.Ellipsis;
                count = Text("Count", Rect("CountLabel", root, 343f, 220f, 120f, 26f), "", 18, font);
            }
            else
            {
                compositionLabel = Text("Composition", Rect("Composition", root, 0f, 157f, 800f, 30f), "", 23, font);
                compositionLabel.richText = false;
                previousCandidates = Key("PreviousCandidates", "‹", -403f, 119f, 44f, () => ChangePage(-1), 44f);
                nextCandidates = Key("NextCandidates", "›", 403f, 119f, 44f, () => ChangePage(1), 44f);
                candidateKeys = new Button[PageSize];
                candidateLabels = new TMP_Text[PageSize];
                for (var i = 0; i < PageSize; i++)
                {
                    var index = i;
                    candidateKeys[i] = Key("Candidate" + (i + 1), "", (i - 3.5f) * 94f, 119f, 91f, () => Choose(index), 44f);
                    var label = candidateLabels[i] = candidateKeys[i].GetComponentInChildren<TMP_Text>();
                    label.enableAutoSizing = true;
                    label.fontSizeMin = 14;
                    label.fontSizeMax = 26;
                    label.enableWordWrapping = false;
                    label.overflowMode = TextOverflowModes.Ellipsis;
                    label.richText = false;
                }
            }
            var rows = new[] { "1234567890", "QWERTYUIOP", "ASDFGHJKL", "ZXCVBNM,.?" };
            for (var row = 0; row < rows.Length; row++)
                for (var column = 0; column < rows[row].Length; column++)
                {
                    var key = rows[row][column];
                    var x = (column - (rows[row].Length - 1) * .5f) * 76f;
                    Key("Key " + key, key.ToString(), x, (docked ? 73f : 95f) - (docked ? 46f : 69f) * row,
                        66f, () => TypeKey(shift ? key : char.ToLowerInvariant(key)), docked ? 41f : 58f);
                }
            var controlsY = docked ? -110f : -183f;
            var controlsHeight = docked ? 41f : 58f;
            shiftLabel = Key("Shift", "Shift", -326f, controlsY, 140f, () => { shift = !shift; Refresh(); }, controlsHeight)
                .GetComponentInChildren<TMP_Text>();
            Key("Space", "Space", -121f, controlsY, 246f, () =>
            {
                if (pinyin && HasComposition) Choose(0);
                else Append(" ");
            }, controlsHeight);
            Key("Backspace", "Backspace", 97f, controlsY, 164f, Backspace, controlsHeight);
            Key("Done", "Done", 290f, controlsY, 168f, () => done(), controlsHeight);
            if (docked)
            {
                languageLabel = Key("Language", "EN / 拼音", -262f, -155f, 216f, ToggleLanguage, 40f).GetComponentInChildren<TMP_Text>();
                separator = Key("Separator", "'", -119f, -155f, 48f, () => TypeKey('\''), 40f);
                Key("NativeInput", "PC keyboard / IME", 28f, -155f, 224f, () => openNativeInput(), 40f);
                Key("CloseKeyboard", "Hide keyboard", 263f, -155f, 212f, () => cancel?.Invoke(), 40f);
            }
            else Text("Help", Rect("HelpLabel", root, 0f, -260f, 820f, 36f), help, 22, font);
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

        public void Show(bool preferPinyin = false)
        {
            field.DeactivateInputField();
            shift = false;
            if (!languageInitialized)
            {
                pinyin = preferPinyin && dictionary != null;
                languageInitialized = true;
            }
            Refresh();
            root.SetAsLastSibling();
            root.gameObject.SetActive(true);
        }

        public void Hide() => root.gameObject.SetActive(false);

        internal void ResetComposition()
        {
            composition = "";
            page = 0;
        }

        public void ReleaseMaterials()
        {
            if (surfaceMaterial != null) UnityEngine.Object.Destroy(surfaceMaterial);
            if (fontMaterial != null) UnityEngine.Object.Destroy(fontMaterial);
        }

        void ToggleLanguage()
        {
            if (dictionary == null) return;
            pinyin = !pinyin;
            ResetComposition();
            Refresh();
        }

        void TypeKey(char key)
        {
            if (pinyin && (char.IsLetter(key) || key == '\'' && HasComposition && !composition.EndsWith("'")))
            {
                if (composition.Length < 40) composition += char.ToLowerInvariant(key);
                page = 0;
                Refresh();
                return;
            }
            if (pinyin && HasComposition && key >= '1' && key <= '8') Choose(key - '1');
            else if (!HasComposition) Append(key.ToString());
        }

        void ChangePage(int delta)
        {
            page = Mathf.Clamp(page + delta, 0, Mathf.Max(0, (suggestions.Count - 1) / PageSize));
            Refresh();
        }

        void Choose(int index)
        {
            index += page * PageSize;
            if (index < 0 || index >= suggestions.Count) return;
            var selection = suggestions[index];
            if (field.characterLimit > 0 && field.text.Length + selection.Length > field.characterLimit) return;
            ResetComposition();
            Append(selection);
        }

        void Append(string value)
        {
            if (field.characterLimit > 0 && field.text.Length + value.Length > field.characterLimit) return;
            field.text += value;
            if (value.Length == 1 && char.IsLetter(value[0])) shift = false;
            page = 0;
            Refresh();
        }

        void Backspace()
        {
            if (HasComposition)
            {
                composition = composition.Substring(0, composition.Length - 1);
                page = 0;
                Refresh();
                return;
            }
            var starts = StringInfo.ParseCombiningCharacters(field.text);
            if (starts.Length == 0) return;
            field.text = field.text.Substring(0, starts[starts.Length - 1]);
            page = 0;
            Refresh();
        }

        void Refresh()
        {
            if (preview != null)
            {
                if (field.text.Length == 0) KitchenLocale.Set(preview, "Your note appears here.");
                else KitchenLocale.SetVerbatim(preview, field.text);
                count.text = $"{field.text.Length} / {field.characterLimit}";
            }
            KitchenLocale.Set(shiftLabel, shift ? "Shift on" : "Shift");
            if (candidateKeys == null) return;
            KitchenLocale.Set(languageLabel, pinyin ? "拼音 / EN" : "EN / 拼音");
            separator.interactable = !pinyin || HasComposition;
            suggestions = !pinyin ? Array.Empty<string>() : HasComposition ? dictionary.Search(composition) : dictionary.Associate(field.text);
            page = Mathf.Clamp(page, 0, Mathf.Max(0, (suggestions.Count - 1) / PageSize));
            previousCandidates.interactable = page > 0;
            nextCandidates.interactable = (page + 1) * PageSize < suggestions.Count;
            if (!pinyin) KitchenLocale.Set(compositionLabel, "English input");
            else if (!HasComposition) KitchenLocale.Set(compositionLabel, suggestions.Count == 0 ? "Enter pinyin" : "Related words");
            else KitchenLocale.SetVerbatim(compositionLabel, suggestions.Count == 0 ? composition :
                $"{composition}  ·  {page + 1}/{(suggestions.Count + PageSize - 1) / PageSize}");
            for (var i = 0; i < candidateKeys.Length; i++)
            {
                var index = i + page * PageSize;
                candidateKeys[i].interactable = index < suggestions.Count;
                KitchenLocale.SetVerbatim(candidateLabels[i], index < suggestions.Count ? suggestions[index] : "");
            }
        }

        Button Key(string name, string title, float x, float y, float width, UnityEngine.Events.UnityAction action,
            float height = 58f) => Button(Rect(name, root, x, y, width, height), title, TMP_Settings.defaultFontAsset, action);

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
            var label = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
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
