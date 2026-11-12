using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using XRApartment.Data;
using XRApartment.Runtime;

namespace XRApartment.EditorTools
{
    public static partial class KitchenCookingAuthoring
    {
        const string RecipeArt = Art + "RecipeModels/";
        const string RecipePrefabs = Prefabs + "Recipe/";
        const string RecipeProfilePath = KitchenAuthoring.Settings + "SET_TomatoEggCooking.asset";
        const string RecipeTomato = KitchenAuthoring.Prefabs + "Generated/tomato_v002/PREF_Prop_tomato.prefab";
        static readonly Dictionary<Material, Material> RecipeMaterials = new Dictionary<Material, Material>();
        static readonly Quaternion CounterLabelRotation = Quaternion.Euler(90f, 0f, 0f);

        static void BuildRecipeAssets()
        {
            PrepareGeneratedFoodVisuals();
            KitchenAuthoring.EnsureFolder(RecipeArt.TrimEnd('/'));
            KitchenAuthoring.EnsureFolder(RecipePrefabs.TrimEnd('/'));
            // The old procedural egg curds have been replaced by generated food visuals.
            foreach (var name in new[] { "fridge_body", "fridge_door", "spatula", "chopping_board" })
                File.Copy("ArtSource/KitchenAssets/Recipe/v001/" + name + ".fbx", RecipeArt + name + ".fbx", true);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var file in Directory.GetFiles(RecipeArt, "*.fbx")) KitchenPreparedAssetImporter.ConfigureModel(file);
            RecipeMaterials.Clear();
            var profile = AssetDatabase.LoadAssetAtPath<KitchenCookingProfile>(RecipeProfilePath);
            if (profile == null)
            {
                AssetDatabase.CopyAsset(ProfilePath, RecipeProfilePath);
                profile = KitchenAuthoring.LoadRequired<KitchenCookingProfile>(RecipeProfilePath);
                profile.burntSeconds = 90; EditorUtility.SetDirty(profile);
            }
            profile.cookedFoodTint = new Color(.96f, .89f, .80f, 1);
            EditorUtility.SetDirty(profile);
            if (!File.Exists(RecipeTomato))
                KitchenPreparedAssetImporter.Import("ArtSource/KitchenAssets/tomato/v002/prepared_recipe/unity_asset.json");
            ConfigureRecipeTomato(profile);
            ConfigureGeneratedTomatoVisuals();
            const string eggPath = RecipePrefabs + "PREF_Prop_egg.prefab";
            if (!File.Exists(eggPath)) AssetDatabase.CopyAsset(Generated("egg"), eggPath);
            KitchenAuthoring.EditPrefab(eggPath, root =>
            {
                var egg = root.GetComponent<KitchenEgg>(); egg.Configure(profile);
                var drop = root.transform.Find("FX_EggDrop"); if (drop == null) drop = Child(root.transform, "FX_EggDrop");
                var stream = GetOrAdd<LineRenderer>(drop.gameObject);
                stream.sharedMaterial = Material("EggStream", new Color(1, .67f, .1f), 0, .5f);
                stream.useWorldSpace = true; stream.numCapVertices = 3; stream.enabled = false;
                egg.ConfigureDropVisual(stream);
            });
            foreach (var name in new[] { "MixingBowl", "FryingPan" })
            {
                var path = RecipePrefabs + "PREF_Cooking_" + name + ".prefab";
                if (!File.Exists(path)) AssetDatabase.CopyAsset(Prefabs + "PREF_Cooking_" + name + ".prefab", path);
                KitchenAuthoring.EditPrefab(path, root =>
                {
                    var vessel = new SerializedObject(root.GetComponent<KitchenFoodVessel>());
                    vessel.FindProperty("profile").objectReferenceValue = profile; vessel.ApplyModifiedPropertiesWithoutUndo();
                    if (name == "FryingPan") ConfigureRecipePan(root, profile);
                });
            }
            BuildRecipeFridge(profile); BuildRecipeSpatula(); BuildRecipeBoard();
            BuildBeefStockPrefab();
            BuildOnionStockPrefab();
            BuildVessel("TomatoBowl", .09f, .008f, .065f, .004f, false, profile);
            KitchenAuthoring.EditPrefab(Prefabs + "PREF_Cooking_TomatoBowl.prefab", root =>
                ConfigurePrepTray(root, false, .09f, .09f, .008f, .085f));
            Parameter("cooking_fridge_width", .72f); Parameter("cooking_fridge_depth", .66f);
            Parameter("cooking_fridge_height", 1.81f); Parameter("cooking_fridge_shelf_lower", 1.009f);
            Parameter("cooking_fridge_shelf_upper", 1.329f); Parameter("cooking_spatula_length", .324f);
            Parameter("cooking_recipe_board_width", .42f); Parameter("cooking_recipe_board_depth", .34f);
        }

