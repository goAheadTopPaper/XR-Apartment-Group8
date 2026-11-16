using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using XRApartment.Runtime;
using XRApartment.Runtime.ReviewV2;

namespace XRApartment.Tests
{
    public sealed class ReviewV2BStorageAssetTests
    {
        static ReviewSchemeWorld World => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/ReviewV2/Schemes/PREF_SCHEME_B.prefab").GetComponent<ReviewSchemeWorld>();
        static Transform Find(string id) => World.identities.Single(i => i.Id == id).transform;
        [Test] public void CounterToolsFollowAWithTheOriginalBHeightAndMicrowaveFitsBelowTheExtendedShelf()
        {
            var a = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/ReviewV2/Schemes/PREF_SCHEME_A.prefab").GetComponent<ReviewSchemeWorld>();
            foreach (var name in new[] { "VIS_Real_gas_hob", "COL_GasHob", "ACT_STOVE", "VIS_GasIndicator", "LOC_BurnerSurface", "ACT_HeatZone", "FX_GasBurner",
                "PREF_Cooking_FryingPan", "PREF_Cooking_ChoppingBoard", "PREF_Prop_knife", "PREF_Cooking_MixingBowl", "PREF_Cooking_TomatoBowl",
                "PREF_Cooking_Whisk", "PREF_Cooking_Spatula", "PREF_Cooking_Ladle", "PREF_Prop_plate" })
            {
                var source = a.station.transform.Find(name); var target = World.station.transform.Find(name);
                Assert.Less(Vector3.Distance(source.position - Vector3.up * .01f, target.position), .00001f, name);
                Assert.Less(Quaternion.Angle(source.rotation, target.rotation), .01f, name);
            }
            var board = World.station.transform.Find("PREF_Cooking_ChoppingBoard");
            Assert.AreEqual(board.position, Find("WORKTOP").position);
            Assert.IsNull(World.transform.Find("A_ChamferWorktop"));
            Assert.IsNull(World.station.GetComponentInChildren<ReviewPanStoveSnap>(true), "Only initial placements were requested for B");
            var microwave = Find("MICROWAVE_VISUAL");
            Assert.IsEmpty(microwave.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable>(true));
            Assert.IsEmpty(microwave.GetComponentsInChildren<KitchenHeatSource>(true));
            Assert.IsEmpty(microwave.GetComponentsInChildren<Rigidbody>(true));
            Assert.Less(Vector3.Distance(microwave.forward, Vector3.left), .00001f);
            var body = microwave.Find("Body"); Assert.AreEqual(new Vector3(.52f, .30f, .38f), body.localScale);
            var shelf = Find("FLOATING_SHELF"); var shelfBox = shelf.Find("B_FloatingShelfTop").GetComponent<BoxCollider>();
            var z = new[] { shelf.TransformPoint(Vector3.right * shelfBox.size.x * .5f).z, shelf.TransformPoint(Vector3.left * shelfBox.size.x * .5f).z };
            Assert.That(z.Min(), Is.EqualTo(.70f).Within(.00001f)); Assert.That(z.Max(), Is.EqualTo(2.26f).Within(.00001f));
            Assert.Greater(2.361947f - z.Max(), .10f, "Leave a gap at the kitchen rear wall");
            Assert.Greater(shelf.position.y - .015f - (microwave.position.y + .15f), .20f);
            foreach (var foot in microwave.Cast<Transform>().Where(t => t.name.StartsWith("Foot_")))
                Assert.That(foot.position.y - foot.lossyScale.y * .5f, Is.EqualTo(1.00f).Within(.00001f));
        }
        [Test] public void StorageHasFourOpenCellsAndTwoConfiguredDoorsUnderTheWorldRoot()
        {
            var world = World; Assert.AreEqual("external-fridge-m2-v8", world.modelVersion); Assert.IsTrue(world.IsConfigured);
            Assert.AreEqual(1, world.identities.Count(i => i.Id == "CABINET_LAYOUT"));
            Assert.IsNull(world.transform.Find("Released_Kitchen_PrepCounter"));
            var window = Find("WINDOW_STORAGE"); Assert.AreSame(world.transform.Find("B_StorageAndPrep"), window.parent);
            Assert.AreEqual(3, window.Cast<Transform>().Count(t => t.name.StartsWith("Upright_")));
            Assert.AreEqual(2, window.Cast<Transform>().Count(t => t.name.StartsWith("Shelf_")));
            var top = window.Find("B_WindowTop").GetComponent<BoxCollider>();
            Assert.AreEqual(new Vector3(1.35f, .03f, .60f), top.size); Assert.That(top.transform.TransformPoint(Vector3.up * .015f).y, Is.EqualTo(1).Within(.00001f));
            Assert.Less(top.transform.position.y + .015f, 1.1576f, "Window sill remains clear");
            var doors = Find("CABINET_LAYOUT").GetComponentsInChildren<KinematicSwingDoor>(true); Assert.AreEqual(2, doors.Length);
            foreach (var door in doors)
            {
                var data = new SerializedObject(door); Assert.AreEqual(90, data.FindProperty("openAngleDegrees").floatValue);
                Assert.IsNotNull(data.FindProperty("handleSweepProbe").objectReferenceValue); Assert.IsFalse(data.FindProperty("startOpen").boolValue);
                var xr = door.GetComponent<XRSimpleInteractable>();
                foreach (var probe in door.GetComponentsInChildren<BoxCollider>(true).Where(c => !c.enabled)) CollectionAssert.DoesNotContain(xr.colliders, probe);
                Assert.AreEqual(4, door.transform.Cast<Transform>().Count(t => t.name.StartsWith("Frame")));
                Assert.IsTrue(xr.colliders.All(c => c.enabled && !c.isTrigger));
            }
            Assert.IsTrue(world.transform.Find("B_StorageAndPrep").GetComponentsInChildren<Transform>(true).All(t => t.localScale.x > 0 && t.localScale.y > 0 && t.localScale.z > 0));
        }
        [Test] public void WorktopJoinsAtOneMetreAndFloatingShelfHasARealQuarterMetreSupport()
        {
            var cabinet = Find("CABINET_LAYOUT"); var top = cabinet.Find("ContinuousWorktop").GetComponent<BoxCollider>();
            Assert.That(top.transform.position.y + top.transform.lossyScale.y * .5f, Is.EqualTo(1).Within(.00001f));
            var north = cabinet.TransformPoint(new Vector3(.97361285f * .5f, .99f, 0)).z;
            Assert.That(1.67661285f - north, Is.EqualTo(.003f).Within(.00001f));
            Assert.That(cabinet.TransformPoint(new Vector3(0, 0, -.30f)).x, Is.EqualTo(-1.4519f).Within(.00001f));
            var shelf = Find("FLOATING_SHELF"); var board = shelf.Find("B_FloatingShelfTop").GetComponent<BoxCollider>();
            Assert.AreEqual(new Vector3(1.56f, .03f, .25f), board.size);
            Assert.That(board.transform.TransformPoint(Vector3.up * .015f).y, Is.EqualTo(1.55f).Within(.00001f));
            Assert.That(shelf.position.y, Is.EqualTo(1.535f).Within(.00001f), "New semantic anchor belongs to the shelf rather than the floor");
            Assert.IsTrue(board.enabled); Assert.IsNull(board.GetComponent<Rigidbody>());
            Assert.AreEqual(World.station.ActiveFridge, World.groceries.storageZone.GetComponentInParent<KitchenFridge>(true));
        }
        [Test] public void BevelMeshesHaveOutwardFacesAndMaterialsStayBLocalWithoutEmission()
        {
            foreach (var name in new[] { "B_WindowTop", "B_FloatingShelfTop" })
            {
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/_Project/Art/ReviewV2/B_Storage/MESH_" + name + ".asset");
                Assert.IsNotNull(mesh); var v = mesh.vertices; var n = mesh.normals; var triangles = mesh.triangles;
                Assert.AreEqual(96, v.Length); Assert.AreEqual(132, triangles.Length);
                for (var i = 0; i < triangles.Length; i += 3)
                {
                    // Vector3.Normalize treats valid 2mm corner cross-products as zero (epsilon 1e-5).
                    var cross = Vector3.Cross(v[triangles[i + 1]] - v[triangles[i]], v[triangles[i + 2]] - v[triangles[i]]);
                    Assert.Greater(cross.magnitude, 1e-9f, name + " has a degenerate face");
                    Assert.Greater(Vector3.Dot(cross, n[triangles[i]]), .99f * cross.magnitude, name + " has an inverted face");
                }
            }
            foreach (var renderer in World.transform.Find("B_StorageAndPrep").GetComponentsInChildren<MeshRenderer>(true))
            {
                Assert.IsNotNull(renderer.sharedMaterial); Assert.IsNotNull(renderer.GetComponent<MeshFilter>().sharedMesh);
                Assert.LessOrEqual(renderer.sharedMaterial.GetColor("_EmissiveColor").maxColorComponent, .000001f);
            }
            var wood = Find("WINDOW_STORAGE").GetComponentInChildren<MeshRenderer>(true).sharedMaterial;
            StringAssert.StartsWith("Assets/_Project/Art/ReviewV2/B_Storage/", AssetDatabase.GetAssetPath(wood));
            Assert.AreEqual(new Vector2(.20f, .30f), wood.GetTextureScale("_BaseColorMap"));
        }
    }
}
