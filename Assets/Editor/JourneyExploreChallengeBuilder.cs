using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.XR.Interaction.Toolkit.UI;
using Unity.XR.CoreUtils;
using XRApartment.Runtime.Core;
using XRApartment.Runtime.Debugging;
using XRApartment.Runtime.UI;

namespace XRApartment.EditorTools
{
    /// <summary>Authors the phase-three visual prefab and connects it to the existing journey view.</summary>
    public static class JourneyExploreChallengeBuilder
    {
        private const string PrefabPath = "Assets/_Project/Prefabs/UI/PREF_UI_ExploreChallenge.prefab";
        private const string ScenePath = "Assets/_Project/Scenes/SCN_Kitchen_Review.unity";

        [MenuItem("XR Apartment/UI/Upgrade session controls")]
        public static void UpgradeSessionControls()
        {
            var prefab = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var root = (RectTransform)prefab.transform;
                var font = TMP_Settings.defaultFontAsset;
                var intro = root.Find("ExplorePanel");
                if (intro.Find("Pause") == null)
                    Button("Pause", intro, -390, 223, 150, 54, "Pause", font);
                var toolbar = root.Find("ExploreToolbar");
                if (toolbar.Find("Pause") == null)
                    Button("Pause", toolbar, -440, 65, 100, 58, "Pause", font);
                var challenge = root.Find("ChallengePanel");
                if (challenge.Find("Pause") == null)
                    Button("Pause", challenge, -160, 320, 130, 48, "Pause", font);
                if (challenge.Find("Compact") == null)
                    Button("Compact", challenge, 0, 320, 130, 48, "Compact", font);
                challenge.Find("Pause").GetComponent<RectTransform>().anchoredPosition = new Vector2(-160, 320);
                challenge.Find("Compact").GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 320);
                challenge.Find("SafeExitChallenge").GetComponent<RectTransform>().anchoredPosition =
                    new Vector2(160, 320);
                var reset = challenge.Find("ResetTask") as RectTransform;
                reset.anchoredPosition = new Vector2(-105, -285);
                reset.sizeDelta = new Vector2(215, 58);
                if (challenge.Find("OpenSkip") == null)
                    Button("OpenSkip", challenge, 115, -285, 190, 58, "Skip task", font);
                challenge.Find("OpenSkip").GetComponent<RectTransform>().anchoredPosition = new Vector2(115, -285);
                var challengeCompact = root.Find("ChallengeCompactPanel") as RectTransform;
                if (challengeCompact == null)
                {
                    challengeCompact = Panel("ChallengeCompactPanel", root, -440, 285, 465, 255,
                        new Color(0.06f, 0.11f, 0.17f, 0.93f));
                    challengeCompact.localScale = Vector3.one * 0.86f;
                    Label("Title", challengeCompact, 0, 78, 420, 58, 27,
                        "Challenge  |  Step 1/8", font);
                    Label("Instruction", challengeCompact, 0, 5, 420, 100, 23,
                        "Follow the target guide.", font);
                    Button("Expand", challengeCompact, 0, -91, 190, 48, "Expand", font);
                    challengeCompact.gameObject.SetActive(false);
                }
                challengeCompact.anchoredPosition = new Vector2(-345, 240);
                var pending = root.Find("PendingPanel");
                if (pending.Find("Pause") == null)
                    Button("Pause", pending, -105, -120, 180, 55, "Pause", font);
                pending.Find("Pause").GetComponent<RectTransform>().anchoredPosition =
                    new Vector2(-105, -120);
                var pendingExit = pending.Find("SafeExitPending") as RectTransform;
                pendingExit.anchoredPosition = new Vector2(105, -120);
                pendingExit.sizeDelta = new Vector2(180, 55);

