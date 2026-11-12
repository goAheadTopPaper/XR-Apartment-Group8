using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace XRApartment.EditorTools
{
    public static partial class KitchenCookingAuthoring
    {
        const string ApartmentArt = Art + "Apartment/";
        const string ApartmentPrefabs = KitchenAuthoring.Prefabs + "Apartment/";
        const string ExteriorDoorPath = ApartmentPrefabs + "PREF_Door_ApartmentExterior.prefab";
        const string ApartmentDressingPath = ApartmentPrefabs + "PREF_Apartment_Dressing.prefab";
        const string ExteriorDoorsName = "ApartmentExteriorDoors";
        const float DoorHeight = 2.70f;
        const float DoorLeafHeight = 2.32f;
        const float DoorDepth = .15f;
        const float CeilingHeight = 2.68f;
        const float FixtureThickness = .054f;

        // World positions and spans measured at the open wall faces in the authored room.
        static readonly (string name, string key, Vector3 position, float yaw, float width)[] ExteriorOpenings =
        {
            ("Door_EastEntry", "east_entry", new Vector3(.215f, 0f, -7.145f), 90f, .945f),
            ("Door_EastDining", "east_dining", new Vector3(.215f, 0f, -.38f), 90f, 1.275f),
            ("Door_EastKitchen", "east_kitchen", new Vector3(.215f, 0f, 1.73f), 90f, 1.38f),
            ("Door_SouthEnd", "south_end", new Vector3(-.365f, 0f, -7.79f), 180f, .99f)
        };

        [MenuItem("XR Apartment/Apartment/Apply Exterior Doors and Fixtures")]
        public static void ApplyApartmentMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) ApplyApartmentBatch();
        }

        public static void ApplyApartmentDoorsBatch()
        {
            KitchenAuthoring.RequireEditMode();
            KitchenAuthoring.EnsureFolder(ApartmentPrefabs.TrimEnd('/'));
            BuildExteriorDoor();
            KitchenAuthoring.EditPrefab(KitchenAuthoring.Prefabs + "PREF_Kitchen_Environment.prefab", PlaceExteriorDoors);
            AssetDatabase.SaveAssets();
        }

        public static void ApplyApartmentBatch()
        {
            ApplyApartmentDoorsBatch();
            KitchenAuthoring.EditPrefab(KitchenAuthoring.Prefabs + "PREF_Lighting_KitchenDaylight.prefab", ConfigureApartmentLights);
            BuildApartmentDressing();
            var cooking = EditorSceneManager.OpenScene(ScenePath);
            ConfigureApartmentDressing(cooking);
            KitchenAuthoring.SaveScene(cooking);
        }

        static void BuildExteriorDoor()
        {
            var root = new GameObject("PREF_Door_ApartmentExterior");
            try
            {
                // One full opening collider covers the fixed leaf, sill and upper panel.
                var barrier = root.AddComponent<BoxCollider>();
                barrier.center = Vector3.up * (DoorHeight * .5f - .008f);
                barrier.size = new Vector3(1f, DoorHeight + .016f, DoorDepth);
                AddApartmentModel(root.transform, "DoorLeaf", "exterior_oak_door",
                    new Vector3(0f, DoorLeafHeight * .5f, -.014f), new Vector3(1f, DoorLeafHeight, .112f));
                var trim = KitchenAuthoring.LoadRequired<Material>(KitchenAuthoring.Root + "Art/Kitchen/Materials/MAT_Kitchen_White.mat");
                const float panelBottom = DoorLeafHeight - .028f;
                var panel = Cube(root.transform, "UpperPanel", new Vector3(0f, (panelBottom + DoorHeight) * .5f, -.012f),
                    new Vector3(1f, DoorHeight - panelBottom, .118f), trim);
                Object.DestroyImmediate(panel.GetComponent<Collider>());
                ConfigureApartmentRenderer(panel.GetComponent<MeshRenderer>());

                // A square backing closes the tiny uneven edges of the generated leaf.
                var backing = Cube(root.transform, "DoorBacking", new Vector3(0f, DoorLeafHeight * .5f, .043f),
                    new Vector3(1f, DoorLeafHeight, .045f), trim);
                Object.DestroyImmediate(backing.GetComponent<Collider>());
                ConfigureApartmentRenderer(backing.GetComponent<MeshRenderer>());
                var probe = Child(root.transform, "LightProbeAnchor");
                probe.localPosition = new Vector3(0f, 1.42f, -.36f);
                foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>()) renderer.probeAnchor = probe;
                PrefabUtility.SaveAsPrefabAsset(root, ExteriorDoorPath);
            }
            finally { Object.DestroyImmediate(root); }
        }

        static void PlaceExteriorDoors(GameObject environment)
        {
            var parent = environment.transform.Find(ExteriorDoorsName) ?? Child(environment.transform, ExteriorDoorsName);
            var prefab = KitchenAuthoring.LoadRequired<GameObject>(ExteriorDoorPath);
            const string measurement = "Collider ray survey of shared apartment wall openings; prototype assumption.";
            foreach (var opening in ExteriorOpenings)
            {
                var door = parent.Find(opening.name) ??
                    ((GameObject)PrefabUtility.InstantiatePrefab(prefab, parent)).transform;
                door.name = opening.name;
                door.SetLocalPositionAndRotation(opening.position, Quaternion.Euler(0f, opening.yaw, 0f));
                door.localScale = new Vector3(opening.width, 1f, 1f);
                Parameter("apartment_exterior_" + opening.key + "_x", opening.position.x, measurement);
                Parameter("apartment_exterior_" + opening.key + "_z", opening.position.z, measurement);
                Parameter("apartment_exterior_" + opening.key + "_width", opening.width, measurement);
            }
            Parameter("apartment_exterior_door_height", DoorHeight, measurement);
            Parameter("apartment_exterior_door_depth", DoorDepth, measurement);
        }

        static void ConfigureApartmentLights(GameObject lighting)
        {
            if (AssetDatabase.LoadAssetAtPath<Mesh>(ApartmentArt + "corridor_ceiling_light.obj") == null) return;
            var fixtures = lighting.transform.Find("ApartmentCeilingFixtures") ?? Child(lighting.transform, "ApartmentCeilingFixtures");
            for (var i = fixtures.childCount - 1; i >= 0; i--) Object.DestroyImmediate(fixtures.GetChild(i).gameObject);
            Place("South door fill", -7.04f, .90f, 9.50f);
            Place("Entry passage fill", -4.85f, 1.20f, 6.20f);
            Place("Dining passage fill", -2.20f, .94f, 5.00f);
            Place("Kitchen passage fill", .40f, 1.20f, 6.00f);
            Place("North door fill", 1.78f, .72f, 6.00f);

            void Place(string name, float z, float length, float intensity)
            {
                var emitter = lighting.transform.Find(name) ?? Child(lighting.transform, name);
                emitter.SetLocalPositionAndRotation(new Vector3(-.37f, CeilingHeight - FixtureThickness - .028f, z),
                    Quaternion.Euler(90f, 0f, 0f));
                var light = GetOrAdd<Light>(emitter.gameObject);
                light.type = LightType.Rectangle;
                light.lightmapBakeType = LightmapBakeType.Baked;
                light.areaSize = new Vector2(.27f, length);
                light.intensity = intensity;
                light.range = 4.6f;
                light.color = new Color(1f, .95f, .87f);
                light.shadows = LightShadows.Soft;
                var visual = AddApartmentModel(fixtures, name + " fixture", "corridor_ceiling_light",
                    new Vector3(-.37f, CeilingHeight - FixtureThickness * .5f, z), new Vector3(.29f, FixtureThickness, length));
                visual.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        static void BuildApartmentDressing()
        {
            var root = new GameObject("PREF_Apartment_Dressing");
            try
            {
                const float tabletop = .758f;
                var planter = Child(root.transform, "DiningHerbPlanter");
                planter.localPosition = new Vector3(-2.94f, tabletop, -1.06f);
                AddApartmentModel(planter, "Planter", "herb_planter", Vector3.up * .14f,
                    new Vector3(.21f, .28f, .19f));
                var pot = planter.gameObject.AddComponent<BoxCollider>();
                pot.center = Vector3.up * .055f;
                pot.size = new Vector3(.148f, .11f, .14f);

                var artwork = Child(root.transform, "PassageWallRelief");
                artwork.SetLocalPositionAndRotation(new Vector3(-.923f, 1.57f, -6.70f), Quaternion.Euler(0f, -90f, 0f));
                var relief = AddApartmentModel(artwork, "Relief", "oak_wall_relief", Vector3.zero,
                    new Vector3(.50f, .46f, .040f));
                var probe = Child(artwork, "LightProbeAnchor");
                probe.localPosition = new Vector3(0f, 0f, -.28f);
                relief.probeAnchor = probe;
                PrefabUtility.SaveAsPrefabAsset(root, ApartmentDressingPath);
            }
            finally { Object.DestroyImmediate(root); }
        }

        static void ConfigureApartmentDressing(Scene scene)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ApartmentDressingPath);
            if (prefab != null && !scene.GetRootGameObjects().Any(go => go.name == prefab.name))
                PrefabUtility.InstantiatePrefab(prefab, scene);
        }

        static MeshRenderer AddApartmentModel(Transform parent, string label, string asset, Vector3 centre, Vector3 size)
        {
            var mesh = KitchenAuthoring.LoadRequired<Mesh>(ApartmentArt + asset + ".obj");
            var shape = Child(parent, label);
            var dimensions = mesh.bounds.size;
            if (dimensions.x < .0001f || dimensions.y < .0001f || dimensions.z < .0001f)
                throw new InvalidOperationException("Invalid generated model bounds for " + asset);
            shape.localScale = new Vector3(size.x / dimensions.x, size.y / dimensions.y, size.z / dimensions.z);
            shape.localPosition = centre - Vector3.Scale(mesh.bounds.center, shape.localScale);
            shape.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = shape.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = ApartmentMaterial(asset);
            ConfigureApartmentRenderer(renderer);
            return renderer;
        }

        static void ConfigureApartmentRenderer(MeshRenderer renderer)
        {
            GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic);
            renderer.receiveGI = ReceiveGI.LightProbes;
        }

        static Material ApartmentMaterial(string asset)
        {
            var path = ApartmentArt + "MAT_" + asset + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = Color.white;
            material.SetTexture("_MainTex", KitchenAuthoring.LoadRequired<Texture2D>(ApartmentArt + asset + "_albedo.png"));
            var normal = ApartmentTexture(asset, "_normal", TextureImporterType.NormalMap);
            material.SetTexture("_BumpMap", normal);
            if (normal != null) material.EnableKeyword("_NORMALMAP");
            var metal = ApartmentTexture(asset, "_metallic", TextureImporterType.Default);
            material.SetTexture("_MetallicGlossMap", metal);
            if (metal != null) material.EnableKeyword("_METALLICGLOSSMAP");
            material.SetFloat("_Glossiness", .33f);
            var emission = ApartmentTexture(asset, "_emission", TextureImporterType.Default);
            if (emission != null)
            {
                material.SetTexture("_EmissionMap", emission);
                material.SetColor("_EmissionColor", new Color(1.55f, 1.32f, 1.08f));
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        static Texture2D ApartmentTexture(string asset, string suffix, TextureImporterType type)
        {
            var path = ApartmentArt + asset + suffix + ".png";
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) return null;
            if (importer.textureType != type || importer.sRGBTexture)
            {
                importer.textureType = type;
                importer.sRGBTexture = false;
                importer.SaveAndReimport();
            }
            return KitchenAuthoring.LoadRequired<Texture2D>(path);
        }
    }
}
