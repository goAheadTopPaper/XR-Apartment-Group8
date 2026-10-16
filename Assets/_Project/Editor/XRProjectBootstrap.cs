using System.Collections.Generic;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.XR.Management;

namespace XRApartment.EditorTools
{
    /// <summary>
    /// One-shot project configuration for the dual-target (PC VR + Quest) XR setup.
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
            "Assets/_Project/Scenes",
            "Assets/_Project/Prefabs",
            "Assets/_Project/Materials",
            "Assets/_Project/Scripts",
            "Assets/_Project/Settings",
            "Assets/_Project/XR",
        };

        public static void Configure()
        {
            SetActiveInputHandler(NewInputSystemOnly);

            var container = EnsureSettingsContainer();
            foreach (var group in new[] { BuildTargetGroup.Standalone, BuildTargetGroup.Android })
                EnableOpenXr(container, group);

            ConfigureAndroid();
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

            foreach (var loader in new List<XRLoader>(manager.activeLoaders))
                if (loader != null)
                    XRPackageMetadataStore.RemoveLoader(manager, loader.GetType().FullName, group);

            var assigned = XRPackageMetadataStore.AssignLoader(manager, OpenXrLoaderTypeName, group);
            Debug.Log($"{Marker} {group}: assign OpenXR loader = {assigned}; " +
                      $"active = [{Describe(manager)}]");
        }

        static void ConfigureAndroid()
        {
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.SetGraphicsAPIs(
                BuildTarget.Android,
                new[]
                {
                    UnityEngine.Rendering.GraphicsDeviceType.Vulkan,
                    UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3
                });
        }

        static void CreateFolders()
        {
            foreach (var folder in ProjectFolders)
            {
                var parts = folder.Split('/');
                for (var i = 1; i < parts.Length; i++)
                {
                    var path = string.Join("/", parts, 0, i + 1);
                    if (!AssetDatabase.IsValidFolder(path))
                        AssetDatabase.CreateFolder(string.Join("/", parts, 0, i), parts[i]);
                }
            }
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
            foreach (var group in new[] { BuildTargetGroup.Standalone, BuildTargetGroup.Android })
            {
                var manager = container.ManagerSettingsForBuildTarget(group);
                Debug.Log($"{Marker} VERIFY {group}: [{(manager == null ? "null" : Describe(manager))}]");
            }

            Debug.Log($"{Marker} VERIFY activeInputHandler={GetActiveInputHandler()} " +
                      $"androidBackend={PlayerSettings.GetScriptingBackend(BuildTargetGroup.Android)} " +
                      $"androidMinSdk={PlayerSettings.Android.minSdkVersion} " +
                      $"androidArch={PlayerSettings.Android.targetArchitectures}");
            Debug.Log($"{Marker} VERIFY activeBuildTarget={EditorUserBuildSettings.activeBuildTarget}");
        }
    }
}
