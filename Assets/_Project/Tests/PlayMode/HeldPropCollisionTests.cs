#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using XRApartment.Runtime;

namespace XRApartment.Tests
{
    // Exercises the real XRI selection/update path without controller hardware.
    public sealed class DrivenGrabInteractor : XRBaseInteractor
    {
        public override bool isSelectActive => true;
    }

    public class HeldPropCollisionTests
    {
        readonly List<GameObject> objects = new List<GameObject>();
        readonly Vector3 origin = new Vector3(100f, 50f, 100f);
        XRInteractionManager manager;
        DrivenGrabInteractor hand;
        XRGrabInteractable grab;
        const string PotPath = "Assets/_Project/Prefabs/PREF_Prop_Pot.prefab";

        [SetUp]
        public void SetUp()
        {
            manager = Create("Test interaction manager").AddComponent<XRInteractionManager>();
            hand = Create("Test tracked controller").AddComponent<DrivenGrabInteractor>();
            hand.interactionManager = manager;
            hand.keepSelectedTargetValid = true;
        }

        [TearDown]
        public void TearDown()
        {
            if (grab != null && grab.isSelected) manager.SelectExit((IXRSelectInteractor)hand, grab);
            for (var i = objects.Count - 1; i >= 0; i--)
                if (objects[i] != null) Object.DestroyImmediate(objects[i]);
            objects.Clear();
        }

        GameObject Create(string name)
        {
            var go = new GameObject(name);
            go.transform.position = origin;
            objects.Add(go);
            return go;
        }

        void LoadProp(string path)
        {
            var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            objects.Add(go);
            go.transform.SetPositionAndRotation(origin + Vector3.up * .4f, Quaternion.identity);
            grab = go.GetComponent<XRGrabInteractable>();
            grab.interactionManager = manager;
            grab.GetComponent<Rigidbody>().useGravity = false;
            hand.transform.position = go.transform.position;
            Physics.SyncTransforms();
        }

        IEnumerator PushThroughTable(string path)
        {
            var table = Create("Test solid tabletop").AddComponent<BoxCollider>();
            table.size = new Vector3(2f, .05f, 2f);
            LoadProp(path);
            yield return null;
            manager.SelectEnter((IXRSelectInteractor)hand, grab);
            Assert.IsTrue(grab.isSelected);
            // The tracked controller crosses the whole tabletop in one frame, then keeps pushing.
            hand.transform.position = origin + Vector3.down * .4f;
            for (var frame = 0; frame < 20; frame++) yield return new WaitForFixedUpdate();
            var colliders = grab.GetComponentsInChildren<Collider>();
            var bottom = float.PositiveInfinity;
            foreach (var collider in colliders)
                if (collider.enabled && !collider.isTrigger) bottom = Mathf.Min(bottom, collider.bounds.min.y);
            Assert.That(bottom, Is.GreaterThanOrEqualTo(table.bounds.max.y - .003f),
                "Held " + grab.name + " crossed a solid tabletop while the controller kept pushing.");
        }

        [UnityTest]
        public IEnumerator HeldPot_CannotBePushedThroughTable() =>
            PushThroughTable(PotPath);

        [UnityTest]
        public IEnumerator HeldMug_CannotBePushedThroughTable() =>
            PushThroughTable("Assets/_Project/Prefabs/Props/PREF_Prop_Mug.prefab");

        [UnityTest]
        public IEnumerator RemainingFoodAndCupPrefabs_AlsoStopAtTheTable()
        {
            var paths = new[]
            {
                "Assets/_Project/Prefabs/PREF_Prop_Cup.prefab",
                "Assets/_Project/Prefabs/Props/PREF_Prop_Banana.prefab",
                "Assets/_Project/Prefabs/Props/PREF_Prop_CerealBox.prefab",
                "Assets/_Project/Prefabs/Props/PREF_Prop_Cheese.prefab",
                "Assets/_Project/Prefabs/Props/PREF_Prop_Egg.prefab",
                "Assets/_Project/Prefabs/Props/PREF_Prop_JuicePack.prefab",
                "Assets/_Project/Prefabs/Props/PREF_Prop_Lemon.prefab",
                "Assets/_Project/Prefabs/Props/PREF_Prop_Onion.prefab",
                "Assets/_Project/Prefabs/Props/PREF_Prop_Tomato.prefab"
            };
            foreach (var path in paths)
            {
                yield return PushThroughTable(path);
                manager.SelectExit((IXRSelectInteractor)hand, grab);
                Object.DestroyImmediate(grab.gameObject);
                grab = null;
                // Remove just this iteration's tabletop before loading the next prop.
                var table = objects.Find(go => go != null && go.name == "Test solid tabletop");
                Object.DestroyImmediate(table);
            }
        }

