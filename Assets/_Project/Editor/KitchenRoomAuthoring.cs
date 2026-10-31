using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.SceneManagement;
using XRApartment.Runtime;

namespace XRApartment.EditorTools
{
    public static partial class KitchenCookingAuthoring
    {
        const string CityTexture = "Assets/ThirdParty/PolyHaven/JoburgCentralSunset/sunset_jhbcentral_4k.exr";
        const string CitySky = Art + "MAT_Sky_CookingCity.mat";
        const string RoomResetPrefab = Prefabs + "PREF_Cooking_RoomReset.prefab";
        const string MainPlaySessionKey = "XRApartment.MainKitchenPlayInitialized";

        [InitializeOnLoadMethod]
        static void RestoreMainPlayOnEditorStartup()
        {
            if (SessionState.GetBool(MainPlaySessionKey, false)) return;
            EditorApplication.delayCall += () =>
            {
                if (SessionState.GetBool(MainPlaySessionKey, false)) return;
                SessionState.SetBool(MainPlaySessionKey, true);
                var first = EditorBuildSettings.scenes.FirstOrDefault(scene => scene.enabled);
                if (first == null || first.path != ScenePath || EditorSceneManager.playModeStartScene != null) return;
                var main = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
                if (main != null) EditorSceneManager.playModeStartScene = main;
            };
        }

        [MenuItem("XR Apartment/Open Main Kitchen", priority = 0)]
        public static void OpenMainKitchen()
        {
            Open();
            if (SceneManager.GetActiveScene().path == ScenePath)
                EditorSceneManager.playModeStartScene = KitchenAuthoring.LoadRequired<SceneAsset>(ScenePath);
        }

        [MenuItem("XR Apartment/Cooking/Use City Kitchen as Main Scene")]
        public static void ConfigureMainScene()
        {
            KitchenAuthoring.RequireEditMode();
            var asset = KitchenAuthoring.LoadRequired<SceneAsset>(ScenePath);
            for (var i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save scene edits before configuring the main kitchen.");

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) }
                .Concat(EditorBuildSettings.scenes.Where(scene => scene.path != ScenePath)).ToArray();
            EditorSceneManager.playModeStartScene = asset;
            VerifyMainScene();
        }

