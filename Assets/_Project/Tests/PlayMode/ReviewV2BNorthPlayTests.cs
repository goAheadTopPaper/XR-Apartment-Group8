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
    public sealed class ReviewV2BNorthPlayTests
    {
        const string Folder = "Logs/ReviewV2/M2/BFridgeGapV8/BNorthRegression/";
        Scene scene;
        ReviewSessionHost host;
        XRInteractionManager manager;
        DrivenGrabInteractor hand, doorHand;
        GameObject driver, obstacle, actor, cameraObject;
        readonly List<GameObject> hidden = new List<GameObject>();
        float beforeDelta;
        KitchenFridge Fridge => host.station.ActiveFridge;
        KinematicSwingDoor Door => Fridge.Door;
        static float Angle(KinematicSwingDoor door) => Mathf.DeltaAngle(0, door.transform.localEulerAngles.y);
        static string Blocker(KinematicSwingDoor door) => ((Collider)typeof(KinematicSwingDoor).GetProperty("OpeningBlocker", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(door))?.name;
        [UnitySetUp] public IEnumerator Load()
        {
            beforeDelta = Time.captureDeltaTime; Time.captureDeltaTime = 1f / 90;
            for (var i = 0; i < SceneManager.sceneCount; i++)
                foreach (var root in SceneManager.GetSceneAt(i).GetRootGameObjects())
                    if (root.activeSelf && (root.GetComponentInChildren<Collider>(true) != null || root.GetComponentInChildren<Renderer>(true) != null || root.GetComponentInChildren<Camera>(true) != null))
                    { hidden.Add(root); root.SetActive(false); }
            scene = EditorSceneManager.LoadSceneInPlayMode("Assets/_Project/Scenes/SCN_Kitchen_ReviewV2.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            while (!scene.isLoaded) yield return null;
            host = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ReviewSessionHost>(true)).Single();
            host.locomotion.Origin.gameObject.SetActive(false);
            host.locomotion.Origin.Camera.transform.position = new Vector3(-3.15f, 1.65f, 1.12f);
            manager = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<XRInteractionManager>()).Single();
            driver = new GameObject("B north tracked hand"); hand = driver.AddComponent<DrivenGrabInteractor>();
            hand.interactionManager = manager; hand.keepSelectedTargetValid = true;
            var doorDriver = new GameObject("B north door hand"); doorDriver.transform.SetParent(driver.transform);
            doorHand = doorDriver.AddComponent<DrivenGrabInteractor>(); doorHand.interactionManager = manager; doorHand.keepSelectedTargetValid = true;
            host.Execute(CommandType.CompleteSetup); host.Execute(CommandType.CompleteOnboarding);
            if (host.Snapshot.SchemeId != SchemeIds.ExternalRefrigerator)
                Assert.IsTrue(host.Execute(CommandType.ChangeScheme, SchemeIds.ExternalRefrigerator).Accepted);
            Assert.AreEqual(SchemeIds.ExternalRefrigerator, host.Snapshot.SchemeId);
            Door.GetComponent<XRSimpleInteractable>().interactionManager = manager;
            Directory.CreateDirectory(Folder); yield return Frames(8);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.timeScale = 1; Time.captureDeltaTime = beforeDelta;
            foreach (var go in new[] { driver, obstacle, actor, cameraObject }) if (go != null) Object.Destroy(go);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            foreach (var root in hidden) if (root != null) root.SetActive(true);
            hidden.Clear(); yield return null;
        }
        static IEnumerator Frames(int count) { for (var i = 0; i < count; i++) { yield return null; yield return new WaitForFixedUpdate(); } }
        void NoPenetration()
        {
            Physics.SyncTransforms();
            foreach (var panel in Door.GetComponentsInChildren<BoxCollider>().Where(c => c.enabled && !c.isTrigger))
                foreach (var other in Physics.OverlapBox(panel.bounds.center, panel.bounds.extents + Vector3.one * .001f, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (other.transform.IsChildOf(Door.transform) || other.attachedRigidbody != null || other.gameObject.scene != host.station.gameObject.scene) continue;
                    if (Physics.ComputePenetration(panel, panel.transform.position, panel.transform.rotation, other, other.transform.position, other.transform.rotation, out _, out var depth))
                        Assert.Less(depth, .0002f, panel.name + " penetrates " + other.name);
                }
        }
        IEnumerator Pull(float degrees)
        {
            var xr = Door.GetComponent<XRSimpleInteractable>(); var pivot = Door.transform.position;
            var radial = Door.PullHandle.position - pivot;
            doorHand.transform.position = Door.PullHandle.position;
            Assert.IsTrue(manager.IsSelectPossible((IXRSelectInteractor)doorHand, xr));
            manager.SelectEnter((IXRSelectInteractor)doorHand, xr); Assert.IsTrue(xr.isSelected);
            var before = Door.Progress; yield return null; Assert.AreEqual(before, Door.Progress);
            for (var i = 1; i <= 80; i++)
            {
                doorHand.transform.position = pivot + Quaternion.AngleAxis(degrees * i / 80, Door.transform.parent.up) * radial;
                yield return null; NoPenetration();
            }
            manager.SelectExit((IXRSelectInteractor)doorHand, xr); yield return Frames(4);
        }
        void FullOpen() { Assert.That(Angle(Door), Is.EqualTo(-105).Within(.25), "blocked by " + Blocker(Door)); Assert.IsTrue(Fridge.Accessible); }
        [UnityTest] public IEnumerator NorthHingeReaches105AndClosesWhileKeepingTheSinkWallGap()
        {
            Assert.Greater(Door.transform.position.z, Door.PullHandle.position.z);
            var distances = new List<float>(); var lines = new List<string>();
            Physics.SyncTransforms();
            foreach (var dx in new[] { -.30f, 0, .30f }) foreach (var y in new[] { .3f, 1f, 1.6f })
            {
                var origin = new Vector3(Fridge.transform.position.x + dx, y, Fridge.transform.position.z + .345f);
                var hits = Physics.RaycastAll(origin, Vector3.forward, 1.6f, ~0, QueryTriggerInteraction.Ignore)
                    .Where(h => h.collider.gameObject.scene == host.station.gameObject.scene && (h.collider.name == "SM_Wall_02" || h.collider.name == "SM_Wall_03")).OrderBy(h => h.distance).ToArray();
                if (hits.Length == 0) continue;
                distances.Add(hits[0].distance); lines.Add(origin.ToString("F4") + " -> " + hits[0].collider.name + " " + hits[0].distance.ToString("F6") + "m");
            }
            Assert.IsNotEmpty(distances, "Must measure the sink-side wall, not the window wall");
            Assert.That(distances.Min(), Is.InRange(.075f, .085f), "Reduced prototype wall gap must stay near 8cm");
            File.WriteAllLines(Folder + "sink-wall-clearance.txt", lines.Concat(new[] { "minimum=" + distances.Min().ToString("F6") + "m" }));
            yield return Pull(-105); FullOpen(); yield return Pull(-Angle(Door));
            Assert.Less(Mathf.Abs(Angle(Door)), .2f); Assert.AreEqual(0, Door.Progress); Assert.IsFalse(Fridge.Accessible);
        }
        [UnityTest] public IEnumerator NorthDoorSweepStopsAtAnObstacleAndResumesAfterRemoval()
        {
            var radial = Door.PullHandle.position - Door.transform.position; radial.y = 0;
            obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube); obstacle.name = "B north door obstruction";
            obstacle.transform.position = Door.transform.position + Quaternion.AngleAxis(-45, Vector3.up) * radial.normalized * .38f + Vector3.up;
            obstacle.transform.localScale = new Vector3(.10f, .32f, .10f); Physics.SyncTransforms();
            yield return Pull(-105); Assert.Less(Mathf.Abs(Angle(Door)), 90, "The original sweep must still reject obstacles");
            Object.Destroy(obstacle); yield return null;
            yield return Pull(-105 - Angle(Door)); FullOpen();
        }
        [UnityTest] public IEnumerator ClosedDoorGuardsStockAndTheRealGrabCanRetrieveItAfterOpening()
        {
            var stock = host.station.GetComponentsInChildren<KitchenStoredIngredient>()
                .Where(f => Fridge.Contains(f.transform.position) && !host.groceries.groceries.Any(g => g.gameObject == f.gameObject))
                .OrderByDescending(f => Fridge.transform.InverseTransformPoint(f.transform.position).x).First();
            var grab = stock.GetComponent<XRGrabInteractable>(); grab.interactionManager = manager;
            hand.transform.position = stock.transform.position;
            Assert.IsFalse(manager.IsSelectPossible((IXRSelectInteractor)hand, grab), "Cannot reach through a closed B door");
            yield return Pull(-105); FullOpen();
            hand.transform.position = stock.transform.position;
            Assert.IsTrue(manager.IsSelectPossible((IXRSelectInteractor)hand, grab));
            manager.SelectEnter((IXRSelectInteractor)hand, grab); yield return null; Assert.IsTrue(grab.isSelected);
            var attach = grab.GetAttachTransform(hand);
            var offset = stock.transform.InverseTransformPoint(attach.position);
            var rotation = stock.transform.rotation;
            var attachRotation = Quaternion.Inverse(rotation) * attach.rotation;
            var origin = stock.transform.position;
            foreach (var destination in new[] { origin + Vector3.up * .05f, origin + Vector3.up * .05f + Fridge.transform.forward * .95f })
            {
                var start = stock.transform.position;
                for (var i = 1; i <= 60; i++)
                { var p = Vector3.Lerp(start, destination, i / 60f); hand.transform.SetPositionAndRotation(p + rotation * offset, rotation * attachRotation); yield return null; yield return new WaitForFixedUpdate(); }
                Assert.That(Vector3.Distance(stock.transform.position, destination), Is.LessThan(.035f), "The held stock must actually clear the shelf and door");
            }
            Assert.IsFalse(Fridge.Contains(stock.transform.position)); Assert.IsTrue(stock.WasRetrieved);
            manager.SelectExit((IXRSelectInteractor)hand, grab); yield return null;
            yield return Pull(-Angle(Door)); Assert.IsFalse(Fridge.Accessible);
        }
        [UnityTest] public IEnumerator ReviewResetAndBtoAtoBRetainThenRestoreDoorAndFood()
        {
            var food = host.station.GetComponentsInChildren<KitchenStoredIngredient>().First();
            var start = food.transform.position; yield return Pull(-45);
            var angle = Angle(Door); var visit = host.Snapshot.SchemeVisitId; var beforeReview = food.transform.position;
            host.Execute(CommandType.Review); host.Execute(CommandType.Continue); yield return Frames(3);
            Assert.AreEqual(visit, host.Snapshot.SchemeVisitId); Assert.That(Angle(Door), Is.EqualTo(angle).Within(.001f)); Assert.AreEqual(beforeReview, food.transform.position);
            host.station.GetComponentInChildren<KitchenHeatSource>().SetOn(true);
            host.Execute(CommandType.ResetWorld);
            for (var i = 0; i < 120 && host.Snapshot.IsBusy; i++) yield return null;
            yield return Frames(8); Assert.Less(Mathf.Abs(Angle(Door)), .2f); Assert.AreEqual(0, Door.Progress);
            Assert.That(Vector3.Distance(food.transform.position, start), Is.LessThan(.01f)); Assert.IsFalse(host.station.GetComponentInChildren<KitchenHeatSource>().IsOn);
            yield return Pull(-105); FullOpen();
            host.Execute(CommandType.ChangeScheme, SchemeIds.IntegratedKitchen); yield return Frames(8);
            Assert.AreEqual("integrated-kitchen-m2-v5", host.Snapshot.ModelVersion);
            Assert.IsTrue(host.station.GetComponentInChildren<ReviewPanStoveSnap>().IsDocked);
            host.Execute(CommandType.ChangeScheme, SchemeIds.ExternalRefrigerator); yield return Frames(8);
            Assert.AreEqual("external-fridge-m2-v8", host.Snapshot.ModelVersion); Assert.AreEqual(0, Door.Progress);
            Assert.That(Vector3.Distance(Fridge.transform.position, new Vector3(-4.65f, 0, -.1f)), Is.LessThan(.00001f));
            Assert.IsFalse(host.station.GetComponentInChildren<KitchenHeatSource>().IsOn);
        }
        [UnityTest] public IEnumerator ExistingCapsuleCanCarryAnIngredientPastClosedAndOpenDoors()
        {
            var source = host.locomotion.Origin.GetComponent<CharacterController>(); Assert.IsNotNull(source);
            actor = new GameObject("B route capsule matching the existing rig");
            var capsule = actor.AddComponent<CharacterController>();
            capsule.height = source.height; capsule.radius = source.radius; capsule.center = source.center;
            capsule.skinWidth = source.skinWidth; capsule.stepOffset = source.stepOffset; capsule.slopeLimit = source.slopeLimit;
            var item = host.groceries.groceries[0]; item.interactionManager = manager;
            hand.transform.position = item.transform.position; manager.SelectEnter((IXRSelectInteractor)hand, item); yield return null;
            Assert.IsTrue(item.isSelected); var attach = item.GetAttachTransform(hand);
            var grip = item.transform.InverseTransformPoint(attach.position); var gripRotation = Quaternion.Inverse(item.transform.rotation) * attach.rotation;
            var start = hand.transform.position; var carry = new Vector3(-3.32f, 1.25f, -1.25f);
            for (var i = 1; i <= 40; i++) { hand.transform.SetPositionAndRotation(Vector3.Lerp(start, carry + grip, i / 40f), gripRotation); yield return null; }
            var waypoints = new[] { new Vector3(-3.6f, 0, -1.25f), new Vector3(-3.6f, 0, -.2f), new Vector3(-3.1f, 0, -.2f), new Vector3(-3.1f, 0, .9f), new Vector3(-3.1f, 0, 1.35f) };
            for (var state = 0; state < 2; state++)
            {
                if (state == 1) { yield return Pull(-105); FullOpen(); }
                if (state == 0) actor.transform.position = waypoints[0]; Physics.SyncTransforms();
                foreach (var point in waypoints.Concat(waypoints.Reverse()))
                {
                    var from = actor.transform.position;
                    for (var step = 1; step <= 80; step++)
                    {
                        var requested = Vector3.Lerp(from, point, step / 80f);
                        capsule.Move(requested - actor.transform.position + Vector3.down * .002f);
                        var p = actor.transform.position + new Vector3(.28f, 1.25f, 0);
                        hand.transform.SetPositionAndRotation(p + grip, gripRotation); yield return null; yield return new WaitForFixedUpdate();
                        Assert.IsTrue(item.isSelected); Assert.That(Vector3.Distance(item.transform.position, p), Is.LessThan(.045f), "Carried ingredient blocked at " + p);
                    }
                    Assert.That(Vector2.Distance(new Vector2(actor.transform.position.x, actor.transform.position.z), new Vector2(point.x, point.z)), Is.LessThan(.04f), "Capsule blocked toward " + point + " at " + actor.transform.position);
                }
            }
            manager.SelectExit((IXRSelectInteractor)hand, item);
            File.WriteAllText(Folder + "route-result.txt", "Existing rig capsule and real XRI-held ingredient: table west approach -> refrigerator front -> east-side kitchen entrance -> worktop; closed/open105 passed; radius=" + source.radius + ", height=" + source.height + ", skin=" + source.skinWidth);
        }
        IEnumerator Capture(Camera camera, RenderTexture texture, string name)
        {
            for (var i = 0; i < 30; i++) yield return null;
            var previous = RenderTexture.active; RenderTexture.active = texture;
            var pixels = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
            try { pixels.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); pixels.Apply(); File.WriteAllBytes(Folder + name, pixels.EncodeToPNG()); }
            finally { RenderTexture.active = previous; Object.Destroy(pixels); }
        }
        [UnityTest] public IEnumerator CaptureTheActualClosedOpenAndTopViews()
        {
            foreach (var panel in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ReviewIntegrationPanel>(true))) panel.SetOpen(false);
            cameraObject = new GameObject("B north acceptance camera"); var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 70; camera.nearClipPlane = .03f; camera.farClipPlane = 30;
            var texture = new RenderTexture(1440, 1080, 24); texture.Create(); camera.targetTexture = texture;
            try
            {
                camera.transform.position = new Vector3(-2.9f, 1.65f, -.8f); camera.transform.LookAt(new Vector3(-4.5f, 1.0f, .12f));
                yield return Capture(camera, texture, "01-closed.png");
                yield return Pull(-105); FullOpen(); yield return Capture(camera, texture, "02-open.png");
                yield return Pull(-Angle(Door)); Assert.Less(Mathf.Abs(Angle(Door)), .2f); Assert.AreEqual(0, Door.Progress);
                camera.transform.position = new Vector3(-3.25f, 2.60f, .15f); camera.transform.rotation = Quaternion.Euler(90, 0, 0);
                camera.orthographic = true; camera.orthographicSize = 2.6f;
                var top = new RenderTexture(1080, 1440, 24); top.Create(); camera.targetTexture = top;
                try { yield return Capture(camera, top, "03-top.png"); }
                finally { camera.targetTexture = texture; top.Release(); Object.Destroy(top); }
            }
            finally { camera.targetTexture = null; texture.Release(); Object.Destroy(texture); }
        }
    }
}
#endif
