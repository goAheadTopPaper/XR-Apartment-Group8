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
using XRApartment.Data;
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
        string feedbackDirectory, previousFeedbackDirectory;
        [UnitySetUp] public IEnumerator Load()
        {
            previousFeedbackDirectory = System.Environment.GetEnvironmentVariable("REVIEW_V2_FEEDBACK_DIRECTORY");
            feedbackDirectory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ReviewV2-regression-" + System.Guid.NewGuid().ToString("N"));
            System.Environment.SetEnvironmentVariable("REVIEW_V2_FEEDBACK_DIRECTORY", feedbackDirectory);

            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                if (root.activeSelf && (root.GetComponentInChildren<XROrigin>(true) != null || root.GetComponentInChildren<EventSystem>(true) != null || root.GetComponentInChildren<XRInteractionManager>(true) != null))
                { hidden.Add(root); root.SetActive(false); }
            scene = EditorSceneManager.LoadSceneInPlayMode("Assets/_Project/Scenes/SCN_Kitchen_ReviewV2_M1Regression.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            while (!scene.isLoaded) yield return null;
            yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            System.Environment.SetEnvironmentVariable("REVIEW_V2_FEEDBACK_DIRECTORY", previousFeedbackDirectory);
            if (System.IO.Directory.Exists(feedbackDirectory)) System.IO.Directory.Delete(feedbackDirectory, true);

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
        [UnityTest] public IEnumerator RealEggProcessingStartsG02OnceAndResetAllowsNewVisitEvidence()
        {
            var host = Experience(); var facts = new List<InteractionEvent>(); host.Observed += facts.Add;
            var egg = All<KitchenEgg>().First(e => e.gameObject.activeInHierarchy);
            var bowl = All<KitchenFoodVessel>().Single(v => v.name == "PREF_Cooking_MixingBowl");
            var visit = host.Snapshot.SchemeVisitId;
            Assert.AreEqual(GoalState.NotStarted, host.Snapshot.Goals.Single(g => g.Id == "G02").State);
            host.station.ActiveFridge.Door.SetOpenImmediate(true);
            yield return null;
            Assert.AreEqual(GoalState.NotStarted, host.Snapshot.Goals.Single(g => g.Id == "G02").State,
                "Opening the fridge must not start Cooking");
            egg.transform.position = bowl.transform.TransformPoint(Vector3.up * (bowl.InnerBottom + bowl.InnerHeight + .06f));
            egg.GetComponent<Rigidbody>().position = egg.transform.position;
            Physics.SyncTransforms();
            Assert.IsTrue(egg.GetComponent<PreparedKitchenProp>().TryCrack());
            Assert.That(egg.LastReceivedMillilitres, Is.GreaterThanOrEqualTo(35));
            for (int i = 0; i < 10 && !facts.Any(f => f.Kind == InteractionKind.EggReceivedInBowl); i++) yield return null;
            Assert.IsTrue(facts.Any(f => f.Kind == InteractionKind.EggReceivedInBowl),
                "Recipe actions=" + host.recipe.Recipe.Actions + "; received=" + egg.LastReceivedMillilitres);
            var start = facts.Single(f => f.Kind == InteractionKind.EggReceivedInBowl);
            var goal = host.Snapshot.Goals.Single(g => g.Id == "G02");
            Assert.AreEqual(GoalState.InProgress, goal.State);
            Assert.AreEqual(start.EventId, goal.EvidenceId);
            Assert.AreEqual(host.Snapshot.SessionId, start.SessionId);
            Assert.AreEqual(visit, start.SchemeVisitId);
            Assert.AreEqual(SchemeIds.Baseline, start.SchemeId);
            Assert.AreEqual(host.Snapshot.ModelVersion, start.ModelVersion);
            Assert.AreEqual("COOKING_STATION", start.ObjectId);
            Assert.AreEqual(SessionPhase.Experience, host.Snapshot.Phase);
            host.recipe.Recipe.Observe(TomatoEggAction.SliceTomato);
            yield return null;
            Assert.AreEqual(1, facts.Count(f => f.Kind == InteractionKind.TomatoCut || f.Kind == InteractionKind.EggReceivedInBowl),
                "Later processing must not publish another start fact");
            var millilitres = bowl.Batch.Millilitres;
            host.Execute(CommandType.Review);
            host.Execute(CommandType.Continue);
            Assert.AreEqual(visit, host.Snapshot.SchemeVisitId);
            Assert.AreEqual(GoalState.InProgress, host.Snapshot.Goals.Single(g => g.Id == "G02").State);
            Assert.AreEqual(millilitres, bowl.Batch.Millilitres);
            Assert.AreEqual(start.EventId, host.Snapshot.Goals.Single(g => g.Id == "G02").EvidenceId);
            Assert.IsTrue(host.Execute(CommandType.ResetWorld).Accepted);
            for (int i = 0; i < 30 && host.Snapshot.IsBusy; i++) yield return null;
            Assert.IsFalse(host.Snapshot.IsBusy);
            Assert.AreNotEqual(visit, host.Snapshot.SchemeVisitId);
            Assert.AreEqual(GoalState.NotStarted, host.Snapshot.Goals.Single(g => g.Id == "G02").State);
            Assert.AreEqual(1, facts.Count(f => f.Kind == InteractionKind.TomatoCut || f.Kind == InteractionKind.EggReceivedInBowl),
                "Programmatic reset must not count as user preparation");
            egg.transform.position = bowl.transform.TransformPoint(Vector3.up * (bowl.InnerBottom + bowl.InnerHeight + .06f));
            egg.GetComponent<Rigidbody>().position = egg.transform.position;
            Physics.SyncTransforms();
            Assert.IsTrue(egg.GetComponent<PreparedKitchenProp>().TryCrack());
            for (int i = 0; i < 10 && facts.Count(f => f.Kind == InteractionKind.EggReceivedInBowl) < 2; i++) yield return null;
            Assert.AreEqual(2, facts.Count(f => f.Kind == InteractionKind.TomatoCut || f.Kind == InteractionKind.EggReceivedInBowl));
            Assert.AreNotEqual(start.EventId, host.Snapshot.Goals.Single(g => g.Id == "G02").EvidenceId);
            Assert.AreEqual(SessionPhase.Experience, host.Snapshot.Phase);
            host.Observed -= facts.Add;
        }
        [UnityTest] public IEnumerator FirstTomatoCutStartsG02BeforeRecipeSliceFlag()
        {
            var host = Experience(); var facts = new List<InteractionEvent>(); host.Observed += facts.Add;
            var tomato = host.Resolve("OBJECT_030").GetComponent<PreparedKitchenProp>();
            Assert.AreEqual(0, tomato.CutCount);
            Assert.AreEqual(TomatoEggAction.None, host.recipe.Recipe.Actions & TomatoEggAction.SliceTomato);
            Assert.IsTrue(tomato.TryCut(100f), "Use the real prepared-food cut component as the fixture");
            Assert.AreEqual(1, tomato.CutCount);
            yield return null;
            var fact = facts.Single(f => f.Kind == InteractionKind.TomatoCut);
            Assert.AreEqual(fact.EventId, host.Snapshot.Goals.Single(g => g.Id == "G02").EvidenceId);
            Assert.AreEqual(GoalState.InProgress, host.Snapshot.Goals.Single(g => g.Id == "G02").State);
            Assert.AreEqual(TomatoEggAction.None, host.recipe.Recipe.Actions & TomatoEggAction.SliceTomato);
            Assert.AreEqual(SessionPhase.Experience, host.Snapshot.Phase);
            host.Observed -= facts.Add;
        }
        [UnityTest] public IEnumerator GroceryStorageRejectsHeldFloorAndTransitAndCoexistsWithCooking()
            => GroceryStorageScenario(false);
        [UnityTest] public IEnumerator CookingFirstThenGroceryKeepsBothActivities()
            => GroceryStorageScenario(true);
        IEnumerator GroceryStorageScenario(bool cookingFirst)
        {
            var host = Experience(); var activity = host.groceries;
            Assert.NotNull(activity); Assert.IsTrue(activity.IsConfigured);
            var facts = new List<InteractionEvent>(); host.Observed += facts.Add;
            var tomato = activity.groceries.Single(g => g.GetComponent<KitchenEgg>() == null).GetComponent<PreparedKitchenProp>();
            if (cookingFirst)
            {
                Assert.IsTrue(tomato.TryCut(100)); yield return null; yield return null;
                Assert.AreEqual(GoalState.InProgress, host.Snapshot.Goals.Single(g => g.Id == "G02").State);
                Assert.AreEqual(GoalState.NotStarted, host.Snapshot.Goals.Single(g => g.Id == "G01").State);
            }
            var eggs = activity.groceries.Where(g => g.GetComponent<KitchenEgg>() != null).ToArray();
            var egg = eggs[0]; var rest = egg.transform.position;
            handObject = new GameObject("Grocery test hand"); var hand = handObject.AddComponent<DrivenGrabInteractor>();
            var manager = All<XRInteractionManager>().Single(); hand.interactionManager = manager; hand.keepSelectedTargetValid = true;
            host.station.ActiveFridge.Door.SetOpenImmediate(true);
            GoalSnapshot Goal() => host.Snapshot.Goals.Single(g => g.Id == "G01");
            void Pose(XRGrabInteractable item, Vector3 position)
            {
                item.transform.position = position; var body = item.GetComponent<Rigidbody>(); body.position = position;
                body.velocity = body.angularVelocity = Vector3.zero;
                item.SetTargetPose(new Pose(position, item.transform.rotation)); Physics.SyncTransforms();
                hand.transform.position = body.worldCenterOfMass;
            }
            void Select(XRGrabInteractable item)
            {
                item.interactionManager = manager; hand.transform.position = item.GetComponent<Rigidbody>().worldCenterOfMass;
                manager.SelectEnter((IXRSelectInteractor)hand, item); Assert.IsTrue(item.isSelected);
            }
            var target = host.station.ActiveFridge.transform.TransformPoint(new Vector3(0,1.334f,0));
            Select(egg); Pose(egg, target); yield return new WaitForSeconds(.7f);
            Assert.AreEqual(GoalState.NotStarted, Goal().State, "Held objects are not stored");
            Pose(egg, new Vector3(-2.5f,.10f,-1.25f)); manager.SelectExit((IXRSelectInteractor)hand, egg);
            yield return new WaitForSeconds(.7f); Assert.AreEqual(GoalState.NotStarted, Goal().State, "Floor is invalid");
            Select(egg); Pose(egg, target); manager.SelectExit((IXRSelectInteractor)hand, egg);
            yield return new WaitForSeconds(.1f); Pose(egg, rest);
            yield return new WaitForSeconds(.6f); Assert.AreEqual(GoalState.NotStarted, Goal().State, "Brief transit is invalid");
            Select(egg); Pose(egg, target); manager.SelectExit((IXRSelectInteractor)hand, egg);
            yield return new WaitForSeconds(1f);
            Assert.AreEqual(GoalState.InProgress, Goal().State);
            var fact = facts.Single(f => f.Kind == InteractionKind.IngredientStored);
            Assert.AreEqual(fact.EventId, Goal().EvidenceId); Assert.AreEqual(activity.zoneIdentity.Id, fact.ZoneId);
            if (!cookingFirst) { Assert.IsTrue(tomato.TryCut(100)); yield return null; yield return null; }
            Assert.AreEqual(2, host.Snapshot.Goals.Count(g => g.State == GoalState.InProgress));
            Select(egg); Pose(egg, target); manager.SelectExit((IXRSelectInteractor)hand, egg);
            yield return new WaitForSeconds(.8f); Assert.AreEqual(1, facts.Count(f => f.Kind == InteractionKind.IngredientStored));
            Select(eggs[1]); Pose(eggs[1], host.station.ActiveFridge.transform.TransformPoint(new Vector3(.10f,1.334f,.10f)));
            manager.SelectExit((IXRSelectInteractor)hand, eggs[1]); yield return new WaitForSeconds(1f);
            Assert.AreEqual(GoalState.Completed, Goal().State); Assert.AreEqual(SessionPhase.Experience, host.Snapshot.Phase);
            var visit = host.Snapshot.SchemeVisitId; host.Execute(CommandType.Review); host.Execute(CommandType.Continue);
            Assert.AreEqual(visit, host.Snapshot.SchemeVisitId); Assert.AreEqual(GoalState.Completed, Goal().State);
            host.Execute(CommandType.ResetWorld);
            for (int i = 0; i < 30 && host.Snapshot.IsBusy; i++) yield return null;
            Assert.IsFalse(host.Snapshot.IsBusy); Assert.AreNotEqual(visit, host.Snapshot.SchemeVisitId);
            yield return new WaitForSeconds(.7f);
            Assert.AreEqual(GoalState.NotStarted, Goal().State); Assert.That(Vector3.Distance(rest, egg.transform.position), Is.LessThan(.04f));
            Assert.AreEqual(2, facts.Count(f => f.Kind == InteractionKind.IngredientStored)); host.Observed -= facts.Add;
        }
        [UnityTest] public IEnumerator ReadyBoundaryCompletesG02WithoutMovingSessionPhase()
        {
            var host = Experience(); var facts = new List<InteractionEvent>(); host.Observed += facts.Add;
            var all = (TomatoEggAction)((1 << 14) - 1);
            host.recipe.Recipe.Observe(all);
            host.recipe.Recipe.ObserveCooking(true, true, false);
            Assert.AreEqual(TomatoEggStep.Ready, host.recipe.Recipe.Step);
            // Sample the aggregate boundary before KitchenRecipeController.Update replaces the synthetic fixture.
            host.SendMessage("LateUpdate");
            Assert.AreEqual(GoalState.Completed, host.Snapshot.Goals.Single(g => g.Id == "G02").State);
            Assert.AreEqual(SessionPhase.Experience, host.Snapshot.Phase);
            var cooked = facts.Single(f => f.Kind == InteractionKind.DishCooked);
            Assert.AreEqual(cooked.EventId, host.Snapshot.Goals.Single(g => g.Id == "G02").EvidenceId);
            yield return null;
            Assert.AreEqual(1, facts.Count(f => f.Kind == InteractionKind.DishCooked));
            host.Observed -= facts.Add;
        }
        [Test] public void SavePortBindingKeepsFailureInReviewAndRoutesMatchingSnapshot()
        {
            var source = All<ReviewSessionHost>().Single();
            var go = new GameObject("Save binding fixture"); go.SetActive(false);
            try
            {
                var port = go.AddComponent<ReviewSavePortProbe>(); var host = go.AddComponent<ReviewSessionHost>();
                host.station = source.station; host.roomReset = source.roomReset; host.recipe = source.recipe;
                host.identities = source.identities; host.sessionSavePort = port;
                host.modelVersion = source.Snapshot.ModelVersion;
                go.SetActive(true);
                host.Execute(CommandType.CompleteSetup); host.Execute(CommandType.CompleteOnboarding); host.Execute(CommandType.Review);
                host.Execute(CommandType.Finish);
                Assert.AreEqual(SessionPhase.Review, host.Snapshot.Phase); Assert.AreEqual(SaveStatus.Failed, host.Snapshot.SaveStatus);
                Assert.AreEqual(host.Snapshot.SessionId, port.Received.SessionId);
                Assert.AreEqual(host.Snapshot.SchemeVisitId, port.Received.SchemeVisitId);
                Assert.AreEqual(source.Snapshot.ModelVersion, port.Received.ModelVersion);
                port.Succeed = true; host.Execute(CommandType.Finish);
                Assert.AreEqual(SessionPhase.Finished, host.Snapshot.Phase); Assert.AreEqual(SaveStatus.Verified, host.Snapshot.SaveStatus);
            }
            finally { Object.DestroyImmediate(go); }
        }
        [Test] public void ModelIdentityDoesNotFollowGroceryAvailability()
        {
            var source = All<ReviewSessionHost>().Single();
            Assert.AreEqual("cooking-m1-groceries-v1", source.Snapshot.ModelVersion);
            var go = new GameObject("Missing grocery configuration fixture"); go.SetActive(false);
            try
            {
                var host = go.AddComponent<ReviewSessionHost>();
                host.station = source.station; host.roomReset = source.roomReset; host.recipe = source.recipe;
                host.identities = source.identities;
                // Same model objects, but no usable G01 configuration.
                host.modelVersion = source.Snapshot.ModelVersion;
                go.SetActive(true);
                Assert.IsFalse(host.Snapshot.Goals.Single(g => g.Id == "G01").Available);
                Assert.AreEqual(source.Snapshot.ModelVersion, host.Snapshot.ModelVersion,
                    "Disabling one activity must not relabel the same world as an older model");
            }
            finally { Object.DestroyImmediate(go); }
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
    public sealed class ReviewSavePortProbe : MonoBehaviour, ISessionSavePort
    {
        public bool Succeed;
        public SessionSnapshot Received;
        public void Save(string id, SessionSnapshot snapshot, System.Action<string, bool, string> completed)
        { Received = snapshot; completed(id, Succeed, "Synthetic port binding fixture; no disk write"); }
    }
}
#endif
