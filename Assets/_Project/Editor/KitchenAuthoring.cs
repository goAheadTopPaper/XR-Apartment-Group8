using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using XRApartment.Data;

namespace XRApartment.EditorTools
{
    /// Shared asset locations and edit/save lifecycle for the kitchen authoring tools.
    internal static class KitchenAuthoring
    {
        public const string Root = "Assets/_Project/";
        public const string ScenePath = Root + "Scenes/SCN_Kitchen_Review.unity";
        public const string Settings = Root + "Settings/";
        public const string Prefabs = Root + "Prefabs/";

        public static ModelAssumptions Assumptions => LoadRequired<ModelAssumptions>(Settings + "ASM_KitchenParameters.asset");

        public static void RequireEditMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Kitchen authoring requires Edit mode.");
        }

        public static Scene RequireKitchenScene()
        {
            RequireEditMode();
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
                throw new InvalidOperationException("Open " + ScenePath + " before authoring the kitchen.");
            return scene;
        }

        public static T LoadRequired<T>(string path) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new InvalidOperationException("Missing " + typeof(T).Name + ": " + path);
            return asset;
        }

        public static void EditPrefab(string path, Action<GameObject> edit)
        {
            RequireEditMode();
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                edit(root);
                if (PrefabUtility.SaveAsPrefabAsset(root, path) == null)
                    throw new InvalidOperationException("Could not save prefab " + path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var separator = path.LastIndexOf('/');
            if (separator < 0) throw new ArgumentException("Expected an Assets-relative folder.", nameof(path));
            var parent = path.Substring(0, separator);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(separator + 1));
        }

        public static void SaveScene(Scene scene)
        {
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
