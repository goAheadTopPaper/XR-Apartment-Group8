using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace XRApartment.Runtime
{
    [DefaultExecutionOrder(-200)]
    public sealed class KitchenLocale : MonoBehaviour
    {
        public enum Language
        {
            English = 0,
            SimplifiedChinese = 1,
            TraditionalChinese = 2
        }

        public const string Preference = "XRApartment.UI.Language";
        public const string LegacyPreference = "XRApartment.UI.Chinese";
        [SerializeField] TextAsset table;
        [SerializeField] TMP_FontAsset hanzi;
        static readonly Dictionary<string, string[]> Exact = new Dictionary<string, string[]>(StringComparer.Ordinal);
        static readonly List<(Regex pattern, string[] translations)> Patterns = new List<(Regex, string[])>();
        static readonly Dictionary<TMP_Text, string> Labels = new Dictionary<TMP_Text, string>();
        static readonly string[] Separators = { "  /  ", "  |  ", " | ", " · " };
        static readonly Regex GroupReference = new Regex(@"\$(\d+)", RegexOptions.CultureInvariant);
        static TextAsset loadedTable;
        static Language language = Language.TraditionalChinese;

        public static Language Current => language;
        public static bool Chinese => language != Language.English;
        public static bool HasSavedPreference => PlayerPrefs.HasKey(Preference) || PlayerPrefs.HasKey(LegacyPreference);
        public void Configure(TextAsset translations, TMP_FontAsset fallback) { table = translations; hanzi = fallback; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetLocale()
        {
            Exact.Clear();
            Patterns.Clear();
            Labels.Clear();
            loadedTable = null;
            language = ReadPreference();
        }

        static Language ReadPreference()
        {
            if (PlayerPrefs.HasKey(Preference))
                return (Language)Mathf.Clamp(PlayerPrefs.GetInt(Preference, 0), 0, 2);
            if (PlayerPrefs.HasKey(LegacyPreference))
                return PlayerPrefs.GetInt(LegacyPreference) == 1 ? Language.SimplifiedChinese : Language.English;
            return Language.TraditionalChinese;
        }

        void Awake()
        {
            if (table != null && table != loadedTable)
            {
                Exact.Clear();
                Patterns.Clear();
                foreach (var line in table.text.Split('\n'))
                {
                    var columns = line.TrimEnd('\r').Split('\t');
                    if (columns.Length < 2) continue;
                    var translations = columns.Length >= 3
                        ? new[] { columns[1], columns[2] }
                        : new[] { columns[1], columns[1] };
                    if (columns[0].StartsWith("^", StringComparison.Ordinal))
                        Patterns.Add((new Regex(columns[0], RegexOptions.CultureInvariant), translations));
                    else
                        Exact[columns[0]] = translations;
                }
                loadedTable = table;
            }
            var preference = ReadPreference();
            if (language != preference)
            {
                language = preference;
                Refresh();
            }
            if (hanzi != null && !TMP_Settings.fallbackFontAssets.Contains(hanzi))
                TMP_Settings.fallbackFontAssets.Add(hanzi);
            // TMP 3.0.9 uses this cached primary atlas when it creates a second page
            // on an Editor font asset. Warm it before the first dynamic glyph lookup.
            foreach (var fallback in TMP_Settings.fallbackFontAssets)
                if (fallback != null && fallback.atlasTextures != null && fallback.atlasTextures.Length > 0)
                    _ = fallback.atlasTexture;
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                foreach (var label in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    var input = label.GetComponentInParent<TMP_InputField>(true);
                    if (input == null || input.textComponent != label) Set(label, label.text);
                }
                foreach (var button in root.GetComponentsInChildren<Button>(true)) AddButtonSound(button);
            }
        }

        void OnDestroy()
        {
            var scene = gameObject.scene;
            var stale = new List<TMP_Text>();
            foreach (var label in Labels.Keys)
                if (label == null || label.gameObject.scene == scene) stale.Add(label);
            foreach (var label in stale) Labels.Remove(label);
        }

        public static void Select(Language value)
        {
            language = value;
            PlayerPrefs.SetInt(Preference, (int)value);
            PlayerPrefs.DeleteKey(LegacyPreference);
            PlayerPrefs.Save();
            Refresh();
        }

        public static void Toggle() => Select((Language)(((int)language + 1) % 3));

        static void Refresh()
        {
            foreach (var label in Labels)
                if (label.Key != null) label.Key.text = Translate(label.Value);
        }

        public static void Set(TMP_Text label, string english)
        {
            if (label == null) return;
            english = english ?? string.Empty;
            if (Labels.TryGetValue(label, out var previous) && previous == english) return;
            Labels[label] = english;
            label.text = Translate(english);
        }

        public static string T(string english) => Translate(english);

        public static void SetLabel(Button button, string english)
        {
            if (button != null) Set(button.GetComponentInChildren<TMP_Text>(true), english);
        }

        public static void SetVerbatim(TMP_Text label, string value)
        {
            if (label == null) return;
            Labels.Remove(label);
            label.text = value;
        }

        public static string Source(TMP_Text label) => label == null ? string.Empty :
            Labels.TryGetValue(label, out var value) ? value : label.text;

        public static void AddButtonSound(Button button)
        {
            button.onClick.AddListener(() =>
                KitchenAudio.Play(button.gameObject.scene, KitchenCue.Menu, button.transform.position, .6f));
        }

        static string Translate(string value)
        {
            if (language == Language.English || string.IsNullOrEmpty(value)) return value;
            var slot = language == Language.SimplifiedChinese ? 0 : 1;
            if (Exact.TryGetValue(value, out var text)) return Normalize(text[slot] ?? value);
            foreach (var entry in Patterns)
            {
                var match = entry.pattern.Match(value);
                if (match.Success)
                    return Normalize(GroupReference.Replace(entry.translations[slot] ?? value, group =>
                        Translate(match.Groups[int.Parse(group.Groups[1].Value)].Value)));
            }
            if (value.IndexOf('\n') >= 0)
                return string.Join("\n", Array.ConvertAll(value.Split('\n'), Translate));
            foreach (var separator in Separators)
                if (value.Contains(separator))
                    return string.Join(separator, Array.ConvertAll(value.Split(new[] { separator }, StringSplitOptions.None), Translate));
            return Normalize(value);
        }

        static string Normalize(string value) => value == null ? null : value.Replace("\\n", "\n");
    }
}
