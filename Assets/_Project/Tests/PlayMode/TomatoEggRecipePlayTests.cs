#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
using Unity.Profiling;
using XRApartment.Data;
using XRApartment.Runtime;
using Object = UnityEngine.Object;

namespace XRApartment.Tests
{
    public sealed class TomatoEggRecipePlayTests
    {
        const string ScenePath = "Assets/_Project/Scenes/SCN_Kitchen_Cooking.unity";
        const string RecipeFolder = "Assets/_Project/Prefabs/Cooking/Recipe/";
        readonly List<GameObject> owned = new List<GameObject>();
        readonly List<GameObject> hidden = new List<GameObject>();
        XRInteractionManager manager;
        DrivenGrabInteractor left, right;
        Scene scene, previous;
        GameObject station;
        Camera sourceCamera;
        GameObject Own(GameObject go) { owned.Add(go); return go; }
        [SetUp] public void SetUp()
        {
            manager = Own(new GameObject("Recipe driver manager")).AddComponent<XRInteractionManager>();
            if (!Object.FindObjectsOfType<AudioListener>().Any(listener => listener.isActiveAndEnabled))
                manager.gameObject.AddComponent<AudioListener>();
            left = Own(new GameObject("Recipe driver left")).AddComponent<DrivenGrabInteractor>();
            right = Own(new GameObject("Recipe driver right")).AddComponent<DrivenGrabInteractor>();
            left.interactionManager = right.interactionManager = manager;
            left.keepSelectedTargetValid = right.keepSelectedTargetValid = true;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (scene.IsValid() && scene.isLoaded)
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                yield return SceneManager.UnloadSceneAsync(scene);
            }
            for (var i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Object.DestroyImmediate(owned[i]);
            owned.Clear(); foreach (var root in hidden) if (root != null) root.SetActive(true); hidden.Clear();
        }
        IEnumerator LoadKitchen()
        {
            previous = SceneManager.GetActiveScene();
            for (var i = 0; i < SceneManager.sceneCount; i++)
                foreach (var root in SceneManager.GetSceneAt(i).GetRootGameObjects())
                    if (!owned.Contains(root) && root.activeSelf && (root.GetComponentInChildren<Renderer>(true) != null ||
                        root.GetComponentInChildren<Collider>(true) != null || root.GetComponentInChildren<UnityEngine.EventSystems.EventSystem>(true) != null))
                    { hidden.Add(root); root.SetActive(false); }
            scene = EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            for (var i = 0; i < 300 && !scene.isLoaded; i++) yield return null;
            Assert.IsTrue(scene.isLoaded); SceneManager.SetActiveScene(scene);
            var roots = scene.GetRootGameObjects(); var rig = roots.Single(go => go.name == "PREF_XR_KitchenRig");
            sourceCamera = rig.GetComponentInChildren<Camera>(true); rig.SetActive(false);
            if (!Object.FindObjectsOfType<AudioListener>().Any(listener => listener.isActiveAndEnabled))
                manager.gameObject.AddComponent<AudioListener>();
            station = roots.Single(go => go.name == "PREF_Cooking_Workbench");
            yield return Frames(40);
        }
        static IEnumerator Frames(int count) { for (var i = 0; i < count; i++) yield return new WaitForFixedUpdate(); }
        IEnumerator Pick(DrivenGrabInteractor hand, GameObject target)
        {
            var grab = target.GetComponent<XRGrabInteractable>(); Assert.IsNotNull(grab);
            grab.interactionManager = manager;
            Physics.SyncTransforms();
            var body = target.GetComponent<Rigidbody>();
            hand.transform.SetPositionAndRotation(body == null ? target.transform.position : target.transform.TransformPoint(body.centerOfMass), target.transform.rotation);
            Physics.SyncTransforms();
            Assert.IsTrue(manager.IsSelectPossible(hand, grab), "Cannot select " + target.name);
            manager.SelectEnter((IXRSelectInteractor)hand, grab); yield return null;
            Assert.IsTrue(grab.isSelected, "Driver did not actually hold " + target.name);
        }
        IEnumerator MoveHeld(DrivenGrabInteractor hand, GameObject target, Vector3 position, Quaternion rotation, int frames = 24)
        {
            var grab = target.GetComponent<XRGrabInteractable>(); var attach = grab.GetAttachTransform(hand);
            var local = target.transform.InverseTransformPoint(attach.position);
            var localRotation = Quaternion.Inverse(target.transform.rotation) * attach.rotation;
            var start = hand.transform.position; var startRotation = hand.transform.rotation;
            var end = position + rotation * local; var endRotation = rotation * localRotation;
            for (var i = 1; i <= frames; i++)
            {
                hand.transform.SetPositionAndRotation(Vector3.Lerp(start, end, i / (float)frames), Quaternion.Slerp(startRotation, endRotation, i / (float)frames));
                // Let XRI consume this tracked pose in its rendered update
                // before advancing the next physics/input sample.
                yield return null;
                yield return new WaitForFixedUpdate();
            }
            yield return null;

        }
        void Drop(DrivenGrabInteractor hand, GameObject target)
        {
            var grab = target.GetComponent<XRGrabInteractable>();
            if (grab.isSelected) manager.SelectExit((IXRSelectInteractor)hand, grab);
        }
        IEnumerator PlaceAndRelease(DrivenGrabInteractor hand, GameObject target)
        {
            // Wait on rendered game time, matching XRI's throw-smoothing window.
            // Several physics catch-up steps alone do not establish a still hand.
            var until = Time.time + .4f;
            while (Time.time < until) yield return null;
            Drop(hand, target); yield return null; yield return null;
        }
        IEnumerator ToggleDoor(KitchenFridge fridge)
        {
            var door = fridge.Door.GetComponent<XRSimpleInteractable>(); door.interactionManager = manager;
            manager.SelectEnter((IXRSelectInteractor)right, door); manager.SelectExit((IXRSelectInteractor)right, door);
            yield return Frames(55);
        }
        IEnumerator Retrieve(DrivenGrabInteractor hand, GameObject item, KitchenFridge fridge)
        {
            yield return Pick(hand, item);
            var p = item.transform.position;
            yield return MoveHeld(hand, item, p + Vector3.up * .11f, item.transform.rotation, 18);
            p = item.transform.position;
            // Step back beyond the open leaf's swing before closing the door.
            var local = fridge.transform.InverseTransformPoint(p); local.z = 1.12f;
            yield return MoveHeld(hand, item, fridge.transform.TransformPoint(local), item.transform.rotation, 48);
            Assert.IsFalse(fridge.Contains(item.transform.position), "Food must leave through the open doorway: " + item.name);
            Assert.IsTrue(item.GetComponent<KitchenStoredIngredient>().WasRetrieved);
        }
        IEnumerator CutWithKnife(GameObject knife, PreparedKitchenProp food)
        {
            var centre = food.GetComponent<Rigidbody>().worldCenterOfMass;
            var upright = Quaternion.Euler(90, 0, 0);
            yield return MoveHeld(right, knife, centre + Vector3.right * .055f + Vector3.up * .18f, upright, 28);
            for (var i = 0; i < 65 && !food.IsSplit; i++)
            {
                var actual = food.GetComponent<Rigidbody>().worldCenterOfMass;
                right.transform.position += new Vector3(Mathf.Clamp(actual.x + .055f - knife.transform.position.x, -.005f, .005f),
                    -.004f, Mathf.Clamp(actual.z - knife.transform.position.z, -.005f, .005f));
                yield return null;
                yield return new WaitForFixedUpdate();
            }
            Assert.IsTrue(food.IsSplit, "Held blade failed to cut " + food.name + "; knife=" + knife.transform.position +
                ", actual target=" + food.GetComponent<Rigidbody>().worldCenterOfMass + ", requested=" + knife.GetComponent<CollisionConstrainedGrabTransformer>().RequestedPose.position);
            yield return MoveHeld(right, knife, knife.transform.position + Vector3.up * .16f, upright, 16);
        }

