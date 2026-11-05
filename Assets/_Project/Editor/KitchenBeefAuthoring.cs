using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using XRApartment.Data;
using XRApartment.Runtime;
using Object = UnityEngine.Object;

namespace XRApartment.EditorTools
{
    public static partial class KitchenCookingAuthoring
    {
        private const string BeefPrefab = RecipePrefabs + "PREF_Cooking_Beef.prefab";
        private const string BeefPreparation = RecipeArt + "SET_BeefPreparation.asset";
        private const float BeefHeight = .019f;
        private const float BeefGrams = 150f;
        private static readonly Vector2[] BeefOutline =
        {
            new Vector2(-.072f, -.016f), new Vector2(-.064f, -.032f), new Vector2(-.045f, -.040f),
            new Vector2(-.007f, -.037f), new Vector2(.034f, -.028f), new Vector2(.065f, -.015f),
            new Vector2(.072f, .004f), new Vector2(.059f, .023f), new Vector2(.031f, .040f),
            new Vector2(-.012f, .037f), new Vector2(-.050f, .026f), new Vector2(-.069f, .011f)
        };
        private static readonly Vector2[][] BeefMarbling =
        {
            new[] { new Vector2(-.050f, -.018f), new Vector2(-.031f, -.014f), new Vector2(-.006f, -.017f),
                new Vector2(.021f, -.010f), new Vector2(.039f, -.012f) },
            new[] { new Vector2(-.052f, .002f), new Vector2(-.028f, .011f), new Vector2(-.008f, .008f),
                new Vector2(.014f, .016f), new Vector2(.047f, .013f) },
            new[] { new Vector2(-.018f, .025f), new Vector2(.005f, .021f), new Vector2(.023f, .026f) }
        };

        private static void BuildBeefStockPrefab()
        {
            var cooking = KitchenAuthoring.LoadRequired<KitchenCookingProfile>(RecipeProfilePath);
            var cutting = AssetDatabase.LoadAssetAtPath<PreparedPropSettings>(BeefPreparation);
            if (cutting == null)
            {
                cutting = ScriptableObject.CreateInstance<PreparedPropSettings>();
                cutting.intactMass = BeefGrams / 1000f;
                cutting.separationSpeed = .028f;
                AssetDatabase.CreateAsset(cutting, BeefPreparation);
            }
            var lean = Material("BeefLean", new Color(.55f, .078f, .096f), 0f, .3f);
            var fat = Material("BeefFat", new Color(.86f, .65f, .54f), 0f, .26f);
            var root = new GameObject("PREF_Cooking_Beef");
            try
            {
                var bodies = new Rigidbody[4];
                var pieces = new CookableIngredient[bodies.Length];
                var fragments = Child(root.transform, "DerivedPieces");
                var cutArea = Area(BeefOutline);
                for (var index = 0; index < pieces.Length; index++)
                {
                    var left = Mathf.Lerp(-.073f, .073f, index / (float)pieces.Length);
                    var right = Mathf.Lerp(-.073f, .073f, (index + 1f) / pieces.Length);
                    var outline = ClipBeef(ClipBeef(BeefOutline, left, true), right, false);
                    var pivot = (left + right) * .5f;
                    var local = outline.Select(point => point - Vector2.right * pivot).ToArray();
                    var part = Child(fragments, "Piece_" + index);
                    part.localPosition = Vector3.right * pivot;
                    var mass = BeefGrams * Area(outline) / cutArea;
                    var visual = BuildBeefVisual(part, "MESH_BeefStrip_" + index, local, lean, fat,
                        left + .0018f, right - .0018f, pivot);
                    bodies[index] = ConfigureIngredientBody(part.gameObject, mass);
                    pieces[index] = part.gameObject.AddComponent<CookableIngredient>();
                    pieces[index].Configure(cooking, Array.Empty<CookableIngredient>());
                    pieces[index].ConfigureIdentity(KitchenIngredientKind.Beef, KitchenCutForm.Strip, mass);
                    pieces[index].ConfigurePresentationUp(visual.up);
                    bodies[index].isKinematic = true;
                    bodies[index].useGravity = false;
                }
                var intact = BuildBeefVisual(root.transform, "MESH_BeefWhole", BeefOutline, lean, fat,
                    -.072f, .072f, 0f);
                ConfigureIngredientBody(root, BeefGrams);
                var prepared = root.AddComponent<PreparedKitchenProp>();
                prepared.Configure(cutting, PreparedPropKind.Slice, intact, fragments, bodies);
                var beef = root.AddComponent<CookableIngredient>();
                beef.Configure(cooking, pieces);
                beef.ConfigureIdentity(KitchenIngredientKind.Beef, KitchenCutForm.Whole, BeefGrams);
                ConfigureSplitFeedback(root, "beef");
                fragments.gameObject.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root, BeefPrefab);
            }
            finally { Object.DestroyImmediate(root); }
            Parameter("cooking_beef_stock_width", .144f);
            Parameter("cooking_beef_stock_depth", .080f);
            Parameter("cooking_beef_stock_height", BeefHeight);
            AssetDatabase.SaveAssets();
        }

