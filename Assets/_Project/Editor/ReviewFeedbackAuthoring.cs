using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using XRApartment.Runtime.ReviewV2;
namespace XRApartment.EditorTools
{
    public static class ReviewFeedbackAuthoring
    {
        const string UiPath = "Assets/_Project/Prefabs/UI/PREF_UI_ReviewV2Integration.prefab";
        const string MarkerPath = "Assets/_Project/Prefabs/UI/PREF_ReviewV2_FeedbackMarker.prefab";
        static Button Button(Transform parent, string name, string label, Vector2 pos, Vector2 size, Button template)
        {
            var existing = parent.Find(name); if (existing != null) return existing.GetComponent<Button>();
            var b = Object.Instantiate(template, parent); b.name = name; b.onClick.RemoveAllListeners();
            var r = (RectTransform)b.transform; r.anchorMin = r.anchorMax = new Vector2(.5f, 1); r.pivot = new Vector2(.5f, 1);
            r.anchoredPosition = pos; r.sizeDelta = size;
            var t = b.GetComponentInChildren<TMP_Text>(true); t.text = label; t.richText = false; t.fontSize = 28;
            t.rectTransform.anchorMin = Vector2.zero; t.rectTransform.anchorMax = Vector2.one; t.rectTransform.offsetMin = new Vector2(8, 4); t.rectTransform.offsetMax = new Vector2(-8, -4);
            b.gameObject.SetActive(true); return b;
        }
        [MenuItem("XR Apartment/Review V2/Connect T3 Feedback")]
        public static void Configure()
        {
            if (Application.isPlaying) throw new System.InvalidOperationException("Stop Play before authoring");
            var root = PrefabUtility.LoadPrefabContents(UiPath);
            try
            {
                var p = root.GetComponent<ReviewIntegrationPanel>(); var m = p.markDraft; var b = p.reviewBoard;
                m.positive = Button(m.editor.transform, "Positive feedback", "正面 +", new Vector2(-300, -98), new Vector2(280, 48), m.cancel);
                m.concern = Button(m.editor.transform, "Concern feedback", "担忧 !", new Vector2(0, -98), new Vector2(280, 48), m.cancel);
                m.discuss = Button(m.editor.transform, "Discuss feedback", "待讨论 ?", new Vector2(300, -98), new Vector2(280, 48), m.cancel);
                b.retry = Button(b.transform, "Retry feedback", "重试读取/保存", new Vector2(-227, -90), new Vector2(206, 52), b.currentScheme);
                b.rowTemplate = Button(b.listScroll.content, "Marker row template", "标记", Vector2.zero, new Vector2(388, 100), m.cancel);
                var layout = b.rowTemplate.GetComponent<LayoutElement>() ?? b.rowTemplate.gameObject.AddComponent<LayoutElement>(); layout.preferredHeight = 100;
                b.rowTemplate.gameObject.SetActive(false); b.retry.gameObject.SetActive(false);
                p.ConfigurePresentation(); PrefabUtility.SaveAsPrefabAsset(root, UiPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(MarkerPath) == null)
            {
                var marker = new GameObject("Review feedback marker", typeof(RectTransform), typeof(Canvas));
                marker.layer = 2; var rect = (RectTransform)marker.transform; rect.sizeDelta = new Vector2(80, 80); rect.localScale = Vector3.one * .001f;
                marker.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                var bg = marker.AddComponent<UnityEngine.UI.Image>(); bg.color = new Color(.02f, .05f, .08f, .9f); bg.raycastTarget = false;
                var label = new GameObject("Symbol", typeof(RectTransform)).AddComponent<TextMeshProUGUI>(); label.transform.SetParent(marker.transform, false);
                label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/Art/UI/FNT_ReviewV2Integration.asset");
                label.rectTransform.sizeDelta = rect.sizeDelta; label.text = "?"; label.fontSize = 64; label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
                PrefabUtility.SaveAsPrefabAsset(marker, MarkerPath); Object.DestroyImmediate(marker);
            }
            var scene = EditorSceneManager.OpenScene(ReviewV2Authoring.ScenePath);
            var host = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ReviewSessionHost>(true)).Single();
            var panel = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ReviewIntegrationPanel>(true)).Single();
            var recorder = host.GetComponent<SpatialFeedbackRecorder>() ?? host.gameObject.AddComponent<SpatialFeedbackRecorder>(); recorder.host = host;
            var save = host.GetComponent<ReviewFeedbackSavePort>() ?? host.gameObject.AddComponent<ReviewFeedbackSavePort>(); save.recorder = recorder; host.sessionSavePort = save;
            var capture = host.GetComponent<ReviewFeedbackCapture>() ?? host.gameObject.AddComponent<ReviewFeedbackCapture>(); capture.host = host; capture.panel = panel;
            var markers = host.GetComponent<ReviewFeedbackMarkers>() ?? host.gameObject.AddComponent<ReviewFeedbackMarkers>(); markers.host = host; markers.recorder = recorder;
            markers.markerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MarkerPath);
            panel.markDraft.t3Source = recorder; panel.markDraft.positionCapture = capture;
            panel.reviewBoard.t3Source = recorder; panel.reviewBoard.host = host;
            PrefabUtility.RecordPrefabInstancePropertyModifications(panel.markDraft); PrefabUtility.RecordPrefabInstancePropertyModifications(panel.reviewBoard);
            EditorUtility.SetDirty(host); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log("T1T3_CONNECTED " + recorder.name);
        }
    }
}
