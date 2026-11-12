using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using XRApartment.Runtime;
using XRApartment.Runtime.UI;
using Object = UnityEngine.Object;

namespace XRApartment.EditorTools
{
    public static partial class KitchenCookingAuthoring
    {
        const string ActivitiesPrefab = KitchenAuthoring.Prefabs + "UI/PREF_UI_KitchenActivities.prefab";
        const string FeedbackNotePrefab = Prefabs + "PREF_Cooking_FeedbackNote.prefab";

        [MenuItem("XR Apartment/Cooking/Apply Kitchen Guide and Pause Menu")]
        public static void ApplyActivitiesMenu()
        {
            KitchenAuthoring.RequireEditMode();
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) ApplyActivitiesBatch();
        }

        public static void ApplyActivitiesBatch()
        {
            KitchenAuthoring.RequireEditMode();
            var scene = EditorSceneManager.OpenScene(ScenePath);
            ConfigureKitchenActivities(scene);
            KitchenAuthoring.SaveScene(scene);
            Debug.Log("KITCHEN_ACTIVITIES_READY " + scene.path);
        }

        static void ConfigureKitchenActivities(Scene scene)
        {
            BuildFeedbackNote();
            BuildKitchenActivities();
            var roots = scene.GetRootGameObjects();
            var movement = roots.Select(root => root.GetComponent<KitchenLocomotionSettings>()).Single(value => value != null);
            var menu = movement.GetComponentInChildren<LocomotionSettingsView>(true);
            var recipe = roots.Select(root => root.GetComponent<KitchenRecipeController>()).Single(value => value != null);
            var lab = roots.Select(root => root.GetComponent<KitchenLayoutLab>()).Single(value => value != null);
            var activities = menu.GetComponentInChildren<KitchenActivityGuide>(true)?.gameObject;
            if (activities == null)
                activities = (GameObject)PrefabUtility.InstantiatePrefab(
                    KitchenAuthoring.LoadRequired<GameObject>(ActivitiesPrefab), menu.transform);

            var panel = (RectTransform)menu.transform.Find("Panel");
            panel.sizeDelta = new Vector2(640, 548);
            PlaceMenuItem("Title", 0, 223);
            PlaceMenuItem("Current", 0, 171);
            PlaceMenuItem("Teleport", -148, 107);
            PlaceMenuItem("Smooth", 148, 107);
            PlaceMenuItem("Instructions", 0, 13);
            PlaceMenuItem("EnterReview", -93, -92);
            PlaceMenuItem("Close", 218, -92);
            PlaceMenuItem("Language", 0, -165);
            PlaceMenuItem("Shortcut", 0, -231);
            SetMenuLabel("Close", "Back");
            SetMenuLabel("Shortcut", "Hold left Menu / F8 to pause.");
            PrefabUtility.RecordPrefabInstancePropertyModifications(panel);
            foreach (var oldAction in new[] { "KitchenGuide", "FeedbackNote" })
            {
                var old = panel.Find(oldAction);
                if (old != null) Object.DestroyImmediate(old.gameObject);
            }

            var pause = activities.GetComponent<KitchenPauseMenu>();
            var menuFields = new SerializedObject(menu);
            menuFields.FindProperty("headDistance").floatValue = .95f;
            menuFields.FindProperty("pauseMenu").objectReferenceValue = pause;
            menuFields.ApplyModifiedPropertiesWithoutUndo();
            Wire(pause, ("menu", menu), ("movement", movement));
            var pauseFields = new SerializedObject(pause);
            var recipeCards = recipe.GetComponentsInChildren<Canvas>(true);
            var cardsField = pauseFields.FindProperty("recipeCards");
            cardsField.arraySize = recipeCards.Length;
            for (var i = 0; i < recipeCards.Length; i++)
                cardsField.GetArrayElementAtIndex(i).objectReferenceValue = recipeCards[i];
            pauseFields.ApplyModifiedPropertiesWithoutUndo();
            Wire(activities.GetComponent<KitchenActivityGuide>(), ("menu", menu), ("movement", movement),
                ("recipe", recipe), ("layout", lab));
            Wire(activities.GetComponent<KitchenFeedbackNotes>(), ("menu", menu), ("movement", movement),
                ("layout", lab));
            ConfigureFeedbackTargets(roots, recipe.transform, lab.transform);

            void PlaceMenuItem(string name, float x, float y)
            {
                var rect = (RectTransform)panel.Find(name);
                rect.anchoredPosition = new Vector2(x, y);
                PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
            }

            void SetMenuLabel(string name, string value)
            {
                var label = panel.Find(name).GetComponentInChildren<TMP_Text>(true);
                label.text = value;
                PrefabUtility.RecordPrefabInstancePropertyModifications(label);
            }
        }

