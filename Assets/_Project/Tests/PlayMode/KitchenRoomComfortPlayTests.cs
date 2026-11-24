using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using XRApartment.Runtime;
using XRApartment.Runtime.UI;

namespace XRApartment.Tests
{
    public sealed class KitchenRoomComfortPlayTests
    {
        LocomotionSettingsView menu;
        KitchenLocomotionSettings movement;

        [UnitySetUp]
        public IEnumerator LoadRoom()
        {
            yield return UnloadKitchenIfLoaded("ComfortSetupScratch");
            SceneManager.LoadScene("SCN_Kitchen_Cooking");
            yield return null;
            yield return null;
            menu = Object.FindObjectOfType<LocomotionSettingsView>();
            movement = menu.GetComponentInParent<KitchenLocomotionSettings>();
        }

        [UnityTearDown]
        public IEnumerator CloseMenu()
        {
            LanguageSelectionView.Hide();
            if (menu != null) menu.Close();
            Time.timeScale = 1f;
            yield return UnloadKitchenIfLoaded("ComfortTeardownScratch");
        }

        static IEnumerator UnloadKitchenIfLoaded(string placeholderName)
        {
            const string scenePath = "Assets/_Project/Scenes/SCN_Kitchen_Cooking.unity";
            var kitchen = SceneManager.GetSceneByPath(scenePath);
            if (!kitchen.IsValid() || !kitchen.isLoaded) yield break;
            var placeholder = SceneManager.GetSceneByName(placeholderName);
            if (!placeholder.IsValid()) placeholder = SceneManager.CreateScene(placeholderName);
            SceneManager.SetActiveScene(placeholder);
            yield return SceneManager.UnloadSceneAsync(kitchen);
        }

        [UnityTest]
        public IEnumerator FridgeChooserStaysReadableAndClickableInFrontOfAnObstacle()
        {
            KitchenSceneTestSetup.DisableStartupGuide(menu);
            var camera = movement.Origin.Camera;
            var obstacle = new GameObject("Cabinet face", typeof(BoxCollider));
            obstacle.transform.SetPositionAndRotation(camera.transform.TransformPoint(0f, 0f, .59f),
                camera.transform.rotation);
            var box = obstacle.GetComponent<BoxCollider>();
            box.size = new Vector3(1.1f, 1.8f, .4f);
            Physics.SyncTransforms();
            try
            {
                using (var keyboard = new KitchenMenuKeyboard()) yield return keyboard.Hold();
                KitchenSceneTestSetup.Click(menu, menu.transform.Find("PREF_UI_KitchenActivities/PauseMenu/Navigation/LayoutLab"));
                yield return null;
                var panel = (RectTransform)menu.GetComponentInChildren<KitchenLayoutMenu>().transform;
                var corners = new Vector3[4];
                panel.GetWorldCorners(corners);
                foreach (var corner in corners)
                {
                    var ray = corner - camera.transform.position;
                    Assert.IsFalse(box.Raycast(new Ray(camera.transform.position, ray.normalized), out _, ray.magnitude),
                        "The cabinet obscures part of the selection panel.");
                }
                var lab = Object.FindObjectOfType<KitchenLayoutLab>();
                Assert.IsFalse(lab.CompactSelected);
                KitchenSceneTestSetup.Click(menu, panel.Find("CompactFridge"));
                Assert.IsTrue(lab.CompactSelected);
                var station = Object.FindObjectOfType<KitchenCookingStation>();
                for (var frame = 0; frame < 5 && station != null && station.IsResetting; frame++) yield return null;
                menu.Close();
            }
            finally { Object.Destroy(obstacle); }
        }

        [UnityTest]
        public IEnumerator HeadTrackingCannotCrossEitherWindowAndRecoversWhenLeaningBack()
        {
            KitchenSceneTestSetup.DisableStartupGuide(menu);
            var head = movement.Origin.Camera.transform;
            if (head.TryGetComponent<TrackedPoseDriver>(out var driver)) driver.enabled = false;
            var windows = GameObject.Find("PREF_Kitchen_Windows").transform;
            foreach (var path in new[] { "Window_Dining/Glass_Right", "Window_Sink/Glass" })
            {
                var pane = windows.Find(path).GetComponent<BoxCollider>();
                var inside = pane.bounds.center + Vector3.right * .4f;
                var outside = pane.bounds.center + Vector3.left * .16f;
                var body = new Vector3(pane.bounds.center.x + .65f, 0f, pane.bounds.center.z);
                movement.transform.position = body;
                head.position = inside;
                yield return null;
                var trackedInside = head.localPosition;
                head.position = outside;
                yield return null;
                yield return null;
                Assert.That(head.position.x, Is.GreaterThan(pane.bounds.max.x), path + " let the tracked view through the glass.");
                Assert.That(head.position.y, Is.EqualTo(outside.y).Within(.01f));
                Assert.That(movement.transform.position, Is.EqualTo(body));
                head.localPosition = trackedInside;
                yield return null;
                yield return null;
                Assert.That(Vector3.Distance(head.position, inside), Is.LessThan(.015f));
            }
        }

