using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;
using XRApartment.Runtime;
using XRApartment.Runtime.UI;

namespace XRApartment.Tests
{
    internal static class KitchenSceneTestSetup
    {
        // Subsystem fixtures run the regular room and rig without replaying the whole tutorial.
        internal static void DisableStartupGuide(LocomotionSettingsView menu)
        {
            var guide = menu.GetComponentInChildren<KitchenActivityGuide>(true);
            if (guide == null) return;
            guide.enabled = false;
            menu.Close();
        }

        internal static void Click(LocomotionSettingsView menu, Transform button)
        {
            Canvas.ForceUpdateCanvases();
            var origin = menu.GetComponent<Canvas>().worldCamera.transform.position;
            var aim = button.position;
            var pointer = new TrackedDeviceEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                layerMask = ~0,
                rayPoints = new List<Vector3> { origin, aim + (aim - origin).normalized * .15f }
            };
            var hits = new List<RaycastResult>();
            menu.GetComponent<TrackedDeviceGraphicRaycaster>().Raycast(pointer, hits);
            Assert.That(hits.Select(hit => hit.gameObject), Does.Contain(button.gameObject), button.name);
            Assert.IsTrue(button.GetComponent<Button>().interactable, button.name);
            ExecuteEvents.Execute<IPointerClickHandler>(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }
    }

    internal sealed class KitchenMenuKeyboard : System.IDisposable
    {
        readonly InputSettings.BackgroundBehavior background = InputSystem.settings.backgroundBehavior;
        readonly InputSettings.EditorInputBehaviorInPlayMode focus = InputSystem.settings.editorInputBehaviorInPlayMode;
        readonly Keyboard keyboard;

        internal KitchenMenuKeyboard()
        {
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>();
        }

