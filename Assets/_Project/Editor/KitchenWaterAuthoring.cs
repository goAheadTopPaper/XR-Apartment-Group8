using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using XRApartment.Data;
using XRApartment.EditorTools;
using XRApartment.Runtime;

/// Edits only the water station and existing prop prefabs; never rebuilds the authored room.
public static class KitchenWaterAuthoring
{
    const string Root = KitchenAuthoring.Root;
    const string WaterArt = Root + "Art/Water/";
    const string ProfilePath = Root + "Settings/SET_WaterInteraction.asset";
    const string StationPath = Root + "Prefabs/PREF_Water_KitchenStation.prefab";

    [MenuItem("XR Apartment/Water/Reset Water (Play)")]
    public static void ResetWater()
    {
        if (!EditorApplication.isPlaying) return;
        foreach (var tap in UnityEngine.Object.FindObjectsByType<WaterTap>(FindObjectsSortMode.None)) tap.SetRunning(false);
        if (KitchenWaterSystem.Instance != null) KitchenWaterSystem.Instance.ResetWater();
    }

    [MenuItem("XR Apartment/Water/Configure Kitchen Water")]
    public static void Configure()
    {
        var scene = KitchenAuthoring.RequireKitchenScene();
        KitchenAuthoring.EnsureFolder(WaterArt.TrimEnd('/'));
        var assumptions = KitchenAuthoring.Assumptions;
        Register(assumptions, "water_cup", 0.034f, -0.044f, 0.094f, 0.006f);
        Register(assumptions, "water_pot", 0.082f, -0.052f, 0.112f, 0.008f);
        Register(assumptions, "water_basin", 0.18f, 0.008f, 0.075f, 0.007f);
        Parameter(assumptions, "water_station_x", -4.35f);
        Parameter(assumptions, "water_station_y", 1f);
        Parameter(assumptions, "water_station_z", 2.05f);
        Parameter(assumptions, "water_tap_height", 0.28f);
        Parameter(assumptions, "water_tap_radius", 0.012f);
        EditorUtility.SetDirty(assumptions);

        var profile = AssetDatabase.LoadAssetAtPath<WaterInteractionProfile>(ProfilePath);
        if (profile == null) { profile = ScriptableObject.CreateInstance<WaterInteractionProfile>(); AssetDatabase.CreateAsset(profile, ProfilePath); }
        var surface = Material("MAT_Water_Kitchen", "#NVJOB/Water Shaders V2/Water Specular");
        surface.SetColor("_AlbedoColor", new Color(0.025f, 0.075f, 0.085f, 0.68f));
        surface.SetFloat("_AlbedoContrast", 1f);
        surface.SetFloat("_Shininess", 0.85f);
        surface.SetColor("_SpecColor", new Color(0.35f, 0.35f, 0.35f));
        surface.SetFloat("_SoftFactor", 120f); // Kitchen water depths are centimetres, not an ocean.
        surface.SetFloat("_NormalMap1Strength", 0.13f);
        surface.SetTexture("_NormalMap1", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ThirdParty/NVJOBWaterV2/Textures/Water N 0.png"));
        surface.SetTextureScale("_NormalMap1", Vector2.one * 0.4f);
        surface.DisableKeyword("EFFECT_MIRROR");
        surface.DisableKeyword("EFFECT_PARALLAX");
        surface.DisableKeyword("EFFECT_FOAM");
        var probe = UnityEngine.Object.FindFirstObjectByType<ReflectionProbe>();
        if (probe != null && probe.bakedTexture != null)
        {
            surface.EnableKeyword("EFFECT_REFLECTION");
            surface.SetTexture("_ReflectionCube", probe.bakedTexture);
            surface.SetFloat("_ReflectionStrength", 0.12f);
            surface.SetColor("_ReflectionColor", Color.white);
        }
        var stream = Material("MAT_Water_Stream", "Standard");
        stream.color = new Color(0.12f, 0.27f, 0.30f, 0.75f);
        stream.SetFloat("_Glossiness", 0.95f);
        stream.SetFloat("_Metallic", 0.05f);
        stream.SetFloat("_Mode", 2);
        stream.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        stream.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        stream.SetInt("_ZWrite", 0);
        stream.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        stream.EnableKeyword("_ALPHABLEND_ON");
        stream.renderQueue = 3000;
        var splash = Material("MAT_Water_Splash", "XR Apartment/Water Droplet");
        splash.color = new Color(0.65f, 0.84f, 0.9f, 0.65f);
        var profileData = new SerializedObject(profile);
        profileData.FindProperty("assumptions").objectReferenceValue = assumptions;
        profileData.FindProperty("surfaceMaterial").objectReferenceValue = surface;
        profileData.FindProperty("streamMaterial").objectReferenceValue = stream;
        profileData.FindProperty("splashMaterial").objectReferenceValue = splash;
        profileData.ApplyModifiedPropertiesWithoutUndo();
        ConfigureProp("Cup", "water_cup", profile, 0.6f);
        ConfigureProp("Pot", "water_pot", profile, 0.15f);
        CreateStation(profile);
        foreach (var camera in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            camera.depthTextureMode |= DepthTextureMode.Depth;
        EditorUtility.SetDirty(surface);
        EditorUtility.SetDirty(stream);
        EditorUtility.SetDirty(splash);
        KitchenAuthoring.SaveScene(scene);
        Debug.Log("Kitchen water configured: open vessels, NVJOB surfaces, conserved volume parcels, tap and drain.");
    }

    static void Register(ModelAssumptions assumptions, string prefix, float radius, float bottom, float height, float thickness)
    {
        Parameter(assumptions, prefix + "_inner_radius", radius);
        Parameter(assumptions, prefix + "_bottom_y", bottom);
        Parameter(assumptions, prefix + "_height", height);
        Parameter(assumptions, prefix + "_wall_thickness", thickness);
    }

    static void Parameter(ModelAssumptions assumptions, string key, float value) => assumptions.Upsert(new AssumptionParameter
    {
        key = key, valueMetres = value,
        source = "Water interaction prototype geometry in the existing authored kitchen; not a client dimension.",
        owner = "Hzc", status = "Prototype assumption",
        verification = "Editor-authored vessel dimensions and station coordinates; Quest Link interaction pending."
    });

    static Material Material(string name, string shader)
    {
        string path = WaterArt + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        var found = Shader.Find(shader);
        if (found == null) throw new InvalidOperationException("Missing shader " + shader);
        if (material == null)
        {
            material = new Material(found) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        else if (material.shader != found) material.shader = found;
        return material;
    }

    static void ConfigureProp(string type, string prefix, WaterInteractionProfile profile, float initialFill)
    {
        string path = Root + "Prefabs/PREF_Prop_" + type + ".prefab";
        KitchenAuthoring.EditPrefab(path, root =>
        {
            var body = root.transform.Find("BODY_" + type);
            body.localScale = Vector3.one;
            foreach (var collider in body.GetComponents<Collider>()) UnityEngine.Object.DestroyImmediate(collider);
            ConfigureVessel(root, body.gameObject, prefix, profile, initialFill, false);
            KitchenGrabAuthoring.ConfigureProp(root);
        });
    }

    static void ConfigureVessel(GameObject root, GameObject body, string prefix, WaterInteractionProfile profile, float fill, bool drain)
    {
        float radius = profile.Length(prefix + "_inner_radius");
        float bottom = profile.Length(prefix + "_bottom_y");
        float height = profile.Length(prefix + "_height");
        float wall = profile.Length(prefix + "_wall_thickness");
        body.GetComponent<MeshFilter>().sharedMesh = SaveMesh("MESH_" + prefix + "_Open", WaterVesselMeshBuilder.OpenVessel(radius, bottom, height, wall));
        var old = root.transform.Find("COL_WaterVessel");
        if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
        var colliders = Child(root.transform, "COL_WaterVessel");
        var baseCollider = colliders.gameObject.AddComponent<BoxCollider>();
        baseCollider.center = Vector3.up * (bottom - wall * 0.5f);
        baseCollider.size = new Vector3(radius * 2, wall, radius * 2);
        const int sides = 16;
        for (int i = 0; i < sides; i++)
        {
            float angle = 2f * Mathf.PI * i / sides;
            var segment = Child(colliders, "Wall_" + i);
            segment.localPosition = new Vector3(Mathf.Cos(angle) * (radius + wall * 0.5f), bottom + height * 0.5f, Mathf.Sin(angle) * (radius + wall * 0.5f));
            segment.localRotation = Quaternion.Euler(0, -angle * Mathf.Rad2Deg, 0);
            segment.gameObject.AddComponent<BoxCollider>().size = new Vector3(wall, height, 2f * (radius + wall) * Mathf.Tan(Mathf.PI / sides));
        }
        var surfaceTransform = root.transform.Find("LIQ_Surface");
        if (surfaceTransform == null) surfaceTransform = Child(root.transform, "LIQ_Surface");
        surfaceTransform.localPosition = Vector3.up * bottom;
        var filter = surfaceTransform.GetComponent<MeshFilter>();
        if (filter == null) filter = surfaceTransform.gameObject.AddComponent<MeshFilter>();
        var renderer = surfaceTransform.GetComponent<MeshRenderer>();
        if (renderer == null) renderer = surfaceTransform.gameObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = profile.SurfaceMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        var surface = surfaceTransform.GetComponent<LiquidSurface>();
        if (surface == null) surface = surfaceTransform.gameObject.AddComponent<LiquidSurface>();
        filter.sharedMesh = SaveMesh("MESH_" + prefix + "_WaterPreview", WaterVesselMeshBuilder.PreviewDisk(radius, height * fill));
        var stream = CreateStream(root.transform, profile);
        var container = root.GetComponent<LiquidContainer>();
        if (container == null) container = root.AddComponent<LiquidContainer>();
        var data = new SerializedObject(container);
        data.FindProperty("profile").objectReferenceValue = profile;
        data.FindProperty("parameterPrefix").stringValue = prefix;
        data.FindProperty("initialFill").floatValue = fill;
        data.FindProperty("drain").boolValue = drain;
        data.FindProperty("surface").objectReferenceValue = surface;
        data.FindProperty("stream").objectReferenceValue = stream;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    static void CreateStation(WaterInteractionProfile profile)
    {
        foreach (var existing in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            if (existing.name == "PREF_Water_KitchenStation") UnityEngine.Object.DestroyImmediate(existing);
        var root = new GameObject("PREF_Water_KitchenStation");
        try
        {
            var chrome = Material("MAT_WaterStation_Chrome", "Standard");
            chrome.color = new Color(0.55f, 0.57f, 0.58f);
            chrome.SetFloat("_Metallic", 0.9f); chrome.SetFloat("_Glossiness", 0.8f);
            EditorUtility.SetDirty(chrome);
            var basin = Child(root.transform, "BASIN_Collect");
            var body = Child(basin, "BODY_Basin");
            body.gameObject.AddComponent<MeshFilter>();
            body.gameObject.AddComponent<MeshRenderer>().sharedMaterial = chrome;
            ConfigureVessel(basin.gameObject, body.gameObject, "water_basin", profile, 0f, true);
            var faucet = Child(root.transform, "TAP_Water");
            float tapHeight = profile.Length("water_tap_height");
            float tapRadius = profile.Length("water_tap_radius");
            Pipe(faucet, "PIPE_Vertical", new Vector3(0, tapHeight * 0.5f, -0.16f), new Vector3(0, tapHeight, 0), tapRadius, chrome);
            Pipe(faucet, "PIPE_Spout", new Vector3(0, tapHeight, -0.07f), new Vector3(0, 0, 0.18f), tapRadius, chrome);
            Pipe(faucet, "PIPE_Outlet", new Vector3(0, tapHeight - 0.015f, 0.02f), new Vector3(0, 0.03f, 0), tapRadius, chrome);
            var outlet = Child(faucet, "POINT_Outlet");
            outlet.localPosition = new Vector3(0, tapHeight - 0.03f, 0.02f);
            var lever = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lever.name = "CONTROL_TapLever";
            lever.transform.SetParent(faucet, false);
            lever.transform.localPosition = new Vector3(0.06f, 0.16f, -0.16f);
            lever.transform.localScale = new Vector3(0.09f, 0.025f, 0.035f);
            var blue = Material("MAT_WaterStation_Lever", "Standard");
            blue.color = new Color(0.10f, 0.30f, 0.40f); EditorUtility.SetDirty(blue);
            lever.GetComponent<Renderer>().sharedMaterial = blue;
            var interactable = lever.AddComponent<XRSimpleInteractable>();
            var tap = faucet.gameObject.AddComponent<WaterTap>();
            var tapData = new SerializedObject(tap);
            tapData.FindProperty("profile").objectReferenceValue = profile;
            tapData.FindProperty("outlet").objectReferenceValue = outlet;
            tapData.FindProperty("lever").objectReferenceValue = lever.transform;
            tapData.FindProperty("control").objectReferenceValue = interactable;
            tapData.FindProperty("stream").objectReferenceValue = CreateStream(faucet, profile);
            tapData.ApplyModifiedPropertiesWithoutUndo();
            var fx = Child(root.transform, "FX_WaterSplashes").gameObject.AddComponent<ParticleSystem>();
            fx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = fx.main;
            main.playOnAwake = false; main.loop = false; main.maxParticles = 256;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.002f, 0.006f);
            main.gravityModifier = 1f; main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = fx.emission; emission.enabled = false;
            var shape = fx.shape; shape.enabled = false;
            fx.GetComponent<ParticleSystemRenderer>().sharedMaterial = profile.SplashMaterial;
            var system = root.AddComponent<KitchenWaterSystem>();
            var systemData = new SerializedObject(system);
            systemData.FindProperty("splashes").objectReferenceValue = fx;
            systemData.ApplyModifiedPropertiesWithoutUndo();
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, StationPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.position = new Vector3(profile.Length("water_station_x"), profile.Length("water_station_y"), profile.Length("water_station_z"));
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    static LiquidStream CreateStream(Transform parent, WaterInteractionProfile profile)
    {
        var child = parent.Find("FX_WaterStream");
        if (child == null) child = Child(parent, "FX_WaterStream");
        var line = child.GetComponent<LineRenderer>();
        if (line == null) line = child.gameObject.AddComponent<LineRenderer>();
        line.sharedMaterial = profile.StreamMaterial;
        line.useWorldSpace = true; line.positionCount = 0; line.enabled = false;
        line.numCapVertices = 4; line.numCornerVertices = 3;
        line.generateLightingData = true; line.shadowCastingMode = ShadowCastingMode.Off;
        var stream = child.GetComponent<LiquidStream>();
        return stream != null ? stream : child.gameObject.AddComponent<LiquidStream>();
    }

    static void Pipe(Transform parent, string name, Vector3 position, Vector3 length, float radius, Material material)
    {
        var pipe = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pipe.name = name; pipe.transform.SetParent(parent, false);
        pipe.transform.localPosition = position;
        pipe.transform.localRotation = Quaternion.FromToRotation(Vector3.up, length.normalized);
        pipe.transform.localScale = new Vector3(radius * 2, length.magnitude * 0.5f, radius * 2);
        pipe.GetComponent<Renderer>().sharedMaterial = material;
    }

    static Transform Child(Transform parent, string name)
    {
        var child = new GameObject(name).transform;
        child.SetParent(parent, false); return child;
    }

    static Mesh SaveMesh(string name, Mesh mesh)
    {
        string path = WaterArt + name + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        mesh.name = name;
        if (existing == null) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
        EditorUtility.CopySerialized(mesh, existing); EditorUtility.SetDirty(existing);
        UnityEngine.Object.DestroyImmediate(mesh); return existing;
    }

}
