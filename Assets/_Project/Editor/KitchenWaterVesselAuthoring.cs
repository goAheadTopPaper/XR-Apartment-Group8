using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using XRApartment.Data;
using XRApartment.EditorTools;
using XRApartment.Runtime;
using XRApartment.Simulation;

public static partial class KitchenWaterAuthoring
{
    const string Cooking = "Cooking/PREF_Cooking_";
    const string Recipe = "Cooking/Recipe/PREF_Cooking_";
    const string Dressing = "Props/PREF_Prop_";

    [MenuItem("XR Apartment/Water/Fit Water Vessels to Models")]
    public static void ConfigurePlayableVessels()
    {
        KitchenAuthoring.RequireEditMode();
        ConfigureCookingVessels();
        ConfigureDressingVessels();
        AssetDatabase.SaveAssets();
    }

    // Sections are (local height, inside X radius) in millimetres, measured from the rendered model.
    internal static void ConfigureCookingVessels()
    {
        var profile = KitchenAuthoring.LoadRequired<WaterInteractionProfile>(ProfilePath);
        Fit(profile, Cooking + "WaterCup", "water_cooking_cup", -6.55f, 0f, 35.6f,
            new[] { (-44f, 29.4f), (54f, 29.4f) }, initialFill: .6f);
        Fit(profile, Cooking + "WaterPot", "water_cooking_stockpot", 2f, 0f, 76f,
            new[] { (-39f, 23f), (-35.5f, 76f), (58f, 76f) }, initialFill: .15f);
        Fit(profile, Cooking + "WaterStation", "water_cooking_sink", 0f, 0f, 178.8f,
            new[] { (10f, 142f), (18f, 156f), (41f, 166f), (83f, 178.8f) }, child: "BASIN_Collect", drain: true);
        foreach (var folder in new[] { Cooking, Recipe })
        {
            Fit(profile, folder + "MixingBowl", "water_mixing_bowl", 0f, 0f, 72.8f,
                new[] { (4.5f, 26f), (11.5f, 45f), (25.8f, 63f), (39.8f, 70.2f), (59.5f, 72.8f) });
            Fit(profile, folder + "FryingPan", "water_frying_pan", 2f, 0f, 92f,
                new[] { (10.5f, 31f), (15.7f, 80f), (17.8f, 88f), (25.8f, 92f) });
        }
        Fit(profile, Cooking + "TomatoBowl", "water_prep_bowl", 0f, 0f, 86.5f,
            new[] { (5.8f, 33f), (14f, 53f), (32f, 75.5f), (49.8f, 83.5f), (74f, 86.5f) });
        Fit(profile, Cooking + "Ladle", "water_ladle", 1f, 18.5f, 22f,
            new[] { (4.2f, 6f), (8f, 16f), (12f, 24f), (15.8f, 29f) });
        Fit(profile, "Generated/plate_v001/PREF_Prop_plate", "water_serving_plate", 0f, 0f, 118.4f,
            new[] { (14f, 92f), (17.5f, 103f), (22f, 110.5f), (28.5f, 118.4f) }, child: "Intact", spillWhenDisabled: true);
    }

    internal static void ConfigureDressingVessels()
    {
        var profile = KitchenAuthoring.LoadRequired<WaterInteractionProfile>(ProfilePath);
        Fit(profile, Dressing + "Mug", "water_dressing_mug", -20.4f, 0f, 41.8f,
            new[] { (5.5f, 41.8f), (98f, 41.8f) });
        Fit(profile, Dressing + "Bowl", "water_dressing_bowl", 0f, 0f, 87.6f,
            new[] { (6.2f, 31f), (16.8f, 53f), (38.5f, 76f), (60f, 84.5f), (90f, 87.6f) });
        Fit(profile, Dressing + "SmallBowl", "water_dressing_small_bowl", 0f, 0f, 45.4f,
            new[] { (3.5f, 16f), (9.3f, 28f), (20.5f, 39.8f), (31.7f, 44.2f), (47f, 45.4f) });
        Fit(profile, Dressing + "Plate", "water_dressing_plate", 0f, 0f, 97f,
            new[] { (16.5f, 10f), (20.7f, 64f), (23f, 80f), (28.5f, 90f), (32.8f, 97f) });
    }

