#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unity.XR.CoreUtils;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit;
using XRApartment.Data.ReviewV2;
using XRApartment.Runtime.ReviewV2;
namespace XRApartment.Tests
{
    public sealed class ReviewV2FeedbackPlayTests
    {
        Scene scene; ReviewIntegrationPanel panel; SpatialFeedbackRecorder recorder;
        string directory, previous; readonly List<GameObject> hidden=new List<GameObject>();
        [UnitySetUp] public IEnumerator Setup()
        {
            previous=Environment.GetEnvironmentVariable("REVIEW_V2_FEEDBACK_DIRECTORY"); directory=Path.Combine(Path.GetTempPath(),"ReviewV2-play-"+Guid.NewGuid().ToString("N")); Environment.SetEnvironmentVariable("REVIEW_V2_FEEDBACK_DIRECTORY",directory);
            foreach(var root in SceneManager.GetActiveScene().GetRootGameObjects()) if(root.activeSelf && (root.GetComponentInChildren<XROrigin>(true)!=null || root.GetComponentInChildren<EventSystem>(true)!=null || root.GetComponentInChildren<XRInteractionManager>(true)!=null)) { hidden.Add(root);root.SetActive(false); }
            scene=EditorSceneManager.LoadSceneInPlayMode("Assets/_Project/Scenes/SCN_Kitchen_ReviewV2.unity",new LoadSceneParameters(LoadSceneMode.Additive));
            while(!scene.isLoaded) yield return null; yield return null;
            panel=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ReviewIntegrationPanel>(true)).Single();recorder=panel.host.GetComponent<SpatialFeedbackRecorder>();
            panel.setup.onClick.Invoke();panel.onboarding.onClick.Invoke();
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if(scene.IsValid()&&scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            foreach(var root in hidden) if(root!=null)root.SetActive(true);hidden.Clear();Time.timeScale=1;
            Environment.SetEnvironmentVariable("REVIEW_V2_FEEDBACK_DIRECTORY",previous); if(Directory.Exists(directory))Directory.Delete(directory,true);
        }
        void Submit(string note)
        {
            panel.OpenMark();panel.markDraft.concern.onClick.Invoke();panel.markDraft.SetCapturedPoint(panel.host.Resolve("FRIDGE").position,"FRIDGE");panel.markDraft.note.text=note;panel.markDraft.submit.onClick.Invoke();
        }
        void Observe() => typeof(ReviewSessionHost).GetMethod("Observe",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(panel.host,new object[]{InteractionKind.ObjectGrabbed,"FRIDGE"});
        [UnityTest] public IEnumerator MarkBoardSwitchResetAndFinishPersistRealData()
        {
            Submit("A：柜门旁的中文意见");Assert.AreEqual(1,recorder.Records.Count);Assert.AreEqual(FeedbackSaveStatus.Verified,recorder.Records[0].saveStatus);
            Assert.AreEqual(FeedbackType.Concern,recorder.Records[0].type);
            var first=recorder.Records[0];panel.markDraft.cancel.onClick.Invoke();panel.quickReview.onClick.Invoke();
            Assert.AreEqual(ReviewBoardDisplayState.Ready,panel.reviewBoard.State);StringAssert.Contains("柜门旁",panel.reviewBoard.detailText.text);
            panel.back.onClick.Invoke();Assert.AreEqual(first.schemeVisitId,panel.host.Snapshot.SchemeVisitId);
            Assert.IsTrue(panel.host.Execute(CommandType.ChangeScheme,SchemeIds.ExternalRefrigerator).Accepted);yield return null;
            Assert.AreEqual(SchemeIds.ExternalRefrigerator,panel.host.Snapshot.SchemeId);Submit("B feedback");panel.markDraft.cancel.onClick.Invoke();
            Assert.AreEqual(2,recorder.Records.Count);Assert.AreNotEqual(first.schemeVisitId,recorder.Records[1].schemeVisitId);
            panel.quickReview.onClick.Invoke();Assert.AreEqual(1,panel.reviewBoard.listScroll.content.GetComponentsInChildren<UnityEngine.UI.Button>().Length);
            panel.reviewBoard.allSchemes.onClick.Invoke();Assert.AreEqual(2,panel.reviewBoard.listScroll.content.GetComponentsInChildren<UnityEngine.UI.Button>().Count(b=>b.gameObject.activeSelf));
            panel.back.onClick.Invoke();panel.host.Execute(CommandType.ChangeScheme,SchemeIds.IntegratedKitchen);yield return null;
            Assert.AreEqual(2,recorder.Records.Count);Assert.AreEqual(0,panel.host.GetComponent<ReviewFeedbackMarkers>().transform.Cast<Transform>().Count(t=>t.name.StartsWith("Feedback ")&&t.gameObject.activeSelf));
            panel.host.Execute(CommandType.ResetWorld);for(int i=0;i<180&&panel.host.Snapshot.IsBusy;i++)yield return null;
            Assert.AreEqual(2,recorder.Records.Count);panel.quickReview.onClick.Invoke();panel.finish.onClick.Invoke();
            Assert.AreEqual(SessionPhase.Finished,panel.host.Snapshot.Phase);Assert.AreEqual(SaveStatus.Verified,panel.host.Snapshot.SaveStatus);
            Assert.AreEqual(2,new LocalFeedbackStore().Load().records.Count);Assert.IsTrue(File.Exists(ReviewFeedbackSavePort.CheckpointPath(panel.host.Snapshot.SessionId)));
        }
        [UnityTest] public IEnumerator FailedMarkerRetriesSameIdAndFailedCheckpointBlocksSwitchAndFinish()
        {
            Directory.CreateDirectory(directory);string feedback=new LocalFeedbackStore().FilePath;
            using(var locked=new FileStream(feedback+".tmp",FileMode.Create,FileAccess.ReadWrite,FileShare.None)) { Submit("failed draft");Assert.AreEqual(FeedbackSaveStatus.Failed,recorder.Records.Single().saveStatus);Assert.IsTrue(panel.markDraft.submit.interactable); }
            var id=recorder.Records.Single().markerId;panel.markDraft.submit.onClick.Invoke();Assert.AreEqual(id,recorder.Records.Single().markerId);Assert.AreEqual(FeedbackSaveStatus.Verified,recorder.Records.Single().saveStatus);
            panel.markDraft.cancel.onClick.Invoke();var visit=panel.host.Snapshot.SchemeVisitId;
            var checkpoint=ReviewFeedbackSavePort.CheckpointPath(panel.host.Snapshot.SessionId);Directory.CreateDirectory(Path.GetDirectoryName(checkpoint));
            using(var locked=new FileStream(checkpoint+".tmp",FileMode.Create,FileAccess.ReadWrite,FileShare.None))
            {
                panel.host.Execute(CommandType.ChangeScheme,SchemeIds.ExternalRefrigerator);Assert.AreEqual(visit,panel.host.Snapshot.SchemeVisitId);Assert.AreEqual(SaveStatus.Failed,panel.host.Snapshot.SaveStatus);
                panel.quickReview.onClick.Invoke();panel.finish.onClick.Invoke();Assert.AreEqual(SessionPhase.Review,panel.host.Snapshot.Phase);Assert.AreEqual(SaveStatus.Failed,panel.host.Snapshot.SaveStatus);
            }
            panel.finish.onClick.Invoke();Assert.AreEqual(SessionPhase.Finished,panel.host.Snapshot.Phase);Assert.AreEqual(SaveStatus.Verified,panel.host.Snapshot.SaveStatus);yield return null;
        }
        [UnityTest] public IEnumerator ContextWindowsCloseAtPauseResetAndSchemeAndFillAllWaitingMarkers()
        {
            Observe();var a=recorder.AddMarker(FeedbackType.Positive,Vector3.zero);var b=recorder.AddMarker(FeedbackType.Discuss,Vector3.one);
            Assert.IsNotNull(a.beforeEventId);Observe();Assert.AreEqual(recorder.Records[0].afterEventId,recorder.Records[1].afterEventId);Assert.IsNotNull(recorder.Records[0].afterEventId);
            var pending=recorder.AddMarker(FeedbackType.Concern,Vector3.zero);panel.host.Execute(CommandType.Pause);panel.host.Execute(CommandType.Resume);Observe();Assert.IsTrue(string.IsNullOrEmpty(recorder.Records.Single(r=>r.markerId==pending.markerId).afterEventId));
            panel.host.Execute(CommandType.ChangeScheme,SchemeIds.ExternalRefrigerator);var next=recorder.AddMarker(FeedbackType.Positive,Vector3.zero);Assert.IsTrue(string.IsNullOrEmpty(next.beforeEventId));Observe();Assert.IsTrue(string.IsNullOrEmpty(recorder.Records.Single(r=>r.markerId==pending.markerId).afterEventId));yield return null;
        }
        [UnityTest] public IEnumerator CaptureCancellationRestoresCanvasAndContextChangeDropsDraft()
        {
            panel.OpenMark();panel.markDraft.capture.onClick.Invoke();var capture=panel.host.GetComponent<ReviewFeedbackCapture>();Assert.IsTrue(capture.IsCapturing,"Right ray must be resolved");Assert.IsFalse(panel.panel.GetComponent<Canvas>().enabled);
            panel.SetOpen(false);Assert.IsFalse(capture.IsCapturing);Assert.IsTrue(panel.panel.GetComponent<Canvas>().enabled);Assert.IsTrue(panel.markDraft.IsOpen);
            panel.host.Execute(CommandType.ChangeScheme,SchemeIds.ExternalRefrigerator);Assert.IsFalse(panel.markDraft.IsOpen);Assert.AreEqual(0,recorder.Records.Count);yield return null;
        }
        [UnityTest] public IEnumerator FeedbackUiFitsBothAspectRatiosAndWorldMarkersFollowTheirObject()
        {
            Submit("可读性与空间标记检查 / Layout verification");
            var camera=panel.host.locomotion.Origin.Camera;
            Directory.CreateDirectory("Logs/ReviewV2/T1T3");
            foreach(var size in new[]{new Vector2Int(1280,720),new Vector2Int(960,720)})
            {
                var target=new RenderTexture(size.x,size.y,24);target.Create();camera.targetTexture=target;camera.aspect=(float)size.x/size.y;
                Assert.IsTrue(panel.markDraft.IsOpen);yield return null;yield return null;Canvas.ForceUpdateCanvases();
                foreach(var text in panel.markDraft.GetComponentsInChildren<TMPro.TMP_Text>()) Assert.IsFalse(text.isTextOverflowing,text.name+": "+text.text);
                SaveImage(target,"mark-"+size.x);
                camera.targetTexture=null;target.Release();UnityEngine.Object.Destroy(target);
            }
            panel.markDraft.cancel.onClick.Invoke();yield return null;
            var marker=panel.host.transform.Cast<Transform>().Single(t=>t.name.StartsWith("Feedback "));
            Assert.IsEmpty(marker.GetComponentsInChildren<Collider>());
            var fridge=panel.host.Resolve("FRIDGE");var oldPoint=marker.position;fridge.position+=Vector3.right*.1f;yield return null;
            Assert.That(Vector3.Distance(oldPoint+Vector3.right*.1f,marker.position),Is.LessThan(.01f));fridge.position-=Vector3.right*.1f;
            panel.quickReview.onClick.Invoke();
            foreach(var size in new[]{new Vector2Int(1280,720),new Vector2Int(960,720)})
            {
                var target=new RenderTexture(size.x,size.y,24);target.Create();camera.targetTexture=target;camera.aspect=(float)size.x/size.y;
                panel.SetOpen(true);yield return null;yield return null;Canvas.ForceUpdateCanvases();
                Assert.IsTrue(panel.reviewBoard.isActiveAndEnabled);
                foreach(var text in panel.reviewBoard.GetComponentsInChildren<TMPro.TMP_Text>()) Assert.IsFalse(text.isTextOverflowing,text.name+": "+text.text);
                foreach(var button in panel.panel.GetComponentsInChildren<UnityEngine.UI.Button>())
                {
                    var corners=new Vector3[4];((RectTransform)button.transform).GetWorldCorners(corners);
                    foreach(var corner in corners) {var v=camera.WorldToViewportPoint(corner);Assert.That(v.x,Is.InRange(0f,1f),button.name);Assert.That(v.y,Is.InRange(0f,1f),button.name);}
                }
                SaveImage(target,"review-"+size.x);camera.targetTexture=null;target.Release();UnityEngine.Object.Destroy(target);
            }
        }
        static void SaveImage(RenderTexture target,string name)
        {
            var old=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();File.WriteAllBytes("Logs/ReviewV2/T1T3/"+name+".png",image.EncodeToPNG());RenderTexture.active=old;UnityEngine.Object.Destroy(image);
        }
        [UnityTest] public IEnumerator ReenteringPlayLoadsHistoryWithoutMixingSessionsAndZeroMarkerFinish()
        {
            Submit("history");panel.markDraft.cancel.onClick.Invoke();string oldSession=panel.host.Snapshot.SessionId;
            yield return SceneManager.UnloadSceneAsync(scene);
            scene=EditorSceneManager.LoadSceneInPlayMode("Assets/_Project/Scenes/SCN_Kitchen_ReviewV2.unity",new LoadSceneParameters(LoadSceneMode.Additive));yield return null;yield return null;
            panel=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ReviewIntegrationPanel>(true)).Single();recorder=panel.host.GetComponent<SpatialFeedbackRecorder>();
            Assert.AreEqual(1,recorder.Records.Count);Assert.AreNotEqual(oldSession,panel.host.Snapshot.SessionId);
            panel.setup.onClick.Invoke();panel.onboarding.onClick.Invoke();panel.quickReview.onClick.Invoke();Assert.AreEqual(ReviewBoardDisplayState.Empty,panel.reviewBoard.State);
            panel.finish.onClick.Invoke();Assert.AreEqual(SaveStatus.Verified,panel.host.Snapshot.SaveStatus);
        }
    }
}
#endif
