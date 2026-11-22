using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using XRApartment.Runtime;
using XRApartment.Runtime.ReviewV2;

namespace XRApartment.Tests
{
    public sealed class ReviewV2BNorthAssetTests
    {
        static ReviewSchemeWorld B => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/ReviewV2/Schemes/PREF_SCHEME_B.prefab").GetComponent<ReviewSchemeWorld>();
        static KitchenFridge Source => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Cooking/PREF_Cooking_Workbench.prefab").GetComponent<KitchenCookingStation>().ActiveFridge;
        [Test] public void NorthHingeSouthHandleAndOriginalSweepAreBoundOnlyInB()
        {
            var world = B; var fridge = world.station.ActiveFridge; var door = fridge.Door;
            Assert.AreEqual("external-fridge-m2-v8", world.modelVersion);
            Assert.That(Vector3.Distance(fridge.transform.position, new Vector3(-4.65f, 0, -.1f)), Is.LessThan(.00001f));
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(90, fridge.transform.eulerAngles.y)), Is.LessThan(.001f));
            Assert.AreEqual(new Vector3(-.345f, .04f, .37f), door.transform.localPosition);
            Assert.Greater(door.transform.position.z, door.PullHandle.position.z);
            var data = new SerializedObject(door);
            Assert.AreEqual(-1, data.FindProperty("swingSign").floatValue);
            Assert.AreEqual(105, data.FindProperty("openAngleDegrees").floatValue);
            var probe = (BoxCollider)data.FindProperty("sweepProbe").objectReferenceValue;
            Assert.IsFalse(probe.enabled);
            var interactable = door.GetComponent<XRSimpleInteractable>();
            CollectionAssert.DoesNotContain(interactable.colliders, probe);
            Assert.IsTrue(interactable.colliders.All(c => c != null && c.enabled && !c.isTrigger && c.transform.IsChildOf(door.transform)));
            Assert.IsNull(fridge.SecondaryDoor); Assert.IsTrue(world.IsConfigured);
            Assert.AreEqual(fridge, world.groceries.storageZone.transform.GetComponentInParent<KitchenFridge>(true), "Inactive prefab asset storage zone must belong to the current fridge");
            Assert.IsTrue(door.GetComponentsInChildren<Transform>(true).All(t => t.localScale.x > 0 && t.localScale.y > 0 && t.localScale.z > 0));
            Assert.AreEqual(1, new SerializedObject(Source.Door).FindProperty("swingSign").floatValue, "Cooking source keeps its original hinge");
        }
        [Test] public void DerivedDoorLodsMirrorGeometryNormalsTangentsAndWindingWithoutChangingTheSource()
        {
            var source = Source.Door.transform; var target = B.station.ActiveFridge.Door.transform;
            foreach (var filter in source.GetComponentsInChildren<MeshFilter>(true))
            {
                var path = AnimationUtility.CalculateTransformPath(filter.transform, source);
                var actual = target.Find(path).GetComponent<MeshFilter>().sharedMesh; var original = filter.sharedMesh;
                StringAssert.StartsWith("Assets/_Project/Art/ReviewV2/B_NorthHinge/", AssetDatabase.GetAssetPath(actual));
                Assert.AreNotSame(original, actual); Assert.AreEqual(original.vertexCount, actual.vertexCount);
                var a = original.vertices; var b = actual.vertices; var max = 0f;
                for (var i = 0; i < a.Length; i++) max = Mathf.Max(max, Vector3.Distance(new Vector3(-a[i].x, a[i].y, a[i].z), b[i]));
                Assert.Less(max, .000001f, path);
                var normals = original.normals; var derivedNormals = actual.normals;
                Assert.AreEqual(normals.Length, derivedNormals.Length);
                for (var i = 0; i < normals.Length; i++)
                    Assert.Less(Vector3.Distance(new Vector3(-normals[i].x, normals[i].y, normals[i].z), derivedNormals[i]), .000001f);
                var tangents = original.tangents; var derivedTangents = actual.tangents;
                Assert.AreEqual(tangents.Length, derivedTangents.Length);
                for (var i = 0; i < tangents.Length; i++) Assert.AreEqual(new Vector4(-tangents[i].x, tangents[i].y, tangents[i].z, -tangents[i].w), derivedTangents[i]);
                for (var sub = 0; sub < original.subMeshCount; sub++)
                {
                    var triangles = original.GetTriangles(sub); var mirrored = actual.GetTriangles(sub);
                    Assert.AreEqual(triangles.Length, mirrored.Length);
                    for (var i = 0; i < triangles.Length; i += 3)
                    { Assert.AreEqual(triangles[i], mirrored[i]); Assert.AreEqual(triangles[i + 1], mirrored[i + 2]); Assert.AreEqual(triangles[i + 2], mirrored[i + 1]); }
                }
            }
        }
    }
}
