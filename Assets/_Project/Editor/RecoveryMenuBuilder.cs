using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using XRApartment.Runtime;
using XRApartment.Runtime.Core;
using XRApartment.Runtime.UI;

namespace XRApartment.EditorTools
{
    /// <summary>One-time authoring of the Recovery Menu prefab and kitchen scene references.</summary>
    public static class RecoveryMenuBuilder
    {
        private const string PrefabPath = "Assets/_Project/Prefabs/UI/PREF_UI_RecoveryMenu.prefab";
        private const string ScenePath = "Assets/_Project/Scenes/SCN_Kitchen_Review.unity";

        [MenuItem("XR Apartment/UI/Build Recovery Menu")]
        public static void Build()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            {
            var root = new GameObject("PREF_UI_RecoveryMenu", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster), typeof(TrackedDeviceGraphicRaycaster));
            try
            {
                root.layer = LayerMask.NameToLayer("UI");
                var rect = (RectTransform)root.transform;
                rect.sizeDelta = new Vector2(900, 720);
                rect.localScale = Vector3.one * 0.001f;
                root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                root.GetComponent<Canvas>().sortingOrder = 200;
                root.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 20;
                var font = TMP_Settings.defaultFontAsset;
                var panel = Panel("Panel", rect, 0, 0, 900, 720, new Color(0.055f, 0.09f, 0.15f, 0.97f));
                Label("Title", panel, 0, 318, 820, 56, 34, "Recovery Menu", font);
                Label("Status", panel, 0, 267, 820, 45, 23, "Current state", font);
                Label("Progress", panel, 0, 229, 820, 43, 21, "Current progress", font);
                Button("Pause", panel, -225, 165, 200, 55, "Pause", font);
                Button("Resume", panel, -225, 165, 200, 55, "Resume", font);
                Button("RestoreStep", panel, 0, 165, 215, 55, "Restore step", font);
                Button("ResetTask", panel, 230, 165, 215, 55, "Reset task", font);
                Button("ReplayBenchmark", panel, -225, 99, 200, 55, "Replay benchmark", font);
                Button("Recenter", panel, 0, 99, 215, 55, "Recenter", font);
                Button("Retry", panel, 230, 99, 215, 55, "Retry", font);
                Label("SelectedItem", panel, -185, 35, 270, 45, 21, "Item: none", font);
                Button("ChooseItem", panel, 75, 35, 180, 50, "Next item", font);
                Button("ReturnItem", panel, 290, 35, 205, 50, "Return to tray", font);
                var input = InputField("SkipReason", panel, -80, -35, 560, 58, font);
                input.characterLimit = 120;
                Button("Skip", panel, 305, -35, 180, 58, "Skip Challenge", font);
                Button("DiscomfortReason", panel, -160, -96, 280, 48, "Discomfort", font);
                Button("BlockedReason", panel, 160, -96, 280, 48, "Interaction blocked", font);
                var operation = Label("Operation", panel, 0, -156, 825, 58, 20, "", font);
                operation.color = new Color(1f, 0.75f, 0.48f);
                var warning = Label("SaveWarning", panel, 0, -225, 825, 56, 19, "", font);
                warning.color = new Color(1f, 0.58f, 0.49f);
                Button("Close", panel, -270, -310, 200, 57, "Close", font);
                Button("SafeExit", panel, 250, -310, 260, 62, "Safe exit", font);
                var confirmation = Panel("ExitConfirmation", panel, 0, 0, 760, 330,
                    new Color(0.12f, 0.16f, 0.22f, 1f));
                Label("Question", confirmation, 0, 78, 690, 125, 27,
                    "End this session? Progress is kept in memory until a save is verified. You may exit even if saving fails.", font);
                Button("CancelExit", confirmation, -150, -89, 220, 62, "Stay", font);
                Button("ConfirmExit", confirmation, 150, -89, 250, 62, "End session", font);
                confirmation.gameObject.SetActive(false);

                var menu = root.AddComponent<RecoveryMenuView>();
                var fields = new SerializedObject(menu);
                Assign(fields, "panel", panel.gameObject);
                Assign(fields, "exitConfirmation", confirmation.gameObject);
                foreach (var field in new[] { "status", "progress", "operation", "saveWarning", "selectedItem" })
                    Assign(fields, field, panel.Find(ToName(field)).GetComponent<TMP_Text>());
                foreach (var field in new[] { "pause", "resume", "restoreStep", "resetTask", "returnItem",
                    "replayBenchmark", "recenter", "retry", "skip", "safeExit", "chooseItem", "close",
                    "discomfortReason", "blockedReason" })
                    Assign(fields, field, panel.Find(ToName(field)).GetComponent<Button>());
                Assign(fields, "confirmExit", confirmation.Find("ConfirmExit").GetComponent<Button>());
                Assign(fields, "cancelExit", confirmation.Find("CancelExit").GetComponent<Button>());
                Assign(fields, "skipReason", input);
                fields.FindProperty("headDistance").floatValue = 0.65f;
                fields.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
            }

            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
                throw new System.InvalidOperationException("Open SCN_Kitchen_Review before scene wiring.");
            var host = Object.FindObjectOfType<JourneyRuntimeHost>(true);
            var view = Object.FindObjectOfType<JourneyWorldSpaceView>(true);
            var origin = Object.FindObjectOfType<XROrigin>(true);
            if (host == null || view == null || origin == null)
                throw new System.InvalidOperationException("Journey host, view or XR Origin is missing.");
            var instance = GameObject.Find("PREF_UI_RecoveryMenu");
            if (instance == null)
            {
                instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
                instance.name = "PREF_UI_RecoveryMenu";
            }
            var recovery = instance.GetComponent<RecoveryMenuView>();
            instance.GetComponent<Canvas>().worldCamera = origin.Camera;
            var sceneFields = new SerializedObject(recovery);
            Assign(sceneFields, "host", host);
            Assign(sceneFields, "xrOrigin", origin);
            sceneFields.ApplyModifiedPropertiesWithoutUndo();
            var viewFields = new SerializedObject(view);
            Assign(viewFields, "recoveryMenu", recovery);
            viewFields.ApplyModifiedPropertiesWithoutUndo();
            var oldMenu = Object.FindObjectOfType<KitchenMenuController>(true);
            if (oldMenu != null) oldMenu.gameObject.SetActive(false);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        private static string ToName(string name) => char.ToUpperInvariant(name[0]) + name.Substring(1);

