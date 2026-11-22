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
            button.GetComponentInParent<TrackedDeviceGraphicRaycaster>().Raycast(pointer, hits);
            Assert.That(hits.Select(hit => hit.gameObject), Does.Contain(button.gameObject), button.name);
            Assert.IsTrue(button.GetComponent<Button>().interactable, button.name);
            ExecuteEvents.Execute<IPointerClickHandler>(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }

        internal static void ChooseStartupLanguage(LocomotionSettingsView menu)
        {
            var picker = Object.FindObjectOfType<LanguageSelectionView>();
            Assert.IsNotNull(picker);
            var option = KitchenLocale.Current switch
            {
                KitchenLocale.Language.SimplifiedChinese => "Simplified Chinese",
                KitchenLocale.Language.TraditionalChinese => "Traditional Chinese",
                _ => "English"
            };
            Click(menu, picker.transform.Find("Language Selection Canvas/Panel/" + option));
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
        public IEnumerator StartsInLanguageSelectionAndLeftControllerHoldPausesAndRestoresTheWorld()
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
            Assert.IsFalse(guidePanel.gameObject.activeInHierarchy);
            Assert.IsNotNull(GameObject.Find("Language Selection Canvas"));
            Assert.IsFalse(guide.IsFreePlay);
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
                Assert.IsFalse(pausePanel.gameObject.activeInHierarchy);
                Assert.IsNotNull(GameObject.Find("Language Selection Canvas"));
                Assert.AreEqual(0f, Time.timeScale);
                KitchenSceneTestSetup.ChooseStartupLanguage(menu);
                Assert.IsFalse(menu.IsOpen);
                Assert.IsFalse(pause.IsPaused);
                Assert.AreEqual(1f, Time.timeScale);
                Assert.IsTrue(cards.All(card => card.enabled));
                yield return HoldControllerMenu(controller, state, .18f);
                Assert.IsNull(GameObject.Find("Menu hold indicator"), "Releasing early must clear the indicator.");
                Assert.IsFalse(menu.IsOpen);

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
                var indicator = GameObject.Find("Menu hold indicator");
                Assert.IsNotNull(indicator, "A pending menu hold must show the circular progress indicator.");
                Assert.IsFalse(indicator.GetComponent<Graphic>().raycastTarget);
                var indicatorInView = movement.Origin.Camera.WorldToViewportPoint(indicator.transform.position);
                Assert.That(indicatorInView.x, Is.InRange(.35f, .65f));
                Assert.That(indicatorInView.y, Is.InRange(.35f, .75f));
                yield return new WaitForSecondsRealtime(.52f);
                Assert.IsFalse(indicator.activeSelf, "The indicator must hide as soon as the menu opens.");
                Assert.IsTrue(pause.IsPaused && pausePanel.gameObject.activeInHierarchy);
                Assert.IsTrue(nav.Find("FeedbackNote").GetComponent<Button>().interactable);
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
                yield return HoldControllerMenu(controller, state, .70f, ControllerButton.SecondaryButton);
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
        public IEnumerator OnlyTheLeftStickClickTogglesCrouchAndPreservesTrackedOffsets()
        {
            SceneManager.LoadScene(0);
            yield return null;
            yield return null;
            var menu = Object.FindObjectOfType<LocomotionSettingsView>();
            var movement = menu.GetComponentInParent<KitchenLocomotionSettings>();
            var headset = movement.Origin.Camera.transform;
            var hand = movement.GetComponentsInChildren<NearFarInteractor>(true).First().transform;
            var floor = movement.transform.position;
            var standing = headset.position.y;
            var background = InputSystem.settings.backgroundBehavior;
            var focus = InputSystem.settings.editorInputBehaviorInPlayMode;
            var left = InputSystem.AddDevice<XRSimulatedController>();
            var right = InputSystem.AddDevice<XRSimulatedController>();
            var idle = new XRSimulatedControllerState { isTracked = true, trackingState = 3, deviceRotation = Quaternion.identity };
            var clicked = idle;
            clicked.WithButton(ControllerButton.Primary2DAxisClick);
            try
            {
                InputSystem.SetDeviceUsage(left, CommonUsages.LeftHand);
                InputSystem.SetDeviceUsage(right, CommonUsages.RightHand);
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings.editorInputBehaviorInPlayMode =
                    InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                InputSystem.QueueStateEvent(left, clicked);
                yield return null;
                Assert.That(headset.position.y, Is.EqualTo(standing).Within(.01f), "The language picker must guard stance input.");
                InputSystem.QueueStateEvent(left, idle);
                yield return null;
                KitchenSceneTestSetup.ChooseStartupLanguage(menu);
                InputSystem.QueueStateEvent(right, clicked);
                yield return null;
                Assert.That(headset.position.y, Is.EqualTo(standing).Within(.01f));
                InputSystem.QueueStateEvent(right, idle);
                yield return null;

                var handOffset = hand.position - headset.position;
                InputSystem.QueueStateEvent(left, clicked);
                yield return null;
                var crouched = headset.position.y;
                Assert.That(standing - crouched, Is.EqualTo(.5f).Within(.02f));
                Assert.That(movement.transform.position, Is.EqualTo(floor));
                Assert.That(Vector3.Distance(handOffset, hand.position - headset.position), Is.LessThan(.02f));
                Assert.That(movement.Character.height, Is.EqualTo(movement.Origin.CameraInOriginSpaceHeight).Within(.02f));
                yield return new WaitForSecondsRealtime(.18f);
                Assert.That(headset.position.y, Is.EqualTo(crouched).Within(.01f), "Holding the stick must toggle only once.");
                InputSystem.QueueStateEvent(left, idle);
                yield return null;
                InputSystem.QueueStateEvent(left, clicked);
                yield return null;
                Assert.That(headset.position.y, Is.EqualTo(standing).Within(.01f));
                Assert.That(movement.transform.position, Is.EqualTo(floor));
            }
            finally
            {
                LanguageSelectionView.Hide();
                InputSystem.RemoveDevice(left);
                InputSystem.RemoveDevice(right);
                InputSystem.settings.backgroundBehavior = background;
                InputSystem.settings.editorInputBehaviorInPlayMode = focus;
            }
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        public void GraphicsOptionsConfigureIndependentHdrpFeatures(bool ray, bool dlss)
        {
            var asset = (HDRenderPipelineAsset)QualitySettings.GetRenderPipelineAssetAt(KitchenGraphicsMenu.Performance);
            var original = asset.currentPlatformRenderPipelineSettings;
            var configured = KitchenGraphicsMenu.Configure(original, ray, dlss);
            Assert.AreEqual(ray, configured.supportRayTracing);
            Assert.AreEqual(original.supportSSR || ray, configured.supportSSR);
            Assert.AreEqual(dlss, configured.dynamicResolutionSettings.enableDLSS);
            Assert.AreEqual(original.supportRayTracing, asset.currentPlatformRenderPipelineSettings.supportRayTracing);
            Assert.AreEqual(original.dynamicResolutionSettings.enableDLSS, asset.currentPlatformRenderPipelineSettings.dynamicResolutionSettings.enableDLSS);
        }

        [UnityTest]
        public IEnumerator GraphicsQualitySwitchesTheRenderPipelineWhilePaused()
        {
            var keys = new[] { KitchenGraphicsMenu.Preference, KitchenGraphicsMenu.RayTracingPreference, KitchenGraphicsMenu.DlssPreference };
            var saved = keys.Select(key => PlayerPrefs.GetInt(key, -1)).ToArray();
            var restore = typeof(KitchenGraphicsMenu).GetMethod("Restore", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            LocomotionSettingsView menu = null;
            using (var keyboard = new KitchenMenuKeyboard())
            {
                try
                {
                    PlayerPrefs.SetInt(keys[0], KitchenGraphicsMenu.Standard);
                    PlayerPrefs.SetInt(keys[1], 0);
                    PlayerPrefs.SetInt(keys[2], 0);
                    restore.Invoke(null, null);
                    SceneManager.LoadScene(0);
                    yield return null;
                    yield return null;
                    menu = Object.FindObjectOfType<LocomotionSettingsView>();
                    KitchenSceneTestSetup.ChooseStartupLanguage(menu);
                    var graphics = menu.GetComponentInChildren<KitchenGraphicsMenu>(true);
                    var pause = menu.GetComponentInChildren<KitchenPauseMenu>(true);
                    var navigation = pause.transform.Find("PauseMenu/Navigation");
                    var panel = graphics.transform.Find("GraphicsQuality");
                    var status = panel.Find("Status").GetComponent<TMP_Text>();
                    var camera = menu.GetComponent<Canvas>().worldCamera;
                    var data = camera.GetComponent<HDAdditionalCameraData>();
                    var volume = Object.FindObjectsOfType<Volume>().Single(value => value.sharedProfile.name == "VOL_KitchenCooking");
                    Assert.IsTrue(volume.sharedProfile.TryGet(out ScreenSpaceReflection reflection));
                    var originalTracing = reflection.tracing.value;

                    yield return keyboard.Hold();
                    KitchenSceneTestSetup.Click(menu, navigation.Find("Graphics"));
                    yield return null; // Let the newly activated graphics Canvas render before casting the next ray.
                    Assert.IsTrue(panel.gameObject.activeInHierarchy && pause.IsPaused);
                    Assert.AreEqual(0f, Time.timeScale);
                    Assert.IsFalse(panel.Find("Standard").GetComponent<Button>().interactable);
                    Assert.IsFalse(data.allowDeepLearningSuperSampling);
                    Assert.IsFalse(data.renderingPathCustomFrameSettings.IsEnabled(FrameSettingsField.RayTracing));

                    KitchenSceneTestSetup.Click(menu, panel.Find("DLSSToggle"));
                    yield return null;
                    Assert.AreEqual(KitchenGraphicsMenu.Standard, QualitySettings.GetQualityLevel());
                    Assert.AreEqual(1, PlayerPrefs.GetInt(keys[2]));
                    Assert.AreEqual(0, PlayerPrefs.GetInt(keys[1]));
                    Assert.IsTrue(data.allowDynamicResolution, "HDRP requires dynamic resolution on the kitchen camera");
                    Assert.IsTrue(data.allowDeepLearningSuperSampling, "DLSS must be requested on the kitchen camera");
                    Assert.IsTrue(((HDRenderPipelineAsset)QualitySettings.renderPipeline).currentPlatformRenderPipelineSettings.dynamicResolutionSettings.enableDLSS);
                    if (!HDDynamicResolutionPlatformCapabilities.DLSSDetected) StringAssert.Contains("NVIDIA DLSS", status.text);

                    KitchenSceneTestSetup.Click(menu, panel.Find("RayTracingToggle"));
                    yield return null;
                    bool dxr = SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D12 && SystemInfo.supportsRayTracing;
                    Assert.AreEqual(1, PlayerPrefs.GetInt(keys[1]));
                    Assert.AreEqual(1, PlayerPrefs.GetInt(keys[2]));
                    Assert.AreEqual(dxr, data.renderingPathCustomFrameSettings.IsEnabled(FrameSettingsField.RayTracing));
                    if (!dxr) StringAssert.Contains("D3D12", status.text);

                    KitchenSceneTestSetup.Click(menu, panel.Find("Performance"));
                    yield return null;
                    Assert.AreEqual(KitchenGraphicsMenu.Performance, QualitySettings.GetQualityLevel());
                    Assert.AreEqual(1, PlayerPrefs.GetInt(keys[1]));
                    Assert.IsTrue(data.allowDeepLearningSuperSampling);
                    KitchenSceneTestSetup.Click(menu, panel.Find("DLSSToggle"));
                    yield return null;
                    Assert.AreEqual(1, PlayerPrefs.GetInt(keys[1]));
                    Assert.AreEqual(0, PlayerPrefs.GetInt(keys[2]));
                    Assert.IsFalse(camera.allowDynamicResolution || data.allowDynamicResolution || data.allowDeepLearningSuperSampling);
                    Assert.AreEqual(dxr, data.renderingPathCustomFrameSettings.IsEnabled(FrameSettingsField.RayTracing));

                    KitchenSceneTestSetup.Click(menu, panel.Find("RayTracingToggle"));
                    KitchenSceneTestSetup.Click(menu, panel.Find("RayTracing")); // Serialized button for the High preset only.
                    yield return null;
                    Assert.AreEqual(KitchenGraphicsMenu.RayTracing, QualitySettings.GetQualityLevel());
                    Assert.AreEqual(0, PlayerPrefs.GetInt(keys[1]));
                    Assert.IsFalse(((HDRenderPipelineAsset)QualitySettings.renderPipeline).currentPlatformRenderPipelineSettings.supportRayTracing);
                    Assert.AreEqual(originalTracing, reflection.tracing.value, "Keep the authored scene Volume intact.");
                    Assert.IsTrue(pause.IsPaused && panel.gameObject.activeInHierarchy);
                    Assert.AreEqual(0f, Time.timeScale);
                    KitchenSceneTestSetup.Click(menu, panel.Find("Back"));
                    Assert.IsTrue(navigation.gameObject.activeInHierarchy && pause.IsPaused);
                }
                finally
                {
                    if (menu != null) menu.Close();
                    for (int i = 0; i < keys.Length; i++)
                    {
                        if (saved[i] >= 0) PlayerPrefs.SetInt(keys[i], saved[i]);
                        else PlayerPrefs.DeleteKey(keys[i]);
                    }
                    restore.Invoke(null, null);
                    PlayerPrefs.Save();
                }
            }
        }

        static IEnumerator HoldControllerMenu(XRSimulatedController controller, XRSimulatedControllerState state, float seconds,
            ControllerButton button = ControllerButton.MenuButton)
        {
            var control = button == ControllerButton.MenuButton ? controller.menuButton : controller.secondaryButton;
            InputSystem.QueueStateEvent(controller, state.WithButton(button));
            yield return null;
            Assert.IsTrue(control.isPressed);
            yield return new WaitForSecondsRealtime(seconds);
            InputSystem.QueueStateEvent(controller, state.WithButton(button, false));
            yield return null;
            yield return null;
            Assert.IsFalse(control.isPressed);
        }
    }
}
