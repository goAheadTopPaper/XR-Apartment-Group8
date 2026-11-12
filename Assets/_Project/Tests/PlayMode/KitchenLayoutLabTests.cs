using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit.UI;
using XRApartment.Data;
using XRApartment.Runtime;
using XRApartment.Runtime.Core;
using XRApartment.Runtime.UI;

namespace XRApartment.Tests
{
    public class KitchenLayoutLabTests
    {
        [UnityTest]
        public IEnumerator MenuSwitchesPhysicalLayoutsAndRestocksTheMovedFridge()
        {
            SceneManager.LoadScene("SCN_Kitchen_Cooking");
            yield return null;
            var lab = Object.FindObjectOfType<KitchenLayoutLab>();
            var fridge = Object.FindObjectOfType<KitchenFridge>();
            var station = Object.FindObjectOfType<KitchenCookingStation>();
            var movement = Object.FindObjectOfType<LocomotionSettingsView>();
            KitchenSceneTestSetup.DisableStartupGuide(movement);
            Assert.NotNull(lab);
            Assert.NotNull(movement.GetComponentInChildren<KitchenLayoutMenu>(true));
            Assert.AreEqual(3, lab.PresetCount);
            Assert.AreEqual(3, lab.FinishCount);
            Assert.IsNull(Object.FindObjectOfType<JourneyRuntimeHost>());
            var originalDock = fridge.transform.position;
            var sink = GameObject.Find("BASIN_Collect").transform;
            var acrossFromFridge = Vector3.ProjectOnPlane(sink.position - originalDock, Vector3.up).normalized;
            Assert.That(Vector3.Dot(fridge.transform.forward, acrossFromFridge), Is.GreaterThan(.99f));

            var cabinet = GameObject.Find("SM_KitchenTable");
            var cabinetCollider = cabinet.GetComponent<MeshCollider>();
            Assert.AreEqual("KitchenTable", cabinetCollider.sharedMesh.name);
            Assert.That(cabinet.GetComponent<MeshRenderer>().bounds.max.x, Is.GreaterThan(-1.47f));
            Assert.IsTrue(cabinetCollider.Raycast(new Ray(new Vector3(-1.87f, 2f, 1.98f), Vector3.down),
                out _, 2f), "The original cabinet must support objects in the former fridge slot.");
            var counter = cabinet.GetComponent<MeshRenderer>();
            var original = counter.sharedMaterial.color;
            var fullEnamel = EnamelRenderers(fridge);
            var fridgeCard = station.transform.Find("UI_FridgeInstructions");
            Assert.NotNull(fridgeCard.GetComponent<Canvas>());
            Assert.IsNull(fridgeCard.GetComponent<TrackedDeviceGraphicRaycaster>());
            var fridgeAnchor = fridge.transform.InverseTransformPoint(fridgeCard.position);
            var fridgeAction = fridgeCard.Find("Action").GetComponent<TMP_Text>();
            var fridgeDetail = fridgeCard.Find("Detail").GetComponent<TMP_Text>();
            var stock = station.GetComponentsInChildren<KitchenStoredIngredient>();
            Assert.AreEqual(11, stock.Length);
            Assert.IsTrue(stock.All(food => fridge.Contains(food.transform.position)), "Stock must start on the moved shelves.");
            AssertClosedDoorRouteIsClear();

            var background = InputSystem.settings.backgroundBehavior;
            var editorFocus = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                yield return OpenLayoutMenu(keyboard, movement);
                var menu = Object.FindObjectOfType<KitchenLayoutMenu>();
                Assert.NotNull(menu);
                var panel = (RectTransform)menu.transform;
                foreach (RectTransform child in panel)
                {
                    var halfHeight = child.rect.height * .5f;
                    Assert.That(child.anchoredPosition.y - halfHeight, Is.GreaterThanOrEqualTo(panel.rect.yMin), child.name);
                    Assert.That(child.anchoredPosition.y + halfHeight, Is.LessThanOrEqualTo(panel.rect.yMax), child.name);
                }
                ClickWithTrackedRay(movement, menu.transform.Find("ForwardDock"));
                Assert.AreEqual(1, lab.ActivePresetIndex);
                Assert.That(Vector3.Distance(fridge.transform.position, originalDock + fridge.transform.forward * .16f),
                    Is.LessThan(.005f));
                Assert.IsFalse(fridge.Door.IsOpen);
                Assert.IsTrue(stock.All(food => fridge.Contains(food.transform.position) && !food.WasRetrieved));
                AssertClosedDoorRouteIsClear();
                movement.Close();

                fridge.Door.SetOpen(true);
                yield return new WaitForSeconds(1.4f);
                Assert.That(lab.ObservedSwing(false), Is.EqualTo("105°"), "The forward dock must allow a full opening.");
                Assert.IsTrue(lab.TryApply(2, out var status), status);
                yield return null;
                yield return null;
                AssertClosedDoorRouteIsClear();
                fridge.Door.SetOpen(true);
                yield return new WaitForSeconds(1.4f);
                Assert.That(fridge.Door.Progress, Is.InRange(.15, .79), "The pantry must physically limit the same door.");
                Assert.IsFalse(fridge.Accessible, "The full-size leaf cannot clear the shelf-access threshold beside the pantry.");
                Assert.That(lab.Description(2), Does.Contain("The pantry stopped the fridge door"));
                Assert.That(fridgeAction.text, Does.Contain("blocked"));
                Assert.That(fridgeDetail.text, Does.Contain("compact fridge"));
                var fullAngle = Mathf.Abs(Mathf.DeltaAngle(0f, fridge.Door.transform.localEulerAngles.y));
                var fullObservation = lab.ObservedSwing(false);
                var foodScales = stock.Select(food => food.transform.lossyScale).ToArray();

                yield return OpenLayoutMenu(keyboard, movement);
                ClickWithTrackedRay(movement, menu.transform.Find("FinishSage"));
                Assert.AreEqual(1, lab.ActiveFinishIndex);
                Assert.That(menu.transform.Find("SelectedFinish").GetComponent<TMP_Text>().text, Does.EndWith("Sage"));
                Assert.AreEqual(original, counter.sharedMaterial.color);
                var sage = InstanceColour(counter);
                Assert.Greater(sage.g, sage.r);
                Assert.AreEqual(sage, InstanceColour(GameObject.Find("VIS_PantryCase").GetComponent<Renderer>()));
                Assert.That(fullEnamel.Select(InstanceColour).Distinct().Count(), Is.EqualTo(1),
                    "Full-size door and body should share their finish at both LODs.");
                Assert.AreEqual(fullObservation, lab.ObservedSwing(false));
                Assert.That(fridge.Door.Progress, Is.GreaterThan(.15), "Finish selection must leave the blocked door open.");

                ClickWithTrackedRay(movement, menu.transform.Find("CompactFridge"));
                Assert.AreEqual(2, lab.ActivePresetIndex, "Model selection must preserve the furniture arrangement.");
                Assert.IsTrue(lab.CompactSelected);
                Assert.IsFalse(fridge.gameObject.activeInHierarchy);
                var compact = station.ActiveFridge;
                Assert.That(Vector3.Distance(fridgeAnchor, compact.transform.InverseTransformPoint(fridgeCard.position)),
                    Is.LessThan(.001f), "The guide must follow the active refrigerator.");
                var compactEnamel = EnamelRenderers(compact);
                Assert.AreEqual(1, lab.ActiveFinishIndex);
                Assert.AreEqual(InstanceColour(fullEnamel[0]), InstanceColour(compactEnamel[0]));
                Assert.IsTrue(compactEnamel.All(part => InstanceColour(part) == InstanceColour(fullEnamel[0])));
                Assert.That(compact.transform.lossyScale.x, Is.EqualTo(.85f).Within(.001f));
                Assert.IsTrue(stock.All(food => compact.Contains(food.transform.position) && !food.WasRetrieved));
                for (var index = 0; index < stock.Length; index++)
                    Assert.That(Vector3.Distance(foodScales[index], stock[index].transform.lossyScale), Is.LessThan(.001f));
                AssertClosedDoorRouteIsClear();
                movement.Close();

                compact.Door.SetOpen(true);
                yield return new WaitForSeconds(1.4f);
                var compactAngle = Mathf.Abs(Mathf.DeltaAngle(0f, compact.Door.transform.localEulerAngles.y));
                Assert.That(compactAngle, Is.GreaterThan(fullAngle + 4f), "A smaller door should clear more of the same pantry.");
                Assert.IsTrue(compact.Accessible, "The compact leaf should clear the shelf-access threshold in the same layout.");
                Assert.That(fridgeAction.text, Does.Contain("Grip to take a tomato"));
                yield return OpenLayoutMenu(keyboard, movement);
                var comparison = menu.transform.Find("ObservedSwing").GetComponent<TMP_Text>().text;
                Assert.That(comparison, Does.Contain("Full " + fullObservation).And.Contain("Compact " + lab.ObservedSwing(true)));
                Assert.That(lab.ObservedSwing(true), Does.EndWith("°"));
                var recipe = station.GetComponent<KitchenRecipeController>().Recipe;
                var step = recipe.Step;
                Assert.AreEqual(TomatoEggStep.TakeTomato, step);
                ClickWithTrackedRay(movement, menu.transform.Find("FinishGraphite"));
                Assert.AreEqual(2, lab.ActiveFinishIndex);
                Assert.Less(InstanceColour(counter).g, sage.g);
                Assert.AreSame(compact, station.ActiveFridge);
                Assert.AreEqual(step, recipe.Step);
                Assert.IsTrue(compact.Accessible);
                Assert.AreEqual(comparison, menu.transform.Find("ObservedSwing").GetComponent<TMP_Text>().text);
                TestContext.WriteLine(comparison);
                movement.Close();

                Assert.IsTrue(lab.TryApply(1, out status), status);
                for (var frame = 0; frame < 3; frame++) yield return null;
                Assert.AreEqual(2, lab.ActiveFinishIndex);
                Assert.AreSame(compact, station.ActiveFridge);
                AssertClosedDoorRouteIsClear();
                compact.Door.SetOpen(true);
                yield return new WaitForSeconds(1.4f);
                Assert.That(lab.ObservedSwing(true), Is.EqualTo("105°"), "The forward dock must also clear the compact door.");

                Assert.IsTrue(lab.TryApply(0, out status), status);
                for (var frame = 0; frame < 3; frame++) yield return null;
                Assert.AreSame(compact, station.ActiveFridge);
                AssertClosedDoorRouteIsClear();
                Assert.IsTrue(lab.TrySelectFridge(false, out status), status);
                yield return null;
                yield return null;
                Assert.AreSame(fridge, station.ActiveFridge);
                Assert.That(Vector3.Distance(fridge.transform.position, originalDock), Is.LessThan(.005f));
                Assert.IsFalse(fridge.Door.IsOpen);
                Assert.IsTrue(stock.All(food => fridge.Contains(food.transform.position)));
                fridge.Door.SetOpen(true);
                yield return new WaitForSeconds(1.4f);
                Assert.That(lab.ObservedSwing(false), Is.EqualTo("105°"), "The door must clear the restored cabinet.");

                yield return OpenLayoutMenu(keyboard, movement);
                ClickWithTrackedRay(movement, menu.transform.Find("FinishWarmIvory"));
                Assert.AreEqual(0, lab.ActiveFinishIndex);
                Assert.IsFalse(counter.HasPropertyBlock());
                Assert.IsTrue(fullEnamel.Concat(compactEnamel).All(part => !part.HasPropertyBlock()));
                Assert.AreEqual(original, counter.sharedMaterial.color);
                ClickWithTrackedRay(movement, menu.transform.Find("FinishSage"));
                Assert.AreEqual(1, lab.ActiveFinishIndex);
                movement.Close();
                SceneManager.LoadScene("SCN_Kitchen_Review");
                yield return null;
                Assert.IsNull(Object.FindObjectOfType<KitchenLayoutLab>());
                Assert.IsFalse(GameObject.Find("SM_KitchenTable").GetComponent<Renderer>().HasPropertyBlock());
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                InputSystem.settings.editorInputBehaviorInPlayMode = editorFocus;
                InputSystem.settings.backgroundBehavior = background;
            }
        }

