using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using XRApartment.Data;
using XRApartment.Data.ReviewV2;
using XRApartment.Runtime;
using XRApartment.Runtime.ReviewV2;
using Object = UnityEngine.Object;

namespace XRApartment.EditorTools
{
    public static class ReviewV2SchemeAuthoring
    {
        public const string Folder = "Assets/_Project/Prefabs/ReviewV2/Schemes";
        public const string ScenePath = "Assets/_Project/Scenes/SCN_Kitchen_ReviewV2_M2.unity";
        public const string M1RegressionPath = "Assets/_Project/Scenes/SCN_Kitchen_ReviewV2_M1Regression.unity";
        static string SourcePath => File.Exists(M1RegressionPath) ? M1RegressionPath : ReviewV2Authoring.ScenePath;
        public static string LayoutPath(string id) => Folder + "/PREF_" + id + ".prefab";
        public static void IsolateHdrpLighting()
        {
            foreach (var id in new[] { SchemeIds.IntegratedKitchen, SchemeIds.ExternalRefrigerator })
            {
                var root = PrefabUtility.LoadPrefabContents(LayoutPath(id));
                try
                {
                    var world = root.GetComponent<ReviewSchemeWorld>();
                    if (id == SchemeIds.ExternalRefrigerator) ReviewV2BFridgeAuthoring.ConfigureWorld(world);
                    ConfigureRealtimeLighting(world);
                    world.modelVersion = id == SchemeIds.IntegratedKitchen ? ReviewV2IntegratedCabinetAuthoring.Version : ReviewV2BFridgeAuthoring.Version;
                    PrefabUtility.SaveAsPrefabAsset(root, LayoutPath(id));
                    File.WriteAllLines("Docs/M2/" + id + "_Identities.tsv", world.identities.Select(i => id + "\t" + world.modelVersion + "\t" + i.Id + "\t" + i.name));
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            var scene = EditorSceneManager.OpenScene(ReviewV2Authoring.ScenePath);
            All<ReviewSessionHost>(scene).Single().modelVersion = ReviewV2IntegratedCabinetAuthoring.Version;
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets(); Debug.Log("REVIEW_V2_M2_HDRP_ISOLATED: realtime HDRP probes, old baked/custom textures cleared");
        }
        static void ConfigureRealtimeLighting(ReviewSchemeWorld world)
        {
            foreach (var renderer in world.GetComponentsInChildren<Renderer>(true))
            { renderer.lightmapIndex = -1; renderer.realtimeLightmapIndex = -1; renderer.lightProbeUsage = LightProbeUsage.BlendProbes; }
            foreach (var light in world.GetComponentsInChildren<Light>(true)) light.lightmapBakeType = LightmapBakeType.Realtime;
            foreach (var probe in world.GetComponentsInChildren<ReflectionProbe>(true))
            { probe.mode = ReflectionProbeMode.Realtime; probe.refreshMode = ReflectionProbeRefreshMode.OnAwake; probe.bakedTexture = null; probe.customBakedTexture = null; }
            foreach (var probe in world.GetComponentsInChildren<HDAdditionalReflectionData>(true))
            {
                probe.mode = ProbeSettings.Mode.Realtime; probe.realtimeMode = ProbeSettings.RealtimeMode.OnEnable;
                probe.bakedTexture = null; probe.customTexture = null;
            }
        }
        public static void IntegrateMainScene()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Requires Edit mode");
            var scene = EditorSceneManager.OpenScene(ReviewV2Authoring.ScenePath);
            var host = All<ReviewSessionHost>(scene).Single();
            if (host.GetComponent<ReviewSchemePort>() != null || File.Exists(M1RegressionPath))
                throw new InvalidOperationException("Integration already exists; inspect instead of repeating");
            var layouts = new[] { SchemeIds.IntegratedKitchen, SchemeIds.ExternalRefrigerator }
                .Select(id => AssetDatabase.LoadAssetAtPath<GameObject>(LayoutPath(id)).GetComponent<ReviewSchemeWorld>()).ToArray();
            if (layouts.Any(w => !w.IsConfigured)) throw new InvalidOperationException("Incomplete layouts");
            // Preserve the exact current M1 wiring as an explicit historical regression fixture.
            if (!EditorSceneManager.SaveScene(scene, M1RegressionPath, true)) throw new IOException("Cannot preserve M1 regression scene");
            var baseline = new GameObject("M1 baseline - preserved inactive");
            foreach (var root in scene.GetRootGameObjects().Where(r => r != baseline && !SessionRoot(r)))
                root.transform.SetParent(baseline.transform, true);
            baseline.SetActive(false);
            if (host.groceries != null) host.groceries.enabled = false;
            var port = host.gameObject.AddComponent<ReviewSchemePort>(); port.host = host; port.layouts = layouts;
            host.schemePort = port; host.initialSchemeId = SchemeIds.IntegratedKitchen; host.modelVersion = layouts[0].modelVersion;
            Lightmapping.lightingDataAsset = null; LightmapSettings.lightmaps = Array.Empty<LightmapData>();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Cannot save incrementally integrated V2 scene");
            AssetDatabase.SaveAssets(); Debug.Log("REVIEW_V2_M2_MAIN_INTEGRATED: M1 objects preserved inactive; existing UI and session wiring retained");
        }
        public static void RefineExternalFridgeNook()
        {
            var root = PrefabUtility.LoadPrefabContents(LayoutPath(SchemeIds.ExternalRefrigerator));
            try
            {
                ConfigureExternalNook(root);
                var world = root.GetComponent<ReviewSchemeWorld>(); ReviewV2BFridgeAuthoring.ConfigureWorld(world);
                ConfigureRealtimeLighting(world);
                PrefabUtility.SaveAsPrefabAsset(root, LayoutPath(SchemeIds.ExternalRefrigerator));
                File.WriteAllLines("Docs/M2/SCHEME_B_Identities.tsv", world.identities.Select(i => world.schemeId + "\t" + world.modelVersion + "\t" + i.Id + "\t" + i.name));
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("REVIEW_V2_M2_B_NOOK: B-only shelf replacement, enclosure, floor and isolated lighting, model " + ReviewV2BFridgeAuthoring.Version);
        }
        static void ConfigureExternalNook(GameObject root)
        {
            root.GetComponentsInChildren<Transform>(true).Single(t => t.name == "SM_WindowTable").gameObject.SetActive(false);
            var ground = root.GetComponentsInChildren<MeshRenderer>(true).First(r => r.name == "SM_Ground").sharedMaterial;
            var wall = root.GetComponentsInChildren<MeshRenderer>(true).First(r => r.name == "SM_Wall_01").sharedMaterial;
            if (root.transform.Find("B_WindowFloorSupport") == null)
                Box(root.transform, "B_WindowFloorSupport", new Vector3(-4.8f,-.03f,-.86f), new Vector3(.7f,.06f,2.5f), ground);
            if (root.transform.Find("B_WindowLowerInfill") == null)
                Box(root.transform, "B_WindowLowerInfill", new Vector3(-5.13f,.55f,-.86f), new Vector3(.08f,1.1f,2.5f), wall);
        }
        public static void AddSemanticAnchors()
        {
            foreach (var id in new[] { SchemeIds.IntegratedKitchen, SchemeIds.ExternalRefrigerator })
            {
                var root = PrefabUtility.LoadPrefabContents(LayoutPath(id));
                try
                {
                    var world = root.GetComponent<ReviewSchemeWorld>();
                    void Anchor(string key, Vector3 position)
                    {
                        if (root.GetComponentsInChildren<ReviewSceneIdentity>(true).Any(i => i.Id == key)) return;
                        var anchor = new GameObject(key); anchor.transform.SetParent(root.transform, false); anchor.transform.position = position;
                        anchor.AddComponent<ReviewSceneIdentity>().Configure(key);
                    }
                    Anchor("WORKTOP", world.station.transform.Find("PREF_Cooking_ChoppingBoard").position);
                    var table = root.GetComponentsInChildren<BoxCollider>(true).Single(c => c.name == "COL_Tabletop");
                    Anchor("DINING_TABLE", table.transform.TransformPoint(table.center));
                    Anchor("SINK", root.GetComponentsInChildren<WaterTap>(true).First().transform.position);
                    Anchor("SCENE_ANCHOR", Vector3.zero);
                    world.identities = root.GetComponentsInChildren<ReviewSceneIdentity>(true);
                    PrefabUtility.SaveAsPrefabAsset(root, LayoutPath(id));
                    File.WriteAllLines("Docs/M2/" + id + "_Identities.tsv", world.identities.Select(i => id + "\t" + world.modelVersion + "\t" + i.Id + "\t" + i.name));
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets(); Debug.Log("REVIEW_V2_M2_SEMANTIC_ANCHORS_ADDED");
        }
        static bool SessionRoot(GameObject r) => r.name == "PREF_XR_KitchenRig" || r.name == "EventSystem" ||
            r.name == "XR Interaction Manager" || r.name == "ReviewV2 Session" || r.name == "PREF_UI_ReviewV2Integration";
        static T[] All<T>(Scene s) where T : Component => s.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
        // Creates new isolated assets only. Never saves the M1 source or shared Prefabs.
        public static void Create()
        {
            if (Application.isPlaying || File.Exists(ScenePath) || File.Exists(LayoutPath(SchemeIds.IntegratedKitchen)) ||
                File.Exists(LayoutPath(SchemeIds.ExternalRefrigerator))) throw new InvalidOperationException("M2 assets already exist; inspect instead of overwriting");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/_Project/Prefabs/ReviewV2", "Schemes");
            foreach (var id in new[] { SchemeIds.IntegratedKitchen, SchemeIds.ExternalRefrigerator }) BuildWorld(id);
            var scene = EditorSceneManager.OpenScene(SourcePath);
            foreach (var root in scene.GetRootGameObjects().Where(r => !SessionRoot(r))) Object.DestroyImmediate(root);
            var host = All<ReviewSessionHost>(scene).Single();
            Object.DestroyImmediate(host.GetComponent<ReviewGroceryActivity>());
            var port = host.gameObject.AddComponent<ReviewSchemePort>(); port.host = host;
            port.layouts = new[] { SchemeIds.IntegratedKitchen, SchemeIds.ExternalRefrigerator }
                .Select(id => AssetDatabase.LoadAssetAtPath<GameObject>(LayoutPath(id)).GetComponent<ReviewSchemeWorld>()).ToArray();
            host.schemePort = port; host.initialSchemeId = SchemeIds.IntegratedKitchen;
            host.modelVersion = port.layouts[0].modelVersion; host.identities = Array.Empty<ReviewSceneIdentity>();
            host.station = null; host.recipe = null; host.roomReset = null; host.groceries = null;
            Lightmapping.lightingDataAsset = null;
            LightmapSettings.lightmaps = Array.Empty<LightmapData>();
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Cannot save isolated M2 integration scene");
            AssetDatabase.SaveAssets(); Debug.Log("REVIEW_V2_M2_AUTHORED: isolated A/B worlds and M2 scene; build settings unchanged");
        }
        static void BuildWorld(string id)
        {
            var scene = EditorSceneManager.OpenScene(SourcePath);
            var host = All<ReviewSessionHost>(scene).Single();
            var world = new GameObject("WORLD_" + id).AddComponent<ReviewSchemeWorld>();
            world.schemeId = id; world.modelVersion = id == SchemeIds.IntegratedKitchen ? ReviewV2IntegratedCabinetAuthoring.Version : ReviewV2BFridgeAuthoring.Version;
            world.station = host.station; world.recipe = host.recipe; world.roomReset = host.roomReset;
            world.groceries = world.gameObject.AddComponent<ReviewGroceryActivity>();
            EditorUtility.CopySerialized(host.groceries, world.groceries); world.groceries.host = null;
            foreach (var root in scene.GetRootGameObjects().Where(r => r != world.gameObject && !SessionRoot(r) && r.name != "SceneIDMap")) root.transform.SetParent(world.transform, true);
            foreach (var bridge in world.GetComponentsInChildren<CookingGrabBridge>(true)) bridge.host = null;
            // Layout Lab and its authored panel are excluded from the new product resources.
            foreach (var lab in world.GetComponentsInChildren<KitchenLayoutLab>(true)) Object.DestroyImmediate(lab.gameObject);
            if (id == SchemeIds.IntegratedKitchen) MakeDoubleDoor(world.station.ActiveFridge);
            else { MoveExternalFridge(world); ConfigureExternalNook(world.gameObject); }
            AddCabinet(world, id == SchemeIds.IntegratedKitchen);
            foreach (var go in world.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(go.gameObject, 0);
            ConfigureRealtimeLighting(world);
            world.identities = world.GetComponentsInChildren<ReviewSceneIdentity>(true);
            if (!world.IsConfigured) throw new InvalidOperationException("Incomplete world " + id);
            Directory.CreateDirectory("Docs/M2");
            File.WriteAllLines("Docs/M2/" + id + "_Identities.tsv", world.identities.Select(i => id + "\t" + world.modelVersion + "\t" + i.Id + "\t" + i.name));
            world.gameObject.SetActive(false);
            if (PrefabUtility.SaveAsPrefabAsset(world.gameObject, LayoutPath(id)) == null)
                throw new IOException("Cannot save world Prefab: " + id);
        }
        static void MoveExternalFridge(ReviewSchemeWorld world)
        {
            ReviewV2BFridgeAuthoring.ConfigureWorld(world);
        }
        static GameObject Box(Transform parent, string name, Vector3 centre, Vector3 size, Material material)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube); box.name = name; box.transform.SetParent(parent, false);
            box.transform.localPosition = centre; box.transform.localScale = size; box.GetComponent<Renderer>().sharedMaterial = material; return box;
        }
        static void MakeDoubleDoor(KitchenFridge fridge)
        {
            var sourceDoor = fridge.Door; var data = new SerializedObject(fridge);
            var profile = (KitchenCookingProfile)data.FindProperty("profile").objectReferenceValue;
            Object.DestroyImmediate(sourceDoor.gameObject);
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Cooking/Furniture/MAT_Furniture_FridgeEnamel.mat");
            KinematicSwingDoor MakeLeaf(float side)
            {
                var hinge = new GameObject(side > 0 ? "ACT_Door_Right" : "ACT_Door_Left"); hinge.transform.SetParent(fridge.transform, false);
                hinge.transform.localPosition = new Vector3(side * .345f, .04f, .37f);
                Box(hinge.transform, "DoorPanel", new Vector3(-side * .1725f, .88f, .03f), new Vector3(.34f, 1.73f, .046f), material);
                var handle = Box(hinge.transform, "Handle", new Vector3(-side * .29f, 1.05f, .09f), new Vector3(.025f, .4f, .025f), material);
                var interactable = hinge.AddComponent<XRSimpleInteractable>(); interactable.colliders.Clear();
                interactable.colliders.AddRange(hinge.GetComponentsInChildren<Collider>());
                var probe = new GameObject("COL_SweepProbe").AddComponent<BoxCollider>(); probe.transform.SetParent(hinge.transform, false);
                probe.center = new Vector3(-side * .1725f, .88f, .05f); probe.size = new Vector3(.34f, 1.73f, .14f); probe.enabled = false;
                var leaf = hinge.AddComponent<KinematicSwingDoor>(); var d = new SerializedObject(leaf);
                d.FindProperty("swingSign").floatValue = side; d.FindProperty("openAngleDegrees").floatValue = 105;
                d.FindProperty("sweepProbe").objectReferenceValue = probe;
                d.FindProperty("collisionProfile").objectReferenceValue = AssetDatabase.LoadAssetAtPath<HandInteractionProfile>("Assets/_Project/Settings/SET_HandInteraction.asset");
                d.ApplyModifiedPropertiesWithoutUndo(); leaf.ConfigurePullHandle(handle.transform); return leaf;
            }
            var right = MakeLeaf(1); var left = MakeLeaf(-1);
            fridge.Configure(right, profile, new Bounds(new Vector3(0, .94f, 0), new Vector3(.66f, 1.7f, .69f)));
            fridge.ConfigureSecondaryDoor(left);
        }
        static void AddCabinet(ReviewSchemeWorld world, bool integrated)
        {
            if (integrated) { ReviewV2IntegratedCabinetAuthoring.Attach(world); return; }
            ReviewV2BStorageAuthoring.Attach(world);
        }
    }
}
