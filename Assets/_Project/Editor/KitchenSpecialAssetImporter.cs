using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using XRApartment.Runtime;

namespace XRApartment.EditorTools
{
    /// Separate acceptance paths for a body rig and an authored architectural module.
    public static class KitchenSpecialAssetImporter
    {
        [Serializable] public sealed class Authoring
        {
            public bool special_authoring_completed;
            public string[] character_actions;
            public float[] hinge_unity_xyz;
            public string door_visual, door_collider;
        }
        [MenuItem("XR Apartment/Assets/Import Authored Character or Module")]
        public static void ImportMenu()
        {
            KitchenAuthoring.RequireEditMode();
            var file = EditorUtility.OpenFilePanel("Choose the authored unity_asset.json", "", "json");
            if (string.IsNullOrEmpty(file)) return;
            Selection.activeObject = Import(file); EditorGUIUtility.PingObject(Selection.activeObject);
        }
        public static void ImportBatch()
        {
            var args = Environment.GetCommandLineArgs(); var index = Array.IndexOf(args, "-kitchenManifest");
            if (index < 0 || index + 1 >= args.Length) throw new ArgumentException("Expected -kitchenManifest path");
            var prefab = Import(args[index + 1]); Debug.Log("KITCHEN_SPECIAL_IMPORTED " + AssetDatabase.GetAssetPath(prefab));
        }
        public static GameObject Import(string path)
        {
            KitchenAuthoring.RequireEditMode();
            var json = File.ReadAllText(path);
            var common = JsonUtility.FromJson<KitchenPreparedAssetImporter.Manifest>(json);
            var authoring = JsonUtility.FromJson<Authoring>(json);
            if (common == null || common.schema_version != 1 || common.version < 1 ||
                !Regex.IsMatch(common.asset_name ?? "", "^[a-z][a-z0-9_]{1,63}$") || !common.design_approved ||
                common.diagnostic_fixture || !authoring.special_authoring_completed || common.unity_units != "metres" ||
                common.game_triangles > common.triangle_budget || (common.asset_class != "character" && common.asset_class != "building"))
                throw new InvalidDataException("Special assets require a reviewed, authored and budgeted descriptor.");
            var version = common.asset_name + "_v" + common.version.ToString("000");
            var folder = KitchenAuthoring.Root + "Art/Generated/" + version;
            var prefabFolder = KitchenAuthoring.Prefabs + "Generated/" + version;
            if (AssetDatabase.IsValidFolder(folder) || AssetDatabase.IsValidFolder(prefabFolder)) throw new InvalidOperationException("Keep existing version: " + version);
            var source = Path.GetDirectoryName(Path.GetFullPath(path));
            KitchenAuthoring.EnsureFolder(folder);
            GameObject root = null;
            try
            {
                KitchenPreparedAssetImporter.CopyRequired(source, common.intact_fbx, folder);
                KitchenAuthoring.EnsureFolder(folder + "/Textures");
                foreach (var file in Directory.GetFiles(Path.Combine(source, "Textures"), "*.png"))
                    File.Copy(file, folder + "/Textures/" + Path.GetFileName(file));
                File.Copy(path, folder + "/unity_asset.json");
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                var modelPath = folder + "/" + common.intact_fbx;
                KitchenPreparedAssetImporter.ConfigureModel(modelPath);
                if (common.asset_class == "character")
                {
                    var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
                    importer.importAnimation = true; importer.animationType = ModelImporterAnimationType.Generic;
                    importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                    importer.animationCompression = ModelImporterAnimationCompression.Off;
                    importer.SaveAndReimport();
                }
                KitchenPreparedAssetImporter.ConfigureTextures(folder);
                var prefix = common.asset_class == "character" ? "PREF_Character_" : "PREF_Module_";
                root = new GameObject(prefix + common.asset_name);
                var model = UnityEngine.Object.Instantiate(KitchenAuthoring.LoadRequired<GameObject>(modelPath), root.transform);
                model.name = "Model";
                KitchenPreparedAssetImporter.AdaptMaterials(model, folder, common.pbr_textures, new Dictionary<Material, Material>());
                if (common.asset_class == "character") ConfigureCharacter(root, model, modelPath, folder, authoring);
                else ConfigureModule(root, model, authoring);
                KitchenAuthoring.EnsureFolder(prefabFolder);
                var result = PrefabUtility.SaveAsPrefabAsset(root, prefabFolder + "/" + root.name + ".prefab");
                if (result == null) throw new InvalidOperationException("Special prefab save failed");
                AssetDatabase.SaveAssets(); return result;
            }
            catch
            {
                if (AssetDatabase.IsValidFolder(prefabFolder)) AssetDatabase.DeleteAsset(prefabFolder);
                AssetDatabase.DeleteAsset(folder); throw;
            }
            finally { if (root != null) UnityEngine.Object.DestroyImmediate(root); }
        }

        static void ConfigureCharacter(GameObject root, GameObject model, string modelPath, string folder, Authoring authoring)
        {
            if (authoring.character_actions == null || authoring.character_actions.Length != 4 || model.GetComponentsInChildren<SkinnedMeshRenderer>().Length == 0)
                throw new InvalidDataException("Expected four body clips and a skinned character, not a static figure");
            var animator = model.GetComponent<Animator>(); if (animator == null) animator = model.AddComponent<Animator>();
            animator.avatar = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Avatar>().Single();
            animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var clips = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>().Where(clip => !clip.name.StartsWith("__preview__")).ToArray();
            var controller = AnimatorController.CreateAnimatorControllerAtPath(folder + "/CTRL_CharacterPreview.controller");
            foreach (var name in authoring.character_actions)
            {
                var matches = clips.Where(clip => clip.name == name || clip.name.EndsWith("|" + name, StringComparison.Ordinal)).ToArray();
                if (matches.Length != 1) throw new InvalidDataException("Expected one imported body clip: " + name + "; found: " + string.Join(", ", clips.Select(clip => clip.name)));
                var state = controller.layers[0].stateMachine.AddState(name); state.motion = matches[0];
                if (name == "Stand") controller.layers[0].stateMachine.defaultState = state;
            }
            animator.runtimeAnimatorController = controller;
            foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>()) skin.updateWhenOffscreen = true;
            root.AddComponent<CharacterMotionPreview>().Configure(animator, authoring.character_actions);
        }

        static void ConfigureModule(GameObject root, GameObject model, Authoring authoring)
        {
            if (authoring.hinge_unity_xyz == null || authoring.hinge_unity_xyz.Length != 3) throw new InvalidDataException("Missing authored hinge");
            KitchenPreparedAssetImporter.InstallColliders(model);
            var transforms = model.GetComponentsInChildren<Transform>();
            var door = transforms.Single(t => t.name == authoring.door_visual);
            var collider = transforms.Single(t => t.name == authoring.door_collider);
            var hinge = new GameObject("ACT_CabinetHinge"); hinge.transform.SetParent(root.transform, false);
            hinge.transform.localPosition = new Vector3(authoring.hinge_unity_xyz[0], authoring.hinge_unity_xyz[1], authoring.hinge_unity_xyz[2]);
            door.SetParent(hinge.transform, true); collider.SetParent(hinge.transform, true);
            var body = hinge.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
            var interactable = hinge.AddComponent<XRSimpleInteractable>(); interactable.colliders.Clear(); interactable.colliders.Add(collider.GetComponent<Collider>());
            hinge.AddComponent<KinematicSwingDoor>();
        }
    }
}
