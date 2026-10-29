#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using XRApartment.Runtime;

namespace XRApartment.Tests
{
    public sealed class GeneratedAssetRigTests
    {
        GameObject sample;
        [TearDown] public void Cleanup() { if (sample != null) Object.DestroyImmediate(sample); }

        [UnityTest]
        public IEnumerator BodyClipsDeformTheImportedSkinnedMeshAndKeepFiniteBounds()
        {
            sample = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/Generated/kitchen_character_v001/PREF_Character_kitchen_character.prefab"));
            sample.transform.position = new Vector3(120, 50, 120);
            var skin = sample.GetComponentInChildren<SkinnedMeshRenderer>();
            var animator = sample.GetComponentInChildren<Animator>();
            var controller = sample.GetComponent<CharacterMotionPreview>();
            Assert.IsTrue(animator.avatar.isValid); Assert.GreaterOrEqual(skin.bones.Length, 19);
            Assert.AreEqual(30000, skin.sharedMesh.triangles.Length / 3);
            Assert.IsTrue(skin.sharedMesh.boneWeights.All(weight => Mathf.Abs(weight.weight0 + weight.weight1 + weight.weight2 + weight.weight3 - 1) < .002f));
            var rest = new Mesh(); var posed = new Mesh();
            try
            {
                controller.SetMotion(0); animator.Update(0); skin.BakeMesh(rest);
                var original = rest.vertices;
                var seams = new System.Collections.Generic.Dictionary<Vector3Int, int>();
                var coincident = new System.Collections.Generic.List<(int first, int second)>();
                for (var i = 0; i < original.Length; i++)
                {
                    var key = Vector3Int.RoundToInt(original[i] * 100000);
                    if (seams.TryGetValue(key, out var previous)) coincident.Add((previous, i)); else seams.Add(key, i);
                }
                foreach (var motion in new[] { "Walk", "RaiseArm", "BendKnee" })
                {
                    animator.Play(motion, 0, motion == "Walk" ? .25f : .5f); animator.Update(0);
                    skin.BakeMesh(posed); var vertices = posed.vertices; var moved = 0;
                    for (var i = 0; i < vertices.Length; i++)
                    {
                        Assert.IsFalse(float.IsNaN(vertices[i].x) || float.IsInfinity(vertices[i].x));
                        if (Vector3.Distance(vertices[i], original[i]) > .003f) moved++;
                    }
                    Assert.Greater(moved, 100, motion + " must deform the mesh rather than animate an unrelated rig.");
                    foreach (var pair in coincident)
                        Assert.That(Vector3.Distance(vertices[pair.first], vertices[pair.second]), Is.LessThan(.002f), motion + " opened a UV seam.");
                    Assert.That(posed.bounds.size.magnitude, Is.InRange(1f, 3f));
                    yield return null;
                }
            }
            finally { Object.DestroyImmediate(rest); Object.DestroyImmediate(posed); }
        }

        [UnityTest]
        public IEnumerator CabinetDoorUsesItsOwnHingeAndTheHollowCarcassStaysFixed()
        {
            sample = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/Generated/cabinet_module_v001/PREF_Module_cabinet_module.prefab"));
            sample.transform.position = new Vector3(120, 50, 125);
            var door = sample.GetComponentInChildren<KinematicSwingDoor>();
            var frame = sample.GetComponentsInChildren<MeshFilter>().Single(mesh => mesh.name == "VIS_LeftSide");
            var panel = sample.GetComponentsInChildren<MeshFilter>().Single(mesh => mesh.name == "VIS_Door");
            var closedCentre = panel.transform.TransformPoint(panel.sharedMesh.bounds.center);
            var before = frame.transform.localToWorldMatrix;
            yield return null;
            door.SetOpen(true); yield return new WaitForSeconds(.9f);
            Assert.That(Quaternion.Angle(door.transform.localRotation, Quaternion.identity), Is.EqualTo(110).Within(.1f));
            Assert.That(panel.transform.TransformPoint(panel.sharedMesh.bounds.center).z, Is.GreaterThan(closedCentre.z + .15f), "The door must open out of the cabinet around its authored hinge.");
            Assert.AreEqual(before, frame.transform.localToWorldMatrix);
            door.SetOpen(false); yield return new WaitForSeconds(.9f);
            Assert.That(Quaternion.Angle(door.transform.localRotation, Quaternion.identity), Is.LessThan(.1f));
            Assert.IsTrue(sample.GetComponentsInChildren<MeshCollider>().All(collider => collider.convex && collider.sharedMesh.triangles.Length / 3 <= 255));
        }
    }
}
#endif
