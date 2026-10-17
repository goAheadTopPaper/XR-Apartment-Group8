using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using XRApartment.Data;
using XRApartment.Runtime;
using XRApartment.Runtime.Core;

namespace XRApartment.Tests
{
    public class KitchenSceneIntegrityTests
    {
        const string ScenePath = "Assets/_Project/Scenes/SCN_Kitchen_Review.unity";
        Scene scene;
        bool openedForTests;

        [OneTimeSetUp]
        public void LoadKitchen()
        {
            scene = SceneManager.GetSceneByPath(ScenePath);
            if (scene.IsValid() && scene.isLoaded) return;
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            openedForTests = true;
        }

        [OneTimeTearDown]
        public void CloseKitchen()
        {
            if (openedForTests) EditorSceneManager.CloseScene(scene, true);
        }

        T[] Components<T>() where T : Component => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

        [Test]
        public void StartupScene_IsTheAuthoredKitchen()
        {
            var enabledScenes = EditorBuildSettings.scenes.Where(item => item.enabled).ToArray();
            Assert.AreEqual(1, enabledScenes.Length);
            Assert.AreEqual(ScenePath, enabledScenes[0].path);
            Assert.AreEqual(0, Components<BenchmarkSlotMarker>().Length,
                "Benchmarks are now instantiated into the runtime slot in Play.");
            Assert.AreEqual(1, Components<JourneyRuntimeHost>().Length);
            Assert.AreEqual(1, Components<RuntimeBenchmarkAdapter>().Length);
            Assert.AreEqual(1, Components<TaskEvidenceBridge>().Length);
            Assert.AreEqual(1, Components<KitchenMenuController>().Length);
            Assert.AreEqual(1, Components<Camera>().Count(camera => camera.CompareTag("MainCamera")));
        }

        [Test]
        public void Scene_HasNoMissingScriptsOrBrokenPrefabInstances()
        {
            foreach (var transform in Components<Transform>())
            {
                Assert.AreEqual(0, GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject), transform.name);
                Assert.AreNotEqual(PrefabInstanceStatus.MissingAsset,
                    PrefabUtility.GetPrefabInstanceStatus(transform.gameObject), transform.name);
            }
        }

