#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using XRApartment.Data.ReviewV2;
using XRApartment.Runtime.ReviewV2;

namespace XRApartment.Tests
{
    public sealed class ReviewV2InitialSchemeTests
    {
        GameObject session;
        GameObject handObject;
        [UnityTearDown] public IEnumerator Cleanup()
        { if (handObject != null) Object.Destroy(handObject); if (session != null) Object.Destroy(session); yield return null; yield return null; Time.timeScale = 1; }
        ReviewSessionHost CreateSession(bool saveProbe = false)
        {
            session = new GameObject("B-only session fixture"); session.SetActive(false);
            session.AddComponent<XRInteractionManager>();
            var host = session.AddComponent<ReviewSessionHost>();
            if (saveProbe)
            {
                host.sessionSavePort = session.AddComponent<ReviewSchemePendingSaveProbe>();
                session.AddComponent<ReviewWorldInteractionGate>().host = host;
            }
            var port = session.AddComponent<ReviewSchemePort>(); port.host = host;
            port.layouts = new[] { SchemeIds.IntegratedKitchen, SchemeIds.ExternalRefrigerator }
                .Select(id => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/ReviewV2/Schemes/PREF_" + id + ".prefab").GetComponent<ReviewSchemeWorld>()).ToArray();
            port.initialSchemeId = SchemeIds.ExternalRefrigerator;
            session.SetActive(true); return host;
        }
        [UnityTest] public IEnumerator ConfiguredBStartsWithoutRecordingAnAVisitAndAllowsZeroGoalReview()
        {
            var host = CreateSession(); yield return null;
            Assert.AreEqual(SchemeIds.ExternalRefrigerator, host.Snapshot.SchemeId);
            Assert.AreEqual("external-fridge-m2-v8", host.Snapshot.ModelVersion);
            CollectionAssert.AreEqual(new[] { SchemeIds.ExternalRefrigerator }, host.Snapshot.SchemeHistory);
            Assert.AreEqual(host.station.ActiveFridge.transform, host.Resolve("FRIDGE"));
            Assert.IsTrue(host.Execute(CommandType.CompleteSetup).Accepted);
            Assert.IsTrue(host.Execute(CommandType.CompleteOnboarding).Accepted);
            Assert.IsTrue(host.Snapshot.Goals.All(g => g.State == GoalState.NotStarted));
            Assert.IsTrue(host.Execute(CommandType.Review).Accepted);
            var visit = host.Snapshot.SchemeVisitId;
            Assert.IsTrue(host.Execute(CommandType.Continue).Accepted);
            Assert.AreEqual(visit, host.Snapshot.SchemeVisitId);
        }
        [UnityTest] public IEnumerator PendingAndFailedPreSwitchSaveKeepTheExistingHandSelection()
        {
            var host = CreateSession(true);
            yield return null;
            host.Execute(CommandType.CompleteSetup); host.Execute(CommandType.CompleteOnboarding);
            var save = session.GetComponent<ReviewSchemePendingSaveProbe>();
            var egg = host.groceries.groceries[1];
            handObject = new GameObject("pending-save hand driver"); var hand = handObject.AddComponent<DrivenGrabInteractor>();
            hand.interactionManager = session.GetComponent<XRInteractionManager>(); egg.interactionManager = hand.interactionManager;
            hand.transform.position = egg.transform.position; yield return null;
            hand.interactionManager.SelectEnter((IXRSelectInteractor)hand, egg); yield return null;
            Assert.IsTrue(egg.isSelected); var visit = host.Snapshot.SchemeVisitId;
            host.Execute(CommandType.ChangeScheme, SchemeIds.IntegratedKitchen);
            yield return null; yield return null;
            Assert.IsTrue(host.Snapshot.IsBusy); Assert.IsTrue(egg.isSelected);
            save.Complete(save.Id, false, "synthetic disk failure");
            Assert.AreEqual(visit, host.Snapshot.SchemeVisitId); Assert.IsTrue(egg.isSelected);
            Assert.AreEqual(SaveStatus.Failed, host.Snapshot.SaveStatus);
        }
    }
    // Deliberately no disk IO: only exercises the pending/failure boundary in T1.
    public sealed class ReviewSchemePendingSaveProbe : MonoBehaviour, ISessionSavePort
    {
        public string Id;
        public System.Action<string, bool, string> Complete;
        public void Save(string id, SessionSnapshot snapshot, System.Action<string, bool, string> completed)
        { Id = id; Complete = completed; }
    }
}
#endif