        [UnityTest] public IEnumerator ClosedFridgeBlocksSelectionAndOpenDoorAllowsRetrieval()
        {
            yield return LoadKitchen(); var fridge = station.GetComponentInChildren<KitchenFridge>();
            var tomato = station.GetComponentsInChildren<CookableIngredient>().Single(f => f.IngredientKind == KitchenIngredientKind.Tomato);
            var grab = tomato.GetComponent<XRGrabInteractable>(); grab.interactionManager = manager;
            Assert.IsTrue(fridge.Contains(tomato.transform.position));
            Assert.IsFalse(manager.IsSelectPossible(left, grab), "A tracked hand inside a closed fridge cannot bypass the door.");
            yield return ToggleDoor(fridge); Assert.That(fridge.Door.Progress, Is.GreaterThan(.9));
            Capture("fridge-open", new Vector3(-1.87f, 1.6f, .50f), fridge.transform.position + Vector3.up * .95f);
            yield return Retrieve(left, tomato.gameObject, fridge);
            Drop(left, tomato.gameObject);
            station.GetComponent<KitchenCookingStation>().ResetStation(); yield return Frames(3);
            Assert.IsFalse(fridge.Accessible); Assert.IsTrue(fridge.Contains(tomato.transform.position), "After reset tomato=" + tomato.transform.position + ", selected=" + grab.isSelected);
            Assert.IsFalse(tomato.GetComponent<KitchenStoredIngredient>().WasRetrieved);
        }

