using System.Linq;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using XRApartment.Runtime;
using XRApartment.Runtime.UI;
using Object = UnityEngine.Object;

namespace XRApartment.EditorTools
{
    public static partial class KitchenCookingAuthoring
    {
        private const string LabPath = Prefabs + "PREF_Cooking_LayoutLab.prefab";
        private const string LabMenuPath = KitchenAuthoring.Prefabs + "UI/PREF_UI_KitchenLayoutLab.prefab";
        private const string CompactFridgeName = "FRIDGE_Compact";
        private const float CompactFridgeScale = .85f;
        private const float ForwardDockOffset = .16f;
        private static readonly Vector3 CookingFridgeDock = new Vector3(-1.81f, 0f, 1.055f);
        private static readonly Quaternion CookingFridgeRotation = Quaternion.Euler(0f, -90f, 0f);
        private static readonly Vector3 PantryOffset = new Vector3(-.10f, 0f, .83f);
        private static readonly Vector3 PantrySize = new Vector3(.25f, 1.82f, .26f);
        private static readonly KitchenLayoutLab.FinishPreset[] LayoutFinishes =
        {
            new KitchenLayoutLab.FinishPreset { label = "Warm Ivory" },
            new KitchenLayoutLab.FinishPreset { label = "Sage", furniture = new Color(.55f, .67f, .62f),
                enamel = new Color(.62f, .73f, .68f) },
            new KitchenLayoutLab.FinishPreset { label = "Graphite", furniture = new Color(.39f, .43f, .50f),
                enamel = new Color(.36f, .43f, .51f) }
        };

        [MenuItem("XR Apartment/Cooking/Apply Layout Lab")]
        public static void ApplyLayoutLab()
        {
            KitchenAuthoring.RequireEditMode();
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            KitchenAuthoring.EditPrefab(StationPath, RelocateCookingFridge);
            var scene = EditorSceneManager.OpenScene(ScenePath);
            ConfigureLayoutLab(scene);
            KitchenAuthoring.SaveScene(scene);
        }

        private static void RelocateCookingFridge(GameObject workbench)
        {
            var fridge = workbench.GetComponent<KitchenCookingStation>().ActiveFridge.transform;
            if (fridge.position != CookingFridgeDock || fridge.rotation != CookingFridgeRotation)
            {
                var previousPosition = fridge.position;
                var turn = CookingFridgeRotation * Quaternion.Inverse(fridge.rotation);
                foreach (var stock in workbench.GetComponentsInChildren<KitchenStoredIngredient>(true))
                {
                    var item = stock.transform;
                    item.SetPositionAndRotation(CookingFridgeDock + turn * (item.position - previousPosition),
                        turn * item.rotation);
                }
                fridge.SetPositionAndRotation(CookingFridgeDock, CookingFridgeRotation);
            }

            EnsureCompactFridge(workbench);
            var guide = workbench.transform.Find("UI_FridgeInstructions");
            if (guide != null) PlaceFridgeGuide(guide, fridge);
        }

        private static void EnsureCompactFridge(GameObject station)
        {
            var standard = station.GetComponentInChildren<KitchenFridge>();
            var compact = station.transform.Find(CompactFridgeName);
            if (compact == null)
            {
                var source = KitchenAuthoring.LoadRequired<GameObject>(RecipePrefabs + "PREF_Cooking_Fridge.prefab");
                compact = ((GameObject)PrefabUtility.InstantiatePrefab(source, station.transform)).transform;
                compact.name = CompactFridgeName;
            }
            compact.SetLocalPositionAndRotation(standard.transform.localPosition, standard.transform.localRotation);
            compact.localScale = Vector3.one * CompactFridgeScale;
            compact.gameObject.SetActive(false);
        }