        [UnityTest]
        public IEnumerator LivingRoomOpeningHasVisibleSealedGlazing()
        {
            var window = GameObject.Find("PREF_Cooking_LivingRoomWindow");
            Assert.NotNull(window, "The sofa-side opening needs its own glazing.");
            Assert.NotNull(window.transform.Find("Frame_Mullion"));
            var panes = window.GetComponentsInChildren<MeshRenderer>()
                .Where(renderer => renderer.name.StartsWith("Glass_")).ToArray();
            Assert.That(panes.Length, Is.EqualTo(2));
            foreach (var pane in panes)
            {
                Assert.IsTrue(pane.enabled && pane.gameObject.activeInHierarchy);
                Assert.That(pane.sharedMaterial.color.a, Is.InRange(.23f, .35f));
                Assert.That(pane.sharedMaterial.renderQueue, Is.GreaterThanOrEqualTo(3000));
                var collider = pane.GetComponent<BoxCollider>();
                Assert.IsTrue(collider.enabled && !collider.isTrigger);
                foreach (var height in new[] { 1.05f, 1.95f })
                {
                    var origin = new Vector3(-4.09f, height, collider.bounds.center.z);
                    Assert.IsTrue(collider.Raycast(new Ray(origin, Vector3.left), out _, .67f));
                }
            }
            var seam = new Vector3(-4.09f, .16f, window.transform.position.z);
            Assert.IsTrue(panes.Any(pane => pane.GetComponent<BoxCollider>()
                .Raycast(new Ray(seam, Vector3.left), out _, .67f)));
            yield return null;
        }

        [UnityTest]
        public IEnumerator TrackedHeadCannotLeaveTheLivingRoomWindowAndRecoversOnRetreat()
        {
            KitchenSceneTestSetup.DisableStartupGuide(menu);
            var head = movement.Origin.Camera.transform;
            if (head.TryGetComponent<TrackedPoseDriver>(out var driver)) driver.enabled = false;
            movement.transform.position = new Vector3(-3.88f, 0f, -4.25f);
            head.position = new Vector3(-4.15f, 1.65f, -4.25f);
            yield return null;
            var inside = head.localPosition;
            head.position = new Vector3(-5.1f, 1.65f, -4.25f);
            yield return null;
            Assert.That(head.position.x, Is.GreaterThan(-4.56f));
            head.localPosition = inside;
            yield return null;
            Assert.That(head.position.x, Is.EqualTo(-4.15f).Within(.01f));
        }

        [UnityTest]
        public IEnumerator DiagonalTrackingStepAcrossAWindowKeepsTheViewInside()
        {
            KitchenSceneTestSetup.DisableStartupGuide(menu);
            var head = movement.Origin.Camera.transform;
            if (head.TryGetComponent<TrackedPoseDriver>(out var driver)) driver.enabled = false;
            var pane = GameObject.Find("PREF_Kitchen_Windows").transform.Find("Window_Sink/Glass")
                .GetComponent<BoxCollider>();
            var bounds = pane.bounds;
            movement.transform.position = new Vector3(bounds.max.x + .60f, 0f, bounds.center.z);
            head.position = new Vector3(bounds.max.x + .15f, bounds.center.y, bounds.max.z - .08f);
            yield return null;
            var inside = head.localPosition;
            head.position = new Vector3(bounds.max.x - .45f, bounds.center.y, bounds.max.z + .08f);
            yield return null;
            Assert.That(head.position.x, Is.GreaterThan(bounds.max.x));
            head.localPosition = inside;
            yield return null;
            Assert.That(head.position.x, Is.EqualTo(bounds.max.x + .15f).Within(.01f));
        }

