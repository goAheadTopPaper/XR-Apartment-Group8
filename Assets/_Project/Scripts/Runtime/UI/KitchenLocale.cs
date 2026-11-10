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
        public const string Preference = "XRApartment.UI.Chinese";
        [SerializeField] TextAsset table;
        [SerializeField] TMP_FontAsset hanzi;
        static readonly Dictionary<string, string> Exact = new Dictionary<string, string>(StringComparer.Ordinal);
        static readonly List<(Regex pattern, string translation)> Patterns = new List<(Regex, string)>();
        static readonly Dictionary<TMP_Text, string> Labels = new Dictionary<TMP_Text, string>();
        static readonly string[] Separators = { "  /  ", "  |  ", " | " };
        static readonly Regex GroupReference = new Regex(@"\$(\d+)", RegexOptions.CultureInvariant);
        static TextAsset loadedTable;
        static bool chinese;

        public static bool Chinese => chinese;
        public void Configure(TextAsset translations, TMP_FontAsset fallback) { table = translations; hanzi = fallback; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetLocale()
        {
            Exact.Clear();
            Patterns.Clear();
            Labels.Clear();
            loadedTable = null;
            chinese = false;
        }

        void Awake()
        {
            if (table == null || hanzi == null) return;
            if (table != loadedTable)
            {
                Exact.Clear();
                Patterns.Clear();
                foreach (var line in table.text.Split('\n'))
                {
                    var columns = line.TrimEnd('\r').Split('\t');
                    if (columns.Length != 2) continue;
                    if (columns[0].StartsWith("^", StringComparison.Ordinal))
                        Patterns.Add((new Regex(columns[0], RegexOptions.CultureInvariant), columns[1]));
                    else Exact.Add(columns[0], columns[1]);
                }
                loadedTable = table;
            }
            chinese = PlayerPrefs.GetInt(Preference) == 1;
            if (!TMP_Settings.fallbackFontAssets.Contains(hanzi)) TMP_Settings.fallbackFontAssets.Add(hanzi);
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

        public static void Toggle()
        {
            chinese = !chinese;
            PlayerPrefs.SetInt(Preference, chinese ? 1 : 0);
            PlayerPrefs.Save();
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
            if (!chinese || string.IsNullOrEmpty(value)) return value;
            if (Exact.TryGetValue(value, out var text)) return text;
            foreach (var entry in Patterns)
            {
                var match = entry.pattern.Match(value);
                if (match.Success)
                    return GroupReference.Replace(entry.translation, group =>
                        Translate(match.Groups[int.Parse(group.Groups[1].Value)].Value));
            }
            if (value.IndexOf('\n') >= 0)
                return string.Join("\n", Array.ConvertAll(value.Split('\n'), Translate));
            foreach (var separator in Separators)
                if (value.Contains(separator))
                    return string.Join(separator, Array.ConvertAll(value.Split(new[] { separator }, StringSplitOptions.None), Translate));
            return value;
        }
    }
}
