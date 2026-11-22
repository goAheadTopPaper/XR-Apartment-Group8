using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using XRApartment.Runtime.ReviewV2;

namespace XRApartment.Tests
{
    public sealed class ReviewV2SchemeAssetTests
    {
        [TestCase("SCHEME_A", "integrated-kitchen-m2-v5", true)]
        [TestCase("SCHEME_B", "external-fridge-m2-v8", false)]
        public void AuthoredWorldHasCompleteLocalBindingsAndStableIdentity(string id, string version, bool doubleDoor)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/ReviewV2/Schemes/PREF_" + id + ".prefab");
            Assert.IsNotNull(asset); Assert.IsFalse(asset.activeSelf);
            var world = asset.GetComponent<ReviewSchemeWorld>(); Assert.IsTrue(world.IsConfigured);
            Assert.AreEqual(id, world.schemeId); Assert.AreEqual(version, world.modelVersion);
            Assert.AreEqual(doubleDoor, world.station.ActiveFridge.SecondaryDoor != null);
            Assert.IsTrue(world.station.transform.IsChildOf(world.transform));
            Assert.IsTrue(world.roomReset.transform.IsChildOf(world.transform));
            Assert.IsTrue(world.groceries.storageZone.transform.IsChildOf(world.station.ActiveFridge.transform));
            Assert.AreEqual(0, world.GetComponentsInChildren<ReviewSessionHost>(true).Length);
            Assert.IsTrue(world.GetComponentsInChildren<CookingGrabBridge>(true).All(b => b.host == null));
            foreach (var key in new[] { "FRIDGE", "COOKING_STATION", "GROCERY_BAG", "ZONE_FRIDGE_STORAGE", "CABINET_LAYOUT", "OBJECT_030", "OBJECT_020" })
                Assert.AreEqual(1, world.identities.Count(i => i.Id == key), key);
            var recipe = new SerializedObject(world.recipe);
            var tomato = recipe.FindProperty("tomato").objectReferenceValue as Component;
            Assert.IsTrue(world.groceries.groceries.Any(g => g.gameObject == tomato.gameObject), "Recipe must use the actual grocery tomato");
            Assert.IsFalse(recipe.FindProperty("requireFridgeOpening").boolValue);
            Assert.IsTrue(world.GetComponentsInChildren<Renderer>(true).All(r => r.lightmapIndex < 0 || r.lightmapIndex >= 65534),
                "Old Cooking baked indices cannot represent the new layouts");
            foreach (var probe in world.GetComponentsInChildren<HDAdditionalReflectionData>(true))
            {
                Assert.AreEqual(ProbeSettings.Mode.Realtime, probe.mode, "HDRP must not render the old Cooking baked probe for " + id + ": " + probe.name);
                Assert.IsNull(probe.bakedTexture); Assert.IsNull(probe.customTexture);
            }
        }
    }
}
