using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using XRApartment.Data;
using XRApartment.Runtime;

namespace XRApartment.EditorTools
{
    /// Imports the FBX bridge, not the GLB master; no additional glTF package is required.
    public static class KitchenPreparedAssetImporter
    {
        [Serializable] public sealed class TexturePaths
        {
            public string basecolor, metallic_smoothness, normal, ao;
        }

        [Serializable] public sealed class PieceInfo { public string visual, collider; public float volume_m3; public PieceInfo[] children; }

        [Serializable] public sealed class Manifest
        {
            public int schema_version, version, piece_count, game_triangles, triangle_budget;
            public string asset_name, asset_class, unity_units, intact_fbx, pieces_fbx, recipe, source_provider, source_sha256;
            public bool diagnostic_fixture, design_approved;
            public float[] size_blender_xyz;
            public PieceInfo[] pieces;
            public TexturePaths pbr_textures;
        }

        [MenuItem("XR Apartment/Assets/Import Prepared Kitchen Asset")]
        public static void ImportMenu()
        {
            KitchenAuthoring.RequireEditMode();
            var file = EditorUtility.OpenFilePanel("Choose Blender's unity_asset.json", "", "json");
            if (string.IsNullOrEmpty(file)) return;
            var result = Import(file);
            Selection.activeObject = result;
            EditorGUIUtility.PingObject(result);
        }

