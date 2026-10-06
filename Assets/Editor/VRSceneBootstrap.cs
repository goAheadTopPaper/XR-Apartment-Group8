using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace XRApartment.EditorTools
{
    /// <summary>
    /// Builds the starting VR scene from the XRI Starter Assets rig.
    /// Run with: Unity.exe -batchmode -executeMethod XRApartment.EditorTools.VRSceneBootstrap.CreateMainScene
    /// </summary>
    public static class VRSceneBootstrap
    {
        const string Marker = "[XRSCENE]";
        const string SampleRoot = "Assets/Samples/XR Interaction Toolkit/3.5.0/Starter Assets";
        const string ScenePath = "Assets/_Project/Scenes/Main.unity";

        public static void CreateMainScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var defaultCamera = GameObject.Find("Main Camera");
            if (defaultCamera != null)
                Object.DestroyImmediate(defaultCamera);

            var rig = Spawn($"{SampleRoot}/Prefabs/XR Origin (XR Rig).prefab", Vector3.zero);

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<XRUIInputModule>();

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(5f, 1f, 5f);

            var teleportArea = Spawn($"{SampleRoot}/DemoAssets/Prefabs/Teleport/Teleport Area.prefab",
                new Vector3(0f, 0.01f, 0f));
            if (teleportArea != null)
                teleportArea.transform.localScale = new Vector3(4f, 1f, 4f);

            Spawn($"{SampleRoot}/DemoAssets/Prefabs/Teleport/Teleport Anchor.prefab", new Vector3(0f, 0.02f, 2f));
            Spawn($"{SampleRoot}/DemoAssets/Prefabs/Interactables/Cube.prefab", new Vector3(0.6f, 0.5f, 2.5f));

            var light = GameObject.Find("Directional Light");
            if (light != null)
            {
                light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                var sun = light.GetComponent<Light>();
                if (sun != null)
                {
                    sun.intensity = 1.1f;
                    RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                    RenderSettings.ambientLight = new Color(0.6f, 0.62f, 0.68f);
                }
            }

            LogRig(rig);

            EditorSceneManager.MarkSceneDirty(scene);
            var saved = EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();

            var roots = new List<string>();
            foreach (var root in scene.GetRootGameObjects())
                roots.Add(root.name);

            Debug.Log($"{Marker} saved={saved} path={ScenePath} roots=[{string.Join(", ", roots)}] " +
                      $"buildScenes={EditorBuildSettings.scenes.Length}");
        }

        static void LogRig(GameObject rig)
        {
            if (rig == null)
                return;

            var componentNames = new HashSet<string>();
            foreach (var behaviour in rig.GetComponentsInChildren<MonoBehaviour>(true))
                if (behaviour != null)
                    componentNames.Add(behaviour.GetType().Name);

            foreach (var probe in new[] { "XROrigin", "InputActionManager", "TeleportationProvider", "NearFarInteractor" })
                Debug.Log($"{Marker} rig has {probe} = {componentNames.Contains(probe)}");
        }

        static GameObject Spawn(string prefabPath, Vector3 position)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogError($"{Marker} prefab missing: {prefabPath}");
                return null;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.SetPositionAndRotation(position, Quaternion.identity);
            return instance;
        }
    }
}
