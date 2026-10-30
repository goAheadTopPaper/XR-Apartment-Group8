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
            var grab = whisk.GetComponent<XRGrabInteractable>(); hand.transform.position = whisk.transform.position;
            yield return null; manager.SelectEnter((IXRSelectInteractor)hand, grab);
            for (var i = 0; i < 240 && bowl.Stage != CookingStage.Beaten; i++)
            {
                var angle = i * .35f;
                hand.transform.position = origin + new Vector3(Mathf.Cos(angle) * .035f, -.005f, Mathf.Sin(angle) * .035f);
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
            var bowl = station.transform.Find("PREF_Cooking_MixingBowl").GetComponent<KitchenFoodVessel>();
            for (var i = 0; i < 80; i++) yield return new WaitForFixedUpdate();
            foreach (var prop in station.GetComponentsInChildren<PreparedKitchenProp>())
            {
                Assert.IsFalse(prop.IsSplit, "Initial placement must not break " + prop.name);
                Assert.That(prop.transform.position.y, Is.GreaterThan(.9f), "Prop fell off the authored table: " + prop.name);
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

        void CaptureCookingScene(Camera source, Vector3 target)
        {
            var go = Own(new GameObject("Cooking visual QA"));
            manager.transform.position = target;
            var camera = go.AddComponent<Camera>(); camera.CopyFrom(source); camera.stereoTargetEye = StereoTargetEyeMask.None;
            camera.transform.position = new Vector3(-2.6f, 2.1f, -.1f); camera.transform.LookAt(target);
            var sourcePost = source.GetComponent("PostProcessLayer");
            if (sourcePost != null) EditorUtility.CopySerialized(sourcePost, go.AddComponent(sourcePost.GetType()));
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
        public IEnumerator ActualHeldKnifeTriggerCutsFoodButDroppedKnifeDoesNot()
        {
            var tomato = Prop("tomato", Vector3.up * .05f);
            tomato.GetComponent<Rigidbody>().isKinematic = true;
            var target = tomato.GetComponent<PreparedKitchenProp>();
            var knife = Prop("knife", new Vector3(.055f, .08f, 0));
            knife.GetComponent<Rigidbody>().isKinematic = true;
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            Assert.IsFalse(target.IsSplit, "An unheld knife cannot activate a prepared cut.");
            knife.transform.position = origin + new Vector3(.055f, .2f, 0); Physics.SyncTransforms();
            var grab = knife.GetComponent<XRGrabInteractable>(); hand.transform.position = knife.transform.position;
            yield return null; manager.SelectEnter((IXRSelectInteractor)hand, grab);
            for (var i = 0; i < 20 && !target.IsSplit; i++)
            {
                hand.transform.position -= Vector3.up * .008f;
                yield return new WaitForFixedUpdate();
            }
            Assert.IsTrue(target.IsSplit, "Authored blade trigger must reach food when its physical blade is constrained at contact.");
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