        static void ConfigureRecipeTomato(KitchenCookingProfile profile) => KitchenAuthoring.EditPrefab(RecipeTomato, root =>
        {
            var descriptor = JsonUtility.FromJson<KitchenPreparedAssetImporter.Manifest>(
                File.ReadAllText(KitchenAuthoring.Root + "Art/Generated/tomato_v002/unity_asset.json"));
            var totalVolume = descriptor.pieces.Sum(p => p.volume_m3);
            var slices = root.transform.Find("DerivedPieces");
            var sliceFoods = new CookableIngredient[slices.childCount];
            for (var i = 0; i < slices.childCount; i++)
            {
                var slice = slices.GetChild(i); var info = descriptor.pieces[i];
                var dice = slice.Find("DerivedPieces"); var dicedFoods = new CookableIngredient[dice.childCount];
                for (var j = 0; j < dice.childCount; j++)
                {
                    var food = GetOrAdd<CookableIngredient>(dice.GetChild(j).gameObject);
                    food.Configure(profile, Array.Empty<CookableIngredient>());
                    food.ConfigureIdentity(KitchenIngredientKind.Tomato, KitchenCutForm.Diced,
                        profile.tomatoGrams * info.children[j].volume_m3 / totalVolume);
                    dicedFoods[j] = food;
                }
                var sliceFood = GetOrAdd<CookableIngredient>(slice.gameObject); sliceFood.Configure(profile, dicedFoods);
                sliceFood.ConfigureIdentity(KitchenIngredientKind.Tomato, KitchenCutForm.Slice,
                    profile.tomatoGrams * info.volume_m3 / totalVolume); sliceFoods[i] = sliceFood;
            }
            var whole = GetOrAdd<CookableIngredient>(root); whole.Configure(profile, sliceFoods);
            whole.ConfigureIdentity(KitchenIngredientKind.Tomato, KitchenCutForm.Whole, profile.tomatoGrams);
            foreach (var grab in root.GetComponentsInChildren<XRGrabInteractable>(true))
            {
                KitchenGrabAuthoring.ConfigureProp(grab.gameObject);
                var food = grab.GetComponent<CookableIngredient>(); var body = grab.GetComponent<Rigidbody>();
                body.mass = Mathf.Max(.001f, food.Grams / 1000f);
                body.drag = profile.foodLinearDamping; body.angularDrag = profile.foodAngularDamping;
                body.maxDepenetrationVelocity = .35f; body.solverIterations = 10; body.solverVelocityIterations = 4;
                foreach (var collider in grab.colliders) collider.contactOffset = profile.foodContactOffset;
            }
            foreach (var prepared in root.GetComponentsInChildren<PreparedKitchenProp>(true))
            {
                var data = new SerializedObject(prepared);
                var tuning = (PreparedPropSettings)data.FindProperty("settings").objectReferenceValue;
                tuning.separationSpeed = profile.cutSeparationSpeed; EditorUtility.SetDirty(tuning);
            }
        });

        static void ConfigureRecipePan(GameObject root, KitchenCookingProfile profile)
        {
            var old = root.transform.Find("VIS_ScrambledEgg");
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var eggRoot = Child(root.transform, "VIS_ScrambledEgg");
            var curds = CreateGeneratedEggCurds(eggRoot);
            GetOrAdd<KitchenPanContents>(root).Configure(profile, curds, true);
            var trigger = root.transform.Find("ACT_PanMouth");
            if (trigger == null) trigger = Child(root.transform, "ACT_PanMouth");
            var collider = GetOrAdd<BoxCollider>(trigger.gameObject); collider.isTrigger = true;
            collider.center = Vector3.up * .042f; collider.size = new Vector3(.198f, .09f, .198f);
        }