        private static void ConfigureLayoutLab(Scene scene)
        {
            var roots = scene.GetRootGameObjects();
            var station = roots.Select(root => root.GetComponent<KitchenCookingStation>()).Single(item => item != null);
            var fridge = station.GetComponentInChildren<KitchenFridge>();
            var compact = station.transform.Find(CompactFridgeName).GetComponent<KitchenFridge>();
            var rig = roots.Select(root => root.GetComponent<XROrigin>()).Single(item => item != null);
            var movement = rig.GetComponentInChildren<LocomotionSettingsView>(true);
            var fridgeBounds = FridgeEnvelope(fridge);
            var compactBounds = new Bounds(fridgeBounds.center * CompactFridgeScale, fridgeBounds.size * CompactFridgeScale);
            var dock = fridge.transform.position;
            var rotation = fridge.transform.rotation;
            var forwardDock = dock + rotation * Vector3.forward * ForwardDockOffset;
            var pantryDock = dock + rotation * PantryOffset;
            BuildLabPrefab(dock, rotation, forwardDock, pantryDock, fridgeBounds, compactBounds);
            BuildLabMenu();

            var lab = roots.Select(root => root.GetComponent<KitchenLayoutLab>()).SingleOrDefault(item => item != null);
            if (lab == null)
                lab = ((GameObject)PrefabUtility.InstantiatePrefab(KitchenAuthoring.LoadRequired<GameObject>(LabPath), scene))
                    .GetComponent<KitchenLayoutLab>();
            var labData = new SerializedObject(lab);
            labData.FindProperty("station").objectReferenceValue = station;
            labData.FindProperty("fridge").objectReferenceValue = fridge;
            labData.FindProperty("compactFridge").objectReferenceValue = compact;
            labData.FindProperty("player").objectReferenceValue = rig;
            labData.ApplyModifiedPropertiesWithoutUndo();

            var environment = roots.Single(root => root.name == "PREF_Kitchen_Environment");
            var cabinet = environment.transform.Find("SM_KitchenTable");
            RestoreCookingCabinet(cabinet);
            var furniture = new Renderer[]
            {
                cabinet.GetComponent<MeshRenderer>(),
                lab.transform.Find("MOD_Pantry/VIS_PantryCase")?.GetComponent<MeshRenderer>()
            };
            var enamel = KitchenAuthoring.LoadRequired<Material>(KitchenAuthoring.Root +
                "Art/Cooking/Furniture/MAT_Furniture_FridgeEnamel.mat");
            var shells = new[] { fridge, compact }.SelectMany(item =>
                item.GetComponentsInChildren<MeshRenderer>(true).Where(renderer => renderer.sharedMaterial == enamel))
                .ToArray();
            if (furniture.Any(target => target == null) || shells.Length == 0)
                throw new System.InvalidOperationException("Layout lab finish surfaces are missing.");
            lab.ConfigureFinishTargets(furniture, shells);
            PrefabUtility.RecordPrefabInstancePropertyModifications(lab);

            var menu = movement.GetComponentInChildren<KitchenLayoutMenu>(true);
            if (menu == null)
                menu = ((GameObject)PrefabUtility.InstantiatePrefab(KitchenAuthoring.LoadRequired<GameObject>(LabMenuPath),
                    movement.transform)).GetComponent<KitchenLayoutMenu>();
            var menuRect = new SerializedObject(menu.transform);
            var size = menuRect.FindProperty("m_SizeDelta");
            if (size.prefabOverride) PrefabUtility.RevertPropertyOverride(size, InteractionMode.AutomatedAction);
            var menuData = new SerializedObject(menu);
            menuData.FindProperty("lab").objectReferenceValue = lab;
            menuData.ApplyModifiedPropertiesWithoutUndo();
            var movementData = new SerializedObject(movement);
            movementData.FindProperty("layout").objectReferenceValue = menu;
            movementData.ApplyModifiedPropertiesWithoutUndo();

            Parameter("cooking_fridge_center_x", dock.x);
            Parameter("cooking_fridge_center_z", dock.z);
            Parameter("layout_forward_fridge_x", forwardDock.x);
            Parameter("layout_forward_fridge_z", forwardDock.z);
            Parameter("layout_pantry_x", pantryDock.x);
            Parameter("layout_pantry_z", pantryDock.z);
            Parameter("layout_pantry_width", PantrySize.x);
            Parameter("layout_pantry_height", PantrySize.y);
            Parameter("layout_pantry_depth", PantrySize.z);
            Parameter("layout_compact_fridge_width", .72f * CompactFridgeScale);
            Parameter("layout_compact_fridge_height", 1.81f * CompactFridgeScale);
            Parameter("layout_compact_fridge_closed_depth", compactBounds.size.z);
        }

        private static void RestoreCookingCabinet(Transform cabinet)
        {
            const string originalPath = KitchenAuthoring.Root + "Art/Kitchen/Meshes/SM_KitchenTable.fbx";
            var original = KitchenAuthoring.LoadRequired<Mesh>(originalPath);
            var mesh = cabinet.GetComponent<MeshFilter>();
            var collider = cabinet.GetComponent<MeshCollider>();
            mesh.sharedMesh = original;
            collider.sharedMesh = original;
            PrefabUtility.RecordPrefabInstancePropertyModifications(mesh);
            PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
        }

        private static Bounds FridgeEnvelope(KitchenFridge fridge)
        {
            Physics.SyncTransforms();
            var local = new Bounds(Vector3.zero, Vector3.zero);
            foreach (var part in fridge.GetComponentsInChildren<BoxCollider>().Where(collider => collider.enabled))
            {
                var b = part.bounds;
                local.Encapsulate(fridge.transform.InverseTransformPoint(b.min));
                local.Encapsulate(fridge.transform.InverseTransformPoint(b.max));
            }
            return local;
        }