        internal IEnumerator Hold()
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F8));
            for (var i = 0; i < 5 && !keyboard.f8Key.isPressed; i++) yield return null;
            Assert.IsTrue(keyboard.f8Key.isPressed);
            yield return new WaitForSecondsRealtime(.72f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            for (var i = 0; i < 5 && keyboard.f8Key.isPressed; i++) yield return null;
            Assert.IsFalse(keyboard.f8Key.isPressed);
            yield return null;
        }

        public void Dispose()
        {
            InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.backgroundBehavior = background;
            InputSystem.settings.editorInputBehaviorInPlayMode = focus;
        }
    }

    public sealed class KitchenPauseMenuPlayTests
    {
        [UnityTest]
        public IEnumerator StartsInCalibrationAndLeftControllerHoldPausesAndRestoresTheWorld()
        {
            SceneManager.LoadScene(0);
            yield return null;
            yield return null;
            var menu = Object.FindObjectOfType<LocomotionSettingsView>();
            var guide = menu.GetComponentInChildren<KitchenActivityGuide>(true);
            var pause = menu.GetComponentInChildren<KitchenPauseMenu>(true);
            var guidePanel = guide.transform.Find("Guide");
            var pausePanel = pause.transform.Find("PauseMenu");
            var nav = pausePanel.Find("Navigation");
            var movement = menu.GetComponentInParent<KitchenLocomotionSettings>();
            var cards = Object.FindObjectOfType<KitchenRecipeController>().GetComponentsInChildren<Canvas>();
            Assert.AreEqual("SCN_Kitchen_Cooking", SceneManager.GetActiveScene().name);
            Assert.AreEqual(2, cards.Length);
            Assert.IsTrue(cards.All(card => !card.enabled));
            Assert.IsTrue(guidePanel.gameObject.activeInHierarchy);
            Assert.IsFalse(guide.IsFreePlay);
            Assert.IsFalse(guidePanel.Find("CloseGuide").gameObject.activeSelf);
            Assert.AreEqual(0f, Time.timeScale);

            var background = InputSystem.settings.backgroundBehavior;
            var focus = InputSystem.settings.editorInputBehaviorInPlayMode;
            var controller = InputSystem.AddDevice<XRSimulatedController>();
            InputSystem.SetDeviceUsage(controller, CommonUsages.LeftHand);
            var state = new XRSimulatedControllerState { isTracked = true, trackingState = 3, deviceRotation = Quaternion.identity };
            GameObject falling = null;
            try
            {
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings.editorInputBehaviorInPlayMode =
                    InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;

                yield return HoldControllerMenu(controller, state, .12f);
                Assert.IsFalse(pausePanel.gameObject.activeSelf, "A tap must leave the current view alone.");
                yield return HoldControllerMenu(controller, state, .70f);
                Assert.IsTrue(pausePanel.gameObject.activeInHierarchy);
                Assert.IsFalse(nav.Find("LayoutLab").GetComponent<Button>().interactable);
                Assert.IsFalse(nav.Find("FeedbackNote").GetComponent<Button>().interactable);
                Assert.AreEqual("Return to guide", KitchenLocale.Source(nav.Find("Resume").GetComponentInChildren<TMP_Text>()));
                KitchenSceneTestSetup.Click(menu, nav.Find("Resume"));
                Assert.IsTrue(guidePanel.gameObject.activeInHierarchy);
                Assert.AreEqual(0f, Time.timeScale);
                yield return null;
                KitchenSceneTestSetup.Click(menu, guidePanel.Find("Continue"));
                yield return null;
                KitchenSceneTestSetup.Click(menu, guidePanel.Find("Continue"));
                Assert.IsFalse(menu.IsOpen);
                Assert.IsFalse(pause.IsPaused);
                Assert.AreEqual(1f, Time.timeScale);
                Assert.IsTrue(cards.All(card => card.enabled));

                var ray = movement.GetComponentsInChildren<NearFarInteractor>().First();
                var cup = GameObject.Find("PREF_Prop_Cup").GetComponent<XRGrabInteractable>();
                Assert.IsTrue(((IXRSelectInteractor)ray).CanSelect(cup));
                var nearCasting = ray.enableNearCasting;
                var throwOnDetach = cup.throwOnDetach;
                ray.keepSelectedTargetValid = true;
                ray.selectInput.inputSourceMode = XRInputButtonReader.InputSourceMode.ManualValue;
                ray.selectInput.manualPerformed = true;
                ray.interactionManager.SelectEnter((IXRSelectInteractor)ray, cup);
                Assert.IsTrue(cup.isSelected);
                Time.timeScale = .8f;
                InputSystem.QueueStateEvent(controller, state.WithButton(ControllerButton.MenuButton));
                yield return null;
                Assert.IsTrue(controller.menuButton.isPressed);
                yield return new WaitForSecondsRealtime(.23f);
                Assert.IsTrue(cup.isSelected, "The prop must be held until the menu opens.");
                yield return new WaitForSecondsRealtime(.52f);
                Assert.IsTrue(pause.IsPaused && pausePanel.gameObject.activeInHierarchy);
                Assert.IsFalse(cup.isSelected);
                Assert.AreEqual(throwOnDetach, cup.throwOnDetach);
                Assert.AreEqual(Vector3.zero, cup.GetComponent<Rigidbody>().velocity);
                ray.selectInput.manualPerformed = false;
                var head = movement.Origin.Camera.transform;
                Assert.That(Vector3.Distance(head.position, pausePanel.position), Is.EqualTo(.95f).Within(.05f));
                Assert.That(Vector3.Dot(head.forward, pausePanel.forward), Is.GreaterThan(.98f));
                Assert.IsTrue(AudioListener.pause);
                Assert.IsTrue(cards.All(card => !card.enabled));
                Assert.IsFalse(ray.enableNearCasting);
                Assert.IsFalse(((IXRSelectInteractor)ray).CanSelect(cup));
                Assert.IsFalse(movement.TeleportProvider.enabled);

                var sceneTime = Time.time;
                falling = new GameObject("Paused physics test");
                falling.transform.position = new Vector3(0f, 2f, 0f);
                var body = falling.AddComponent<Rigidbody>();
                var initial = body.position;
                yield return new WaitForSecondsRealtime(.25f);
                Assert.AreEqual(sceneTime, Time.time);
                Assert.AreEqual(initial, body.position);
                Assert.IsTrue(pausePanel.gameObject.activeSelf, "Holding the button must fire only once.");
                InputSystem.QueueStateEvent(controller, state.WithButton(ControllerButton.MenuButton, false));
                yield return null;

                var anchor = pausePanel.position;
                KitchenSceneTestSetup.Click(menu, nav.Find("Options"));
                yield return null;
                Assert.AreEqual(anchor, menu.transform.Find("Panel").position);
                Assert.IsTrue(pause.IsPaused);
                KitchenSceneTestSetup.Click(menu, menu.transform.Find("Panel/Close"));
                yield return null;
                Assert.AreEqual(anchor, pausePanel.position);
                yield return HoldControllerMenu(controller, state, .70f);
                Assert.IsFalse(menu.IsOpen);
                Assert.AreEqual(.8f, Time.timeScale);
                Assert.IsFalse(AudioListener.pause);
                Assert.IsTrue(cards.All(card => card.enabled));
                Assert.AreEqual(nearCasting, ray.enableNearCasting);
                Assert.IsTrue(((IXRSelectInteractor)ray).CanSelect(cup));
                Assert.IsTrue(movement.TeleportProvider.enabled);
                yield return new WaitForSecondsRealtime(.20f);
                Assert.That(body.position.y, Is.LessThan(initial.y));
            }
            finally
            {
                menu.Close();
                Time.timeScale = 1f;
                AudioListener.pause = false;
                if (falling != null) Object.Destroy(falling);
                InputSystem.RemoveDevice(controller);
                InputSystem.settings.backgroundBehavior = background;
                InputSystem.settings.editorInputBehaviorInPlayMode = focus;
            }
        }

        [UnityTest]
        public IEnumerator GraphicsQualitySwitchesTheRenderPipelineWhilePaused()
        {
            var oldQuality = QualitySettings.GetQualityLevel();
            var oldPreference = PlayerPrefs.GetInt(KitchenGraphicsMenu.Preference, -1);
            LocomotionSettingsView menu = null;
            using (var keyboard = new KitchenMenuKeyboard())
            {
                try
                {
                    QualitySettings.SetQualityLevel(KitchenGraphicsMenu.Standard, true);
                    SceneManager.LoadScene(0);
                    yield return null;
                    yield return null;
                    menu = Object.FindObjectOfType<LocomotionSettingsView>();
                    var graphics = menu.GetComponentInChildren<KitchenGraphicsMenu>(true);
                    var pause = menu.GetComponentInChildren<KitchenPauseMenu>(true);
                    var navigation = pause.transform.Find("PauseMenu/Navigation");
                    var panel = graphics.transform.Find("GraphicsQuality");
                    var status = panel.Find("Status").GetComponent<TMP_Text>();
                    var camera = menu.GetComponent<Canvas>().worldCamera;
                    var cameraData = camera.GetComponent<HDAdditionalCameraData>();
                    Assert.IsTrue(cameraData.renderingPathCustomFrameSettings.IsEnabled(FrameSettingsField.RayTracing));
                    var volume = Object.FindObjectsOfType<Volume>().Single(value => value.sharedProfile.name == "VOL_KitchenCooking");
                    Assert.IsTrue(volume.sharedProfile.TryGet(out ScreenSpaceReflection reflection));
                    Assert.AreEqual(RayCastingMode.RayTracing, reflection.tracing.value);
                    Assert.IsTrue(reflection.enabled.value);

                    yield return keyboard.Hold();
                    KitchenSceneTestSetup.Click(menu, navigation.Find("Graphics"));
                    yield return null;
                    Assert.IsTrue(panel.gameObject.activeInHierarchy && pause.IsPaused);
                    Assert.AreEqual(0f, Time.timeScale);
                    Assert.IsFalse(panel.Find("Standard").GetComponent<Button>().interactable);

                    foreach (var selection in new[]
                    {
                        (button: "Performance", quality: KitchenGraphicsMenu.Performance),
                        (button: "RayTracing", quality: KitchenGraphicsMenu.RayTracing),
                        (button: "Standard", quality: KitchenGraphicsMenu.Standard)
                    })
                    {
                        KitchenSceneTestSetup.Click(menu, panel.Find(selection.button));
                        yield return null;
                        yield return null;
                        Assert.AreEqual(selection.quality, QualitySettings.GetQualityLevel());
                        Assert.AreEqual(selection.quality, PlayerPrefs.GetInt(KitchenGraphicsMenu.Preference));
                        Assert.IsFalse(panel.Find(selection.button).GetComponent<Button>().interactable);
                        Assert.IsTrue(pause.IsPaused && panel.gameObject.activeInHierarchy);
                        Assert.AreEqual(0f, Time.timeScale);

                        var asset = (HDRenderPipelineAsset)QualitySettings.renderPipeline;
                        Assert.AreEqual(selection.quality == KitchenGraphicsMenu.RayTracing,
                            asset.currentPlatformRenderPipelineSettings.supportRayTracing);
                        Assert.AreEqual(selection.quality != KitchenGraphicsMenu.Performance,
                            asset.currentPlatformRenderPipelineSettings.supportSSR);
                        if (selection.quality == KitchenGraphicsMenu.RayTracing &&
                            (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Direct3D12 || !SystemInfo.supportsRayTracing))
                        {
                            Assert.AreEqual("High quality. Ray tracing requires a DirectX 12 ray tracing GPU.", KitchenLocale.Source(status));
                            var target = RenderTexture.GetTemporary(256, 256, 24);
                            var previous = camera.targetTexture;
                            try
                            {
                                camera.targetTexture = target;
                                camera.Render();
                                Assert.That(RenderPipelineManager.currentPipeline, Is.TypeOf<HDRenderPipeline>());
                                Assert.IsFalse(((HDRenderPipeline)RenderPipelineManager.currentPipeline).rayTracingSupported);
                            }
                            finally
                            {
                                camera.targetTexture = previous;
                                RenderTexture.ReleaseTemporary(target);
                            }
                        }
                    }
                    KitchenSceneTestSetup.Click(menu, panel.Find("Back"));
                    Assert.IsTrue(navigation.gameObject.activeInHierarchy && pause.IsPaused);
                }
                finally
                {
                    if (menu != null) menu.Close();
                    QualitySettings.SetQualityLevel(oldQuality, true);
                    if (oldPreference >= 0) PlayerPrefs.SetInt(KitchenGraphicsMenu.Preference, oldPreference);
                    else PlayerPrefs.DeleteKey(KitchenGraphicsMenu.Preference);
                    PlayerPrefs.Save();
                }
            }
        }

        static IEnumerator HoldControllerMenu(XRSimulatedController controller, XRSimulatedControllerState state, float seconds)
        {
            InputSystem.QueueStateEvent(controller, state.WithButton(ControllerButton.MenuButton));
            yield return null;
            Assert.IsTrue(controller.menuButton.isPressed);
            yield return new WaitForSecondsRealtime(seconds);
            InputSystem.QueueStateEvent(controller, state.WithButton(ControllerButton.MenuButton, false));
            yield return null;
            yield return null;
            Assert.IsFalse(controller.menuButton.isPressed);
        }
    }
}