        [UnityTest]
        public IEnumerator PullingBackResumesTracking_AndGoingAroundTheEdgeCanReachUnderTable()
        {
            var table = Create("Test solid tabletop").AddComponent<BoxCollider>();
            table.size = new Vector3(2f, .05f, 2f);
            LoadProp(PotPath);
            yield return null;
            manager.SelectEnter((IXRSelectInteractor)hand, grab);
            hand.transform.position = origin + Vector3.down * .4f;
            for (var frame = 0; frame < 10; frame++) { yield return null; yield return new WaitForFixedUpdate(); }
            var guard = grab.GetComponent<CollisionConstrainedGrabTransformer>();
            Assert.IsTrue(guard.IsBlocked);
            var presented = guard.ResolveHand(new Pose(hand.transform.position, hand.transform.rotation));
            Assert.That(presented.position.y, Is.GreaterThan(table.bounds.max.y));
            Assert.That(Vector3.Distance(presented.position, hand.transform.position), Is.GreaterThan(.1f));

            hand.transform.position = origin + Vector3.up * .4f;
            for (var frame = 0; frame < 10; frame++) { yield return null; yield return new WaitForFixedUpdate(); }
            Assert.IsFalse(guard.IsBlocked);
            Assert.That(grab.transform.position.y, Is.EqualTo(guard.RequestedPose.position.y).Within(.003f));

            hand.transform.position = origin + new Vector3(1.5f, -.4f, 0f);
            for (var frame = 0; frame < 20; frame++) { yield return null; yield return new WaitForFixedUpdate(); }
            Assert.That(grab.transform.position.y, Is.LessThan(table.bounds.min.y - .1f));
            hand.transform.position = origin + Vector3.down * .4f;
            for (var frame = 0; frame < 10; frame++) { yield return null; yield return new WaitForFixedUpdate(); }
            Assert.IsFalse(guard.IsBlocked);
            Assert.That(grab.transform.position.y, Is.EqualTo(guard.RequestedPose.position.y).Within(.003f));
        }

        [UnityTest]
        public IEnumerator ReleasingWhileBlocked_DropsSafelyAndNextGrabRestoresThrow()
        {
            var table = Create("Test solid tabletop").AddComponent<BoxCollider>();
            table.size = new Vector3(2f, .05f, 2f);
            LoadProp(PotPath);
            var body = grab.GetComponent<Rigidbody>();
            body.useGravity = true;
            yield return null;
            manager.SelectEnter((IXRSelectInteractor)hand, grab);
            hand.transform.position = origin + Vector3.down * .4f;
            for (var frame = 0; frame < 12; frame++) yield return new WaitForFixedUpdate();
            Assert.IsTrue(grab.GetComponent<CollisionConstrainedGrabTransformer>().IsBlocked);
            manager.SelectExit((IXRSelectInteractor)hand, grab);
            Assert.That(body.velocity.magnitude, Is.LessThan(.001f));
            Assert.That(body.angularVelocity.magnitude, Is.LessThan(.001f));
            for (var frame = 0; frame < 20; frame++) yield return new WaitForFixedUpdate();
            Assert.That(body.position.y, Is.GreaterThan(table.bounds.max.y));
            Assert.That(body.velocity.magnitude, Is.LessThan(.5f));
            hand.transform.position = body.position;
            manager.SelectEnter((IXRSelectInteractor)hand, grab);
            Assert.IsTrue(grab.throwOnDetach, "Blocked-release suppression must not disable later throws.");
        }

        [UnityTest]
        public IEnumerator AuthoredDiningTableMesh_StopsTheActualPot()
        {
            var environment = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/PREF_Kitchen_Environment.prefab"));
            environment.transform.position += origin;
            objects.Add(environment);
            var colliders = environment.GetComponentsInChildren<MeshCollider>();
            var table = System.Array.Find(colliders, c => c.name == "SM_DiningTable");
            Physics.SyncTransforms();
            Assert.IsTrue(table.Raycast(new Ray(origin + new Vector3(-2.5f, 3f, -1.2f), Vector3.down), out var hit, 5f));
            LoadProp(PotPath);
            grab.transform.position = hit.point + Vector3.up * .3f;
            hand.transform.position = grab.transform.position;
            Physics.SyncTransforms();
            yield return null;
            manager.SelectEnter((IXRSelectInteractor)hand, grab);
            hand.transform.position = hit.point + Vector3.down * .4f;
            for (var frame = 0; frame < 20; frame++) yield return new WaitForFixedUpdate();
            foreach (var collider in grab.colliders)
                Assert.That(collider.bounds.min.y, Is.GreaterThanOrEqualTo(hit.point.y - .003f), collider.name);
            Assert.IsTrue(grab.GetComponent<CollisionConstrainedGrabTransformer>().IsBlocked);
        }
    }
}
#endif
