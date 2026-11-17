using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using XRApartment.Data.ReviewV2;
namespace XRApartment.Runtime.ReviewV2
{
    public enum ReviewBoardDisplayState { NotConnected, Loading, Empty, Ready, Failed }
    public sealed class ReviewPlaceholderBoardView : MonoBehaviour
    {
        public MonoBehaviour t3Source;
        public ReviewSessionHost host;
        public Button currentScheme, allSchemes, retry, rowTemplate;
        public TMP_Text listText, detailText;
        public ScrollRect listScroll, detailScroll;
        public ReviewBoardDisplayState State { get; private set; }
        readonly List<Button> rows = new List<Button>();
        SpatialFeedbackRecorder Recorder => t3Source as SpatialFeedbackRecorder;
        bool all, initialized;
        string selected;
        void Awake() => Initialize();
        void Initialize()
        {
            if (initialized) return; initialized = true;
            currentScheme.onClick.AddListener(() => { all = false; selected = null; Refresh(); });
            allSchemes.onClick.AddListener(() => { all = true; selected = null; Refresh(); });
            if (retry != null) retry.onClick.AddListener(() => { if (Recorder != null) { Recorder.Reload(); Refresh(); } });
        }
        void OnEnable() { all = false; selected = null; Initialize(); if (Recorder != null) Recorder.RecordsChanged += Refresh; Refresh(); }
        void OnDisable() { if (Recorder != null) Recorder.RecordsChanged -= Refresh; }
        public void ConfigurePresentation()
        {
            foreach (var filter in new[] { currentScheme, allSchemes, retry })
            {
                if (filter == null) continue;
                ((RectTransform)filter.transform).sizeDelta = new Vector2(206, 52);
                var label = filter.GetComponentInChildren<TMP_Text>(true); label.fontSize = 28; label.rectTransform.sizeDelta = new Vector2(186, 44);
            }
            if (retry != null) retry.GetComponentInChildren<TMP_Text>(true).text = "重试 / Retry";
            if (rowTemplate != null) rowTemplate.GetComponentInChildren<TMP_Text>(true).fontSize = 28;
            foreach (var text in new[] { listText, detailText }) { text.fontSize = 30; text.alignment = TextAlignmentOptions.TopLeft; text.overflowMode = TextOverflowModes.Overflow; text.richText = false; }
        }
        public void ShowUnavailable() => Present(ReviewBoardDisplayState.NotConnected, t3Source == null ? "T3 未接入\n暂不能读取标记" : "T3 组件已配置\n待完成接口适配", "标记详情\n\n当前没有加载反馈数据。");
        public void Refresh()
        {
            if (!isActiveAndEnabled) return;
            foreach (var row in rows) { row.gameObject.SetActive(false); Destroy(row.gameObject); } rows.Clear();
            if (Recorder == null || host == null || rowTemplate == null) { ShowUnavailable(); return; }
            currentScheme.interactable = allSchemes.interactable = true;
            if (retry != null) retry.gameObject.SetActive(!Recorder.IsReady);
            if (!Recorder.IsReady) { Present(ReviewBoardDisplayState.Failed, "标记读取或保存失败\n点击重试，保留原数据。", Recorder.Error); EnableFilters(); return; }
            var s = host.Snapshot;
            var records = Recorder.Records.Where(r => r.sessionId == s.SessionId && (all || r.schemeId == s.SchemeId)).OrderByDescending(r => r.timestampUtcMilliseconds).ToArray();
            State = records.Length == 0 ? ReviewBoardDisplayState.Empty : ReviewBoardDisplayState.Ready;
            listText.gameObject.SetActive(records.Length == 0); listText.text = "暂无标记";
            if (records.Length == 0) { detailText.text = "标记详情\n\n本次会话尚无符合筛选条件的记录。"; selected = null; }
            else
            {
                if (!records.Any(r => r.markerId == selected)) selected = records[0].markerId;
                foreach (var record in records)
                {
                    var row = Instantiate(rowTemplate, listScroll.content); row.name = "Marker " + record.markerId; row.gameObject.SetActive(true);
                    row.GetComponentInChildren<TMP_Text>(true).text = (record.schemeId == SchemeIds.IntegratedKitchen ? "A" : "B") + " · " + ReviewMarkDraftView.TypeName(record.type) + "\n" + LocationName(record);
                    row.onClick.AddListener(() => { selected = record.markerId; ShowDetail(record); }); rows.Add(row);
                    if (record.markerId == selected) ShowDetail(record);
                }
            }
            EnableFilters(); Canvas.ForceUpdateCanvases(); listScroll.verticalNormalizedPosition = 1;
        }
        void EnableFilters() { currentScheme.interactable = allSchemes.interactable = true; }
        static string LocationName(SpatialFeedbackRecord r) => !string.IsNullOrEmpty(r.objectId) ? r.objectId : !string.IsNullOrEmpty(r.anchorId) ? r.anchorId : "世界坐标";
        void ShowDetail(SpatialFeedbackRecord r)
        {
            detailText.text = ReviewMarkDraftView.TypeName(r.type) + " · " + (r.saveStatus == FeedbackSaveStatus.Verified ? "保存已验证" : "保存未验证") +
                "\n方案：" + r.schemeId + "\nVisit：" + r.schemeVisitId.Substring(0, Math.Min(8, r.schemeVisitId.Length)) +
                "\n模型：" + r.modelVersion + "\n对象：" + LocationName(r) +
                "\n位置：" + new Vector3(r.worldX, r.worldY, r.worldZ).ToString("F2") + "\n\n" + (string.IsNullOrEmpty(r.explanation) ? "（无备注）" : r.explanation);
            detailScroll.verticalNormalizedPosition = 1;
        }
        public void Present(ReviewBoardDisplayState state, string list = null, string detail = null)
        {
            State = state; listText.gameObject.SetActive(true);
            listText.text = list ?? (state == ReviewBoardDisplayState.Empty ? "暂无标记" : state == ReviewBoardDisplayState.Failed ? "标记读取失败，请重试" : state == ReviewBoardDisplayState.Ready ? "标记已加载" : state == ReviewBoardDisplayState.Loading ? "正在读取标记…" : "T3 未接入，暂不能读取标记");
            detailText.text = detail ?? "标记详情"; currentScheme.interactable = allSchemes.interactable = false;
            Canvas.ForceUpdateCanvases(); listScroll.verticalNormalizedPosition = detailScroll.verticalNormalizedPosition = 1;
        }
    }
}
