using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using XRApartment.Runtime;

namespace XRApartment.Tests
{
    public class HeldPropSweepTests
    {
        readonly List<GameObject> objects = new List<GameObject>();
        readonly Vector3 origin = new Vector3(100f, 80f, 100f);
        Transform prop;
        BoxCollider probe;
        HeldPropSweep sweep;

        [SetUp]
        public void SetUp()
        {
            sweep = null;
            prop = Create("Test held prop", Vector3.zero).transform;
            probe = prop.gameObject.AddComponent<BoxCollider>();
            probe.size = Vector3.one * .2f;
            probe.enabled = false;
        }

        [TearDown]
        public void TearDown()
        {
            for (var i = objects.Count - 1; i >= 0; i--) Object.DestroyImmediate(objects[i]);
            objects.Clear();
            Physics.SyncTransforms();
        }

        GameObject Create(string name, Vector3 offset)
        {
            var go = new GameObject(name);
            go.transform.position = origin + offset;
            objects.Add(go);
            return go;
        }

        BoxCollider Wall()
        {
            var wall = Create("Test wall", Vector3.zero).AddComponent<BoxCollider>();
            wall.size = new Vector3(.01f, 10f, 10f);
            return wall;
        }

        Pose Resolve(Vector3 from, Vector3 target, Transform rig = null)
        {
            return Resolve(new Pose(origin + from, Quaternion.identity),
                new Pose(origin + target, Quaternion.identity), rig);
        }

        Pose Resolve(Pose from, Pose target, Transform rig = null)
        {
            if (sweep == null) sweep = new HeldPropSweep(probe, prop, Physics.DefaultRaycastLayers);
            Physics.SyncTransforms();
            var result = sweep.Resolve(from, target, rig, .002f, 3f, 64);
            Assert.IsFalse(probe.enabled, "Query-only probe must never participate in physics or XRI.");
            return result;
        }

        [Test]
        public void FastTranslation_CannotCrossThinWall()
        {
            Wall();
            var result = Resolve(Vector3.left, Vector3.right);
            Assert.That(result.position.x - origin.x, Is.InRange(-.109f, -.104f));
        }

        [Test]
        public void DiagonalMotion_SlidesAlongWall()
        {
            Wall();
            var result = Resolve(Vector3.left, new Vector3(1f, 1f, 0f));
            Assert.That(result.position.x - origin.x, Is.LessThanOrEqualTo(-.104f));
            Assert.That(result.position.y - origin.y, Is.EqualTo(1f).Within(.003f));
        }

        [Test]
        public void PullingBack_ReturnsFreelyWithoutSnappingAcrossWall()
        {
            Wall();
            var blocked = Resolve(Vector3.left, Vector3.right);
            var returned = Resolve(blocked, new Pose(origin + Vector3.left, Quaternion.identity));
            Assert.That(returned.position.x - origin.x, Is.EqualTo(-1f).Within(.003f));
        }

        [Test]
        public void InitialSmallOverlap_RecoversOnOriginalSide()
        {
            Wall();
            var result = Resolve(Vector3.left * .03f, Vector3.right);
            Assert.That(result.position.x - origin.x, Is.LessThanOrEqualTo(-.104f));
        }

        [Test]
        public void FastRotation_SweepsTheArcEvenWhenFinalPoseIsClear()
        {
            probe.size = new Vector3(1f, .08f, .08f);
            var wall = Create("Thin wall across angular path", Vector3.forward * .3f).AddComponent<BoxCollider>();
            wall.size = new Vector3(2f, 2f, .005f);
            var result = Resolve(new Pose(origin, Quaternion.identity), new Pose(origin, Quaternion.Euler(0, 180f, 0)));
            Assert.That(Quaternion.Angle(Quaternion.identity, result.rotation), Is.InRange(10f, 40f));
        }

        [Test]
        public void RestingOnTable_CanYawWithoutLifting()
        {
            probe.size = new Vector3(.33f, .12f, .18f);
            var table = Create("Table", Vector3.down * .1f).AddComponent<BoxCollider>();
            table.size = new Vector3(2f, .2f, 2f);
            var from = new Pose(origin + Vector3.up * .06f, Quaternion.identity);
            var requested = new Pose(from.position, Quaternion.Euler(0, 90f, 0));
            var result = Resolve(from, requested);
            Assert.That(Quaternion.Angle(result.rotation, requested.rotation), Is.LessThan(.1f));
            Assert.That(result.position.y - origin.y, Is.InRange(.06f, .065f));
        }

        [Test]
        public void LiftingAwayFromContact_AllowsRotationAgain()
        {
            var table = Create("Table", Vector3.down * .1f).AddComponent<BoxCollider>();
            table.size = new Vector3(2f, .2f, 2f);
            var near = new Pose(origin + Vector3.up * .103f, Quaternion.identity);
            var wanted = new Pose(origin + Vector3.up * .5f, Quaternion.Euler(0, 0, 90));
            var lifted = Resolve(near, wanted);
            var rotated = Resolve(lifted, wanted);
            Assert.That(Quaternion.Angle(rotated.rotation, wanted.rotation), Is.LessThan(.1f));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void BothDynamicPropsAndKinematicDoors_BlockTheHeldProp(bool kinematic)
        {
            var body = Wall().gameObject.AddComponent<Rigidbody>();
            body.isKinematic = kinematic;
            body.useGravity = false;
            var result = Resolve(Vector3.left, Vector3.right);
            Assert.That(result.position.x - origin.x, Is.LessThanOrEqualTo(-.104f));
        }

        [Test]
        public void Triggers_DoNotBlockGrab()
        {
            Wall().isTrigger = true;
            Assert.That(Resolve(Vector3.left, Vector3.right).position.x - origin.x, Is.EqualTo(1f).Within(.001f));
        }

        [Test]
        public void OwnRigAndOwnColliders_DoNotBlockGrab()
        {
            var rig = Create("Rig", Vector3.zero).transform;
            Wall().transform.SetParent(rig, true);
            var own = prop.gameObject.AddComponent<BoxCollider>();
            own.size = Vector3.one * 2f;
            Assert.That(Resolve(Vector3.left, Vector3.right, rig).position.x - origin.x, Is.EqualTo(1f).Within(.001f));
        }

        [Test]
        public void SaturatedQuery_StopsInsteadOfIgnoringPotentialObstacle()
        {
            for (var i = 0; i < 70; i++) Wall();
            var result = Resolve(Vector3.left, Vector3.right);
            Assert.IsTrue(sweep.QuerySaturated);
            Assert.That(result.position.x - origin.x, Is.EqualTo(-1f).Within(.001f));
        }
    }
}
