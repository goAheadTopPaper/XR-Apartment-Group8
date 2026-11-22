using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using XRApartment.Runtime;
using XRApartment.Runtime.UI;

namespace XRApartment.Tests
{
    public sealed class KitchenLocaleStartupTests
    {
        [UnityTest]
        public IEnumerator FreshProfileShowsTraditionalChineseAndSavesTheChosenLanguage()
        {
            var originalLanguage = KitchenLocale.Current;
            var saved = StoredPreference(KitchenLocale.Preference);
            var legacy = StoredPreference(KitchenLocale.LegacyPreference);
            try
            {
                PlayerPrefs.DeleteKey(KitchenLocale.Preference);
                PlayerPrefs.DeleteKey(KitchenLocale.LegacyPreference);
                SceneManager.LoadScene("SCN_Kitchen_Cooking");
                yield return null;
                yield return null;

                var view = Object.FindObjectOfType<LanguageSelectionView>();
                var guide = Object.FindObjectOfType<KitchenActivityGuide>().transform.Find("Guide");
                Assert.That(view, Is.Not.Null);
                var panel = view.transform.Find("Language Selection Canvas/Panel");
                Assert.That(panel.Find("Title").GetComponent<TMP_Text>().text, Is.EqualTo("選擇語言"));
                Assert.That(KitchenLocale.Current, Is.EqualTo(KitchenLocale.Language.TraditionalChinese));
                Assert.That(KitchenLocale.HasSavedPreference, Is.False);
                Assert.That(guide.gameObject.activeInHierarchy, Is.False, "The language picker must own the startup view.");

                panel.Find("English").GetComponent<Button>().onClick.Invoke();
                Assert.That(panel.gameObject.activeInHierarchy, Is.False);
                Assert.That(PlayerPrefs.GetInt(KitchenLocale.Preference), Is.EqualTo((int)KitchenLocale.Language.English));
                yield return null;
                Assert.That(guide.gameObject.activeInHierarchy, Is.False);
                Assert.That(guide.Find("Title").GetComponent<TMP_Text>().text, Is.EqualTo("Find the kitchen"));
                Assert.That(Time.timeScale, Is.EqualTo(1f));

                SceneManager.LoadScene("SCN_Kitchen_Cooking");
                yield return null;
                yield return null;
                Assert.That(panel.gameObject.activeInHierarchy, Is.True, "A saved language must not skip the startup picker.");
                Assert.That(panel.Find("Title").GetComponent<TMP_Text>().text, Is.EqualTo("Choose Language"));
            }
            finally
            {
                LanguageSelectionView.Hide();
                KitchenLocale.Select(originalLanguage);
                RestorePreference(KitchenLocale.Preference, saved);
                RestorePreference(KitchenLocale.LegacyPreference, legacy);
                PlayerPrefs.Save();
            }
        }

        [UnityTest]
        public IEnumerator SavedProfileStillOpensTheLanguagePicker()
        {
            var originalLanguage = KitchenLocale.Current;
            var saved = StoredPreference(KitchenLocale.Preference);
            var legacy = StoredPreference(KitchenLocale.LegacyPreference);
            try
            {
                KitchenLocale.Select(KitchenLocale.Language.English);
                SceneManager.LoadScene("SCN_Kitchen_Cooking");
                yield return null;
                yield return null;

                var view = Object.FindObjectOfType<LanguageSelectionView>();
                Assert.That(view, Is.Not.Null, "The picker must open even with an existing language preference.");
                Assert.That(view.transform.Find("Language Selection Canvas/Panel").gameObject.activeInHierarchy, Is.True);
            }
            finally
            {
                LanguageSelectionView.Hide();
                KitchenLocale.Select(originalLanguage);
                RestorePreference(KitchenLocale.Preference, saved);
                RestorePreference(KitchenLocale.LegacyPreference, legacy);
                PlayerPrefs.Save();
            }
        }

        [Test]
        public void LocalizedCalibrationTextRendersWithoutReplacementGlyphs()
        {
            const string content = "請選擇坐姿並調節視線，校準後確認。简体中文";
            var host = new GameObject("Localized Text Test", typeof(Canvas));
            try
            {
                var label = new GameObject("Label", typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
                label.transform.SetParent(host.transform, false);
                label.font = TMP_Settings.defaultFontAsset;
                label.fontStyle = FontStyles.Bold;
                label.text = content;
                label.ForceMeshUpdate();

                Assert.That(label.textInfo.characterCount, Is.EqualTo(content.Length));
                for (var index = 0; index < content.Length; index++)
                {
                    var expected = content[index];
                    var rendered = label.textInfo.characterInfo[index].textElement.unicode;
                    Assert.That(rendered, Is.EqualTo((uint)expected), $"Missing glyph U+{(int)expected:X4} ({expected})");
                }
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        static int? StoredPreference(string key) => PlayerPrefs.HasKey(key) ? PlayerPrefs.GetInt(key) : (int?)null;

        static void RestorePreference(string key, int? value)
        {
            if (value.HasValue) PlayerPrefs.SetInt(key, value.Value);
            else PlayerPrefs.DeleteKey(key);
        }
    }
}
