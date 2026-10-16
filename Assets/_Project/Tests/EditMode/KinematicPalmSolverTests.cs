using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using XRApartment.Runtime;

namespace XRApartment.Tests
{
    public class KinematicPalmSolverTests
    {
        readonly List<GameObject> objects = new List<GameObject>();
        readonly Vector3 origin = new Vector3(1000f, 1000f, 1000f);
        Transform rig;
        KinematicPalmSolver solver;

        [SetUp]
        public void CreatePalm()
        {
            rig = Create("Test Rig", Vector3.zero).transform;
            var probe = rig.gameObject.AddComponent<SphereCollider>();
            probe.enabled = false;
            solver = new KinematicPalmSolver(probe, rig, Physics.DefaultRaycastLayers);
        }

        [TearDown]
        public void DestroyFixtures()
        {
            for (var i = objects.Count - 1; i >= 0; i--)
                if (objects[i] != null) Object.DestroyImmediate(objects[i]);
            objects.Clear();
            Physics.SyncTransforms();
        }

        GameObject Create(string name, Vector3 position)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.position = origin + position;
            objects.Add(gameObject);
            return gameObject;
        }

        BoxCollider Wall()
        {
            var wall = Create("Test Wall", Vector3.zero).AddComponent<BoxCollider>();
            wall.size = new Vector3(.1f, 10f, 10f);
            return wall;
        }

        Vector3 Resolve(Vector3 from, Vector3 target, Transform held = null)
        {
            Physics.SyncTransforms();
            return solver.Resolve(origin + from, origin + target, .05f, .002f, held) - origin;
        }

        [Test]
        public void FastMovement_CannotTunnelThroughWall()
        {
            Wall();
            var result = Resolve(Vector3.left, Vector3.right);
            Assert.That(result.x, Is.InRange(-.105f, -.099f));
            Assert.IsFalse(solver.QuerySaturated);
        }

        [Test]
        public void DiagonalMovement_SlidesAlongWall()
        {
            Wall();
            var result = Resolve(Vector3.left, new Vector3(1f, 1f, 0f));
            Assert.That(result.x, Is.LessThanOrEqualTo(-.099f));
            Assert.That(result.y, Is.EqualTo(1f).Within(.003f));
        }

        [Test]
        public void StartingInsideSurface_RecoversBeforeSweeping()
        {
            Wall();
            var inside = new Vector3(-.02f, 0f, 0f);
            var result = Resolve(inside, inside);
            Assert.That(result.x, Is.LessThanOrEqualTo(-.099f));
        }

        [Test]
        public void HeldTarget_DoesNotBlockItsHand()
        {
            var held = Wall().transform;
            Assert.That(Resolve(Vector3.left, Vector3.right, held).x, Is.EqualTo(1f).Within(.001f));
        }

        [Test]
        public void RigCollider_DoesNotBlockItsOwnHand()
        {
            Wall().transform.SetParent(rig, true);
            Assert.That(Resolve(Vector3.left, Vector3.right).x, Is.EqualTo(1f).Within(.001f));
        }

        [Test]
        public void Trigger_DoesNotBlockHand()
        {
            Wall().isTrigger = true;
            Assert.That(Resolve(Vector3.left, Vector3.right).x, Is.EqualTo(1f).Within(.001f));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RigidbodyProp_IsLeftToExistingXriGrab(bool kinematic)
        {
            var body = Wall().gameObject.AddComponent<Rigidbody>();
            body.isKinematic = kinematic;
            body.useGravity = false;
            Assert.That(Resolve(Vector3.left, Vector3.right).x, Is.EqualTo(1f).Within(.001f));
        }
    }
}
