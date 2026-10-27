using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.XR.Interaction.Toolkit.UI;

using Unity.XR.CoreUtils;
using UnityEditor.SceneManagement;
using XRApartment.Runtime;
using XRApartment.Runtime.Core;
using XRApartment.Runtime.UI;

namespace XRApartment.EditorTools
{
    /// <summary>Authoring utility for the single reusable world-space journey panel.</summary>
    public static class JourneyUiPrefabBuilder
    {
        private const string Path = "Assets/_Project/Prefabs/UI/PREF_UI_JourneyStageOne.prefab";

        [InitializeOnLoadMethod]
        private static void RebuildAfterImport()
        {
            EditorApplication.delayCall += () =>
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Path);
                if (!EditorApplication.isPlayingOrWillChangePlaymode &&
                    (prefab == null || prefab.transform.Find("Panel/OrientationControls") == null))
                    Build();
            };
        }

        [MenuItem("XR Apartment/UI/Rebuild Setup Orientation UI Prefab")]
        public static void Build()
        {
            var root = new GameObject("PREF_UI_JourneyStageOne", typeof(RectTransform),
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(TrackedDeviceGraphicRaycaster),
                typeof(JourneyWorldSpaceView));
            try
            {
                var rect = (RectTransform)root.transform;
                rect.sizeDelta = new Vector2(1200, 800);
                rect.localScale = Vector3.one * 0.0011f;
                var canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.sortingOrder = 100;
                root.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 20;
                root.GetComponent<GraphicRaycaster>().ignoreReversedGraphics = false;
                var font = TMP_Settings.defaultFontAsset;
                var background = Panel("Panel", rect, 0, 0, 1160, 760,
                    new Color(0.06f, 0.10f, 0.16f, 0.96f));
                var state = Label("State", background, 0, 324, 1050, 65, 32, "Journey: Setup", font);
                var benchmark = Label("Design", background, -360, 278, 330, 38, 22, "Design: None", font);
                var progress = Label("Progress", background, 280, 278, 430, 38, 22, "Task: not started", font);
                var title = Label("PageTitle", background, 0, 180, 1040, 65, 38, "Kitchen design review", font);
                var body = Label("PageBody", background, 0, 25, 1010, 225, 30, "", font);
                body.alignment = TextAlignmentOptions.MidlineLeft;
                var error = Label("Error", background, 0, -108, 1010, 65, 25, "", font);
                error.color = new Color(1f, 0.65f, 0.3f);
                error.gameObject.SetActive(false);
                var setup = Panel("SetupControls", background, 0, -202, 1080, 100,
                    new Color(0.10f, 0.16f, 0.23f, 0.96f));
                var setupHint = Label("SetupHint", setup, -165, 25, 650, 45, 23,
                    "Choose a posture and press Calibrate.", font);
                var lowerEye = Button("LowerEye", setup, -390, -30, 140, 50, "- 5 cm", font);
                var raiseEye = Button("RaiseEye", setup, -230, -30, 140, 50, "+ 5 cm", font);
                var standing = Button("Standing", setup, 190, 17, 170, 55, "Standing", font);
                var seated = Button("Seated", setup, 385, 17, 170, 55, "Seated", font);
                var calibrate = Button("Calibrate", setup, 190, -37, 170, 50, "Calibrate", font);
                var complete = Button("CompleteSetup", setup, 385, -37, 170, 50, "Continue", font);
                var orientation = Panel("OrientationControls", background, 0, -202, 1080, 100,
                    new Color(0.10f, 0.16f, 0.23f, 0.96f));
                var status = Label("PracticeStatus", orientation, -90, 23, 830, 43, 23,
                    "Teleport [ ]  Select [ ]  Grab [ ]  Reset [ ]  Exit [ ]", font);
                var select = Button("SelectPractice", orientation, -220, -29, 260, 49,
                    "Select practice", font);
                var teleportPractice = Button("PracticeTeleport", orientation, 75, -29, 260, 49,
                    "Teleport now", font);
                var reset = Button("ResetPractice", orientation, 75, -29, 260, 49,
                    "Reset view", font);
                var acknowledge = Button("Acknowledge", orientation, 370, -29, 260, 49,
                    "I understand / Explore", font);
                var previous = Button("Previous", background, -385, -310, 190, 65, "Back", font);
                var next = Button("Next", background, -155, -310, 190, 65, "Next", font);
                var exit = Button("SafeExit", background, 365, -310, 255, 65, "Safe exit", font);
                exit.targetGraphic.color = new Color(0.65f, 0.23f, 0.22f);
                var compactButton = Button("Compact", background, 365, 324, 200, 52, "Compact", font);
                var pauseButton = Button("Pause", background, -440, 324, 150, 52, "Pause", font);
                var retryButton = Button("Retry", background, -255, 324, 150, 52, "Retry", font);
                retryButton.gameObject.SetActive(false);
                var compactPanel = CreateCompactPanel(rect, font, out var compactTitle,
                    out var compactInstruction, out var compactStatus, out var expandButton);

                var view = new SerializedObject(root.GetComponent<JourneyWorldSpaceView>());
                Set(view, "stateText", state); Set(view, "benchmarkText", benchmark);
                Set(view, "progressText", progress); Set(view, "errorText", error);
                Set(view, "pageTitle", title); Set(view, "pageBody", body);
                Set(view, "setupHintText", setupHint); Set(view, "practiceStatus", status);
                Set(view, "setupControls", setup.gameObject);
                Set(view, "orientationControls", orientation.gameObject);
                Set(view, "standingButton", standing); Set(view, "seatedButton", seated);
                Set(view, "lowerEyeButton", lowerEye); Set(view, "raiseEyeButton", raiseEye);
                Set(view, "calibrateButton", calibrate); Set(view, "completeSetupButton", complete);
                Set(view, "selectPracticeButton", select); Set(view, "resetPracticeButton", reset);
                Set(view, "teleportPracticeButton", teleportPractice);
                Set(view, "acknowledgeButton", acknowledge);
                Set(view, "previousButton", previous); Set(view, "nextButton", next);
                Set(view, "exitButton", exit);
                Set(view, "compactPanel", compactPanel.gameObject);
                Set(view, "compactTitle", compactTitle); Set(view, "compactStatus", compactStatus);
                Set(view, "compactInstruction", compactInstruction);
                Set(view, "compactButton", compactButton); Set(view, "expandButton", expandButton);
                Set(view, "setupPauseButton", pauseButton); Set(view, "setupRetryButton", retryButton);
                view.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, Path);
                AssetDatabase.SaveAssets();
            }
            finally { Object.DestroyImmediate(root); }
        }

        [MenuItem("XR Apartment/UI/Add Compact Journey Panel To Existing Prefab")]
        public static void AddCompactPanelToExistingPrefab()
        {
            var root = PrefabUtility.LoadPrefabContents(Path);
            try
            {
                var fullPanel = root.transform.Find("Panel");
                if (fullPanel == null) throw new System.InvalidOperationException("Full panel is missing.");
                var font = TMP_Settings.defaultFontAsset;
                var compactButton = fullPanel.Find("Compact") != null
                    ? fullPanel.Find("Compact").GetComponent<Button>()
                    : Button("Compact", fullPanel, 365, 324, 200, 52, "Compact", font);
                var pauseButton = fullPanel.Find("Pause") != null
                    ? fullPanel.Find("Pause").GetComponent<Button>()
                    : Button("Pause", fullPanel, -440, 324, 150, 52, "Pause", font);
                var retryButton = fullPanel.Find("Retry") != null
                    ? fullPanel.Find("Retry").GetComponent<Button>()
                    : Button("Retry", fullPanel, -255, 324, 150, 52, "Retry", font);
                retryButton.gameObject.SetActive(false);
                var compact = root.transform.Find("CompactPanel") as RectTransform;
                TextMeshProUGUI title, instruction, status;
                Button expand;
                if (compact == null)
                    compact = CreateCompactPanel((RectTransform)root.transform, font,
                        out title, out instruction, out status, out expand);
                else
                {
                    compact.sizeDelta = new Vector2(640, 330);
                    compact.anchoredPosition = new Vector2(-260, 190);
                    title = compact.Find("CompactTitle").GetComponent<TextMeshProUGUI>();
                    status = compact.Find("CompactStatus").GetComponent<TextMeshProUGUI>();
                    expand = compact.Find("Expand").GetComponent<Button>();
                    instruction = compact.Find("CompactInstruction") != null
                        ? compact.Find("CompactInstruction").GetComponent<TextMeshProUGUI>()
                        : Label("CompactInstruction", compact, 0, 30, 585, 130, 22, "", font);
                    var oldContinue = compact.Find("CompactContinue");
                    if (oldContinue != null) Object.DestroyImmediate(oldContinue.gameObject);
                    title.rectTransform.anchoredPosition = new Vector2(0, 126);
                    title.rectTransform.sizeDelta = new Vector2(580, 55);
                    status.rectTransform.anchoredPosition = new Vector2(0, -64);
                    status.rectTransform.sizeDelta = new Vector2(590, 55);
                    expand.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -127);
                }
                var view = new SerializedObject(root.GetComponent<JourneyWorldSpaceView>());
                Set(view, "compactPanel", compact.gameObject);
                Set(view, "compactTitle", title); Set(view, "compactStatus", status);
                Set(view, "compactInstruction", instruction);
                Set(view, "compactButton", compactButton); Set(view, "expandButton", expand);
                Set(view, "setupPauseButton", pauseButton); Set(view, "setupRetryButton", retryButton);
                view.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, Path);
                AssetDatabase.SaveAssets();
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static RectTransform CreateCompactPanel(RectTransform parent, TMP_FontAsset font,
            out TextMeshProUGUI title, out TextMeshProUGUI instruction,
            out TextMeshProUGUI status, out Button expand)
        {
            var compact = Panel("CompactPanel", parent, -260, 190, 640, 330,
                new Color(0.06f, 0.10f, 0.16f, 0.96f));
            title = Label("CompactTitle", compact, 0, 126, 580, 55, 26, "Orientation", font);
            instruction = Label("CompactInstruction", compact, 0, 30, 585, 130, 22, "", font);
            status = Label("CompactStatus", compact, 0, -64, 590, 55, 20, "", font);
            expand = Button("Expand", compact, 0, -127, 200, 54, "Expand", font);
            compact.gameObject.SetActive(false);
            return compact;
        }

        [MenuItem("XR Apartment/UI/Wire Setup Orientation In Review Scene")]
        public static void WireActiveReviewScene()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != "Assets/_Project/Scenes/SCN_Kitchen_Review.unity") return;
            var hosts = Object.FindObjectsOfType<JourneyRuntimeHost>(true);
            var views = Object.FindObjectsOfType<JourneyWorldSpaceView>(true);
            var origins = Object.FindObjectsOfType<XROrigin>(true);
            var teleports = Object.FindObjectsOfType<UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationProvider>(true);
            var grabs = Object.FindObjectsOfType<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>(true);
            if (hosts.Length != 1 || views.Length != 1 || origins.Length != 1 || teleports.Length != 1)
            {
                Debug.LogError("Setup/Orientation scene wiring requires exactly one Host, view, XR Origin and TeleportationProvider.");
                return;
            }
            UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable cup = null;
            foreach (var grab in grabs)
                if (grab.name == "PREF_Prop_Cup") { cup = grab; break; }
            if (cup == null) { Debug.LogError("Setup/Orientation practice cup is missing."); return; }

            var view = new SerializedObject(views[0]);
            Set(view, "host", hosts[0]); Set(view, "xrOrigin", origins[0]);
            Set(view, "teleportationProvider", teleports[0]);
            Set(view, "practiceGrabTarget", cup);
            view.ApplyModifiedPropertiesWithoutUndo();
            var host = new SerializedObject(hosts[0]);
            Set(host, "viewAdapter", views[0]);
            host.ApplyModifiedPropertiesWithoutUndo();
            var canvas = views[0].GetComponent<Canvas>();
            canvas.worldCamera = origins[0].Camera;
            ((RectTransform)views[0].transform).sizeDelta = new Vector2(1200, 800);
            foreach (var legacy in Object.FindObjectsOfType<KitchenMenuController>(true))
            {
                legacy.enabled = false;
                legacy.gameObject.SetActive(false);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void Set(SerializedObject target, string name, Object value)
        { target.FindProperty(name).objectReferenceValue = value; }
        private static RectTransform Child(string name, Transform parent,
            float x, float y, float width, float height)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }
        private static RectTransform Panel(string name, Transform parent,
            float x, float y, float width, float height, Color color)
        {
            var rect = Child(name, parent, x, y, width, height);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color; image.raycastTarget = false;
            return rect;
        }
        private static TextMeshProUGUI Label(string name, Transform parent, float x, float y,
            float width, float height, int size, string value, TMP_FontAsset font)
        {
            var rect = Child(name, parent, x, y, width, height);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font; label.fontSize = Mathf.RoundToInt(size * 1.3f);
            label.text = value; label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = true;
            label.overflowMode = TextOverflowModes.Truncate;
            label.raycastTarget = false;
            return label;
        }
        private static Button Button(string name, Transform parent, float x, float y,
            float width, float height, string value, TMP_FontAsset font)
        {
            var rect = Child(name, parent, x, y, width, height);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.15f, 0.48f, 0.88f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            Label("Label", rect, 0, 0, width, height, value.Length > 16 ? 21 : 27, value, font);
            return button;
        }
    }
}
