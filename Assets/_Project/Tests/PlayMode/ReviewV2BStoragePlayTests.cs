#if UNITY_EDITOR
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
using XRApartment.Data.ReviewV2;
using XRApartment.Runtime;
using XRApartment.Runtime.ReviewV2;
using Object = UnityEngine.Object;

namespace XRApartment.Tests
{
    public sealed class ReviewV2BStoragePlayTests
    {
        const string Folder = "Logs/ReviewV2/M2/BFridgeGapV8/";
        Scene scene; ReviewSessionHost host; XRInteractionManager manager;
        DrivenGrabInteractor hand; GameObject driver, obstacle, cameraObject;
        XRGrabInteractable cup; Vector3 cupStart, attachOffset; Quaternion cupRotation, attachRotation;
        float oldDelta; readonly List<GameObject> hidden = new List<GameObject>();
        KinematicSwingDoor[] Doors => host.Resolve("CABINET_LAYOUT").GetComponentsInChildren<KinematicSwingDoor>();
        static float Angle(KinematicSwingDoor door) => Mathf.DeltaAngle(0, door.transform.localEulerAngles.y);
        static IEnumerator Frames(int n) { for (var i = 0; i < n; i++) { yield return null; yield return new WaitForFixedUpdate(); } }
        [UnitySetUp] public IEnumerator Load()
        {
            oldDelta = Time.captureDeltaTime; Time.captureDeltaTime = 1f / 90;
            for (var i = 0; i < SceneManager.sceneCount; i++) foreach (var root in SceneManager.GetSceneAt(i).GetRootGameObjects())
                if (root.activeSelf && (root.GetComponentInChildren<Renderer>(true) != null || root.GetComponentInChildren<Collider>(true) != null || root.GetComponentInChildren<Camera>(true) != null || root.GetComponentInChildren<Light>(true) != null))
                { hidden.Add(root); root.SetActive(false); }
            scene = EditorSceneManager.LoadSceneInPlayMode("Assets/_Project/Scenes/SCN_Kitchen_ReviewV2.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            while (!scene.isLoaded) yield return null;
            host = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ReviewSessionHost>(true)).Single();
            host.locomotion.Origin.gameObject.SetActive(false); host.locomotion.Origin.Camera.transform.position = new Vector3(-3.15f, 1.65f, 1.12f);
            manager = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<XRInteractionManager>()).Single();
            driver = new GameObject("B storage tracked hand"); hand = driver.AddComponent<DrivenGrabInteractor>(); hand.interactionManager = manager; hand.keepSelectedTargetValid = true;
            host.Execute(CommandType.CompleteSetup); host.Execute(CommandType.CompleteOnboarding);
            if (host.Snapshot.SchemeId != SchemeIds.ExternalRefrigerator) Assert.IsTrue(host.Execute(CommandType.ChangeScheme, SchemeIds.ExternalRefrigerator).Accepted);
            Assert.AreEqual("external-fridge-m2-v8", host.Snapshot.ModelVersion);
            foreach (var door in Doors) door.GetComponent<XRSimpleInteractable>().interactionManager = manager;
            var world = host.station.GetComponentInParent<ReviewSchemeWorld>();
            cup = world.GetComponentsInChildren<XRGrabInteractable>().Single(g => g.name == "PREF_Prop_Cup"); cup.interactionManager = manager;
            yield return Frames(15); cupStart = cup.transform.position; Directory.CreateDirectory(Folder);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.timeScale = 1; Time.captureDeltaTime = oldDelta;
            foreach (var go in new[] { driver, obstacle, cameraObject }) if (go != null) Object.Destroy(go);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            foreach (var root in hidden) if (root != null) root.SetActive(true); hidden.Clear(); yield return null;
        }
        void NoPenetration(KinematicSwingDoor door)
        {
            Physics.SyncTransforms();
            foreach (var c in door.GetComponentsInChildren<BoxCollider>().Where(c => c.enabled && !c.isTrigger))
                foreach (var other in Physics.OverlapBox(c.bounds.center, c.bounds.extents + Vector3.one * .001f, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (other.transform.IsChildOf(door.transform) || other.attachedRigidbody != null || other.gameObject.scene != host.station.gameObject.scene) continue;
                    if (Physics.ComputePenetration(c, c.transform.position, c.transform.rotation, other, other.transform.position, other.transform.rotation, out _, out var depth))
                        Assert.Less(depth, .0002f, door.name + "/" + c.name + " penetrates " + other.name);
                }
        }
        IEnumerator Pull(KinematicSwingDoor door, float degrees)
        {
            var xr = door.GetComponent<XRSimpleInteractable>(); var pivot = door.transform.position; var radial = door.PullHandle.position - pivot;
            hand.transform.position = door.PullHandle.position; Assert.IsTrue(manager.IsSelectPossible((IXRSelectInteractor)hand, xr));
            manager.SelectEnter((IXRSelectInteractor)hand, xr); Assert.IsTrue(xr.isSelected); var before = door.Progress;
            yield return null; Assert.AreEqual(before, door.Progress);
            for (var i = 1; i <= 80; i++) { hand.transform.position = pivot + Quaternion.AngleAxis(degrees * i / 80f, door.transform.parent.up) * radial; yield return null; NoPenetration(door); }
            manager.SelectExit((IXRSelectInteractor)hand, xr); yield return Frames(5);
        }
        float OpenAngle(KinematicSwingDoor door) { var data = new SerializedObject(door); return data.FindProperty("swingSign").floatValue * data.FindProperty("openAngleDegrees").floatValue; }
        IEnumerator PickCup()
        {
            hand.transform.SetPositionAndRotation(cup.transform.position, cup.transform.rotation);
            Assert.IsTrue(manager.IsSelectPossible((IXRSelectInteractor)hand, cup)); manager.SelectEnter((IXRSelectInteractor)hand, cup); yield return null;
            Assert.IsTrue(cup.isSelected); var attach = cup.GetAttachTransform(hand);
            cupRotation = cup.transform.rotation; attachOffset = cup.transform.InverseTransformPoint(attach.position); attachRotation = Quaternion.Inverse(cupRotation) * attach.rotation;
        }
        IEnumerator MoveCup(Vector3 point, int frames = 35)
        {
            var start = cup.transform.position;
            for (var i = 1; i <= frames; i++) { var p = Vector3.Lerp(start, point, i / (float)frames); hand.transform.SetPositionAndRotation(p + cupRotation * attachOffset, cupRotation * attachRotation); yield return null; yield return new WaitForFixedUpdate(); }
            Assert.That(Vector3.Distance(cup.transform.position, point), Is.LessThan(.035f), "Actual swept cup blocked toward " + point + ", at " + cup.transform.position);
        }
        IEnumerator PlaceCup(Vector3 surface, string colliderName)
        {
            yield return PickCup(); Physics.SyncTransforms();
            var bottomOffset = cup.colliders.Where(c => c.enabled && !c.isTrigger).Min(c => c.bounds.min.y) - cup.transform.position.y;
            if (cup.transform.position.x < -4.4f && cup.transform.position.z < -.5f) yield return MoveCup(new Vector3(-4.20f, cup.transform.position.y, cup.transform.position.z));
            else if (cup.transform.position.x > -2.0f && cup.transform.position.y > 1.4f) yield return MoveCup(new Vector3(-2.10f, cup.transform.position.y, cup.transform.position.z));
            yield return MoveCup(new Vector3(cup.transform.position.x, 1.8f, cup.transform.position.z));
            yield return MoveCup(new Vector3(-3.1f, 1.8f, cup.transform.position.z));
            yield return MoveCup(new Vector3(-3.1f, 1.8f, surface.z));
            var x = surface.x < -4 ? -4.20f : -2.10f;
            var target = surface + Vector3.up * (.045f - bottomOffset);
            yield return MoveCup(new Vector3(x, 1.8f, surface.z));
            yield return MoveCup(new Vector3(x, target.y, surface.z)); yield return MoveCup(target);
            yield return Frames(24); manager.SelectExit((IXRSelectInteractor)hand, cup); yield return Frames(60); Physics.SyncTransforms();
            var bottom = cup.colliders.Where(c => c.enabled && !c.isTrigger).Min(c => c.bounds.min.y);
            Assert.That(bottom - surface.y, Is.InRange(-.001f, .018f), colliderName + " must support the cup: bottom=" + bottom);
            Assert.Less(cup.GetComponent<Rigidbody>().velocity.magnitude, .05f); Assert.IsFalse(cup.isSelected);
            var hits = Physics.RaycastAll(new Vector3(cup.transform.position.x, bottom + .02f, cup.transform.position.z), Vector3.down, .08f, ~0, QueryTriggerInteraction.Ignore)
                .Where(h => h.collider.attachedRigidbody == null).OrderBy(h => h.distance).ToArray();
            Assert.IsNotEmpty(hits); Assert.AreEqual(colliderName, hits[0].collider.name);
        }
        [UnityTest] public IEnumerator BothPrepDoorsReachNinetyAndCloseWithExistingPauseGate()
        {
            var first = Doors[0].GetComponent<XRSimpleInteractable>();
            host.Execute(CommandType.Pause); Assert.IsFalse(manager.IsSelectPossible((IXRSelectInteractor)hand, first)); host.Execute(CommandType.Resume);
            foreach (var door in Doors)
            { yield return Pull(door, OpenAngle(door)); Assert.That(Mathf.Abs(Angle(door)), Is.EqualTo(90).Within(.25f), door.name); yield return Pull(door, -Angle(door)); Assert.Less(Mathf.Abs(Angle(door)), .2f); Assert.AreEqual(0, door.Progress); }
        }
        [UnityTest] public IEnumerator NewDoorSweepRejectsAnObstacleAndOpensAfterItsRemoval()
        {
            var door = Doors.First(d => d.name == "CABINET_PREP_NORTH"); var radial = door.PullHandle.position - door.transform.position; radial.y = 0;
            obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube); obstacle.name = "B prep obstruction";
            obstacle.transform.position = door.transform.position + Quaternion.AngleAxis(OpenAngle(door) * .5f, door.transform.parent.up) * radial.normalized * .28f + Vector3.up * .45f;
            obstacle.transform.localScale = new Vector3(.08f, .22f, .08f); Physics.SyncTransforms();
            yield return Pull(door, OpenAngle(door)); Assert.Less(Mathf.Abs(Angle(door)), 80);
            Object.Destroy(obstacle); yield return null; yield return Pull(door, OpenAngle(door) - Angle(door)); Assert.That(Mathf.Abs(Angle(door)), Is.EqualTo(90).Within(.25f));
        }
        [UnityTest] public IEnumerator FourOpenCellsAndFloatingShelfSupportTheExistingCupAfterRealRelease()
        {
            var root = host.Resolve("WINDOW_STORAGE"); var rows = new List<string>();
            foreach (var x in new[] { -.33f, .33f }) foreach (var y in new[] { .12f, .55f })
            { yield return PlaceCup(root.TransformPoint(new Vector3(x, y, .06f)), y < .2f ? "Shelf_0.11" : "Shelf_0.54"); rows.Add(cup.transform.position.ToString("F4")); }
            var shelf = host.Resolve("FLOATING_SHELF"); yield return PlaceCup(shelf.position + Vector3.up * .015f, "B_FloatingShelfTop");
            Assert.IsTrue(host.Snapshot.Goals.All(g => g.State == GoalState.NotStarted)); rows.Add("floating=" + cup.transform.position.ToString("F4")); File.WriteAllLines(Folder + "cup-support.txt", rows);
        }
        [UnityTest] public IEnumerator BothEndsOfTheExtendedShelfSupportTheCupAndMicrowaveFeetRestOnTheOriginalCounter()
        {
            var microwave = host.Resolve("MICROWAVE_VISUAL"); var rows = new List<string>();
            foreach (var foot in microwave.GetComponentsInChildren<BoxCollider>().Where(c => c.name.StartsWith("Foot_")))
            {
                var hits = Physics.RaycastAll(foot.bounds.center + Vector3.up * .03f, Vector3.down, .10f, ~0, QueryTriggerInteraction.Ignore)
                    .Where(h => !h.collider.transform.IsChildOf(microwave)).OrderBy(h => h.distance).ToArray();
                Assert.IsNotEmpty(hits, "Microwave must stand on the original worktop");
                Assert.AreEqual("SM_KitchenTable", hits[0].collider.name); Assert.That(hits[0].point.y, Is.EqualTo(1.00f).Within(.002f));
                rows.Add(foot.name + " support=" + hits[0].point.ToString("F5"));
            }
            var shelf = host.Resolve("FLOATING_SHELF");
            foreach (var z in new[] { .81f, 2.15f })
            {
                yield return PlaceCup(new Vector3(shelf.position.x, 1.55f, z), "B_FloatingShelfTop");
                rows.Add("cup=" + cup.transform.position.ToString("F5"));
            }
            var pan = host.station.transform.Find("PREF_Cooking_FryingPan"); var burner = host.station.transform.Find("LOC_BurnerSurface");
            var heat = host.station.GetComponentInChildren<KitchenHeatSource>(); Assert.IsFalse(heat.IsOn);
            rows.Add("settled pan=" + pan.position.ToString("F5") + " burner=" + burner.position.ToString("F5"));
            // Check the existing cooking behavior after the long support test, rather than
            // imposing a new docking distance on B's unchanged dynamic pan.
            heat.SetOn(true); yield return Frames(3);
            Assert.IsTrue(heat.IsHeating(pan.GetComponent<KitchenFoodVessel>()), "The initially placed pan must remain usable on the real burner");
            heat.SetOn(false);
            File.WriteAllLines(Folder + "extended-shelf-and-microwave-support.txt", rows);
        }
        [UnityTest] public IEnumerator ReviewKeepsNewDoorAndCupStateResetAndRoundTripRestoreTheStart()
        {
            yield return PlaceCup(host.Resolve("FLOATING_SHELF").position + Vector3.up * .015f, "B_FloatingShelfTop");
            var door = Doors[0]; yield return Pull(door, OpenAngle(door) * .5f); var angle = Angle(door); var position = cup.transform.position; var visit = host.Snapshot.SchemeVisitId;
            host.Execute(CommandType.Review); host.Execute(CommandType.Continue); yield return Frames(5);
            Assert.AreEqual(visit, host.Snapshot.SchemeVisitId); Assert.That(Angle(door), Is.EqualTo(angle).Within(.001f)); Assert.Less(Vector3.Distance(cup.transform.position, position), .003f);
            host.Execute(CommandType.ResetWorld); for (var i = 0; i < 120 && host.Snapshot.IsBusy; i++) yield return null;
            yield return Frames(15); foreach (var d in Doors) { Assert.AreEqual(0, d.Progress); Assert.Less(Mathf.Abs(Angle(d)), .2f); }
            Assert.Less(Vector3.Distance(cup.transform.position, cupStart), .025f);
            AssertCounterStart();
            host.Execute(CommandType.ChangeScheme, SchemeIds.IntegratedKitchen); yield return Frames(8);
            Assert.AreEqual("integrated-kitchen-m2-v5", host.Snapshot.ModelVersion); Assert.IsNull(host.Resolve("WINDOW_STORAGE"));
            host.Execute(CommandType.ChangeScheme, SchemeIds.ExternalRefrigerator); yield return Frames(12);
            Assert.AreEqual("external-fridge-m2-v8", host.Snapshot.ModelVersion); Assert.AreEqual(2, Doors.Length); Assert.IsTrue(Doors.All(d => d.Progress == 0));
            AssertCounterStart();
        }
        void AssertCounterStart()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/ReviewV2/Schemes/PREF_SCHEME_B.prefab").GetComponent<ReviewSchemeWorld>();
            foreach (var name in new[] { "PREF_Cooking_FryingPan", "PREF_Cooking_ChoppingBoard", "PREF_Prop_knife", "PREF_Cooking_MixingBowl", "PREF_Cooking_TomatoBowl",
                "PREF_Cooking_Whisk", "PREF_Cooking_Spatula", "PREF_Cooking_Ladle", "PREF_Prop_plate" })
                Assert.Less(Vector3.Distance(source.station.transform.Find(name).position, host.station.transform.Find(name).position), .04f, name + " must return to its new initial layout");
            Assert.IsFalse(host.station.GetComponentInChildren<KitchenHeatSource>().IsOn);
        }
        IEnumerator Capture(Camera camera, RenderTexture texture, string name)
        {
            for (var i = 0; i < 30; i++) yield return null;
            var previous = RenderTexture.active; RenderTexture.active = texture; var pixels = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
            try { pixels.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); pixels.Apply(); File.WriteAllBytes(Folder + name, pixels.EncodeToPNG()); }
            finally { RenderTexture.active = previous; Object.Destroy(pixels); }
        }
        [UnityTest] public IEnumerator CaptureTheActualWoodStoragePrepClosedOpenAndTopViews()
        {
            foreach (var panel in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ReviewIntegrationPanel>(true))) panel.SetOpen(false);
            cameraObject = new GameObject("B storage acceptance camera"); var camera = cameraObject.AddComponent<Camera>(); camera.fieldOfView = 65; camera.nearClipPlane = .03f; camera.farClipPlane = 30;
            var texture = new RenderTexture(1440, 1080, 24); texture.Create(); camera.targetTexture = texture;
            try
            {
                yield return PlaceCup(host.Resolve("WINDOW_STORAGE").TransformPoint(new Vector3(-.33f, .55f, .06f)), "Shelf_0.54");
                camera.transform.position = new Vector3(-3.1f, 1.65f, -1.75f); camera.transform.LookAt(new Vector3(-4.7f, .95f, -.95f)); yield return Capture(camera, texture, "01-window-storage.png");
                yield return PlaceCup(host.Resolve("FLOATING_SHELF").position + Vector3.up * .015f, "B_FloatingShelfTop");
                camera.transform.position = new Vector3(-3.65f, 1.5f, 1.30f); camera.transform.LookAt(new Vector3(-1.75f, 1.00f, 1.45f)); yield return Capture(camera, texture, "02-prep-closed.png");
                foreach (var door in Doors) { yield return Pull(door, OpenAngle(door)); Assert.That(Mathf.Abs(Angle(door)), Is.EqualTo(90).Within(.25f)); }
                yield return Capture(camera, texture, "03-prep-open.png");
                foreach (var door in Doors) yield return Pull(door, -Angle(door));
                camera.orthographic = true; camera.orthographicSize = 2.6f; camera.transform.SetPositionAndRotation(new Vector3(-3.25f, 1.95f, .15f), Quaternion.Euler(90, 0, 0));
                var top = new RenderTexture(1080, 1440, 24); top.Create(); camera.targetTexture = top;
                try { yield return Capture(camera, top, "04-top.png"); } finally { camera.targetTexture = texture; top.Release(); Object.Destroy(top); }
            }
            finally { camera.targetTexture = null; texture.Release(); Object.Destroy(texture); }
        }
    }
}
#endif
