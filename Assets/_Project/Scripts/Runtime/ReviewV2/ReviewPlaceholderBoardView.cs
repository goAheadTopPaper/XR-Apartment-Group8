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
            if (retry != null) retry.onClick.AddListener(() => { if (Recorder != null) { _ = Recorder.ReloadAsync(); Refresh(); } });
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
            if (retry != null) retry.GetComponentInChildren<TMP_Text>(true).text = "Retry";
            // The English label otherwise wraps beyond its existing 186 x 44 text rect.
            if (currentScheme != null) currentScheme.GetComponentInChildren<TMP_Text>(true).fontSize = 24;
            if (rowTemplate != null) rowTemplate.GetComponentInChildren<TMP_Text>(true).fontSize = 28;
            foreach (var text in new[] { listText, detailText }) { text.fontSize = 30; text.alignment = TextAlignmentOptions.TopLeft; text.overflowMode = TextOverflowModes.Overflow; text.richText = false; }
        }
        public void ShowUnavailable() => Present(ReviewBoardDisplayState.NotConnected, t3Source == null ? "T3 unavailable\nMarkers cannot be loaded." : "T3 configured\nIntegration pending.", "Marker Details\n\nNo feedback data loaded.");
        public void Refresh()
        {
            if (!isActiveAndEnabled) return;
            foreach (var row in rows) { row.gameObject.SetActive(false); Destroy(row.gameObject); } rows.Clear();
            if (Recorder == null || host == null || rowTemplate == null) { ShowUnavailable(); return; }
            currentScheme.interactable = allSchemes.interactable = true;
            if (retry != null) retry.gameObject.SetActive(!Recorder.IsReady && !Recorder.IsLoading);
            if (Recorder.IsLoading) { Present(ReviewBoardDisplayState.Loading); return; }
            if (!Recorder.IsReady) { Present(ReviewBoardDisplayState.Failed, "Marker read or save failed.\nRetry; existing data is retained.", Recorder.Error); EnableFilters(); return; }
            var s = host.Snapshot;
            var records = Recorder.Records.Where(r => r.sessionId == s.SessionId && (all || r.schemeId == s.SchemeId)).OrderByDescending(r => r.timestampUtcMilliseconds).ToArray();
            State = records.Length == 0 ? ReviewBoardDisplayState.Empty : ReviewBoardDisplayState.Ready;
            listText.gameObject.SetActive(records.Length == 0); listText.text = "No Markers Yet";
            if (records.Length == 0) { detailText.text = "Marker Details\n\nNo matching records in this session."; selected = null; }
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
        static string LocationName(SpatialFeedbackRecord r) => !string.IsNullOrEmpty(r.objectId) ? r.objectId : !string.IsNullOrEmpty(r.anchorId) ? r.anchorId : "World Position";
        void ShowDetail(SpatialFeedbackRecord r)
        {
            detailText.text = ReviewMarkDraftView.TypeName(r.type) + " · " + (r.saveStatus == FeedbackSaveStatus.Verified ? "Save Verified" : "Save Not Verified") +
                "\nScheme: " + r.schemeId + "\nVisit：" + r.schemeVisitId.Substring(0, Math.Min(8, r.schemeVisitId.Length)) +
                "\nModel: " + r.modelVersion + "\nObject: " + LocationName(r) +
                "\nLocation: " + new Vector3(r.worldX, r.worldY, r.worldZ).ToString("F2") + "\n\n" + (string.IsNullOrEmpty(r.explanation) ? "(No note)" : r.explanation);
            detailScroll.verticalNormalizedPosition = 1;
        }
        public void Present(ReviewBoardDisplayState state, string list = null, string detail = null)
        {
            State = state; listText.gameObject.SetActive(true);
            listText.text = list ?? (state == ReviewBoardDisplayState.Empty ? "No Markers Yet" : state == ReviewBoardDisplayState.Failed ? "Unable to load markers. Retry." : state == ReviewBoardDisplayState.Ready ? "Markers Loaded" : state == ReviewBoardDisplayState.Loading ? "Loading Markers..." : "T3 unavailable. Markers cannot be loaded.");
            detailText.text = detail ?? "Marker Details"; currentScheme.interactable = allSchemes.interactable = false;
            Canvas.ForceUpdateCanvases(); listScroll.verticalNormalizedPosition = detailScroll.verticalNormalizedPosition = 1;
        }
    }
}