        [UnityTest]
        public IEnumerator SeatedHeightRaisesTheViewAndControllersTogether()
        {
            var head = movement.Origin.Camera.transform;
            if (head.TryGetComponent<TrackedPoseDriver>(out var driver)) driver.enabled = false;
            head.position = new Vector3(head.position.x, 1.12f, head.position.z);
            KitchenSceneTestSetup.ChooseStartupLanguage(menu);
            yield return null;
            Assert.That(movement.Origin.CameraInOriginSpaceHeight, Is.EqualTo(1.68f).Within(.02f),
                "The virtual standing view is too low for a seated starting pose.");

            head.localPosition -= Vector3.up * .16f;
            var controller = movement.GetComponentsInChildren<NearFarInteractor>(true).First().transform;
            var relativeHand = controller.position - head.position;
            var floor = movement.transform.position;
            menu.Open();
            yield return null;
            var seated = menu.transform.Find("Panel/SeatedHeight");
            Assert.IsNotNull(seated, "Movement options need a seated height alignment action.");
            KitchenSceneTestSetup.Click(menu, seated);
            Assert.That(movement.Origin.CameraInOriginSpaceHeight, Is.EqualTo(1.68f).Within(.02f));
            Assert.That(Vector3.Distance(relativeHand, controller.position - head.position), Is.LessThan(.015f));
            Assert.That(movement.Character.height, Is.EqualTo(1.68f).Within(.03f));
            Assert.That(movement.transform.position, Is.EqualTo(floor));

            // Real head movement remains relative to the calibrated posture.
            head.localPosition += Vector3.up * .1f;
            yield return null;
            Assert.That(movement.Origin.CameraInOriginSpaceHeight, Is.EqualTo(1.78f).Within(.02f));
        }

        [UnityTest]
        public IEnumerator PhysicalRadioStartsQuietlyAndSupportsPowerAndVolume()
        {
            KitchenSceneTestSetup.DisableStartupGuide(menu);
            var radio = GameObject.Find("PREF_Cooking_Radio");
            Assert.IsNotNull(radio, "A physical radio prefab is missing from the room.");
            var audio = radio.GetComponent<AudioSource>();
            var lamp = radio.transform.Find("PowerLight").GetComponent<Renderer>();
            var power = Button("Power");
            var volumeDown = Button("VolumeDown");
            var volumeUp = Button("VolumeUp");
            Assert.IsNotNull(audio.clip);
            Assert.IsTrue(audio.loop);
            Assert.IsFalse(audio.playOnAwake);
            Assert.That(audio.spatialBlend, Is.EqualTo(1f));
            Assert.IsTrue(lamp.enabled, "The radio starts powered at background volume.");
            var quiet = audio.volume;
            Assert.That(quiet, Is.EqualTo(.08f).Within(.001f), "The radio opens at a background listening level.");

            using (var keyboard = new KitchenMenuKeyboard())
            {
                var controller = InputSystem.AddDevice<XRSimulatedController>();
                try
                {
                    InputSystem.SetDeviceUsage(controller, CommonUsages.LeftHand);
                    InputSystem.QueueStateEvent(controller, new XRSimulatedControllerState
                    {
                        isTracked = true, trackingState = 3, deviceRotation = Quaternion.identity
                    });
                    for (var frame = 0; frame < 4; frame++) yield return null;
                    var ray = movement.GetComponentsInChildren<NearFarInteractor>().First();
                    ray.selectInput.inputSourceMode = XRInputButtonReader.InputSourceMode.ManualValue;
                    ray.selectInput.manualPerformed = true;
                    Select(ray, power);
                    Assert.IsFalse(lamp.enabled);
                    Select(ray, power);
                    Assert.IsTrue(lamp.enabled);
                    Select(ray, volumeUp);
                    Assert.That(audio.volume, Is.GreaterThan(quiet));
                    Select(ray, volumeDown);
                    Assert.That(audio.volume, Is.EqualTo(quiet).Within(.001f));
                    Select(ray, power);
                    Assert.IsFalse(lamp.enabled);
                    Select(ray, power);
                    var room = Object.FindObjectOfType<KitchenRoomReset>();
                    room.ResetRoom();
                    Assert.IsTrue(lamp.enabled, "Reset restores default-on playback.");
                    Assert.That(audio.volume, Is.EqualTo(quiet).Within(.001f));
                    for (var frame = 0; frame < 5 && room.IsResetting; frame++) yield return null;
                    Assert.IsFalse(room.IsResetting);
                }
                finally { InputSystem.RemoveDevice(controller); }
            }

            XRSimpleInteractable Button(string path)
            {
                var button = radio.transform.Find(path).GetComponent<XRSimpleInteractable>();
                Assert.That(button.colliders.Count, Is.GreaterThan(0), path);
                var front = button.transform.position + radio.transform.forward * .5f;
                Assert.IsTrue(Physics.Raycast(front, -radio.transform.forward, out var hit, .52f));
                Assert.Contains(hit.collider, button.colliders, path + " must be exposed on the front face.");
                return button;
            }

            void Select(NearFarInteractor ray, XRSimpleInteractable button)
            {
                Assert.IsTrue(((IXRSelectInteractor)ray).CanSelect(button), button.name);
                ray.interactionManager.SelectEnter((IXRSelectInteractor)ray, button);
                Assert.IsTrue(button.isSelected);
                ray.interactionManager.SelectExit((IXRSelectInteractor)ray, button);
            }
        }
    }
}
