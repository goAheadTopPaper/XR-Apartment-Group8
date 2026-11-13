using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.UI;
using XRApartment.Runtime;
using XRApartment.Runtime.Core;
using XRApartment.Runtime.UI;
using XRApartment.Runtime.ReviewV2;
using Object = UnityEngine.Object;

namespace XRApartment.EditorTools
{
    public static class ReviewV2Authoring
    {
        public const string ScenePath = "Assets/_Project/Scenes/SCN_Kitchen_ReviewV2.unity";
        const string Source = "Assets/_Project/Scenes/SCN_Kitchen_Cooking.unity";
        const string PanelPath = "Assets/_Project/Prefabs/UI/PREF_UI_ReviewV2Integration.prefab";
        const string FontPath = "Assets/_Project/Art/UI/FNT_ReviewV2Integration.asset";
        static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
        [MenuItem("XR Apartment/Review V2/Create M0 Scene")]
        public static void Create() => CreateInternal(false);
        // Explicit migration entry; preserves the V2 scene GUID and existing object IDs.
        public static void RebaseOnCooking() => CreateInternal(true);
        static void CreateInternal(bool rebase)
        {
            if (Application.isPlaying || (!rebase && File.Exists(ScenePath))) throw new InvalidOperationException("Create requires Edit mode and no existing V2 scene; use the explicit migration entry to rebase.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var previous = new Dictionary<string, string>(StringComparer.Ordinal);
            const string catalog = "Docs/ReviewV2_M0_Identities.tsv";
            if (rebase && File.Exists(ScenePath))
            {
                var oldScene = EditorSceneManager.OpenScene(ScenePath);
                foreach (var grab in All<XRGrabInteractable>(oldScene))
                    if (grab.TryGetComponent<ReviewSceneIdentity>(out var identity))
                        previous.Add(SerializedKey(grab), identity.Id);
            }
            var scene = EditorSceneManager.OpenScene(Source);
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Cannot save isolated Cooking copy");
            foreach (var view in All<LocomotionSettingsView>(scene)) view.gameObject.SetActive(false);
            foreach (var menu in All<KitchenLayoutMenu>(scene)) menu.gameObject.SetActive(false);
            foreach (var lab in All<KitchenLayoutLab>(scene)) { lab.enabled = false; PrefabUtility.RecordPrefabInstancePropertyModifications(lab); }
            var station = All<KitchenCookingStation>(scene).Single();
            var recipe = All<KitchenRecipeController>(scene).Single();
            var recipeData = new SerializedObject(recipe);
            var recipeButton = recipeData.FindProperty("recipeButton").objectReferenceValue as Component;
            if (recipeButton != null) recipeButton.gameObject.SetActive(false);
            recipeData.FindProperty("recipeButton").objectReferenceValue = null;
            recipeData.ApplyModifiedPropertiesWithoutUndo();
            var stationData = new SerializedObject(station);
            var reset = stationData.FindProperty("resetButton").objectReferenceValue as XRSimpleInteractable;
            stationData.FindProperty("resetButton").objectReferenceValue = null;
            stationData.ApplyModifiedPropertiesWithoutUndo();
            var host = new GameObject("ReviewV2 Session").AddComponent<ReviewSessionHost>();
            host.station = station; host.recipe = recipe; host.roomReset = All<KitchenRoomReset>(scene).Single();
            host.locomotion = All<KitchenLocomotionSettings>(scene).Single();
            host.gameObject.AddComponent<ReviewWorldInteractionGate>().host = host;
            AddId(station.gameObject, "COOKING_STATION");
            AddId(station.ActiveFridge != null ? station.ActiveFridge.gameObject :
                (new SerializedObject(station).FindProperty("fridge").objectReferenceValue as Component).gameObject, "FRIDGE");
            int index = previous.Values.Where(id => id.StartsWith("OBJECT_", StringComparison.Ordinal))
                .Select(id => int.Parse(id.Substring(7))).DefaultIfEmpty(0).Max();
            foreach (var grab in All<XRGrabInteractable>(scene).OrderBy(g => Hierarchy(g.transform), StringComparer.Ordinal))
            {
                AddId(grab.gameObject, previous.TryGetValue(SerializedKey(grab), out var stableId) ? stableId : "OBJECT_" + (++index).ToString("D3"));
                grab.gameObject.AddComponent<CookingGrabBridge>().host = host;
            }
            host.identities = All<ReviewSceneIdentity>(scene);
            BuildPanel();
            var ui = ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath), scene)).GetComponent<ReviewIntegrationPanel>();
            ui.host = host; ui.resetButton = reset;
            ui.GetComponentInChildren<Canvas>().worldCamera = All<XROrigin>(scene).Single().Camera;
            PrefabUtility.RecordPrefabInstancePropertyModifications(ui);
            PrefabUtility.RecordPrefabInstancePropertyModifications(ui.GetComponentInChildren<Canvas>());
            Validate(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("Logs/ReviewV2");
            File.WriteAllLines("Logs/ReviewV2/identity-catalog.tsv", host.identities.Select(i => i.Id + "\t" + Hierarchy(i.transform)));
            File.WriteAllLines(catalog, host.identities.Select(i => i.Id + "\t" + Hierarchy(i.transform)));
            Debug.Log("REVIEW_V2_IDENTITIES retained=" + host.identities.Count(i => previous.Values.Contains(i.Id)) + " total=" + host.identities.Length);
            Debug.Log("REVIEW_V2_CREATED: isolated scene; original build settings unchanged");
        }
        static void AddId(GameObject go, string id)
        { var identity = go.GetComponent<ReviewSceneIdentity>() ?? go.AddComponent<ReviewSceneIdentity>(); identity.Configure(id); }
        static string SerializedKey(Object item)
        {
            var id = GlobalObjectId.GetGlobalObjectIdSlow(item);
            return id.targetObjectId + ":" + id.targetPrefabId;
        }
        static string Hierarchy(Transform t) => t.parent == null ? t.name : Hierarchy(t.parent) + "/" + t.name;
        public static void Validate(Scene scene)
        {
            if (All<XROrigin>(scene).Count(x => x.gameObject.activeInHierarchy) != 1 ||
                All<EventSystem>(scene).Count(x => x.isActiveAndEnabled) != 1 ||
                All<XRInteractionManager>(scene).Count(x => x.isActiveAndEnabled) != 1 ||
                All<ReviewSessionHost>(scene).Count(x => x.isActiveAndEnabled) != 1 ||
                All<ReviewIntegrationPanel>(scene).Count(x => x.isActiveAndEnabled) != 1 ||
                All<ReviewWorldInteractionGate>(scene).Count(x => x.isActiveAndEnabled) != 1 ||
                All<JourneyRuntimeHost>(scene).Length != 0 ||
                All<KitchenActivityGuide>(scene).Any(x => x.gameObject.activeInHierarchy) ||
                All<KitchenPauseMenu>(scene).Any(x => x.gameObject.activeInHierarchy) ||
                All<KitchenFeedbackNotes>(scene).Any(x => x.gameObject.activeInHierarchy) ||
                All<LocomotionSettingsView>(scene).Any(x => x.gameObject.activeInHierarchy))
                throw new InvalidOperationException("Duplicate or legacy control infrastructure");
            foreach (var t in All<Transform>(scene))
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) != 0) throw new InvalidOperationException("Missing script: " + t.name);
            var ids = All<ReviewSceneIdentity>(scene);
            if (ids.Any(i => string.IsNullOrWhiteSpace(i.Id)) || ids.Select(i => i.Id).Distinct().Count() != ids.Length)
                throw new InvalidOperationException("Invalid or duplicate stable identity");
        }
        [MenuItem("XR Apartment/Review V2/Open M0 Scene")]
        public static void Open()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        }
        static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.layer = 5;
            var rect = (RectTransform)go.transform; rect.SetParent(parent, false); rect.sizeDelta = size; rect.anchoredPosition = position; return rect;
        }
        static TMP_Text Label(Transform parent, string text, Vector2 size, Vector2 position, int fontSize)
        {
            var label = Rect("Label", parent, size, position).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            label.text = text; label.fontSize = fontSize; label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false; return label;
        }
        static Button Button(Transform parent, string text, int n)
        {
            var rect = Rect(text, parent, new Vector2(285, 48), new Vector2(n % 2 == 0 ? -150 : 150, 116 - (n / 2) * 57));
            var image = rect.gameObject.AddComponent<Image>(); image.color = new Color(.12f, .25f, .32f);
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            Label(rect, text, new Vector2(275, 44), Vector2.zero, 19); return button;
        }
        static void BuildPanel()
        {
            EnsureFont();
            var root = new GameObject("PREF_UI_ReviewV2Integration");
            try
            {
                var view = root.AddComponent<ReviewIntegrationPanel>();
                var panel = Rect("Panel", root.transform, new Vector2(640, 800), Vector2.zero);
                panel.localScale = Vector3.one * .001f;
                panel.gameObject.AddComponent<Image>().color = new Color(.025f, .045f, .065f, .98f);
                var canvas = panel.gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.sortingOrder = 260;
                panel.gameObject.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 20;
                var raycaster = panel.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
                raycaster.checkFor3DOcclusion = false; raycaster.checkFor2DOcclusion = false;
                view.panel = panel.gameObject;
                view.status = Label(panel, "Review V2", new Vector2(600, 230), new Vector2(0, 265), 19);
                view.setup = Button(panel, "Confirm setup", 0); view.onboarding = Button(panel, "Start experience", 1);
                view.review = Button(panel, "Review feedback", 2); view.back = Button(panel, "Continue exploring", 3);
                view.finish = Button(panel, "Finish (not saved)", 4); view.exit = Button(panel, "Safe exit", 5);
                view.pause = Button(panel, "Pause", 6); view.resume = Button(panel, "Resume", 7);
                view.teleport = Button(panel, "Teleport", 8); view.smooth = Button(panel, "Continuous movement", 9);
                view.recenter = Button(panel, "Recenter", 10); view.reset = Button(panel, "Reset world...", 11);
                view.confirmReset = Button(panel, "Confirm reset", 12); view.cancelReset = Button(panel, "Cancel reset", 13);
                view.standing = Button(panel, "Standing view", 14); view.seated = Button(panel, "Seated view", 15);
                view.close = Button(panel, "Close menu / F8", 16);
                PrefabUtility.SaveAsPrefabAsset(root, PanelPath);
            }
            finally { Object.DestroyImmediate(root); }
        }
        static void EnsureFont()
        {
            if (File.Exists(FontPath)) return;
            if (!AssetDatabase.CopyAsset("Assets/_Project/Art/UI/FNT_KitchenChinese.asset", FontPath))
                throw new IOException("Cannot create isolated V2 font");
        }
        public static void IsolatePanelFont()
        {
            EnsureFont();
            var root = PrefabUtility.LoadPrefabContents(PanelPath);
            try
            {
                foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                    text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
                PrefabUtility.SaveAsPrefabAsset(root, PanelPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        public static void BuildValidation()
        {
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { ScenePath },
                locationPathName = "Builds/ReviewV2/ReviewV2.exe", target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development });
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new Exception("V2 build failed: " + report.summary.result);
        }
    }
}
