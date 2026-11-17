using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using XRApartment.Data.ReviewV2;
namespace XRApartment.Runtime.ReviewV2
{
    public sealed class ReviewMarkDraftView : MonoBehaviour
    {
        public MonoBehaviour t3Source;
        public ReviewFeedbackCapture positionCapture;
        public Button positive, concern, discuss;
        public TMP_InputField note;
        public TMP_Text availability, discardMessage;
        public Button capture, submit, cancel, discard, keepEditing;
        public CanvasGroup editor;
        public GameObject discardPrompt;
        public event Action Closed;
        string session, visit, scheme, model, objectId, markerId;
        bool initialized, hasPosition, saved;
        Vector3 point;
        FeedbackType feedbackType = FeedbackType.Discuss;
        SpatialFeedbackRecorder Recorder => t3Source as SpatialFeedbackRecorder;
        public bool IsOpen => gameObject.activeSelf;
        public bool HasDraft => !saved && (hasPosition || !string.IsNullOrWhiteSpace(note.text));
        public bool IsConfirmingDiscard => discardPrompt.activeSelf;
        void Awake() => Initialize();
        void Initialize()
        {
            if (initialized) return; initialized = true;
            cancel.onClick.AddListener(() => RequestClose()); discard.onClick.AddListener(Close);
            keepEditing.onClick.AddListener(() => ConfirmDiscard(false));
            capture.onClick.AddListener(() => positionCapture?.Begin()); submit.onClick.AddListener(Submit);
            if (positive != null) positive.onClick.AddListener(() => SelectType(FeedbackType.Positive));
            if (concern != null) concern.onClick.AddListener(() => SelectType(FeedbackType.Concern));
            if (discuss != null) discuss.onClick.AddListener(() => SelectType(FeedbackType.Discuss));
        }
        public void ConfigurePresentation()
        {
            var root = (RectTransform)transform;
            root.anchoredPosition = new Vector2(0, -156); root.sizeDelta = new Vector2(884, 480);
            ((RectTransform)editor.transform).sizeDelta = root.sizeDelta; ((RectTransform)discardPrompt.transform).sizeDelta = root.sizeDelta;
            availability.rectTransform.sizeDelta = new Vector2(884, 92);
            bool connected = positive != null;
            var heading = (RectTransform)editor.transform.Find("Note heading");
            heading.anchoredPosition = new Vector2(0, connected ? -164 : -100); heading.sizeDelta = new Vector2(884, 48);
            heading.GetComponent<TMP_Text>().text = connected ? "备注（可选，最多 2000 字）" : "备注（可试填，最多 2000 字）";
            var input = (RectTransform)note.transform;
            input.anchoredPosition = new Vector2(0, connected ? -220 : -156); input.sizeDelta = new Vector2(884, connected ? 166 : 230);
            note.targetGraphic.color = new Color(.1f, .19f, .26f, 1);
            note.textViewport.sizeDelta = input.sizeDelta - new Vector2(32, 24);
            note.textComponent.rectTransform.sizeDelta = note.textViewport.sizeDelta; ((RectTransform)note.placeholder.transform).sizeDelta = note.textViewport.sizeDelta;
            if (connected) ((TMP_Text)note.placeholder).text = "记录厨房体验中的感受、问题或建议…";
            foreach (var button in new[] { capture, submit, cancel }) { var r = (RectTransform)button.transform; r.anchoredPosition = new Vector2(r.anchoredPosition.x, -406); }
            note.textComponent.fontSize = 30; note.textComponent.alignment = TextAlignmentOptions.TopLeft;
            note.textComponent.overflowMode = TextOverflowModes.Overflow; note.textComponent.richText = note.richText = false;
            note.lineType = TMP_InputField.LineType.MultiLineNewline; note.characterLimit = 2000; note.caretWidth = 2;
            note.customCaretColor = true; note.caretColor = Color.white;
            availability.alignment = TextAlignmentOptions.TopLeft; availability.fontSize = 28; availability.color = new Color(1, .81f, .44f);
            discardMessage.fontSize = 32;
            foreach (var b in new[] { positive, concern, discuss }) if (b != null)
            {
                ((RectTransform)b.transform).sizeDelta = new Vector2(280, 60);
                var label = b.GetComponentInChildren<TMP_Text>(true); label.fontSize = 28;
                label.rectTransform.offsetMin = new Vector2(8, 4); label.rectTransform.offsetMax = new Vector2(-8, -4);
            }
            SetLabel(capture, connected ? "捕捉位置" : "位置待接入"); SetLabel(submit, connected ? "提交标记" : "提交待接入");
            capture.interactable = submit.interactable = false;
        }
        public void Open(SessionSnapshot context)
        {
            Initialize(); session = context.SessionId; visit = context.SchemeVisitId; scheme = context.SchemeId; model = context.ModelVersion;
            markerId = objectId = null; hasPosition = saved = false; feedbackType = FeedbackType.Discuss;
            note.SetTextWithoutNotify(""); note.readOnly = false; ConfirmDiscard(false); gameObject.SetActive(true);
            Refresh("先选择类型，再捕捉厨房中的位置。\n尚未提交、未保存。");
        }
        public void SetCapturedPoint(Vector3 worldPoint, string stableObjectId)
        {
            if (positionCapture == null || !Matches(positionCapture.host.Snapshot)) return;
            point = worldPoint; objectId = stableObjectId; hasPosition = true;
            Refresh("位置：" + (objectId ?? "世界坐标") + "\n填写备注后提交，尚未保存。");
        }
        void SelectType(FeedbackType type) { if (markerId == null) { feedbackType = type; Refresh(null); } }
        public static string TypeName(FeedbackType type) => type == FeedbackType.Positive ? "正面 +" : type == FeedbackType.Concern ? "担忧 !" : "待讨论 ?";
        public void ShowMessage(string message) => Refresh(message);
        void Refresh(string message)
        {
            bool ready = Recorder != null && Recorder.IsReady;
            if (Recorder == null) message = t3Source == null ? "标记服务未接入 · 捕捉和提交暂不可用\n界面草稿，未提交、未保存" : "T3 组件已配置，待接口适配 · 提交暂不可用\n界面草稿，未提交、未保存";
            else if (!ready) message = "保存或读取失败，可重试；数据尚未验证。\n" + Short(Recorder.Error);
            if (message != null) availability.text = message;
            capture.interactable = ready && !saved && markerId == null && positionCapture != null;
            submit.interactable = !saved && Recorder != null && ((ready && hasPosition) || markerId != null);
            SetLabel(submit, markerId != null && !saved ? "重试保存" : "提交标记");
            foreach (var button in new[] { positive, concern, discuss })
                if (button != null) { button.interactable = !saved && markerId == null; button.GetComponent<UnityEngine.UI.Image>().color =
                    (button == positive && feedbackType == FeedbackType.Positive) || (button == concern && feedbackType == FeedbackType.Concern) || (button == discuss && feedbackType == FeedbackType.Discuss)
                    ? new Color(.055f, .36f, .46f) : new Color(.10f, .20f, .28f); }
        }
        void Submit()
        {
            if (Recorder == null || saved || positionCapture == null || !Matches(positionCapture.host.Snapshot) || !positionCapture.host.Snapshot.CanReview) return;
            try
            {
                if (markerId == null)
                {
                    if (!hasPosition) return;
                    var r = Recorder.AddMarker(feedbackType, point, note.text, "", objectId, null, null); markerId = r.markerId;
                    saved = r.saveStatus == FeedbackSaveStatus.Verified;
                }
                else saved = Recorder.AddExplanation(markerId, note.text, "");
                note.readOnly = saved;
                Refresh(saved ? TypeName(feedbackType) + " · 已提交并读回验证\n位置：" + (objectId ?? "世界坐标") : "保存失败，保留同一标记；请重试。");
            }
            catch (Exception ex) { Refresh("提交失败：" + Short(ex.Message)); }
        }
        static string Short(string s) => string.IsNullOrEmpty(s) ? "请检查存储路径。" : s.Length > 65 ? s.Substring(0, 65) + "…" : s;
        static void SetLabel(Button button, string text) => button.GetComponentInChildren<TMP_Text>(true).text = text;
        public bool Matches(SessionSnapshot context) => session == context.SessionId && visit == context.SchemeVisitId && scheme == context.SchemeId && model == context.ModelVersion;
        public bool RequestClose()
        {
            if (positionCapture != null && positionCapture.IsCapturing) { positionCapture.Cancel(); return false; }
            if (HasDraft) { discardMessage.text = markerId == null ? "丢弃未提交的草稿？\n\n备注和位置尚未提交，也没有保存。" : "离开未保存的标记？\n\n该标记仍保留在本次内存中，可在回顾页重试保存。"; ConfirmDiscard(true); return false; }
            Close(); return true;
        }
        void ConfirmDiscard(bool show) { discardPrompt.SetActive(show); editor.interactable = editor.blocksRaycasts = !show; if (show) note.DeactivateInputField(); }
        void Close() { Invalidate(); Closed?.Invoke(); }
        public void Invalidate()
        {
            positionCapture?.Cancel(); note.DeactivateInputField(); note.SetTextWithoutNotify(""); ConfirmDiscard(false);
            gameObject.SetActive(false); session = visit = scheme = model = markerId = null; hasPosition = saved = false;
        }
    }
}
