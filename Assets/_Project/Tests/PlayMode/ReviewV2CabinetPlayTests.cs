#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
    public sealed class ReviewV2CabinetPlayTests
    {
        Scene scene;
        ReviewSessionHost host;
        XRInteractionManager manager;
        DrivenGrabInteractor hand;
        GameObject handObject, obstacle, cameraObject;
        readonly List<GameObject> hidden = new List<GameObject>();
        float originalDelta;
        KinematicSwingDoor[] Doors => host.Resolve("CABINET_LAYOUT").GetComponentsInChildren<KinematicSwingDoor>();
        [UnitySetUp] public IEnumerator Load()
        {
            originalDelta = Time.captureDeltaTime; Time.captureDeltaTime = 1f / 90;
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
            handObject = new GameObject("Cabinet tracked-hand test driver"); hand = handObject.AddComponent<DrivenGrabInteractor>();
            hand.interactionManager = manager; hand.keepSelectedTargetValid = true;
            foreach (var door in Doors) door.GetComponent<XRSimpleInteractable>().interactionManager = manager;
            yield return null;
        }
        void Experience() { host.Execute(CommandType.CompleteSetup); host.Execute(CommandType.CompleteOnboarding); }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.timeScale = 1; Time.captureDeltaTime = originalDelta;
            if (handObject != null) Object.Destroy(handObject);
            if (obstacle != null) Object.Destroy(obstacle);
            if (cameraObject != null) Object.Destroy(cameraObject);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            foreach (var root in hidden) if (root != null) root.SetActive(true);
            hidden.Clear(); yield return null;
        }
        IEnumerator Pull(KinematicSwingDoor door, float delta)
        {
            var xr = door.GetComponent<XRSimpleInteractable>();
            var pivot = door.transform.position; var radial = door.PullHandle.position - pivot;
            hand.transform.position = door.PullHandle.position; Physics.SyncTransforms();
            Assert.IsTrue(manager.IsSelectPossible((IXRSelectInteractor)hand, xr), door.name);
            manager.SelectEnter((IXRSelectInteractor)hand, xr); Assert.IsTrue(xr.isSelected);
            var before = door.Progress; yield return null;
            Assert.That(door.Progress, Is.EqualTo(before).Within(.002), "Selection must not toggle a pull door");
            for (var i = 1; i <= 64; i++)
            { hand.transform.position = pivot + Quaternion.AngleAxis(delta * i / 64f, door.transform.parent.up) * radial; yield return null; }
            manager.SelectExit((IXRSelectInteractor)hand, xr);
            for (var i = 0; i < 8; i++) yield return null;
        }
        static float Angle(KinematicSwingDoor d) => Mathf.DeltaAngle(0, d.transform.localEulerAngles.y);
        static string Blocker(KinematicSwingDoor d) => ((Collider)typeof(KinematicSwingDoor).GetProperty("OpeningBlocker",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(d))?.name;
        // Unity 2022 Quaternion.Angle rounds rotations below about .162 degrees to zero.
        // Require the closed target AND completed timeline as well as that angular tolerance.
        static bool Closed(KinematicSwingDoor d) => !d.IsOpen && d.Progress == 0 && Mathf.Abs(Angle(d)) < .2f;
        [UnityTest] public IEnumerator ChamferedTopSupportsTheRecoveredAreaWithoutARectangularGap()
        {
            Physics.SyncTransforms();
            for (var i = 1; i < 20; i++)
            {
                var t = i / 20f; var x = Mathf.Lerp(-2.56f, -1.99f, t); var z = Mathf.Lerp(1.67661285f, 1.81f, t) + .02f;
                Assert.IsTrue(Physics.Raycast(new Vector3(x, 1.024f, z), Vector3.down, out var hit, .05f));
                Assert.AreEqual("A_ChamferWorktop", hit.collider.name, "The filled diagonal must be a real support surface");
                Assert.That(hit.point.y, Is.EqualTo(1.01f).Within(.0001f));
            }
            yield return null;
        }
        void PanIsOnColdHob()
        {
            var pan = host.station.transform.Find("PREF_Cooking_FryingPan");
            var surface = host.station.transform.Find("LOC_BurnerSurface");
            Assert.That(new Vector2(pan.position.x - surface.position.x, pan.position.z - surface.position.z).magnitude, Is.LessThan(.03f));
            var baseCollider = pan.GetComponentsInChildren<BoxCollider>().Single(c => c.name == "COL_OpenVessel");
            Assert.That(baseCollider.bounds.min.y - surface.position.y, Is.InRange(-.003f, .015f), "Pan bottom must rest on the burner supports");
            Assert.IsFalse(host.station.GetComponentInChildren<KitchenHeatSource>().IsOn);
        }
        [UnityTest] public IEnumerator PanStartsOnColdHobHeatsFromTheMovedSwitchAndReturnsAfterResetAndReload()
        {
            Experience();
            for (var i = 0; i < 20; i++) yield return new WaitForFixedUpdate();
            PanIsOnColdHob();
            var pan = host.station.transform.Find("PREF_Cooking_FryingPan").GetComponent<KitchenFoodVessel>();
            var heat = host.station.GetComponentInChildren<KitchenHeatSource>();
            var button = host.station.transform.Find("ACT_STOVE").GetComponent<XRSimpleInteractable>(); button.interactionManager = manager;
            hand.transform.position = button.transform.position;
            manager.SelectEnter((IXRSelectInteractor)hand, button); manager.SelectExit((IXRSelectInteractor)hand, button);
            for (var i = 0; i < 8; i++) yield return new WaitForFixedUpdate();
            Assert.IsTrue(heat.IsHeating(pan), "Moved switch, zone and pan must still form the real heating path");
            var body = pan.GetComponent<Rigidbody>(); body.position = pan.transform.position + new Vector3(.5f, .3f, 0); pan.transform.position = body.position;
            Assert.IsTrue(host.Execute(CommandType.ResetWorld).Accepted);
            for (var i = 0; i < 120 && host.Snapshot.IsBusy; i++) yield return null;
            Assert.IsFalse(host.Snapshot.IsBusy);
            for (var i = 0; i < 15; i++) yield return new WaitForFixedUpdate();
            PanIsOnColdHob();
            var before = pan.transform.position; host.Execute(CommandType.Review); host.Execute(CommandType.Continue); yield return null;
            Assert.That(Vector3.Distance(before, pan.transform.position), Is.LessThan(.003f));
            host.Execute(CommandType.ChangeScheme, SchemeIds.ExternalRefrigerator);
            yield return ReviewOperationWait.Idle(host); yield return null;
            Assert.AreEqual(SchemeIds.ExternalRefrigerator, host.Snapshot.SchemeId);
            host.Execute(CommandType.ChangeScheme, SchemeIds.IntegratedKitchen);
            yield return ReviewOperationWait.Idle(host); yield return null;
            Assert.AreEqual(SchemeIds.IntegratedKitchen, host.Snapshot.SchemeId);
            for (var i = 0; i < 15; i++) yield return new WaitForFixedUpdate();
            PanIsOnColdHob();
        }
        [UnityTest] public IEnumerator CaptureChamferAndLeftHobRightPreparationLayout()
        {
            Experience();
            foreach (var panel in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ReviewIntegrationPanel>(true))) panel.SetOpen(false);
            cameraObject = new GameObject("A worktop inspection camera"); var camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>(); camera.fieldOfView = 70; camera.nearClipPlane = .025f; camera.farClipPlane = 30;
            var target = new RenderTexture(1600, 1000, 24); target.Create(); camera.targetTexture = target;
            Directory.CreateDirectory("Logs/ReviewV2/M2/Counter");
            try
            {
                for (var view = 0; view < 2; view++)
                {
                    camera.orthographic = view == 1; camera.orthographicSize = 1.45f;
                    camera.transform.position = view == 0 ? new Vector3(-3.05f, 2.15f, .35f) : new Vector3(-3.05f, 2.64f, 1.6f);
                    if (view == 0) camera.transform.LookAt(new Vector3(-2.95f, 1.07f, 2.0f));
                    else camera.transform.rotation = Quaternion.Euler(90, 0, 0);
                    for (var i = 0; i < 30; i++) yield return null;
                    var previous = RenderTexture.active; RenderTexture.active = target;
                    var pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                    try { pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); pixels.Apply();
                        File.WriteAllBytes("Logs/ReviewV2/M2/Counter/" + (view == 0 ? "01-worktop.png" : "02-top-view.png"), pixels.EncodeToPNG()); }
                    finally { RenderTexture.active = previous; Object.Destroy(pixels); }
                }
            }
            finally { camera.targetTexture = null; target.Release(); Object.Destroy(target); }
        }
        void NoPenetration(KinematicSwingDoor door)
        {
            Physics.SyncTransforms();
            foreach (var panel in door.GetComponentsInChildren<BoxCollider>().Where(c => c.enabled))
                foreach (var other in Physics.OverlapBox(panel.bounds.center, panel.bounds.extents + Vector3.one * .001f, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (other.transform.IsChildOf(door.transform) || other.attachedRigidbody != null) continue;
                    if (Physics.ComputePenetration(panel, panel.transform.position, panel.transform.rotation, other,
                        other.transform.position, other.transform.rotation, out _, out var depth))
                        Assert.That(depth, Is.LessThan(.0002f), door.name + " penetrates " + other.name);
                }
        }
        [UnityTest] public IEnumerator SixDoorsCanBeSelectedPulledToNinetyAndClosedWithoutFurniturePenetration()
        {
            Experience(); var failures = new List<string>();
            foreach (var door in Doors)
            {
                float sign = door.name.EndsWith("_R") ? -1 : 1;
                yield return Pull(door, sign * 90);
                if (Mathf.Abs(Angle(door)) < 89) failures.Add(door.name + " opens only to " + Angle(door) + " by " + Blocker(door));
                try { NoPenetration(door); } catch (AssertionException e) { failures.Add(e.Message); }
                yield return Pull(door, -Angle(door));
                if (Mathf.Abs(Angle(door)) > 1) failures.Add(door.name + " closes only to " + Angle(door) + " by " + Blocker(door));
                try { NoPenetration(door); } catch (AssertionException e) { failures.Add(e.Message); }
            }
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }
        [UnityTest] public IEnumerator CabinetSweepStopsAtAddedObstacleAndResumesAfterRemoval()
        {
            Experience(); var door = Doors.Single(d => d.name == "CABINET_LEFT_UPPER_L");
            obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube); obstacle.name = "Cabinet sweep obstruction";
            obstacle.transform.position = door.transform.position + door.transform.parent.rotation *
                (Quaternion.Euler(0, 45, 0) * new Vector3(-.25f, .55f, 0));
            obstacle.transform.localScale = new Vector3(.08f, .18f, .08f); Physics.SyncTransforms();
            yield return Pull(door, 90);
            Assert.That(Mathf.Abs(Angle(door)), Is.InRange(5, 70)); NoPenetration(door);
            Object.Destroy(obstacle); yield return null; Physics.SyncTransforms();
            yield return Pull(door, 90 - Angle(door));
            Assert.That(Mathf.Abs(Angle(door)), Is.GreaterThan(89));
        }
        [UnityTest] public IEnumerator CabinetGateReviewResetAndSchemeRoundTripPreserveTheExpectedState()
        {
            var door = Doors[0]; var xr = door.GetComponent<XRSimpleInteractable>();
            Assert.IsFalse(manager.IsSelectPossible((IXRSelectInteractor)hand, xr), "Setup gate");
            Experience(); yield return Pull(door, 40);
            for (var i = 0; i < 120; i++) yield return null;
            var angle = Angle(door); var item = host.groceries.groceries[1]; var position = item.transform.position;
            host.Execute(CommandType.Review); host.Execute(CommandType.Continue); yield return null;
            Assert.That(Angle(door), Is.EqualTo(angle).Within(.02)); Assert.That(Vector3.Distance(position, item.transform.position), Is.LessThan(.001f));
            host.Execute(CommandType.ResetWorld);
            for (var i = 0; i < 120 && host.Snapshot.IsBusy; i++) yield return null;
            Assert.IsFalse(host.Snapshot.IsBusy); Assert.IsTrue(Doors.All(Closed), "Reset doors: " + string.Join(", ", Doors.Select(d => d.name + "=" + Angle(d) + ", progress=" + d.Progress)));
            yield return Pull(Doors[0], 40);
            Assert.IsTrue(host.Execute(CommandType.ChangeScheme, SchemeIds.ExternalRefrigerator).Accepted);
            yield return ReviewOperationWait.Idle(host); yield return null;
            Assert.AreEqual(SchemeIds.ExternalRefrigerator, host.Snapshot.SchemeId); Assert.IsNull(host.Resolve("MICROWAVE_VISUAL"));
            var initialItemPosition = ((ReviewSchemePort)host.schemePort).layouts.Single(w => w.schemeId == SchemeIds.IntegratedKitchen).groceries.groceries[1].transform.position;
            Assert.IsTrue(host.Execute(CommandType.ChangeScheme, SchemeIds.IntegratedKitchen).Accepted);
            yield return ReviewOperationWait.Idle(host);
            Assert.AreEqual(initialItemPosition, host.groceries.groceries[1].transform.position, "New A world starts with the authored item pose");
            yield return null;
            Assert.AreEqual("integrated-kitchen-m2-v5", host.Snapshot.ModelVersion);
            Assert.IsTrue(Doors.All(Closed), "Fresh A doors: " + string.Join(", ", Doors.Select(d => d.name + "=" + Angle(d))));
        }
        [UnityTest] public IEnumerator CabinetDoesNotReduceEitherFridgeDoorBaselineSweep()
        {
            Experience(); var cabinet = host.Resolve("CABINET_LAYOUT").gameObject;
            var fridge = host.station.ActiveFridge; var leaves = new[] { fridge.Door, fridge.SecondaryDoor };
            var report = new List<string>();
            foreach (var door in leaves)
            {
                fridge.ResetFridge(); cabinet.SetActive(false); Physics.SyncTransforms();
                door.SetOpenImmediate(true); var baseline = Mathf.Abs(Angle(door));
                door.SetOpenImmediate(false); cabinet.SetActive(true); Physics.SyncTransforms();
                door.SetOpenImmediate(true); var actual = Mathf.Abs(Angle(door));
                report.Add(door.name + " withoutCabinet=" + baseline + " withCabinet=" + actual);
                Assert.That(actual, Is.GreaterThanOrEqualTo(baseline - .5f), string.Join("\n", report));
                Assert.That(door.Progress, Is.GreaterThan(.8)); NoPenetration(door);
                door.SetOpenImmediate(false); Physics.SyncTransforms();
            }
            Directory.CreateDirectory("Logs/ReviewV2/M2/Cabinet");
            File.WriteAllLines("Logs/ReviewV2/M2/Cabinet/fridge-baseline.txt", report);
            yield return null;
        }
        [UnityTest] public IEnumerator CaptureClosedFridgeOpenAndCabinetOpenFromOneCamera()
        {
            Experience();
            foreach (var panel in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ReviewIntegrationPanel>(true))) panel.SetOpen(false);
            cameraObject = new GameObject("Cabinet acceptance camera"); var camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            camera.transform.position = new Vector3(-4.6f, 1.50f, 1.60f); camera.transform.LookAt(new Vector3(-1.9f, 1.35f, 1.52f));
            camera.fieldOfView = 60; camera.nearClipPlane = .03f; camera.farClipPlane = 30;
            var texture = new RenderTexture(1440, 1080, 24); texture.Create(); camera.targetTexture = texture;
            var folder = "Logs/ReviewV2/M2/Cabinet/"; Directory.CreateDirectory(folder);
            try
            {
                for (var state = 0; state < 3; state++)
                {
                    if (state == 1)
                    {
                        host.station.ActiveFridge.Door.SetOpenImmediate(true); host.station.ActiveFridge.SecondaryDoor.SetOpenImmediate(true);
                        Assert.IsTrue(host.station.ActiveFridge.Accessible);
                        Assert.That(Mathf.Abs(Angle(host.station.ActiveFridge.Door)), Is.GreaterThan(104));
                        Assert.That(host.station.ActiveFridge.SecondaryDoor.Progress, Is.GreaterThan(.8), "Existing kitchen door frame limits the secondary leaf");
                    }
                    if (state == 2)
                    {
                        host.station.ActiveFridge.Door.SetOpenImmediate(false); host.station.ActiveFridge.SecondaryDoor.SetOpenImmediate(false);
                        foreach (var d in Doors) { d.SetOpenImmediate(true); Assert.That(Mathf.Abs(Angle(d)), Is.GreaterThan(89), d.name + " by " + Blocker(d)); NoPenetration(d); }
                    }
                    for (var i = 0; i < 30; i++) yield return null;
                    var previous = RenderTexture.active; RenderTexture.active = texture;
                    var pixels = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
                    try { pixels.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); pixels.Apply();
                        File.WriteAllBytes(folder + new[] { "01-closed.png", "02-fridge-open.png", "03-cabinet-open.png" }[state], pixels.EncodeToPNG()); }
                    finally { RenderTexture.active = previous; Object.Destroy(pixels); }
                }
            }
            finally { camera.targetTexture = null; texture.Release(); Object.Destroy(texture); }
        }
    }
}
#endif
