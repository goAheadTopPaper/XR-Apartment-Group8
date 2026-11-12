#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using UnityEngine.XR.Interaction.Toolkit.UI;
using Unity.Profiling;
using XRApartment.Data;
using XRApartment.Runtime;
using XRApartment.Runtime.UI;
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
        KitchenMenuKeyboard menuKeyboard;
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
            menuKeyboard?.Dispose();
            menuKeyboard = null;
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
                        root.GetComponentInChildren<Collider>(true) != null ||
                        root.GetComponentInChildren<Light>(true) != null || root.GetComponentInChildren<UnityEngine.EventSystems.EventSystem>(true) != null))
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
            // XRI skips throw samples at very short frame times. Feed a stationary
            // hand across 0.4 seconds of 90 Hz frames before the real SelectExit.
            var originalDelta = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 90f;
            try
            {
                for (var frame = 0; frame < 36; frame++) yield return null;
                Drop(hand, target);
                yield return null;
                yield return null;
            }
            finally { Time.captureDeltaTime = originalDelta; }
        }
        IEnumerator ToggleDoor(KitchenFridge fridge, bool mayPause = false)
        {
            var angle = Mathf.DeltaAngle(0f, fridge.Door.transform.localEulerAngles.y);
            yield return PullDoor(fridge.Door, fridge.Accessible ? -angle : 105f - angle, mayPause);
        }

        IEnumerator PullDoor(KinematicSwingDoor hinge, float degrees, bool mayPause = false)
        {
            var door = hinge.GetComponent<XRSimpleInteractable>();
            door.interactionManager = manager;
            var hand = right.hasSelection ? left : right;
            var pivot = hinge.transform.position;
            var radial = hinge.PullHandle.position - pivot;
            hand.transform.position = hinge.PullHandle.position;
            var originalDelta = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 90f;
            try
            {
                manager.SelectEnter((IXRSelectInteractor)hand, door);
                Assert.IsTrue(door.isSelected);
                var before = hinge.Progress;
                yield return null;
                Assert.That(hinge.Progress, Is.EqualTo(before).Within(.002), "Selection by itself must leave the door in place.");
                for (var frame = 1; frame <= 48; frame++)
                {
                    hand.transform.position = pivot + Quaternion.AngleAxis(degrees * frame / 48f, hinge.transform.parent.up) * radial;
                    yield return null;
                }
                manager.SelectExit((IXRSelectInteractor)hand, door);
                if (mayPause)
                    for (var frame = 0; frame < 6; frame++) yield return null;
                else yield return Frames(6);
            }
            finally { Time.captureDeltaTime = originalDelta; }
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
        IEnumerator CutWithKnife(GameObject knife, PreparedKitchenProp food, float bladeOffset = .055f)
        {
            for (var cut = food.CutCount; cut < food.RequiredCuts; cut++)
                yield return CutOnceWithKnife(knife, food, bladeOffset);
        }

        IEnumerator CutOnceWithKnife(GameObject knife, PreparedKitchenProp food, float bladeOffset = .055f)
        {
            var cuts = food.CutCount;
            var body = food.GetComponent<Rigidbody>();
            var upright = Quaternion.Euler(90, 0, 0);
            var stroke = body.worldCenterOfMass + Vector3.right * bladeOffset + Vector3.up * .18f;
            yield return MoveHeld(right, knife, stroke, upright, 28);
            var attach = knife.GetComponent<XRGrabInteractable>().GetAttachTransform(right);
            var grip = knife.transform.InverseTransformPoint(attach.position);
            var gripOffset = upright * Vector3.Scale(grip, knife.transform.lossyScale);
            for (var i = 0; i < 65 && food.CutCount == cuts; i++)
            {
                // Anchor each input sample to the food. Accumulating correction from
                // the stopped solid blade can steer the tracked wrist off the board.
                var centre = body.worldCenterOfMass;
                stroke = new Vector3(centre.x + bladeOffset, stroke.y - .004f, centre.z);
                right.transform.position = stroke + gripOffset;
                yield return null;
                yield return new WaitForFixedUpdate();
            }
            Assert.AreEqual(cuts + 1, food.CutCount, "Held blade failed to cut " + food.name + "; knife=" + knife.transform.position +
                ", actual target=" + food.GetComponent<Rigidbody>().worldCenterOfMass + ", requested=" + knife.GetComponent<CollisionConstrainedGrabTransformer>().RequestedPose.position);
            yield return MoveHeld(right, knife, knife.transform.position + Vector3.up * .16f, upright, 16);
        }

        [UnityTest] public IEnumerator ResetDuringFastHeldMotionLeavesToolsAtRest()
        {
            yield return LoadKitchen();
            var reset = station.GetComponent<KitchenCookingStation>();
            var targets = new[]
            {
                station.GetComponentsInChildren<KitchenFoodVessel>().Single(v => v.name.Contains("FryingPan")).gameObject,
                station.GetComponentInChildren<KitchenSpatula>().gameObject,
                scene.GetRootGameObjects().Single(go => go.name.Contains("Cup") && go.GetComponent<LiquidContainer>() != null)
            };
            var originalDelta = Time.captureDeltaTime;
            try
            {
                Time.captureDeltaTime = 1f / 90f;
                foreach (var releaseBeforeReset in new[] { false, true })
                foreach (var target in targets)
                {
                    var rest = target.transform.position;
                    var body = target.GetComponent<Rigidbody>();
                    var grab = target.GetComponent<XRGrabInteractable>();
                    var throws = grab.throwOnDetach;
                    yield return Pick(left, target);
                    yield return MoveHeld(left, target, rest + Vector3.up * .4f, target.transform.rotation, 30);
                    for (var frame = 0; frame < 6; frame++)
                    {
                        left.transform.position += Vector3.up * .035f;
                        yield return null;
                    }
                    Assert.IsTrue(grab.isSelected);
                    if (releaseBeforeReset) Drop(left, target);
                    reset.ResetStation();
                    reset.ResetStation();
                    for (var frame = 0; frame < 12; frame++)
                    {
                        yield return null;
                        yield return new WaitForFixedUpdate();
                        Assert.That(Vector3.Distance(body.position, rest), Is.LessThan(.075f),
                            target.name + " moved away from its reset pose.");
                        Assert.That(body.velocity.magnitude, Is.LessThan(1.5f),
                            target.name + " retained a throw after reset.");
                    }
                    Assert.IsFalse(grab.isSelected);
                    Assert.AreEqual(throws, grab.throwOnDetach);
                    yield return Pick(left, target);
                    yield return MoveHeld(left, target, rest + Vector3.up * .4f, target.transform.rotation, 30);
                    for (var frame = 0; frame < 6; frame++)
                    {
                        left.transform.position += Vector3.up * .035f;
                        yield return null;
                    }
                    Drop(left, target);
                    yield return null;
                    yield return new WaitForFixedUpdate();
                    if (throws)
                        Assert.That(body.velocity.y, Is.GreaterThan(.3f),
                            target.name + " must retain normal throwing after reset.");
                    reset.ResetStation();
                    yield return null;
                    yield return null;
                    yield return null;
                }
            }
            finally { Time.captureDeltaTime = originalDelta; }
        }

        [UnityTest] public IEnumerator CounterCutDoesNotLaunchPiecesAndRoomResetClearsDetachedFood()
        {
            yield return LoadKitchen();
            var tomato = station.GetComponentsInChildren<CookableIngredient>()
                .Single(food => food.IngredientKind == KitchenIngredientKind.Tomato).GetComponent<PreparedKitchenProp>();
            var board = station.transform.Find("PREF_Cooking_ChoppingBoard");
            var surface = board.GetComponent<Collider>();
            var body = tomato.GetComponent<Rigidbody>();
            var centre = surface.bounds.center;
            tomato.transform.SetPositionAndRotation(
                new Vector3(centre.x, surface.bounds.max.y + .055f, centre.z) - body.centerOfMass,
                Quaternion.identity);
            body.position = tomato.transform.position;
            body.velocity = body.angularVelocity = Vector3.zero;
            Physics.SyncTransforms();
            yield return Frames(40);
            var knife = station.GetComponentInChildren<PreparedKitchenKnife>().GetComponentInParent<XRGrabInteractable>().gameObject;
            yield return Pick(right, knife);
            yield return CutOnceWithKnife(knife, tomato);
            Assert.AreEqual(1, tomato.Pieces.Count(piece => piece.gameObject.activeInHierarchy));
            Assert.IsFalse(tomato.IsSplit);
            var remainder = tomato.transform.Find("RemainingSlices");
            var firstRemainder = remainder.GetComponentsInChildren<MeshRenderer>().Single();
            Assert.AreEqual("VIS_Remainder_01", firstRemainder.GetComponent<MeshFilter>().sharedMesh.name);
            Assert.IsTrue(firstRemainder.sharedMaterials.Any(material => material.name.Contains("TomatoFleshDetailed")));
            var peakSpeed = 0f;
            for (var frame = 0; frame < 100; frame++)
            {
                yield return null;
                yield return new WaitForFixedUpdate();
                foreach (var piece in tomato.Pieces)
                {
                    if (!piece.gameObject.activeInHierarchy) continue;
                    peakSpeed = Mathf.Max(peakSpeed, piece.velocity.magnitude);
                    Assert.That(Vector3.Distance(piece.worldCenterOfMass, centre), Is.LessThan(.65f),
                        piece.name + " escaped the chopping area after a cut.");
                    Assert.That(piece.velocity.magnitude, Is.LessThan(2f),
                        piece.name + " was launched by a normal cut.");
                }
            }
            Assert.AreEqual(1, tomato.CutCount, "Holding the knife in place must not cut a second slice.");
            yield return CutOnceWithKnife(knife, tomato);
            Assert.AreEqual(2, tomato.Pieces.Count(piece => piece.gameObject.activeInHierarchy));
            Assert.AreEqual("VIS_Remainder_02", remainder.GetComponentsInChildren<MeshFilter>().Single().sharedMesh.name);
            yield return CutOnceWithKnife(knife, tomato);
            Assert.AreEqual(3, tomato.CutCount);
            Assert.AreEqual(4, tomato.Pieces.Count(piece => piece.gameObject.activeInHierarchy));
            Assert.IsEmpty(remainder.GetComponentsInChildren<MeshRenderer>());
            Debug.Log("CUT_STABILITY peak=" + peakSpeed);
            station.GetComponent<KitchenCookingStation>().ResetStation();
            yield return null;
            yield return null;
            yield return null;
            Assert.IsFalse(tomato.IsSplit);
            foreach (var piece in tomato.Pieces)
            {
                Assert.IsFalse(piece.gameObject.activeInHierarchy);
                Assert.IsTrue(piece.isKinematic);
            }
        }

        [UnityTest] public IEnumerator PullingTheFridgeHandleKeepsPartialOpening()
        {
            yield return LoadKitchen();
            var hinge = station.GetComponentInChildren<KitchenFridge>().Door;
            yield return PullDoor(hinge, 32f);
            var angle = Mathf.DeltaAngle(0f, hinge.transform.localEulerAngles.y);
            Assert.That(angle, Is.EqualTo(32f).Within(2f));
            yield return Frames(40);
            Assert.That(Mathf.DeltaAngle(0f, hinge.transform.localEulerAngles.y), Is.EqualTo(angle).Within(.1f));
            yield return PullDoor(hinge, -angle);
            Assert.That(hinge.Progress, Is.LessThan(.03));
        }

        [UnityTest] public IEnumerator ClosedFridgeBlocksSelectionAndOpenDoorAllowsRetrieval()
        {
            yield return LoadKitchen(); var fridge = station.GetComponentInChildren<KitchenFridge>();
            var tomato = station.GetComponentsInChildren<CookableIngredient>().Single(f => f.IngredientKind == KitchenIngredientKind.Tomato);
            var grab = tomato.GetComponent<XRGrabInteractable>(); grab.interactionManager = manager;
            Assert.IsTrue(fridge.Contains(tomato.transform.position));
            Assert.IsFalse(manager.IsSelectPossible(left, grab), "A tracked hand inside a closed fridge cannot bypass the door.");
            yield return ToggleDoor(fridge); Assert.That(fridge.Door.Progress, Is.GreaterThan(.9));
            var fridgeView = fridge.transform.TransformPoint(new Vector3(-.23f, 1.65f, 1.95f));
            Capture("fridge-open", fridgeView, fridge.transform.position + Vector3.up * 1.22f);
            yield return Retrieve(left, tomato.gameObject, fridge);
            Drop(left, tomato.gameObject);
            station.GetComponent<KitchenCookingStation>().ResetStation(); yield return Frames(3);
            Assert.IsFalse(fridge.Accessible); Assert.IsTrue(fridge.Contains(tomato.transform.position), "After reset tomato=" + tomato.transform.position + ", selected=" + grab.isSelected);
            Assert.IsFalse(tomato.GetComponent<KitchenStoredIngredient>().WasRetrieved);
        }

        [UnityTest] public IEnumerator ChangingFridgeModelRebindsRecipeAndStock()
        {
            yield return LoadKitchen();
            var lab = scene.GetRootGameObjects().Select(root => root.GetComponent<KitchenLayoutLab>()).Single(item => item != null);
            var workbench = station.GetComponent<KitchenCookingStation>();
            var recipe = station.GetComponent<KitchenRecipeController>().Recipe;
            var tomato = station.GetComponentsInChildren<CookableIngredient>().Single(food => food.IngredientKind == KitchenIngredientKind.Tomato);
            var grab = tomato.GetComponent<XRGrabInteractable>(); grab.interactionManager = manager;
            var oldFridge = workbench.ActiveFridge;
            Assert.IsTrue(lab.TrySelectFridge(true, out var status), status);
            yield return Frames(32);
            var compact = workbench.ActiveFridge;
            Assert.AreNotSame(oldFridge, compact);
            foreach (var stock in station.GetComponentsInChildren<KitchenStoredIngredient>()) AssertOnKitchenSupport(stock.transform);
            Assert.AreEqual(TomatoEggStep.OpenFridge, recipe.Step);
            Assert.IsFalse(manager.IsSelectPossible(left, grab), "The compact door must guard the same stock.");
            yield return ToggleDoor(compact);
            Assert.IsTrue(compact.Accessible);
            Assert.AreEqual(TomatoEggStep.TakeTomato, recipe.Step);
            yield return Retrieve(left, tomato.gameObject, compact);
            Assert.AreEqual(TomatoEggStep.SliceTomato, recipe.Step);

            workbench.ResetStation();
            yield return Frames(10);
            Assert.AreSame(compact, workbench.ActiveFridge);
            Assert.IsTrue(compact.Contains(tomato.transform.position));
            Assert.IsFalse(grab.isSelected);
            Assert.IsFalse(manager.IsSelectPossible(left, grab));
            Assert.AreEqual(TomatoEggStep.OpenFridge, recipe.Step);
        }

        [UnityTest] public IEnumerator CookingRoomSupportsCookwareAndGasFlameFollowsSwitch()
        {
            yield return LoadKitchen();
            foreach (var grab in scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<XRGrabInteractable>()))
            {
                Assert.That(grab.transform.position.x, Is.InRange(-5.06f,-1.45f), "Outside kitchen: "+grab.name);
                Assert.That(grab.transform.position.z, Is.InRange(.60f,2.39f), "Outside kitchen: "+grab.name);
                AssertOnKitchenSupport(grab.transform);
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
                AssertOnKitchenSupport(body.transform);
            foreach (var vessel in station.GetComponentsInChildren<KitchenFoodVessel>())
                Assert.That(Vector3.Dot(vessel.transform.up, Vector3.up), Is.GreaterThan(.95f), "Initial vessel tipped: " + vessel.name);
        }

        void AssertOnKitchenSupport(Transform target)
        {
            KitchenCookingTests.AssertOnKitchenSupport(target, station.GetComponent<KitchenCookingStation>().ActiveFridge);
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

        [UnityTest] public IEnumerator BeefOnionRecipeUsesGrabsCutsHeatStirAndServing()
        {
            yield return LoadKitchen();
            var workbench = station.GetComponent<KitchenCookingStation>();
            var flow = station.GetComponent<KitchenRecipeController>();
            var heading = station.transform.Find("UI_CookingInstructions/Heading").GetComponent<TMP_Text>();
            var fridgeDetail = station.transform.Find("UI_FridgeInstructions/Detail").GetComponent<TMP_Text>();
            var fridge = workbench.ActiveFridge;
            var beef = station.transform.Find("STOCK_Beef").GetComponent<CookableIngredient>();
            var onion = station.transform.Find("STOCK_Onion").GetComponent<CookableIngredient>();
            var pan = station.transform.Find("PREF_Cooking_FryingPan").GetComponent<KitchenFoodVessel>();
            var contents = pan.GetComponent<KitchenPanContents>();
            var stove = station.GetComponentInChildren<KitchenHeatSource>();
            var board = station.transform.Find("PREF_Cooking_ChoppingBoard");
            var plate = station.transform.Find("PREF_Prop_plate/Intact");
            var knife = station.GetComponentInChildren<PreparedKitchenKnife>().GetComponentInParent<XRGrabInteractable>();
            var knifeRest = new Pose(knife.transform.position, knife.transform.rotation);
            var spatula = station.GetComponentInChildren<KitchenSpatula>();

            SelectKitchenControl("ACT_RECIPE");
            yield return Frames(5);
            Assert.AreEqual(KitchenRecipeChoice.BeefOnion, flow.ActiveRecipe);
            Assert.AreEqual(BeefOnionStep.TakeIngredients, flow.BeefStep);
            AssertOnKitchenSupport(beef.transform);
            AssertOnKitchenSupport(onion.transform);
            Assert.IsFalse(contents.TryReceive(beef));
            Assert.IsFalse(contents.TryReceive(onion));

            var panBody = pan.GetComponent<Rigidbody>();
            // This fixture holds the pan at the physical burner position for repeatable ingredient entry.
            // The tomato-and-egg test also places the pan using the XRI grip.
            panBody.isKinematic = true;
            var burner = station.transform.Find("LOC_BurnerSurface").position;
            pan.transform.SetPositionAndRotation(burner, Quaternion.identity);
            panBody.position = burner;
            panBody.rotation = Quaternion.identity;
            Physics.SyncTransforms();
            var cut = board.position + new Vector3(0f, .029f, .09f);
            yield return TakeAndCut(fridge, beef, knife, knifeRest, cut, .055f);
            var strips = beef.Prepared.Pieces.ToArray();
            Assert.AreEqual(4, strips.Length);
            yield return ReleaseIntoPan(strips, pan, contents);
            Assert.AreEqual(BeefOnionStep.TakeIngredients, flow.BeefStep, "Beef alone cannot satisfy the recipe.");
            yield return new WaitForSecondsRealtime(.22f);
            Assert.AreEqual("For this recipe: an onion.", fridgeDetail.text);

            yield return TakeAndCut(fridge, onion, knife, knifeRest, cut, .043f);
            var quarters = onion.Prepared.Pieces.ToArray();
            Assert.AreEqual(4, quarters.Length);
            Assert.AreEqual(BeefOnionStep.AddToPan, flow.BeefStep);
            yield return ReleaseIntoPan(quarters, pan, contents);
            Assert.AreEqual(8, contents.CapturedCount);
            Assert.That(contents.BeefGrams, Is.EqualTo(150f).Within(.01f));
            Assert.That(contents.OnionGrams, Is.EqualTo(80f).Within(.01f));
            Assert.AreEqual(CookingStage.Raw, contents.BeefStage);
            Assert.AreEqual(CookingStage.Raw, contents.OnionStage);
            Assert.AreEqual(BeefOnionStep.CookAndStir, flow.BeefStep);

            SelectKitchenControl("ACT_STOVE");
            yield return Frames(3);
            Assert.IsTrue(stove.IsHeating(pan));
            Assert.IsTrue(stove.GetComponent<AudioSource>().isPlaying);
            for (var i = 0; i < 700 && contents.OnionStage != CookingStage.Cooked; i++) yield return new WaitForFixedUpdate();
            Assert.AreEqual(CookingStage.Cooked, contents.BeefStage);
            Assert.AreEqual(CookingStage.Cooked, contents.OnionStage);
            Assert.AreEqual(BeefOnionStep.CookAndStir, flow.BeefStep, "Heat alone cannot replace spatula motion.");
            var pieces = strips.Concat(quarters).Select(piece => piece.GetComponent<CookableIngredient>()).ToArray();
            var doses = pieces.Select(piece => piece.HeatDose).ToArray();
            // Exercise the burnt guard with the actual captured dish, then restore the measured doses for serving.
            pan.Heat(1f, 100f);
            Assert.AreEqual(BeefOnionStep.Burnt, flow.BeefStep);
            Assert.IsFalse(contents.BeefOnionPlated);
            for (var index = 0; index < pieces.Length; index++)
            {
                pieces[index].ResetCooking();
                pieces[index].Heat(1f, doses[index]);
            }
            Assert.AreEqual(BeefOnionStep.CookAndStir, flow.BeefStep);
            var beforeStir = strips.Select(strip => strip.transform.position).ToArray();
            yield return Pick(right, spatula.gameObject);
            for (var stroke = 0; stroke < 6 && flow.BeefStep == BeefOnionStep.CookAndStir; stroke++)
                yield return StirWithSpatula(spatula, pan, 90);
            Assert.IsTrue(strips.Where((strip, index) => Vector3.Distance(strip.transform.position, beforeStir[index]) > .005f).Any());
            Assert.AreEqual(BeefOnionStep.TurnOffStove, flow.BeefStep, "Stir measured " + contents.BeefOnionStirDistance);
            yield return MoveHeld(right, spatula.gameObject, pan.transform.position + new Vector3(.24f, .24f, -.18f), spatula.transform.rotation, 26);
            Drop(right, spatula.gameObject);
            SelectKitchenControl("ACT_STOVE");
            Assert.AreEqual(BeefOnionStep.Plate, flow.BeefStep);

            panBody.isKinematic = false;
            yield return Pick(left, pan.gameObject);
            yield return MoveHeld(left, pan.gameObject, pan.transform.position + Vector3.up * .32f, Quaternion.identity, 30);
            var pour = plate.position + new Vector3(-.073f, .20f, 0f);
            yield return MoveHeld(left, pan.gameObject, pour + Vector3.up * .14f, Quaternion.identity, 85);
            yield return MoveHeld(left, pan.gameObject, pour, Quaternion.identity, 26);
            Assert.IsFalse(contents.BeefOnionPlated, "A level pan must not serve the dish.");
            var tilted = Quaternion.Euler(0, 0, -65f);
            var wrongAim = pour + Vector3.back * .22f;
            yield return MoveHeld(left, pan.gameObject, wrongAim + Vector3.up * .14f, tilted, 35);
            yield return MoveHeld(left, pan.gameObject, wrongAim, tilted, 25);
            Assert.IsFalse(contents.BeefOnionPlated, "A tilt away from the plate must not serve the dish.");
            yield return MoveHeld(left, pan.gameObject, wrongAim + Vector3.up * .14f, tilted, 25);
            yield return MoveHeld(left, pan.gameObject, pour + Vector3.up * .14f, tilted, 35);
            yield return MoveHeld(left, pan.gameObject, pour, tilted, 26);
            yield return new WaitForSeconds(.35f);
            Assert.AreEqual(BeefOnionStep.Served, flow.BeefStep,
                "Pan " + pan.transform.position + " up " + pan.transform.up + " plate " + plate.position);
            Assert.That(heading.text, Does.Contain("SERVED"));
            Assert.AreEqual(0, contents.CapturedCount);
            Assert.IsTrue(strips.Concat(quarters).All(piece => piece.transform.parent == plate));
            yield return MoveHeld(left, pan.gameObject, pan.transform.position + new Vector3(-.24f, .28f, 0f),
                Quaternion.identity, 30);
            Capture("beef-onion-served", plate.position + new Vector3(.14f, .37f, .26f), plate.position);
            var plateObject = plate.parent.gameObject;
            yield return Pick(right, plateObject);
            var previousPlateHeight = plate.position.y;
            yield return MoveHeld(right, plateObject, plateObject.transform.position + Vector3.up * .18f,
                plateObject.transform.rotation, 30);
            Assert.That(plate.position.y, Is.GreaterThan(previousPlateHeight + .13f));
            Assert.IsTrue(strips.Concat(quarters).All(piece => piece.transform.parent == plate));

            workbench.ResetStation();
            yield return Frames(8);
            for (var frame = 0; frame < 4 && workbench.IsResetting; frame++) yield return null;
            Assert.IsFalse(workbench.IsResetting);
            Assert.AreEqual(KitchenRecipeChoice.BeefOnion, flow.ActiveRecipe);
            Assert.AreEqual(BeefOnionStep.TakeIngredients, flow.BeefStep);
            Assert.IsFalse(contents.BeefOnionPlated);
            Assert.IsFalse(beef.Prepared.IsSplit);
            Assert.IsFalse(onion.Prepared.IsSplit);
            Assert.IsTrue(fridge.Contains(beef.transform.position) && fridge.Contains(onion.transform.position));
            Assert.IsFalse(stove.IsOn);

            SelectKitchenControl("ACT_RECIPE");
            yield return Frames(6);
            Assert.AreEqual(KitchenRecipeChoice.TomatoEgg, flow.ActiveRecipe);
            Assert.AreEqual(TomatoEggStep.OpenFridge, flow.Recipe.Step);
        }

        void SelectKitchenControl(string name)
        {
            var control = station.transform.Find(name).GetComponent<XRSimpleInteractable>();
            control.interactionManager = manager;
            manager.SelectEnter((IXRSelectInteractor)right, control);
            manager.SelectExit((IXRSelectInteractor)right, control);
        }

        IEnumerator TakeAndCut(KitchenFridge fridge, CookableIngredient food, XRGrabInteractable knife,
            Pose knifeRest, Vector3 cut, float bladeOffset)
        {
            yield return ToggleDoor(fridge);
            yield return Retrieve(left, food.gameObject, fridge);
            yield return ToggleDoor(fridge);
            yield return MoveHeld(left, food.gameObject, cut + Vector3.up * .25f, Quaternion.identity, 120);
            yield return MoveHeld(left, food.gameObject, cut, Quaternion.identity, 25);
            yield return PlaceAndRelease(left, food.gameObject);
            Assert.That(Vector3.Distance(food.transform.position, cut), Is.LessThan(.06f), food.name);
            yield return Pick(right, knife.gameObject);
            yield return CutWithKnife(knife.gameObject, food.Prepared, bladeOffset);
            yield return MoveHeld(right, knife.gameObject, knifeRest.position + Vector3.up * .28f, knifeRest.rotation, 30);
            yield return MoveHeld(right, knife.gameObject, knifeRest.position + Vector3.up * .008f, knifeRest.rotation, 30);
            yield return PlaceAndRelease(right, knife.gameObject);
        }

        IEnumerator ReleaseIntoPan(Rigidbody[] pieces, KitchenFoodVessel pan, KitchenPanContents contents)
        {
            foreach (var piece in pieces)
            {
                yield return Pick(left, piece.gameObject);
                yield return MoveHeld(left, piece.gameObject, piece.position + Vector3.up * .18f, piece.rotation, 22);
                var centre = pan.transform.position - piece.centerOfMass;
                yield return MoveHeld(left, piece.gameObject, centre + Vector3.up * .16f, Quaternion.identity, 36);
                yield return MoveHeld(left, piece.gameObject, centre + Vector3.up * .045f, Quaternion.identity, 24);
                Assert.IsFalse(contents.TryReceive(piece.GetComponent<CookableIngredient>()), "Held food cannot enter the pan.");
                yield return PlaceAndRelease(left, piece.gameObject);
                yield return Frames(18);
                Assert.AreEqual(pan.transform, piece.transform.parent,
                    piece.name + " missed the pan mouth at " + pan.transform.InverseTransformPoint(piece.worldCenterOfMass));
            }
        }

        [UnityTest] public IEnumerator CompleteRecipeUsesFridgeKnifeEggPourAndSpatulaAndCanReset()
        {
            yield return LoadKitchen();
            yield return StartGuidedRecipe();
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
            var knifePark = board.transform.position + new Vector3(.32f, .4f, -.12f);
            var cuttingGrip = Quaternion.Euler(90, 0, 0);
            yield return MoveHeld(right, knife, knifePark, cuttingGrip, 22);
            var slices = tomato.Pieces.Select(b => b.GetComponent<PreparedKitchenProp>()).ToArray();
            var prep = station.transform.Find("PREF_Cooking_TomatoBowl").gameObject;
            var prepRest = prep.transform.position;
            var boardSurface = board.GetComponent<Collider>();
            var dicedCount = 0;
            foreach (var slice in slices)
            {
                var sliceBody = slice.GetComponent<Rigidbody>();
                yield return Pick(left, slice.gameObject);
                yield return MoveHeld(left, slice.gameObject, slice.transform.position + Vector3.up * .25f, slice.transform.rotation, 24);
                // The slices keep the original tomato pivot. Place the actual
                // centre of mass clear of the pile and inside the board edge.
                var centreOffset = Vector3.Scale(sliceBody.centerOfMass, slice.transform.lossyScale);
                var slicePlace = cutPosition + new Vector3(-.115f, .06f, -.025f) - Vector3.ProjectOnPlane(centreOffset, Vector3.up);
                yield return MoveHeld(left, slice.gameObject, slicePlace + Vector3.up * .25f, Quaternion.identity, 25);
                yield return MoveHeld(left, slice.gameObject, slicePlace, Quaternion.identity, 20);
                var heldCentre = sliceBody.worldCenterOfMass;
                yield return PlaceAndRelease(left, slice.gameObject); yield return Frames(20);
                var centre = sliceBody.worldCenterOfMass;
                Assert.IsTrue(boardSurface.Raycast(new Ray(centre + Vector3.up * .025f, Vector3.down), out _, .125f),
                    $"{slice.name} must settle on the chopping board before cutting. Centre {centre:F3}, " +
                    $"held centre {heldCentre:F3}, requested pivot {slicePlace:F3}, board {boardSurface.bounds}.");
                yield return CutWithKnife(knife, slice);
                yield return MoveHeld(right, knife, knifePark, cuttingGrip, 22);
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
            yield return new WaitForSecondsRealtime(.22f);
            var heading = station.transform.Find("UI_CookingInstructions/Heading").GetComponent<TMP_Text>();
            var detail = station.transform.Find("UI_CookingInstructions/Detail").GetComponent<TMP_Text>();
            Assert.That(heading.text, Does.Contain("READY"));
            Assert.That(detail.text, Does.Contain("50 mL egg").And.Contain($"{contents.TomatoGrams:0} g tomato"));
            var menu = EnableKitchenMenu();
            var guide = menu.transform.Find("PREF_UI_KitchenActivities/Guide");
            yield return null;
            yield return null;
            Assert.IsTrue(guide.gameObject.activeInHierarchy, "A finished meal with the stove off should open the next guide step.");
            Assert.AreEqual("Preview fridges", ButtonText(guide.Find("Continue")));
            yield return Click(menu, guide.Find("Continue"));
            Assert.AreEqual("Compare the fridges", guide.Find("Title").GetComponent<TMP_Text>().text);
            menu.Close();
            SetKitchenRigActive(false);
            File.WriteAllText("Logs/TomatoEggRecipe/recipe-scenario.json", JsonUtility.ToJson(new RecipeReport {
                actions = flow.Recipe.Actions.ToString(), completed = flow.Recipe.Ready, eggMl = pan.Batch.Millilitres,
                tomatoGrams = contents.TomatoGrams, scrambledMetres = contents.EggScrambleDistance,
                stirFryMetres = contents.StirFryDistance, capturedPieces = contents.CapturedCount,
                presentationMeanMs = meanPresentationMs, presentationMaxMs = maxPresentationMs, presentationSamples = presentationSamples }, true));
            pan.Heat(1, 100); yield return null; Assert.AreEqual(TomatoEggStep.Burnt, flow.Recipe.Step);
            yield return new WaitForSecondsRealtime(.22f);
            Assert.That(heading.text, Does.Contain("BURNT"));
            station.GetComponent<KitchenCookingStation>().ResetStation(); yield return Frames(4);
            Assert.AreEqual(TomatoEggStep.OpenFridge, flow.Recipe.Step); Assert.IsFalse(fridge.Accessible);
            Assert.AreEqual(0, contents.CapturedCount); Assert.AreEqual(0, pan.Batch.Millilitres);
            foreach (var prop in station.GetComponentsInChildren<PreparedKitchenProp>(true)) Assert.IsFalse(prop.IsSplit);
            foreach (var item in station.GetComponentsInChildren<KitchenStoredIngredient>())
            { Assert.IsTrue(fridge.Contains(item.transform.position)); Assert.IsFalse(item.WasRetrieved); }
            yield return PreviewFridgesInGuide();
            yield return SaveGuidedFeedbackAndStartFreePlay();
        }

        IEnumerator StartGuidedRecipe()
        {
            menuKeyboard = new KitchenMenuKeyboard();
            var menu = EnableKitchenMenu();
            var movement = menu.GetComponentInParent<KitchenLocomotionSettings>();
            var guide = menu.transform.Find("PREF_UI_KitchenActivities/Guide");
            yield return null;
            yield return null;
            Assert.IsTrue(guide.gameObject.activeInHierarchy);
            Assert.AreEqual(0f, Time.timeScale);
            var startScene = SceneManager.GetActiveScene();
            var startPosition = movement.Origin.Origin.transform.position;
            yield return Click(menu, guide.Find("Posture/Seated"));
            yield return Click(menu, guide.Find("Posture/Raise"));
            yield return Click(menu, guide.Find("Continue"));
            Assert.That(movement.Origin.CameraInOriginSpaceHeight, Is.EqualTo(1.25f).Within(.015f));
            Assert.AreEqual(startPosition, movement.Origin.Origin.transform.position);
            Capture("guide-calibration", sourceCamera.transform.position, guide.position);
            yield return Click(menu, guide.Find("Continue"));
            Assert.IsFalse(menu.IsOpen);
            Assert.AreEqual("Practice controls", guide.Find("Title").GetComponent<TMP_Text>().text);
            Assert.IsFalse(guide.Find("Continue").GetComponent<Button>().interactable);

            var landing = new Vector3(-3.5f, .004f, 1.12f);
            Assert.IsTrue(movement.TeleportProvider.QueueTeleportRequest(new TeleportRequest
            {
                destinationPosition = landing,
                matchOrientation = MatchOrientation.None,
                requestTime = Time.time
            }));
            for (var frame = 0; frame < 30 && Vector3.Distance(movement.Origin.Origin.transform.position, landing) > .03f; frame++)
                yield return null;
            Assert.That(Vector3.Distance(movement.Origin.Origin.transform.position, landing), Is.LessThan(.03f));
            Assert.IsFalse(guide.Find("Continue").GetComponent<Button>().interactable);

            var cup = scene.GetRootGameObjects().Single(root => root.name == "PREF_Prop_Cup");
            yield return Pick(left, cup);
            yield return MoveHeld(left, cup, cup.transform.position + Vector3.up * .12f, cup.transform.rotation, 15);
            yield return PlaceAndRelease(left, cup);
            Assert.IsTrue(guide.gameObject.activeInHierarchy);
            yield return Click(menu, guide.Find("Continue"));
            Assert.AreEqual("Cook tomato and egg", guide.Find("Title").GetComponent<TMP_Text>().text);
            Assert.IsFalse(guide.Find("Continue").GetComponent<Button>().interactable);
            Assert.AreEqual(startScene, SceneManager.GetActiveScene());
            station.GetComponent<KitchenCookingStation>().ResetStation();
            yield return Frames(8);
            SetKitchenRigActive(false);
        }

        IEnumerator PreviewFridgesInGuide()
        {
            var menu = EnableKitchenMenu();
            yield return OpenGuide(menu);
            var guide = menu.transform.Find("PREF_UI_KitchenActivities/Guide");
            yield return Click(menu, guide.Find("Continue"));
            var labMenu = menu.GetComponentInChildren<KitchenLayoutMenu>().transform;
            yield return Click(menu, labMenu.Find("PantryReturn"));
            yield return Click(menu, labMenu.Find("CloseLayoutLab"));
            yield return Frames(5);
            var kitchen = station.GetComponent<KitchenCookingStation>();
            yield return ToggleDoor(kitchen.ActiveFridge);
            yield return OpenGuide(menu);
            Assert.That(guide.Find("Instruction").GetComponent<TMP_Text>().text, Does.Contain("Choose Compact"));
            yield return Click(menu, guide.Find("Continue"));
            yield return Click(menu, labMenu.Find("CompactFridge"));
            yield return Click(menu, labMenu.Find("CloseLayoutLab"));
            yield return Frames(5);
            yield return ToggleDoor(kitchen.ActiveFridge, mayPause: true);
            Assert.IsTrue(guide.gameObject.activeInHierarchy);
            Assert.AreEqual("Leave feedback", ButtonText(guide.Find("Continue")));
            Assert.That(guide.Find("Detail").GetComponent<TMP_Text>().text, Does.Contain("°").And.Not.Contain("try opening"));
            yield return Click(menu, guide.Find("Continue"));
            Assert.AreEqual("Feedback note", ButtonText(guide.Find("Continue")));
        }

        IEnumerator SaveGuidedFeedbackAndStartFreePlay()
        {
            var menu = EnableKitchenMenu();
            var guide = menu.GetComponentInChildren<KitchenActivityGuide>();
            var panel = guide.transform.Find("Guide");
            Assert.IsFalse(guide.IsFreePlay);
            yield return Click(menu, panel.Find("Continue"));
            var note = LooseNote();
            var counter = scene.GetRootGameObjects().Single(root => root.name == "PREF_Kitchen_Environment")
                .transform.Find("SM_KitchenTable").GetComponent<MeshCollider>();
            var above = new Vector3(-1.87f, 1.6f, 1.98f);
            Assert.IsTrue(counter.Raycast(new Ray(above, Vector3.down), out var surface, 1f));
            yield return PinNote(note, above, surface);
            var form = guide.transform.Find("FeedbackEditor/Form");
            form.Find("FeedbackText").GetComponent<TMP_InputField>().text = "The counter is easy to reach.";
            var path = KitchenFeedbackNotes.DocumentPath;
            var backup = File.Exists(path) ? File.ReadAllBytes(path) : null;
            try
            {
                yield return Click(menu, form.Find("SaveFeedback"));
                Assert.That(File.ReadAllText(path), Does.Contain("The counter is easy to reach."));
                Assert.IsTrue(panel.gameObject.activeInHierarchy);
                Assert.AreEqual("Start free play", ButtonText(panel.Find("Continue")));
                Assert.AreEqual(0f, Time.timeScale);
                yield return Click(menu, panel.Find("Continue"));
                Assert.IsTrue(guide.IsFreePlay);
                Assert.IsFalse(menu.IsOpen);
                Assert.AreEqual(1f, Time.timeScale);
                yield return menuKeyboard.Hold();
                var pause = guide.transform.Find("PauseMenu");
                Assert.IsTrue(pause.gameObject.activeInHierarchy);
                Assert.IsTrue(pause.Find("Navigation/LayoutLab").GetComponent<Button>().interactable);
                Assert.IsTrue(pause.Find("Navigation/FeedbackNote").GetComponent<Button>().interactable);
                Capture("pause-free-play", sourceCamera.transform.position, pause.position);
                yield return Click(menu, pause.Find("Navigation/Resume"));
                Assert.IsFalse(menu.IsOpen);
            }
            finally
            {
                if (backup != null) File.WriteAllBytes(path, backup);
                else if (File.Exists(path)) File.Delete(path);
            }
        }

        [UnityTest] public IEnumerator FeedbackNotesFollowTheirObjectsAndAppendUnicodeTextToTheDocument()
        {
            yield return LoadKitchen();
            menuKeyboard = new KitchenMenuKeyboard();
            var menu = EnableKitchenMenu();
            KitchenSceneTestSetup.DisableStartupGuide(menu);
            var kitchen = station.GetComponent<KitchenCookingStation>();
            var lab = scene.GetRootGameObjects().Select(root => root.GetComponent<KitchenLayoutLab>()).Single(item => item != null);
            Assert.IsTrue(lab.TrySelectFridge(true, out var status), status);
            yield return Frames(6);
            var fridge = kitchen.ActiveFridge;
            var editor = menu.transform.Find("PREF_UI_KitchenActivities/FeedbackEditor");
            var form = editor.Find("Form");
            var field = form.Find("FeedbackText").GetComponent<TMP_InputField>();
            var file = KitchenFeedbackNotes.DocumentPath;
            var backup = File.Exists(file) ? File.ReadAllBytes(file) : null;
            var originalLength = backup == null ? 0 : File.ReadAllText(file).Length;
            const string text = "门把手需要更好握住。\n试开后仍容易拿到食材。";
            try
            {
                yield return SpawnFeedbackNote(menu);
                var note = LooseNote();
                yield return Pick(left, note.gameObject);
                Drop(left, note.gameObject);
                yield return null;
                Assert.IsFalse(editor.gameObject.activeSelf, "Releasing in mid-air must not open a note editor.");
                yield return SpawnFeedbackNote(menu);
                Assert.AreSame(note, LooseNote(), "Reopening a loose draft should recall the same note.");

                var outside = fridge.transform.TransformPoint(new Vector3(0f, 1.25f, 1f));
                var ray = new Ray(outside, -fridge.transform.forward);
                var face = fridge.Door.GetComponentsInChildren<Collider>()
                    .Select(collider => collider.enabled && !collider.isTrigger && collider.Raycast(ray, out var hit, 2f)
                        ? hit : default)
                    .Where(hit => hit.collider != null).OrderBy(hit => hit.distance).First();
                yield return PinNote(note, outside, face);
                Assert.IsTrue(editor.gameObject.activeSelf);
                Assert.That(form.Find("Target").GetComponent<TMP_Text>().text, Is.EqualTo("Compact fridge door"));
                Assert.IsFalse(form.Find("SaveFeedback").GetComponent<Button>().interactable);
                yield return Click(menu, form.Find("WriteInVR"));
                var keyboard = editor.Find("FeedbackKeyboard");
                yield return Click(menu, keyboard.Find("Key O"));
                yield return Click(menu, keyboard.Find("Key K"));
                yield return Click(menu, keyboard.Find("Done"));
                Assert.AreEqual("ok", field.text);
                field.text = text;
                Capture("feedback-note-editor", sourceCamera.transform.position, editor.position);
                yield return Click(menu, form.Find("CloseFeedback"));
                yield return SpawnFeedbackNote(menu);
                Assert.AreEqual(text, field.text, "Closing the editor must keep the note draft.");
                yield return Click(menu, form.Find("SaveFeedback"));
                Assert.That(form.Find("SaveStatus").GetComponent<TMP_Text>().text, Does.Contain("Saved"));
                yield return Click(menu, form.Find("CloseFeedback"));
                var onDoor = note.transform.localPosition;
                yield return ToggleDoor(fridge);
                Assert.That(fridge.Door.Progress, Is.GreaterThan(.9f));
                Assert.IsTrue(note.transform.IsChildOf(fridge.Door.transform));
                Assert.AreEqual(onDoor, note.transform.localPosition);

                var counter = scene.GetRootGameObjects().Single(root => root.name == "PREF_Kitchen_Environment")
                    .transform.Find("SM_KitchenTable").GetComponent<MeshCollider>();
                var above = new Vector3(-1.87f, 1.6f, 1.98f);
                Assert.IsTrue(counter.Raycast(new Ray(above, Vector3.down), out var worktop, 1f));
                yield return SpawnFeedbackNote(menu);
                var second = LooseNote();
                yield return PinNote(second, above, worktop);
                Assert.AreEqual("Kitchen counter", form.Find("Target").GetComponent<TMP_Text>().text);
                field.text = "The work surface is easy to reach.";
                yield return Click(menu, form.Find("SaveFeedback"));
                var saved = File.ReadAllText(file).Substring(originalLength);
                Assert.That(saved, Does.Contain("Compact fridge door (fridge-compact-door)").And.Contain(text)
                    .And.Contain("Kitchen counter (counter)").And.Contain(field.text)
                    .And.Contain("Fridge: Compact fridge").And.Contain("Opposite the sink"));
                Directory.CreateDirectory("Logs/GuidedKitchen");
                File.WriteAllText("Logs/GuidedKitchen/feedback-test.txt", saved);
                yield return Click(menu, form.Find("CloseFeedback"));
                kitchen.ResetStation();
                yield return Frames(8);
                Assert.IsTrue(note.transform.IsChildOf(fridge.Door.transform));
                Assert.IsTrue(second.transform.IsChildOf(counter.transform));
            }
            finally
            {
                if (backup != null) File.WriteAllBytes(file, backup);
                else if (File.Exists(file)) File.Delete(file);
            }
        }

        LocomotionSettingsView EnableKitchenMenu()
        {
            SetKitchenRigActive(true);
            return sourceCamera.transform.root.GetComponentInChildren<LocomotionSettingsView>();
        }

        void SetKitchenRigActive(bool active)
        {
            var fallbackListener = manager.GetComponent<AudioListener>();
            if (fallbackListener != null) fallbackListener.enabled = !active;
            sourceCamera.transform.root.gameObject.SetActive(active);
        }

        IEnumerator OpenGuide(LocomotionSettingsView menu)
        {
            yield return menuKeyboard.Hold();
            yield return Click(menu, menu.transform.Find("PREF_UI_KitchenActivities/PauseMenu/Navigation/KitchenGuide"));
        }

        IEnumerator SpawnFeedbackNote(LocomotionSettingsView menu)
        {
            yield return menuKeyboard.Hold();
            yield return Click(menu, menu.transform.Find("PREF_UI_KitchenActivities/PauseMenu/Navigation/FeedbackNote"));
        }

        KitchenFeedbackNote LooseNote() => scene.GetRootGameObjects()
            .Select(root => root.GetComponent<KitchenFeedbackNote>()).Single(note => note != null);

        IEnumerator PinNote(KitchenFeedbackNote note, Vector3 approach, RaycastHit surface)
        {
            yield return Pick(left, note.gameObject);
            var up = Mathf.Abs(Vector3.Dot(surface.normal, Vector3.up)) > .95f ? Vector3.forward : Vector3.up;
            var rotation = Quaternion.LookRotation(-surface.normal, up);
            yield return MoveHeld(left, note.gameObject, approach, rotation, 45);
            yield return MoveHeld(left, note.gameObject, surface.point + surface.normal * .026f, rotation, 35);
            Drop(left, note.gameObject);
            yield return null;
            yield return null;
            Assert.IsFalse(note.GetComponent<XRGrabInteractable>().enabled, $"Note failed to attach at {note.transform.position} near {surface.point}.");
        }

        static string ButtonText(Transform button) => button.GetComponentInChildren<TMP_Text>().text;

        IEnumerator Click(LocomotionSettingsView menu, Transform button)
        {
            yield return null;
            Assert.IsNotNull(button);
            Assert.IsTrue(button.GetComponent<Button>().interactable, button.name);
            Canvas.ForceUpdateCanvases();
            var origin = sourceCamera.transform.position;
            var aim = button.position;
            var pointer = new TrackedDeviceEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                layerMask = -1,
                rayPoints = new List<Vector3> { origin, aim + (aim - origin).normalized * .2f }
            };
            var hits = new List<RaycastResult>();
            menu.GetComponent<TrackedDeviceGraphicRaycaster>().Raycast(pointer, hits);
            Assert.IsTrue(hits.Any(hit => hit.gameObject == button.gameObject), button.name + " must be reachable by tracked rays. Graphic depth " +
                button.GetComponent<Image>().depth + ", hits " + string.Join(", ", hits.Select(hit => hit.gameObject.name)));
            ExecuteEvents.Execute<IPointerClickHandler>(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
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
