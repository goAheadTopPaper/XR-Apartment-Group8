#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using XRApartment.Data.ReviewV2;
using XRApartment.Runtime;
using XRApartment.Runtime.ReviewV2;

namespace XRApartment.Tests
{
    public sealed class ReviewPanStoveSnapPlayTests
    {
        Scene scene;
        ReviewSessionHost host;
        XRInteractionManager manager;
        DrivenGrabInteractor hand;
        GameObject driver, obstacle;
        ReviewPanStoveSnap snap;
        XRGrabInteractable pan;
        Rigidbody body;
        float previousDelta;
        readonly List<GameObject> hidden = new List<GameObject>();
        void BindPan()
        {
            pan = host.station.transform.Find("PREF_Cooking_FryingPan").GetComponent<XRGrabInteractable>();
            snap = pan.GetComponent<ReviewPanStoveSnap>(); body = pan.GetComponent<Rigidbody>();
            pan.interactionManager = manager;
        }
        [UnitySetUp] public IEnumerator Load()
        {
            previousDelta = Time.captureDeltaTime; Time.captureDeltaTime = 1f / 90;
            for (var i = 0; i < SceneManager.sceneCount; i++)
                foreach (var root in SceneManager.GetSceneAt(i).GetRootGameObjects())
                    if (root.activeSelf && (root.GetComponentInChildren<Collider>(true) != null || root.GetComponentInChildren<Camera>(true) != null))
                    { hidden.Add(root); root.SetActive(false); }
            scene = EditorSceneManager.LoadSceneInPlayMode("Assets/_Project/Scenes/SCN_Kitchen_ReviewV2.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            while (!scene.isLoaded) yield return null;
            host = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ReviewSessionHost>(true)).Single();
            host.locomotion.Origin.gameObject.SetActive(false);
            host.locomotion.Origin.Camera.transform.position = new Vector3(-3.15f, 1.65f, 1.12f);
            manager = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<XRInteractionManager>()).Single();
            driver = new GameObject("Stove snap tracked hand"); hand = driver.AddComponent<DrivenGrabInteractor>();
            hand.interactionManager = manager; hand.keepSelectedTargetValid = true;
            host.Execute(CommandType.CompleteSetup); host.Execute(CommandType.CompleteOnboarding);
            // The open Editor scene may have an unsaved B start; this fixture exercises A.
            if (host.Snapshot.SchemeId != SchemeIds.IntegratedKitchen)
            { Assert.IsTrue(host.Execute(CommandType.ChangeScheme, SchemeIds.IntegratedKitchen).Accepted); yield return null; }
            BindPan();
            yield return Frames(10);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.timeScale = 1; Time.captureDeltaTime = previousDelta;
            if (driver != null) Object.Destroy(driver);
            if (obstacle != null) Object.Destroy(obstacle);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            foreach (var root in hidden) if (root != null) root.SetActive(true);
            hidden.Clear(); yield return null;
        }
        static IEnumerator Frames(int n)
        { for (var i = 0; i < n; i++) { yield return null; yield return new WaitForFixedUpdate(); } }
        IEnumerator Pick()
        {
            hand.transform.SetPositionAndRotation(body.worldCenterOfMass, pan.transform.rotation);
            Assert.IsTrue(manager.IsSelectPossible(hand, pan));
            manager.SelectEnter((IXRSelectInteractor)hand, pan); yield return null;
            Assert.IsTrue(pan.isSelected); Assert.IsFalse(snap.IsDocked);
            Assert.AreEqual(RigidbodyConstraints.None, body.constraints, "XRI must receive an unlocked body");
        }
        IEnumerator Move(Vector3 position, Quaternion rotation, int frames = 24)
        {
            var attach = pan.GetAttachTransform(hand); var local = pan.transform.InverseTransformPoint(attach.position);
            var localRotation = Quaternion.Inverse(pan.transform.rotation) * attach.rotation;
            var start = hand.transform.position; var startRotation = hand.transform.rotation;
            var end = position + rotation * local; var endRotation = rotation * localRotation;
            for (var i = 1; i <= frames; i++)
            {
                hand.transform.SetPositionAndRotation(Vector3.Lerp(start, end, i / (float)frames), Quaternion.Slerp(startRotation, endRotation, i / (float)frames));
                yield return null; yield return new WaitForFixedUpdate();
            }
        }
        IEnumerator Drop()
        {
            yield return Frames(18); manager.SelectExit((IXRSelectInteractor)hand, pan); yield return Frames(10);
        }
        void Docked()
        {
            Assert.IsTrue(snap.IsDocked, "pan=" + pan.transform.position + " target=" + snap.DockPosition + " up=" + pan.transform.up + " speed=" + body.velocity);
            Assert.IsFalse(pan.isSelected, "Docking must not create an XRI selection");
            Assert.IsFalse(body.isKinematic); Assert.IsTrue(body.useGravity);
            Assert.AreEqual(RigidbodyConstraints.FreezeAll, body.constraints);
            var d = pan.transform.position - snap.DockPosition;
            Assert.That(new Vector2(d.x, d.z).magnitude, Is.LessThan(.0021f));
            Assert.That(Mathf.Abs(d.y), Is.LessThan(.016f));
        }
        [UnityTest] public IEnumerator InitialDockResistsForcesAndStillHeatsWithoutAHandSelection()
        {
            Docked(); var pose = new Pose(pan.transform.position, pan.transform.rotation);
            body.AddForce(new Vector3(100, 10, 35), ForceMode.Impulse); body.AddTorque(Vector3.one * 50, ForceMode.Impulse);
            yield return Frames(50); Docked();
            Assert.That(Vector3.Distance(pose.position, pan.transform.position), Is.LessThan(.0002f));
            Assert.That(Quaternion.Angle(pose.rotation, pan.transform.rotation), Is.LessThan(.1f));
            var button = host.station.transform.Find("ACT_STOVE").GetComponent<XRSimpleInteractable>(); button.interactionManager = manager;
            hand.transform.position = button.transform.position;
            manager.SelectEnter((IXRSelectInteractor)hand, button); manager.SelectExit((IXRSelectInteractor)hand, button); yield return Frames(8);
            Assert.IsTrue(host.station.GetComponentInChildren<KitchenHeatSource>().IsHeating(pan.GetComponent<KitchenFoodVessel>()));
            Docked();
        }
        [UnityTest] public IEnumerator RealGrabUnlocksAndReleaseRecentersWithoutResettingHandleYaw()
        {
            Docked(); yield return Pick();
            yield return Move(snap.DockPosition + new Vector3(.08f, .22f, -.04f), Quaternion.Euler(55, 30, 0));
            Assert.IsFalse(snap.IsDocked); Assert.IsTrue(pan.isSelected);
            Assert.That(Vector3.Angle(pan.transform.up, Vector3.up), Is.GreaterThan(35), "Held pan must tilt freely");
            yield return Move(snap.DockPosition + new Vector3(.025f, .10f, 0), Quaternion.Euler(0, 35, 0));
            yield return Move(snap.DockPosition + new Vector3(.025f, .035f, 0), Quaternion.Euler(0, 35, 0));
            yield return Drop(); Docked();
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(35, pan.transform.eulerAngles.y)), Is.LessThan(1));
            yield return Pick(); yield return Move(snap.DockPosition + Vector3.up * .2f, Quaternion.identity); yield return Drop();
        }
        [UnityTest] public IEnumerator ReleaseAwayFromStoveKeepsFreePhysics()
        {
            yield return Pick(); var destination = snap.DockPosition + new Vector3(.55f, .2f, 0);
            yield return Move(destination, Quaternion.identity); yield return Drop(); yield return Frames(15);
            Assert.IsFalse(snap.IsDocked); Assert.IsFalse(pan.isSelected);
            Assert.AreEqual(RigidbodyConstraints.None, body.constraints);
            Assert.IsFalse(body.isKinematic); Assert.IsTrue(body.useGravity);
            Assert.That(Vector3.Distance(pan.transform.position, snap.DockPosition), Is.GreaterThan(.3f));
        }
        [UnityTest] public IEnumerator ObstacleAtDockStopsAutomaticRecentering()
        {
            yield return Pick(); yield return Move(snap.DockPosition + Vector3.up * .25f, Quaternion.identity);
            obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube); obstacle.name = "Snap obstruction";
            obstacle.transform.position = snap.DockPosition + new Vector3(-.10f, .04f, 0);
            obstacle.transform.localScale = new Vector3(.025f, .09f, .09f); Physics.SyncTransforms();
            yield return Move(snap.DockPosition + new Vector3(.055f, .04f, 0), Quaternion.identity);
            yield return Drop(); Assert.IsFalse(snap.IsDocked, "Do not snap through an occupied target");
            Object.Destroy(obstacle); yield return null;
            yield return Pick(); yield return Move(snap.DockPosition + Vector3.up * .2f, Quaternion.identity);
            yield return Move(snap.DockPosition + Vector3.up * .035f, Quaternion.identity); yield return Drop(); Docked();
        }
        [UnityTest] public IEnumerator ResetAndSchemeRoundTripRestoreColdDockWhileReviewKeepsPose()
        {
            yield return Pick(); yield return Move(snap.DockPosition + new Vector3(.4f, .2f, 0), Quaternion.identity);
            host.station.GetComponentInChildren<KitchenHeatSource>().SetOn(true);
            Assert.IsTrue(host.Execute(CommandType.ResetWorld).Accepted);
            for (var i = 0; i < 120 && host.Snapshot.IsBusy; i++) yield return null;
            yield return Frames(12); Docked(); Assert.IsFalse(host.station.GetComponentInChildren<KitchenHeatSource>().IsOn);
            var position = pan.transform.position; var visit = host.Snapshot.SchemeVisitId;
            host.Execute(CommandType.Review); host.Execute(CommandType.Continue); yield return Frames(3);
            Docked(); Assert.AreEqual(position, pan.transform.position); Assert.AreEqual(visit, host.Snapshot.SchemeVisitId);
            host.Execute(CommandType.ChangeScheme, SchemeIds.ExternalRefrigerator); yield return Frames(3);
            Assert.AreEqual(SchemeIds.ExternalRefrigerator, host.Snapshot.SchemeId);
            Assert.IsNull(host.station.transform.Find("PREF_Cooking_FryingPan").GetComponent<ReviewPanStoveSnap>());
            host.Execute(CommandType.ChangeScheme, SchemeIds.IntegratedKitchen); yield return Frames(12); BindPan(); Docked();
            Assert.IsFalse(host.station.GetComponentInChildren<KitchenHeatSource>().IsOn);
        }
        [UnityTest] public IEnumerator DisableRestoresTheOriginalBodyConstraints()
        {
            Docked(); snap.enabled = false;
            Assert.AreEqual(RigidbodyConstraints.None, body.constraints); Assert.IsFalse(snap.IsDocked);
            snap.enabled = true; yield return Frames(8); Docked();
        }
    }
}
#endif