        [UnityTest] public IEnumerator CookingRoomSupportsCookwareAndGasFlameFollowsSwitch()
        {
            yield return LoadKitchen();
            foreach (var grab in scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<XRGrabInteractable>()))
            {
                Assert.That(grab.transform.position.x, Is.InRange(-5.06f,-1.45f), "Outside kitchen: "+grab.name);
                Assert.That(grab.transform.position.z, Is.InRange(.60f,2.39f), "Outside kitchen: "+grab.name);
                Assert.That(grab.transform.position.y, Is.GreaterThan(.9f), "Unsupported: "+grab.name);
                Assert.IsNotNull(grab.GetComponent<CollisionConstrainedGrabTransformer>(), grab.name);
            }
            var heat = station.GetComponentInChildren<KitchenHeatSource>();
            var flame = station.GetComponentInChildren<KitchenBurnerFlame>();
            var pan = station.transform.Find("PREF_Cooking_FryingPan").GetComponent<KitchenFoodVessel>();
            Assert.IsFalse(heat.IsOn); Assert.IsFalse(flame.IsLit); Assert.AreEqual(0,flame.ParticleCount);
            var control = station.transform.Find("ACT_STOVE").GetComponent<XRSimpleInteractable>(); control.interactionManager = manager;
            manager.SelectEnter((IXRSelectInteractor)right,control); manager.SelectExit((IXRSelectInteractor)right,control);
            yield return new WaitForSeconds(.35f);
            Assert.IsTrue(flame.IsLit); Assert.That(flame.ParticleCount,Is.GreaterThan(10));
            Assert.IsFalse(heat.IsHeating(pan), "The burner starts empty; flames cannot depend on food contact.");
            Capture("gas-flame",heat.transform.position+new Vector3(.20f,.20f,-.32f),heat.transform.position-Vector3.up*.06f);
            Capture("kitchen-room-ready",new Vector3(-3.05f,1.9f,.60f),new Vector3(-3.4f,1.15f,2.05f));
            heat.SetOn(false); Assert.IsFalse(flame.IsLit); Assert.AreEqual(0,flame.ParticleCount);
            heat.SetOn(true); heat.enabled=false;
            Assert.IsFalse(flame.IsLit); Assert.IsFalse(heat.IsOn); Assert.AreEqual(0,flame.ParticleCount);
            heat.enabled=true; heat.SetOn(true);
            station.GetComponent<KitchenCookingStation>().ResetStation();
            Assert.IsFalse(flame.IsLit); Assert.IsFalse(heat.IsOn); Assert.AreEqual(0,flame.ParticleCount);
        }

        [UnityTest] public IEnumerator RealisticWaterCupCanBeHeldAndFilledAtTheKitchenTap()
        {
            yield return LoadKitchen();
            var roots=scene.GetRootGameObjects();
            var cup=roots.Single(g=>g.name=="PREF_Prop_Cup");
            var tap=roots.Single(g=>g.name=="PREF_Water_KitchenStation").GetComponentInChildren<WaterTap>();
            var water=cup.GetComponent<LiquidContainer>(); var before=water.Millilitres;
            var rest=new Pose(cup.transform.position,cup.transform.rotation);
            yield return Pick(left,cup);
            yield return MoveHeld(left,cup,cup.transform.position+Vector3.up*.3f,Quaternion.identity,25);
            // Approach below the spout from the aisle; a downward path through
            // the solid faucet is correctly rejected by held-prop sweeping.
            var fill=tap.OutletPosition-Vector3.up*.1f;
            var approach=fill+Vector3.forward*.25f;
            yield return MoveHeld(left,cup,approach+Vector3.up*.25f,Quaternion.identity,40);
            yield return MoveHeld(left,cup,approach,Quaternion.identity,25);
            yield return MoveHeld(left,cup,fill,Quaternion.identity,30);
            Assert.That(Vector3.Distance(cup.transform.position,fill),Is.LessThan(.02f),"Reach the spout from below: "+cup.transform.position);
            var control=tap.GetComponentInChildren<XRSimpleInteractable>();control.interactionManager=manager;
            manager.SelectEnter((IXRSelectInteractor)right,control);manager.SelectExit((IXRSelectInteractor)right,control);
            Assert.IsTrue(tap.Running); yield return Frames(60);
            manager.SelectEnter((IXRSelectInteractor)right,control);manager.SelectExit((IXRSelectInteractor)right,control);
            Assert.IsFalse(tap.Running); Assert.That(water.Millilitres,Is.GreaterThan(before+40),"The new mug must remain an open water receiver.");
            Directory.CreateDirectory("Logs/KitchenRoom");
            File.WriteAllText("Logs/KitchenRoom/water-scenario.json",JsonUtility.ToJson(new WaterReport {
                beforeMl=before,afterMl=water.Millilitres,filledWhileHeld=cup.GetComponent<XRGrabInteractable>().isSelected,
                cupPosition=cup.transform.position,outletPosition=tap.OutletPosition },true));
            Capture("kitchen-washing",tap.transform.position+new Vector3(.65f,.50f,-.35f),tap.transform.position+Vector3.up*.08f);
            yield return MoveHeld(left,cup,approach,Quaternion.identity,25);
            yield return MoveHeld(left,cup,approach+Vector3.up*.25f,Quaternion.identity,25);
            yield return MoveHeld(left,cup,rest.position+Vector3.up*.3f,rest.rotation,35);
            yield return MoveHeld(left,cup,rest.position,rest.rotation,25); yield return PlaceAndRelease(left,cup);
        }

