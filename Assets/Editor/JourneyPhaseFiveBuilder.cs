using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using UnityEngine.SceneManagement;
using XRApartment.Runtime.Core;
using XRApartment.Runtime.Debugging;
using XRApartment.Runtime.UI;

namespace XRApartment.EditorTools
{
    /// <summary>Author the phase-five shell and its single scene instance. Does not enter Play mode.</summary>
    public static class JourneyPhaseFiveBuilder
    {
        private const string PrefabPath = "Assets/_Project/Prefabs/UI/PREF_UI_JourneyPhaseFive.prefab";
        private const string ScenePath = "Assets/_Project/Scenes/SCN_Kitchen_Review.unity";

        [MenuItem("XR Apartment/UI/Compact phase five save status")]
        public static void CompactSaveStatus()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var save = root.transform.Find("NoticePanel/SaveStatus") as RectTransform;
                var retry = root.transform.Find("NoticePanel/RetrySave") as RectTransform;
                if (save == null || retry == null)
                    throw new System.InvalidOperationException("Phase-five save controls are missing.");
                save.anchoredPosition = new Vector2(-45, 88);
                save.sizeDelta = new Vector2(292, 27);
                var label = save.GetComponent<TextMeshProUGUI>();
                label.fontSize = 15;
                label.alignment = TextAlignmentOptions.Right;
                label.color = new Color(.72f, .79f, .84f);
                retry.anchoredPosition = new Vector2(150, 88);
                retry.sizeDelta = new Vector2(94, 30);
                retry.GetComponentInChildren<TextMeshProUGUI>().fontSize = 15;
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [MenuItem("XR Apartment/UI/Add skipped feedback continuation")]
        public static void AddSkippedFeedbackContinuation()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var panel = root.transform.Find("FeedbackPanel");
                if (panel == null) throw new System.InvalidOperationException("FeedbackPanel is missing.");
                var button = panel.Find("ContinueSkipped");
                if (button == null)
                {
                    var font = TMP_Settings.defaultFontAsset;
                    button = Button("ContinueSkipped", panel, 0, -237, 310, 56,
                        "Save skip and continue", font).transform;
                }
                var view = root.GetComponent<JourneyPhaseFiveView>();
                var data = new SerializedObject(view);
                Ref(data, "skippedContinueButton", button.GetComponent<Button>());
                data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [MenuItem("XR Apartment/UI/Author phase five shell")]
        public static void Author()
        {
            var font = TMP_Settings.defaultFontAsset;
            var root = new GameObject("PREF_UI_JourneyPhaseFive", typeof(RectTransform),
                typeof(Canvas), typeof(CanvasScaler), typeof(TrackedDeviceGraphicRaycaster),
                typeof(JourneyPhaseFiveView));
            var rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(1600, 760);
            rect.localScale = Vector3.one * .0011f;
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 130;

            var feedback = Panel("FeedbackPanel", rect, -260, 0, 820, 580);
            var feedbackTitle = Label("Title", feedback, 0, 247, 760, 54, 34, "Feedback", font);
            var feedbackOutcome = Label("Outcome", feedback, 0, 188, 760, 56, 24, "", font);
            var feedbackStatus = Label("Status", feedback, 0, 130, 750, 65, 22, "", font);
            var feedbackSlot = Child("E1_FeedbackInputSlot", feedback, 0, -37, 750, 260);
            Label("Dependency", feedbackSlot, 0, 0, 710, 200, 23,
                "E1 Feedback form prefab connects here. No substitute validation is supplied by this shell.", font);
            var feedbackOpen = Button("OpenFeedback", feedback, -230, -237, 210, 56, "Open feedback", font);
            var feedbackCancel = Button("CancelFeedback", feedback, 0, -237, 210, 56, "Close draft", font);
            var feedbackSubmit = Button("SubmitFeedback", feedback, 230, -237, 210, 56, "Submit feedback", font);

            var comparison = Panel("ComparisonPanel", rect, -260, 0, 920, 620);
            var comparisonTitle = Label("Title", comparison, 0, 270, 850, 55, 34,
                "Final comparison", font);
            var b1 = Label("B1Summary", comparison, -222, 118, 420, 190, 24, "B1", font);
            var b2 = Label("B2Summary", comparison, 222, 118, 420, 190, 24, "B2", font);
            var comparisonSlot = Child("E1_ComparisonInputSlot", comparison, 0, -88, 830, 175);
            Label("Dependency", comparisonSlot, 0, 0, 800, 140, 23,
                "E1 final preference and trade-off input prefab connects here.", font);
            var comparisonStatus = Label("Status", comparison, 0, -205, 830, 65, 21, "", font);
            var confirm = Button("ConfirmComparison", comparison, 0, -267, 300, 55,
                "Confirm comparison", font);

            var finished = Panel("FinishedPanel", rect, -260, 0, 800, 280);
            var finishedStatus = Label("Status", finished, 0, 0, 735, 220, 25, "Review complete", font);

            var notice = Panel("NoticePanel", rect, 560, 220, 410, 210);
            var save = Label("SaveStatus", notice, -45, 88, 292, 27, 15, "", font);
            save.alignment = TextAlignmentOptions.Right;
            save.color = new Color(.72f, .79f, .84f);
            var conflict = Label("ConflictStatus", notice, 0, 20, 385, 78, 20, "", font);
            var safety = Label("SafetyStatus", notice, 0, -42, 385, 66, 20, "", font);
            safety.color = new Color(1f, .35f, .35f);
            var retry = Button("RetrySave", notice, 150, 88, 94, 30, "Retry", font);
            retry.GetComponentInChildren<TextMeshProUGUI>().fontSize = 15;
            var pause = Button("SafetyPause", notice, 0, -88, 122, 38, "Pause", font);
            var exit = Button("SafetyExit", notice, 123, -88, 122, 38, "Safe exit", font);

            var view = root.GetComponent<JourneyPhaseFiveView>();
            var data = new SerializedObject(view);
            Ref(data, "feedbackPanel", feedback.gameObject);
            Ref(data, "comparisonPanel", comparison.gameObject);
            Ref(data, "finishedPanel", finished.gameObject);
            Ref(data, "noticePanel", notice.gameObject);
            Ref(data, "feedbackInputSlot", feedbackSlot);
            Ref(data, "comparisonInputSlot", comparisonSlot);
            Ref(data, "feedbackTitle", feedbackTitle);
            Ref(data, "feedbackOutcome", feedbackOutcome);
            Ref(data, "feedbackStatus", feedbackStatus);
            Ref(data, "comparisonTitle", comparisonTitle);
            Ref(data, "b1Summary", b1);
            Ref(data, "b2Summary", b2);
            Ref(data, "comparisonStatus", comparisonStatus);
            Ref(data, "finishedStatus", finishedStatus);
            Ref(data, "saveStatus", save);
            Ref(data, "conflictStatus", conflict);
            Ref(data, "safetyStatus", safety);
            Ref(data, "feedbackOpen", feedbackOpen);
            Ref(data, "feedbackCancel", feedbackCancel);
            Ref(data, "feedbackSubmit", feedbackSubmit);
            Ref(data, "comparisonConfirm", confirm);
            Ref(data, "saveRetry", retry);
            Ref(data, "safetyPause", pause);
            Ref(data, "safetyExit", exit);
            data.FindProperty("headDistance").floatValue = 0.8f;
            data.ApplyModifiedPropertiesWithoutUndo();
            feedback.gameObject.SetActive(false);
            comparison.gameObject.SetActive(false);
            finished.gameObject.SetActive(false);
            notice.gameObject.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
                throw new System.InvalidOperationException("Open SCN_Kitchen_Review before authoring scene wiring.");
            var old = Object.FindObjectOfType<JourneyPhaseFiveView>();
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, scene);
            instance.transform.position = new Vector3(0, 1.5f, 2.1f);
            var shell = instance.GetComponent<JourneyPhaseFiveView>();
            var host = Object.FindObjectOfType<JourneyRuntimeHost>();
            var recovery = Object.FindObjectOfType<RecoveryMenuView>();
            var origin = Object.FindObjectOfType<XROrigin>();
            var existingView = Object.FindObjectOfType<JourneyWorldSpaceView>();
            var debugView = Object.FindObjectOfType<JourneyDebugUI>();
            if (host == null || recovery == null || origin == null || existingView == null)
                throw new System.InvalidOperationException("Existing Host, Recovery, XR Origin or view is missing.");
            var shellData = new SerializedObject(shell);
            Ref(shellData, "host", host);
            Ref(shellData, "recoveryMenu", recovery);
            Ref(shellData, "xrOrigin", origin);
            shellData.ApplyModifiedPropertiesWithoutUndo();
            var originalData = new SerializedObject(existingView);
            Ref(originalData, "phaseFiveView", shell);
            originalData.ApplyModifiedPropertiesWithoutUndo();
            var sceneCanvas = instance.GetComponent<Canvas>();
            sceneCanvas.worldCamera = origin.Camera;
            if (debugView != null)
            {
                debugView.enabled = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(debugView);
            }
            PrefabUtility.RecordPrefabInstancePropertyModifications(shell);
            PrefabUtility.RecordPrefabInstancePropertyModifications(existingView);
            PrefabUtility.RecordPrefabInstancePropertyModifications(sceneCanvas);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        private static void Ref(SerializedObject data, string property, Object value)
        { data.FindProperty(property).objectReferenceValue = value; }

        private static RectTransform Child(string name, Transform parent, float x, float y,
            float width, float height)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        private static RectTransform Panel(string name, Transform parent, float x, float y,
            float width, float height)
        {
            var rect = Child(name, parent, x, y, width, height);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(.05f, .1f, .16f, .93f);
            image.raycastTarget = false;
            return rect;
        }

        private static TextMeshProUGUI Label(string name, Transform parent, float x, float y,
            float width, float height, int size, string value, TMP_FontAsset font)
        {
            var rect = Child(name, parent, x, y, width, height);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSize = size;
            label.text = value;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = true;
            label.raycastTarget = false;
            return label;
        }

        private static Button Button(string name, Transform parent, float x, float y,
            float width, float height, string value, TMP_FontAsset font)
        {
            var rect = Child(name, parent, x, y, width, height);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(.14f, .42f, .77f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            Label("Label", rect, 0, 0, width - 8, height - 4, height < 42 ? 17 : 22,
                value, font);
            return button;
        }
    }
}
