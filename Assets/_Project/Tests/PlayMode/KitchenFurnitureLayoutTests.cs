#if UNITY_EDITOR
using System;
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
using XRApartment.Data;
using Object = UnityEngine.Object;

namespace XRApartment.Tests
{
    public sealed class KitchenFurnitureLayoutTests
    {
        [Serializable] sealed class RouteEvidence { public Vector3[] requested, reached; public float height, radius; }
        readonly List<GameObject> hidden = new List<GameObject>();
        Scene scene, previous;
        GameObject motor;

        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (motor != null) Object.DestroyImmediate(motor);
            if (scene.IsValid() && scene.isLoaded)
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                yield return SceneManager.UnloadSceneAsync(scene);
            }
            foreach (var root in hidden) if (root != null) root.SetActive(true);
            hidden.Clear();
        }

        [UnityTest] public IEnumerator DiningSetLeavesTheKitchenApproachAndTableEndsWalkable()
        {
            previous = SceneManager.GetActiveScene();
            for (var index = 0; index < SceneManager.sceneCount; index++)
                foreach (var root in SceneManager.GetSceneAt(index).GetRootGameObjects())
                    // Isolate other kitchens without disabling the runner that owns this coroutine.
                    if (root.activeSelf && (root.GetComponentInChildren<Renderer>(true) != null ||
                        root.GetComponentInChildren<Collider>(true) != null ||
                        root.GetComponentInChildren<UnityEngine.EventSystems.EventSystem>(true) != null))
                    { hidden.Add(root); root.SetActive(false); }
            scene = EditorSceneManager.LoadSceneInPlayMode("Assets/_Project/Scenes/SCN_Kitchen_Cooking.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
            for (var index = 0; index < 300 && !scene.isLoaded; index++) yield return null;
            Assert.IsTrue(scene.isLoaded); SceneManager.SetActiveScene(scene);
            scene.GetRootGameObjects().Single(g => g.name == "PREF_XR_KitchenRig").SetActive(false);
            var assumptions = AssetDatabase.LoadAssetAtPath<ModelAssumptions>("Assets/_Project/Settings/ASM_KitchenParameters.asset");
            Assert.IsNotNull(assumptions);
            Assert.IsTrue(assumptions.TryGetLength("cooking_route_character_height", out var height));
            Assert.IsTrue(assumptions.TryGetLength("cooking_route_character_radius", out var radius));
            Assert.IsTrue(assumptions.TryGetLength("cooking_route_start_x", out var startX));
            Assert.IsTrue(assumptions.TryGetLength("cooking_route_start_z", out var startZ));
            Assert.IsTrue(assumptions.TryGetLength("cooking_route_far_end_x", out var farX));
            Assert.IsTrue(assumptions.TryGetLength("cooking_route_approach_z", out var approachZ));
            motor = new GameObject("Furniture route character");
            motor.transform.position = new Vector3(startX, .03f, startZ);
            var controller = motor.AddComponent<CharacterController>();
            controller.height = height; controller.radius = radius; controller.center = Vector3.up * (height * .5f);
            controller.skinWidth = .01f; controller.stepOffset = .12f; controller.minMoveDistance = 0;
            var route = new[] { new Vector3(startX, .03f, approachZ), new Vector3(farX, .03f, approachZ), new Vector3(farX, .03f, startZ) };
            var reached = new Vector3[route.Length];
            yield return null; Physics.SyncTransforms();
            for (var waypoint = 0; waypoint < route.Length; waypoint++)
            {
                for (var step = 0; step < 160; step++)
                {
                    var delta = route[waypoint] - motor.transform.position; delta.y = 0;
                    if (delta.magnitude < .025f) break;
                    controller.Move(Vector3.ClampMagnitude(delta, .03f) + Vector3.down * .02f);
                    yield return null;
                }
                reached[waypoint] = motor.transform.position;
                var distance = route[waypoint] - reached[waypoint]; distance.y = 0;
                Assert.Less(distance.magnitude, .08f, "Furniture blocked the actual character controller at waypoint " + waypoint);
            }
            Directory.CreateDirectory("Logs/KitchenFurniture");
            File.WriteAllText("Logs/KitchenFurniture/route-verification.json", JsonUtility.ToJson(new RouteEvidence
                { requested = route, reached = reached, height = controller.height, radius = controller.radius }, true));
        }
    }
}
#endif