        static void BuildKitchenActivities()
        {
            var root = KitchenLocomotionAuthoring.Rect("PREF_UI_KitchenActivities", null, 0, 0, 0, 0);
            try
            {
                var guide = root.gameObject.AddComponent<KitchenActivityGuide>();
                var notes = root.gameObject.AddComponent<KitchenFeedbackNotes>();
                var pause = root.gameObject.AddComponent<KitchenPauseMenu>();
                BuildPauseMenu(root, pause, guide, notes);
                var panel = ActivityPanel("Guide", root, 710, 480, .0008f);
                var title = KitchenLocomotionAuthoring.Label("Title", panel, 0, 187, 646, 52, 34, "Calibrate your view");
                var instruction = KitchenLocomotionAuthoring.Label("Instruction", panel, 0, 104, 646, 99, 27, "");
                var detail = KitchenLocomotionAuthoring.Label("Detail", panel, 0, 18, 646, 57, 23, "");
                var calibration = KitchenLocomotionAuthoring.Rect("Posture", panel, 0, -66, 650, 64);
                var standing = KitchenLocomotionAuthoring.Button("Standing", calibration, -221, 0, 172, 53, "Standing");
                var seated = KitchenLocomotionAuthoring.Button("Seated", calibration, -35, 0, 172, 53, "Seated");
                var lower = KitchenLocomotionAuthoring.Button("Lower", calibration, 104, 0, 91, 53, "−");
                var raise = KitchenLocomotionAuthoring.Button("Raise", calibration, 209, 0, 91, 53, "+");
                var status = KitchenLocomotionAuthoring.Label("Status", panel, 0, -130, 646, 46, 21, "");
                var advance = KitchenLocomotionAuthoring.Button("Continue", panel, -93, -190, 417, 56, "Calibrate");
                advance.GetComponent<Image>().color = new Color(.14f, .42f, .37f);
                var close = KitchenLocomotionAuthoring.Button("CloseGuide", panel, 229, -190, 176, 56, "CLOSE");
                var hintCard = ActivityPanel("GuideHint", root, 800, 98, .00072f);
                hintCard.GetComponent<Image>().raycastTarget = false;
                var hint = KitchenLocomotionAuthoring.Label("Hint", hintCard, 0, 0, 755, 84, 26, "");
                ApplyActivityMaterials(panel);
                ApplyActivityMaterials(hintCard);
                panel.gameObject.SetActive(false);
                hintCard.gameObject.SetActive(false);
                Wire(guide, ("notes", notes), ("pauseMenu", pause), ("panel", panel.gameObject), ("calibration", calibration.gameObject),
                    ("close", close), ("advance", advance), ("standing", standing), ("seated", seated),
                    ("lower", lower), ("raise", raise), ("title", title), ("instruction", instruction),
                    ("detail", detail), ("status", status), ("actionLabel", advance.GetComponentInChildren<TMP_Text>()),
                    ("hint", hint));

                var editor = ActivityPanel("FeedbackEditor", root, 880, 590, .00073f);
                var form = KitchenLocomotionAuthoring.Rect("Form", editor, 0, 0, 880, 590);
                var group = form.gameObject.AddComponent<CanvasGroup>();
                KitchenLocomotionAuthoring.Label("Title", form, 0, 241, 810, 52, 34, "Feedback note");
                var target = KitchenLocomotionAuthoring.Label("Target", form, 0, 174, 810, 50, 32, "");
                var context = KitchenLocomotionAuthoring.Label("Context", form, 0, 122, 810, 38, 23, "");
                var fieldRect = KitchenLocomotionAuthoring.Rect("FeedbackText", form, 0, 14, 810, 146);
                var fieldImage = fieldRect.gameObject.AddComponent<Image>();
                fieldImage.color = new Color(.14f, .22f, .26f);
                var field = fieldRect.gameObject.AddComponent<TMP_InputField>();
                var viewport = KitchenLocomotionAuthoring.Rect("Viewport", fieldRect, 0, 0, 788, 130);
                viewport.gameObject.AddComponent<RectMask2D>();
                var value = KitchenLocomotionAuthoring.Label("Text", viewport, 0, 0, 780, 126, 25, "");
                value.alignment = TextAlignmentOptions.TopLeft;
                value.richText = false;
                var placeholder = KitchenLocomotionAuthoring.Label("Placeholder", viewport, 0, 0, 780, 126, 25,
                    "What would you change about this object?");
                placeholder.alignment = TextAlignmentOptions.TopLeft;
                placeholder.color = new Color(.67f, .75f, .8f);
                field.targetGraphic = fieldImage;
                field.textViewport = viewport;
                field.textComponent = (TextMeshProUGUI)value;
                field.placeholder = placeholder;
                field.characterLimit = 240;
                field.lineType = TMP_InputField.LineType.MultiLineNewline;
                field.richText = false;
                var write = KitchenLocomotionAuthoring.Button("WriteInVR", form, -222, -100, 346, 55, "Write in VR");
                var save = KitchenLocomotionAuthoring.Button("SaveFeedback", form, 222, -100, 346, 55, "Save feedback");
                save.GetComponent<Image>().color = new Color(.14f, .42f, .37f);
                var saveStatus = KitchenLocomotionAuthoring.Label("SaveStatus", form, 0, -169, 810, 52, 24, "");
                var discard = KitchenLocomotionAuthoring.Button("DiscardDraft", form, -222, -237, 346, 54, "Discard draft");
                var done = KitchenLocomotionAuthoring.Button("CloseFeedback", form, 222, -237, 346, 54, "CLOSE");
                ApplyActivityMaterials(editor);
                editor.gameObject.SetActive(false);
                Wire(notes, ("notePrefab", KitchenAuthoring.LoadRequired<GameObject>(FeedbackNotePrefab)
                        .GetComponent<KitchenFeedbackNote>()), ("editor", editor), ("form", group), ("close", done),
                    ("discard", discard), ("save", save), ("writeInVr", write), ("input", field), ("target", target),
                    ("context", context), ("status", saveStatus), ("keyboardSurfaceShader", JourneyPhaseFiveBuilder.ReviewPanelShader),
                    ("keyboardTextShader", JourneyPhaseFiveBuilder.ReviewFontShader));
                PrefabUtility.SaveAsPrefabAsset(root.gameObject, ActivitiesPrefab);
            }
            finally { Object.DestroyImmediate(root.gameObject); }
        }

