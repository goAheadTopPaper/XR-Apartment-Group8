using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using XRApartment.Data;
using XRApartment.Runtime;
using XRApartment.Runtime.ReviewV2;

namespace XRApartment.EditorTools
{
    public static class ReviewV2PanSnapAuthoring
    {
        public const string Version = "integrated-kitchen-m2-v5";
        [MenuItem("XR Apartment/Review V2/Apply A Pan Stove Snap")]
        public static void Apply()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Stop Play Mode before authoring");
            var path = ReviewV2SchemeAuthoring.LayoutPath("SCHEME_A");
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var world = root.GetComponent<ReviewSchemeWorld>(); ConfigureWorld(world);
                world.modelVersion = Version;
                if (PrefabUtility.SaveAsPrefabAsset(root, path) == null) throw new IOException(path);
                File.WriteAllLines("Docs/M2/SCHEME_A_Identities.tsv", world.identities.Select(i =>
                    "SCHEME_A\t" + Version + "\t" + i.Id + "\t" + i.name));
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("REVIEW_V2_A_PAN_SNAP_V5: release-to-dock, grab-to-unlock; A-only, geometry unchanged");
        }
        public static void ConfigureWorld(ReviewSchemeWorld world)
        {
            if (world.schemeId != "SCHEME_A") throw new InvalidOperationException("A-only pan snap");
            var pan = world.station.transform.Find("PREF_Cooking_FryingPan");
            var support = world.station.transform.Find("LOC_BurnerSurface");
            if (pan == null || support == null) throw new InvalidOperationException("Missing pan or burner support");
            var guard = new SerializedObject(pan.GetComponent<CollisionConstrainedGrabTransformer>());
            var probe = guard.FindProperty("sweepProbe").objectReferenceValue as BoxCollider;
            var profile = AssetDatabase.LoadAssetAtPath<HandInteractionProfile>("Assets/_Project/Settings/SET_HandInteraction.asset");
            if (probe == null || probe.enabled || profile == null) throw new InvalidOperationException("Missing disabled pan sweep/profile");
            var snap = pan.GetComponent<ReviewPanStoveSnap>() ?? pan.gameObject.AddComponent<ReviewPanStoveSnap>();
            snap.Configure(world.station, support, support.InverseTransformPoint(pan.position), probe, profile);
            if (!snap.IsConfigured) throw new InvalidOperationException("Invalid pan snap configuration");
        }
    }
}
