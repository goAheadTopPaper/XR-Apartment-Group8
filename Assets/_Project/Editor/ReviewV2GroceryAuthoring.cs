using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using XRApartment.Runtime;
using XRApartment.Runtime.ReviewV2;

namespace XRApartment.EditorTools
{
    // V2-only authoring. Inspect does not save or rebuild the scene.
    public static class ReviewV2GroceryAuthoring
    {
        public static void ConfigureModelIdentity()
        {
            if (Application.isPlaying) throw new System.InvalidOperationException("Requires Edit mode");
            var scene = EditorSceneManager.OpenScene(ReviewV2Authoring.ScenePath);
            var host = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ReviewSessionHost>(true)).Single();
            if (host.groceries == null) throw new System.InvalidOperationException("Expected the authored grocery prototype model");
            host.modelVersion = "cooking-m1-groceries-v1";
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Debug.Log("REVIEW_V2_MODEL_IDENTITY: explicit scene version independent of goal availability");
        }
        public static void ApplyRecipePolicy()
        {
            if (Application.isPlaying) throw new System.InvalidOperationException("Requires Edit mode");
            var scene = EditorSceneManager.OpenScene(ReviewV2Authoring.ScenePath);
            var recipe = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<KitchenRecipeController>(true)).Single();
            var data = new SerializedObject(recipe); data.FindProperty("requireFridgeOpening").boolValue = false;
            data.ApplyModifiedPropertiesWithoutUndo(); PrefabUtility.RecordPrefabInstancePropertyModifications(recipe);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Debug.Log("REVIEW_V2_RECIPE_POLICY: fridge opening not required; no synthetic action evidence");
        }
        [MenuItem("XR Apartment/Review V2/Configure Grocery Activity")]
        public static void Configure()
        {
            if (Application.isPlaying) throw new System.InvalidOperationException("Requires Edit mode");
            var scene = EditorSceneManager.OpenScene(ReviewV2Authoring.ScenePath);
            var host = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ReviewSessionHost>(true)).Single();
            if (host.groceries != null) throw new System.InvalidOperationException("Already configured; inspect existing scene overrides before editing");
            const string folder = "Assets/_Project/Prefabs/ReviewV2";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/_Project/Prefabs", "ReviewV2");
            const string bagPath = folder + "/PREF_ReviewV2_GroceryBag.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(bagPath) != null) throw new System.InvalidOperationException("Existing bag resource must be reviewed");
            var material = new Material(Shader.Find("HDRP/Lit")) { name = "MAT_ReviewV2_GroceryBag" };
            material.SetColor("_BaseColor", new Color(.53f, .32f, .16f));
            AssetDatabase.CreateAsset(material, folder + "/MAT_ReviewV2_GroceryBag.mat");
            var bag = new GameObject("PREF_ReviewV2_GroceryBag");
            void Box(string name, Vector3 pos, Vector3 scale)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
                go.transform.SetParent(bag.transform, false); go.transform.localPosition = pos; go.transform.localScale = scale;
                go.GetComponent<Renderer>().sharedMaterial = material;
            }
            Box("BagBase", Vector3.up * .01f, new Vector3(.58f, .02f, .38f));
            Box("BagSideLeft", new Vector3(-.28f,.055f,0), new Vector3(.02f,.09f,.38f));
            Box("BagSideRight", new Vector3(.28f,.055f,0), new Vector3(.02f,.09f,.38f));
            Box("BagFront", new Vector3(0,.055f,-.18f), new Vector3(.54f,.09f,.02f));
            Box("BagBack", new Vector3(0,.055f,.18f), new Vector3(.54f,.09f,.02f));
            foreach (float z in new[] { -.18f, .18f })
            {
                Box("Handle", new Vector3(0,.26f,z), new Vector3(.25f,.018f,.018f));
                foreach (float x in new[] { -.12f,.12f }) Box("HandleUpright", new Vector3(x,.18f,z), new Vector3(.018f,.16f,.018f));
            }
            bag.AddComponent<ReviewSceneIdentity>().Configure("GROCERY_BAG");
            PrefabUtility.SaveAsPrefabAsset(bag, bagPath); Object.DestroyImmediate(bag);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(bagPath), scene);
            var table = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Collider>(true)).Single(c => c.name == "COL_Tabletop");
            instance.transform.position = new Vector3(table.bounds.center.x, table.bounds.max.y + .002f, table.bounds.center.z);
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
            var zone = new GameObject("ZONE_FridgeStorage"); zone.transform.SetParent(host.station.ActiveFridge.transform, false);
            var box = zone.AddComponent<BoxCollider>(); box.isTrigger = true; box.center = new Vector3(0,1.11f,0); box.size = new Vector3(.54f,1.10f,.57f);
            var zoneId = zone.AddComponent<ReviewSceneIdentity>(); zoneId.Configure("ZONE_FRIDGE_STORAGE");
            var activity = host.gameObject.AddComponent<ReviewGroceryActivity>(); activity.host = host;
            activity.storageZone = box; activity.zoneIdentity = zoneId;
            var recipe = new SerializedObject(host.recipe);
            recipe.FindProperty("requireFridgeOpening").boolValue = false;
            recipe.ApplyModifiedPropertiesWithoutUndo(); PrefabUtility.RecordPrefabInstancePropertyModifications(host.recipe);
            var items = new System.Collections.Generic.List<Component> { recipe.FindProperty("tomato").objectReferenceValue as Component };
            var eggs = recipe.FindProperty("eggs");
            for (int i = 0; i < eggs.arraySize; i++) items.Add(eggs.GetArrayElementAtIndex(i).objectReferenceValue as Component);
            activity.groceries = items.Select(c => c.GetComponent<XRGrabInteractable>()).ToArray();
            for (int i = 0; i < items.Count; i++)
            {
                var grab = activity.groceries[i]; var cols = grab.colliders.Where(c => c != null && c.enabled && !c.isTrigger).ToArray();
                var bounds = cols[0].bounds; foreach (var col in cols) bounds.Encapsulate(col.bounds);
                var p = instance.transform.position + new Vector3(i == 0 ? -.12f : .10f, 0, i == 0 ? 0 : (i - 2) * .095f);
                p.y += .025f + (grab.transform.position.y - bounds.min.y);
                grab.transform.position = p; PrefabUtility.RecordPrefabInstancePropertyModifications(grab.transform);
            }
            host.groceries = activity;
            host.modelVersion = "cooking-m1-groceries-v1";
            host.identities = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ReviewSceneIdentity>(true)).ToArray();
            ReviewV2Authoring.Validate(scene); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            File.WriteAllLines("Docs/ReviewV2_M1_Identities.tsv", host.identities.Select(i => i.Id + "\t" + i.name));
            Debug.Log("REVIEW_V2_GROCERY_CONFIGURED: same recipe objects, V2 instance overrides only");
        }
        public static void Inspect()
        {
            var scene = EditorSceneManager.OpenScene(ReviewV2Authoring.ScenePath);
            var roots = scene.GetRootGameObjects();
            var host = roots.SelectMany(r => r.GetComponentsInChildren<ReviewSessionHost>(true)).Single();
            var recipe = new SerializedObject(host.recipe);
            var tomato = recipe.FindProperty("tomato").objectReferenceValue as Component;
            var eggs = recipe.FindProperty("eggs");
            var lines = new System.Collections.Generic.List<string>();
            void Item(Component item)
            {
                var id = item.GetComponent<ReviewSceneIdentity>();
                lines.Add($"ITEM {id?.Id} {item.name} position={item.transform.position:F4} " +
                    $"parent={item.transform.parent?.name}");
            }
            Item(tomato);
            for (int i = 0; i < eggs.arraySize; i++) Item(eggs.GetArrayElementAtIndex(i).objectReferenceValue as Component);
            foreach (var collider in roots.SelectMany(r => r.GetComponentsInChildren<Collider>(true))
                .Where(c => c.name == "COL_Tabletop" || c.transform.IsChildOf(host.station.ActiveFridge.transform)))
                lines.Add($"COL {collider.name} centre={collider.bounds.center:F4} size={collider.bounds.size:F4}");
            var fridge = new SerializedObject(host.station.ActiveFridge);
            lines.Add("FRIDGE position=" + host.station.ActiveFridge.transform.position.ToString("F4") +
                " bounds=" + fridge.FindProperty("storageBounds").boundsValue);
            Directory.CreateDirectory("Logs/ReviewV2");
            File.WriteAllLines("Logs/ReviewV2/grocery-inspection.txt", lines);
            Debug.Log("REVIEW_V2_GROCERY_INSPECTED: scene not saved");
        }
    }
}
