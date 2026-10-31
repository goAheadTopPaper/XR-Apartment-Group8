#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using UnityEngine.XR.Interaction.Toolkit.UI;
using XRApartment.Runtime;
using XRApartment.Runtime.UI;
using Object = UnityEngine.Object;

namespace XRApartment.Tests
{
    public sealed class LocomotionModesPlayTests
    {
        const string SimulatorPath = "Assets/_Project/XR/InteractionSimulator/XR Interaction Simulator.prefab";
        readonly List<GameObject> hidden = new List<GameObject>();
        Scene previous, scene;
        KitchenLocomotionSettings settings;
        XRInteractionSimulator simulator;
        Keyboard keyboard;
        bool restoreSimulator, hadPreference;
        int preference;
        float timeScale;

        [UnitySetUp] public IEnumerator SetUp()
        {
            hadPreference = PlayerPrefs.HasKey(KitchenLocomotionSettings.PreferenceKey);
            preference = PlayerPrefs.GetInt(KitchenLocomotionSettings.PreferenceKey);
            PlayerPrefs.DeleteKey(KitchenLocomotionSettings.PreferenceKey);
            timeScale = Time.timeScale; Time.timeScale = 1;
            previous = SceneManager.GetActiveScene();
            var existing = Object.FindObjectOfType<XRInteractionSimulator>();
            restoreSimulator = existing != null;
            if (existing != null) Object.Destroy(existing.gameObject);
            yield return null;
            for (var i = 0; i < SceneManager.sceneCount; i++)
                foreach (var root in SceneManager.GetSceneAt(i).GetRootGameObjects())
                    if (root.activeSelf && (root.GetComponentInChildren<Renderer>(true) != null ||
                        root.GetComponentInChildren<Collider>(true) != null ||
                        root.GetComponentInChildren<UnityEngine.EventSystems.EventSystem>(true) != null))
                    { hidden.Add(root); root.SetActive(false); }
            scene = EditorSceneManager.LoadSceneInPlayMode("Assets/_Project/Scenes/SCN_Kitchen_Cooking.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
            for (var i = 0; i < 300 && !scene.isLoaded; i++) yield return null;
            Assert.IsTrue(scene.isLoaded); SceneManager.SetActiveScene(scene);
            settings = scene.GetRootGameObjects().Select(g => g.GetComponent<KitchenLocomotionSettings>()).Single(s => s != null);
            simulator = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SimulatorPath)).GetComponent<XRInteractionSimulator>();
            while (Time.time < 1.1f) yield return null;
            for (var i = 0; i < 4; i++) yield return null;
            Physics.SyncTransforms();
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            if (simulator != null) Object.Destroy(simulator.gameObject);
            yield return null;
            if (scene.IsValid() && scene.isLoaded)
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                yield return SceneManager.UnloadSceneAsync(scene);
            }
            foreach (var root in hidden) if (root != null) root.SetActive(true);
            hidden.Clear();
            if (hadPreference) PlayerPrefs.SetInt(KitchenLocomotionSettings.PreferenceKey, preference);
            else PlayerPrefs.DeleteKey(KitchenLocomotionSettings.PreferenceKey);
            PlayerPrefs.Save(); Time.timeScale = timeScale;
            if (restoreSimulator && XRInteractionSimulator.instance == null)
                Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SimulatorPath));
        }

        bool ActionEnabled(string side, string field)
        {
            var manager = settings.GetComponentsInChildren<MonoBehaviour>(true)
                .Single(c => c != null && c.name == side + " Controller" && c.GetType().Name == "ControllerInputActionManager");
            return ((InputActionReference)new SerializedObject(manager).FindProperty(field).objectReferenceValue).action.enabled;
        }

        [UnityTest] public IEnumerator BothModesConfigureActualControllerActionsAndSmoothWalkingHitsTheCabinet()
        {
            Assert.AreEqual(KitchenMovementMode.Teleport, settings.Mode);
            Assert.IsFalse(settings.MoveProvider.enabled);
            Assert.IsFalse(ActionEnabled("Left", "m_Move"));
            Assert.IsTrue(ActionEnabled("Left", "m_TeleportMode"));
            settings.SetMode(KitchenMovementMode.Smooth, false);
            Assert.IsTrue(settings.MoveProvider.enabled);
            Assert.IsTrue(ActionEnabled("Left", "m_Move"));
            Assert.IsFalse(ActionEnabled("Left", "m_TeleportMode"));
            Assert.IsTrue(ActionEnabled("Right", "m_TeleportMode"));
            Assert.IsTrue(ActionEnabled("Right", "m_SnapTurn"));
            var move = settings.MoveProvider;
            move.leftHandMoveInput.inputSourceMode = XRInputValueReader.InputSourceMode.ManualValue;
            move.leftHandMoveInput.manualValue = Vector2.left;
            var start = settings.Origin.Camera.transform.position;
            var elapsed = 0f;
            while (elapsed < 1.3f) { yield return null; elapsed += Time.deltaTime; }
            move.leftHandMoveInput.manualValue = Vector2.zero;
            yield return null;
            var head = settings.Origin.Camera.transform.position;
            Assert.Less(head.x, start.x - .4f, "The actual smooth provider must move the player.");
            Assert.Greater(head.x, -4.48f, "The actual smooth provider crossed the kitchen cabinet.");
            settings.SetMode(KitchenMovementMode.Teleport, false);
            Assert.IsFalse(move.enabled); Assert.IsFalse(ActionEnabled("Left", "m_Move"));
            Assert.IsTrue(ActionEnabled("Left", "m_TeleportMode"));
        }

        [UnityTest] public IEnumerator ActualRaySelectionTeleportsToClearFloorAndPreservesFacing()
        {
            var ray = settings.GetComponentsInChildren<XRRayInteractor>(true).First(r => r.name == "Teleport Interactor");
            var probe = new GameObject("Test teleport aim"); SceneManager.MoveGameObjectToScene(probe, scene);
            probe.transform.position = settings.Origin.Camera.transform.position;
            var destination = new Vector3(-3.5f, .004f, 1.12f);
            probe.transform.LookAt(destination);
            ray.rayOriginTransform = probe.transform;
            ray.lineType = XRRayInteractor.LineType.StraightLine;
            ray.raycastMask = Physics.DefaultRaycastLayers;
            ray.gameObject.SetActive(true);
            for (var i = 0; i < 4; i++) yield return null;
            Assert.IsTrue(ray.TryGetCurrent3DRaycastHit(out var hit));
            var area = hit.collider.GetComponent<KitchenTeleportationArea>();
            Assert.IsNotNull(area, "Teleport ray must hit the project's floor layer.");
            Assert.AreNotEqual(0, ray.interactionLayers.value & area.interactionLayers.value);
            Assert.IsTrue(area.IsSelectableBy((IXRSelectInteractor)ray), "Clear ground was rejected by the actual ray/area path.");
            var facing = settings.Origin.Camera.transform.rotation;
            var started = 0;
            settings.TeleportProvider.locomotionStarted += _ => started++;
            ray.interactionManager.SelectEnter((IXRSelectInteractor)ray, area);
            ray.interactionManager.SelectExit((IXRSelectInteractor)ray, area);
            for (var i = 0; i < 4; i++) yield return null;
            var reached = settings.Origin.Camera.transform.position;
            Assert.Less(Vector2.Distance(new Vector2(reached.x, reached.z), new Vector2(hit.point.x, hit.point.z)), .04f);
            Assert.Less(Quaternion.Angle(facing, settings.Origin.Camera.transform.rotation), .1f);
            Assert.AreEqual(1, started, "The actual XRI teleport provider did not complete the request.");
        }

        [UnityTest] public IEnumerator OccupiedLandingsWallsAndCancelledRequestsCannotMoveThePlayer()
        {
            var provider = settings.TeleportProvider;
            Assert.IsTrue(provider.CanTeleportTo(new Vector3(-3.5f, .004f, 1.12f)));
            Assert.IsFalse(provider.CanTeleportTo(new Vector3(-4.75f, 0, 1.12f)), "Cabinet landing must be blocked.");
            Assert.IsFalse(provider.CanTeleportTo(new Vector3(-8, 0, 1.12f)), "Outside the authored floor must be blocked.");
            settings.transform.position += Vector3.right * (-4.05f - settings.Origin.Camera.transform.position.x);
            Physics.SyncTransforms();
            Assert.IsFalse(provider.CanTeleportTo(new Vector3(-4.05f, 0, .1f)), "Teleport crossed a solid wall.");
            settings.transform.position += Vector3.right * (-3.15f - settings.Origin.Camera.transform.position.x);
            Physics.SyncTransforms();
            var before = settings.transform.position;
            Assert.IsTrue(provider.QueueTeleportRequest(new TeleportRequest
                { destinationPosition = new Vector3(-3.5f, .004f, 1.12f), matchOrientation = MatchOrientation.None }));
            settings.SetMenuOpen(true);
            Assert.IsFalse(provider.HasPendingRequest);
            settings.SetMenuOpen(false);
            for (var i = 0; i < 4; i++) yield return null;
            Assert.Less(Vector3.Distance(before, settings.transform.position), .02f, "A cancelled teleport resumed after closing the menu.");

            Assert.IsTrue(provider.QueueTeleportRequest(new TeleportRequest
                { destinationPosition = new Vector3(-3.5f, .004f, 1.12f), matchOrientation = MatchOrientation.None }));
            var obstruction = new GameObject("Object entering teleport path", typeof(BoxCollider));
            SceneManager.MoveGameObjectToScene(obstruction, scene);
            obstruction.transform.position = new Vector3(-3.325f, .75f, 1.12f);
            obstruction.GetComponent<BoxCollider>().size = new Vector3(.04f, 1.5f, .6f);
            Physics.SyncTransforms();
            for (var i = 0; i < 4; i++) yield return null;
            Assert.IsFalse(provider.HasPendingRequest);
            Assert.Less(Vector3.Distance(before, settings.transform.position), .02f,
                "A newly blocked path must cancel the queued teleport before moving.");
        }

        [UnityTest] public IEnumerator SavedChoiceAppliesToANewRigAfterItsInputManagersStart()
        {
            settings.SetMode(KitchenMovementMode.Smooth);
            settings.gameObject.SetActive(false);
            var next = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/PREF_XR_KitchenRig.prefab"));
            SceneManager.MoveGameObjectToScene(next, scene);
            settings = next.GetComponent<KitchenLocomotionSettings>();
            for (var i = 0; i < 4; i++) yield return null;
            Assert.AreEqual(KitchenMovementMode.Smooth, settings.Mode);
            Assert.IsTrue(settings.MoveProvider.enabled);
            Assert.IsTrue(ActionEnabled("Left", "m_Move"));
            Assert.IsFalse(ActionEnabled("Left", "m_TeleportMode"));
        }

        [UnityTest] public IEnumerator KeyboardMenuAndRealButtonsSavePreferenceWithoutChangingRoomOrTime()
        {
            var view = settings.GetComponentInChildren<LocomotionSettingsView>(true);
            var before = settings.transform.position;
            keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F8));
            for (var i = 0; i < 3; i++) yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            Assert.IsTrue(view.IsOpen, "F8 must open the actual runtime settings view.");
            Assert.IsTrue(settings.MenuOpen); Assert.IsFalse(settings.TeleportProvider.enabled);
            Canvas.ForceUpdateCanvases();
            var smoothButton = view.transform.Find("Panel/Smooth").GetComponent<Button>();
            var start = settings.Origin.Camera.transform.position;
            var target = smoothButton.transform.position;
            var pointer = new TrackedDeviceEventData(UnityEngine.EventSystems.EventSystem.current)
            {
                layerMask = ~0,
                rayPoints = new List<Vector3> { start, target + (target - start).normalized * .1f }
            };
            var hits = new List<UnityEngine.EventSystems.RaycastResult>();
            view.GetComponent<TrackedDeviceGraphicRaycaster>().Raycast(pointer, hits);
            Assert.IsTrue(hits.Any(h => h.gameObject == smoothButton.gameObject),
                "The actual tracked-device UI raycaster must be able to hit the movement button.");
            view.transform.Find("Panel/Smooth").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual((int)KitchenMovementMode.Smooth, PlayerPrefs.GetInt(KitchenLocomotionSettings.PreferenceKey));
            Assert.IsFalse(settings.MoveProvider.enabled, "The open menu must block walking even after selecting Smooth.");
            view.transform.Find("Panel/Close").GetComponent<Button>().onClick.Invoke();
            Assert.IsFalse(view.IsOpen); Assert.IsTrue(settings.MoveProvider.enabled);
            settings.SetMode(KitchenMovementMode.Teleport, false);
            settings.ReloadPreference();
            Assert.AreEqual(KitchenMovementMode.Smooth, settings.Mode);
            Assert.Less(Vector3.Distance(before, settings.transform.position), .02f);
            Assert.AreEqual(1, Time.timeScale);
        }
    }
}
#endif
