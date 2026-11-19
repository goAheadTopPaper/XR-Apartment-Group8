using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using XRApartment.Data.ReviewV2;
using XRApartment.Runtime.ReviewV2;
namespace XRApartment.Tests
{
    public sealed class ReviewV2FeedbackStorageTests
    {
        string folder, previous;
        [SetUp] public void Setup() { previous = Environment.GetEnvironmentVariable("REVIEW_V2_FEEDBACK_DIRECTORY"); folder = Path.Combine(Path.GetTempPath(), "ReviewV2-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder); Environment.SetEnvironmentVariable("REVIEW_V2_FEEDBACK_DIRECTORY", folder); }
        [TearDown] public void Cleanup() { Environment.SetEnvironmentVariable("REVIEW_V2_FEEDBACK_DIRECTORY", previous); if (Directory.Exists(folder)) Directory.Delete(folder, true); }
        [Test] public void RoundTripAllFieldsAndSameCountUpdate()
        {
            var store = new LocalFeedbackStore(); var doc = new SpatialFeedbackDocument();
            doc.records.Add(new SpatialFeedbackRecord { markerId="marker", sessionId="session", schemeId="A", schemeVisitId="visit", modelVersion="v5", objectId="FRIDGE", anchorId="anchor", zoneId="zone", localX=.1f, localY=.2f, localZ=.3f, hasObjectLocalPosition=true, worldX=1, worldY=2, worldZ=3, type=FeedbackType.Concern, explanation="中文\n\"备注\"" + new string('长', 1900), importance="高", timestampUtcMilliseconds=123, beforeEventId="before", afterEventId="after", saveStatus=FeedbackSaveStatus.Verified });
            store.Save(doc); Assert.AreEqual(JsonUtility.ToJson(doc), JsonUtility.ToJson(store.Load()));
            doc.records[0].explanation = "same count, changed content"; store.Save(doc);
            Assert.AreEqual("same count, changed content", store.Load().records[0].explanation); Assert.IsFalse(File.Exists(store.FilePath + ".tmp"));
        }
        [UnityTest] public IEnumerator CorruptLoadIsNotOverwrittenByRecorderOrSavePort()
        {
            var path = new LocalFeedbackStore().FilePath; File.WriteAllText(path, "{broken");
            var go = new GameObject("corrupt storage");
            try
            {
                var recorder = go.AddComponent<SpatialFeedbackRecorder>();
                var load = recorder.ReloadAsync();
                yield return new WaitUntil(() => load.IsCompleted);
                Assert.IsFalse(load.GetAwaiter().GetResult());
                Assert.IsFalse(recorder.RetrySaveAsync().GetAwaiter().GetResult());
                var retry = recorder.ReloadAsync();
                yield return new WaitUntil(() => retry.IsCompleted);
                Assert.IsFalse(retry.GetAwaiter().GetResult());
                Assert.AreEqual("{broken", File.ReadAllText(path));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
        [Test] public void MissingSchemaIsRejected() { var p = new LocalFeedbackStore().FilePath; File.WriteAllText(p, "{}"); Assert.Throws<InvalidDataException>(()=>new LocalFeedbackStore().Load()); }
        [Test] public void UnsupportedSchemaIsRejected() { var p = new LocalFeedbackStore().FilePath; File.WriteAllText(p, "{\"schemaVersion\":\"other\",\"records\":[]}"); Assert.Throws<InvalidDataException>(()=>new LocalFeedbackStore().Load()); }
        [UnityTest] public IEnumerator CheckpointContainsIdentityGoalsAndHistoryAndRejectsWriteFailure()
        {
            var go = new GameObject("save port");
            try
            {
                var recorder=go.AddComponent<SpatialFeedbackRecorder>(); var load = recorder.ReloadAsync(); yield return new WaitUntil(() => load.IsCompleted); Assert.IsTrue(load.GetAwaiter().GetResult()); var port=go.AddComponent<ReviewFeedbackSavePort>(); port.recorder=recorder;
                var s=new SessionSnapshot("session",SessionPhase.Review,"A","visit","model",new[]{new GoalSnapshot("G01",GoalState.InProgress,true,"fact",42)},false,false,false,SaveStatus.Pending,new[]{"A","B","A"});
                string receipt = null, message = null; bool success = false;
                port.Save("request", s, (id, ok, msg) => { receipt = id; success = ok; message = msg; });
                Assert.IsNull(receipt, "Save must return before verified disk work completes");
                yield return new WaitUntil(() => receipt != null);
                Assert.AreEqual("request", receipt); Assert.IsTrue(success, message);
                var check=JsonUtility.FromJson<ReviewFeedbackSavePort.Checkpoint>(File.ReadAllText(ReviewFeedbackSavePort.CheckpointPath("session")));
                Assert.AreEqual("visit",check.schemeVisitId); CollectionAssert.AreEqual(new[]{"A","B","A"},check.schemeHistory); Assert.AreEqual(42,check.goals[0].updatedAtMilliseconds);
                using(var locked=new FileStream(ReviewFeedbackSavePort.CheckpointPath("session")+".tmp",FileMode.Create,FileAccess.ReadWrite,FileShare.None))
                {
                    receipt = null;
                    port.Save("failed", s, (id, ok, _) => { receipt = id; success = ok; });
                    yield return new WaitUntil(() => receipt != null);
                    Assert.AreEqual("failed", receipt); Assert.IsFalse(success);
                }
                Assert.AreEqual("request",JsonUtility.FromJson<ReviewFeedbackSavePort.Checkpoint>(File.ReadAllText(ReviewFeedbackSavePort.CheckpointPath("session"))).requestId);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
    }
}