        private static void BuildLabPrefab(Vector3 position, Quaternion rotation, Vector3 forwardDock,
            Vector3 pantryDock, Bounds fridgeBounds, Bounds compactBounds)
        {
            var root = new GameObject("PREF_Cooking_LayoutLab");
            try
            {
                var current = Child(root.transform, "LOC_Fridge_Original");
                current.SetPositionAndRotation(position, rotation);
                var forward = Child(root.transform, "LOC_Fridge_Forward");
                forward.SetPositionAndRotation(forwardDock, rotation);
                var pantry = Child(root.transform, "LOC_Pantry");
                pantry.SetPositionAndRotation(pantryDock, rotation * Quaternion.Euler(0f, 180f, 0f));
                var cabinet = Child(root.transform, "MOD_Pantry");
                cabinet.SetPositionAndRotation(pantry.position, pantry.rotation);
                var collider = cabinet.gameObject.AddComponent<BoxCollider>();
                collider.center = Vector3.up * (PantrySize.y * .5f);
                collider.size = PantrySize;
                var white = KitchenAuthoring.LoadRequired<Material>(KitchenAuthoring.Root + "Art/Kitchen/Materials/MAT_Kitchen_White.mat");
                var wood = KitchenAuthoring.LoadRequired<Material>(KitchenAuthoring.Root + "Art/Kitchen/Materials/M_WindowTable.mat");
                var caseBody = Cube(cabinet, "VIS_PantryCase", new Vector3(0f, .91f, .009f),
                    new Vector3(.25f, 1.82f, .242f), white);
                Object.DestroyImmediate(caseBody.GetComponent<Collider>());
                for (var section = 0; section < 3; section++)
                {
                    var front = Cube(cabinet, "VIS_PantryFront_" + section,
                        new Vector3(0f, .355f + .58f * section, -.121f),
                        new Vector3(.238f, .568f, .018f), wood);
                    Object.DestroyImmediate(front.GetComponent<Collider>());
                }
                cabinet.gameObject.SetActive(false);
                var options = new[]
                {
                    new KitchenLayoutLab.Preset { label = "A  |  Opposite the sink",
                        description = "The fridge is across from the sink. Try opening it and walking to the worktop.",
                        fridgeSocket = current },
                    new KitchenLayoutLab.Preset { label = "B  |  Forward dock",
                        description = "The fridge sits closer to the sink. Try the opening and the route to the worktop.",
                        fridgeSocket = forward },
                    new KitchenLayoutLab.Preset { label = "C  |  Pantry return",
                        description = "Try opening the fridge with a pantry cabinet beside its doorway.",
                        fridgeSocket = current, panelSocket = pantry }
                };
                root.AddComponent<KitchenLayoutLab>().Configure(collider, fridgeBounds, compactBounds, options, LayoutFinishes);
                PrefabUtility.SaveAsPrefabAsset(root, LabPath);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void BuildLabMenu()
        {
            var root = KitchenLocomotionAuthoring.Rect("PREF_UI_KitchenLayoutLab", null, 0f, 0f, 640f, 672f);
            try
            {
                root.localScale = Vector3.one * .0008f;
                root.gameObject.AddComponent<Image>().color = new Color(.04f, .065f, .09f, .98f);
                KitchenLocomotionAuthoring.Label("Title", root, 0, 286, 590, 52, 34, "LAYOUT LAB");
                var status = KitchenLocomotionAuthoring.Label("Status", root, 0, 210, 580, 83, 23, "");
                var options = new[]
                {
                    KitchenLocomotionAuthoring.Button("Original", root, 0, 129, 566, 50, "A  ·  OPPOSITE THE SINK"),
                    KitchenLocomotionAuthoring.Button("ForwardDock", root, 0, 72, 566, 50, "B  ·  FORWARD DOCK"),
                    KitchenLocomotionAuthoring.Button("PantryReturn", root, 0, 15, 566, 50, "C  ·  PANTRY RETURN")
                };
                var standard = KitchenLocomotionAuthoring.Button("StandardFridge", root, -145, -55, 276, 52, "FULL SIZE  ·  72 CM");
                var compact = KitchenLocomotionAuthoring.Button("CompactFridge", root, 145, -55, 276, 52, "COMPACT  ·  61 CM");
                var measured = KitchenLocomotionAuthoring.Label("ObservedSwing", root, 0, -108, 570, 36, 20, "");
                var appearance = KitchenLocomotionAuthoring.Label("SelectedFinish", root, 0, -148, 570, 32, 20, "");
                var finishOptions = LayoutFinishes.Select((finish, index) =>
                    KitchenLocomotionAuthoring.Button("Finish" + finish.label.Replace(" ", ""), root,
                        (index - 1) * 194, -192, 178, 47, finish.label.ToUpperInvariant())).ToArray();
                KitchenLocomotionAuthoring.Label("ResetNotice", root, -76, -252, 410, 48, 18,
                    "Layout or size changes reset the room and meal.");
                KitchenLocomotionAuthoring.Label("Shortcut", root, 0, -306, 570, 34, 18, "Right B / F9");
                var close = KitchenLocomotionAuthoring.Button("CloseLayoutLab", root, 214, -252, 140, 47, "CLOSE");
                root.gameObject.AddComponent<KitchenLayoutMenu>().Configure(null, status, measured, options,
                    standard, compact, appearance, finishOptions, close);
                root.gameObject.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root.gameObject, LabMenuPath);
            }
            finally { Object.DestroyImmediate(root.gameObject); }
        }
    }
}