        static GameObject RecipeModel(Transform parent, string filename, bool colliders)
        {
            var source = KitchenAuthoring.LoadRequired<GameObject>(RecipeArt + filename + ".fbx");
            var model = UnityEngine.Object.Instantiate(source, parent); model.name = "VIS_" + filename;
            model.transform.localPosition = Vector3.zero; model.transform.localRotation = Quaternion.identity;
            AdaptRecipeMaterials(model);
            if (colliders)
                foreach (var mesh in model.GetComponentsInChildren<MeshFilter>(true))
                {
                    var collider = mesh.gameObject.AddComponent<BoxCollider>();
                    collider.center = mesh.sharedMesh.bounds.center; collider.size = mesh.sharedMesh.bounds.size;
                }
            return model;
        }

        static void AdaptRecipeMaterials(GameObject root)
        {
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var values = renderer.sharedMaterials;
                for (var i = 0; i < values.Length; i++)
                {
                    var original = values[i]; if (original == null) continue;
                    if (!RecipeMaterials.TryGetValue(original, out var material))
                    {
                        var name = original.name.Replace(" ", "_").Replace("/", "_");
                        var path = RecipeArt + name + ".mat";
                        material = AssetDatabase.LoadAssetAtPath<Material>(path);
                        if (material == null)
                        {
                            material = new Material(original) { shader = Shader.Find("Standard"), name = name };
                            AssetDatabase.CreateAsset(material, path);
                            KitchenHdrpMaterials.UpgradeStandard(material);
                        }
                        RecipeMaterials.Add(original, material);
                    }
                    values[i] = material;
                }
                renderer.sharedMaterials = values;
            }
        }

        static void BuildRecipeFridge(KitchenCookingProfile profile)
        {
            var root = new GameObject("PREF_Cooking_Fridge");
            try
            {
                RecipeModel(root.transform, "fridge_body", true);
                // The FBX bridge mirrors Blender X, so this leaf extends -X
                // from its right hinge in Unity. No negative physics scale.
                var hinge = Child(root.transform, "ACT_FridgeDoor"); hinge.localPosition = new Vector3(.345f, .04f, .37f);
                RecipeModel(hinge, "fridge_door", true);
                var interactable = hinge.gameObject.AddComponent<XRSimpleInteractable>();
                interactable.colliders.Clear(); interactable.colliders.AddRange(hinge.GetComponentsInChildren<Collider>());
                var probe = Child(hinge, "COL_DoorSweepProbe").gameObject.AddComponent<BoxCollider>();
                probe.center = new Vector3(-.345f, .88f, .035f); probe.size = new Vector3(.69f, 1.73f, .135f); probe.enabled = false;
                var door = hinge.gameObject.AddComponent<KinematicSwingDoor>();
                var data = new SerializedObject(door);
                data.FindProperty("openAngleDegrees").floatValue = 105;
                data.FindProperty("swingSign").floatValue = 1;
                data.FindProperty("sweepProbe").objectReferenceValue = probe;
                data.FindProperty("collisionProfile").objectReferenceValue = KitchenAuthoring.LoadRequired<HandInteractionProfile>(KitchenAuthoring.Settings + "SET_HandInteraction.asset");
                data.ApplyModifiedPropertiesWithoutUndo();
                var lamp = Child(root.transform, "LGT_FridgeInterior"); lamp.localPosition = new Vector3(0, 1.68f, .10f);
                var light = lamp.gameObject.AddComponent<Light>(); light.type = LightType.Point; light.range = 1.5f; light.intensity = .55f;
                KitchenHdrpAuthoring.ConfigureLight(light);
                light.color = new Color(1, .94f, .8f); light.shadows = LightShadows.None;
                var fridge = root.AddComponent<KitchenFridge>(); fridge.Configure(door, profile,
                    new Bounds(new Vector3(0, .94f, 0), new Vector3(.66f, 1.7f, .69f))); fridge.ConfigureLight(light);
                PrefabUtility.SaveAsPrefabAsset(root, RecipePrefabs + root.name + ".prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        static void BuildRecipeSpatula()
        {
            var root = new GameObject("PREF_Cooking_Spatula");
            try
            {
                RecipeModel(root.transform, "spatula", false);
                var head = root.AddComponent<BoxCollider>(); head.center = Vector3.up * .003f; head.size = new Vector3(.066f, .006f, .083f);
                var handle = Child(root.transform, "COL_SpatulaHandle").gameObject.AddComponent<BoxCollider>();
                handle.center = new Vector3(0, .02f, .18f); handle.size = new Vector3(.026f, .023f, .22f);
                root.AddComponent<Rigidbody>().mass = .12f; var grab = root.AddComponent<XRGrabInteractable>(); KitchenGrabAuthoring.ConfigureProp(root);
                var tip = Child(root.transform, "StirTip"); tip.localPosition = Vector3.up * .006f;
                root.AddComponent<KitchenSpatula>().Configure(grab, tip, null);
                PrefabUtility.SaveAsPrefabAsset(root, RecipePrefabs + root.name + ".prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        static void BuildRecipeBoard()
        {
            var root = new GameObject("PREF_Cooking_ChoppingBoard");
            try
            {
                RecipeModel(root.transform, "chopping_board", false);
                var collider = root.AddComponent<BoxCollider>(); collider.center = Vector3.up * .011f; collider.size = new Vector3(.42f, .022f, .34f);
                root.AddComponent<Rigidbody>().mass = .45f; root.AddComponent<XRGrabInteractable>(); KitchenGrabAuthoring.ConfigureProp(root);
                ConfigurePrepTray(root, true, .21f, .17f, .022f, .1f);
                PrefabUtility.SaveAsPrefabAsset(root, RecipePrefabs + root.name + ".prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        static void ConfigurePrepTray(GameObject root, bool rectangle, float width, float depth, float bottom, float height)
        {
            GetOrAdd<KitchenPrepTray>(root).Configure(rectangle, width, depth, bottom, height);
            var mouth = root.transform.Find("ACT_PrepTray"); if (mouth == null) mouth = Child(root.transform, "ACT_PrepTray");
            var trigger = GetOrAdd<BoxCollider>(mouth.gameObject); trigger.isTrigger = true;
            trigger.center = Vector3.up * (bottom + height / 2); trigger.size = new Vector3(width * 2, height, depth * 2);
        }

        static void BuildRecipeWorkbench()
        {
            var root = new GameObject("PREF_Cooking_Workbench");
            try
            {
                const float table = 1.005f;
                var targets = new List<Transform>(); var profile = KitchenAuthoring.LoadRequired<KitchenCookingProfile>(RecipeProfilePath);
                var bowl = Place(root.transform, RecipePrefabs + "PREF_Cooking_MixingBowl.prefab", new Vector3(-3.20f, table, 1.86f), targets).GetComponent<KitchenFoodVessel>();
                var pan = Place(root.transform, RecipePrefabs + "PREF_Cooking_FryingPan.prefab", new Vector3(-4.22f, table, 1.88f), targets).GetComponent<KitchenFoodVessel>();
                Place(root.transform, Prefabs + "PREF_Cooking_TomatoBowl.prefab", new Vector3(-3.20f, table, 2.13f), targets);
                var whisk = Place(root.transform, Prefabs + "PREF_Cooking_Whisk.prefab", new Vector3(-3.80f, table + .015f, 2.22f), targets);
                whisk.transform.rotation = Quaternion.Euler(90, 90, 0);
                var spatula = Place(root.transform, RecipePrefabs + "PREF_Cooking_Spatula.prefab", new Vector3(-3.02f, table + .015f, 2.26f), targets).GetComponent<KitchenSpatula>();
                spatula.transform.rotation = Quaternion.Euler(0, 90, 0);
                spatula.SetPan(pan.GetComponent<KitchenPanContents>());
                Place(root.transform, RecipePrefabs + "PREF_Cooking_ChoppingBoard.prefab", new Vector3(-3.68f, table, 1.92f), targets);
                Place(root.transform, Generated("knife"), new Vector3(-3.68f, table + .03f, 1.92f), targets);
                Place(root.transform, Generated("plate"), new Vector3(-4.22f, table, 2.23f), targets);
                var ladle = Place(root.transform, Prefabs + "PREF_Cooking_Ladle.prefab", new Vector3(-3.28f, table + .025f, 2.31f), targets);
                ladle.transform.rotation = Quaternion.Euler(0, -90, 0);
                Parameter("cooking_counter_height", 1f); Parameter("cooking_counter_depth", .65f);
                Parameter("cooking_side_counter_height", 1.11f);
                Parameter("cooking_board_x", -3.68f); Parameter("cooking_board_z", 1.92f);
                Parameter("cooking_burner_x", -2.66f); Parameter("cooking_burner_z", 1.95f);
                // Keep the burner empty: placing the pan is an actual player action.
                var stove = BuildGasBurner(root.transform, new Vector3(-2.66f, table, 1.95f));
                var heat = stove.transform;
                var sound = heat.gameObject.AddComponent<AudioSource>(); sound.clip = KitchenAuthoring.LoadRequired<AudioClip>(Art + "SFX_sizzle.wav");
                sound.loop = true; sound.playOnAwake = false; sound.spatialBlend = 1; sound.minDistance = .2f; sound.maxDistance = 4; sound.volume = .35f;
                var steam = Particles(heat, "FX_FoodSteam", 12, .7f, .035f); var emission = steam.emission; emission.enabled = false;
                stove.ConfigureFeedback(sound, steam);
                var fridgeObject = (GameObject)PrefabUtility.InstantiatePrefab(KitchenAuthoring.LoadRequired<GameObject>(RecipePrefabs + "PREF_Cooking_Fridge.prefab"), root.transform);
                fridgeObject.transform.SetPositionAndRotation(CookingFridgeDock, CookingFridgeRotation);
                var fridge = fridgeObject.GetComponent<KitchenFridge>();
                var tomato = Stock(root.transform, fridge, RecipeTomato, KitchenStoredFood.Tomato, new Vector3(-.18f, 1.014f, .02f), targets).GetComponent<PreparedKitchenProp>();
                Stock(root.transform, fridge, Generated("carrot"), KitchenStoredFood.Other, new Vector3(.11f, 1.014f, .02f), targets);
                Stock(root.transform, fridge, Generated("lemon"), KitchenStoredFood.Other, new Vector3(.19f, 1.014f, -.16f), targets);
                Stock(root.transform, fridge, Generated("apple"), KitchenStoredFood.Other, new Vector3(-.19f, 1.334f, -.05f), targets);
                var eggs = new KitchenEgg[3];
                for (var i = 0; i < eggs.Length; i++)
                    eggs[i] = Stock(root.transform, fridge, RecipePrefabs + "PREF_Prop_egg.prefab", KitchenStoredFood.Egg, new Vector3(-.02f + .09f * i, 1.334f, .08f), targets).GetComponent<KitchenEgg>();
                AddPantryStock(root.transform, fridge, targets);
                var reset = Button(root.transform, "RESET", new Vector3(-4.22f, table + .025f, 1.68f), new Color(.1f, .28f, .3f));
                var flow = root.AddComponent<KitchenRecipeController>();
                var slices = tomato.transform.Find("DerivedPieces").Cast<Transform>().Select(t => t.GetComponent<PreparedKitchenProp>()).ToArray();
                flow.Configure(profile, fridge, tomato, slices, eggs, bowl, pan, stove);
                var station = root.AddComponent<KitchenCookingStation>(); station.Configure(targets.ToArray(), reset, bowl, pan, stove, null); station.ConfigureRecipe(flow, fridge);
                EnsureCompactFridge(root);
                ConfigureBeefOnionWorkbench(root);
                ConfigureRecipeGuides(root);
                PrefabUtility.SaveAsPrefabAsset(root, StationPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [MenuItem("XR Apartment/Cooking/Apply Recipe Guides")]
        public static void ApplyRecipeGuides() => KitchenAuthoring.EditPrefab(StationPath, ConfigureRecipeGuides);

        private static void ConfigureRecipeGuides(GameObject workbench)
        {
            var controller = workbench.GetComponent<KitchenRecipeController>();
            var station = new SerializedObject(workbench.GetComponent<KitchenCookingStation>());
            station.FindProperty("status").objectReferenceValue = null;
            station.ApplyModifiedPropertiesWithoutUndo();
            workbench.transform.Find("ACT_RESET/Label").localRotation = CounterLabelRotation;
            workbench.transform.Find("ACT_RECIPE/Label").localRotation = CounterLabelRotation;
            controller.SetFridgeHint(null);
            var worktopGuide = BuildRecipeGuide(workbench.transform, "UI_CookingInstructions",
                new Vector3(-3.60f, 1.57f, 2.20f), 740f);
            var fridge = workbench.GetComponent<KitchenCookingStation>().ActiveFridge.transform;
            var fridgeGuide = BuildRecipeGuide(workbench.transform, "UI_FridgeInstructions", fridge.position, 660f);
            PlaceFridgeGuide(fridgeGuide.heading.transform.parent, fridge);
            controller.ConfigureGuides(worktopGuide, fridgeGuide);
        }

        private static void PlaceFridgeGuide(Transform guide, Transform fridge)
        {
            guide.SetPositionAndRotation(fridge.TransformPoint(new Vector3(0f, 2.02f, .42f)),
                fridge.rotation * Quaternion.Euler(0f, 180f, 0f));
        }

        private static KitchenRecipeController.Guide BuildRecipeGuide(
            Transform workbench, string name, Vector3 position, float width)
        {
            var old = workbench.Find(name);
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var card = KitchenLocomotionAuthoring.Rect(name, workbench, 0f, 0f, width, 230f);
            card.SetPositionAndRotation(position, Quaternion.identity);
            card.localScale = Vector3.one * .001f;
            var canvas = card.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var background = card.gameObject.AddComponent<Image>();
            background.color = new Color(.045f, .077f, .10f, .98f);
            background.raycastTarget = false;
            var stripe = KitchenLocomotionAuthoring.Rect("Edge", card, -width * .5f + 5f, 0f, 10f, 230f)
                .gameObject.AddComponent<Image>();
            stripe.color = new Color(.25f, .71f, .72f);
            stripe.raycastTarget = false;
            var heading = KitchenLocomotionAuthoring.Label("Heading", card, 4f, 76f, width - 62f, 38f, 27, "RECIPE");
            var action = KitchenLocomotionAuthoring.Label("Action", card, 4f, 12f, width - 62f, 80f, 30, "");
            var detail = KitchenLocomotionAuthoring.Label("Detail", card, 4f, -72f, width - 62f, 50f, 23, "");
            heading.alignment = action.alignment = detail.alignment = TMPro.TextAlignmentOptions.MidlineLeft;
            return new KitchenRecipeController.Guide { heading = heading, action = action, detail = detail };
        }

        static GameObject Stock(Transform parent, KitchenFridge fridge, string prefab, KitchenStoredFood kind, Vector3 local, List<Transform> targets)
        {
            var item = Place(parent, prefab, fridge.transform.TransformPoint(local), targets);
            item.transform.rotation = fridge.transform.rotation;
            item.AddComponent<KitchenStoredIngredient>().Configure(fridge, kind);
            return item;
        }

        [MenuItem("XR Apartment/Cooking/Apply Pantry Stock")]
        public static void ApplyPantryStock()
        {
            KitchenAuthoring.RequireEditMode();
            BuildBeefStockPrefab();
            BuildOnionStockPrefab();
            KitchenAuthoring.EditPrefab(StationPath, root =>
            {
                var station = new SerializedObject(root.GetComponent<KitchenCookingStation>());
                var targets = station.FindProperty("resetTargets");
                var current = Enumerable.Range(0, targets.arraySize)
                    .Select(i => (Transform)targets.GetArrayElementAtIndex(i).objectReferenceValue).ToList();
                AddPantryStock(root.transform, root.GetComponentInChildren<KitchenFridge>(), current);
                targets.arraySize = current.Count;
                for (var index = 0; index < current.Count; index++)
                    targets.GetArrayElementAtIndex(index).objectReferenceValue = current[index];
                station.ApplyModifiedPropertiesWithoutUndo();
                ConfigureBeefOnionWorkbench(root);
            });
        }

        private static void AddPantryStock(Transform root, KitchenFridge fridge, List<Transform> targets)
        {
            Add("Banana", new Vector3(-.15f, .634f, .015f));
            var oldOnion = root.Find("STOCK_Onion");
            if (oldOnion != null && oldOnion.GetComponent<PreparedKitchenProp>() == null)
            {
                targets.Remove(oldOnion);
                UnityEngine.Object.DestroyImmediate(oldOnion.gameObject);
            }
            Add("Onion", new Vector3(.14f, .634f, .115f), OnionPrefab);
            Add("Cheese", new Vector3(.12f, .634f, -.12f));
            Add("Beef", new Vector3(.12f, 1.336f, -.15f), BeefPrefab);

            void Add(string name, Vector3 position, string prefab = null)
            {
                if (root.Find("STOCK_" + name) != null) return;
                if (prefab == null) prefab = KitchenAuthoring.Prefabs + "Props/PREF_Prop_" + name + ".prefab";
                var item = Stock(root, fridge, prefab, KitchenStoredFood.Other, position, targets);
                item.name = "STOCK_" + name;
            }
        }
    }
}
