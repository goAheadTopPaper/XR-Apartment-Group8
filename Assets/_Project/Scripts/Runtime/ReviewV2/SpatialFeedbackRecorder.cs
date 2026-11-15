using System;
using System.Collections.Generic;
using UnityEngine;
using XRApartment.Data.ReviewV2;

namespace XRApartment.Runtime.ReviewV2
{
    public sealed class SpatialFeedbackRecorder : MonoBehaviour
    {
        public ReviewSessionHost host;
        public int contextEventLimit = 12;
        readonly Queue<string> recent = new Queue<string>();
        LocalFeedbackStore store;
        SpatialFeedbackDocument document;
        public IReadOnlyList<SpatialFeedbackRecord> Records => document.records.AsReadOnly();
        public event Action RecordsChanged;
        void Awake() { store = new LocalFeedbackStore(); document = store.Load(); }
        void OnEnable() { if (host != null) host.Observed += OnFact; }
        void OnDisable() { if (host != null) host.Observed -= OnFact; }
        public void Bind(ReviewSessionHost sessionHost) { if(host!=null) host.Observed-=OnFact; host=sessionHost; if(isActiveAndEnabled&&host!=null)host.Observed+=OnFact; }
        void OnFact(InteractionEvent fact)
        {
            for(int i=document.records.Count-1;i>=0;i--) { var r=document.records[i]; if(r.sessionId==fact.SessionId&&r.schemeVisitId==fact.SchemeVisitId&&string.IsNullOrEmpty(r.afterEventId)){r.afterEventId=fact.EventId;Persist(r);break;} }
            recent.Enqueue(fact.EventId); while(recent.Count>Mathf.Max(1,contextEventLimit))recent.Dequeue();
        }
        public SpatialFeedbackRecord AddMarker(FeedbackType type, Vector3 point, string objectId=null, string anchorId=null)
        {
            if(host==null||!host.CanObserve)throw new InvalidOperationException("Feedback is available during Experience or Review.");
            var s=host.Snapshot; var t=!string.IsNullOrEmpty(objectId)?host.Resolve(objectId):null; if(t==null&&!string.IsNullOrEmpty(anchorId))t=host.Resolve(anchorId);
            var p=t!=null?t.InverseTransformPoint(point):point; string before=null; foreach(var id in recent)before=id;
            var r=new SpatialFeedbackRecord{sessionId=s.SessionId,schemeId=s.SchemeId,schemeVisitId=s.SchemeVisitId,modelVersion=s.ModelVersion,markerId=Guid.NewGuid().ToString("N"),timestampUtcMilliseconds=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),objectId=objectId,anchorId=anchorId,hasObjectLocalPosition=t!=null,localX=p.x,localY=p.y,localZ=p.z,worldX=point.x,worldY=point.y,worldZ=point.z,type=type,beforeEventId=before,saveStatus=FeedbackSaveStatus.Pending};
            document.records.Add(r); Persist(r); RecordsChanged?.Invoke(); return r;
        }
        public bool AddExplanation(string id,string explanation,string importance){var r=document.records.Find(x=>x.markerId==id);if(r==null)return false;r.explanation=explanation??"";r.importance=importance??"";Persist(r);RecordsChanged?.Invoke();return r.saveStatus==FeedbackSaveStatus.Verified;}
        void Persist(SpatialFeedbackRecord r){try{r.saveStatus=FeedbackSaveStatus.Verified;store.Save(document);}catch(Exception ex){r.saveStatus=FeedbackSaveStatus.Failed;Debug.LogError(ex,this);}}
        public string ExportJson()=>store.ExportJson();
    }
}