                var paused = root.Find("PausedPanel") as RectTransform;
                if (paused == null)
                {
                    paused = Panel("PausedPanel", root, 0, 0, 650, 330,
                        new Color(0.06f, 0.11f, 0.17f, 0.96f));
                    Label("Status", paused, 0, 75, 590, 100, 30,
                        "Session paused. Resume to continue.", font);
                    Button("Resume", paused, -145, -90, 200, 62, "Resume", font);
                    Button("SafeExit", paused, 145, -90, 200, 62, "Safe exit", font);
                    paused.gameObject.SetActive(false);
                }
                var finished = root.Find("FinishedPanel") as RectTransform;
                if (finished == null)
                {
                    finished = Panel("FinishedPanel", root, 0, 0, 650, 300,
                        new Color(0.06f, 0.11f, 0.17f, 0.96f));
                    Label("Status", finished, 0, 60, 590, 100, 30,
                        "Session finished. Start a new session.", font);
                    Button("RestartSession", finished, 0, -75, 300, 68, "New session", font);
                    finished.gameObject.SetActive(false);
                }
                var notice = root.Find("NoticePanel") as RectTransform;
                if (notice != null) Object.DestroyImmediate(notice.gameObject);
                if (root.Find("RetryFailed") == null)
                    Button("RetryFailed", root, 350, 255, 180, 52, "Retry", font);
                root.Find("RetryFailed").gameObject.SetActive(false);
                var skip = root.Find("SkipPanel") as RectTransform;
                if (skip == null)
                {
                    skip = Panel("SkipPanel", root, 0, 0, 720, 450,
                        new Color(0.06f, 0.11f, 0.17f, 0.98f));
                    Label("Title", skip, 0, 172, 660, 55, 30,
                        "Skip this Challenge", font);
                    Label("Instruction", skip, 0, 112, 660, 55, 23,
                        "Choose a reason or enter one before skipping.", font);
                    Button("Discomfort", skip, -170, 48, 290, 55, "Discomfort", font);
                    Button("Blocked", skip, 170, 48, 290, 55, "Interaction blocked", font);
                    InputField("Reason", skip, 0, -28, 620, 65, font);
                    var skipError = Label("Error", skip, 0, -85, 620, 42, 19, "", font);
                    skipError.color = new Color(1f, 0.68f, 0.36f);
                    Button("Cancel", skip, -150, -160, 230, 58, "Cancel", font);
                    Button("Confirm", skip, 150, -160, 230, 58, "Skip Challenge", font);
                    skip.gameObject.SetActive(false);
                }
                var reasonViewport = skip.Find("Reason/Viewport");
                if (reasonViewport.GetComponent<RectMask2D>() == null)
                    reasonViewport.gameObject.AddComponent<RectMask2D>();
                PrefabUtility.SaveAsPrefabAsset(prefab, PrefabPath);
                AssetDatabase.SaveAssets();
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }

            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != ScenePath) return;
            var view = Object.FindObjectOfType<JourneyWorldSpaceView>(true);
            var panel = GameObject.Find("PREF_UI_ExploreChallenge");
            if (view == null || panel == null)
                throw new System.InvalidOperationException("Session control scene wiring is missing.");
            var fields = new SerializedObject(view);
            Assign(fields, "exploreIntroPauseButton", panel.transform.Find("ExplorePanel/Pause").GetComponent<Button>());
            Assign(fields, "explorePauseButton", panel.transform.Find("ExploreToolbar/Pause").GetComponent<Button>());
            Assign(fields, "challengePauseButton", panel.transform.Find("ChallengePanel/Pause").GetComponent<Button>());
            Assign(fields, "challengeCompactPanel", panel.transform.Find("ChallengeCompactPanel").gameObject);
            Assign(fields, "challengeCompactTitle", panel.transform.Find("ChallengeCompactPanel/Title").GetComponent<TMP_Text>());
            Assign(fields, "challengeCompactInstruction", panel.transform.Find("ChallengeCompactPanel/Instruction").GetComponent<TMP_Text>());
            Assign(fields, "challengeCompactButton", panel.transform.Find("ChallengePanel/Compact").GetComponent<Button>());
            Assign(fields, "challengeExpandButton", panel.transform.Find("ChallengeCompactPanel/Expand").GetComponent<Button>());
            Assign(fields, "pendingPauseButton", panel.transform.Find("PendingPanel/Pause").GetComponent<Button>());
            Assign(fields, "pausedPanel", panel.transform.Find("PausedPanel").gameObject);
            Assign(fields, "pausedStatus", panel.transform.Find("PausedPanel/Status").GetComponent<TMP_Text>());
            Assign(fields, "resumeButton", panel.transform.Find("PausedPanel/Resume").GetComponent<Button>());
            Assign(fields, "pausedExitButton", panel.transform.Find("PausedPanel/SafeExit").GetComponent<Button>());
            Assign(fields, "finishedPanel", panel.transform.Find("FinishedPanel").gameObject);
            Assign(fields, "finishedStatus", panel.transform.Find("FinishedPanel/Status").GetComponent<TMP_Text>());
            Assign(fields, "finishedRestartButton", panel.transform.Find("FinishedPanel/RestartSession").GetComponent<Button>());
            Assign(fields, "retryButton", panel.transform.Find("RetryFailed").GetComponent<Button>());
            Assign(fields, "skipPanel", panel.transform.Find("SkipPanel").gameObject);
            Assign(fields, "skipError", panel.transform.Find("SkipPanel/Error").GetComponent<TMP_Text>());
            Assign(fields, "openSkipButton", panel.transform.Find("ChallengePanel/OpenSkip").GetComponent<Button>());
            Assign(fields, "confirmSkipButton", panel.transform.Find("SkipPanel/Confirm").GetComponent<Button>());
            Assign(fields, "cancelSkipButton", panel.transform.Find("SkipPanel/Cancel").GetComponent<Button>());
            Assign(fields, "skipDiscomfortButton", panel.transform.Find("SkipPanel/Discomfort").GetComponent<Button>());
            Assign(fields, "skipBlockedButton", panel.transform.Find("SkipPanel/Blocked").GetComponent<Button>());
            Assign(fields, "skipReasonField", panel.transform.Find("SkipPanel/Reason").GetComponent<TMP_InputField>());
            fields.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static TMP_InputField InputField(string name, Transform parent, float x, float y,
            float width, float height, TMP_FontAsset font)
        {
            var rect = Panel(name, parent, x, y, width, height,
                new Color(0.18f, 0.23f, 0.31f, 1f));
            var image = rect.GetComponent<Image>();
            image.raycastTarget = true;
            var input = rect.gameObject.AddComponent<TMP_InputField>();
            input.targetGraphic = image;
            var viewport = Child("Viewport", rect, 12, 0, width - 32, height - 12);
            var value = Label("Text", viewport, 0, 0, width - 42, height - 16, 23, "", font);
            value.alignment = TextAlignmentOptions.MidlineLeft;
            var placeholder = Label("Placeholder", viewport, 0, 0, width - 42, height - 16,
                21, "Reason for skipping", font);
            placeholder.alignment = TextAlignmentOptions.MidlineLeft;
            placeholder.color = new Color(0.70f, 0.74f, 0.80f);
            input.textViewport = viewport;
            input.textComponent = value;
            input.placeholder = placeholder;
            input.characterLimit = 120;
            input.lineType = TMP_InputField.LineType.SingleLine;
            return input;
        }

        [MenuItem("XR Apartment/UI/Upgrade Challenge HUD and exit controls")]
        public static void UpgradeChallengeControls()
        {
            var prefab = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var root = (RectTransform)prefab.transform;
                var font = TMP_Settings.defaultFontAsset;
                var challenge = root.Find("ChallengePanel") as RectTransform;
                challenge.anchoredPosition = new Vector2(-345, 45);
                challenge.sizeDelta = new Vector2(465, 700);
                challenge.localScale = Vector3.one * 0.86f;
                var reset = challenge.Find("ResetTask") != null
                    ? challenge.Find("ResetTask").GetComponent<Button>()
                    : Button("ResetTask", challenge, 0, -270, 280, 58, "Reset task", font);
                challenge.Find("Benchmark").GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 245);
                challenge.Find("Step").GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 185);
                challenge.Find("Instruction").GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 75);
                challenge.Find("Items").GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -20);
                challenge.Find("Status").GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -78);
                challenge.Find("Error").GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -150);
                challenge.Find("ConfirmReorganisation").GetComponent<RectTransform>().anchoredPosition = new Vector2(-105, -215);
                challenge.Find("RestoreStep").GetComponent<RectTransform>().anchoredPosition = new Vector2(115, -215);
                challenge.Find("SafeExitChallenge").GetComponent<RectTransform>().anchoredPosition = new Vector2(160, 320);
                reset.GetComponent<RectTransform>().anchoredPosition = new Vector2(-105, -285);
                var exited = root.Find("ExitedPanel") as RectTransform;
                if (exited == null)
                {
                    exited = Panel("ExitedPanel", root, 0, 0, 820, 340,
                        new Color(0.06f, 0.11f, 0.17f, 0.96f));
                    Label("Status", exited, 0, 70, 740, 110, 30,
                        "Session exited. Start a new session to return to Setup.", font);
                    Button("RestartSession", exited, 0, -75, 320, 70, "New session", font);
                    exited.gameObject.SetActive(false);
                }
                PrefabUtility.SaveAsPrefabAsset(prefab, PrefabPath);
                AssetDatabase.SaveAssets();
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }

            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != ScenePath) return;
            var view = Object.FindObjectOfType<JourneyWorldSpaceView>(true);
            var panel = GameObject.Find("PREF_UI_ExploreChallenge");
            if (view == null || panel == null) throw new System.InvalidOperationException("Challenge scene wiring is missing.");
            var serialized = new SerializedObject(view);
            Assign(serialized, "resetTaskButton", panel.transform.Find("ChallengePanel/ResetTask").GetComponent<Button>());
            Assign(serialized, "exitedPanel", panel.transform.Find("ExitedPanel").gameObject);
            Assign(serialized, "exitedStatus", panel.transform.Find("ExitedPanel/Status").GetComponent<TMP_Text>());
            Assign(serialized, "restartSessionButton", panel.transform.Find("ExitedPanel/RestartSession").GetComponent<Button>());
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        [MenuItem("XR Apartment/UI/Upgrade Explore introduction and toolbar")]
        public static void UpgradeExploreFlow()
        {
            var prefab = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var root = (RectTransform)prefab.transform;
                var intro = root.Find("ExplorePanel");
                var font = TMP_Settings.defaultFontAsset;
                intro.Find("Heading").GetComponent<TMP_Text>().text = "Explore  |  Explore the kitchen";
                intro.Find("Description").GetComponent<TMP_Text>().text =
                    "Explore lets you preview B1 and B2 freely. Next, Challenge guides you through eight real kitchen tasks for each design.";
                intro.Find("Status").GetComponent<TMP_Text>().text =
                    "Look around first. Start Explore to open the design choices.";
                foreach (var name in new[] { "PreviewB1", "PreviewB2", "StartChallenge" })
                    intro.Find(name).gameObject.SetActive(false);
                var begin = intro.Find("BeginExplore") != null
                    ? intro.Find("BeginExplore").GetComponent<Button>()
                    : Button("BeginExplore", intro, 0, -214, 320, 70, "Start Explore", font);
                var toolbar = root.Find("ExploreToolbar") as RectTransform;
                if (toolbar == null)
                {
                    toolbar = Panel("ExploreToolbar", root, 0, 268, 990, 145,
                        new Color(0.06f, 0.11f, 0.17f, 0.94f));
                    Label("Design", toolbar, -340, 30, 285, 52, 30, "Explore", font);
                    Label("Status", toolbar, -65, -35, 800, 45, 22, "Choose B1 or B2", font);
                    var error = Label("Error", toolbar, 365, -35, 230, 44, 19, "", font);
                    error.color = new Color(1f, 0.68f, 0.36f);
                    Button("PreviewB1", toolbar, -82, 35, 160, 58, "B1", font);
                    Button("PreviewB2", toolbar, 105, 35, 160, 58, "B2", font);
                    Button("Challenge", toolbar, 330, 35, 200, 58, "Challenge", font);
                    toolbar.gameObject.SetActive(false);
                }
                toolbar.anchoredPosition = new Vector2(0, 190);
                toolbar.sizeDelta = new Vector2(1050, 420);
                var design = toolbar.Find("Design") as RectTransform;
                design.anchoredPosition = new Vector2(-290, 105);
                design.sizeDelta = new Vector2(170, 68);
                design.GetComponent<TMP_Text>().text = "Explore";
                var status = toolbar.Find("Status") as RectTransform;
                status.anchoredPosition = new Vector2(0, -35);
                status.sizeDelta = new Vector2(980, 115);
                var errorRect = toolbar.Find("Error") as RectTransform;
                errorRect.anchoredPosition = new Vector2(0, -140);
                errorRect.sizeDelta = new Vector2(980, 75);
                var b1 = toolbar.Find("PreviewB1") as RectTransform;
                b1.anchoredPosition = new Vector2(-95, 105);
                b1.sizeDelta = new Vector2(135, 58);
                var b2 = toolbar.Find("PreviewB2") as RectTransform;
                b2.anchoredPosition = new Vector2(55, 105);
                b2.sizeDelta = new Vector2(135, 58);
                var challengeButton = toolbar.Find("Challenge") as RectTransform;
                challengeButton.anchoredPosition = new Vector2(250, 105);
                challengeButton.sizeDelta = new Vector2(210, 58);
                if (toolbar.Find("SafeExitToolbar") == null)
                    Button("SafeExitToolbar", toolbar, 455, 105, 100, 58, "Exit", font);
                var exitButton = toolbar.Find("SafeExitToolbar") as RectTransform;
                exitButton.anchoredPosition = new Vector2(455, 105);
                exitButton.sizeDelta = new Vector2(100, 58);
                var pauseButton = toolbar.Find("Pause") as RectTransform;
                if (pauseButton != null)
                {
                    pauseButton.anchoredPosition = new Vector2(-440, 105);
                    pauseButton.sizeDelta = new Vector2(100, 58);
                }
                var pending = root.Find("PendingPanel") as RectTransform;
                pending.sizeDelta = new Vector2(465, 340);
                var pendingMessage = pending.Find("Status") as RectTransform;
                pendingMessage.anchoredPosition = new Vector2(0, 80);
                pendingMessage.sizeDelta = new Vector2(430, 115);
                if (pending.Find("ContinueSkipped") == null)
                    Button("ContinueSkipped", pending, 0, -25, 350, 60, "Continue to Explore", font);
                pending.Find("ContinueSkipped").gameObject.SetActive(false);
                if (pending.Find("SafeExitPending") == null)
                    Button("SafeExitPending", pending, 105, -120, 180, 55, "Safe exit", font);
                PrefabUtility.SaveAsPrefabAsset(prefab, PrefabPath);
                AssetDatabase.SaveAssets();
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }

            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != ScenePath) return;
            var view = Object.FindObjectOfType<JourneyWorldSpaceView>(true);
            var panel = GameObject.Find("PREF_UI_ExploreChallenge");
            if (view == null || panel == null) throw new System.InvalidOperationException("Explore scene wiring is missing.");
            var serialized = new SerializedObject(view);
            Assign(serialized, "exploreToolbar", panel.transform.Find("ExploreToolbar").gameObject);
            Assign(serialized, "exploreToolbarDesign", panel.transform.Find("ExploreToolbar/Design").GetComponent<TMP_Text>());
            Assign(serialized, "exploreToolbarStatus", panel.transform.Find("ExploreToolbar/Status").GetComponent<TMP_Text>());
            Assign(serialized, "exploreToolbarError", panel.transform.Find("ExploreToolbar/Error").GetComponent<TMP_Text>());
            Assign(serialized, "beginExploreButton", panel.transform.Find("ExplorePanel/BeginExplore").GetComponent<Button>());
            Assign(serialized, "toolbarB1Button", panel.transform.Find("ExploreToolbar/PreviewB1").GetComponent<Button>());
            Assign(serialized, "toolbarB2Button", panel.transform.Find("ExploreToolbar/PreviewB2").GetComponent<Button>());
            Assign(serialized, "toolbarChallengeButton", panel.transform.Find("ExploreToolbar/Challenge").GetComponent<Button>());
            Assign(serialized, "toolbarExitButton", panel.transform.Find("ExploreToolbar/SafeExitToolbar").GetComponent<Button>());
            Assign(serialized, "pendingExitButton", panel.transform.Find("PendingPanel/SafeExitPending").GetComponent<Button>());
            Assign(serialized, "continueSkippedButton", panel.transform.Find("PendingPanel/ContinueSkipped").GetComponent<Button>());
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        [MenuItem("XR Apartment/UI/Build and wire Explore Challenge UI")]
        public static void BuildAndWire()
        {
            BuildPrefab();
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                Debug.LogError("Open SCN_Kitchen_Review before wiring Explore and Challenge UI.");
                return;
            }
            var views = Object.FindObjectsOfType<JourneyWorldSpaceView>(true);
            var hosts = Object.FindObjectsOfType<JourneyRuntimeHost>(true);
            var bridges = Object.FindObjectsOfType<TaskEvidenceBridge>(true);
            var origins = Object.FindObjectsOfType<XROrigin>(true);
            if (views.Length != 1 || hosts.Length != 1 || bridges.Length != 1 || origins.Length != 1)
            {
                Debug.LogError("Explore/Challenge wiring needs one journey view, host, evidence bridge and XR Origin.");
                return;
            }

            GameObject panel = GameObject.Find("PREF_UI_ExploreChallenge");
            if (panel == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                panel = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                panel.name = "PREF_UI_ExploreChallenge";
                panel.transform.position = new Vector3(0f, 1.36f, 2.47f);
                panel.transform.rotation = Quaternion.identity;
            }
            var canvas = panel.GetComponent<Canvas>();
            canvas.worldCamera = origins[0].Camera;
            var view = new SerializedObject(views[0]);
            Assign(view, "host", hosts[0]);
            Assign(view, "evidenceBridge", bridges[0]);
            Assign(view, "stageOnePanel", views[0].transform.Find("Panel").gameObject);
            Assign(view, "explorePanel", panel.transform.Find("ExplorePanel").gameObject);
            Assign(view, "challengePanel", panel.transform.Find("ChallengePanel").gameObject);
            Assign(view, "pendingPanel", panel.transform.Find("PendingPanel").gameObject);
            foreach (var name in new[] { "exploreDesign", "exploreDescription", "exploreStatus", "exploreError",
                "challengeBenchmark", "challengeStep", "challengeInstruction", "challengeItems",
                "challengeStatus", "challengeError", "pendingStatus" })
                Assign(view, name, panel.transform.Find(FieldPath(name)).GetComponent<TMP_Text>());
            foreach (var name in new[] { "previewB1Button", "previewB2Button", "startChallengeButton",
                "confirmReorganisationButton", "restoreStepButton",
                "exploreExitButton", "challengeExitButton" })
                Assign(view, name, panel.transform.Find(FieldPath(name)).GetComponent<Button>());
            view.ApplyModifiedPropertiesWithoutUndo();

            var host = new SerializedObject(hosts[0]);
            Assign(host, "viewAdapter", views[0]);
            host.ApplyModifiedPropertiesWithoutUndo();
            foreach (var debug in Object.FindObjectsOfType<JourneyDebugUI>(true)) debug.enabled = false;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            UpgradeExploreFlow();
            UpgradeChallengeControls();
            UpgradeSessionControls();
        }

        private static string FieldPath(string field)
        {
            switch (field)
            {
                case "exploreDesign": return "ExplorePanel/Design";
                case "exploreDescription": return "ExplorePanel/Description";
                case "exploreStatus": return "ExplorePanel/Status";
                case "exploreError": return "ExplorePanel/Error";
                case "previewB1Button": return "ExplorePanel/PreviewB1";
                case "previewB2Button": return "ExplorePanel/PreviewB2";
                case "startChallengeButton": return "ExplorePanel/StartChallenge";
                case "challengeBenchmark": return "ChallengePanel/Benchmark";
                case "challengeStep": return "ChallengePanel/Step";
                case "challengeInstruction": return "ChallengePanel/Instruction";
                case "challengeItems": return "ChallengePanel/Items";
                case "challengeStatus": return "ChallengePanel/Status";
                case "challengeError": return "ChallengePanel/Error";
                case "confirmReorganisationButton": return "ChallengePanel/ConfirmReorganisation";
                case "restoreStepButton": return "ChallengePanel/RestoreStep";
                case "exploreExitButton": return "ExplorePanel/SafeExitExplore";
                case "challengeExitButton": return "ChallengePanel/SafeExitChallenge";
                case "pendingStatus": return "PendingPanel/Status";
                default: throw new System.ArgumentOutOfRangeException(nameof(field), field, null);
            }
        }

        private static void Assign(SerializedObject owner, string field, Object value)
        {
            owner.FindProperty(field).objectReferenceValue = value;
        }

        private static void BuildPrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null) return;
            var root = new GameObject("PREF_UI_ExploreChallenge", typeof(RectTransform),
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(TrackedDeviceGraphicRaycaster));
            try
            {
                var rect = (RectTransform)root.transform;
                rect.sizeDelta = new Vector2(1100, 700);
                rect.localScale = Vector3.one * 0.0011f;
                var canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.sortingOrder = 110;
                root.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 20;
                root.GetComponent<GraphicRaycaster>().ignoreReversedGraphics = false;
                var font = TMP_Settings.defaultFontAsset;
                var explore = Panel("ExplorePanel", rect, 0, 0, 990, 570,
                    new Color(0.06f, 0.11f, 0.17f, 0.94f));
                Label("Heading", explore, 0, 220, 900, 60, 38, "Explore  |  F2 layouts", font);
                Label("Design", explore, 0, 150, 900, 50, 31, "No design loaded", font);
                Label("Description", explore, 0, 52, 870, 130, 29,
                    "Preview B1 or B2. Explore interactions do not count as Challenge evidence.", font);
                Label("Status", explore, 0, -65, 900, 60, 25, "Choose a design to preview.", font);
                var exploreError = Label("Error", explore, 0, -128, 900, 50, 24, "", font);
                exploreError.color = new Color(1f, 0.68f, 0.36f);
                Button("PreviewB1", explore, -285, -214, 260, 70, "Preview B1", font);
                Button("PreviewB2", explore, 0, -214, 260, 70, "Preview B2", font);
                Button("StartChallenge", explore, 285, -214, 260, 70, "Start Challenge", font);
                Button("SafeExitExplore", explore, 390, 223, 170, 54, "Safe exit", font);

                var challenge = Panel("ChallengePanel", rect, -315, 70, 465, 515,
                    new Color(0.06f, 0.11f, 0.17f, 0.91f));
                Label("Benchmark", challenge, 0, 208, 420, 48, 29, "Challenge", font);
                Label("Step", challenge, 0, 148, 420, 52, 30, "STEP 1 / 8", font);
                Label("Instruction", challenge, 0, 53, 415, 126, 27, "Follow the target guide.", font);
                Label("Items", challenge, 0, -37, 415, 42, 26, "", font);
                Label("Status", challenge, 0, -96, 415, 75, 23, "", font);
                var challengeError = Label("Error", challenge, 0, -156, 415, 48, 22, "", font);
                challengeError.color = new Color(1f, 0.68f, 0.36f);
                Button("ConfirmReorganisation", challenge, -105, -216, 215, 58,
                    "Confirm move", font);
                Button("RestoreStep", challenge, 115, -216, 190, 58,
                    "Restore step", font);
                Button("SafeExitChallenge", challenge, 160, 205, 145, 45,
                    "Safe exit", font);

                var pending = Panel("PendingPanel", rect, -315, 70, 465, 190,
                    new Color(0.06f, 0.11f, 0.17f, 0.91f));
                Label("Status", pending, 0, 0, 420, 145, 27,
                    "Task complete. Feedback is pending.", font);
                explore.gameObject.SetActive(false);
                challenge.gameObject.SetActive(false);
                pending.gameObject.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static RectTransform Child(string name, Transform parent, float x, float y,
            float width, float height)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        private static RectTransform Panel(string name, Transform parent, float x, float y,
            float width, float height, Color colour)
        {
            var rect = Child(name, parent, x, y, width, height);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = colour;
            image.raycastTarget = false;
            return rect;
        }

        private static TextMeshProUGUI Label(string name, Transform parent, float x, float y,
            float width, float height, int size, string value, TMP_FontAsset font)
        {
            var rect = Child(name, parent, x, y, width, height);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSize = Mathf.RoundToInt(size * 1.25f);
            label.text = value;
            label.color = Color.white;
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
            image.color = new Color(0.15f, 0.43f, 0.80f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            Label("Label", rect, 0, 0, width - 10, height - 4, value.Length > 16 ? 22 : 28, value, font);
            return button;
        }
    }
}
