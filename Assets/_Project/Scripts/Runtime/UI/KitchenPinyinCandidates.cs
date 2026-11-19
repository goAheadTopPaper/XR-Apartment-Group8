using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace XRApartment.Runtime.UI
{
    // Offline phrase lookup. Index both readings and words so typing and association
    // only visit a bounded slice of the bundled lexicon.
    internal sealed class KitchenPinyinCandidates
    {
        const int Limit = 96;
        static KitchenPinyinCandidates instance;
        static readonly (string reading, string word)[] Common =
        {
            ("nihao", "你好"), ("xiexie", "谢谢"), ("wojuede", "我觉得"), ("wojianyi", "我建议"),
            ("chufang", "厨房"), ("chufang", "廚房"), ("bingxiang", "冰箱"), ("bingxiangmen", "冰箱门"),
            ("bingxiangmen", "冰箱門"),
            ("gongzuotai", "工作台"), ("taimian", "台面"), ("taimian", "檯面"), ("shuicao", "水槽"),
            ("bashou", "把手"), ("guimen", "柜门"), ("guimen", "櫃門"), ("danglu", "挡路"), ("danglu", "擋路"),
            ("bukang", "不扛"), ("bufangbian", "不方便"), ("henfangbian", "很方便"), ("haoyong", "好用"),
            ("kaimen", "开门"), ("kaimen", "開門"), ("shouna", "收纳"), ("jianyi", "建议"),
            ("gaijin", "改进"), ("kongjian", "空间"), ("tongdao", "通道"), ("keyi", "可以"),
            ("xiwang", "希望"), ("fankui", "反馈"), ("fankui", "回饋"), ("shushi", "舒适")
        };
        static readonly Dictionary<string, string[]> Next = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["冰箱"] = new[] { "门", "門", "把手", "有点挡路", "開門方便" },
            ["冰箱门"] = new[] { "把手", "不好开", "有点窄" },
            ["工作台"] = new[] { "太短", "高度合适", "不方便" },
            ["厨房"] = new[] { "空间", "布局", "很方便" },
            ["廚房"] = new[] { "空間", "佈局", "很方便" },
            ["你好"] = new[] { "，", "谢谢", "我是" },
            ["我"] = new[] { "觉得", "建議", "希望" },
            ["我觉得"] = new[] { "很好", "可以", "不方便" },
            ["建议"] = new[] { "调整", "增加", "改进" }
        };
        readonly Dictionary<string, string> characters = new Dictionary<string, string>(StringComparer.Ordinal);
        readonly Dictionary<string, List<string>> readings = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        readonly string[] keys, words;

        internal KitchenPinyinCandidates(string singles, string phrases)
        {
            foreach (var row in Rows(singles)) characters[row.key] = row.value;
            foreach (var seed in Common) Add(seed.reading.ToLowerInvariant(), seed.word);
            foreach (var row in Rows(phrases)) Add(row.key, row.value);
            keys = readings.Keys.OrderBy(key => key, StringComparer.Ordinal).ToArray();
            words = readings.Values.SelectMany(values => values).Distinct().OrderBy(word => word, StringComparer.Ordinal).ToArray();
        }

        void Add(string key, string word)
        {
            if (!readings.TryGetValue(key, out var values)) readings[key] = values = new List<string>();
            if (!values.Contains(word)) values.Add(word);
        }

        static IEnumerable<(string key, string value)> Rows(string data)
        {
            if (string.IsNullOrEmpty(data)) yield break;
            foreach (var raw in data.Split('\n'))
            {
                var line = raw.TrimEnd('\r');
                int split = line.IndexOf('\t');
                if (line.Length > 0 && line[0] != '#' && split > 0 && split < line.Length - 1)
                    yield return (line.Substring(0, split), line.Substring(split + 1));
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => instance = null;

        internal static KitchenPinyinCandidates Load()
        {
            if (instance != null) return instance;
            var singles = Resources.Load<TextAsset>("KitchenFeedbackPinyin");
            var phrases = Resources.Load<TextAsset>("KitchenFeedbackPhrases");
            return singles != null ? instance = new KitchenPinyinCandidates(singles.text, phrases?.text) : null;
        }

        internal IReadOnlyList<string> Search(string typed)
        {
            var key = typed.Replace("'", "").Replace(" ", "").ToLowerInvariant();
            if (key.Length == 0) return Array.Empty<string>();
            var results = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            void Offer(string word) { if (results.Count < Limit && seen.Add(word)) results.Add(word); }
            if (readings.TryGetValue(key, out var exact)) foreach (var word in exact) Offer(word);
            characters.TryGetValue(key, out var single);
            // Single exact syllables still put familiar single characters on the first page.
            if (single != null) foreach (char c in single.Take(16)) Offer(c.ToString());
            if (exact == null && single == null && key.Length >= 4 && CompleteSequence(key) is string sequence) Offer(sequence);
            foreach (var seed in Common) if (seed.reading.StartsWith(key, StringComparison.OrdinalIgnoreCase)) Offer(seed.word);
            int start = LowerBound(keys, key);
            for (int i = start; i < keys.Length && i < start + 144 && results.Count < 48 && keys[i].StartsWith(key, StringComparison.Ordinal); i++)
                foreach (var word in readings[keys[i]]) { if (results.Count >= 48) break; Offer(word); }
            if (single != null) foreach (char c in single) Offer(c.ToString());
            return results;
        }

        string CompleteSequence(string key)
        {
            var best = new string[key.Length + 1];
            var cost = Enumerable.Repeat(int.MaxValue, best.Length).ToArray();
            best[0] = ""; cost[0] = 0;
            for (int i = 0; i < key.Length; i++)
                if (best[i] != null)
                    for (int length = 1; length <= Math.Min(24, key.Length - i); length++)
                    {
                        string code = key.Substring(i, length);
                        string token = readings.TryGetValue(code, out var phrases) ? phrases[0] :
                            characters.TryGetValue(code, out var chars) ? chars[0].ToString() : null;
                        if (token == null) continue;
                        int score = cost[i] + (token.Length == 1 ? 4 : 1);
                        if (score >= cost[i + length]) continue;
                        cost[i + length] = score;
                        best[i + length] = best[i] + token;
                    }
            return best[key.Length];
        }

        internal IReadOnlyList<string> Associate(string text)
        {
            if (string.IsNullOrEmpty(text)) return Array.Empty<string>();
            var results = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            void Offer(string value) { if (value.Length > 0 && results.Count < 24 && seen.Add(value)) results.Add(value); }
            int length = Math.Min(text.Length, 6);
            for (; length > 0 && results.Count < 24; length--)
            {
                string prefix = text.Substring(text.Length - length);
                if (Next.TryGetValue(prefix, out var defaults)) foreach (var next in defaults) Offer(next);
                int start = LowerBound(words, prefix);
                for (int i = start; i < words.Length && i < start + 32 && results.Count < 24 && words[i].StartsWith(prefix, StringComparison.Ordinal); i++)
                    Offer(words[i].Substring(prefix.Length));
            }
            return results;
        }

        static int LowerBound(string[] sorted, string key)
        {
            int index = Array.BinarySearch(sorted, key, StringComparer.Ordinal);
            return index >= 0 ? index : ~index;
        }
    }
}
