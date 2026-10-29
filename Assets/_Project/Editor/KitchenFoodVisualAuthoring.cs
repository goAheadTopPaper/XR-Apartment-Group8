using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using XRApartment.Data;
using XRApartment.Runtime;

namespace XRApartment.EditorTools
{
    public static partial class KitchenCookingAuthoring
    {
        const string FoodArt = Art + "FoodVisuals/";
        const string FoodSource = "ArtSource/KitchenAssets/Recipe/v002_food_visuals/";

        [MenuItem("XR Apartment/Cooking/Apply Generated Food Visuals")]
        public static void ApplyGeneratedFoodVisualsMenu()
        {
            KitchenAuthoring.RequireEditMode();
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            ApplyGeneratedFoodVisualsBatch();
        }

        public static void ApplyGeneratedFoodVisualsBatch()
        {
            KitchenAuthoring.RequireEditMode();
            PrepareGeneratedFoodVisuals();
            var profile = KitchenAuthoring.LoadRequired<KitchenCookingProfile>(RecipeProfilePath);
            // Keep the generated albedo visible when cooked; the old brown
            // multiplier hid the seed/flesh detail even before food burnt.
            profile.cookedFoodTint = new Color(.96f, .89f, .80f, 1);
            EditorUtility.SetDirty(profile);
            ConfigureGeneratedTomatoVisuals();
            KitchenAuthoring.EditPrefab(RecipePrefabs + "PREF_Cooking_FryingPan.prefab", root => ConfigureRecipePan(root, profile));
            AssetDatabase.SaveAssets();
            // Reload saved instances. The scene's authored positions, rig and
            // baked environment are not rebuilt by a visual-only operation.
            var scene = EditorSceneManager.OpenScene(ScenePath);
            ValidateGeneratedFoodVisuals(scene.GetRootGameObjects().Single(go => go.name == "PREF_Cooking_Workbench"));
            Debug.Log("GENERATED_FOOD_VISUALS_READY " + ScenePath);
        }

        static void PrepareGeneratedFoodVisuals()
        {
            foreach (var name in new[] { "tomato_slice", "scrambled_egg" })
            {
                var prefab = KitchenAuthoring.Prefabs + "Generated/" + name + "_v001/PREF_Prop_" + name + ".prefab";
                if (!File.Exists(prefab)) KitchenPreparedAssetImporter.Import("ArtSource/KitchenAssets/" + name + "/v001/prepared/unity_asset.json");
            }
            KitchenAuthoring.EnsureFolder(FoodArt.TrimEnd('/'));
            KitchenAuthoring.EnsureFolder(FoodArt + "Textures");
            foreach (var file in Directory.GetFiles(FoodSource, "*.fbx")) File.Copy(file, FoodArt + Path.GetFileName(file), true);
            File.Copy("ArtSource/KitchenAssets/References/tomato_slice_single_v001.png", FoodArt + "Textures/tomato_flesh.png", true);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var file in Directory.GetFiles(FoodArt, "*.fbx")) KitchenPreparedAssetImporter.ConfigureModel(file);
            KitchenPreparedAssetImporter.ConfigureTextures(FoodArt.TrimEnd('/'));
            FoodMaterial("TomatoGenerated", "tomato_slice");
            FoodMaterial("EggGenerated", "scrambled_egg");
            var interior = StableFoodMaterial("TomatoFleshDetailed");
            interior.color = Color.white;
            interior.mainTexture = KitchenAuthoring.LoadRequired<Texture2D>(FoodArt + "Textures/tomato_flesh.png");
            interior.SetFloat("_Metallic", 0); interior.SetFloat("_Glossiness", .44f);
            EditorUtility.SetDirty(interior);
        }