        private static Renderer[] EnamelRenderers(KitchenFridge fridge)
        {
            var targets = fridge.GetComponentsInChildren<Renderer>(true)
                .Where(part => part.sharedMaterial.name == "MAT_Furniture_FridgeEnamel").ToArray();
            Assert.AreEqual(4, targets.Length, "Both door and body need their two authored LODs.");
            return targets;
        }

        private static Color InstanceColour(Renderer renderer)
        {
            var properties = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(properties);
            Assert.IsFalse(properties.isEmpty, renderer.name + " must receive the current finish.");
            return properties.GetColor("_BaseColor");
        }

        private static IEnumerator OpenLayoutMenu(Keyboard keyboard, LocomotionSettingsView movement)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F9));
            for (var frame = 0; frame < 8 && !movement.IsOpen; frame++) yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            Assert.IsTrue(movement.IsOpen, "F9 must open the layout menu.");
        }

        private static void ClickWithTrackedRay(LocomotionSettingsView movement, Transform button)
        {
            Canvas.ForceUpdateCanvases();
            var origin = Camera.main.transform.position;
            var target = button.position;
            var pointer = new TrackedDeviceEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                layerMask = -1,
                rayPoints = new List<Vector3> { origin, target + (target - origin).normalized * .2f }
            };
            var hits = new List<RaycastResult>();
            movement.GetComponent<TrackedDeviceGraphicRaycaster>().Raycast(pointer, hits);
            Assert.IsTrue(hits.Any(hit => hit.gameObject == button.gameObject), button.name + " must be reachable by tracked rays.");
            ExecuteEvents.Execute<IPointerClickHandler>(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }

        private static void AssertClosedDoorRouteIsClear()
        {
            var player = GameObject.Find("PREF_XR_KitchenRig").transform;
            var route = new[]
            {
                new Vector3(-2.62f, 0f, .72f),
                new Vector3(-2.90f, 0f, .72f),
                new Vector3(-2.90f, 0f, 1.21f),
                new Vector3(-2.57f, 0f, 1.21f),
                new Vector3(-3.68f, 0f, 1.21f)
            };
            for (var leg = 1; leg < route.Length; leg++)
                for (var step = 0; step <= 24; step++)
                {
                    var position = Vector3.Lerp(route[leg - 1], route[leg], step / 24f);
                    var overlaps = Physics.OverlapCapsule(position + Vector3.up * .12f, position + Vector3.up * 1.34f,
                        .1f, ~0, QueryTriggerInteraction.Ignore)
                        .Where(collider => !collider.transform.IsChildOf(player)).ToArray();
                    Assert.IsEmpty(overlaps, "Blocked kitchen route at " + position + " by " +
                        string.Join(", ", overlaps.Select(collider => collider.name)));
                }
        }

        [UnityTest]
        public IEnumerator OccupiedDestinationLeavesTheCurrentLayoutAndCookingStateIntact()
        {
            SceneManager.LoadScene("SCN_Kitchen_Cooking");
            yield return null;
            var lab = Object.FindObjectOfType<KitchenLayoutLab>();
            var fridge = Object.FindObjectOfType<KitchenFridge>();
            var stove = Object.FindObjectOfType<KitchenHeatSource>();
            var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var originalDock = fridge.transform.position;
            blocker.transform.position = fridge.transform.TransformPoint(new Vector3(0f, .7f, .62f));
            blocker.transform.localScale = Vector3.one * .08f;
            stove.SetOn(true);
            try
            {
                Assert.IsFalse(lab.TryApply(1, out var reason));
                Assert.That(reason, Does.Contain("Move yourself"));
                Assert.AreEqual(0, lab.ActivePresetIndex);
                Assert.AreEqual(originalDock, fridge.transform.position);
                Assert.IsTrue(stove.IsOn, "A refused switch must preserve the current meal.");

                blocker.transform.position = fridge.transform.TransformPoint(new Vector3(.337f, .7f, 0f));
                blocker.transform.localScale = Vector3.one * .015f;
                Assert.IsTrue(lab.TrySelectFridge(true, out reason), reason);
                for (var frame = 0; frame < 3; frame++) yield return null;
                Assert.IsTrue(lab.CompactSelected);
                stove.SetOn(true);
                Assert.IsFalse(lab.TrySelectFridge(false, out reason), "The wider model cannot use the occupied space.");
                Assert.IsTrue(lab.CompactSelected);
                Assert.IsTrue(stove.IsOn);
            }
            finally { Object.Destroy(blocker); }
        }
    }
}
