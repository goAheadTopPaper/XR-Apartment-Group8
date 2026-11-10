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
        const string Source = "Assets/ThirdParty/NotoSansSC/XRKitchenHan-Regular.ttf";
        const string Art = KitchenAuthoring.Root + "Art/UI/";

        [MenuItem("XR Apartment/UI/Apply Chinese and English")]
        public static void Apply()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Art + "FNT_KitchenChinese.asset");
            if (font == null)
            {
                font = TMP_FontAsset.CreateFontAsset(KitchenAuthoring.LoadRequired<Font>(Source),
                    48, 6, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
                font.name = "FNT_KitchenChinese";
                AssetDatabase.CreateAsset(font, Art + "FNT_KitchenChinese.asset");
                font.material.hideFlags = HideFlags.None;
                AssetDatabase.AddObjectToAsset(font.material, font);
                foreach (var atlas in font.atlasTextures)
                {
                    atlas.hideFlags = HideFlags.None;
                    AssetDatabase.AddObjectToAsset(atlas, font);
                }
            }
            KitchenAuthoring.EditPrefab(KitchenAuthoring.Prefabs + "UI/PREF_UI_LocomotionSettings.prefab", ConfigureMenu);
            KitchenAuthoring.EditPrefab(KitchenAuthoring.Prefabs + "PREF_XR_KitchenRig.prefab", root =>
            {
                var locale = root.GetComponent<KitchenLocale>();
                if (locale == null) locale = root.AddComponent<KitchenLocale>();
                locale.Configure(KitchenAuthoring.LoadRequired<TextAsset>(Art + "KitchenStrings.txt"), font);
            });
            AssetDatabase.SaveAssets();
        }

        internal static void ConfigureMenu(GameObject root)
        {
            var panel = (RectTransform)root.transform.Find("Panel");
            panel.sizeDelta = new Vector2(640, 548);
            ((RectTransform)panel.Find("Shortcut")).anchoredPosition = new Vector2(0, -246);
            var button = panel.Find("Language")?.GetComponent<Button>();
            if (button == null) button = KitchenLocomotionAuthoring.Button("Language", panel, 0, -197, 300, 45, "简体中文 / English");
            var data = new SerializedObject(root.GetComponent<LocomotionSettingsView>());
            data.FindProperty("language").objectReferenceValue = button;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
