#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using XRApartment.Data.ReviewV2;
using XRApartment.Runtime.ReviewV2;

namespace XRApartment.Tests
{
    public sealed class ReviewV2SchemePlayTests
    {
        Scene scene;
        ReviewSessionHost host;
        ReviewSchemePort port;
        GameObject heldDriver;
        int initialHistoryCount;
        [UnitySetUp] public IEnumerator Load()
        {
            scene = EditorSceneManager.LoadSceneInPlayMode("Assets/_Project/Scenes/SCN_Kitchen_ReviewV2.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            while (!scene.isLoaded) yield return null;
            yield return null;
            host = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ReviewSessionHost>(true)).Single();
            port = host.GetComponent<ReviewSchemePort>();
            host.Execute(CommandType.CompleteSetup); host.Execute(CommandType.CompleteOnboarding);
            // Normalize only this runtime fixture, preserving the user's unsaved Editor start.
            if (host.Snapshot.SchemeId != SchemeIds.IntegratedKitchen)
                Assert.IsTrue(host.Execute(CommandType.ChangeScheme, SchemeIds.IntegratedKitchen).Accepted);
            initialHistoryCount = host.Snapshot.SchemeHistory.Count;
            yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.timeScale = 1;
            if (heldDriver != null) Object.Destroy(heldDriver);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            yield return null;
        }
        [UnityTest] public IEnumerator RealWorldRoundTripRebindsAllReferencesAndStartsFresh()
        {
            Assert.AreEqual(SchemeIds.IntegratedKitchen, host.Snapshot.SchemeId);
            Assert.IsTrue(port.Current.IsConfigured); Assert.IsNotNull(host.station.ActiveFridge.SecondaryDoor);
            var session = host.Snapshot.SessionId; var visit = host.Snapshot.SchemeVisitId;
            var aPosition = host.Resolve("FRIDGE").position;
            var aWorld = port.Current;
            host.station.ActiveFridge.Door.SetOpenImmediate(true);
            Assert.IsFalse(host.station.ActiveFridge.Accessible, "Both half-width doors must be open to expose the full storage zone");
            host.station.ActiveFridge.SecondaryDoor.SetOpenImmediate(true);
            Assert.IsTrue(host.station.ActiveFridge.Accessible);
            var egg = host.groceries.groceries[1]; var eggPosition = egg.transform.position;
            host.Execute(CommandType.Review); host.Execute(CommandType.Continue);
            Assert.AreSame(aWorld, port.Current); Assert.AreEqual(eggPosition, egg.transform.position);
            Assert.AreEqual(visit, host.Snapshot.SchemeVisitId);
            OperationResult result = null; host.OperationCompleted += r => result = r;
            Assert.IsTrue(host.Execute(CommandType.ChangeScheme, SchemeIds.ExternalRefrigerator).Accepted);
            Assert.IsNotNull(result); Assert.IsTrue(result.Succeeded, result.Message);
            yield return null;
            Assert.AreEqual(SchemeIds.ExternalRefrigerator, host.Snapshot.SchemeId);
            Assert.AreEqual("external-fridge-m2-v8", host.Snapshot.ModelVersion);
            Assert.AreEqual(session, host.Snapshot.SessionId); Assert.AreNotEqual(visit, host.Snapshot.SchemeVisitId);
            Assert.IsNull(host.station.ActiveFridge.SecondaryDoor); Assert.IsFalse(host.station.ActiveFridge.Accessible);
            Assert.That(Vector3.Distance(aPosition, host.Resolve("FRIDGE").position), Is.GreaterThan(2));
            Assert.AreEqual(host.station.gameObject.scene, host.roomReset.gameObject.scene);
            Assert.AreEqual(port.Current, host.station.GetComponentInParent<ReviewSchemeWorld>());
            Assert.IsTrue(host.identities.All(i => i.gameObject.scene == port.Current.gameObject.scene));
            Assert.IsFalse(host.identities.GroupBy(i => i.Id).Any(g => g.Count() != 1));
            Assert.IsFalse(host.Snapshot.Goals.Any(g => g.State != GoalState.NotStarted));
            Assert.IsFalse(host.Execute(CommandType.ChangeScheme, SchemeIds.ExternalRefrigerator).Accepted);
            Assert.IsTrue(host.Execute(CommandType.ChangeScheme, SchemeIds.IntegratedKitchen).Accepted);
            Assert.IsTrue(result.Succeeded, result.Message); yield return null;
            Assert.AreEqual("integrated-kitchen-m2-v5", host.Snapshot.ModelVersion);
            Assert.AreEqual(aPosition, host.Resolve("FRIDGE").position);
            Assert.IsFalse(host.station.ActiveFridge.Door.IsOpen); Assert.IsFalse(host.station.ActiveFridge.SecondaryDoor.IsOpen);
            Assert.AreEqual(initialHistoryCount + 2, host.Snapshot.SchemeHistory.Count);
            visit = host.Snapshot.SchemeVisitId;
            host.Execute(CommandType.ResetWorld);
            for (int i = 0; i < 100 && host.Snapshot.IsBusy; i++) yield return null;
            Assert.IsFalse(host.Snapshot.IsBusy); Assert.AreNotEqual(visit, host.Snapshot.SchemeVisitId);
        }
        [UnityTest] public IEnumerator OccupiedDestinationRollsBackWorldAndIdentity()
        {
            var original = port.Current; var snapshot = host.Snapshot;
            var egg = host.groceries.groceries[1];
            var driver = heldDriver = new GameObject("M2 rollback held-object driver");
            var hand = driver.AddComponent<DrivenGrabInteractor>();
            var manager = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<XRInteractionManager>()).Single();
            hand.interactionManager = manager; hand.keepSelectedTargetValid = true; egg.interactionManager = manager;
            hand.transform.position = egg.transform.position;
            yield return null;
            manager.SelectEnter((IXRSelectInteractor)hand, egg); yield return null;
            Assert.IsTrue(egg.isSelected); var position = egg.transform.position;
            host.station.ActiveFridge.Door.SetOpenImmediate(true);
            var target = port.layouts.Single(w => w.schemeId == SchemeIds.ExternalRefrigerator);
            var camera = host.locomotion.Origin.Camera.transform; var before = camera.position;
            camera.position = target.station.ActiveFridge.transform.TransformPoint(new Vector3(.34f, 1.2f, 0));
            var destination = camera.position;
            OperationResult result = null; host.OperationCompleted += r => result = r;
            Assert.IsTrue(host.Execute(CommandType.ChangeScheme, SchemeIds.ExternalRefrigerator).Accepted);
            Assert.IsNotNull(result); Assert.IsFalse(result.Succeeded,
                $"head={camera.position:F3} intended={destination:F3} overlap=" +
                string.Join(",", Physics.OverlapSphere(destination, .18f).Select(c => c.name)));
            Assert.AreSame(original, port.Current); Assert.AreEqual(snapshot.SchemeVisitId, host.Snapshot.SchemeVisitId);
            Assert.AreEqual(snapshot.ModelVersion, host.Snapshot.ModelVersion); Assert.AreEqual(initialHistoryCount, host.Snapshot.SchemeHistory.Count);
            Assert.AreEqual(position, egg.transform.position); Assert.IsTrue(host.station.ActiveFridge.Door.IsOpen);
            Assert.IsTrue(egg.isSelected, "A rejected switch must preserve the original held object");
            camera.position = before;
            Assert.IsTrue(host.Execute(CommandType.ChangeScheme, SchemeIds.ExternalRefrigerator).Accepted);
            Assert.IsTrue(result.Succeeded, result.Message); Object.Destroy(driver); yield return null;
        }
        [UnityTest] public IEnumerator ClosedFridgeEnvelopeHasNoFixedFurnitureOverlap()
        {
            foreach (var scheme in new[] { SchemeIds.IntegratedKitchen, SchemeIds.ExternalRefrigerator })
            {
                if (host.Snapshot.SchemeId != scheme) host.Execute(CommandType.ChangeScheme, scheme);
                Assert.AreEqual(scheme, host.Snapshot.SchemeId);
                Physics.SyncTransforms();
                var fridge = host.station.ActiveFridge.transform;
                var hits = Physics.OverlapBox(fridge.TransformPoint(new Vector3(0,.94f,0)),
                    new Vector3(.35f,.86f,.33f), fridge.rotation, ~0, QueryTriggerInteraction.Ignore)
                    .Where(c => c.gameObject.scene == port.Current.gameObject.scene &&
                        c.attachedRigidbody == null && !c.transform.IsChildOf(fridge)).ToArray();
                Assert.IsEmpty(hits, scheme + " closed refrigerator overlaps " + string.Join(",", hits.Select(c => c.name)));
                if (scheme == SchemeIds.ExternalRefrigerator)
                {
                    var floor = port.Current.transform.Find("B_WindowFloorSupport").GetComponent<Collider>();
                    Assert.IsTrue(floor.enabled && floor.gameObject.activeInHierarchy);
                    Assert.IsTrue(floor.Raycast(new Ray(new Vector3(-4.8f,.2f,-1.4f), Vector3.down), out var support, .4f));
                    Assert.AreEqual("B_WindowFloorSupport", support.collider.name, "The former shelf footprint stays supported below the new cabinet");
                }
                yield return null;
            }
        }
    }
}
#endif
