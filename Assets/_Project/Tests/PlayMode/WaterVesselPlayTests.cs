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
                var waterSound = tap.GetComponentsInChildren<AudioSource>().Single(source => source.clip != null && source.clip.name == "SFX_Faucet");
                Assert.IsTrue(waterSound.loop);
                Assert.That(waterSound.spatialBlend, Is.EqualTo(1f));
                Assert.That(waterSound.transform.position, Is.EqualTo(tap.OutletPosition));
                yield return Frames(14);
                tap.SetRunning(false);
                Assert.IsFalse(waterSound.isPlaying);
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
        public IEnumerator WaterSurfaceHeightFollowsFillAndRecoversAfterEmptying()
        {
            var root = Instantiate(Recipe + "MixingBowl");
            root.GetComponent<Rigidbody>().interpolation = RigidbodyInterpolation.None;
            root.transform.position = Origin + Vector3.right;
            var vessel = Water(root);
            var surface = root.transform.Find("LIQ_Surface");
            var renderer = surface.GetComponent<MeshRenderer>();
            var meshFilter = surface.GetComponent<MeshFilter>();

            vessel.ResetWater();
            Assert.That(vessel.Receive(50f), Is.EqualTo(50f));
            yield return null;
            Assert.That(renderer.enabled, Is.True);
            var firstHeight = meshFilter.sharedMesh.vertices[0].y;

            Assert.That(vessel.Receive(25f), Is.EqualTo(25f));
            yield return null;
            var filledHeight = meshFilter.sharedMesh.vertices[0].y;
            Assert.That(filledHeight, Is.GreaterThan(firstHeight));

            vessel.ResetWater();
            yield return null;
            Assert.That(renderer.enabled, Is.False);

            Assert.That(vessel.Receive(75f), Is.EqualTo(75f));
            yield return null;
            Assert.That(renderer.enabled, Is.True);
            Assert.That(meshFilter.sharedMesh.vertices[0].y, Is.EqualTo(filledHeight).Within(.0001f));
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

        [UnityTest]
        public IEnumerator WaterPotAndPanHeatOnLitBurnerEmitSteamAndStaySafeWhenOffOrEmpty()
        {
            var workbench = Instantiate(Cooking + "Workbench");
            var heat = workbench.GetComponentInChildren<KitchenHeatSource>();
            var surface = workbench.transform.Find("LOC_BurnerSurface");
            var burnerVapour = workbench.transform.Find("ACT_HeatZone/FX_FoodSteam").GetComponent<ParticleSystem>();

            var pot = Instantiate(Cooking + "WaterPot");
            var potWater = Water(pot);
            var potSteam = pot.transform.Find("FX_Steam")?.GetComponent<ParticleSystem>();
            Assert.IsNotNull(potSteam, "PREF_Cooking_WaterPot must include a rim FX_Steam particle system.");
            potWater.ResetWater();
            var initialPotMl = potWater.Millilitres;
            Assert.Greater(initialPotMl, 50f);
            pot.transform.position = surface.position + Vector3.up * .06f;
            Physics.SyncTransforms();

            heat.SetOn(false);
            yield return Frames(4);
            Assert.That(potWater.TemperatureCelsius, Is.EqualTo(20f).Within(.1f));
            Assert.IsFalse(potWater.HasHotSteam);
            Assert.IsFalse(potWater.IsBoiling);
            Assert.IsFalse(potSteam.emission.enabled);
            Assert.IsFalse(burnerVapour.emission.enabled);

            heat.SetOn(true);
            Time.timeScale = 5f;
            for (var i = 0; i < 30 && !potWater.HasHotSteam; i++) yield return new WaitForSeconds(.4f);
            Assert.IsTrue(heat.IsHeatingWater(potWater));
            Assert.IsTrue(potWater.HasHotSteam);
            Assert.That(potWater.TemperatureCelsius, Is.GreaterThanOrEqualTo(45f));
            Assert.IsTrue(potSteam.emission.enabled, "Warming water pot should show visible steam.");
            Assert.IsTrue(burnerVapour.emission.enabled, "Burner vapour should engage while heating water.");

            for (var i = 0; i < 40 && !potWater.IsBoiling; i++) yield return new WaitForSeconds(.4f);
            Time.timeScale = 1f;
            Assert.IsTrue(potWater.IsBoiling);
            Assert.That(potWater.TemperatureCelsius, Is.EqualTo(100f).Within(.2f));
            Assert.That(potWater.Millilitres, Is.EqualTo(initialPotMl).Within(.01f), "Boiling must not consume water volume.");
            Assert.IsTrue(potSteam.emission.enabled);
            Assert.IsTrue(burnerVapour.emission.enabled);

            heat.SetOn(false);
            yield return null;
            Assert.IsFalse(burnerVapour.emission.enabled);

            heat.SetOn(true);
            pot.transform.rotation = Quaternion.Euler(0f, 0f, 125f);
            Time.timeScale = 4f;
            for (var i = 0; i < 60 && potWater.Millilitres > .05f; i++) yield return new WaitForSeconds(.2f);
            Time.timeScale = 1f;
            pot.transform.rotation = Quaternion.identity;
            Physics.SyncTransforms();
            yield return Frames(4);
            Assert.That(potWater.Millilitres, Is.LessThanOrEqualTo(.05f));
            Assert.That(potWater.TemperatureCelsius, Is.EqualTo(20f).Within(.1f));
            Assert.IsFalse(potWater.HasHotSteam);
            Assert.IsFalse(potSteam.emission.enabled);
            Assert.IsFalse(burnerVapour.emission.enabled);
            Object.Destroy(pot);

            var pan = Instantiate(Cooking + "FryingPan");
            var panFood = pan.GetComponent<KitchenFoodVessel>();
            var panWater = Water(pan);
            var panSteam = pan.transform.Find("FX_Steam").GetComponent<ParticleSystem>();
            panWater.ResetWater();
            Assert.That(panWater.Receive(180f), Is.EqualTo(180f).Within(.01f));
            pan.transform.position = surface.position + Vector3.up * .002f;
            Physics.SyncTransforms();
            Time.timeScale = 5f;
            for (var i = 0; i < 30 && !panWater.HasHotSteam; i++) yield return new WaitForSeconds(.4f);
            Time.timeScale = 1f;
            Assert.IsTrue(heat.IsHeatingWater(panWater));
            Assert.IsTrue(panSteam.emission.enabled, "Pan holding heated water must emit FX_Steam.");
            Assert.That(panWater.Millilitres, Is.EqualTo(180f).Within(.01f));
            Assert.Zero(panFood.Batch.Millilitres);
            heat.SetOn(false);
            yield return null;
            Assert.IsFalse(burnerVapour.emission.enabled);
        }
    }
}
#endif
