using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace XRApartment.Runtime.UI
{
    public sealed class LanguageSelectionView : MonoBehaviour
    {
        public static bool SuppressAutoShow;
        const float WorldScale = .0012f;
        static LanguageSelectionView instance;
        readonly RaycastHit[] panelHits = new RaycastHit[48];
        Canvas canvas;
        GameObject panel;
        LocomotionSettingsView startupMenu;
        Camera startupCamera;
        Action startupConfirmed;

        internal static bool IsVisible => instance != null && instance.canvas.gameObject.activeSelf;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Application.isPlaying && !SuppressAutoShow && !KitchenLocale.HasSavedPreference) Show();
        }

        public static void Show()
        {
            if (Application.isPlaying) GetOrCreate().Open();
        }

        internal static void ShowForStartup(LocomotionSettingsView menu, Camera camera, Action confirmed)
        {
            if (SuppressAutoShow)
            {
                confirmed();
                return;
            }
            var picker = GetOrCreate();
            picker.startupMenu = menu;
            picker.startupCamera = camera;
            picker.startupConfirmed = confirmed;
            menu.OpenAuxiliary(picker.canvas.gameObject);
            picker.Place();
        }

        internal static void CancelStartup(LocomotionSettingsView menu)
        {
            if (instance != null && instance.startupMenu == menu) instance.Close();
        }

        static LanguageSelectionView GetOrCreate()
        {
            if (instance == null)
            {
                var host = new GameObject("Language Selection", typeof(LanguageSelectionView));
                DontDestroyOnLoad(host);
                instance = host.GetComponent<LanguageSelectionView>();
                instance.Build();
            }
            return instance;
        }

        public static void Hide()
        {
            if (instance != null) instance.Close();
        }

        void Build()
        {
            var canvasObject = new GameObject("Language Selection Canvas", typeof(RectTransform), typeof(Canvas),
                typeof(GraphicRaycaster), typeof(TrackedDeviceGraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 400;
            var rect = (RectTransform)canvas.transform;
            rect.sizeDelta = new Vector2(900, 520);
            rect.localScale = Vector3.one * WorldScale;

            panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canvas.transform, false);
            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(.05f, .06f, .08f, .93f);

            var title = Label("Title", panel.transform, new Vector2(0, 170), new Vector2(820, 80), 40, FontStyles.Bold);
            KitchenLocale.Set(title, "Choose Language");
            var subtitle = Label("Subtitle", panel.transform, new Vector2(0, 115), new Vector2(820, 60), 26, FontStyles.Normal);
            KitchenLocale.Set(subtitle, "Select the language for menus and guidance.");

            Choice("English", new Vector2(-285, 10), KitchenLocale.Language.English);
            Choice("Simplified Chinese", new Vector2(0, 10), KitchenLocale.Language.SimplifiedChinese);
            Choice("Traditional Chinese", new Vector2(285, 10), KitchenLocale.Language.TraditionalChinese);

            var note = Label("Note", panel.transform, new Vector2(0, -140), new Vector2(820, 60), 22, FontStyles.Normal);
            KitchenLocale.Set(note, "You can change this later from the language button.");
            Close();
        }

        void Choice(string key, Vector2 position, KitchenLocale.Language language)
        {
            var go = new GameObject(key, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(panel.transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(270, 90);
            go.GetComponent<Image>().color = new Color(.18f, .22f, .3f, 1f);
            var label = Label("Label", go.transform, Vector2.zero, new Vector2(250, 70), 28, FontStyles.Bold);
            KitchenLocale.Set(label, key);
            go.GetComponent<Button>().onClick.AddListener(() => Choose(language));
        }

        TMP_Text Label(string name, Transform parent, Vector2 position, Vector2 size, int fontSize, FontStyles style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var text = go.GetComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        void Choose(KitchenLocale.Language language)
        {
            var camera = startupCamera != null ? startupCamera : Camera.main;
            if (camera != null) KitchenAudio.Play(camera.gameObject.scene, KitchenCue.Menu, camera.transform.position, .65f);
            KitchenLocale.Select(language);
            var confirmed = startupConfirmed;
            Close();
            confirmed?.Invoke();
        }

        void Open()
        {
            canvas.gameObject.SetActive(true);
            Place();
        }

        void Close()
        {
            var menu = startupMenu;
            startupMenu = null;
            startupCamera = null;
            startupConfirmed = null;
            canvas.gameObject.SetActive(false);
            if (menu != null) menu.Close();
        }

        void Place()
        {
            var camera = startupCamera != null ? startupCamera : Camera.main;
            if (camera == null) return;
            canvas.worldCamera = camera;
            var head = camera.transform;
            var pose = new Pose(head.position + head.forward * 1.1f, head.rotation);
            var movement = camera.GetComponentInParent<KitchenLocomotionSettings>();
            if (movement != null && movement.gameObject.scene.name == "SCN_Kitchen_Cooking")
                KitchenPanelPlacement.Place((RectTransform)canvas.transform, camera, movement.transform,
                    pose, Vector3.one * WorldScale, panelHits);
            else
            {
                canvas.transform.localScale = Vector3.one * WorldScale;
                canvas.transform.SetPositionAndRotation(pose.position, pose.rotation);
            }
        }

        void LateUpdate() { if (canvas.gameObject.activeSelf) Place(); }
    }
}
