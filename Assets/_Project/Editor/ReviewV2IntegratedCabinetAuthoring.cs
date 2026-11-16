using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using XRApartment.Data;
using XRApartment.Runtime;
using XRApartment.Runtime.ReviewV2;
using Object = UnityEngine.Object;

namespace XRApartment.EditorTools
{
    /// A-only authoring. Coordinates use the refrigerator's local axes: +Z faces the room.
    public static class ReviewV2IntegratedCabinetAuthoring
    {
        public const string Version = ReviewV2CounterLayoutAuthoring.Version;
        public const string PrefabPath = ReviewV2SchemeAuthoring.Folder + "/PREF_A_IntegratedCabinet.prefab";
        const string MaterialFolder = "Assets/_Project/Art/ReviewV2";
        const float Front = .414f;
        const float Gap = .003f;

        [MenuItem("XR Apartment/Review V2/Apply A Integrated Cabinet")]
        public static void Apply()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Stop Play Mode before authoring");
            BuildPrefab();
            var path = ReviewV2SchemeAuthoring.LayoutPath("SCHEME_A");
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var world = root.GetComponent<ReviewSchemeWorld>();
                Attach(world);
                world.modelVersion = Version;
                world.identities = root.GetComponentsInChildren<ReviewSceneIdentity>(true);
                if (world.identities.GroupBy(i => i.Id).Any(g => g.Count() != 1))
                    throw new InvalidOperationException("Duplicate scheme identity");
                if (PrefabUtility.SaveAsPrefabAsset(root, path) == null) throw new IOException(path);
                File.WriteAllLines("Docs/M2/SCHEME_A_Identities.tsv", world.identities.Select(i =>
                    "SCHEME_A\t" + Version + "\t" + i.Id + "\t" + i.name));
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("REVIEW_V2_A_CABINET: " + Version + "; six pull doors and current A cooking layout; B and scenes unchanged");
        }

