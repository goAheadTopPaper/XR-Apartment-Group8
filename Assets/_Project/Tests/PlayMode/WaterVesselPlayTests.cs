#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit;
using XRApartment.Runtime;

namespace XRApartment.Tests
{
    public sealed class WaterVesselPlayTests
    {
        const string Prefabs = "Assets/_Project/Prefabs/";
        const string Cooking = "Cooking/PREF_Cooking_";
        const string Recipe = "Cooking/Recipe/PREF_Cooking_";
        const string Props = "Props/PREF_Prop_";
        const string Plate = "Generated/plate_v001/PREF_Prop_plate";
        static readonly Vector3 Origin = new Vector3(50f, 5f, 50f);
        Scene scene, previous;
        WaterTap tap;
        KitchenWaterSystem world;
        float timeScale;

        [SetUp]
        public void SetUp()
        {
            previous = SceneManager.GetActiveScene();
            scene = SceneManager.CreateScene("Water vessel fixture");
            SceneManager.SetActiveScene(scene);
            timeScale = Time.timeScale;
            Time.timeScale = 1f;
            new GameObject("Interaction manager").AddComponent<XRInteractionManager>();
            if (!Object.FindObjectsOfType<AudioListener>().Any(listener => listener.isActiveAndEnabled))
                new GameObject("Test audio listener").AddComponent<AudioListener>();
            var station = Instantiate(Cooking + "WaterStation");
            station.transform.position = Origin;
            tap = station.GetComponentInChildren<WaterTap>();
            world = station.GetComponent<KitchenWaterSystem>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = timeScale;
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        }

        static GameObject Instantiate(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + path + ".prefab");
            Assert.IsNotNull(prefab, path);
            var instance = Object.Instantiate(prefab);
            if (instance.TryGetComponent<Rigidbody>(out var body)) body.isKinematic = true;
            return instance;
        }

        static LiquidContainer Water(GameObject root) => root.GetComponentInChildren<LiquidContainer>();

        static Vector3 Mouth(LiquidContainer vessel) =>
            vessel.transform.Find("LIQ_Surface").position + vessel.transform.up * vessel.Height;

        static IEnumerator Frames(int count)
        {
            for (var i = 0; i < count; i++) yield return new WaitForFixedUpdate();
        }

        [UnityTest]
        public IEnumerator FaucetFillsEveryCalibratedUtensilIncludingThoseWithSolidGrabProxies()
        {
            var paths = new[] { Cooking + "WaterCup", Cooking + "WaterPot", Recipe + "MixingBowl",
                Cooking + "TomatoBowl", Recipe + "FryingPan", Cooking + "Ladle", Plate,
                Props + "Mug", Props + "Bowl", Props + "SmallBowl", Props + "Plate" };
            foreach (var path in paths)
            {
                var root = Instantiate(path);
                var vessel = Water(root);
                Assert.IsNotNull(vessel, path);
                root.transform.position += tap.OutletPosition - Vector3.up * .06f - Mouth(vessel);
                vessel.ResetWater();
                Physics.SyncTransforms();
                var before = vessel.Millilitres;
                tap.SetRunning(true);
                yield return Frames(14);
                tap.SetRunning(false);
                yield return Frames(9);
                Assert.That(vessel.Millilitres, Is.GreaterThan(before + 6f), path);
                Assert.That(vessel.Millilitres, Is.LessThanOrEqualTo(vessel.CapacityMillilitres), path);
                Object.Destroy(root);
                yield return null;
                world.ClearFlow();
            }
        }

