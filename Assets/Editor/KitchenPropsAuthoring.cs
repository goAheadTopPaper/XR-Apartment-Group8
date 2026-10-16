using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using XRApartment.Data;

/// Builds project-owned props from selected vendor meshes; edits only the dressing prefab.
public static class KitchenPropsAuthoring
{
    const string Root = "Assets/_Project/";
    const string Art = Root + "Art/Props/";
    const string Prefabs = Root + "Prefabs/Props/";
    const string Dressing = Root + "Prefabs/PREF_Kitchen_Dressing.prefab";
    const string Kitchen = "Assets/ThirdParty/KitchenPropsFree/";
    const string Food = "Assets/ThirdParty/FoodPackMixed/";

    [MenuItem("XR Apartment/Props/Configure Kitchen Dressing")]
    public static void Configure()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || scene.path != Root + "Scenes/SCN_Kitchen_Review.unity")
            throw new InvalidOperationException("Open the kitchen in Edit mode before configuring props.");
        Directory.CreateDirectory(Art);
        Directory.CreateDirectory(Prefabs);
        AssetDatabase.Refresh();
        ConfigureImporters();

        var assumptions = AssetDatabase.LoadAssetAtPath<ModelAssumptions>(Root + "Settings/ASM_KitchenParameters.asset");
        BuildProp("Plate", Kitchen + "models/plate.fbx", null, .27f, false, false, assumptions);
        BuildProp("Bowl", Kitchen + "models/bowl.fbx", null, .19f, false, false, assumptions);
        BuildProp("SmallBowl", Kitchen + "models/bowl.fbx", null, .10f, false, false, assumptions);
        BuildProp("Mug", Kitchen + "models/mug.fbx", null, .10f, true, true, assumptions);
        BuildProp("DishRack", Kitchen + "models/dish-rack.fbx", null, .505f, false, false, assumptions);
        BuildProp("Toaster", Kitchen + "models/toaster.fbx", null, .27f, false, false, assumptions);
        BuildProp("Tomato", Food + "Prefabs/Tomato.prefab", "Tomato_Mid", .08f, false, true, assumptions);
        BuildProp("Lemon", Food + "Prefabs/Lemons.prefab", "Lemon1_Mid", .085f, false, true, assumptions);
        BuildProp("Banana", Food + "Prefabs/Banana.prefab", "Banana_Mid", .21f, false, true, assumptions);
        BuildProp("Onion", Food + "Prefabs/Onion.prefab", "Onion_Mid", .075f, false, true, assumptions);
        BuildProp("Egg", Food + "Prefabs/egg.prefab", "egg_Mid", .06f, true, true, assumptions);
        BuildProp("Cheese", Food + "Prefabs/Cheese.prefab", "Cheese_Mid", .12f, false, true, assumptions);
        BuildProp("JuicePack", Food + "Prefabs/JuicePack.prefab", "JuicePack_Mid", .22f, true, true, assumptions);
        BuildProp("CerealBox", Food + "Prefabs/CerealBox.prefab", "CerealBox_Mid", .28f, true, true, assumptions);
        BuildProp("BlackPepper", Food + "Prefabs/BlackPepper.prefab", "BlackPepper_Mid", .065f, false, false, assumptions);
        BuildProp("Oregano", Food + "Prefabs/OreganoPowder.prefab", "OreganoPowder_Mid", .065f, false, false, assumptions);

        // This shared root stays outside SLOT_Fridge, so all benchmarks see the same props.
        var root = new GameObject("PREF_Kitchen_Dressing");
        try
        {
            var washing = Group(root, "SET_Crockery");
            Place(washing, "DishRack", -4.76f, 1.46f, new Vector3(0, 90, 0));
            for (int i = 0; i < 3; i++)
                Place(washing, "Plate", -4.76f, 1.40f + i * .055f, new Vector3(75, 0, 0), .025f);
            Place(washing, "Bowl", -4.76f, 1.83f, Vector3.zero);

            var preparation = Group(root, "SET_Preparation");
            Place(preparation, "Plate", -3.95f, 1.84f, new Vector3(0, 8, 0));
            Place(preparation, "Tomato", -4.00f, 1.85f, new Vector3(0, 15, 0), .013f);
            Place(preparation, "Onion", -3.90f, 1.83f, new Vector3(0, -25, 0), .013f);
            Place(preparation, "SmallBowl", -3.95f, 2.28f, Vector3.zero);
            Place(preparation, "BlackPepper", -3.95f, 2.28f, new Vector3(0, 185, 0), .012f);
            Place(preparation, "SmallBowl", -3.80f, 2.28f, Vector3.zero);
            Place(preparation, "Oregano", -3.80f, 2.28f, new Vector3(0, 170, 0), .012f);

            var breakfast = Group(root, "SET_Breakfast");
            Place(breakfast, "Toaster", -4.76f, .91f, new Vector3(0, 90, 0));
            Place(breakfast, "CerealBox", -4.83f, .72f, new Vector3(0, 270, 0));

            var dining = Group(root, "SET_Dining");
            Place(dining, "Plate", -2.95f, -1.35f, new Vector3(0, 5, 0));
            Place(dining, "Cheese", -3.00f, -1.35f, new Vector3(0, 25, 0), .013f);
            Place(dining, "Mug", -2.70f, -1.13f, new Vector3(0, -35, 0));
            Place(dining, "JuicePack", -2.87f, -.97f, new Vector3(0, 180, 0));
            Place(dining, "Bowl", -2.10f, -1.28f, Vector3.zero);
            Place(dining, "Lemon", -2.13f, -1.27f, new Vector3(0, 15, 0), .025f);
            Place(dining, "Banana", -1.89f, -1.28f, new Vector3(325, 305, 117));
            Place(dining, "Egg", -2.90f, -1.35f, new Vector3(0, 0, 75), .013f);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, Dressing);
            if (prefab == null) throw new InvalidOperationException("Could not save kitchen dressing.");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
        foreach (var previous in scene.GetRootGameObjects().Where(go => go.name == "PREF_Kitchen_Dressing"))
            UnityEngine.Object.DestroyImmediate(previous);
        PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Dressing), scene);
        EditorUtility.SetDirty(assumptions);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Kitchen dressing configured. Bake lighting and reflections after changing fixed props.");
    }

    static void ConfigureImporters()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Texture", new[] { Kitchen, Food }))
        {
            var importer = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as TextureImporter;
            if (importer == null || importer.maxTextureSize <= 1024) continue;
            importer.maxTextureSize = 1024;
            importer.SaveAndReimport();
        }
        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { Kitchen + "models", Food + "Meshes" }))
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));
            if (importer.generateSecondaryUV) continue;
            importer.generateSecondaryUV = true;
            importer.SaveAndReimport();
        }
    }

    static void BuildProp(string name, string source, string selection, float size, bool measureHeight,
        bool grab, ModelAssumptions assumptions)
    {
        string key = "prop_" + name.ToLowerInvariant() + (measureHeight ? "_height" : "_span");
        if (!assumptions.TryGetLength(key, out float registered))
        {
            registered = size;
            assumptions.Upsert(new AssumptionParameter { key = key, valueMetres = size, owner = "Hzc",
                status = "Prototype assumption", source = "Art dressing scale selected for this prototype; not a client dimension.",
                verification = "Normalized renderer bounds in the kitchen editor; Quest Link handling pending." });
        }
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(source);
        if (model == null) throw new InvalidOperationException("Missing prop source " + source);
        Transform selected = selection == null ? model.transform : model.GetComponentsInChildren<Transform>(true)
            .First(t => t.name.StartsWith(selection, StringComparison.Ordinal));
        var root = new GameObject("PREF_Prop_" + name);
        try
        {
            var visual = UnityEngine.Object.Instantiate(selected.gameObject, root.transform);
            visual.name = "VIS_" + name;
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            if (name == "Tomato")
                foreach (var t in visual.GetComponentsInChildren<Transform>().Where(t => t.name.Contains("Half")).ToArray())
                    UnityEngine.Object.DestroyImmediate(t.gameObject);
            foreach (var component in visual.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(component);
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
            {
                renderer.sharedMaterials = renderer.sharedMaterials.Select(AdaptMaterial).ToArray();
                // Small rims and rack slats contribute occlusion but receive indirect
                // light from probes, avoiding sub-texel lightmap charts and UV overlap.
                renderer.receiveGI = ReceiveGI.LightProbes;
                renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
            }
            var bounds = Bounds(root);
            float original = measureHeight ? bounds.size.y : Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            visual.transform.localScale *= registered / original;
            bounds = Bounds(root);
            // Bottom-centred project pivot removes the source pack's display offsets and unit mismatch.
            visual.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            bounds = Bounds(root);
            if (grab)
            {
                // A single convex hull is sufficient for food and empty decorative mugs.
                // The existing water cup and pot retain their open-vessel colliders and scripts.
                foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
                {
                    var collider = filter.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = filter.sharedMesh;
                    collider.convex = true;
                }
                var body = root.AddComponent<Rigidbody>();
                body.mass = .15f; // Generic handling mass, not a simulated food weight.
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                var interactable = root.AddComponent<XRGrabInteractable>();
                interactable.movementType = XRBaseInteractable.MovementType.Kinematic;
                interactable.useDynamicAttach = true;
                interactable.matchAttachPosition = true;
                interactable.matchAttachRotation = true;
                interactable.throwOnDetach = false;
                interactable.colliders.Clear();
                interactable.colliders.AddRange(root.GetComponentsInChildren<Collider>());
            }
            else
            {
                foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
                {
                    var collider = filter.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = filter.sharedMesh;
                }
                foreach (var transform in root.GetComponentsInChildren<Transform>())
                    GameObjectUtility.SetStaticEditorFlags(transform.gameObject,
                        StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic);
            }
            PrefabUtility.SaveAsPrefabAsset(root, Prefabs + "PREF_Prop_" + name + ".prefab");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    static Material AdaptMaterial(Material source)
    {
        if (source == null) throw new InvalidOperationException("Missing vendor material.");
        string path = Art + "MAT_Prop_" + source.name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        material = new Material(source) { name = "MAT_Prop_" + source.name, shader = Shader.Find("Standard") };
        bool ceramic = source.name == "plates" || source.name == "mug";
        bool metal = source.name == "drainer" || source.name == "toaster";
        if (ceramic || !metal)
        {
            material.SetTexture("_MetallicGlossMap", null);
            material.DisableKeyword("_METALLICGLOSSMAP");
            material.SetFloat("_Metallic", 0);
        }
        material.SetFloat("_Glossiness", ceramic ? .46f : metal ? .52f : source.name.StartsWith("Tomato") ? .32f : .18f);
        if (ceramic) material.color = new Color(.88f, .90f, .88f);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    static GameObject Group(GameObject root, string name)
    {
        var group = new GameObject(name);
        group.transform.SetParent(root.transform, false);
        return group;
    }

    static GameObject Place(GameObject group, string name, float x, float z, Vector3 rotation, float lift = 0)
    {
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(
            AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "PREF_Prop_" + name + ".prefab"), group.transform);
        instance.transform.rotation = Quaternion.Euler(rotation);
        var bounds = Bounds(instance);
        var environment = GameObject.Find("PREF_Kitchen_Environment");
        var support = environment.GetComponentsInChildren<MeshCollider>()
            .Where(c => c.name == "SM_KitchenTable" || c.name == "SM_DiningTable")
            .Select(c => { bool hit = c.Raycast(new Ray(new Vector3(x, 1.65f, z), Vector3.down), out var result, 2); return (hit, result); })
            .Where(h => h.hit && h.result.normal.y > .9f).OrderByDescending(h => h.result.point.y).ToArray();
        if (support.Length == 0) throw new InvalidOperationException("No authored tabletop below " + name);
        instance.transform.position += new Vector3(x - bounds.center.x, support[0].result.point.y + .003f + lift - bounds.min.y, z - bounds.center.z);
        PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
        return instance;
    }

    static Bounds Bounds(GameObject root)
    {
        bool found = false;
        var bounds = new Bounds();
        // Rotating an AABB can leave a curved banana hovering far above its support.
        // Exact vertex bounds are inexpensive here and run only during authoring.
        foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
            foreach (var vertex in filter.sharedMesh.vertices)
            {
                var point = filter.transform.TransformPoint(vertex);
                if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; }
                else bounds.Encapsulate(point);
            }
        if (!found) throw new InvalidOperationException("No prop mesh: " + root.name);
        return bounds;
    }
}