        static void BuildPauseMenu(RectTransform root, KitchenPauseMenu pause, KitchenActivityGuide guide, KitchenFeedbackNotes notes)
        {
            var panel = KitchenLocomotionAuthoring.Rect("PauseMenu", root, 0, 0, 920, 520);
            panel.localScale = Vector3.one * .00083f;
            var navigation = Surface("Navigation", -276, 348);
            var context = Surface("Context", 184, 536);
            var accent = new Color(1f, .78f, .37f);
            var title = KitchenLocomotionAuthoring.Label("Paused", navigation, 0, 206, 280, 45, 35, "Paused");
            title.color = accent;
            title.alignment = TextAlignmentOptions.MidlineLeft;
            var resume = Row("Resume", 134, "Resume");
            resume.GetComponent<Image>().color = new Color(.43f, .32f, .15f);
            var guideButton = Row("KitchenGuide", 68, "Kitchen Guide");
            var layout = Row("LayoutLab", 2, "Layout Lab");
            var feedback = Row("FeedbackNote", -64, "Feedback note");
            var options = Row("Options", -130, "Options");
            var stage = KitchenLocomotionAuthoring.Label("Stage", context, 0, 123, 442, 98, 43, "Calibrate your view");
            stage.alignment = TextAlignmentOptions.MidlineLeft;
            var instructions = KitchenLocomotionAuthoring.Label("Instruction", context, 0, -16, 442, 155, 27, "");
            instructions.alignment = TextAlignmentOptions.TopLeft;
            var shortcut = KitchenLocomotionAuthoring.Label("Shortcut", context, 0, -190, 442, 52, 21,
                "Hold left Menu / F8 to pause.");
            shortcut.alignment = TextAlignmentOptions.MidlineLeft;
            shortcut.color = new Color(.75f, .76f, .68f);
            ApplyActivityMaterials(panel);
            panel.gameObject.SetActive(false);
            Wire(pause, ("guide", guide), ("notes", notes), ("panel", panel.gameObject),
                ("resume", resume), ("kitchenGuide", guideButton), ("layout", layout), ("feedback", feedback),
                ("options", options), ("resumeLabel", resume.GetComponentInChildren<TMP_Text>()),
                ("stageTitle", stage), ("stageInstruction", instructions));

            RectTransform Surface(string name, float x, float width)
            {
                var surface = KitchenLocomotionAuthoring.Rect(name, panel, x, 0, width, 494);
                var background = surface.gameObject.AddComponent<Image>();
                background.color = new Color(.16f, .18f, .17f, .96f);
                background.raycastTarget = false;
                var top = KitchenLocomotionAuthoring.Rect("Top edge", surface, 0, 244, width, 4);
                var line = top.gameObject.AddComponent<Image>();
                line.color = new Color(.65f, .54f, .34f);
                line.raycastTarget = false;
                return surface;
            }

            Button Row(string name, float y, string label)
            {
                var button = KitchenLocomotionAuthoring.Button(name, navigation, 0, y, 282, 56, label);
                button.GetComponent<Image>().color = new Color(.28f, .29f, .25f);
                var colours = button.colors;
                colours.highlightedColor = new Color(1f, .87f, .61f);
                colours.pressedColor = new Color(.90f, .70f, .40f);
                colours.disabledColor = new Color(.40f, .42f, .40f);
                button.colors = colours;
                var text = button.GetComponentInChildren<TMP_Text>();
                text.alignment = TextAlignmentOptions.MidlineLeft;
                text.margin = new Vector4(15, 0, 8, 0);
                return button;
            }
        }

