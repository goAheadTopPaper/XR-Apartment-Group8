using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Transformers;
using XRApartment.Data;
using XRApartment.EditorTools;
using XRApartment.Runtime;

/// Adds collision-constrained grabs to project props without rebuilding room art or lighting.
public static class KitchenGrabAuthoring
{
    const string Root = KitchenAuthoring.Root;

    [MenuItem("XR Apartment/Hands/Configure Held Prop Collision")]
    public static void Configure()
    {
        KitchenAuthoring.RequireEditMode();
        var paths = AssetDatabase.FindAssets("t:Prefab", new[] { Root + "Prefabs" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => path.EndsWith("/PREF_Prop_Pot.prefab") || path.EndsWith("/PREF_Prop_Cup.prefab") ||
                path.StartsWith(Root + "Prefabs/Props/", StringComparison.Ordinal) ||
                path.StartsWith(Root + "Prefabs/Generated/", StringComparison.Ordinal));
        foreach (var path in paths)
        {
            if (KitchenAuthoring.LoadRequired<GameObject>(path).GetComponent<XRGrabInteractable>() == null) continue;
            KitchenAuthoring.EditPrefab(path, ConfigureProp);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Collision-constrained grabbing configured for project props.");
    }

    public static void ConfigureCookingToolGrips()
    {
        KitchenAuthoring.RequireEditMode();
        foreach (var path in new[]
        {
            "Generated/knife_v001/PREF_Prop_knife.prefab",
            "Cooking/PREF_Cooking_FryingPan.prefab",
            "Cooking/Recipe/PREF_Cooking_FryingPan.prefab",
            "Cooking/PREF_Cooking_WaterPot.prefab",
            "Cooking/Recipe/PREF_Cooking_Spatula.prefab",
            "Cooking/PREF_Cooking_Whisk.prefab"
        })
            KitchenAuthoring.EditPrefab(KitchenAuthoring.Prefabs + path, ConfigureProp);
        AssetDatabase.SaveAssets();
    }

    public static void ConfigureProp(GameObject root)
    {
        var grab = root.GetComponent<XRGrabInteractable>();
        if (grab == null) return;
        var profile = KitchenAuthoring.LoadRequired<HandInteractionProfile>(KitchenAuthoring.Settings + "SET_HandInteraction.asset");
        EditorUtility.SetDirty(profile);
        if (!profile.Assumptions.TryGetLength("grab_contact_skin", out _))
        {
            profile.Assumptions.Upsert(new AssumptionParameter
            {
                key = "grab_contact_skin", valueMetres = .002f, owner = "Hzc", status = "Prototype assumption",
                source = "Conservative held-prop sweep margin; not a kitchen clearance or client dimension.",
                verification = "Automated XRI push/rotation/release checks; Quest Link feel pending."
            });
            EditorUtility.SetDirty(profile.Assumptions);
        }
        var body = root.GetComponent<Rigidbody>();
        var remainder = root.transform.Find(KitchenCookingAuthoring.TomatoRemainder);
        var physical = root.GetComponentsInChildren<Collider>(true)
            .Where(c => c.enabled && !c.isTrigger && c.name != "COL_GrabSweepProbe" &&
                (remainder == null || !c.transform.IsChildOf(remainder)) &&
                c.GetComponentInParent<Rigidbody>(true) == body).ToArray();
        if (physical.Length == 0) throw new InvalidOperationException("No physical colliders on " + root.name);
        var bounds = LocalBounds(root.transform, physical);
        var query = root.transform.Find("COL_GrabSweepProbe");
        if (query == null)
        {
            query = new GameObject("COL_GrabSweepProbe").transform;
            query.SetParent(root.transform, false);
        }
        query.gameObject.layer = root.layer;
        var probe = query.GetComponent<BoxCollider>();
        if (probe == null) probe = query.gameObject.AddComponent<BoxCollider>();
        probe.enabled = false;
        probe.center = bounds.center;
        probe.size = bounds.size;

        foreach (var old in root.GetComponents<XRGeneralGrabTransformer>())
            if (!(old is CollisionConstrainedGrabTransformer)) UnityEngine.Object.DestroyImmediate(old);
        var transformer = root.GetComponent<CollisionConstrainedGrabTransformer>();
        if (transformer == null) transformer = root.AddComponent<CollisionConstrainedGrabTransformer>();
        var data = new SerializedObject(transformer);
        data.FindProperty("profile").objectReferenceValue = profile;
        data.FindProperty("sweepProbe").objectReferenceValue = probe;
        data.FindProperty("alternateGrip").objectReferenceValue = ConfigureGrip(root.transform, grab, physical);
        data.ApplyModifiedPropertiesWithoutUndo();
        transformer.allowOneHandedScaling = false;
        transformer.allowTwoHandedScaling = false;
        grab.startingSingleGrabTransformers.Clear();
        grab.startingMultipleGrabTransformers.Clear();
        grab.startingSingleGrabTransformers.Add(transformer);
        grab.startingMultipleGrabTransformers.Add(transformer);
        grab.addDefaultGrabTransformers = false;
        grab.colliders.Clear();
        grab.colliders.AddRange(physical);
        // The final pose is swept before XRI applies it, including OnBeforeRender.
        // Smoothing after the sweep would interpolate a fresh, unchecked angular path.
        grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
        grab.attachEaseInTime = 0f;
        grab.smoothPosition = false;
        grab.smoothRotation = false;
        grab.trackScale = false;
        grab.limitLinearVelocity = true;
        grab.maxLinearVelocityDelta = profile.MaxReleaseSpeed;
        grab.limitAngularVelocity = true;
        grab.maxAngularVelocityDelta = profile.MaxReleaseAngularSpeed;
        // A deterministic prototype balance point from this body's authored
        // shapes. Disabled query probes and nested food bodies must not shift it.
        body.centerOfMass = bounds.center;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        EditorUtility.SetDirty(grab);
        EditorUtility.SetDirty(transformer);
    }

    static Transform ConfigureGrip(Transform root, XRGrabInteractable grab, Collider[] physical)
    {
        Transform grip = null, alternate = null;
        switch (root.name)
        {
            case "PREF_Prop_knife":
                // The blade projects along +X and its cutting edge is on the +Z side.
                grip = WriteGrip(root, "ATT_Handle", new Vector3(-.095f, .014f, -.01f),
                    Quaternion.LookRotation(Vector3.right, Vector3.back));
                break;
            case "PREF_Cooking_FryingPan":
                grip = WriteGrip(root, GripCentre(PickHandle("COL_Proxy_VIS_PanHandle", "VIS_PanHandle")),
                    Quaternion.LookRotation(Vector3.left, Vector3.up));
                break;
            case "PREF_Cooking_Spatula":
                grip = WriteGrip(root, GripCentre(PickHandle("COL_SpatulaHandle")),
                    Quaternion.LookRotation(Vector3.back, Vector3.up));
                break;
            case "PREF_Cooking_Whisk":
                grip = WriteGrip(root, GripCentre(PickHandle("COL_Proxy_VIS_Handle", "VIS_Handle")),
                    Quaternion.Euler(30f, 0f, 0f));
                break;
            case "PREF_Cooking_WaterPot":
                var handles = physical.OfType<BoxCollider>()
                    .Where(collider => collider.name == "COL_Proxy_HANDLE_Pot" || collider.name == "HANDLE_Pot")
                    .Select(GripCentre).OrderBy(position => position.x).ToArray();
                if (handles.Length != 2) throw new InvalidOperationException("The water pot needs two solid handle proxies.");
                grip = WriteGrip(root, handles[0], Quaternion.LookRotation(Vector3.right, Vector3.up));
                alternate = WriteGrip(root, "ATT_AlternateGrip", handles[1], Quaternion.LookRotation(Vector3.left, Vector3.up));
                break;
        }

        if (grip != null) grab.attachTransform = grip;
        // A two-handle pot uses a mutable XRI attachment for the chosen handle.
        // Single-handle tools use a fixed attachment, and generic props keep free pickup.
        grab.useDynamicAttach = grip == null || alternate != null;
        grab.matchAttachPosition = grip == null;
        grab.matchAttachRotation = grip == null;
        return alternate;

        BoxCollider PickHandle(string name, string sourceName = null) => physical.OfType<BoxCollider>()
            .Single(collider => collider.name == name || collider.name == sourceName);
        Vector3 GripCentre(BoxCollider handle) => root.InverseTransformPoint(handle.transform.TransformPoint(handle.center));
    }

    static Transform WriteGrip(Transform root, Vector3 position, Quaternion rotation) =>
        WriteGrip(root, "ATT_ToolGrip", position, rotation);

    static Transform WriteGrip(Transform root, string name, Vector3 position, Quaternion rotation)
    {
        var grip = root.Find(name);
        if (grip == null)
        {
            grip = new GameObject(name).transform;
            grip.SetParent(root, false);
        }
        grip.SetLocalPositionAndRotation(position, rotation);
        return grip;
    }

    static Bounds LocalBounds(Transform root, Collider[] colliders)
    {
        var result = new Bounds();
        var initialized = false;
        foreach (var collider in colliders)
        {
            Bounds bounds;
            if (collider is BoxCollider box) bounds = new Bounds(box.center, box.size);
            else if (collider is MeshCollider mesh && mesh.sharedMesh != null) bounds = mesh.sharedMesh.bounds;
            else throw new InvalidOperationException("Register a sweep bounds implementation for " + collider.GetType().Name);
            var matrix = root.worldToLocalMatrix * collider.transform.localToWorldMatrix;
            for (var corner = 0; corner < 8; corner++)
            {
                var point = bounds.center + Vector3.Scale(bounds.extents, new Vector3(
                    (corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                point = matrix.MultiplyPoint3x4(point);
                if (!initialized) { result = new Bounds(point, Vector3.zero); initialized = true; }
                else result.Encapsulate(point);
            }
        }
        return result;
    }
}