        private static Rigidbody ConfigureIngredientBody(GameObject root, float grams)
        {
            var body = root.AddComponent<Rigidbody>();
            body.mass = grams / 1000f;
            root.AddComponent<XRGrabInteractable>();
            KitchenGrabAuthoring.ConfigureProp(root);
            return body;
        }

        private static Transform BuildBeefVisual(Transform parent, string name, Vector2[] outline,
            Material lean, Material fat, float left, float right, float pivot)
        {
            var intact = Child(parent, "Intact");
            var mesh = SaveMesh(name, MakeBeefMesh(outline, left, right, pivot));
            intact.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            intact.gameObject.AddComponent<MeshRenderer>().sharedMaterials = new[] { lean, fat };
            var collider = intact.gameObject.AddComponent<BoxCollider>();
            collider.center = mesh.bounds.center;
            collider.size = mesh.bounds.size;
            return intact;
        }

        private static Mesh MakeBeefMesh(Vector2[] outline, float left, float right, float pivot)
        {
            var count = outline.Length;
            var center = outline.Aggregate(Vector2.zero, (sum, point) => sum + point) / count;
            var vertices = new List<Vector3>(count * 4 + 2);
            var lean = new List<int>();
            var fat = new List<int>();
            var heights = new[] { 0f, .003f, BeefHeight - .003f, BeefHeight };
            for (var ring = 0; ring < heights.Length; ring++)
                foreach (var point in outline)
                {
                    var flat = ring == 0 || ring == 3 ? Vector2.Lerp(point, center, .08f) : point;
                    vertices.Add(new Vector3(flat.x, heights[ring], flat.y));
                }
            var bottom = vertices.Count;
            vertices.Add(new Vector3(center.x, 0f, center.y));
            var top = vertices.Count;
            vertices.Add(new Vector3(center.x, BeefHeight, center.y));
            for (var index = 0; index < count; index++)
            {
                var next = (index + 1) % count;
                lean.AddRange(new[] { bottom, index, next, top, 3 * count + next, 3 * count + index });
                for (var ring = 0; ring < 3; ring++)
                {
                    var lower = ring * count;
                    var upper = lower + count;
                    var faces = outline[index].y + outline[next].y < -.051f && ring > 0 ? fat : lean;
                    faces.AddRange(new[] { lower + index, upper + index, upper + next,
                        lower + index, upper + next, lower + next });
                }
            }
            AddBeefMarbling(vertices, fat, left, right, pivot);
            var mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(lean, 0);
            mesh.SetTriangles(fat, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddBeefMarbling(List<Vector3> vertices, List<int> faces, float left, float right, float pivot)
        {
            foreach (var vein in BeefMarbling)
                for (var index = 1; index < vein.Length; index++)
                {
                    var a = vein[index - 1];
                    var b = vein[index];
                    var direction = (b - a).normalized;
                    var side = new Vector2(-direction.y, direction.x) * .001f;
                    var ribbon = new[] { a - side, b - side * .6f, b + side * .6f, a + side };
                    var segment = ClipBeef(ribbon, left, true);
                    if (segment.Length < 3) continue;
                    segment = ClipBeef(segment, right, false);
                    if (segment.Length < 3) continue;
                    var start = vertices.Count;
                    foreach (var point in segment)
                        vertices.Add(new Vector3(point.x - pivot, BeefHeight + .0002f, point.y));
                    for (var corner = 2; corner < segment.Length; corner++)
                        faces.AddRange(new[] { start, start + corner, start + corner - 1 });
                }
        }

        private static Vector2[] ClipBeef(Vector2[] input, float cut, bool keepRight)
        {
            var output = new List<Vector2>(input.Length + 2);
            var previous = input[input.Length - 1];
            var previousInside = keepRight ? previous.x >= cut : previous.x <= cut;
            foreach (var point in input)
            {
                var inside = keepRight ? point.x >= cut : point.x <= cut;
                if (inside != previousInside)
                    output.Add(Vector2.Lerp(previous, point, (cut - previous.x) / (point.x - previous.x)));
                if (inside) output.Add(point);
                previous = point;
                previousInside = inside;
            }
            return output.ToArray();
        }

        private static float Area(Vector2[] outline)
        {
            var doubled = 0f;
            var previous = outline[outline.Length - 1];
            foreach (var point in outline)
            {
                doubled += previous.x * point.y - previous.y * point.x;
                previous = point;
            }
            return Mathf.Abs(doubled) * .5f;
        }
    }
}