        static RectTransform ActivityPanel(string name, Transform parent, float width, float height, float scale)
        {
            var panel = KitchenLocomotionAuthoring.Rect(name, parent, 0, 0, width, height);
            panel.localScale = Vector3.one * scale;
            panel.gameObject.AddComponent<Image>().color = new Color(.06f, .12f, .14f);
            return panel;
        }

        static void ApplyActivityMaterials(RectTransform panel)
        {
            const string art = KitchenAuthoring.Root + "Art/UI/";
            var surface = KitchenAuthoring.LoadRequired<Material>(art + "MAT_UI_RecoverySurface.mat");
            var text = KitchenAuthoring.LoadRequired<Material>(art + "MAT_UI_RecoveryText.mat");
            foreach (var image in panel.GetComponentsInChildren<Image>(true)) image.material = surface;
            foreach (var label in panel.GetComponentsInChildren<TMP_Text>(true)) label.fontSharedMaterial = text;
        }

        static void BuildFeedbackNote()
        {
            var root = new GameObject("PREF_Cooking_FeedbackNote", typeof(Rigidbody), typeof(BoxCollider),
                typeof(XRGrabInteractable), typeof(KitchenFeedbackNote));
            try
            {
                var body = root.GetComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;
                root.GetComponent<BoxCollider>().size = new Vector3(.122f, .122f, .006f);
                root.GetComponent<XRGrabInteractable>().throwOnDetach = false;
                KitchenGrabAuthoring.ConfigureProp(root);
                var face = KitchenLocomotionAuthoring.Rect("Paper", root.transform, 0, 0, 600, 600);
                face.localScale = Vector3.one * .0002f;
                face.gameObject.AddComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                var paper = face.gameObject.AddComponent<Image>();
                paper.color = new Color(1f, .83f, .35f);
                paper.raycastTarget = false;
                var caption = KitchenLocomotionAuthoring.Label("Caption", face, 0, 58, 540, 185, 74, "Feedback note");
                caption.color = new Color(.16f, .10f, .035f);
                KitchenLocomotionAuthoring.Label("Mark", face, 0, -128, 530, 96, 58, "•").color = caption.color;
                Wire(root.GetComponent<KitchenFeedbackNote>(), ("caption", caption));
                PrefabUtility.SaveAsPrefabAsset(root, FeedbackNotePrefab);
            }
            finally { Object.DestroyImmediate(root); }
        }

