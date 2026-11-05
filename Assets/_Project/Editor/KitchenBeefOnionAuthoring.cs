using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using XRApartment.Data;
using XRApartment.Runtime;

namespace XRApartment.EditorTools
{
    public static partial class KitchenCookingAuthoring
    {
        private const string OnionPrefab = RecipePrefabs + "PREF_Cooking_Onion.prefab";
        private const float OnionGrams = 80f;
        private const float ServingRadius = .082f;

        private static void BuildOnionStockPrefab()
        {
            var cooking = KitchenAuthoring.LoadRequired<KitchenCookingProfile>(RecipeProfilePath);
            var cutting = KitchenAuthoring.LoadRequired<PreparedPropSettings>(BeefPreparation);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(OnionPrefab) == null &&
                !AssetDatabase.CopyAsset(KitchenAuthoring.Prefabs + "Props/PREF_Prop_Onion.prefab", OnionPrefab))
                throw new InvalidOperationException("Could not create the cooking onion.");

            var pivot = new Vector3(.013f, 0f, .013f);
            var mesh = SaveMesh("MESH_OnionQuarter", MakeOnionQuarter(pivot));
            var skin = Material("OnionSkin", new Color(.46f, .18f, .30f), 0f, .3f);
            var flesh = Material("OnionFlesh", new Color(.92f, .80f, .76f), 0f, .31f);
            var ring = Material("OnionRing", new Color(.65f, .36f, .49f), 0f, .31f);
            KitchenAuthoring.EditPrefab(OnionPrefab, root =>
            {
                var intact = root.transform.Find("Intact") ?? root.transform.Find("VIS_Onion");
                intact.name = "Intact";
                var old = root.transform.Find("DerivedPieces");
                if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
                var fragments = Child(root.transform, "DerivedPieces");
                var bodies = new Rigidbody[4];
                var pieces = new CookableIngredient[bodies.Length];
                for (var index = 0; index < pieces.Length; index++)
                {
                    var piece = Child(fragments, "Quarter_" + index);
                    piece.localRotation = Quaternion.Euler(0, 90f * index, 0);
                    piece.localPosition = piece.localRotation * pivot;
                    var visual = Child(piece, "Intact").gameObject;
                    visual.AddComponent<MeshFilter>().sharedMesh = mesh;
                    visual.AddComponent<MeshRenderer>().sharedMaterials = new[] { skin, flesh, ring };
                    var collider = visual.AddComponent<MeshCollider>();
                    collider.sharedMesh = mesh;
                    collider.convex = true;
                    bodies[index] = ConfigureIngredientBody(piece.gameObject, OnionGrams / pieces.Length);
                    bodies[index].isKinematic = true;
                    bodies[index].useGravity = false;
                    pieces[index] = piece.gameObject.AddComponent<CookableIngredient>();
                    pieces[index].Configure(cooking, Array.Empty<CookableIngredient>());
                    pieces[index].ConfigureIdentity(KitchenIngredientKind.Onion, KitchenCutForm.Slice, OnionGrams / pieces.Length);
                    pieces[index].ConfigurePresentationUp(Vector3.up);
                }
                root.GetComponent<Rigidbody>().mass = OnionGrams / 1000f;
                GetOrAdd<PreparedKitchenProp>(root).Configure(cutting, PreparedPropKind.Slice, intact, fragments, bodies);
                var onion = GetOrAdd<CookableIngredient>(root);
                onion.Configure(cooking, pieces);
                onion.ConfigureIdentity(KitchenIngredientKind.Onion, KitchenCutForm.Whole, OnionGrams);
                ConfigureSplitFeedback(root, "onion");
                fragments.gameObject.SetActive(false);
            });
        }

        private static Mesh MakeOnionQuarter(Vector3 pivot)
        {
            const int segments = 6;
            var heights = new[] { 0f, .012f, .023f, .032f, .037f, .041f };
            var radii = new[] { .014f, .029f, .035f, .033f, .028f, .019f };
            var positions = new List<Vector3>();
            var skin = new List<int>();
            var flesh = new List<int>();
            var rings = new List<int>();
            Vector3 Arc(int level, int segment)
            {
                var angle = segment * Mathf.PI / (2 * segments);
                return new Vector3(Mathf.Cos(angle) * radii[level], heights[level], Mathf.Sin(angle) * radii[level]);
            }
            void Quad(List<int> faces, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                var start = positions.Count;
                positions.AddRange(new[] { a - pivot, b - pivot, c - pivot, d - pivot });
                faces.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }
            void Triangle(Vector3 a, Vector3 b, Vector3 c)
            {
                var start = positions.Count;
                positions.AddRange(new[] { a - pivot, b - pivot, c - pivot });
                flesh.AddRange(new[] { start, start + 1, start + 2 });
            }
            for (var level = 0; level < heights.Length - 1; level++)
            {
                for (var segment = 0; segment < segments; segment++)
                    Quad(skin, Arc(level, segment), Arc(level + 1, segment), Arc(level + 1, segment + 1), Arc(level, segment + 1));
                var centerLow = Vector3.up * heights[level];
                var centerHigh = Vector3.up * heights[level + 1];
                var cutMaterial = level == 1 || level == 3 ? rings : flesh;
                Quad(cutMaterial, centerLow, centerHigh, Arc(level + 1, 0), Arc(level, 0));
                Quad(cutMaterial, centerLow, Arc(level, segments), Arc(level + 1, segments), centerHigh);
            }
            for (var segment = 0; segment < segments; segment++)
            {
                var top = heights.Length - 1;
                Triangle(Vector3.zero, Arc(0, segment), Arc(0, segment + 1));
                var topCenter = Vector3.up * heights[top];
                Triangle(topCenter, Arc(top, segment + 1), Arc(top, segment));
            }
            var mesh = new Mesh();
            mesh.SetVertices(positions);
            mesh.subMeshCount = 3;
            mesh.SetTriangles(skin, 0);
            mesh.SetTriangles(flesh, 1);
            mesh.SetTriangles(rings, 2);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void ConfigureBeefOnionWorkbench(GameObject root)
        {
            var button = root.transform.Find("ACT_RECIPE");
            var control = button == null
                ? Button(root.transform, "RECIPE", new Vector3(-3.25f, 1.030f, 1.67f), new Color(.15f, .30f, .54f))
                : button.GetComponent<XRSimpleInteractable>();
            var plate = root.transform.Find("PREF_Prop_plate/Intact");
            if (plate == null) throw new InvalidOperationException("The serving plate is missing from the workbench.");
            root.GetComponent<KitchenRecipeController>().ConfigureBeefOnion(control,
                root.transform.Find("STOCK_Beef").GetComponent<CookableIngredient>(),
                root.transform.Find("STOCK_Onion").GetComponent<CookableIngredient>(),
                plate, ServingRadius);
            Parameter("cooking_serving_radius", ServingRadius);
        }
    }
}
