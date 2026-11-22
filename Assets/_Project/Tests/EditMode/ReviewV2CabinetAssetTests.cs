using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using XRApartment.Runtime;
using XRApartment.Runtime.ReviewV2;

namespace XRApartment.Tests
{
    public sealed class ReviewV2CabinetAssetTests
    {
        [Test] public void PanDockIsConfiguredOnlyInAWithTheExistingDisabledSweepAndFreeSerializedBody()
        {
            var a = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/ReviewV2/Schemes/PREF_SCHEME_A.prefab").GetComponent<ReviewSchemeWorld>();
            var pan = a.station.transform.Find("PREF_Cooking_FryingPan");
            var snap = pan.GetComponent<ReviewPanStoveSnap>(); Assert.IsNotNull(snap); Assert.IsTrue(snap.IsConfigured);
            Assert.AreEqual(RigidbodyConstraints.None, pan.GetComponent<Rigidbody>().constraints);
            Assert.IsFalse(pan.GetComponent<Rigidbody>().isKinematic);
            var data = new SerializedObject(snap);
            Assert.AreSame(a.station, data.FindProperty("station").objectReferenceValue);
            Assert.AreSame(a.station.transform.Find("LOC_BurnerSurface"), data.FindProperty("support").objectReferenceValue);
            var probe = data.FindProperty("sweepProbe").objectReferenceValue as BoxCollider;
            Assert.IsFalse(probe.enabled); CollectionAssert.DoesNotContain(pan.GetComponent<XRGrabInteractable>().colliders, probe);
            var b = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/ReviewV2/Schemes/PREF_SCHEME_B.prefab");
            Assert.IsEmpty(b.GetComponentsInChildren<ReviewPanStoveSnap>(true));
        }
        [Test] public void AWorktopHasClosedChamferMeshAndConsistentCookingBindings()
        {
            var world = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/ReviewV2/Schemes/PREF_SCHEME_A.prefab").GetComponent<ReviewSchemeWorld>();
            var top = world.identities.Single(i => i.Id == "COUNTER_WORKTOP");
            var mesh = top.GetComponent<MeshFilter>().sharedMesh;
            Assert.AreSame(mesh, top.GetComponent<MeshCollider>().sharedMesh);
            Assert.That(mesh.bounds.size.y, Is.EqualTo(.02f).Within(.00001f));
            Assert.That(mesh.bounds.max.y, Is.EqualTo(1.01f).Within(.00001f));
            Assert.IsTrue(mesh.vertices.Any(v => Vector3.Distance(v, new Vector3(-2.56f, 1.01f, 1.67661285f)) < .00001f));
            Assert.IsTrue(mesh.vertices.Any(v => Vector3.Distance(v, new Vector3(-1.99f, 1.01f, 1.81f)) < .00001f));
            var station = world.station.transform;
            var hob = station.Find("VIS_Real_gas_hob").position;
            var surface = station.Find("LOC_BurnerSurface").position;
            Assert.That(Vector3.Distance(hob, new Vector3(-3.95f, 1.015f, 1.95f)), Is.LessThan(.00001f));
            Assert.That(Vector3.Distance(surface, hob + Vector3.up * .055f), Is.LessThan(.00001f));
            Assert.That(Vector3.Distance(station.Find("ACT_HeatZone").position, hob + Vector3.up * .105f), Is.LessThan(.00001f));
            Assert.That(Vector3.Distance(station.Find("ACT_STOVE").position, hob + new Vector3(0, .025f, -.18f)), Is.LessThan(.00001f));
            var board = station.Find("PREF_Cooking_ChoppingBoard").position;
            Assert.That(board.x - hob.x, Is.GreaterThan(.8f));
            Assert.AreEqual(board, world.identities.Single(i => i.Id == "WORKTOP").transform.position);
            var pan = station.Find("PREF_Cooking_FryingPan").position;
            Assert.That(new Vector2(pan.x - hob.x, pan.z - hob.z).magnitude, Is.LessThan(.00001f));
            foreach (var name in new[] { "PREF_Cooking_MixingBowl", "PREF_Cooking_TomatoBowl", "PREF_Prop_knife", "PREF_Cooking_Whisk", "PREF_Cooking_Spatula", "PREF_Cooking_Ladle", "PREF_Prop_plate" })
                Assert.That(station.Find(name).position.x, Is.GreaterThan(hob.x + .4f), name + " should be in the right preparation area");
            var b = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/ReviewV2/Schemes/PREF_SCHEME_B.prefab").GetComponent<ReviewSchemeWorld>();
            Assert.IsFalse(b.identities.Any(i => i.Id == "COUNTER_WORKTOP"));
            Assert.AreEqual(new Vector3(-3.95f, 1.005f, 1.95f), b.station.transform.Find("VIS_Real_gas_hob").position);
        }
        [Test] public void CabinetIsLocalToAAndUsesConfiguredPullDoorsWithoutQueryCollidersInXri()
        {
            var world = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/ReviewV2/Schemes/PREF_SCHEME_A.prefab").GetComponent<ReviewSchemeWorld>();
            var cabinet = world.identities.Single(i => i.Id == "CABINET_LAYOUT");
            Assert.IsFalse(cabinet.transform.IsChildOf(world.station.transform), "RoomReset must discover the cabinet outside CookingStation");
            Assert.That(Quaternion.Angle(cabinet.transform.rotation, world.station.ActiveFridge.transform.rotation), Is.LessThan(.01));
            var doors = cabinet.GetComponentsInChildren<KinematicSwingDoor>(true);
            Assert.AreEqual(6, doors.Length);
            foreach (var fridgeDoor in new[] { world.station.ActiveFridge.Door, world.station.ActiveFridge.SecondaryDoor })
                Assert.AreEqual(105, new SerializedObject(fridgeDoor).FindProperty("openAngleDegrees").floatValue);
            foreach (var door in doors)
            {
                var data = new SerializedObject(door);
                Assert.AreEqual(90, data.FindProperty("openAngleDegrees").floatValue);
                Assert.IsFalse(data.FindProperty("startOpen").boolValue);
                Assert.IsNotNull(door.PullHandle);
                Assert.IsNotNull(data.FindProperty("collisionProfile").objectReferenceValue);
                foreach (var key in new[] { "sweepProbe", "handleSweepProbe" })
                {
                    var probe = data.FindProperty(key).objectReferenceValue as BoxCollider;
                    Assert.IsNotNull(probe); Assert.IsFalse(probe.enabled);
                    CollectionAssert.DoesNotContain(door.GetComponent<XRSimpleInteractable>().colliders, probe);
                }
                Assert.Contains(door.GetComponent<ReviewSceneIdentity>(), world.identities);
            }
            var microwave = world.identities.Single(i => i.Id == "MICROWAVE_VISUAL");
            Assert.IsEmpty(microwave.GetComponentsInChildren<XRBaseInteractable>(true));
            Assert.IsEmpty(microwave.GetComponentsInChildren<Rigidbody>(true));
            Assert.IsEmpty(microwave.GetComponentsInChildren<KitchenHeatSource>(true));
            var b = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/ReviewV2/Schemes/PREF_SCHEME_B.prefab").GetComponent<ReviewSchemeWorld>();
            Assert.IsFalse(b.identities.Any(i => i.Id.StartsWith("CABINET_NARROW")));
            Assert.AreEqual(1, b.identities.Count(i => i.Id == "MICROWAVE_VISUAL"));
            Assert.AreEqual("external-fridge-m2-v8", b.modelVersion);
            var aCounter = world.GetComponentsInChildren<MeshFilter>(true).Single(f => f.name == "SM_KitchenTable");
            var bCounter = b.GetComponentsInChildren<MeshFilter>(true).Single(f => f.name == "SM_KitchenTable");
            Assert.AreEqual("Assets/_Project/Art/ReviewV2/MESH_A_CounterCabinetRelief.asset", AssetDatabase.GetAssetPath(aCounter.sharedMesh));
            Assert.AreSame(aCounter.sharedMesh, aCounter.GetComponent<MeshCollider>().sharedMesh);
            Assert.AreEqual("Assets/_Project/Art/Kitchen/Meshes/SM_KitchenTable.fbx", AssetDatabase.GetAssetPath(bCounter.sharedMesh));
        }
    }
}