        static void ConfigureFeedbackTargets(GameObject[] roots, Transform station, Transform lab)
        {
            var environment = roots.Single(root => root.name == "PREF_Kitchen_Environment").transform;
            Bind(environment.Find("SM_KitchenTable"), "counter", "Kitchen counter");
            Bind(lab.Find("MOD_Pantry"), "pantry", "Pantry cabinet");
            foreach (var fridge in station.GetComponentsInChildren<KitchenFridge>(true))
            {
                var compact = fridge.name == CompactFridgeName;
                var id = compact ? "fridge-compact" : "fridge-full";
                var label = compact ? "Compact fridge" : "Full-size fridge";
                Bind(fridge.transform, id, label);
                Bind(fridge.Door.transform, id + "-door", label + " door");
            }
            Bind(station.GetComponentInChildren<KitchenHeatSource>(true).transform, "stove", "Stove");
            var vessels = new[]
            {
                ("PREF_Cooking_MixingBowl", "mixing-bowl", "Mixing bowl"),
                ("PREF_Cooking_TomatoBowl", "prep-bowl", "Prep bowl"),
                ("PREF_Cooking_FryingPan", "frying-pan", "Frying pan"),
                ("PREF_Cooking_Whisk", "whisk", "Whisk"),
                ("PREF_Cooking_Spatula", "spatula", "Spatula"),
                ("PREF_Cooking_ChoppingBoard", "chopping-board", "Chopping board"),
                ("PREF_Cooking_Ladle", "ladle", "Ladle"),
                ("PREF_Prop_knife", "knife", "Kitchen knife"),
                ("PREF_Prop_plate", "plate", "Plate")
            };
            foreach (var (name, id, label) in vessels) Bind(station.Find(name), id, label);
            Bind(roots.Single(root => root.name == "PREF_Prop_Cup").transform, "cup", "Cup");
            Bind(roots.Single(root => root.name == "PREF_Prop_Pot").transform, "pot", "Pot");

            var sink = roots.Single(root => root.name == "PREF_Water_KitchenStation").transform;
            Bind(sink, "sink", "Sink");
            Bind(sink.GetComponentInChildren<WaterTap>(true).transform, "faucet", "Faucet");
            var dining = roots.Single(root => root.name == "PREF_Furniture_DiningSet").transform;
            Bind(dining.Find("PREF_Furniture_DiningTable"), "dining-table", "Dining table");
            var chairIndex = 0;
            foreach (Transform item in dining)
                if (item.name.StartsWith("PREF_Furniture_DiningChair", StringComparison.Ordinal))
                {
                    chairIndex++;
                    Bind(item, "dining-chair-" + chairIndex, "Dining chair " + chairIndex);
                }

            void Bind(Transform target, string id, string label)
            {
                if (target == null || target.GetComponentInChildren<Collider>(true) == null)
                    throw new InvalidOperationException("Feedback target has no physical surface: " + label);
                var item = GetOrAdd<KitchenFeedbackTarget>(target.gameObject);
                item.Configure(id, label);
                EditorUtility.SetDirty(item);
                PrefabUtility.RecordPrefabInstancePropertyModifications(item);
            }
        }

        static void Wire(Object component, params (string name, Object value)[] values)
        {
            var data = new SerializedObject(component);
            foreach (var (name, value) in values) data.FindProperty(name).objectReferenceValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        }
    }
}
