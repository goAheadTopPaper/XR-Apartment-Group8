using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using XRApartment.Data;
using XRApartment.Data.ReviewV2;
using XRApartment.Runtime;
using XRApartment.Runtime.ReviewV2;
using Object = UnityEngine.Object;

namespace XRApartment.EditorTools
{
    public static class ReviewV2BStorageAuthoring
    {
        public const string Version = "external-fridge-m2-v8";
        public const string PrefabPath = "Assets/_Project/Prefabs/ReviewV2/Schemes/PREF_B_StorageAndPrep.prefab";
        public const string ArtFolder = "Assets/_Project/Art/ReviewV2/B_Storage";
        public const float WallX = -1.4519f;
        public const float ExistingCounterFront = 1.67661285f;
        public const float CounterNorth = ExistingCounterFront - .003f;
        public const float CounterSouth = .70f;
        public const float CounterWidth = CounterNorth - CounterSouth;
        public const float ShelfSouth = .70f;
        public const float ShelfNorth = 2.26f;
        public const float CounterHeightOffsetFromA = -.01f;
        static readonly string[] CounterBindings = {
            "VIS_Real_gas_hob", "COL_GasHob", "ACT_STOVE", "VIS_GasIndicator",
            "LOC_BurnerSurface", "ACT_HeatZone", "FX_GasBurner",
            "PREF_Cooking_FryingPan", "PREF_Cooking_ChoppingBoard", "PREF_Prop_knife",
            "PREF_Cooking_MixingBowl", "PREF_Cooking_TomatoBowl", "PREF_Cooking_Whisk",
            "PREF_Cooking_Spatula", "PREF_Cooking_Ladle", "PREF_Prop_plate"
        };
        const float DoorFront = .278f;

        [MenuItem("XR Apartment/Review V2/Apply B Storage And Prep Design")]
        public static void Apply()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Stop Play before authoring");
            BuildPrefab();
            var path = ReviewV2SchemeAuthoring.LayoutPath(SchemeIds.ExternalRefrigerator);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var world = root.GetComponent<ReviewSchemeWorld>(); Attach(world);
                if (PrefabUtility.SaveAsPrefabAsset(root, path) == null) throw new IOException(path);
                File.WriteAllLines("Docs/M2/SCHEME_B_Identities.tsv", world.identities.Select(i =>
                    world.schemeId + "\t" + Version + "\t" + i.Id + "\t" + i.name));
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("REVIEW_V2_B_STORAGE: " + Version + "; extended shelf, fixed microwave, A counter placements and current B fridge pose");
        }