    static void Fit(WaterInteractionProfile profile, string prefab, string prefix, float centerX, float centerZ,
        float radiusZ, (float Y, float Radius)[] rings, string child = null, float initialFill = 0f,
        bool drain = false, bool spillWhenDisabled = false)
    {
        const float mm = .001f;
        var bottom = rings[0].Y;
        var rim = rings[rings.Length - 1];
        var height = rim.Y - bottom;
        var origin = new Vector3(centerX, bottom, centerZ) * mm;
        var sections = new VesselLiquidGeometry.Section[rings.Length];
        sections[0] = new VesselLiquidGeometry.Section(0f, rings[0].Radius / rim.Radius);
        for (var i = 1; i < rings.Length - 1; i++)
            sections[i] = new VesselLiquidGeometry.Section((rings[i].Y - bottom) / height, rings[i].Radius / rim.Radius);
        sections[sections.Length - 1] = new VesselLiquidGeometry.Section(1f, 1f);
        // Reject a malformed fit before saving the prefab or registering its dimensions.
        _ = new VesselLiquidGeometry(rim.Radius * mm, radiusZ * mm, height * mm, sections);
        var assumptions = KitchenAuthoring.Assumptions;
        Parameter(assumptions, prefix + "_inner_radius", rim.Radius * mm);
        Parameter(assumptions, prefix + "_depth_radius", radiusZ * mm);
        Parameter(assumptions, prefix + "_height", height * mm);
        Parameter(assumptions, prefix + "_bottom_y", origin.y);
        Parameter(assumptions, prefix + "_center_x", origin.x);
        Parameter(assumptions, prefix + "_center_z", origin.z);
        EditorUtility.SetDirty(assumptions);

        KitchenAuthoring.EditPrefab(KitchenAuthoring.Prefabs + prefab + ".prefab", root =>
        {
            var vessel = child == null ? root.transform : root.transform.Find(child);
            if (vessel == null) throw new System.InvalidOperationException($"Missing vessel {child} in {prefab}.");
            var plane = vessel.Find("LIQ_Surface") ?? Child(vessel, "LIQ_Surface");
            plane.SetLocalPositionAndRotation(origin, Quaternion.identity);
            plane.localScale = Vector3.one;
            GetOrAdd<MeshFilter>(plane.gameObject).sharedMesh = null;
            var renderer = GetOrAdd<MeshRenderer>(plane.gameObject);
            renderer.sharedMaterial = profile.SurfaceMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.enabled = false;
            var surface = GetOrAdd<LiquidSurface>(plane.gameObject);
            var stream = CreateStream(vessel, profile);
            var data = new SerializedObject(GetOrAdd<LiquidContainer>(vessel.gameObject));
            data.FindProperty("profile").objectReferenceValue = profile;
            data.FindProperty("parameterPrefix").stringValue = prefix;
            data.FindProperty("initialFill").floatValue = initialFill;
            data.FindProperty("drain").boolValue = drain;
            data.FindProperty("surface").objectReferenceValue = surface;
            data.FindProperty("stream").objectReferenceValue = stream;
            data.FindProperty("fittedCavity").boolValue = true;
            data.FindProperty("spillWhenDisabled").boolValue = spillWhenDisabled;
            var profileData = data.FindProperty("sections");
            profileData.arraySize = sections.Length;
            for (var i = 0; i < sections.Length; i++)
            {
                var section = profileData.GetArrayElementAtIndex(i);
                section.FindPropertyRelative("heightFraction").floatValue = sections[i].heightFraction;
                section.FindPropertyRelative("radiusFraction").floatValue = sections[i].radiusFraction;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            var eggPlane = vessel.Find("LIQ_Egg");
            if (eggPlane != null) eggPlane.localPosition = origin;
        });
    }

    static T GetOrAdd<T>(GameObject target) where T : Component =>
        target.TryGetComponent<T>(out var component) ? component : target.AddComponent<T>();
}