        private static void Assign(SerializedObject fields, string name, Object value)
        {
            var property = fields.FindProperty(name);
            if (property == null) throw new System.InvalidOperationException("Missing Recovery Menu field: " + name);
            property.objectReferenceValue = value;
        }

        private static RectTransform Panel(string name, Transform parent, float x, float y,
            float width, float height, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = LayerMask.NameToLayer("UI");
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
            go.GetComponent<Image>().color = color;
            return rect;
        }

        private static TMP_Text Label(string name, Transform parent, float x, float y,
            float width, float height, int size, string value, TMP_FontAsset font)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.layer = LayerMask.NameToLayer("UI");
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = size;
            text.text = value;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = true;
            text.raycastTarget = false;
            return text;
        }

        private static Button Button(string name, Transform parent, float x, float y,
            float width, float height, string caption, TMP_FontAsset font)
        {
            var rect = Panel(name, parent, x, y, width, height, new Color(.17f, .32f, .43f, 1f));
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            Label("Label", rect, 0, 0, width - 12, height - 8, 21, caption, font);
            return button;
        }

        private static TMP_InputField InputField(string name, Transform parent, float x,
            float y, float width, float height, TMP_FontAsset font)
        {
            var rect = Panel(name, parent, x, y, width, height, new Color(.17f, .23f, .3f, 1f));
            var input = rect.gameObject.AddComponent<TMP_InputField>();
            input.targetGraphic = rect.GetComponent<Image>();
            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewport.layer = LayerMask.NameToLayer("UI");
            var viewportRect = (RectTransform)viewport.transform;
            viewportRect.SetParent(rect, false);
            viewportRect.anchorMin = viewportRect.anchorMax = new Vector2(.5f, .5f);
            viewportRect.sizeDelta = new Vector2(width - 24, height - 8);
            var value = Label("Text", viewportRect, 0, 0, width - 38, height - 12, 21, "", font);
            value.alignment = TextAlignmentOptions.MidlineLeft;
            var placeholder = Label("Placeholder", viewportRect, 0, 0, width - 38, height - 12,
                20, "Reason to skip this Challenge", font);
            placeholder.alignment = TextAlignmentOptions.MidlineLeft;
            placeholder.color = new Color(.72f, .76f, .81f);
            input.textViewport = viewportRect;
            input.textComponent = (TextMeshProUGUI)value;
            input.placeholder = placeholder;
            input.lineType = TMP_InputField.LineType.SingleLine;
            return input;
        }
    }
}
