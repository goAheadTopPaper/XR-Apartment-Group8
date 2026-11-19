using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using XRApartment.Runtime;
using XRApartment.Runtime.UI;

namespace XRApartment.EditorTools
{
    public static class KitchenLocaleAuthoring
    {
        const string SimplifiedSource = "Assets/ThirdParty/NotoSansSC/XRKitchenHan-Regular.ttf";
        const string TraditionalSource = "Assets/ThirdParty/NotoSansTC/NotoSansCJKtc-Medium.otf";
        const string Art = KitchenAuthoring.Root + "Art/UI/";

        [MenuItem("XR Apartment/UI/Apply Three-Language UI")]
        public static void Apply()
        {
            var font = EnsureFont("FNT_KitchenChinese", SimplifiedSource);
            EnsureTraditionalFontFallback();
            KitchenAuthoring.EditPrefab(KitchenAuthoring.Prefabs + "UI/PREF_UI_LocomotionSettings.prefab", ConfigureMenu);
            KitchenAuthoring.EditPrefab(KitchenAuthoring.Prefabs + "PREF_XR_KitchenRig.prefab", root =>
            {
                var locale = root.GetComponent<KitchenLocale>();
                if (locale == null) locale = root.AddComponent<KitchenLocale>();
                locale.Configure(KitchenAuthoring.LoadRequired<TextAsset>(Art + "KitchenStrings.txt"), font);
            });
            AssetDatabase.SaveAssets();
        }

        internal static void EnsureTraditionalFontFallback()
        {
            var font = EnsureFont("FNT_KitchenTraditional", TraditionalSource);
            var fallbacks = TMP_Settings.fallbackFontAssets;
            if (fallbacks.Contains(font)) return;
            fallbacks.Add(font);
            EditorUtility.SetDirty(TMP_Settings.instance);
            AssetDatabase.SaveAssetIfDirty(TMP_Settings.instance);
        }

        static TMP_FontAsset EnsureFont(string name, string source)
        {
            var path = Art + name + ".asset";
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (font != null) return font;

            font = TMP_FontAsset.CreateFontAsset(KitchenAuthoring.LoadRequired<Font>(source),
                48, 6, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            font.name = name;
            AssetDatabase.CreateAsset(font, path);
            font.material.hideFlags = HideFlags.None;
            AssetDatabase.AddObjectToAsset(font.material, font);
            foreach (var atlas in font.atlasTextures)
            {
                atlas.hideFlags = HideFlags.None;
                AssetDatabase.AddObjectToAsset(atlas, font);
            }
            AssetDatabase.SaveAssetIfDirty(font);
            return font;
        }

        internal static void ConfigureMenu(GameObject root)
        {
            var panel = (RectTransform)root.transform.Find("Panel");
            panel.sizeDelta = new Vector2(640, 548);
            ((RectTransform)panel.Find("Shortcut")).anchoredPosition = new Vector2(0, -246);
            var button = panel.Find("Language")?.GetComponent<Button>();
            if (button == null) button = KitchenLocomotionAuthoring.Button("Language", panel, 0, -197, 300, 45, "Language");
            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = "Language";
            var data = new SerializedObject(root.GetComponent<LocomotionSettingsView>());
            data.FindProperty("language").objectReferenceValue = button;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