        public static void ImportBatch()
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, "-kitchenManifest");
            if (index < 0 || index + 1 >= args.Length)
                throw new ArgumentException("Provide -kitchenManifest <prepared/unity_asset.json>");
            var prefab = Import(args[index + 1]);
            Debug.Log("KITCHEN_ASSET_IMPORTED " + AssetDatabase.GetAssetPath(prefab));
        }

        public static GameObject Import(string manifestPath, bool diagnostic = false)
        {
            KitchenAuthoring.RequireEditMode();
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(manifestPath));
            Validate(manifest, diagnostic);
            var parent = KitchenAuthoring.Root + "Art/Generated";
            var folder = parent + "/" + (diagnostic ? "Diagnostic_" : "") + manifest.asset_name + "_v" + manifest.version.ToString("000");
            var prefabFolder = KitchenAuthoring.Prefabs + "Generated/" + (diagnostic ? "Diagnostic_" : "") +
                manifest.asset_name + "_v" + manifest.version.ToString("000");
            if (AssetDatabase.IsValidFolder(folder) || AssetDatabase.IsValidFolder(prefabFolder))
                throw new InvalidOperationException("Keep existing asset/prefab versions: " + manifest.asset_name);
            KitchenAuthoring.EnsureFolder(folder);
            try
            {
                ImportFiles(manifestPath, manifest, folder);
                var profile = ScriptableObject.CreateInstance<PreparedPropSettings>();
                AssetDatabase.CreateAsset(profile, folder + "/SET_PreparedProp.asset");
                var root = new GameObject("PREF_Prop_" + manifest.asset_name);
                try
                {
                    var intact = UnityEngine.Object.Instantiate(KitchenAuthoring.LoadRequired<GameObject>(folder + "/" + manifest.intact_fbx));
                    intact.name = "Intact";
                    intact.transform.SetParent(root.transform, false);
                    var materials = new Dictionary<Material, Material>();
                    AdaptMaterials(intact, folder, manifest.pbr_textures, materials);
                    var physical = InstallColliders(intact);
                    if (physical == 0) throw new InvalidOperationException("No authored COL_ mesh in the FBX bridge.");
                    var rigidbody = root.AddComponent<Rigidbody>();
                    rigidbody.mass = profile.intactMass;
                    root.AddComponent<XRGrabInteractable>();
                    KitchenGrabAuthoring.ConfigureProp(root);
                    if (manifest.piece_count > 0)
                    {
                        var fragments = new GameObject("DerivedPieces").transform;
                        fragments.SetParent(root.transform, false);
                        var model = KitchenAuthoring.LoadRequired<GameObject>(folder + "/" + manifest.pieces_fbx);
                        var bodies = CreatePieces(model, manifest.pieces, fragments, profile,
                            manifest.recipe == "slice", profile.intactMass, folder, manifest.pbr_textures, materials, 0);
                        var kind = manifest.recipe == "slice" ? PreparedPropKind.Slice :
                            manifest.recipe == "egg" ? PreparedPropKind.Eggshell : PreparedPropKind.Fracture;
                        root.AddComponent<PreparedKitchenProp>().Configure(profile, kind, intact.transform, fragments, bodies);
                        fragments.gameObject.SetActive(false);
                    }
                    // Characters and building modules retain pending rig/pivot QA;
                    // this importer deliberately creates interactive props only.
                    KitchenAuthoring.EnsureFolder(prefabFolder);
                    var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabFolder + "/PREF_Prop_" + manifest.asset_name + ".prefab");
                    if (prefab == null) throw new InvalidOperationException("Prefab save failed.");
                    AssetDatabase.SaveAssets();
                    return prefab;
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
            catch
            {
                // Only this newly created version is owned by the failed import.
                if (AssetDatabase.IsValidFolder(prefabFolder)) AssetDatabase.DeleteAsset(prefabFolder);
                AssetDatabase.DeleteAsset(folder);
                throw;
            }
        }

        static Rigidbody[] CreatePieces(GameObject model, PieceInfo[] pieces, Transform parent,
            PreparedPropSettings profile, bool grabbable, float totalMass, string folder,
            TexturePaths textures, Dictionary<Material, Material> materials, int depth)
        {
            if (depth > 1 || pieces.Length < 2 || pieces.Length > 24)
                throw new InvalidDataException("Prepared cuts support two finite stages with 2–24 pieces each.");
            var result = new Rigidbody[pieces.Length];
            var volume = pieces.Sum(piece => Mathf.Max(0, piece.volume_m3));
            for (var i = 0; i < pieces.Length; i++)
            {
                var info = pieces[i];
                var piece = new GameObject("Piece_" + i.ToString("00")); piece.transform.SetParent(parent, false);
                var intact = new GameObject("Intact").transform; intact.SetParent(piece.transform, false);
                CopyMesh(model, info.visual, intact); CopyMesh(model, info.collider, intact);
                AdaptMaterials(piece, folder, textures, materials); InstallColliders(piece);
                var body = piece.AddComponent<Rigidbody>(); result[i] = body;
                body.mass = volume > 0 ? Mathf.Max(.001f, totalMass * info.volume_m3 / volume) : totalMass / pieces.Length;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                body.interpolation = RigidbodyInterpolation.Interpolate; body.isKinematic = true; body.useGravity = false;
                if (grabbable) { piece.AddComponent<XRGrabInteractable>(); KitchenGrabAuthoring.ConfigureProp(piece); }
                if (info.children != null && info.children.Length > 0)
                {
                    var children = new GameObject("DerivedPieces").transform; children.SetParent(piece.transform, false);
                    var childBodies = CreatePieces(model, info.children, children, profile, grabbable,
                        body.mass, folder, textures, materials, depth + 1);
                    piece.AddComponent<PreparedKitchenProp>().Configure(profile, PreparedPropKind.Slice, intact, children, childBodies);
                    children.gameObject.SetActive(false);
                }
            }
            return result;
        }

        static void Validate(Manifest manifest, bool diagnostic)
        {
            if (manifest == null || manifest.schema_version != 1 || manifest.version < 1 ||
                !Regex.IsMatch(manifest.asset_name ?? "", "^[a-z][a-z0-9_]{1,63}$"))
                throw new InvalidDataException("Unsupported or invalid asset manifest.");
            if (manifest.diagnostic_fixture != diagnostic || (!diagnostic && !manifest.design_approved))
                throw new InvalidDataException("Production imports require approved design and actual source; diagnostics use a separate path.");
            if (manifest.asset_class != "prop")
                throw new InvalidDataException("Characters need rig QA; building modules need dimensions and moving-part authoring in Blender.");
            if (manifest.unity_units != "metres" || manifest.game_triangles > manifest.triangle_budget ||
                manifest.piece_count < 0 || manifest.piece_count > 24 || manifest.piece_count == 1 ||
                (manifest.piece_count > 0 && (manifest.pieces == null || manifest.pieces.Length != manifest.piece_count)))
                throw new InvalidDataException("Invalid unit, triangle budget or piece set.");
        }

        internal static void CopyRequired(string source, string filename, string target)
        {
            if (string.IsNullOrEmpty(filename) || filename != Path.GetFileName(filename) || Path.GetExtension(filename).ToLowerInvariant() != ".fbx")
                throw new InvalidDataException("Expected a local FBX filename.");
            File.Copy(Path.Combine(source, filename), target + "/" + filename);
        }

        internal static void ImportFiles(string manifestPath, Manifest manifest, string folder)
        {
            var source = Path.GetDirectoryName(Path.GetFullPath(manifestPath));
            var models = manifest.piece_count > 0 ? new[] { manifest.intact_fbx, manifest.pieces_fbx } : new[] { manifest.intact_fbx };
            foreach (var model in models) CopyRequired(source, model, folder);
            var textureSource = Path.Combine(source, "Textures");
            if (Directory.Exists(textureSource))
            {
                KitchenAuthoring.EnsureFolder(folder + "/Textures");
                foreach (var image in Directory.GetFiles(textureSource, "*.png"))
                    File.Copy(image, folder + "/Textures/" + Path.GetFileName(image));
            }
            File.Copy(manifestPath, folder + "/unity_asset.json");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var modelPaths = models.Select(name => folder + "/" + name).ToArray();
            foreach (var model in modelPaths) ConfigureModel(model);
            // Keep FBX material dependencies as well as the four Standard PBR maps.
            // Export-only metallic/roughness and unused embedded copies stay in ArtSource.
            var required = new HashSet<string>(AssetDatabase.GetDependencies(modelPaths, true), StringComparer.Ordinal);
            var maps = manifest.pbr_textures;
            if (maps != null)
                foreach (var map in new[] { maps.basecolor, maps.metallic_smoothness, maps.normal, maps.ao })
                    if (!string.IsNullOrEmpty(map)) required.Add(folder + "/Textures/" + map);
            if (Directory.Exists(folder + "/Textures"))
                foreach (var image in Directory.GetFiles(folder + "/Textures", "*.png").Select(path => path.Replace('\\', '/')))
                    if (!required.Contains(image)) AssetDatabase.DeleteAsset(image);
            ConfigureTextures(folder);
        }

        internal static void ConfigureModel(string path)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.isReadable = true;
            importer.addCollider = false;
            importer.importAnimation = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.SaveAndReimport();
        }

        internal static void ConfigureTextures(string folder)
        {
            foreach (var id in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(id);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                var name = Path.GetFileNameWithoutExtension(path);
                if (name == "normal") importer.textureType = TextureImporterType.NormalMap;
                else if (name != "basecolor" && !name.StartsWith("embedded_", StringComparison.Ordinal))
                    importer.sRGBTexture = false;
                importer.SaveAndReimport();
            }
        }

        internal static void AdaptMaterials(GameObject root, string folder, TexturePaths textures, Dictionary<Material, Material> cache)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)) continue;
                if (renderer.name.StartsWith("COL_", StringComparison.Ordinal)) continue;
                var values = renderer.sharedMaterials;
                for (var i = 0; i < values.Length; i++)
                {
                    var original = values[i];
                    if (original == null) continue;
                    if (!cache.TryGetValue(original, out var material))
                    {
                        material = new Material(original) { shader = Shader.Find("Standard") };
                        if (textures != null && !original.name.StartsWith("MAT_Interior_", StringComparison.Ordinal))
                        {
                            SetMap(material, "_MainTex", textures.basecolor, folder);
                            SetMap(material, "_MetallicGlossMap", textures.metallic_smoothness, folder);
                            SetMap(material, "_BumpMap", textures.normal, folder);
                            SetMap(material, "_OcclusionMap", textures.ao, folder);
                            if (material.GetTexture("_MetallicGlossMap") != null)
                            { material.EnableKeyword("_METALLICGLOSSMAP"); material.SetFloat("_GlossMapScale", 1f); }
                            if (material.GetTexture("_BumpMap") != null) material.EnableKeyword("_NORMALMAP");
                        }
                        var path = AssetDatabase.GenerateUniqueAssetPath(folder + "/MAT_" + cache.Count.ToString("00") + ".mat");
                        AssetDatabase.CreateAsset(material, path);
                        cache.Add(original, material);
                    }
                    values[i] = material;
                }
                renderer.sharedMaterials = values;
            }
        }

        internal static void SetMap(Material material, string property, string filename, string folder)
        {
            if (string.IsNullOrEmpty(filename)) return;
            if (filename != Path.GetFileName(filename)) throw new InvalidDataException("Invalid texture filename.");
            material.SetTexture(property, KitchenAuthoring.LoadRequired<Texture2D>(folder + "/Textures/" + filename));
        }

        internal static int InstallColliders(GameObject root)
        {
            var count = 0;
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!filter.name.StartsWith("COL_", StringComparison.Ordinal)) continue;
                var collider = filter.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = filter.sharedMesh;
                collider.convex = true;
                var renderer = filter.GetComponent<MeshRenderer>();
                if (renderer != null) renderer.enabled = false;
                count++;
            }
            return count;
        }

        internal static void CopyMesh(GameObject model, string name, Transform parent)
        {
            var source = model.GetComponentsInChildren<MeshFilter>(true).Single(f => f.name == name);
            var clone = UnityEngine.Object.Instantiate(source.gameObject, parent);
            // Include the FBX root's axis transform while retaining the common origin.
            clone.transform.localPosition = model.transform.InverseTransformPoint(source.transform.position);
            clone.transform.localRotation = Quaternion.Inverse(model.transform.rotation) * source.transform.rotation;
            clone.transform.localScale = source.transform.lossyScale;
        }
    }
}
