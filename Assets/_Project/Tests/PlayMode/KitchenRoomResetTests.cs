#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using XRApartment.Data;
using XRApartment.Runtime;

namespace XRApartment.Tests
{
    public sealed class KitchenRoomResetTests
    {
        readonly List<GameObject> hidden = new List<GameObject>();
        readonly List<GameObject> owned = new List<GameObject>();
        Scene cooking, sentinel, previous;
        float timeScale;
        const string Cup = "Assets/_Project/Prefabs/Cooking/PREF_Cooking_WaterCup.prefab";

        [SetUp] public void Setup() { timeScale = Time.timeScale; previous = SceneManager.GetActiveScene(); }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.timeScale = timeScale;
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            if (cooking.IsValid() && cooking.isLoaded) yield return SceneManager.UnloadSceneAsync(cooking);
            if (sentinel.IsValid() && sentinel.isLoaded) yield return SceneManager.UnloadSceneAsync(sentinel);
            foreach (var go in owned) if (go != null) Object.DestroyImmediate(go);
            owned.Clear(); foreach (var go in hidden) if (go != null) go.SetActive(true); hidden.Clear();
        }
        GameObject Own(GameObject go) { owned.Add(go); return go; }
        static GameObject LoadCup()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Cup); Assert.IsNotNull(source);
            return Object.Instantiate(source);
        }
        IEnumerator LoadRoom()
        {
            for (var i = 0; i < SceneManager.sceneCount; i++)
                foreach (var root in SceneManager.GetSceneAt(i).GetRootGameObjects())
                    if (root.activeSelf && (root.GetComponentInChildren<Renderer>(true) != null || root.GetComponentInChildren<Collider>(true) != null ||
                        root.GetComponentInChildren<UnityEngine.EventSystems.EventSystem>(true) != null))
                    { hidden.Add(root); root.SetActive(false); }
            cooking = EditorSceneManager.LoadSceneInPlayMode(KitchenScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            for (var i = 0; i < 300 && !cooking.isLoaded; i++) yield return null;
            Assert.IsTrue(cooking.isLoaded); SceneManager.SetActiveScene(cooking);
            cooking.GetRootGameObjects().Single(g => g.name == "PREF_XR_KitchenRig").SetActive(false);
            yield return null; yield return new WaitForFixedUpdate();
        }
        const string KitchenScenePath = "Assets/_Project/Scenes/SCN_Kitchen_Cooking.unity";

        [UnityTest] public IEnumerator ResetButtonRestoresHeldAndLostPropsFoodWaterAndFireWhilePaused()
        {
            yield return LoadRoom();
            var roots = cooking.GetRootGameObjects();
            var room = roots.SelectMany(g => g.GetComponentsInChildren<KitchenRoomReset>()).Single();
            var station = roots.Single(g => g.name == "PREF_Cooking_Workbench");
            var cup = roots.Single(g => g.GetComponent<LiquidContainer>() != null && g.name.Contains("Cup"));
            var pot = roots.Single(g => g.GetComponent<LiquidContainer>() != null && g.name.Contains("Pot"));
            var cupRest = cup.transform.position; var potRest = pot.transform.position;
            var liquid = cup.GetComponent<LiquidContainer>(); var initialMl = liquid.Millilitres;
            var grab = cup.GetComponent<XRGrabInteractable>(); var initialThrow = grab.throwOnDetach;
            var manager = Own(new GameObject("Room reset test manager")).AddComponent<XRInteractionManager>();
            var hand = Own(new GameObject("Room reset tracked hand")).AddComponent<DrivenGrabInteractor>();
            hand.interactionManager = manager; hand.keepSelectedTargetValid = true; grab.interactionManager = manager;
            hand.transform.SetPositionAndRotation(cup.transform.TransformPoint(cup.GetComponent<Rigidbody>().centerOfMass), cup.transform.rotation);
            yield return null; manager.SelectEnter((IXRSelectInteractor)hand, grab); Assert.IsTrue(grab.isSelected);
            var start = hand.transform.position;
            for (var i = 1; i <= 30; i++)
            { hand.transform.position = start + new Vector3(.1f, .2f, -.2f) * (i / 30f); yield return null; yield return new WaitForFixedUpdate(); }
            Assert.That(Vector3.Distance(cup.transform.position, cupRest), Is.GreaterThan(.08f));
            liquid.Receive(100); Assert.Greater(liquid.Millilitres, initialMl);
            pot.GetComponent<Rigidbody>().position = potRest + Vector3.down * 4; Physics.SyncTransforms();
            var tap = roots.SelectMany(g => g.GetComponentsInChildren<WaterTap>()).Single(); tap.SetRunning(true);
            var world = roots.SelectMany(g => g.GetComponentsInChildren<KitchenWaterSystem>()).Single();
            Assert.IsTrue(world.Emit(cupRest + Vector3.up * .5f, Vector3.zero, 20, null));
            var heat = station.GetComponentInChildren<KitchenHeatSource>(); heat.SetOn(true);
            var pan = station.GetComponentsInChildren<KitchenFoodVessel>().Single(v => v.name.Contains("FryingPan"));
            pan.ReceiveEgg(50); pan.Stir(1); pan.Heat(1, 100); Assert.AreEqual(CookingStage.Burnt, pan.Stage);
            var tomato = station.GetComponentsInChildren<CookableIngredient>().Single(f => f.IngredientKind == KitchenIngredientKind.Tomato && f.CutForm == KitchenCutForm.Whole);
            tomato.Heat(1, 13); Assert.IsTrue(tomato.GetComponent<PreparedKitchenProp>().TryCut(1));
            var plate = station.GetComponentsInChildren<PreparedKitchenProp>().Single(p => p.Kind == PreparedPropKind.Fracture);
            Assert.IsTrue(plate.TryImpact(10));
            var fridge = station.GetComponentInChildren<KitchenFridge>(); fridge.Door.SetOpenImmediate(true);

            sentinel = SceneManager.CreateScene("Room reset sentinel");
            var otherCup = LoadCup(); SceneManager.MoveGameObjectToScene(otherCup, sentinel);
            otherCup.transform.position = new Vector3(50, 50, 50); otherCup.GetComponent<Rigidbody>().isKinematic = true;
            yield return null; var otherLiquid = otherCup.GetComponent<LiquidContainer>(); otherLiquid.Receive(50); var otherMl = otherLiquid.Millilitres;
            var rig = roots.Single(g => g.name == "PREF_XR_KitchenRig"); var rigPosition = rig.transform.position;
            Time.timeScale = 0;
            var button = station.transform.Find("ACT_RESET").GetComponent<XRSimpleInteractable>(); button.interactionManager = manager;
            var resetHand = Own(new GameObject("Room reset other tracked hand")).AddComponent<DrivenGrabInteractor>();
            resetHand.interactionManager = manager; resetHand.keepSelectedTargetValid = true; resetHand.transform.position = button.transform.position;
            manager.SelectEnter((IXRSelectInteractor)resetHand, button); manager.SelectExit((IXRSelectInteractor)resetHand, button);
            Assert.IsFalse(grab.isSelected); Assert.IsTrue(room.IsResetting);
            room.ResetRoom(); // Repeat before delayed XRI detach has drained.
            for (var i = 0; i < 5; i++) yield return null;
            Assert.IsFalse(room.IsResetting); Assert.IsFalse(tap.Running); Assert.IsFalse(heat.IsOn);
            Assert.IsFalse(plate.IsSplit); Assert.That(fridge.Door.Progress, Is.LessThan(.001));
            Assert.AreEqual(CookingStage.Empty, pan.Stage); Assert.AreEqual(0, pan.Batch.Millilitres);
            Assert.AreEqual(CookingStage.Raw, tomato.Stage); Assert.IsFalse(tomato.GetComponent<PreparedKitchenProp>().IsSplit);
            Assert.That(Vector3.Distance(cup.transform.position, cupRest), Is.LessThan(.02f));
            Assert.That(Vector3.Distance(pot.transform.position, potRest), Is.LessThan(.02f));
            Assert.That(liquid.Millilitres, Is.EqualTo(initialMl).Within(.01f)); Assert.AreEqual(0, world.ActiveParcels);
            Assert.That(otherLiquid.Millilitres, Is.EqualTo(otherMl).Within(.001f)); Assert.AreEqual(new Vector3(50, 50, 50), otherCup.transform.position);
            Assert.AreEqual(rigPosition, rig.transform.position); Assert.AreEqual(0, Time.timeScale);
            Assert.IsTrue(grab.enabled); Assert.AreEqual(initialThrow, grab.throwOnDetach);
            Assert.IsFalse(roots.SelectMany(g => g.GetComponentsInChildren<LiquidStream>(true)).Any(s => s.GetComponent<LineRenderer>().enabled));
            hand.transform.SetPositionAndRotation(cup.transform.position, cup.transform.rotation); yield return null;
            manager.SelectEnter((IXRSelectInteractor)hand, grab); Assert.IsTrue(grab.isSelected); manager.SelectExit((IXRSelectInteractor)hand, grab);
        }

        [UnityTest] public IEnumerator DisablingResetDuringItsPendingRestorePreservesOriginallyDisabledInteraction()
        {
            sentinel = SceneManager.CreateScene("Room reset isolated fixture"); SceneManager.SetActiveScene(sentinel);
            var stationSource = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Cooking/PREF_Cooking_Workbench.prefab");
            var station = Object.Instantiate(stationSource).GetComponent<KitchenCookingStation>();
            var cup = LoadCup(); cup.transform.position = new Vector3(30, 30, 30);
            var body = cup.GetComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
            var grab = cup.GetComponent<XRGrabInteractable>(); grab.enabled = false; grab.throwOnDetach = false;
            var rest = cup.transform.position;
            var doorRoot = new GameObject("Fixture room door"); doorRoot.transform.position = new Vector3(30, 32, 30); doorRoot.AddComponent<BoxCollider>();
            var door = doorRoot.AddComponent<KinematicSwingDoor>();
            var drawerRoot = new GameObject("Fixture room drawer"); drawerRoot.transform.position = new Vector3(30, 34, 30); drawerRoot.AddComponent<BoxCollider>();
            var drawer = drawerRoot.AddComponent<KinematicLinearDrawer>(); var drawerRest = drawer.transform.position;
            var resetRoot = new GameObject("Fixture room reset"); resetRoot.SetActive(false);
            var room = resetRoot.AddComponent<KitchenRoomReset>(); room.Configure(station); resetRoot.SetActive(true);
            yield return null;
            door.SetOpenImmediate(true); drawer.SetOpenImmediate(true);
            body.position += Vector3.down * 5; Physics.SyncTransforms();
            room.ResetRoom(); room.ResetRoom(); room.enabled = false;
            Assert.IsFalse(room.IsResetting); Assert.IsFalse(grab.enabled); Assert.IsFalse(grab.throwOnDetach);
            Assert.IsTrue(body.isKinematic); Assert.IsFalse(body.useGravity);
            Assert.That(Vector3.Distance(cup.transform.position, rest), Is.LessThan(.001f));
            Assert.IsFalse(door.IsOpen); Assert.IsFalse(drawer.IsOpen); Assert.That(Vector3.Distance(drawer.transform.position, drawerRest), Is.LessThan(.001f));
            yield return null;
        }
    }
}
#endif
