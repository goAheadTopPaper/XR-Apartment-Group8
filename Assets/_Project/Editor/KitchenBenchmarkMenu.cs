using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using XRApartment.Data;
using XRApartment.Runtime;

namespace XRApartment.EditorTools
{
    /// Preset switching for the authored kitchen. Geometry is edited in its prefabs;
    /// these commands only exchange the fridge and toggle its flush surround (D03).
    public static class KitchenBenchmarkMenu
    {
        const string ScenePath = "Assets/_Project/Scenes/SCN_Kitchen_Review.unity";
        const string SettingsRoot = "Assets/_Project/Settings";
        const string PrefabRoot = "Assets/_Project/Prefabs";

        [MenuItem("XR Apartment/Open Kitchen Review Scene")]
        public static void OpenKitchenScene()
        {
            if (Application.isPlaying) return;
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("XR Apartment/Benchmark/Apply B1 - Proud + F2")]
        public static void ApplyB1() => ApplyBenchmark("B1");

        [MenuItem("XR Apartment/Benchmark/Apply B2 - Flush + F2")]
        public static void ApplyB2() => ApplyBenchmark("B2");

        [MenuItem("XR Apartment/Benchmark/Apply B3 - Flush + F4")]
        public static void ApplyB3() => ApplyBenchmark("B3");

        public static void ApplyBenchmark(string benchmarkId)
        {
            var scene = SceneManager.GetActiveScene();
            BenchmarkSlotMarker marker = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                marker = root.GetComponentInChildren<BenchmarkSlotMarker>();
                if (marker != null) break;
            }
            var registry = AssetDatabase.LoadAssetAtPath<BenchmarkRegistry>($"{SettingsRoot}/REG_Benchmarks.asset");
            var definition = registry != null ? registry.Find(benchmarkId) : null;
            var slot = marker != null ? marker.transform.Find("SLOT_Fridge") : null;
            var prefab = definition != null
                ? AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabRoot}/PREF_Fridge_{definition.Fridge}.prefab")
                : null;
            if (slot == null || prefab == null)
            {
                Debug.LogError($"[KITCHEN] Cannot apply {benchmarkId}. Open {ScenePath} and check the registry and fridge prefab.");
                return;
            }

            for (var i = slot.childCount - 1; i >= 0; i--)
            {
                var previous = slot.GetChild(i).gameObject;
                if (Application.isPlaying)
                {
                    previous.SetActive(false);
                    Object.Destroy(previous);
                }
                else Object.DestroyImmediate(previous);
            }
            var instance = Application.isPlaying
                ? Object.Instantiate(prefab, slot, false)
                : (GameObject)PrefabUtility.InstantiatePrefab(prefab, slot);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;

            var flush = definition.Cabinet == CabinetStrategy.FlushOriented;
            foreach (var name in new[] { "TRIM_Filler_Left", "TRIM_Filler_Right", "TRIM_Bridge_Top" })
            {
                var trim = marker.transform.Find(name);
                if (trim == null) continue;
                trim.gameObject.SetActive(flush);
                if (!Application.isPlaying)
                    PrefabUtility.RecordPrefabInstancePropertyModifications(trim.gameObject);
            }
            marker.BenchmarkId = definition.Id;
            marker.Fridge = definition.Fridge;
            marker.Cabinet = definition.Cabinet;
            if (!Application.isPlaying)
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(marker);
                EditorSceneManager.MarkSceneDirty(scene);
            }
            Debug.Log($"[KITCHEN] Applied {definition.Id}: {definition.Cabinet} + {definition.Fridge}");
        }
    }
}