        [UnityTest]
        public IEnumerator WaterAndEggUseTheSameTaperedBowlAndWaterStaysLevelWhenTilted()
        {
            var root = Instantiate(Recipe + "MixingBowl");
            // This fixture assigns poses directly. Keep Rigidbody interpolation from replaying the prior pose.
            root.GetComponent<Rigidbody>().interpolation = RigidbodyInterpolation.None;
            root.transform.position = Origin + Vector3.right;
            var vessel = Water(root);
            var food = root.GetComponent<KitchenFoodVessel>();
            var water = root.transform.Find("LIQ_Surface");
            var egg = root.transform.Find("LIQ_Egg");
            vessel.ResetWater();
            Assert.That(vessel.Receive(50f), Is.EqualTo(50f));
            Assert.That(food.ReceiveEgg(50f), Is.Zero, "A wet bowl cannot simultaneously present an egg batch.");
            yield return null;
            var dryPoseRadius = SurfaceRadius(water);
            Assert.That(dryPoseRadius, Is.InRange(.038f, .057f), "A fixed 75 mm disk projects through the tapered ceramic.");
            root.transform.rotation = Quaternion.Euler(0f, 0f, 18f);
            Physics.SyncTransforms();
            yield return null;
            yield return null;
            Assert.That(Vector3.Angle(root.transform.up, Vector3.up), Is.EqualTo(18f).Within(.01f));
            var mesh = water.GetComponent<MeshFilter>().sharedMesh;
            var heights = mesh.vertices.Select(p => water.TransformPoint(p).y).ToArray();
            Assert.That(heights.Max() - heights.Min(), Is.LessThan(.0001f));
            Assert.That(vessel.Millilitres, Is.EqualTo(50f).Within(.01f));
            root.transform.rotation = Quaternion.identity;
            vessel.ResetWater();
            Assert.That(food.ReceiveEgg(50f), Is.EqualTo(50f));
            Assert.That(vessel.Receive(50f), Is.Zero);
            yield return null;
            Assert.That(SurfaceRadius(egg), Is.EqualTo(dryPoseRadius).Within(.0001f));
            Assert.That(water.GetComponent<MeshRenderer>().enabled, Is.False);
        }

        static float SurfaceRadius(Transform plane)
        {
            Assert.That(plane.GetComponent<MeshRenderer>().enabled, Is.True);
            var vertices = plane.GetComponent<MeshFilter>().sharedMesh.vertices;
            Assert.That(vertices.Length, Is.GreaterThan(2));
            return vertices.Max(p => new Vector2(p.x, p.z).magnitude);
        }

        [UnityTest]
        public IEnumerator CupPoursIntoBowlAndBrokenPlateReleasesItsWater()
        {
            var bowl = Water(Instantiate(Cooking + "TomatoBowl"));
            bowl.transform.position = Origin + Vector3.right;
            var cup = Water(Instantiate(Cooking + "WaterCup"));
            cup.transform.rotation = Quaternion.Euler(0f, 0f, 110f);
            cup.transform.position += Mouth(bowl) + Vector3.up * .14f - Mouth(cup) + cup.transform.right * cup.Radius;
            cup.ResetWater();
            var volume = cup.Millilitres;
            Physics.SyncTransforms();
            yield return Frames(80);
            Assert.That(cup.Millilitres, Is.LessThan(.01f));
            Assert.That(bowl.Millilitres, Is.EqualTo(volume).Within(.1f));

            var plate = Instantiate(Plate);
            plate.transform.position = Origin + Vector3.right * 2f;
            var plateWater = Water(plate);
            Assert.That(plateWater.Receive(40f), Is.EqualTo(40f));
            var spillBefore = world.SpilledMillilitres;
            var prepared = plate.GetComponent<PreparedKitchenProp>();
            Assert.That(prepared.TryImpact(10f), Is.True);
            Assert.That(plateWater.isActiveAndEnabled, Is.False);
            Assert.That(plateWater.Millilitres, Is.Zero);
            Assert.That(world.SpilledMillilitres - spillBefore, Is.EqualTo(40f).Within(.01f));
            prepared.ResetProp();
            Assert.That(plateWater.isActiveAndEnabled, Is.True);
        }
    }
}
#endif
