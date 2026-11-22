using System.Collections.Generic;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.XR.Management;

namespace XRApartment.EditorTools
{
    /// <summary>
    /// Configures the approved Standalone PC XR setup without switching build target.
    /// Run with: Unity.exe -batchmode -executeMethod XRApartment.EditorTools.XRProjectBootstrap.Configure
    /// </summary>
    public static class XRProjectBootstrap
    {
        const string Marker = "[XRBOOT]";
        const string OpenXrLoaderTypeName = "UnityEngine.XR.OpenXR.OpenXRLoader";

        // ProjectSettings.asset "activeInputHandler": 0 = legacy, 1 = new Input System only, 2 = both.
        const int NewInputSystemOnly = 1;

        static readonly string[] ProjectFolders =
        {
            KitchenAuthoring.Root + "Scenes",
            KitchenAuthoring.Root + "Prefabs",
            KitchenAuthoring.Root + "Materials",
            KitchenAuthoring.Root + "Scripts",
            KitchenAuthoring.Root + "Settings",
            KitchenAuthoring.Root + "XR",
        };

        public static void Configure()
        {
            SetActiveInputHandler(NewInputSystemOnly);

            var container = EnsureSettingsContainer();
            EnableOpenXr(container, BuildTargetGroup.Standalone);
            CreateFolders();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Report();
        }

        static XRGeneralSettingsPerBuildTarget EnsureSettingsContainer()
        {
            if (EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.settingsKey,
                    out XRGeneralSettingsPerBuildTarget existing) && existing != null)
                return existing;

            if (!AssetDatabase.IsValidFolder("Assets/XR"))
                AssetDatabase.CreateFolder("Assets", "XR");

            const string path = "Assets/XR/XRGeneralSettingsPerBuildTarget.asset";
            var container = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(path);
            if (container == null)
            {
                container = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(container, path);
            }

            EditorBuildSettings.AddConfigObject(XRGeneralSettings.settingsKey, container, true);
            return container;
        }

        static void SetActiveInputHandler(int value)
        {
            var settings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (settings.Length == 0)
            {
                Debug.LogError($"{Marker} ProjectSettings.asset not found");
                return;
            }

            var serialized = new SerializedObject(settings[0]);
            var property = serialized.FindProperty("activeInputHandler");
            if (property == null)
            {
                Debug.LogError($"{Marker} activeInputHandler property not found");
                return;
            }

            property.intValue = value;
            serialized.ApplyModifiedProperties();
        }

        static int GetActiveInputHandler()
        {
            var settings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (settings.Length == 0)
                return -1;

            var property = new SerializedObject(settings[0]).FindProperty("activeInputHandler");
            return property?.intValue ?? -1;
        }

        static void EnableOpenXr(XRGeneralSettingsPerBuildTarget container, BuildTargetGroup group)
        {
            if (!container.HasManagerSettingsForBuildTarget(group))
                container.CreateDefaultManagerSettingsForBuildTarget(group);

            var manager = container.ManagerSettingsForBuildTarget(group);
            if (manager == null)
            {
                Debug.LogError($"{Marker} {group}: no XRManagerSettings created");
                return;
            }

            foreach (var loader in manager.activeLoaders)
                if (loader != null && loader.GetType().FullName == OpenXrLoaderTypeName) return;

            var assigned = XRPackageMetadataStore.AssignLoader(manager, OpenXrLoaderTypeName, group);
            Debug.Log($"{Marker} {group}: assign OpenXR loader = {assigned}; " +
                      $"active = [{Describe(manager)}]");
        }

        static void CreateFolders()
        {
            foreach (var folder in ProjectFolders)
                KitchenAuthoring.EnsureFolder(folder);
        }

        static string Describe(XRManagerSettings manager)
        {
            var names = new List<string>();
            foreach (var loader in manager.activeLoaders)
                if (loader != null)
                    names.Add(loader.GetType().Name);
            return string.Join(", ", names);
        }

        static void Report()
        {
            var container = EnsureSettingsContainer();
            var manager = container.ManagerSettingsForBuildTarget(BuildTargetGroup.Standalone);
            Debug.Log($"{Marker} VERIFY Standalone: [{(manager == null ? "null" : Describe(manager))}]");
            Debug.Log($"{Marker} VERIFY activeInputHandler={GetActiveInputHandler()}");
            Debug.Log($"{Marker} VERIFY activeBuildTarget={EditorUserBuildSettings.activeBuildTarget}");
        }
    }
}
