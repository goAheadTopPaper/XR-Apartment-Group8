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
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using Object = UnityEngine.Object;

namespace XRApartment.Tests
{
    // Drive the simulator's actual FPS input readers and rendered updates, not a substitute motor.
    public sealed class KeyboardSimulatorCollisionTests
    {
        const string SimulatorPath = "Assets/_Project/XR/InteractionSimulator/XR Interaction Simulator.prefab";
        readonly List<GameObject> hidden = new List<GameObject>();
        Scene scene, previous;
        XRInteractionSimulator simulator;
        Transform rig, head;
        bool restoreSimulator;
        float previousTimeScale;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousTimeScale = Time.timeScale;
            Time.timeScale = 1;
            previous = SceneManager.GetActiveScene();
            var existing = Object.FindObjectOfType<XRInteractionSimulator>();
            restoreSimulator = existing != null;
            if (existing != null) Object.Destroy(existing.gameObject);
            yield return null;
            for (var index = 0; index < SceneManager.sceneCount; index++)
                foreach (var root in SceneManager.GetSceneAt(index).GetRootGameObjects())
                    if (root.activeSelf && (root.GetComponentInChildren<Renderer>(true) != null ||
                        root.GetComponentInChildren<Collider>(true) != null ||
                        root.GetComponentInChildren<UnityEngine.EventSystems.EventSystem>(true) != null))
                    { hidden.Add(root); root.SetActive(false); }