        static Material StableFoodMaterial(string name)
        {
            var path = FoodArt + "MAT_Food_" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Standard")) { name = "MAT_Food_" + name };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static Material FoodMaterial(string name, string asset)
        {
            var material = StableFoodMaterial(name);
            var folder = KitchenAuthoring.Root + "Art/Generated/" + asset + "_v001";
            material.color = Color.white;
            KitchenPreparedAssetImporter.SetMap(material, "_MainTex", "basecolor.png", folder);
            KitchenPreparedAssetImporter.SetMap(material, "_MetallicGlossMap", "metallic_smoothness.png", folder);
            KitchenPreparedAssetImporter.SetMap(material, "_BumpMap", "normal.png", folder);
            KitchenPreparedAssetImporter.SetMap(material, "_OcclusionMap", "ao.png", folder);
            material.SetFloat("_GlossMapScale", 1); material.SetFloat("_BumpScale", .7f);
            material.EnableKeyword("_METALLICGLOSSMAP"); material.EnableKeyword("_NORMALMAP");
            EditorUtility.SetDirty(material);
            return material;
        }

        static void ConfigureGeneratedTomatoVisuals() => KitchenAuthoring.EditPrefab(RecipeTomato, root =>
        {
            var source = KitchenAuthoring.LoadRequired<GameObject>(FoodArt + "tomato_detailed_visuals.fbx");
            var meshes = source.GetComponentsInChildren<MeshFilter>(true).ToDictionary(m => m.name);
            foreach (var food in root.GetComponentsInChildren<CookableIngredient>(true))
            {
                if (food.CutForm == KitchenCutForm.Whole) continue;
                var visual = food.transform.Find("Intact").GetComponentsInChildren<MeshFilter>(true)
                    .Single(m => m.name.StartsWith("VIS_", StringComparison.Ordinal));
                var generated = meshes[visual.name.Replace("(Clone)", "")];
                // Preserve the actor, hierarchy, collider meshes and serialized
                // action references. Both FBXs share the same native axis bridge.
                visual.sharedMesh = generated.sharedMesh;
                var materials = generated.GetComponent<MeshRenderer>().sharedMaterials;
                visual.GetComponent<MeshRenderer>().sharedMaterials = materials.Select(m =>
                    StableFoodMaterial(m.name.StartsWith("MAT_Interior_", StringComparison.Ordinal) ? "TomatoFleshDetailed" : "TomatoGenerated")).ToArray();
                // The project's FBX bridge mirrors Blender X into Unity -X.
                food.ConfigurePresentationUp(Vector3.left);
            }
        });

        static Transform[] CreateGeneratedEggCurds(Transform parent)
        {
            var model = KitchenAuthoring.LoadRequired<GameObject>(FoodArt + "egg_curd_detailed.fbx");
            var material = StableFoodMaterial("EggGenerated");
            var curds = new Transform[12];
            for (var i = 0; i < curds.Length; i++)
            {
                var curd = Child(parent, "VIS_CookedEgg_" + i.ToString("00"));
                KitchenPreparedAssetImporter.CopyMesh(model, "VIS_EggCurd_Detailed", curd);
                KitchenPreparedAssetImporter.CopyMesh(model, "VIS_EggCurd_LOD1", curd);
                var renderers = curd.GetComponentsInChildren<MeshRenderer>(true);
                foreach (var renderer in renderers) renderer.sharedMaterial = material;
                var lod = curd.gameObject.AddComponent<LODGroup>();
                lod.SetLODs(new[] { new LOD(.045f, new Renderer[] { renderers[0] }), new LOD(.008f, new Renderer[] { renderers[1] }) });
                lod.RecalculateBounds();
                curd.localScale = new Vector3(.82f + (i % 3) * .11f, .72f + (i % 4) * .08f, .84f + (i % 2) * .12f);
                curds[i] = curd; curd.gameObject.SetActive(false);
            }
            return curds;
        }

        static void ValidateGeneratedFoodVisuals(GameObject workbench)
        {
            var foods = workbench.GetComponentsInChildren<CookableIngredient>(true)
                .Where(f => f.IngredientKind == KitchenIngredientKind.Tomato && f.CutForm != KitchenCutForm.Whole).ToArray();
            if (foods.Length != 20) throw new InvalidDataException("Expected 4 seeded slices and 16 derived diced actors.");
            foreach (var food in foods)
            {
                var renderer = food.transform.Find("Intact").GetComponentsInChildren<MeshRenderer>(true)
                    .Single(r => r.name.StartsWith("VIS_", StringComparison.Ordinal));
                if (renderer.sharedMaterials.Any(m => m == null || m.mainTexture == null))
                    throw new InvalidDataException("Generated tomato material lost its texture: " + food.name);
                if (!AssetDatabase.GetAssetPath(renderer.GetComponent<MeshFilter>().sharedMesh).StartsWith(FoodArt, StringComparison.Ordinal))
                    throw new InvalidDataException("Tomato is still using the old plain cut mesh.");
            }
            var pan = workbench.GetComponentInChildren<KitchenPanContents>(true);
            var lods = pan.GetComponentsInChildren<LODGroup>(true);
            if (lods.Length != 12 || lods.Any(l => l.GetLODs().Length != 2))
                throw new InvalidDataException("Generated egg curds require both near and far meshes.");
            Debug.Log("GENERATED_FOOD_REFERENCES_VERIFIED tomatoActors=" + foods.Length + " eggCurds=" + lods.Length);
        }
    }
}
