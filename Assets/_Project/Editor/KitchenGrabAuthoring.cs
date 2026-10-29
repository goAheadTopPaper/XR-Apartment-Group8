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
        var physical = root.GetComponentsInChildren<Collider>(true)
            .Where(c => c.enabled && !c.isTrigger && c.name != "COL_GrabSweepProbe" &&
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
        data.ApplyModifiedPropertiesWithoutUndo();
        transformer.allowOneHandedScaling = false;
        transformer.allowTwoHandedScaling = false;
        grab.startingSingleGrabTransformers.Clear();
        grab.startingMultipleGrabTransformers.Clear();
        grab.startingSingleGrabTransformers.Add(transformer);
        grab.startingMultipleGrabTransformers.Add(transformer);
        grab.addDefaultGrabTransformers = false;
        grab.useDynamicAttach = true;
        grab.matchAttachPosition = true;
        grab.matchAttachRotation = true;
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
