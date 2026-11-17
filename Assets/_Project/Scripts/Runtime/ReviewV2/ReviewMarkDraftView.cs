using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using XRApartment.Data.ReviewV2;

namespace XRApartment.Runtime.ReviewV2
{
    // Temporary form state only. This view does not create or persist Marker records.
    public sealed class ReviewMarkDraftView : MonoBehaviour
    {
        [Tooltip("Optional T3 component. Mapping is completed after the actual delivery is available.")]
        public MonoBehaviour t3Source;
        public TMP_InputField note;
        public TMP_Text availability, discardMessage;
        public Button capture, submit, cancel, discard, keepEditing;
        public CanvasGroup editor;
        public GameObject discardPrompt;
        public event Action Closed;
        string session, visit, scheme, model;
        bool initialized;
        public bool IsOpen => gameObject.activeSelf;
        public bool HasDraft => !string.IsNullOrWhiteSpace(note.text);
        public bool IsConfirmingDiscard => discardPrompt.activeSelf;

        void Awake() => Initialize();
        void Initialize()
        {
            if (initialized) return;
            initialized = true;
            cancel.onClick.AddListener(() => RequestClose());
            discard.onClick.AddListener(Close);
            keepEditing.onClick.AddListener(() => ConfirmDiscard(false));
        }
        public void ConfigurePresentation()
        {
            var root = (RectTransform)transform;
            root.anchoredPosition = new Vector2(0, -156); root.sizeDelta = new Vector2(884, 480);
            ((RectTransform)editor.transform).sizeDelta = root.sizeDelta;
            ((RectTransform)discardPrompt.transform).sizeDelta = root.sizeDelta;
            availability.rectTransform.sizeDelta = new Vector2(884, 92);
            var heading = (RectTransform)editor.transform.Find("Note heading");
            heading.anchoredPosition = new Vector2(0, -100); heading.sizeDelta = new Vector2(884, 48);
            var input = (RectTransform)note.transform;
            input.anchoredPosition = new Vector2(0, -156); input.sizeDelta = new Vector2(884, 230);
            note.targetGraphic.color = new Color(.1f, .19f, .26f, 1);
            note.textViewport.sizeDelta = new Vector2(852, 206);
            note.textComponent.rectTransform.sizeDelta = note.textViewport.sizeDelta;
            ((RectTransform)note.placeholder.transform).sizeDelta = note.textViewport.sizeDelta;
            foreach (var button in new[] { capture, submit, cancel })
            {
                var rect = (RectTransform)button.transform;
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -406);
            }
            note.textComponent.fontSize = 30;
            note.textComponent.alignment = TextAlignmentOptions.TopLeft;
            note.textComponent.overflowMode = TextOverflowModes.Overflow;
            note.textComponent.richText = false;
            note.richText = false;
            note.lineType = TMP_InputField.LineType.MultiLineNewline;
            note.characterLimit = 2000;
            note.caretWidth = 2;
            note.customCaretColor = true; note.caretColor = Color.white;
            availability.alignment = TextAlignmentOptions.TopLeft;
            availability.fontSize = 28;
            availability.color = new Color(1, .81f, .44f);
            discardMessage.fontSize = 32;
            capture.interactable = submit.interactable = false;
        }
        public void Open(SessionSnapshot context)
        {
            Initialize();
            session = context.SessionId; visit = context.SchemeVisitId;
            scheme = context.SchemeId; model = context.ModelVersion;
            note.SetTextWithoutNotify("");
            ConfirmDiscard(false);
            availability.text = t3Source == null ? "标记服务未接入 · 位置捕捉和提交暂不可用\n界面草稿，未提交、未保存" :
                "T3 组件已配置，待接口适配 · 提交暂不可用\n界面草稿，未提交、未保存";
            capture.interactable = submit.interactable = false;
            gameObject.SetActive(true);
        }
        public bool Matches(SessionSnapshot context) => session == context.SessionId && visit == context.SchemeVisitId &&
            scheme == context.SchemeId && model == context.ModelVersion;
        public bool RequestClose()
        {
            if (HasDraft) { ConfirmDiscard(true); return false; }
            Close(); return true;
        }
        void ConfirmDiscard(bool show)
        {
            discardPrompt.SetActive(show);
            editor.interactable = editor.blocksRaycasts = !show;
            if (show) note.DeactivateInputField();
        }
        void Close()
        {
            Invalidate(); Closed?.Invoke();
        }
        public void Invalidate()
        {
            note.DeactivateInputField(); note.SetTextWithoutNotify("");
            ConfirmDiscard(false); gameObject.SetActive(false);
            session = visit = scheme = model = null;
        }
    }
}
