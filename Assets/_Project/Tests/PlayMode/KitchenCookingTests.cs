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
    public sealed class KitchenCookingTests
    {
        readonly List<GameObject> owned = new List<GameObject>();
        readonly List<GameObject> hiddenSceneRoots = new List<GameObject>();
        readonly Vector3 origin = new Vector3(150, 50, 150);
        XRInteractionManager manager;
        DrivenGrabInteractor hand;
        Scene cookingScene, previousScene;
        const string Cooking = "Assets/_Project/Prefabs/Cooking/";

        [SetUp]
        public void SetUp()
        {
            manager = Own(new GameObject("Cooking test manager")).AddComponent<XRInteractionManager>();
            if (!Object.FindObjectsOfType<AudioListener>().Any(listener => listener.isActiveAndEnabled)) manager.gameObject.AddComponent<AudioListener>();
            hand = Own(new GameObject("Cooking test hand")).AddComponent<DrivenGrabInteractor>();
            hand.interactionManager = manager; hand.keepSelectedTargetValid = true;
        }
        [TearDown]
        public void Cleanup()
        {
            for (var i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Object.DestroyImmediate(owned[i]);
            owned.Clear();
        }
        [UnityTearDown]
        public IEnumerator CloseCookingScene()
        {
            if (cookingScene.IsValid() && cookingScene.isLoaded)
            {
                if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
                yield return SceneManager.UnloadSceneAsync(cookingScene);
            }
            foreach (var root in hiddenSceneRoots) if (root != null) root.SetActive(true);
            hiddenSceneRoots.Clear();
        }
        GameObject Own(GameObject go) { owned.Add(go); return go; }
        GameObject Load(string path, Vector3 offset)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(source, "Production prefab missing: " + path);
            var go = Own(Object.Instantiate(source)); go.transform.position = origin + offset;
            var body = go.GetComponent<Rigidbody>(); if (body != null) body.useGravity = false;
            var grab = go.GetComponent<XRGrabInteractable>(); if (grab != null) grab.interactionManager = manager;
            Physics.SyncTransforms(); return go;
        }
        GameObject Prop(string name, Vector3 offset) => Load("Assets/_Project/Prefabs/Generated/" + name + "_v001/PREF_Prop_" + name + ".prefab", offset);

        [UnityTest]
        public IEnumerator RealPlateFallsBreaksAndResetReusesEightDerivedPieces()
        {
            var table = Own(GameObject.CreatePrimitive(PrimitiveType.Cube)); table.transform.position = origin;
            table.transform.localScale = new Vector3(2, .05f, 2);
            var plate = Prop("plate", Vector3.up * .5f); plate.GetComponent<Rigidbody>().useGravity = true;
            var prop = plate.GetComponent<PreparedKitchenProp>();
            var events = 0; prop.Split += _ => events++;
            for (var i = 0; i < 70 && !prop.IsSplit; i++) yield return new WaitForFixedUpdate();
            Assert.IsTrue(prop.IsSplit, "An actual falling Comfy-generated plate must break through OnCollisionEnter.");
            Assert.AreEqual(1, events);
            prop.ResetProp();
            Assert.AreEqual(8, plate.transform.Find("DerivedPieces").childCount);
            Assert.IsFalse(prop.IsSplit);
        }

        [UnityTest]
        public IEnumerator GeneratedFoodCutsKeepHeatAndPiecesRemainGrabbable()
        {
            foreach (var name in new[] { "tomato", "carrot", "apple", "lemon" })
            {
                var go = Prop(name, Vector3.up); var food = go.GetComponent<CookableIngredient>();
                food.Heat(1, 13); Assert.AreEqual(CookingStage.Cooked, food.Stage);
                var prop = go.GetComponent<PreparedKitchenProp>(); Assert.IsTrue(prop.TryCut(1));
                yield return null;
                var fragmentRoot = FindDetachedGroup(go);
                foreach (Transform piece in fragmentRoot)
                {
                    Assert.AreEqual(CookingStage.Cooked, piece.GetComponent<CookableIngredient>().Stage);
                    Assert.IsNotNull(piece.GetComponent<CollisionConstrainedGrabTransformer>());
                    Assert.IsTrue(piece.GetComponent<XRGrabInteractable>().enabled);
                }
                var first = fragmentRoot.GetChild(0).GetComponent<XRGrabInteractable>();
                first.GetComponent<Rigidbody>().useGravity = false; hand.transform.position = first.transform.position;
                yield return null;
                manager.SelectEnter((IXRSelectInteractor)hand, first); Assert.IsTrue(first.isSelected, "A cut " + name + " piece must use its intact parent's interaction manager.");
                manager.SelectExit((IXRSelectInteractor)hand, first);
                prop.ResetProp(); food.ResetCooking(); Assert.AreEqual(CookingStage.Raw, food.Stage);
                Object.DestroyImmediate(go); owned.Remove(go);
            }
        }

        Transform FindDetachedGroup(GameObject original)
        {
            // The object owns exactly one newly detached group after a split.
            foreach (var food in Object.FindObjectsOfType<CookableIngredient>())
                if (food.transform.parent != null && food.transform.parent.name == "DerivedPieces" &&
                    food.transform.parent.parent == null && Vector3.Distance(food.transform.position, original.transform.position) < 1)
                    return food.transform.parent;
            Assert.Fail("No active derived-food group."); return null;
        }

        [UnityTest]
        public IEnumerator EggCracksIntoActualBowlWhisksTransfersAndFryingCanBurn()
        {
            var bowl = Load(Cooking + "PREF_Cooking_MixingBowl.prefab", Vector3.zero).GetComponent<KitchenFoodVessel>();
            bowl.GetComponent<Rigidbody>().isKinematic = true;
            var egg = Prop("egg", Vector3.up * .12f);
            Assert.AreSame(bowl, KitchenFoodVessel.ReceiverBelow(egg.transform.position, .2f));
            Assert.IsTrue(egg.GetComponent<PreparedKitchenProp>().TryCrack());
            Assert.That(egg.GetComponent<KitchenEgg>().LastReceivedMillilitres, Is.EqualTo(50).Within(.001f));
            Assert.AreEqual(CookingStage.Raw, bowl.Stage);
            // Transfer is verified; loose shells must not obstruct the independent whisking fixture.
            egg.SetActive(false);
            var whisk = Load(Cooking + "PREF_Cooking_Whisk.prefab", Vector3.up * -.005f);
            var grab = whisk.GetComponent<XRGrabInteractable>();
            var grip = grab.attachTransform;
            var gripOffset = grip.position - whisk.transform.position;
            hand.transform.SetPositionAndRotation(grip.position, grip.rotation);
            yield return null; manager.SelectEnter((IXRSelectInteractor)hand, grab);
            for (var i = 0; i < 240 && bowl.Stage != CookingStage.Beaten; i++)
            {
                var angle = i * .35f;
                hand.transform.position = origin + new Vector3(Mathf.Cos(angle) * .035f, -.005f, Mathf.Sin(angle) * .035f) + gripOffset;
                yield return null;
                yield return new WaitForFixedUpdate();
            }
            Assert.AreEqual(CookingStage.Beaten, bowl.Stage, "Actual held whisk-tip movement should mix the batch. Mixed=" +
                bowl.Batch.MixedFraction + ", tip=" + whisk.transform.Find("StirTip").position + ", blocked=" +
                whisk.GetComponent<CollisionConstrainedGrabTransformer>().IsBlocked);
            manager.SelectExit((IXRSelectInteractor)hand, grab);
            var pan = Load(Cooking + "PREF_Cooking_FryingPan.prefab", new Vector3(.4f, 0, 0)).GetComponent<KitchenFoodVessel>();
            Assert.That(bowl.Batch.TransferTo(pan.Batch, 50), Is.EqualTo(50).Within(.001f));
            Assert.AreEqual(CookingStage.Empty, bowl.Stage);
            pan.Heat(1, 13); Assert.AreEqual(CookingStage.Cooked, pan.Stage);
            yield return new WaitForFixedUpdate();
            Assert.AreEqual(13, pan.Batch.Cooking.Dose, "No heat source means no added heat.");
            pan.Heat(1, 20); Assert.AreEqual(CookingStage.Burnt, pan.Stage);
            Assert.AreEqual(0, pan.Batch.TransferTo(bowl.Batch, 50), "Cooked egg is solid.");
            pan.ResetFood(); Assert.AreEqual(CookingStage.Empty, pan.Stage);
        }

        [UnityTest]
        public IEnumerator TiltedBowlPoursAlongGravityArcIntoPanWithoutCreatingVolume()
        {
            var pan = Load(Cooking + "PREF_Cooking_FryingPan.prefab", Vector3.zero).GetComponent<KitchenFoodVessel>();
            var bowl = Load(Cooking + "PREF_Cooking_MixingBowl.prefab", new Vector3(-.06f, .19f, 0)).GetComponent<KitchenFoodVessel>();
            bowl.ReceiveEgg(100); bowl.Stir(1);
            bowl.transform.rotation = Quaternion.Euler(0, 0, -95);
            Physics.SyncTransforms();
            for (var i = 0; i < 80; i++) yield return new WaitForFixedUpdate();
            Assert.That(pan.Batch.Millilitres, Is.GreaterThan(80), "Tilt must route liquid across the pan mouth.");
            Assert.That(bowl.Batch.Millilitres + pan.Batch.Millilitres + bowl.SpilledMillilitres, Is.EqualTo(100).Within(.01f));
        }

        [UnityTest]
        public IEnumerator AuthoredKitchenStaysSupportedAcceptsActivatedEggAndResets()
        {
            previousScene = SceneManager.GetActiveScene();
            // Batch tests may start with another authored kitchen still loaded.
            // Isolate its geometry so duplicate colliders cannot support this one,
            // and duplicate labels/props cannot contaminate its visual evidence.
            for (var i = 0; i < SceneManager.sceneCount; i++)
                foreach (var root in SceneManager.GetSceneAt(i).GetRootGameObjects())
                    if (root.activeSelf && (root.GetComponentInChildren<Renderer>(true) != null ||
                                            root.GetComponentInChildren<Collider>(true) != null ||
                        root.GetComponentInChildren<Light>(true) != null ||
                                            root.GetComponentInChildren<UnityEngine.EventSystems.EventSystem>(true) != null))
                    { hiddenSceneRoots.Add(root); root.SetActive(false); }
            cookingScene = EditorSceneManager.LoadSceneInPlayMode("Assets/_Project/Scenes/SCN_Kitchen_Cooking.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            for (var frame = 0; frame < 300 && !cookingScene.isLoaded; frame++) yield return null;
            Assert.IsTrue(cookingScene.isLoaded, "Cooking scene did not finish loading.");
            SceneManager.SetActiveScene(cookingScene);
            Assert.AreEqual(1, Object.FindObjectsOfType<KitchenCookingStation>().Length,
                "Scene checks must use exactly one active cooking station.");
            var roots = cookingScene.GetRootGameObjects();
            var rig = System.Array.Find(roots, go => go.name == "PREF_XR_KitchenRig");
            var sourceCamera = rig.GetComponentInChildren<Camera>(true);
            rig.SetActive(false);
            if (!Object.FindObjectsOfType<AudioListener>().Any(listener => listener.isActiveAndEnabled))
                manager.gameObject.AddComponent<AudioListener>();
            var station = System.Array.Find(roots, go => go.name == "PREF_Cooking_Workbench");
            var fridge = station.GetComponent<KitchenCookingStation>().ActiveFridge;
            var bowl = station.transform.Find("PREF_Cooking_MixingBowl").GetComponent<KitchenFoodVessel>();
            for (var i = 0; i < 80; i++) yield return new WaitForFixedUpdate();
            foreach (var prop in station.GetComponentsInChildren<PreparedKitchenProp>())
            {
                Assert.IsFalse(prop.IsSplit, "Initial placement must not break " + prop.name);
                AssertOnKitchenSupport(prop.transform, fridge);
            }
            foreach (var vessel in station.GetComponentsInChildren<KitchenFoodVessel>())
            {
                Assert.That(vessel.transform.position.y, Is.GreaterThan(.9f), "Vessel fell off the authored table: " + vessel.name);
                Assert.That(Vector3.Dot(vessel.transform.up, Vector3.up), Is.GreaterThan(.9f), "Vessel tipped at its initial placement: " + vessel.name);
            }
            var egg = station.GetComponentInChildren<KitchenEgg>();
            egg.transform.SetPositionAndRotation(bowl.transform.position + Vector3.up * .14f, Quaternion.identity);
            // Registration may span a slow batch frame. The fixture waits for the
            // driven hand, rather than dropping the egg before it is selected.
            egg.GetComponent<Rigidbody>().useGravity = false;
            var grab = egg.GetComponent<XRGrabInteractable>(); grab.interactionManager = manager;
            hand.transform.position = egg.transform.position; Physics.SyncTransforms();
            yield return null; manager.SelectEnter((IXRSelectInteractor)hand, grab);
            Assert.IsTrue(grab.isSelected, "The driven hand must actually hold the egg before activating it.");
            Assert.AreSame(bowl, KitchenFoodVessel.ReceiverBelow(egg.transform.position, .2f),
                "Egg=" + egg.transform.position + ", bowl=" + bowl.transform.position + ", bowl up=" + bowl.transform.up);
            grab.activated.Invoke(new ActivateEventArgs { interactableObject = grab });
            Assert.IsTrue(egg.GetComponent<PreparedKitchenProp>().IsSplit);
            Assert.That(bowl.Batch.Millilitres, Is.EqualTo(50).Within(.01f));
            for (var i = 0; i < 35; i++) yield return new WaitForFixedUpdate();
            Assert.That(bowl.Batch.Millilitres, Is.EqualTo(50).Within(.1f), "A supported receiving bowl should retain its egg.");
            CaptureCookingScene(sourceCamera, bowl.transform.position);
            station.GetComponent<KitchenCookingStation>().ResetStation();
            Assert.AreEqual(CookingStage.Empty, bowl.Stage);
            foreach (var prop in station.GetComponentsInChildren<PreparedKitchenProp>()) Assert.IsFalse(prop.IsSplit);
        }

        internal static void AssertOnKitchenSupport(Transform target, KitchenFridge fridge)
        {
            var position = target.position;
            if (target.GetComponent<KitchenStoredIngredient>() == null)
            {
                Assert.That(position.y, Is.GreaterThan(.9f), "Unsupported initial placement: " + target.name + " at " + position);
                return;
            }
            var supportRay = new Ray(position + Vector3.up * .04f, Vector3.down);
            Assert.IsTrue(fridge.Contains(position) && fridge.GetComponentsInChildren<BoxCollider>()
                .Any(part => part.name.StartsWith("VIS_Shelf_", System.StringComparison.Ordinal) &&
                    part.Raycast(supportRay, out _, .09f)),
                "Fridge stock must rest above an actual shelf: " + target.name + " at " + position);
        }

        void CaptureCookingScene(Camera source, Vector3 target)
        {
            var go = Own(new GameObject("Cooking visual QA"));
            manager.transform.position = target;
            var camera = go.AddComponent<Camera>(); camera.CopyFrom(source); camera.stereoTargetEye = StereoTargetEyeMask.None;
            camera.transform.position = new Vector3(-2.6f, 2.1f, -.1f); camera.transform.LookAt(target);
            var targetTexture = new RenderTexture(1200, 900, 24);
            var texture = new Texture2D(1200, 900, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = targetTexture; camera.Render(); RenderTexture.active = targetTexture;
                texture.ReadPixels(new Rect(0, 0, 1200, 900), 0, 0); texture.Apply();
                System.IO.Directory.CreateDirectory("Logs/KitchenAssetPipeline");
                System.IO.File.WriteAllBytes("Logs/KitchenAssetPipeline/cooking-play-scene.png", texture.EncodeToPNG());
                foreach (var probe in Object.FindObjectsOfType<ReflectionProbe>())
                    Debug.Log("COOKING_REFLECTION " + probe.name + " / " + AssetDatabase.GetAssetPath(probe.bakedTexture));
            }
            finally
            {
                RenderTexture.active = previous; camera.targetTexture = null; targetTexture.Release();
                Object.DestroyImmediate(targetTexture); Object.DestroyImmediate(texture);
            }
        }

        [UnityTest]
        public IEnumerator CookingToolsAlignAuthoredGripForEitherHand()
        {
            const string prefabs = "Assets/_Project/Prefabs/";
            var tools = new[]
            {
                (path: "Generated/knife_v001/PREF_Prop_knife.prefab", axis: Vector3.forward, direction: Vector3.down),
                (path: "Cooking/PREF_Cooking_FryingPan.prefab", axis: Vector3.up, direction: Vector3.up),
                (path: "Cooking/Recipe/PREF_Cooking_FryingPan.prefab", axis: Vector3.up, direction: Vector3.up),
                (path: "Cooking/PREF_Cooking_WaterPot.prefab", axis: Vector3.up, direction: Vector3.up),
                (path: "Cooking/Recipe/PREF_Cooking_Spatula.prefab", axis: Vector3.back, direction: Vector3.forward),
                (path: "Cooking/PREF_Cooking_Whisk.prefab", axis: Vector3.down, direction: new Vector3(0f, -.866f, .5f))
            };
            foreach (var (path, axis, direction) in tools)
            {
                var tool = Load(prefabs + path, Vector3.zero);
                var body = tool.GetComponent<Rigidbody>();
                var grab = tool.GetComponent<XRGrabInteractable>();
                var guard = tool.GetComponent<CollisionConstrainedGrabTransformer>();
                Assert.IsNotNull(grab.attachTransform, path);
                body.interpolation = RigidbodyInterpolation.None;
                var grips = new List<Transform> { grab.attachTransform };
                if (path.EndsWith("WaterPot.prefab", System.StringComparison.Ordinal))
                {
                    var other = tool.transform.Find("ATT_AlternateGrip");
                    Assert.IsNotNull(other, "Both visible stockpot handles must be grippable.");
                    grips.Add(other);
                }

                foreach (var side in new[] { InteractorHandedness.Left, InteractorHandedness.Right })
                foreach (var grip in grips)
                {
                    var start = new Pose(origin + Vector3.up * .2f, Quaternion.Euler(24f, 73f, 11f));
                    body.position = start.position; body.rotation = start.rotation;
                    body.velocity = body.angularVelocity = Vector3.zero;
                    tool.transform.SetPositionAndRotation(start.position, start.rotation);
                    grab.SetTargetPose(start);
                    hand.handedness = side;
                    var handRotation = Quaternion.Euler(6f, -25f, 9f);
                    hand.transform.SetPositionAndRotation(grip.position + Vector3.up * .03f, handRotation);
                    Physics.SyncTransforms();
                    yield return null;
                    manager.SelectEnter((IXRSelectInteractor)hand, grab);
                    for (var frame = 0; frame < 5; frame++) yield return null;

                    Assert.IsTrue(grab.isSelected, path);
                    Assert.IsFalse(guard.IsBlocked, path);
                    Assert.That(Vector3.Distance(grip.position, hand.GetAttachTransform(grab).position), Is.LessThan(.003f), $"{side} {path}");
                    Assert.That(Quaternion.Angle(grip.rotation, handRotation), Is.LessThan(.5f), $"{side} {path}");
                    Assert.That(Vector3.Dot(tool.transform.TransformDirection(axis), handRotation * direction), Is.GreaterThan(.98f), $"{side} {path}");
                    manager.SelectExit((IXRSelectInteractor)hand, grab);
                    yield return null;
                }
                Object.DestroyImmediate(tool);
            }

            var food = Prop("tomato", Vector3.zero).GetComponent<XRGrabInteractable>();
            Assert.IsTrue(food.useDynamicAttach && food.matchAttachPosition && food.matchAttachRotation);
        }

        [UnityTest]
        public IEnumerator ActualHeldKnifeTriggerCutsFoodButDroppedKnifeDoesNot()
        {
            var tomato = Prop("tomato", Vector3.up * .05f);
            var tomatoBody = tomato.GetComponent<Rigidbody>();
            tomatoBody.interpolation = RigidbodyInterpolation.None;
            tomatoBody.isKinematic = true;
            var target = tomato.GetComponent<PreparedKitchenProp>();
            var knife = Prop("knife", Vector3.zero);
            var knifeBody = knife.GetComponent<Rigidbody>();
            var grab = knife.GetComponent<XRGrabInteractable>();
            var guard = knife.GetComponent<CollisionConstrainedGrabTransformer>();
            var blade = knife.GetComponentInChildren<PreparedKitchenKnife>().GetComponent<BoxCollider>();
            var upright = Quaternion.Euler(90f, 0f, 0f);
            knifeBody.interpolation = RigidbodyInterpolation.None;
            knifeBody.isKinematic = true;

            void SetKnifePose(Vector3 position)
            {
                knifeBody.position = position;
                knifeBody.rotation = upright;
                knife.transform.SetPositionAndRotation(position, upright);
                grab.SetTargetPose(new Pose(position, upright));
                Physics.SyncTransforms();
            }

            SetKnifePose(origin + new Vector3(-.071f, .08f, 0f));
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.IsFalse(target.IsSplit, "An unheld knife cannot activate a prepared cut.");

            var handleStart = origin + new Vector3(.095f, .24f, 0f);
            var tomatoMaxX = tomato.GetComponent<XRGrabInteractable>().colliders.Max(collider => collider.bounds.max.x);
            SetKnifePose(handleStart);
            yield return new WaitForFixedUpdate();
            yield return null;
            hand.transform.SetPositionAndRotation(grab.attachTransform.position, grab.attachTransform.rotation);
            manager.SelectEnter((IXRSelectInteractor)hand, grab);
            yield return null;
            Assert.IsTrue(grab.isSelected);
            var gripOffset = grab.GetAttachTransform(hand).position - knife.transform.position;

            IEnumerator PressFrom(Vector3 start)
            {
                for (var step = 1; step <= 30 && !target.IsSplit; step++)
                {
                    hand.transform.position = start + gripOffset - Vector3.up * (.008f * step);
                    yield return null;
                    yield return new WaitForFixedUpdate();
                }
            }

            yield return PressFrom(handleStart);
            Assert.IsTrue(guard.IsBlocked, "The solid handle must actually press against the tomato.");
            Assert.That(blade.bounds.min.x, Is.GreaterThan(tomatoMaxX + .005f),
                $"The blade must stay clear during the handle press. Knife {knife.transform.position:F4}.");
            Assert.IsFalse(target.IsSplit, "Pressing the handle against the food must not cut it.");

            hand.transform.position = handleStart + gripOffset;
            yield return null;
            yield return new WaitForFixedUpdate();
            var bladeStart = origin + new Vector3(-.071f, .24f, 0f);
            hand.transform.position = bladeStart + gripOffset;
            yield return null;
            yield return new WaitForFixedUpdate();
            yield return PressFrom(bladeStart);
            Assert.IsTrue(target.IsSplit, "The held cutting edge must cut at the solid guard's contact distance.");
            manager.SelectExit((IXRSelectInteractor)hand, grab);
        }

        [UnityTest]
        public IEnumerator StoveHeatsCompoundPanOnceAndStopsWhenPanLeaves()
        {
            var zone = Own(new GameObject("Test stove")); zone.transform.position = origin;
            var trigger = zone.AddComponent<BoxCollider>(); trigger.isTrigger = true; trigger.size = new Vector3(.5f, .3f, .5f);
            var source = zone.AddComponent<KitchenHeatSource>(); source.SetOn(true);
            var pan = Load(Cooking + "PREF_Cooking_FryingPan.prefab", Vector3.up * .03f).GetComponent<KitchenFoodVessel>();
            pan.ReceiveEgg(50);
            for (var i = 0; i < 25; i++) yield return new WaitForFixedUpdate();
            Assert.That(pan.Batch.Cooking.Dose, Is.InRange(.35f, .6f), "17 compound contacts must not multiply heat.");
            pan.transform.position += Vector3.right * 2; Physics.SyncTransforms();
            yield return new WaitForFixedUpdate(); var stopped = pan.Batch.Cooking.Dose;
            for (var i = 0; i < 10; i++) yield return new WaitForFixedUpdate();
            Assert.That(pan.Batch.Cooking.Dose, Is.EqualTo(stopped).Within(.021f));
        }
    }
}
#endif