        [UnityTest] public IEnumerator InitialCookwareAndIngredientsStaySupportedAndUpright()
        {
            yield return LoadKitchen(); yield return Frames(120);
            foreach (var body in station.GetComponentsInChildren<Rigidbody>())
                Assert.That(body.transform.position.y, Is.GreaterThan(.9f), "Unsupported initial placement: " + body.name + " at " + body.position);
            foreach (var vessel in station.GetComponentsInChildren<KitchenFoodVessel>())
                Assert.That(Vector3.Dot(vessel.transform.up, Vector3.up), Is.GreaterThan(.95f), "Initial vessel tipped: " + vessel.name);
        }

        [UnityTest] public IEnumerator SweptFridgeDoorStopsAtObstacleAndCanResume()
        {
            var fridge = Own(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RecipeFolder + "PREF_Cooking_Fridge.prefab"))).GetComponent<KitchenFridge>();
            fridge.transform.position = new Vector3(150, 50, 150); yield return null;
            var door = fridge.Door; var block = Own(GameObject.CreatePrimitive(PrimitiveType.Cube));
            block.transform.position = door.transform.position + Quaternion.Euler(0, 50, 0) * new Vector3(-.44f, .88f, .035f);
            block.transform.localScale = new Vector3(.12f, 1.55f, .12f); Physics.SyncTransforms();
            door.SetOpen(true); yield return Frames(70);
            Assert.That(door.Progress, Is.LessThan(.8), "The fixed hinge must stop before the obstruction.");
            Object.DestroyImmediate(block); Physics.SyncTransforms(); yield return Frames(75);
            Assert.That(door.Progress, Is.GreaterThan(.99));
        }

        [UnityTest] public IEnumerator PanRejectsUncutAndHeldTomatoAndColdStirringDoesNotCount()
        {
            yield return LoadKitchen();
            var whole = station.GetComponentsInChildren<CookableIngredient>().Single(f => f.IngredientKind == KitchenIngredientKind.Tomato);
            var pan = station.GetComponentInChildren<KitchenPanContents>();
            Assert.IsFalse(pan.TryReceive(whole));
            whole.Prepared.TryCut(1);
            var slice = whole.Prepared.Pieces[0].GetComponent<CookableIngredient>(); Assert.IsFalse(pan.TryReceive(slice));
            slice.Prepared.TryCut(1); var body = slice.Prepared.Pieces[0]; var dice = body.GetComponent<CookableIngredient>();
            body.useGravity = false;
            var centre = body.worldCenterOfMass - body.position;
            body.transform.position = pan.transform.position + Vector3.up * .028f - centre;
            body.position = body.transform.position;
            yield return Pick(left, dice.gameObject); Assert.IsFalse(pan.TryReceive(dice));
            Drop(left, dice.gameObject); yield return Frames(2);
            yield return null; yield return null;
            if (pan.CapturedCount == 0) Assert.IsTrue(pan.TryReceive(dice), "kinematic=" + body.isKinematic +
                ", held=" + dice.GetComponent<XRGrabInteractable>().isSelected + ", local centre=" +
                pan.transform.InverseTransformPoint(body.worldCenterOfMass) + ", kind=" + dice.IngredientKind + ", form=" + dice.CutForm + ", grams=" + dice.Grams);
            var grams = pan.TomatoGrams; Assert.IsFalse(pan.TryReceive(dice)); Assert.AreEqual(grams, pan.TomatoGrams);
            pan.Stir(1); Assert.AreEqual(0, pan.EggScrambleDistance); Assert.AreEqual(0, pan.StirFryDistance);
            Assert.IsFalse(station.GetComponent<KitchenRecipeController>().Recipe.Ready);
            station.GetComponent<KitchenCookingStation>().ResetStation();
        }

        [UnityTest] public IEnumerator CompleteRecipeUsesFridgeKnifeEggPourAndSpatulaAndCanReset()
        {
            yield return LoadKitchen();
            var fridge = station.GetComponentInChildren<KitchenFridge>();
            var flow = station.GetComponent<KitchenRecipeController>();
            var tomato = station.GetComponentsInChildren<CookableIngredient>().Single(f => f.IngredientKind == KitchenIngredientKind.Tomato).GetComponent<PreparedKitchenProp>();
            var bowl = station.transform.Find("PREF_Cooking_MixingBowl").GetComponent<KitchenFoodVessel>();
            var pan = station.transform.Find("PREF_Cooking_FryingPan").GetComponent<KitchenFoodVessel>();
            var contents = pan.GetComponent<KitchenPanContents>(); var heat = station.GetComponentInChildren<KitchenHeatSource>();
            var knife = station.GetComponentInChildren<PreparedKitchenKnife>().transform.root == station.transform ?
                station.GetComponentInChildren<PreparedKitchenKnife>().GetComponentInParent<XRGrabInteractable>().gameObject : null;
            Assert.IsNotNull(knife);
            var board = station.transform.Find("PREF_Cooking_ChoppingBoard").gameObject;
            var spatula = station.GetComponentInChildren<KitchenSpatula>();
            var spatulaRest = new Pose(spatula.transform.position, spatula.transform.rotation);
            var knifeRest = new Pose(knife.transform.position, knife.transform.rotation);
            var bowlRest = bowl.transform.position;
            var whiskObject = station.GetComponentInChildren<KitchenStirTool>().gameObject;
            var whiskRest = new Pose(whiskObject.transform.position, whiskObject.transform.rotation);
            var cutPosition = board.transform.position + Vector3.up * .029f;
            yield return ToggleDoor(fridge); yield return Retrieve(left, tomato.gameObject, fridge);
            yield return ToggleDoor(fridge);
            Assert.That(fridge.Door.Progress, Is.LessThan(.05), "Retrieved food must clear the closing leaf.");
            yield return MoveHeld(left, tomato.gameObject, cutPosition + Vector3.up * .25f, Quaternion.identity, 140);
            yield return MoveHeld(left, tomato.gameObject, cutPosition, Quaternion.identity, 35); yield return PlaceAndRelease(left, tomato.gameObject);
            Assert.That(Vector3.Distance(tomato.transform.position,cutPosition),Is.LessThan(.065f), "Tomato must reach the actual kitchen board.");
            yield return Frames(12); yield return Pick(right, knife); yield return CutWithKnife(knife, tomato);
            var slices = tomato.Pieces.Select(b => b.GetComponent<PreparedKitchenProp>()).ToArray();
            var prep = station.transform.Find("PREF_Cooking_TomatoBowl").gameObject;
            var prepRest = prep.transform.position;
            var dicedCount = 0;
            foreach (var slice in slices)
            {
                yield return Pick(left, slice.gameObject);
                yield return MoveHeld(left, slice.gameObject, slice.transform.position + Vector3.up * .25f, slice.transform.rotation, 24);
                var slicePlace = cutPosition + Vector3.forward * .13f;
                yield return MoveHeld(left, slice.gameObject, slicePlace + Vector3.up * .25f, Quaternion.identity, 25);
                yield return MoveHeld(left, slice.gameObject, slicePlace, Quaternion.identity, 20);
                yield return PlaceAndRelease(left, slice.gameObject); yield return Frames(6);
                yield return CutWithKnife(knife, slice);
                yield return MoveHeld(right, knife, board.transform.position + new Vector3(.32f, .4f, -.12f), Quaternion.Euler(90, 0, 0), 22);
                // Clear each chopped slice into the actual prep bowl, as a player
                // would, leaving room for the next knife stroke.
                foreach (var body in slice.Pieces)
                {
                    yield return Pick(left, body.gameObject);
                    yield return MoveHeld(left, body.gameObject, body.transform.position + Vector3.up * .18f, body.transform.rotation, 20);
                    var localCentre = body.centerOfMass;
                    var point = prep.transform.position + new Vector3((dicedCount % 2 == 0 ? -.02f : .02f), .055f,
                        ((dicedCount / 2) % 2 == 0 ? -.02f : .02f));
                    yield return MoveHeld(left, body.gameObject, point + Vector3.up * .18f - localCentre, Quaternion.identity, 30);
                    yield return MoveHeld(left, body.gameObject, point - localCentre, Quaternion.identity, 16);
                    yield return PlaceAndRelease(left, body.gameObject); dicedCount++;
                }
            }
            yield return MoveHeld(right, knife, knifeRest.position + Vector3.up * .25f, knifeRest.rotation, 24);
            yield return MoveHeld(right, knife, knifeRest.position, knifeRest.rotation, 24);
            yield return PlaceAndRelease(right, knife); yield return Frames(4);
            var diced = slices.SelectMany(cut => cut.Pieces).ToArray(); Assert.AreEqual(16, diced.Length);
            Assert.That(diced.Sum(body => body.GetComponent<CookableIngredient>().Grams), Is.EqualTo(120).Within(.02f));
            yield return Frames(20); Assert.AreEqual(TomatoEggStep.TakeEgg, flow.Recipe.Step);
            Capture("tomato-diced", board.transform.position + new Vector3(.12f,.7f,-.6f), board.transform.position);
            yield return ToggleDoor(fridge);
            var egg = station.GetComponentInChildren<KitchenEgg>(); yield return Retrieve(right, egg.gameObject, fridge);
            yield return ToggleDoor(fridge);
            yield return MoveHeld(right, egg.gameObject, bowl.transform.position + Vector3.up * .25f, Quaternion.identity, 140);
            yield return Pick(left, bowl.gameObject); yield return Frames(2);
            var eggAbove = bowl.transform.position + new Vector3(.06f, .16f, 0);
            yield return MoveHeld(right, egg.gameObject, eggAbove, Quaternion.identity, 25);
            // A real requested downward knock stops at the rim and opens the shell.
            for (var i = 0; i < 18 && !egg.GetComponent<PreparedKitchenProp>().IsSplit; i++)
            { right.transform.position += Vector3.down * .018f; yield return new WaitForFixedUpdate(); }
            Assert.IsTrue(egg.GetComponent<PreparedKitchenProp>().IsSplit, "Rim knock must crack the held egg.");
            Assert.That(bowl.Batch.Millilitres, Is.EqualTo(50).Within(.02f)); Drop(left, bowl.gameObject);
            var whisk = station.GetComponentInChildren<KitchenStirTool>().gameObject;
            yield return Pick(right, whisk);
            yield return MoveHeld(right, whisk, whisk.transform.position + Vector3.up * .35f, whisk.transform.rotation, 28);
            yield return MoveHeld(right, whisk, bowl.transform.position + Vector3.up * .25f, Quaternion.identity, 28);
            yield return MoveHeld(right, whisk, bowl.transform.position - Vector3.up * .005f, Quaternion.identity, 32);
            for (var i = 0; i < 170 && bowl.Batch.MixedFraction < .999f; i++)
            {
                var root = bowl.transform.position + new Vector3(Mathf.Cos(i * .3f) * .03f, -.005f, Mathf.Sin(i * .3f) * .03f);
                yield return MoveHeld(right, whisk, root, Quaternion.identity, 1);
            }
            Assert.AreEqual(CookingStage.Beaten, bowl.Stage, "mixed=" + bowl.Batch.MixedFraction +
                ", tip=" + whisk.transform.Find("StirTip").position + ", bowl=" + bowl.transform.position +
                ", whisk rotation=" + whisk.transform.eulerAngles + ", blocked=" + whisk.GetComponent<CollisionConstrainedGrabTransformer>().IsBlocked);
            yield return MoveHeld(right, whisk, whisk.transform.position + Vector3.up * .3f, Quaternion.identity, 24);
            yield return MoveHeld(right, whisk, whiskRest.position + Vector3.up * .3f, whiskRest.rotation, 24);
            yield return MoveHeld(right, whisk, whiskRest.position, whiskRest.rotation, 24);
            yield return PlaceAndRelease(right, whisk);
            var switchButton = station.transform.Find("ACT_STOVE").GetComponent<XRSimpleInteractable>(); switchButton.interactionManager = manager;
            manager.SelectEnter((IXRSelectInteractor)right, switchButton); manager.SelectExit((IXRSelectInteractor)right, switchButton);
            Assert.IsTrue(heat.IsOn);
            yield return Pick(left, pan.gameObject);
            yield return MoveHeld(left, pan.gameObject, pan.transform.position + Vector3.up * .25f, pan.transform.rotation, 24);
            yield return MoveHeld(left, pan.gameObject, heat.transform.position + Vector3.up * .08f, Quaternion.identity, 40);
            yield return MoveHeld(left, pan.gameObject, heat.transform.position - Vector3.up * .045f, Quaternion.identity, 25);
            yield return PlaceAndRelease(left, pan.gameObject); yield return Frames(15);
            Assert.IsTrue(heat.IsHeating(pan), "Place the pan on the actual hot surface. Pan=" + pan.transform.position +
                ", up=" + pan.transform.up + ", heat=" + heat.transform.position);
            yield return Pick(left, bowl.gameObject);
            yield return MoveHeld(left, bowl.gameObject, pan.transform.position + new Vector3(-.06f, .19f, 0), Quaternion.identity, 45);
            yield return MoveHeld(left, bowl.gameObject, pan.transform.position + new Vector3(-.06f, .19f, 0), Quaternion.Euler(0, 0, -95), 30);
            yield return Frames(75);
            Assert.That(pan.Batch.Millilitres, Is.GreaterThan(35));
            Assert.That(pan.Batch.Millilitres + bowl.Batch.Millilitres + bowl.SpilledMillilitres, Is.EqualTo(50).Within(.05f));
            yield return MoveHeld(left, bowl.gameObject, bowlRest + Vector3.up * .25f, Quaternion.identity, 35);
            yield return MoveHeld(left, bowl.gameObject, bowlRest + Vector3.up * .01f, Quaternion.identity, 24);
            yield return PlaceAndRelease(left, bowl.gameObject);
            for (var i = 0; i < 750 && pan.Stage != CookingStage.Cooked; i++) yield return new WaitForFixedUpdate();
            Assert.AreEqual(CookingStage.Cooked, pan.Stage); Assert.That(contents.CoagulatedFraction, Is.EqualTo(1).Within(.001f));
            yield return Pick(right, spatula.gameObject); yield return StirWithSpatula(spatula, pan, 160);
            Assert.That(contents.EggScrambleDistance, Is.GreaterThanOrEqualTo(.45f), "Accepted spatula motion must scramble the set egg.");
            yield return MoveHeld(right, spatula.gameObject, pan.transform.position + new Vector3(.27f, .3f, -.15f), Quaternion.Euler(-55, 0, 0), 24);
            yield return Pick(left, prep);
            if (prep.GetComponent<KitchenPrepTray>().CarriedCount < 12)
                foreach (var body in diced)
                    Debug.Log("RECIPE_PREP_DIAG local=" + prep.transform.InverseTransformPoint(body.transform.TransformPoint(body.centerOfMass)) +
                        " active=" + body.gameObject.activeInHierarchy + " held=" + body.GetComponent<XRGrabInteractable>().isSelected + " kine=" + body.isKinematic);
            Assert.That(prep.GetComponent<KitchenPrepTray>().CarriedCount, Is.GreaterThanOrEqualTo(12),
                "Tracked=" + prep.GetComponent<KitchenPrepTray>().TrackedFoodCount + ", up=" + prep.transform.up);
            var pour = pan.transform.position + new Vector3(-.06f, .22f, 0);
            yield return MoveHeld(left, prep, pour, Quaternion.identity, 65);
            yield return MoveHeld(left, prep, pour, Quaternion.Euler(0, 0, -95), 35);
            yield return Frames(75);
            Assert.That(contents.TomatoGrams, Is.GreaterThanOrEqualTo(90), "Tilting the real prep bowl must drop tomato through the pan mouth.");
            Assert.AreEqual(0, prep.GetComponent<KitchenPrepTray>().CarriedCount);
            yield return MoveHeld(left, prep, prepRest + Vector3.up * .28f, Quaternion.identity, 30);
            yield return MoveHeld(left, prep, prepRest + Vector3.up * .01f, Quaternion.identity, 24);
            yield return PlaceAndRelease(left, prep);
            for (var i = 0; i < 8 && !flow.Recipe.Ready; i++) yield return StirWithSpatula(spatula, pan, 100);
            Assert.IsTrue(flow.Recipe.Ready, "Recipe missing evidence: " + flow.Recipe.Actions + "; step=" + flow.Recipe.Step +
                "; egg=" + pan.Stage + "; tomato cooked=" + contents.TomatoCooked + "; stir=" + contents.StirFryDistance);
            heat.SetOn(false);
            yield return MoveHeld(right, spatula.gameObject, spatula.transform.position + Vector3.up * .25f, spatula.transform.rotation, 20);
            yield return MoveHeld(right, spatula.gameObject, spatulaRest.position + Vector3.up * .25f, spatulaRest.rotation, 24);
            yield return MoveHeld(right, spatula.gameObject, spatulaRest.position + Vector3.up * .01f, spatulaRest.rotation, 24);
            yield return PlaceAndRelease(right, spatula.gameObject);
            var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "XR Apartment.Cooking Presentation", 128);
            double meanPresentationMs = 0, maxPresentationMs = 0;
            var presentationSamples = 0;
            try
            {
                for (var frame = 0; frame < 70; frame++) yield return null;
                var samples = recorder.ToArray(); presentationSamples = samples.Length;
                foreach (var sample in samples) { meanPresentationMs += sample.Value / 1000000.0; maxPresentationMs = Math.Max(maxPresentationMs, sample.Value / 1000000.0); }
                if (samples.Length > 0) meanPresentationMs /= samples.Length;
                Debug.Log("RECIPE_PRESENTATION_PROFILE samples=" + samples.Length + " meanMs=" + meanPresentationMs + " maxMs=" + maxPresentationMs);
            }
            finally { recorder.Dispose(); }
            Capture("tomato-egg-finished", pan.transform.position + new Vector3(.14f, .43f, .31f), pan.transform.position + Vector3.up * .02f);
            Capture("kitchen-recipe-complete", new Vector3(-3.05f, 1.95f, .7f), new Vector3(-3.4f, 1.1f, 1.95f));
            File.WriteAllText("Logs/TomatoEggRecipe/recipe-scenario.json", JsonUtility.ToJson(new RecipeReport {
                actions = flow.Recipe.Actions.ToString(), completed = flow.Recipe.Ready, eggMl = pan.Batch.Millilitres,
                tomatoGrams = contents.TomatoGrams, scrambledMetres = contents.EggScrambleDistance,
                stirFryMetres = contents.StirFryDistance, capturedPieces = contents.CapturedCount,
                presentationMeanMs = meanPresentationMs, presentationMaxMs = maxPresentationMs, presentationSamples = presentationSamples }, true));
            pan.Heat(1, 100); yield return null; Assert.AreEqual(TomatoEggStep.Burnt, flow.Recipe.Step);
            station.GetComponent<KitchenCookingStation>().ResetStation(); yield return Frames(4);
            Assert.AreEqual(TomatoEggStep.OpenFridge, flow.Recipe.Step); Assert.IsFalse(fridge.Accessible);
            Assert.AreEqual(0, contents.CapturedCount); Assert.AreEqual(0, pan.Batch.Millilitres);
            foreach (var prop in station.GetComponentsInChildren<PreparedKitchenProp>(true)) Assert.IsFalse(prop.IsSplit);
            foreach (var item in station.GetComponentsInChildren<KitchenStoredIngredient>())
            { Assert.IsTrue(fridge.Contains(item.transform.position)); Assert.IsFalse(item.WasRetrieved); }
        }
        IEnumerator StirWithSpatula(KitchenSpatula spatula, KitchenFoodVessel pan, int frames)
        {
            var rotation = Quaternion.Euler(-55, 0, 0);
            var origin = pan.transform.position + Vector3.up * .049f;
            yield return MoveHeld(right, spatula.gameObject, origin, rotation, 25);
            for (var i = 0; i < frames; i++)
                yield return MoveHeld(right, spatula.gameObject, origin + new Vector3(Mathf.Cos(i * .24f) * .024f, 0, Mathf.Sin(i * .24f) * .024f), rotation, 1);
        }
        [Serializable] sealed class RecipeReport
        {
            public string actions; public bool completed; public float eggMl, tomatoGrams, scrambledMetres, stirFryMetres; public int capturedPieces;
            public double presentationMeanMs, presentationMaxMs; public int presentationSamples;
        }
        [Serializable] sealed class WaterReport
        {
            public float beforeMl,afterMl; public bool filledWhileHeld; public Vector3 cupPosition,outletPosition;
        }
        void Capture(string name, Vector3 position, Vector3 target)
        {
            Directory.CreateDirectory("Logs/TomatoEggRecipe");
            var go = Own(new GameObject("Recipe visual camera")); var camera = go.AddComponent<Camera>();
            camera.CopyFrom(sourceCamera); camera.stereoTargetEye = StereoTargetEyeMask.None;
            camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position));
            camera.nearClipPlane = .025f;
            var post = sourceCamera.GetComponent("PostProcessLayer"); if (post != null) EditorUtility.CopySerialized(post, go.AddComponent(post.GetType()));
            var rt = new RenderTexture(1200, 900, 24); var image = new Texture2D(1200, 900, TextureFormat.RGB24, false);
            var before = RenderTexture.active;
            try
            {
                camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
                image.ReadPixels(new Rect(0, 0, 1200, 900), 0, 0); image.Apply();
                File.WriteAllBytes("Logs/TomatoEggRecipe/" + name + ".png", image.EncodeToPNG());
            }
            finally { RenderTexture.active = before; camera.targetTexture = null; rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(image); go.SetActive(false); }
        }
    }
}
#endif
