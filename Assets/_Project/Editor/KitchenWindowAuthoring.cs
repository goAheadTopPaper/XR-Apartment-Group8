using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using XRApartment.Runtime;
using Object = UnityEngine.Object;

namespace XRApartment.EditorTools
{
    public static partial class KitchenCookingAuthoring
    {
        const string LivingWindowName = "PREF_Cooking_LivingRoomWindow";
        const float LivingWindowWidth = 2.62f;
        const float LivingWindowHeight = 2.40f;
        const float GlassColliderDepth = .035f;
        static readonly Color CookingWindowTint = new Color(.36f, .62f, .72f, .28f);

        public static void ApplyCookingWindowsBatch()
        {
            KitchenAuthoring.RequireEditMode();
            var scene = EditorSceneManager.OpenScene(ScenePath);
            ConfigureCookingWindows(scene, CookingMovement(scene));
            KitchenAuthoring.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        static void ConfigureCookingWindows(Scene scene, KitchenLocomotionSettings movement)
        {
            var windows = scene.GetRootGameObjects().Single(root => root.name == "PREF_Kitchen_Environment")
                .transform.Find("PREF_Kitchen_Windows");
            var existingPanes = new[] { "Window_Dining/Glass_Left", "Window_Dining/Glass_Right", "Window_Sink/Glass" }
                .Select(path => windows.Find(path).GetComponent<BoxCollider>()).ToArray();
            var path = Art + "MAT_Cooking_WindowGlass.mat";
            var glass = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (glass == null)
            {
                glass = new Material(existingPanes[0].GetComponent<Renderer>().sharedMaterial);
                AssetDatabase.CreateAsset(glass, path);
            }
            glass.color = CookingWindowTint;
            KitchenHdrpMaterials.ConfigureTransparent(glass, preserveSpecular: true);
            foreach (var pane in existingPanes)
            {
                var renderer = pane.GetComponent<Renderer>();
                renderer.sharedMaterial = glass;
                var thickness = GlassColliderDepth / Mathf.Abs(pane.transform.lossyScale.x);
                pane.size = new Vector3(thickness, pane.size.y, pane.size.z);
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                PrefabUtility.RecordPrefabInstancePropertyModifications(pane);
            }

            var frame = windows.Find("Window_Dining/Frame_Mullion").GetComponent<Renderer>().sharedMaterial;
            var prefab = BuildLivingWindow(glass, frame);
            var livingWindow = scene.GetRootGameObjects().SingleOrDefault(root => root.name == LivingWindowName);
            if (livingWindow == null) livingWindow = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            // Seat the glass behind the existing SM_WallDoor jambs in the west living-room opening.
            livingWindow.transform.SetPositionAndRotation(new Vector3(-4.575f, 1.225f, -4.32f), Quaternion.identity);
            PrefabUtility.RecordPrefabInstancePropertyModifications(livingWindow.transform);
            var panes = existingPanes.Concat(livingWindow.GetComponentsInChildren<BoxCollider>()).ToArray();
            var fields = new SerializedObject(movement);
            var paneField = fields.FindProperty("windowPanes");
            paneField.arraySize = panes.Length;
            for (var i = 0; i < panes.Length; i++) paneField.GetArrayElementAtIndex(i).objectReferenceValue = panes[i];
            fields.ApplyModifiedPropertiesWithoutUndo();

            const string source = "Cooking living-room SM_WallDoor aperture; fitted prototype glazing.";
            Parameter("cooking_living_window_width", LivingWindowWidth, source);
            Parameter("cooking_living_window_height", LivingWindowHeight, source);
            Parameter("cooking_window_collision_depth", GlassColliderDepth, source);
        }

        static GameObject BuildLivingWindow(Material glass, Material frame)
        {
            const float frameFace = .045f;
            const float glassFace = .008f;
            const float collisionHeight = 2.46f;
            var root = new GameObject(LivingWindowName);
            try
            {
                var halfWidth = LivingWindowWidth * .5f;
                var halfHeight = LivingWindowHeight * .5f;
                var insideHeight = LivingWindowHeight - frameFace * 2f;
                var paneWidth = halfWidth - frameFace * .5f;
                AddPane("Glass_Left", -paneWidth * .5f);
                AddPane("Glass_Right", paneWidth * .5f);
                Block(root.transform, "Frame_Left", new Vector3(0f, 0f, -halfWidth + frameFace * .5f), new Vector3(.066f, insideHeight, frameFace), frame);
                Block(root.transform, "Frame_Right", new Vector3(0f, 0f, halfWidth - frameFace * .5f), new Vector3(.066f, insideHeight, frameFace), frame);
                Block(root.transform, "Frame_Bottom", new Vector3(0f, -halfHeight + frameFace * .5f, 0f), new Vector3(.066f, frameFace, LivingWindowWidth), frame);
                Block(root.transform, "Frame_Top", new Vector3(0f, halfHeight - frameFace * .5f, 0f), new Vector3(.066f, frameFace, LivingWindowWidth), frame);
                Block(root.transform, "Frame_Mullion", Vector3.zero, new Vector3(.066f, insideHeight, frameFace), frame);
                return PrefabUtility.SaveAsPrefabAsset(root, Prefabs + LivingWindowName + ".prefab");

                void AddPane(string name, float z)
                {
                    var position = new Vector3(0f, 0f, z);
                    var size = new Vector3(glassFace, insideHeight, paneWidth);
                    var pane = Cube(root.transform, name, position, size, glass);
                    pane.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                    var collider = pane.GetComponent<BoxCollider>();
                    // Extend to the floor and behind the frame, with a small overlap at the center seam.
                    collider.center = new Vector3(0f, -.015f / insideHeight, 0f);
                    collider.size = new Vector3(GlassColliderDepth / glassFace, collisionHeight / insideHeight,
                        (halfWidth + .01f) / paneWidth);
                }
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