        public static void Attach(ReviewSchemeWorld world)
        {
            if (world.schemeId != SchemeIds.ExternalRefrigerator) throw new InvalidOperationException("B-only storage");
            ReviewV2BFridgeAuthoring.ConfigurePosition(world);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null) BuildPrefab();
            var legacy = world.transform.Find("Released_Kitchen_PrepCounter");
            if (legacy != null)
            {
                if (legacy.GetComponent<ReviewSceneIdentity>()?.Id != "CABINET_LAYOUT") throw new InvalidOperationException("Preserve unknown legacy cabinet");
                Object.DestroyImmediate(legacy.gameObject);
            }
            var instance = world.transform.Find("B_StorageAndPrep");
            if (instance == null)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), world.transform);
                go.name = "B_StorageAndPrep"; instance = go.transform;
            }
            else if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instance.gameObject) != PrefabPath)
                throw new InvalidOperationException("Preserve unrecognized storage instance");
            instance.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity); instance.localScale = Vector3.one;
            CopyCounterLayoutFromA(world);
            world.identities = world.GetComponentsInChildren<ReviewSceneIdentity>(true);
            if (world.identities.GroupBy(i => i.Id).Any(g => g.Count() != 1)) throw new InvalidOperationException("Duplicate stable storage IDs");
            world.modelVersion = Version;
        }

        static void CopyCounterLayoutFromA(ReviewSchemeWorld world)
        {
            var reference = AssetDatabase.LoadAssetAtPath<GameObject>(ReviewV2SchemeAuthoring.LayoutPath(SchemeIds.IntegratedKitchen))
                .GetComponent<ReviewSchemeWorld>();
            var offset = Vector3.up * CounterHeightOffsetFromA;
            foreach (var name in CounterBindings)
            {
                var source = reference.station.transform.Find(name);
                var target = world.station.transform.Find(name);
                if (source == null || target == null) throw new InvalidOperationException("Missing counter binding: " + name);
                target.SetPositionAndRotation(source.position + offset, source.rotation);
            }
            // B retains its 1.00m source top; A's replacement top is 1.01m high.
            // Only authored poses are copied, leaving geometry and pan behavior unchanged.
            var anchor = world.GetComponentsInChildren<ReviewSceneIdentity>(true).Single(i => i.Id == "WORKTOP");
            anchor.transform.position = world.station.transform.Find("PREF_Cooking_ChoppingBoard").position;
        }

        static GameObject Node(Transform parent, string name)
        {
            var t = parent.Find(name);
            var go = t == null ? new GameObject(name) : t.gameObject;
            if (t == null) go.transform.SetParent(parent, false);
            go.SetActive(true); return go;
        }
        static T Component<T>(GameObject go) where T : UnityEngine.Component
        {
            var component = go.GetComponent<T>();
            return component != null ? component : go.AddComponent<T>();
        }
        static void Identity(GameObject go, string id)
        { Component<ReviewSceneIdentity>(go).Configure(id); }
        static Material Finish(string name, Color colour, float smoothness, Material source = null)
        {
            var path = ArtFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(source ?? AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Cooking/Furniture/MAT_Furniture_FridgeEnamel.mat"));
                material.name = name; AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", colour); material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", 0); material.SetColor("_EmissiveColor", Color.black);
            EditorUtility.SetDirty(material); return material;
        }
        static GameObject Box(Transform parent, string name, Vector3 position, Vector3 size, Material material)
        {
            var go = Node(parent, name); go.transform.SetLocalPositionAndRotation(position, Quaternion.identity); go.transform.localScale = size;
            var filter = Component<MeshFilter>(go);
            filter.sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            var renderer = Component<MeshRenderer>(go); renderer.sharedMaterial = material;
            renderer.lightmapIndex = -1; renderer.realtimeLightmapIndex = -1;
            var collider = Component<BoxCollider>(go); collider.center = Vector3.zero; collider.size = Vector3.one; collider.enabled = true;
            return go;
        }
        static void BevelBoard(Transform parent, string name, Vector3 centre, Vector3 size, float bevel, Material material)
        {
            var go = Box(parent, name, centre, size, material); go.transform.localScale = Vector3.one;
            go.GetComponent<BoxCollider>().size = size;
            var path = ArtFolder + "/MESH_" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                existing = BevelMesh(size, bevel); existing.name = name; AssetDatabase.CreateAsset(existing, path);
            }
            else if (Vector3.Distance(existing.bounds.size, size) > .000001f)
            {
                var rebuilt = BevelMesh(size, bevel); rebuilt.name = name;
                EditorUtility.CopySerialized(rebuilt, existing); Object.DestroyImmediate(rebuilt); EditorUtility.SetDirty(existing);
            }
            go.GetComponent<MeshFilter>().sharedMesh = existing;
        }
        // Six flat faces, twelve bevel strips and eight corner triangles; positive scales only.
        static Mesh BevelMesh(Vector3 size, float bevel)
        {
            var vertices = new List<Vector3>(); var normals = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            var h = size * .5f; var inner = h - Vector3.one * bevel;
            Action<Vector3[], Vector3> face = (points, normal) =>
            {
                if (Vector3.Dot(Vector3.Cross(points[1] - points[0], points[2] - points[0]), normal) < 0) Array.Reverse(points);
                var start = vertices.Count;
                foreach (var p in points)
                {
                    vertices.Add(p); normals.Add(normal.normalized);
                    uv.Add(Mathf.Abs(normal.y) > .5f ? new Vector2(p.x / size.x + .5f, p.z / size.z + .5f) :
                        Mathf.Abs(normal.x) > .5f ? new Vector2(p.z / size.z + .5f, p.y / size.y + .5f) : new Vector2(p.x / size.x + .5f, p.y / size.y + .5f));
                }
                for (var i = 1; i < points.Length - 1; i++) { triangles.Add(start); triangles.Add(start + i); triangles.Add(start + i + 1); }
            };
            for (var axis = 0; axis < 3; axis++) foreach (var sign in new[] { -1, 1 })
            {
                var p = new Vector3[4]; var a = (axis + 1) % 3; var b = (axis + 2) % 3;
                for (var i = 0; i < 4; i++) { p[i][axis] = sign * h[axis]; p[i][a] = (i == 0 || i == 3 ? -1 : 1) * inner[a]; p[i][b] = (i < 2 ? -1 : 1) * inner[b]; }
                var n = Vector3.zero; n[axis] = sign; face(p, n);
            }
            for (var along = 0; along < 3; along++) foreach (var sa in new[] { -1, 1 }) foreach (var sb in new[] { -1, 1 })
            {
                var a = (along + 1) % 3; var b = (along + 2) % 3; var p = new Vector3[4];
                for (var i = 0; i < 4; i++)
                { p[i][along] = (i < 2 ? -1 : 1) * inner[along]; p[i][a] = sa * (i == 0 || i == 3 ? h[a] : inner[a]); p[i][b] = sb * (i == 0 || i == 3 ? inner[b] : h[b]); }
                var n = Vector3.zero; n[a] = sa; n[b] = sb; face(p, n);
            }
            foreach (var x in new[] { -1, 1 }) foreach (var y in new[] { -1, 1 }) foreach (var z in new[] { -1, 1 })
                face(new[] { new Vector3(x * h.x, y * inner.y, z * inner.z), new Vector3(x * inner.x, y * h.y, z * inner.z), new Vector3(x * inner.x, y * inner.y, z * h.z) }, new Vector3(x, y, z));
            var mesh = new Mesh(); mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds(); mesh.RecalculateTangents(); return mesh;
        }

        public static void BuildPrefab()
        {
            if (!AssetDatabase.IsValidFolder(ArtFolder)) AssetDatabase.CreateFolder("Assets/_Project/Art/ReviewV2", "B_Storage");
            var warm = Finish("MAT_B_CabinetWarmWhite", new Color(.78f, .755f, .71f), .22f);
            var inset = Finish("MAT_B_CabinetInset", new Color(.735f, .715f, .675f), .20f);
            var interior = Finish("MAT_B_CabinetInterior", new Color(.64f, .62f, .58f), .18f);
            var hardware = Finish("MAT_B_CabinetHardware", new Color(.11f, .12f, .125f), .28f);
            var wood = Finish("MAT_B_WindowStorageWood", Color.white, .24f, AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Kitchen/Materials/M_WindowTable.mat"));
            // The source is an atlas. Sample one continuous wood patch, preserving matching PBR UVs.
            wood.SetTextureScale("_BaseColorMap", new Vector2(.20f, .30f)); wood.SetTextureOffset("_BaseColorMap", new Vector2(.315f, .58f));
            var top = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Cooking/Furniture/MAT_Furniture_FridgeEnamel.mat");
            var loaded = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;
            var root = loaded ? PrefabUtility.LoadPrefabContents(PrefabPath) : new GameObject("B_StorageAndPrep");
            try
            {
                var window = Node(root.transform, "WindowOpenStorage"); Identity(window, "WINDOW_STORAGE");
                window.transform.SetLocalPositionAndRotation(new Vector3(-4.79f, 0, -1.255f), Quaternion.Euler(0, 90, 0));
                window.transform.localScale = Vector3.one;
                BevelBoard(window.transform, "B_WindowTop", new Vector3(0, .985f, 0), new Vector3(1.35f, .03f, .60f), .003f, wood);
                Box(window.transform, "Back", new Vector3(0, .535f, -.291f), new Vector3(1.35f, .87f, .018f), wood);
                foreach (var x in new[] { -.665f, 0, .665f }) Box(window.transform, "Upright_" + x, new Vector3(x, .535f, 0), new Vector3(.02f, .87f, .60f), wood);
                foreach (var y in new[] { .11f, .54f }) Box(window.transform, "Shelf_" + y, new Vector3(0, y, 0), new Vector3(1.31f, .02f, .60f), wood);
                Box(window.transform, "RecessedPlinth", new Vector3(0, .05f, -.025f), new Vector3(1.29f, .10f, .45f), wood);

                var cabinet = Node(root.transform, "KitchenPrepCabinet"); Identity(cabinet, "CABINET_LAYOUT");
                cabinet.transform.SetLocalPositionAndRotation(new Vector3(WallX - .30f, 0, (CounterSouth + CounterNorth) * .5f), Quaternion.Euler(0, -90, 0));
                cabinet.transform.localScale = Vector3.one;
                Box(cabinet.transform, "ContinuousWorktop", new Vector3(0, .99f, 0), new Vector3(CounterWidth, .02f, .60f), top);
                Box(cabinet.transform, "CaseBack", new Vector3(0, .54f, -.291f), new Vector3(CounterWidth, .88f, .018f), interior);
                foreach (var side in new[] { -1, 1 })
                {
                    Box(cabinet.transform, "EndPanel_" + side, new Vector3(side * (CounterWidth * .5f - .009f), .54f, -.0275f), new Vector3(.018f, .88f, .545f), warm);
                    Box(cabinet.transform, "HingeReveal_" + side, new Vector3(side * (CounterWidth * .5f - .01f), .54f, .245f), new Vector3(.020f, .88f, .018f), warm);
                }
                foreach (var y in new[] { .109f, .52f }) Box(cabinet.transform, "InteriorShelf_" + y, new Vector3(0, y, -.0275f), new Vector3(CounterWidth - .036f, .018f, .545f), interior);
                Box(cabinet.transform, "RecessedToe", new Vector3(0, .05f, -.025f), new Vector3(CounterWidth - .04f, .10f, .45f), interior);
                var usable = CounterWidth * .5f - .020f;
                Door(cabinet.transform, "CABINET_PREP_NORTH", .0015f, usable, 1, warm, inset, hardware);
                Door(cabinet.transform, "CABINET_PREP_SOUTH", -usable, -.0015f, -1, warm, inset, hardware);
                var shelf = Node(root.transform, "FloatingShelf"); Identity(shelf, "FLOATING_SHELF");
                shelf.transform.SetLocalPositionAndRotation(new Vector3(WallX - .125f, 1.535f, (ShelfSouth + ShelfNorth) * .5f), Quaternion.Euler(0, -90, 0)); shelf.transform.localScale = Vector3.one;
                BevelBoard(shelf.transform, "B_FloatingShelfTop", Vector3.zero, new Vector3(ShelfNorth - ShelfSouth, .03f, .25f), .002f, warm);
                Box(shelf.transform, "ConcealedRearSupport", new Vector3(0, -.023f, -.105f), new Vector3(ShelfNorth - ShelfSouth - .09f, .016f, .035f), interior);
                var microwave = root.transform.Find("Microwave_VisualOnly");
                if (microwave == null)
                {
                    var source = AssetDatabase.LoadAssetAtPath<GameObject>(ReviewV2IntegratedCabinetAuthoring.PrefabPath)
                        .transform.Find("Microwave_VisualOnly");
                    if (source == null) throw new InvalidOperationException("Missing A microwave appearance");
                    var clone = Object.Instantiate(source.gameObject, root.transform, false);
                    clone.name = "Microwave_VisualOnly"; microwave = clone.transform;
                }
                microwave.SetLocalPositionAndRotation(new Vector3(WallX - .30f, 1.16f, 2.00f), Quaternion.Euler(0, -90, 0));
                microwave.localScale = Vector3.one;
                if (PrefabUtility.SaveAsPrefabAsset(root, PrefabPath) == null) throw new IOException(PrefabPath);
            }
            finally { if (loaded) PrefabUtility.UnloadPrefabContents(root); else Object.DestroyImmediate(root); }
        }

        static BoxCollider Probe(Transform parent, string name, Vector3 centre, Vector3 size)
        {
            var go = Node(parent, name); go.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity); go.transform.localScale = Vector3.one;
            var probe = Component<BoxCollider>(go); probe.center = centre; probe.size = size; probe.enabled = false; return probe;
        }
        static void Door(Transform parent, string id, float x0, float x1, float side, Material warm, Material inset, Material hardware)
        {
            var root = Node(parent, id); Identity(root, id);
            root.transform.SetLocalPositionAndRotation(new Vector3(side > 0 ? x1 : x0, .105f, DoorFront), Quaternion.identity); root.transform.localScale = Vector3.one;
            var width = x1 - x0; const float height = .86f, border = .020f;
            var cx = -side * width * .5f;
            Box(root.transform, "InsetDoorPanel", new Vector3(cx, height * .5f, 0), new Vector3(width, height, .018f), inset);
            foreach (var edge in new[] { -1, 1 })
            {
                Box(root.transform, "FrameVertical_" + edge, new Vector3(cx + edge * (width * .5f - border * .5f), height * .5f, .009f), new Vector3(border, height, .006f), warm);
                Box(root.transform, "FrameHorizontal_" + edge, new Vector3(cx, height * .5f + edge * (height * .5f - border * .5f), .009f), new Vector3(width - border * 2, border, .006f), warm);
            }
            var pullPosition = new Vector3(-side * (width - .065f), .645f, .030f);
            var pullSize = new Vector3(.012f, .14f, .018f);
            var handle = Box(root.transform, "PullHandle", pullPosition, pullSize, hardware);
            var xr = Component<XRSimpleInteractable>(root);
            xr.colliders.Clear(); xr.colliders.AddRange(root.GetComponentsInChildren<Collider>().Where(c => c.enabled && !c.isTrigger));
            var panelProbe = Probe(root.transform, "COL_SweepProbe", new Vector3(cx, height * .5f, .0015f), new Vector3(width, height, .021f));
            var handleProbe = Probe(root.transform, "COL_HandleSweepProbe", pullPosition, pullSize);
            var door = Component<KinematicSwingDoor>(root); var data = new SerializedObject(door);
            data.FindProperty("swingSign").floatValue = side; data.FindProperty("openAngleDegrees").floatValue = 90;
            data.FindProperty("startOpen").boolValue = false; data.FindProperty("sweepStepDegrees").floatValue = .2f;
            data.FindProperty("sweepProbe").objectReferenceValue = panelProbe; data.FindProperty("handleSweepProbe").objectReferenceValue = handleProbe;
            data.FindProperty("collisionProfile").objectReferenceValue = AssetDatabase.LoadAssetAtPath<HandInteractionProfile>("Assets/_Project/Settings/SET_HandInteraction.asset");
            data.ApplyModifiedPropertiesWithoutUndo(); door.ConfigurePullHandle(handle.transform);
        }
    }
}