        public static void Attach(ReviewSchemeWorld world)
        {
            foreach (var name in new[] { "Integrated_UpperCabinet", "A_IntegratedCabinet" })
            {
                var previous = world.transform.Find(name);
                if (previous != null) Object.DestroyImmediate(previous.gameObject);
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null) BuildPrefab();
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), world.transform);
            instance.name = "A_IntegratedCabinet";
            var fridge = world.station.ActiveFridge.transform;
            instance.transform.SetPositionAndRotation(fridge.position, fridge.rotation);
            ReviewV2CabinetCounterMesh.Apply(world, MaterialFolder);
            ReviewV2CounterLayoutAuthoring.ConfigureWorld(world);
        }

        static Material Material(string name, Color colour, float smoothness)
        {
            var path = MaterialFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/_Project/Art/Cooking/Furniture/MAT_Furniture_FridgeEnamel.mat"));
                material.name = name; AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", colour);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", 0);
            material.SetColor("_EmissiveColor", Color.black);
            EditorUtility.SetDirty(material); return material;
        }

        public static void BuildPrefab()
        {
            if (!AssetDatabase.IsValidFolder(MaterialFolder)) AssetDatabase.CreateFolder("Assets/_Project/Art", "ReviewV2");
            var enamel = Material("MAT_A_CabinetWarmWhite", new Color(.76f, .735f, .685f), .25f);
            var interior = Material("MAT_A_CabinetInterior", new Color(.59f, .565f, .51f), .18f);
            var glass = Material("MAT_A_MicrowaveGlass", new Color(.025f, .035f, .04f), .6f);
            var metal = Material("MAT_A_CabinetHardware", new Color(.32f, .33f, .32f), .52f);
            var root = new GameObject("A_IntegratedCabinet");
            try
            {
                root.AddComponent<ReviewSceneIdentity>().Configure("CABINET_LAYOUT");
                // Cases stop behind the hinge plane so the full leaf and handle can sweep.
                Case(root.transform, "LeftUpper", .405f, 1.335f, 1.495f, 2.615f, enamel, interior, .29f);
                Case(root.transform, "FridgeUpper", -.395f, .395f, 1.845f, 2.615f, enamel, interior);
                Case(root.transform, "NarrowLower", .405f, .695f, .10f, 1.005f, enamel, interior, .29f);
                Case(root.transform, "NarrowUpper", .405f, .695f, 1.005f, 1.495f, enamel, interior, .29f);
                // Exact clear niche: 0.60 x 0.44 x 0.50 m (back face -0.112 to front 0.388).
                Box(root.transform, "Niche_Back", new Vector3(1.015f, 1.257f, -.121f), new Vector3(.60f, .44f, .018f), interior);
                Box(root.transform, "Niche_Base", new Vector3(1.015f, 1.028f, .138f), new Vector3(.636f, .018f, .5f), enamel);
                Box(root.transform, "Niche_LeftSide", new Vector3(1.324f, 1.257f, .138f), new Vector3(.018f, .44f, .5f), enamel);
                Box(root.transform, "Niche_RightSide", new Vector3(.706f, 1.257f, .138f), new Vector3(.018f, .44f, .5f), enamel);
                Box(root.transform, "Niche_Top", new Vector3(1.015f, 1.486f, .138f), new Vector3(.636f, .018f, .5f), enamel);
                Box(root.transform, "Fridge_LeftReveal", new Vector3(.385f, .9725f, -.025f), new Vector3(.018f, 1.745f, .626f), enamel);
                Box(root.transform, "Fridge_RightReveal", new Vector3(-.385f, .9725f, -.025f), new Vector3(.018f, 1.745f, .626f), enamel);
                Box(root.transform, "Fridge_Toe", new Vector3(0, .025f, .19f), new Vector3(.79f, .05f, .35f), interior);
                Box(root.transform, "Narrow_Toe", new Vector3(.55f, .05f, .10f), new Vector3(.29f, .10f, .35f), interior);
                Box(root.transform, "Crown", new Vector3(.47f, 2.6375f, .138f), new Vector3(1.73f, .045f, .5f), enamel);
                Pair(root.transform, "CABINET_LEFT_UPPER", .455f, 1.28f, 1.52f, 2.60f, enamel, metal, .32f);
                Box(root.transform, "WallHingeFiller", new Vector3(1.3075f, 2.0575f, .285f), new Vector3(.055f, 1.085f, .018f), enamel);
                Box(root.transform, "UpperHingeSeparation", new Vector3(.43f, 2.0575f, .285f), new Vector3(.05f, 1.085f, .018f), enamel);
                Box(root.transform, "CounterCutEndCap", new Vector3(.764f, .517f, .196f), new Vector3(.018f, .966f, 1.108f), enamel);
                Box(root.transform, "CounterCutReturnCap", new Vector3(.6883f, .517f, .759f), new Vector3(.1334f, .966f, .018f), enamel);
                Pair(root.transform, "CABINET_FRIDGE_UPPER", -.395f, .395f, 1.865f, 2.60f, enamel, metal);
                Door(root.transform, "CABINET_NARROW_LOWER", .405f, .675f, .12f, .985f, 1, true, enamel, metal, .32f);
                Door(root.transform, "CABINET_NARROW_UPPER", .405f, .675f, 1.025f, 1.47f, 1, true, enamel, metal, .32f);
                Microwave(root.transform, enamel, glass, metal);
                if (PrefabUtility.SaveAsPrefabAsset(root, PrefabPath) == null) throw new IOException(PrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
        }

        static void Case(Transform root, string name, float x0, float x1, float y0, float y1, Material enamel, Material interior, float front = .388f)
        {
            var centre = (x0 + x1) * .5f; var width = x1 - x0; var height = y1 - y0;
            Box(root, name + "_Back", new Vector3(centre, (y0 + y1) * .5f, -.121f), new Vector3(width, height, .018f), interior);
            var depth = front + .112f; var z = (front - .112f) * .5f;
            foreach (var x in new[] { x0 + .009f, x1 - .009f })
                Box(root, name + "_Side_" + (x < centre ? "R" : "L"), new Vector3(x, (y0 + y1) * .5f, z), new Vector3(.018f, height, depth), enamel);
            foreach (var y in new[] { y0 + .009f, y1 - .009f })
                Box(root, name + "_Shelf_" + (y < (y0 + y1) * .5f ? "Bottom" : "Top"), new Vector3(centre, y, z), new Vector3(width - .036f, .018f, depth), enamel);
        }

        static void Pair(Transform root, string id, float x0, float x1, float y0, float y1, Material enamel, Material metal, float front = Front)
        {
            var mid = (x0 + x1) * .5f;
            Door(root, id + "_L", mid + Gap * .5f, x1, y0, y1, 1, false, enamel, metal, front);
            Door(root, id + "_R", x0, mid - Gap * .5f, y0, y1, -1, false, enamel, metal, front);
        }

        static void Door(Transform root, string id, float x0, float x1, float y0, float y1, float side, bool vertical, Material enamel, Material metal, float front = Front)
        {
            x0 += Gap * .5f; x1 -= Gap * .5f;
            var width = x1 - x0; var height = y1 - y0;
            var hinge = new GameObject(id); hinge.transform.SetParent(root, false);
            hinge.transform.localPosition = new Vector3(side > 0 ? x1 : x0, y0, front);
            hinge.AddComponent<ReviewSceneIdentity>().Configure(id);
            Box(hinge.transform, "DoorPanel", new Vector3(-side * width * .5f, height * .5f, 0), new Vector3(width, height, .018f), enamel);
            // Centre the narrow-door pull to keep it clear of the unchanged fridge hinge sweep.
            var handleCentre = new Vector3(-side * (width * .5f), vertical ? height * .65f : .045f, .03f);
            var handleSize = vertical ? new Vector3(.012f, Mathf.Min(.16f, height * .45f), .018f) : new Vector3(.12f, .012f, .018f);
            if (!vertical) handleCentre.x = -side * (width - .09f);
            var handle = Box(hinge.transform, "PullHandle", handleCentre, handleSize, metal);
            var xr = hinge.AddComponent<XRSimpleInteractable>(); xr.colliders.Clear();
            xr.colliders.AddRange(hinge.GetComponentsInChildren<Collider>().Where(c => c.enabled));
            var panelProbe = Probe(hinge.transform, "COL_SweepProbe", new Vector3(-side * width * .5f, height * .5f, 0), new Vector3(width, height, .018f));
            var handleProbe = Probe(hinge.transform, "COL_HandleSweepProbe", handleCentre, handleSize);
            var door = hinge.AddComponent<KinematicSwingDoor>();
            var data = new SerializedObject(door);
            data.FindProperty("openAngleDegrees").floatValue = 90;
            data.FindProperty("swingSign").floatValue = side;
            data.FindProperty("sweepStepDegrees").floatValue = .2f;
            data.FindProperty("sweepProbe").objectReferenceValue = panelProbe;
            data.FindProperty("handleSweepProbe").objectReferenceValue = handleProbe;
            data.FindProperty("collisionProfile").objectReferenceValue = AssetDatabase.LoadAssetAtPath<HandInteractionProfile>("Assets/_Project/Settings/SET_HandInteraction.asset");
            data.ApplyModifiedPropertiesWithoutUndo(); door.ConfigurePullHandle(handle.transform);
        }

        static BoxCollider Probe(Transform parent, string name, Vector3 centre, Vector3 size)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            var probe = go.AddComponent<BoxCollider>(); probe.center = centre; probe.size = size; probe.enabled = false; return probe;
        }
        static void Microwave(Transform parent, Material enamel, Material glass, Material metal)
        {
            var root = new GameObject("Microwave_VisualOnly"); root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(1.015f, 1.197f, .16f);
            root.AddComponent<ReviewSceneIdentity>().Configure("MICROWAVE_VISUAL");
            Box(root.transform, "Body", Vector3.zero, new Vector3(.52f, .30f, .38f), enamel);
            Box(root.transform, "DarkGlassDoor", new Vector3(.04f, 0, .193f), new Vector3(.405f, .248f, .009f), glass);
            Box(root.transform, "DoorPull", new Vector3(-.138f, 0, .213f), new Vector3(.012f, .18f, .025f), metal);
            Box(root.transform, "Display", new Vector3(-.216f, .075f, .194f), new Vector3(.055f, .035f, .008f), glass);
            foreach (var y in new[] { -.015f, -.075f })
                Box(root.transform, "Control_" + y, new Vector3(-.216f, y, .20f), new Vector3(.027f, .027f, .012f), metal);
            foreach (var x in new[] { -.2f, .2f })
                Box(root.transform, "Foot_" + x, new Vector3(x, -.155f, .08f), new Vector3(.04f, .01f, .1f), metal);
        }
        static GameObject Box(Transform parent, string name, Vector3 centre, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(parent, false);
            go.transform.localPosition = centre; go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material; return go;
        }
    }
}
