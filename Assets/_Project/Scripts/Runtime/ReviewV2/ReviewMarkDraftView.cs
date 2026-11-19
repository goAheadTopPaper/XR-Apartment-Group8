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
        bool initialized, hasPosition, saved, submitting;
        int draftVersion;
        Vector3 point;
        FeedbackType feedbackType = FeedbackType.Discuss;
        SpatialFeedbackRecorder Recorder => t3Source as SpatialFeedbackRecorder;
        public bool IsOpen => gameObject.activeSelf;
        public bool HasDraft => !saved && (hasPosition || !string.IsNullOrWhiteSpace(note.text));
        public bool IsConfirmingDiscard => discardPrompt.activeSelf;
        void Awake() => Initialize();
        void OnEnable() { if (Recorder != null) Recorder.RecordsChanged += OnRecordsChanged; }
        void OnDisable() { if (Recorder != null) Recorder.RecordsChanged -= OnRecordsChanged; }
        void OnRecordsChanged()
        {
            if (!hasPosition && !saved) Refresh("Select a type, then capture a location.\nNot submitted or saved.");
        }
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
            heading.GetComponent<TMP_Text>().text = connected ? "Note (optional, up to 2000 characters)" : "Note (preview, up to 2000 characters)";
            var input = (RectTransform)note.transform;
            input.anchoredPosition = new Vector2(0, connected ? -220 : -156); input.sizeDelta = new Vector2(884, connected ? 166 : 230);
            note.targetGraphic.color = new Color(.1f, .19f, .26f, 1);
            note.textViewport.sizeDelta = input.sizeDelta - new Vector2(32, 24);
            note.textComponent.rectTransform.sizeDelta = note.textViewport.sizeDelta; ((RectTransform)note.placeholder.transform).sizeDelta = note.textViewport.sizeDelta;
            if (connected) ((TMP_Text)note.placeholder).text = "Describe your experience, concerns or suggestions...";
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
            SetLabel(capture, connected ? "Capture Position" : "Position Unavailable"); SetLabel(submit, connected ? "Submit Marker" : "Submit Unavailable");
            capture.interactable = submit.interactable = false;
        }
        public void Open(SessionSnapshot context)
        {
            draftVersion++; submitting = false;
            Initialize(); session = context.SessionId; visit = context.SchemeVisitId; scheme = context.SchemeId; model = context.ModelVersion;
            markerId = objectId = null; hasPosition = saved = false; feedbackType = FeedbackType.Discuss;
            note.SetTextWithoutNotify(""); note.readOnly = false; ConfirmDiscard(false); gameObject.SetActive(true);
            Refresh("Select a type, then capture a location.\nNot submitted or saved.");
        }
        public void SetCapturedPoint(Vector3 worldPoint, string stableObjectId)
        {
            if (positionCapture == null || !Matches(positionCapture.host.Snapshot)) return;
            point = worldPoint; objectId = stableObjectId; hasPosition = true;
            Refresh("Location: " + (objectId ?? "World Position") + "\nAdd an optional note, then submit. Not saved yet.");
        }
        void SelectType(FeedbackType type) { if (markerId == null) { feedbackType = type; Refresh(null); } }
        public static string TypeName(FeedbackType type) => type == FeedbackType.Positive ? "Positive +" : type == FeedbackType.Concern ? "Concern !" : "Discuss ?";
        public void ShowMessage(string message) => Refresh(message);
        void Refresh(string message)
        {
            bool ready = Recorder != null && Recorder.IsReady;
            if (Recorder == null) message = t3Source == null ? "Marker service unavailable. Capture and submit disabled.\nDraft only; not submitted or saved." : "T3 configured; integration pending. Submit unavailable.\nDraft only; not submitted or saved.";
            else if (Recorder.IsLoading) message = "Loading feedback…";
            else if (!ready && !submitting) message = "Read or save failed. Retry; data is not verified.\n" + Short(Recorder.Error);
            if (message != null) availability.text = message;
            capture.interactable = ready && !saved && !submitting && markerId == null && positionCapture != null;
            submit.interactable = !saved && !submitting && Recorder != null && ((ready && hasPosition) || markerId != null);
            SetLabel(submit, submitting ? "Saving…" : markerId != null && !saved ? "Retry Save" : "Submit Marker");
            foreach (var button in new[] { positive, concern, discuss })
                if (button != null) { button.interactable = !saved && markerId == null; button.GetComponent<UnityEngine.UI.Image>().color =
                    (button == positive && feedbackType == FeedbackType.Positive) || (button == concern && feedbackType == FeedbackType.Concern) || (button == discuss && feedbackType == FeedbackType.Discuss)
                    ? new Color(.055f, .36f, .46f) : new Color(.10f, .20f, .28f); }
        }
        async void Submit()
        {
            var recorder = Recorder;
            if (recorder == null || saved || submitting || positionCapture == null) return;
            var snapshot = positionCapture.host.Snapshot;
            if (!Matches(snapshot) || !snapshot.CanReview || !hasPosition) return;
            int version = draftVersion;
            submitting = note.readOnly = true;
            Refresh("Saving marker and verifying read-back…");
            string message;
            try
            {
                bool verified;
                if (markerId == null)
                {
                    markerId = recorder.AddMarker(feedbackType, point, note.text, "", objectId, null, null).markerId;
                    verified = await recorder.RetrySaveAsync();
                }
                else verified = await recorder.AddExplanationAsync(markerId, note.text, "");
                if (this == null || version != draftVersion) return;
                saved = verified;
                message = saved ? TypeName(feedbackType) + " · Submitted and verified\nLocation: " + (objectId ?? "World Position") : "Save failed. The same marker is retained; retry.";
            }
            catch (Exception ex) { message = "Submit failed: " + Short(ex.Message); }
            if (this == null || version != draftVersion) return;
            submitting = false;
            note.readOnly = saved;
            Refresh(message);
        }
        static string Short(string s) => string.IsNullOrEmpty(s) ? "Check the storage path." : s.Length > 65 ? s.Substring(0, 65) + "…" : s;
        static void SetLabel(Button button, string text) => button.GetComponentInChildren<TMP_Text>(true).text = text;
        public bool Matches(SessionSnapshot context) => session == context.SessionId && visit == context.SchemeVisitId && scheme == context.SchemeId && model == context.ModelVersion;
        public bool RequestClose()
        {
            if (positionCapture != null && positionCapture.IsCapturing) { positionCapture.Cancel(); return false; }
            if (HasDraft) { discardMessage.text = markerId == null ? "Discard draft?\nNote and location: not submitted or saved." : "Leave unsaved marker?\nKept in memory. Retry saving from Review."; ConfirmDiscard(true); return false; }
            Close(); return true;
        }
        void ConfirmDiscard(bool show) { discardPrompt.SetActive(show); editor.interactable = editor.blocksRaycasts = !show; if (show) note.DeactivateInputField(); }
        void Close() { Invalidate(); Closed?.Invoke(); }
        public void Invalidate()
        {
            draftVersion++; submitting = false;
            positionCapture?.Cancel(); note.DeactivateInputField(); note.SetTextWithoutNotify(""); ConfirmDiscard(false);
            gameObject.SetActive(false); session = visit = scheme = model = markerId = null; hasPosition = saved = false;
        }
    }
}