        [Test]
        public void ControllerHands_AreWiredToTheProjectRigAndRegisteredParameters()
        {
            var hands = Components<ControllerHandPresentation>();
            Assert.AreEqual(2, hands.Length);
            foreach (var hand in hands)
            {
                var serialized = new SerializedObject(hand);
                foreach (var field in new[] { "interactor", "solidRoot", "ghostRoot", "contactProbe", "solidAnimator", "ghostAnimator" })
                    Assert.IsNotNull(serialized.FindProperty(field).objectReferenceValue, hand.name + "." + field);
                var profile = (HandInteractionProfile)serialized.FindProperty("profile").objectReferenceValue;
                Assert.IsNotNull(profile, hand.name);
                Assert.That(profile.Length("hand_palm_radius"), Is.GreaterThan(0f));
                Assert.That(profile.Length("hand_ghost_show_distance"), Is.GreaterThan(profile.Length("hand_ghost_hide_distance")));
                Assert.IsFalse(((SphereCollider)serialized.FindProperty("contactProbe").objectReferenceValue).enabled);
                var interactor = (Component)serialized.FindProperty("interactor").objectReferenceValue;
                Assert.AreEqual(hand.transform.parent, interactor.transform.parent);
                Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(hand.transform.root.gameObject),
                    Is.EqualTo("Assets/_Project/Prefabs/PREF_XR_KitchenRig.prefab"));
                var animator = (Animator)serialized.FindProperty("solidAnimator").objectReferenceValue;
                Assert.IsNotNull(animator.runtimeAnimatorController);
                Assert.AreEqual(4, animator.runtimeAnimatorController.animationClips.Distinct().Count());
                Assert.IsFalse(hand.transform.parent.Find(hand.transform.parent.name + " Visual").gameObject.activeSelf);
            }
        }

        [Test]
        public void ImportedGeometry_HasMeshesCollidersAndBuiltInMaterials()
        {
            var environment = scene.GetRootGameObjects().Single(root => root.name == "PREF_Kitchen_Environment");
            var filters = environment.GetComponentsInChildren<MeshFilter>();
            Assert.IsNotEmpty(filters);
            foreach (var filter in filters)
            {
                Assert.IsNotNull(filter.sharedMesh, filter.name);
                var collider = filter.GetComponent<MeshCollider>();
                Assert.IsNotNull(collider, filter.name);
                Assert.AreSame(filter.sharedMesh, collider.sharedMesh, filter.name);
                foreach (var material in filter.GetComponent<Renderer>().sharedMaterials)
                {
                    Assert.IsNotNull(material, filter.name);
                    Assert.IsNotNull(material.shader, material.name);
                    Assert.AreEqual("Standard", material.shader.name, material.name);
                }
            }
        }

        [Test]
        public void DressingProps_UseRegisteredMetreScaleAndBoundGrabColliders()
        {
            var dressing = scene.GetRootGameObjects().Single(root => root.name == "PREF_Kitchen_Dressing");
            Assert.IsNull(dressing.GetComponentInParent<BenchmarkSlotMarker>());
            var assumptions = AssetDatabase.LoadAssetAtPath<ModelAssumptions>("Assets/_Project/Settings/ASM_KitchenParameters.asset");
            var paths = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/Prefabs/Props" })
                .Select(AssetDatabase.GUIDToAssetPath).ToArray();
            Assert.IsNotEmpty(paths);
            foreach (var path in paths)
            {
                var prop = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    Assert.AreEqual(Vector3.one, prop.transform.localScale, path);
                    var renderers = prop.GetComponentsInChildren<Renderer>();
                    var bounds = renderers[0].bounds;
                    foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                    string key = "prop_" + prop.name.Replace("PREF_Prop_", "").ToLowerInvariant();
                    bool height = assumptions.TryGetLength(key + "_height", out float registered);
                    if (!height) Assert.IsTrue(assumptions.TryGetLength(key + "_span", out registered), path);
                    float size = height ? bounds.size.y : Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                    Assert.That(size, Is.EqualTo(registered).Within(.001f), path);
                    Assert.That(bounds.min.y, Is.EqualTo(0).Within(.001f), path);
                    foreach (var material in renderers.SelectMany(renderer => renderer.sharedMaterials))
                    {
                        Assert.IsNotNull(material, path);
                        Assert.AreEqual("Standard", material.shader.name, path);
                    }
                    var grab = prop.GetComponent<XRGrabInteractable>();
                    if (grab == null) continue;
                    Assert.IsNotNull(prop.GetComponent<Rigidbody>(), path);
                    Assert.IsTrue(grab.useDynamicAttach, path);
                    Assert.IsNotEmpty(grab.colliders, path);
                    Assert.IsTrue(grab.colliders.All(collider => collider != null && collider.enabled
                        && collider.transform.IsChildOf(prop.transform)), path);
                }
                finally { PrefabUtility.UnloadPrefabContents(prop); }
            }
        }

        [Test]
        public void WaterAndSky_AreWiredToTheSelectedAssets()
        {
            Assert.AreEqual(1, Components<KitchenWaterSystem>().Length);
            Assert.AreEqual(1, Components<WaterTap>().Length);
            Assert.AreEqual(3, Components<LiquidContainer>().Length);
            foreach (var container in Components<LiquidContainer>())
            {
                var data = new SerializedObject(container);
                var profile = (WaterInteractionProfile)data.FindProperty("profile").objectReferenceValue;
                Assert.IsNotNull(profile, container.name);
                string prefix = data.FindProperty("parameterPrefix").stringValue;
                Assert.That(profile.Length(prefix + "_inner_radius"), Is.GreaterThan(0));
                Assert.That(profile.Length(prefix + "_height"), Is.GreaterThan(0));
                Assert.IsNotNull(data.FindProperty("surface").objectReferenceValue);
                Assert.IsNotNull(data.FindProperty("stream").objectReferenceValue);
                Assert.That(profile.SurfaceMaterial.shader.name, Is.EqualTo("#NVJOB/Water Shaders V2/Water Specular"));
                Assert.IsFalse(profile.SurfaceMaterial.IsKeywordEnabled("EFFECT_MIRROR"));
            }
            // The test runner can make its own scratch scene active; RenderSettings
            // are scene-specific, unlike the explicitly scoped component queries.
            var previous = SceneManager.GetActiveScene();
            try
            {
                SceneManager.SetActiveScene(scene);
                Assert.That(RenderSettings.skybox.shader.name, Is.EqualTo("Skybox/Cubemap"));
                Assert.IsNotNull(RenderSettings.skybox.GetTexture("_Tex"));
                Assert.That(AssetDatabase.GetAssetPath(RenderSettings.skybox.GetTexture("_Tex")),
                    Does.StartWith("Assets/ThirdParty/AllSkyFree/"));
            }
            finally { SceneManager.SetActiveScene(previous); }
        }

        [Test]
        public void CounterSurfaceAndFridgeSlot_MatchRegisteredPrototypeParameters()
        {
            var assumptions = AssetDatabase.LoadAssetAtPath<ModelAssumptions>("Assets/_Project/Settings/ASM_KitchenParameters.asset");
            var counter = Components<ReviewElement>().Single(element => element.Role == ReviewRole.CounterSurface);
            Assert.IsTrue(assumptions.TryGetLength(counter.AssumptionKey, out var height));
            Assert.IsTrue(assumptions.TryGetLength("counter_fridge_cut_x", out var cut));
            var filter = counter.GetComponentInChildren<MeshFilter>();
            Assert.That(filter.GetComponent<Renderer>().bounds.max.x, Is.EqualTo(cut).Within(0.001f));
            Physics.SyncTransforms();
            Assert.IsTrue(filter.GetComponent<MeshCollider>().Raycast(
                new Ray(new Vector3(-3f, 2f, 2f), Vector3.down), out var hit, 3f));
            Assert.That(hit.point.y, Is.EqualTo(height).Within(0.001f));
        }
    }
}
