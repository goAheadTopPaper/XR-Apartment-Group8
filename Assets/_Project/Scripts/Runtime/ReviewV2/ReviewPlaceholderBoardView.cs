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
        sealed class Row
        {
            public Button Button;
            public TMP_Text Label;
            public SpatialFeedbackRecord Record;
        }
        readonly List<Row> rows = new List<Row>();
        SpatialFeedbackRecorder Recorder => t3Source as SpatialFeedbackRecorder;
        bool all, initialized;
        string selected;
        void Awake() => Initialize();
        void Initialize()
        {
            if (initialized) return; initialized = true;
            currentScheme.onClick.AddListener(() => { all = false; selected = null; ResetScroll(); Refresh(); });
            allSchemes.onClick.AddListener(() => { all = true; selected = null; ResetScroll(); Refresh(); });
            if (retry != null) retry.onClick.AddListener(() => { if (Recorder != null) { _ = Recorder.ReloadAsync(); Refresh(); } });
        }
        void OnEnable() { all = false; selected = null; Initialize(); ResetScroll(); if (Recorder != null) Recorder.RecordsChanged += Refresh; Refresh(); }
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
            var recorder = Recorder;
            if (recorder == null || host == null || rowTemplate == null) { ShowUnavailable(); return; }
            if (retry != null) retry.gameObject.SetActive(!recorder.IsReady && !recorder.IsLoading);
            if (recorder.IsLoading) { Present(ReviewBoardDisplayState.Loading); return; }
            if (!recorder.IsReady) { Present(ReviewBoardDisplayState.Failed, "Marker read or save failed.
Retry; existing data is retained.", recorder.Error); return; }
            var snapshot = host.Snapshot;
            var records = recorder.RecordsForSession(snapshot.SessionId, all ? null : snapshot.SchemeId)
                .OrderByDescending(r => r.timestampUtcMilliseconds).ToArray();
            State = records.Length == 0 ? ReviewBoardDisplayState.Empty : ReviewBoardDisplayState.Ready;
            listText.gameObject.SetActive(records.Length == 0);
            if (records.Length == 0)
            {
                listText.text = "No Markers Yet";
                detailText.text = "Marker Details

No matching records in this session.";
                selected = null;
            }
            else
            {
                var detail = records.FirstOrDefault(r => r.markerId == selected) ?? records[0];
                bool changedSelection = selected != detail.markerId;
                selected = detail.markerId;
                for (int i = 0; i < records.Length; ++i)
                {
                    var record = records[i];
                    var row = i < rows.Count ? rows[i] : CreateRow();
                    if (row.Record == null || row.Record.markerId != record.markerId)
                    {
                        row.Button.name = "Marker " + record.markerId;
                        row.Label.text = (record.schemeId == SchemeIds.IntegratedKitchen ? "A" : "B") + " · " + ReviewMarkDraftView.TypeName(record.type) + "
" + LocationName(record);
                    }
                    row.Record = record;
                    row.Button.gameObject.SetActive(true);
                }
                ShowDetail(detail, changedSelection);
            }
            HideUnusedRows(records.Length);
            EnableFilters();
        }
        Row CreateRow()
        {
            var button = Instantiate(rowTemplate, listScroll.content);
            var row = new Row { Button = button, Label = button.GetComponentInChildren<TMP_Text>(true) };
            button.onClick.AddListener(() =>
            {
                if (row.Record == null) return;
                selected = row.Record.markerId;
                ShowDetail(row.Record, true);
            });
            rows.Add(row);
            return row;
        }
        void HideUnusedRows(int count)
        {
            for (int i = count; i < rows.Count; ++i)
            {
                rows[i].Record = null;
                rows[i].Button.gameObject.SetActive(false);
            }
        }
        void ResetScroll()
        {
            if (listScroll != null)
            {
                listScroll.StopMovement();
                listScroll.verticalNormalizedPosition = 1;
            }
            if (detailScroll != null)
            {
                detailScroll.StopMovement();
                detailScroll.verticalNormalizedPosition = 1;
            }
        }
        void EnableFilters() { currentScheme.interactable = allSchemes.interactable = true; }
        static string LocationName(SpatialFeedbackRecord r) => !string.IsNullOrEmpty(r.objectId) ? r.objectId : !string.IsNullOrEmpty(r.anchorId) ? r.anchorId : "World Position";
        void ShowDetail(SpatialFeedbackRecord r, bool resetScroll)
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
            HideUnusedRows(0); ResetScroll();
        }
    }
}