        public static void VerifyMainScene()
        {
            KitchenAuthoring.RequireEditMode();
            var first = EditorBuildSettings.scenes.FirstOrDefault(scene => scene.enabled);
            if (first == null || first.path != ScenePath ||
                AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene) != ScenePath)
                throw new InvalidOperationException("The city kitchen is not configured as the build and Editor Play entry.");
            var main = EditorSceneManager.OpenScene(ScenePath);
            if (RenderSettings.skybox != KitchenAuthoring.LoadRequired<Material>(CitySky))
                throw new InvalidOperationException("The main kitchen must use the city panorama sky.");
            var missing = main.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
            if (missing != 0) throw new InvalidOperationException("The main kitchen contains missing scripts.");
            AssetDatabase.SaveAssets();
            Debug.Log("KITCHEN_MAIN_SCENE_READY main=" + main.path + " editorPlay=" +
                AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene) + " sky=" +
                AssetDatabase.GetAssetPath(RenderSettings.skybox) + " scenes=" + EditorBuildSettings.scenes.Length +
                " missingScripts=" + missing + " target=" + EditorUserBuildSettings.activeBuildTarget);
        }

        [MenuItem("XR Apartment/Cooking/Apply Room Reset and City View")]
        public static void ApplyRoomMenu()
        {
            KitchenAuthoring.RequireEditMode();
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) ApplyRoomBatch();
        }
        public static void ApplyRoomBatch()
        {
            KitchenAuthoring.RequireEditMode();
            var scene = EditorSceneManager.OpenScene(ScenePath);
            ConfigureCookingRoom(scene);
            KitchenAuthoring.SaveScene(scene); AssetDatabase.SaveAssets();
            InspectRoomBatch();
        }
        public static void ApplyRoomAndBakeBatch()
        {
            ApplyRoomBatch(); BakeLightingBatch(); InspectRoomBatch();
        }
        static void ConfigureCookingRoom(Scene scene)
        {
            if (scene.path != ScenePath) throw new InvalidOperationException("Cooking room updates require the cooking scene.");
            var roots = scene.GetRootGameObjects();
            var station = roots.Single(g => g.name == "PREF_Cooking_Workbench").GetComponent<KitchenCookingStation>();
            if (!File.Exists(RoomResetPrefab))
            {
                var source = new GameObject("PREF_Cooking_RoomReset");
                try { source.AddComponent<KitchenRoomReset>(); PrefabUtility.SaveAsPrefabAsset(source, RoomResetPrefab); }
                finally { UnityEngine.Object.DestroyImmediate(source); }
            }
            var reset = roots.SelectMany(g => g.GetComponentsInChildren<KitchenRoomReset>(true)).SingleOrDefault();
            if (reset == null)
                reset = ((GameObject)PrefabUtility.InstantiatePrefab(KitchenAuthoring.LoadRequired<GameObject>(RoomResetPrefab), scene)).GetComponent<KitchenRoomReset>();
            reset.Configure(station); EditorUtility.SetDirty(reset); PrefabUtility.RecordPrefabInstancePropertyModifications(reset);
            var label = station.transform.Find("ACT_RESET/Label").GetComponent<TextMesh>();
            label.text = "RESET ROOM"; EditorUtility.SetDirty(label); PrefabUtility.RecordPrefabInstancePropertyModifications(label);
            var importer = (TextureImporter)AssetImporter.GetAtPath(CityTexture);
            if (importer == null) throw new FileNotFoundException("Download the licensed city EXR first", CityTexture);
            importer.textureShape = TextureImporterShape.Texture2D; importer.sRGBTexture = false;
            importer.maxTextureSize = 4096; importer.mipmapEnabled = true;
            importer.wrapModeU = TextureWrapMode.Repeat; importer.wrapModeV = TextureWrapMode.Clamp; importer.SaveAndReimport();
            var sky = AssetDatabase.LoadAssetAtPath<Material>(CitySky);
            if (sky == null) { sky = new Material(Shader.Find("Skybox/Panoramic")); AssetDatabase.CreateAsset(sky, CitySky); }
            sky.SetTexture("_MainTex", KitchenAuthoring.LoadRequired<Texture2D>(CityTexture));
            sky.SetColor("_Tint", new Color(.5f, .5f, .5f)); sky.SetFloat("_Exposure", 1f);
            sky.SetFloat("_Rotation", 120); sky.SetFloat("_Mapping", 1); sky.SetFloat("_ImageType", 0);
            RenderSettings.skybox = sky; RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox; RenderSettings.customReflection = null;
            RenderSettings.ambientMode = AmbientMode.Skybox; EditorUtility.SetDirty(sky); DynamicGI.UpdateEnvironment();
        }

        [Serializable] sealed class WindowInfo { public string path, material, shader; public Bounds bounds; public Color colour; public float mode; }
        [Serializable] sealed class RoomInfo { public string scene, sky; public int missingScripts; public WindowInfo[] windows; }
        public static void InspectRoomBatch()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath); var roots = scene.GetRootGameObjects();
            var room = new RoomInfo { scene = scene.path, sky = AssetDatabase.GetAssetPath(RenderSettings.skybox),
                windows = roots.Single(r => r.name == "PREF_Kitchen_Environment").GetComponentsInChildren<Renderer>(true)
                    .Where(r => r.name.IndexOf("window", StringComparison.OrdinalIgnoreCase) >= 0 || r.sharedMaterials.Any(m => m != null && m.name.IndexOf("glass", StringComparison.OrdinalIgnoreCase) >= 0))
                    .SelectMany(r => r.sharedMaterials.Where(m => m != null).Select(m => new WindowInfo { path = r.name, material = AssetDatabase.GetAssetPath(m),
                        shader = m.shader.name, bounds = r.bounds, colour = m.color, mode = m.HasProperty("_Mode") ? m.GetFloat("_Mode") : -1 })).ToArray() };
            foreach (var t in roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)))
                room.missingScripts += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
            Directory.CreateDirectory("Logs/RoomResetAndCity");
            File.WriteAllText("Logs/RoomResetAndCity/scene-inspection.json", JsonUtility.ToJson(room, true));
            RenderCity(new Vector3(-.85f, 1.6f, .12f), new Vector3(-2.65f, 1.12f, -1.3f), "dining-city.png", 62);
            RenderCity(new Vector3(-3.9f, 1.6f, 1.5f), new Vector3(-5, 1.5f, 1.5f), "kitchen-window.png", 65);
            if (room.missingScripts != 0) throw new InvalidOperationException("Room contains missing scripts");
            Debug.Log("COOKING_ROOM_READY " + JsonUtility.ToJson(room));
        }
        static void RenderCity(Vector3 position, Vector3 target, string filename, float fov)
        {
            var go = new GameObject("Temporary room QA camera");
            var texture = new RenderTexture(1400, 1000, 24); var image = new Texture2D(1400, 1000, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                var camera = go.AddComponent<Camera>(); camera.transform.position = position; camera.transform.LookAt(target);
                camera.fieldOfView = fov; camera.nearClipPlane = .02f; camera.farClipPlane = 80; camera.allowHDR = true;
                var source = UnityEngine.Object.FindObjectsOfType<PostProcessLayer>(true).FirstOrDefault();
                if (source != null) { var layer = go.AddComponent<PostProcessLayer>(); EditorUtility.CopySerialized(source, layer); layer.volumeTrigger = camera.transform; }
                camera.targetTexture = texture; camera.Render(); RenderTexture.active = texture;
                image.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); image.Apply();
                File.WriteAllBytes("Logs/RoomResetAndCity/" + filename, image.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; texture.Release(); UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(image); UnityEngine.Object.DestroyImmediate(go); }
        }
    }
}
