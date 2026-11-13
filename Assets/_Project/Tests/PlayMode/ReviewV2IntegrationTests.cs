#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Unity.XR.CoreUtils;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using XRApartment.Data.ReviewV2;
using XRApartment.Runtime;
using XRApartment.Runtime.Core;
using XRApartment.Runtime.ReviewV2;
using XRApartment.Runtime.UI;

namespace XRApartment.Tests
{
    public class ReviewV2IntegrationTests
    {
        Scene scene;
        GameObject handObject;
        readonly List<GameObject> hidden = new List<GameObject>();
        T[] All<T>() where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
        [UnitySetUp] public IEnumerator Load()
        {
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                if (root.activeSelf && (root.GetComponentInChildren<XROrigin>(true) != null || root.GetComponentInChildren<EventSystem>(true) != null || root.GetComponentInChildren<XRInteractionManager>(true) != null))
                { hidden.Add(root); root.SetActive(false); }
            scene = EditorSceneManager.LoadSceneInPlayMode("Assets/_Project/Scenes/SCN_Kitchen_ReviewV2.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            while (!scene.isLoaded) yield return null;
            yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (handObject != null) Object.Destroy(handObject);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            foreach (var go in hidden) if (go != null) go.SetActive(true);
            hidden.Clear(); Time.timeScale = 1;
        }
        ReviewSessionHost Experience()
        {
            var host = All<ReviewSessionHost>().Single();
            Assert.IsTrue(host.Execute(CommandType.CompleteSetup).Accepted);
            Assert.IsTrue(host.Execute(CommandType.CompleteOnboarding).Accepted);
            All<ReviewIntegrationPanel>().Single().SetOpen(false); return host;
        }
        [UnityTest] public IEnumerator SceneHasOneOwnerAndReviewPreservesWorld()
        {
            Assert.AreEqual(1, All<XROrigin>().Count(x => x.gameObject.activeInHierarchy));
            Assert.AreEqual(1, All<EventSystem>().Count(x => x.isActiveAndEnabled));
            Assert.AreEqual(1, All<XRInteractionManager>().Count(x => x.isActiveAndEnabled));
            Assert.AreEqual(0, All<JourneyRuntimeHost>().Length);
            Assert.IsFalse(All<LocomotionSettingsView>().Any(x => x.gameObject.activeInHierarchy));
            Assert.IsFalse(All<KitchenActivityGuide>().Any(x => x.gameObject.activeInHierarchy));
            Assert.IsFalse(All<KitchenPauseMenu>().Any(x => x.gameObject.activeInHierarchy));
            Assert.IsFalse(All<KitchenFeedbackNotes>().Any(x => x.gameObject.activeInHierarchy));
            Assert.AreEqual(All<ReviewSceneIdentity>().Length, All<ReviewSceneIdentity>().Select(i => i.Id).Distinct().Count());
            CapturePanel(All<XROrigin>().Single().Camera);
            var host = Experience(); var grab = All<XRGrabInteractable>().First(g => g.isActiveAndEnabled);
            var position = grab.transform.position; var step = host.recipe.Recipe.Step;
            var water = All<LiquidContainer>().Select(c => c.Millilitres).ToArray();
            var visit = host.Snapshot.SchemeVisitId;
            host.Execute(CommandType.Review); host.Execute(CommandType.Continue);
            Assert.AreEqual(position, grab.transform.position); Assert.AreEqual(step, host.recipe.Recipe.Step);
            CollectionAssert.AreEqual(water, All<LiquidContainer>().Select(c => c.Millilitres).ToArray());
            Assert.AreEqual(visit, host.Snapshot.SchemeVisitId);
            host.Execute(CommandType.Review); host.Execute(CommandType.Finish);
            Assert.AreEqual(SessionPhase.Finished, host.Snapshot.Phase);
            Assert.AreEqual(SaveStatus.NotConfigured, host.Snapshot.SaveStatus);
            yield return null;
        }
        [UnityTest] public IEnumerator RealGrabReleaseReenableAndResetHaveCorrectEvents()
        {
            var host = Experience(); var facts = new List<InteractionEvent>(); host.Observed += facts.Add;
            var grab = All<KitchenEgg>().First(e => e.gameObject.activeInHierarchy).GetComponent<XRGrabInteractable>();
            var bridge = grab.GetComponent<CookingGrabBridge>();
            bridge.enabled = false; bridge.enabled = true; bridge.enabled = false; bridge.enabled = true;
            handObject = new GameObject("V2 driven hand"); var hand = handObject.AddComponent<DrivenGrabInteractor>();
            var manager = All<XRInteractionManager>().Single(); hand.interactionManager = manager; hand.keepSelectedTargetValid = true;
            grab.interactionManager = manager;
            host.station.ActiveFridge.Door.SetOpenImmediate(true);
            Assert.IsTrue(host.station.ActiveFridge.Accessible, "Fixture must open the real fridge before selecting stored food");
            hand.transform.position = grab.transform.position; yield return null;
            manager.SelectEnter((IXRSelectInteractor)hand, grab);
            Assert.IsTrue(grab.isSelected);
            manager.SelectExit((IXRSelectInteractor)hand, grab);
            Assert.AreEqual(1, facts.Count(e => e.Kind == InteractionKind.ObjectGrabbed));
            Assert.AreEqual(1, facts.Count(e => e.Kind == InteractionKind.ObjectReleased));
            Assert.AreEqual(grab.GetComponent<ReviewSceneIdentity>().Id, facts[0].ObjectId);
            Assert.AreEqual(host.Snapshot.SessionId, facts[0].SessionId); Assert.IsFalse(facts[0].IsMock);
            Assert.IsTrue(host.Snapshot.Goals.All(g => g.State == GoalState.NotStarted));
            manager.SelectEnter((IXRSelectInteractor)hand, grab); int count = facts.Count;
            string visit = host.Snapshot.SchemeVisitId;
            Assert.IsTrue(host.Execute(CommandType.ResetWorld).Accepted);
            for (int i = 0; i < 30 && host.Snapshot.IsBusy; i++) yield return null;
            Assert.IsFalse(host.Snapshot.IsBusy); Assert.AreNotEqual(visit, host.Snapshot.SchemeVisitId);
            Assert.AreEqual(count, facts.Count, "Programmatic reset must not masquerade as a release");
            host.Observed -= facts.Add;
        }
        static void CapturePanel(Camera camera)
        {
            // Camera.Render is a Built-in pipeline API. Do not make old screenshot evidence stand for HDRP QA.
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null ||
                UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null) return;
            var target = new RenderTexture(1024, 1024, 24);
            var pixels = new Texture2D(1024, 1024, TextureFormat.RGB24, false);
            var previous = camera.targetTexture; var active = RenderTexture.active;
            try
            {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1024, 1024), 0, 0); pixels.Apply();
                System.IO.Directory.CreateDirectory("Logs/ReviewV2");
                System.IO.File.WriteAllBytes("Logs/ReviewV2/panel.png", pixels.EncodeToPNG());
            }
            finally { camera.targetTexture = previous; RenderTexture.active = active; Object.Destroy(target); Object.Destroy(pixels); }
        }
        [UnityTest] public IEnumerator SetupAndPauseGatePhysicalInteractionAndCalibrationPreservesVisit()
        {
            var host = All<ReviewSessionHost>().Single();
            var grab = All<KitchenEgg>().First(e => e.gameObject.activeInHierarchy).GetComponent<XRGrabInteractable>();
            var manager = All<XRInteractionManager>().Single();
            handObject = new GameObject("V2 gate hand"); var hand = handObject.AddComponent<DrivenGrabInteractor>();
            hand.interactionManager = manager; hand.keepSelectedTargetValid = true; grab.interactionManager = manager;
            host.station.ActiveFridge.Door.SetOpenImmediate(true);
            hand.transform.position = grab.transform.position; yield return null;
            Assert.IsFalse(manager.IsSelectPossible((IXRSelectInteractor)hand, grab), "Setup must block Cooking selections");
            string visit = host.Snapshot.SchemeVisitId;
            Assert.IsTrue(host.Execute(CommandType.CalibrateSeated).Accepted);
            Assert.IsTrue(host.Execute(CommandType.CalibrateStanding).Accepted);
            Assert.AreEqual(visit, host.Snapshot.SchemeVisitId);
            Experience(); manager.SelectEnter((IXRSelectInteractor)hand, grab); Assert.IsTrue(grab.isSelected);
            var position = grab.transform.position;
            host.Execute(CommandType.Pause);
            Assert.IsFalse(grab.isSelected); Assert.IsFalse(manager.IsSelectPossible((IXRSelectInteractor)hand, grab));
            Assert.AreEqual(position, grab.transform.position); Assert.AreEqual(visit, host.Snapshot.SchemeVisitId);
            Assert.AreEqual(0, Time.timeScale);
            host.Execute(CommandType.Resume);
            Assert.IsTrue(manager.IsSelectPossible((IXRSelectInteractor)hand, grab));
            Assert.AreEqual(visit, host.Snapshot.SchemeVisitId);
            yield return null;
        }
    }
}
#endif