            scene = EditorSceneManager.LoadSceneInPlayMode("Assets/_Project/Scenes/SCN_Kitchen_Cooking.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
            for (var i = 0; i < 300 && !scene.isLoaded; i++) yield return null;
            Assert.IsTrue(scene.isLoaded);
            SceneManager.SetActiveScene(scene);
            rig = scene.GetRootGameObjects().Single(g => g.name == "PREF_XR_KitchenRig").transform;
            head = rig.GetComponentInChildren<Camera>().transform;
            simulator = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SimulatorPath))
                .GetComponent<XRInteractionSimulator>();
            simulator.targetedDeviceInput = TargetedDevices.FPS | TargetedDevices.RightDevice;
            SetInput(simulator.translateXInput, 0);
            SetInput(simulator.translateYInput, 0);
            SetInput(simulator.translateZInput, 0);
            simulator.mouseScrollInput.inputSourceMode = XRInputValueReader.InputSourceMode.ManualValue;
            simulator.mouseScrollInput.manualValue = Vector2.zero;
            simulator.mouseRotationDeltaInput.inputSourceMode = XRInputValueReader.InputSourceMode.ManualValue;
            simulator.mouseRotationDeltaInput.manualValue = Vector2.zero;
            simulator.keyboardRotationDeltaInput.inputSourceMode = XRInputValueReader.InputSourceMode.ManualValue;
            simulator.keyboardRotationDeltaInput.manualValue = Vector2.zero;
            // Fixed speed keeps the same requested travel under different Editor frame rates.
            simulator.translateXSpeed = simulator.translateZSpeed = .4f;
            simulator.bodyTranslateMultiplier = 5;
            while (Time.time < 1.1f) yield return null;
            for (var i = 0; i < 4; i++) yield return null;
            Physics.SyncTransforms();
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (simulator != null) Object.Destroy(simulator.gameObject);
            yield return null;
            if (scene.IsValid() && scene.isLoaded)
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                yield return SceneManager.UnloadSceneAsync(scene);
            }
            foreach (var root in hidden) if (root != null) root.SetActive(true);
            hidden.Clear();
            if (restoreSimulator && XRInteractionSimulator.instance == null)
                Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SimulatorPath));
            Time.timeScale = previousTimeScale;
        }

        static void SetInput(XRInputValueReader<float> reader, float value)
        {
            reader.inputSourceMode = XRInputValueReader.InputSourceMode.ManualValue;
            reader.manualValue = value;
        }

        IEnumerator Walk(float x, float z, float seconds)
        {
            simulator.translateXInput.manualValue = x;
            simulator.translateZInput.manualValue = z;
            var elapsed = 0f;
            while (elapsed < seconds) { yield return null; elapsed += Time.deltaTime; }
            simulator.translateXInput.manualValue = simulator.translateZInput.manualValue = 0;
            for (var i = 0; i < 3; i++) yield return null;
        }

        [UnityTest]
        public IEnumerator KeyboardWalkingStopsAtTheActualKitchenCabinet()
        {
            var start = head.position;
            // The worktop's vertical cabinet face is below eye level, as in the reported bug.
            Assert.IsTrue(Physics.Raycast(new Vector3(start.x, .65f, start.z), Vector3.left,
                out var face, 3, ~(1 << 2), QueryTriggerInteraction.Ignore));
            Assert.AreEqual("SM_KitchenTable", face.collider.name);
            yield return Walk(-1, 0, 1.4f);
            Assert.GreaterOrEqual(head.position.x, face.point.x - .02f,
                "Keyboard FPS movement bypassed the cabinet's collider. Head=" + head.position + "; face=" + face.point);
            Assert.Less(head.position.x, start.x - .3f, "The player must still approach the cabinet normally.");
            Assert.Greater(head.position.y, start.y - .05f, "The player left the room and fell through the floor boundary.");
            var blocked = head.position;
            yield return Walk(1, 0, .3f);
            Assert.Greater(head.position.x, blocked.x + .3f, "Walking away from contact must recover immediately.");
        }

        [UnityTest]
        public IEnumerator KeyboardWalkingPassesThroughTheDoorwayButStopsAtTheWall()
        {
            rig.position += Vector3.right * (-3.5f - head.position.x);
            Physics.SyncTransforms();
            yield return Walk(0, -1, .7f);
            Assert.Less(head.position.z, .2f, "The actual kitchen doorway must remain walkable.");
            yield return Walk(0, 1, .7f);
            Assert.Greater(head.position.z, .9f, "The player must be able to return through the doorway.");

            rig.position += new Vector3(-4.05f - head.position.x, 0, 1.12f - head.position.z);
            Physics.SyncTransforms();
            Assert.IsTrue(Physics.Raycast(new Vector3(head.position.x, .7f, head.position.z), Vector3.back,
                out var wall, 2, ~(1 << 2), QueryTriggerInteraction.Ignore));
            Assert.AreEqual("SM_Wall_02", wall.collider.name);
            yield return Walk(0, -1, .8f);
            Assert.GreaterOrEqual(head.position.z, wall.point.z - .02f, "Walking bypassed the actual kitchen wall.");
        }

        [UnityTest]
        public IEnumerator DiagonalWalkingSlidesAlongTheCabinet()
        {
            yield return Walk(-1, 0, .8f);
            var contact = head.position;
            yield return Walk(-1, 1, .15f);
            Assert.Greater(head.position.z, contact.z + .15f, "Diagonal input should slide along contact.");
            // Follow the actual mesh face at the new Z, rather than assuming a perfectly flat cabinet.
            Assert.IsTrue(Physics.Raycast(new Vector3(-3.15f, .65f, head.position.z), Vector3.left,
                out var face, 3, ~(1 << 2), QueryTriggerInteraction.Ignore));
            Assert.AreEqual("SM_KitchenTable", face.collider.name);
            Assert.GreaterOrEqual(head.position.x, face.point.x - .02f, "Sliding must not cross the cabinet face.");
        }

        [UnityTest]
        public IEnumerator LookingAndEditingAControllerDoNotWalkTheBody()
        {
            var position = head.position;
            var rotation = head.rotation;
            simulator.keyboardRotationDeltaInput.manualValue = new Vector2(5, 0);
            yield return null;
            simulator.keyboardRotationDeltaInput.manualValue = Vector2.zero;
            for (var i = 0; i < 3; i++) yield return null;
            Assert.Greater(Quaternion.Angle(rotation, head.rotation), .1f, "Mouse/keyboard look must still rotate the view.");
            Assert.Less(Vector3.Distance(position, head.position), .02f);

            var bodyPosition = rig.position;
            var controller = rig.GetComponentsInChildren<Transform>().Single(t => t.name == "Right Controller");
            var controllerPosition = controller.position;
            simulator.targetedDeviceInput = TargetedDevices.RightDevice;
            yield return Walk(1, 0, .2f);
            Assert.Less(Vector3.Distance(bodyPosition, rig.position), .02f, "Controller manipulation was mistaken for walking.");
            Assert.Less(Vector3.Distance(position, head.position), .02f);
            Assert.Greater(Vector3.Distance(controllerPosition, controller.position), .1f,
                "Independent controller pose editing must remain available.");
        }
    }
}
#endif
