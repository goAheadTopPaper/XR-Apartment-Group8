using System.Collections;
using System.Linq;
using NUnit.Framework;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using XRApartment.Runtime;
using XRApartment.Runtime.UI;
using XRApartment.Tests;

public class CorePlayWiringSmokeTests
{
    [UnityTest]
    public IEnumerator DefaultCookingSceneStartsDirectlyWithNoSecondaryReviewEntry()
    {
        SceneManager.LoadScene(0);
        yield return null;
        Assert.AreEqual("SCN_Kitchen_Cooking", SceneManager.GetActiveScene().name);
        if (!UnityEngine.XR.XRSettings.isDeviceActive)
            Assert.AreEqual(XROrigin.TrackingOriginMode.NotSpecified,
                Object.FindObjectOfType<XROrigin>().RequestedTrackingOriginMode);
        var view = Object.FindObjectOfType<LocomotionSettingsView>();
        KitchenSceneTestSetup.DisableStartupGuide(view);
        view.Open();
        yield return null;
        var entry = view.transform.Find("Panel/EnterReview");
        Assert.IsTrue(entry == null || !entry.gameObject.activeInHierarchy,
            "Single-scene build must hide the formal review scene switch.");
        view.Close();
    }

    [UnityTest]
    public IEnumerator LowProbesLightTheFloorAndVirtualRigRecoversInsideTheRoom()
    {
        SceneManager.LoadScene("SCN_Kitchen_Cooking");
        yield return null;
        yield return null;
        KitchenSceneTestSetup.DisableStartupGuide(Object.FindObjectOfType<LocomotionSettingsView>());
        var light = new Color[1];
        var prop = GameObject.Find("PREF_Cooking_FryingPan");
        Assert.IsNotNull(prop);
        var body = prop.GetComponent<Rigidbody>();
        body.position = new Vector3(-3.95f, .19f, 1.2f);
        body.rotation = Quaternion.identity;
        body.velocity = body.angularVelocity = Vector3.zero;
        Physics.SyncTransforms();
        yield return new WaitForSeconds(.55f);
        Assert.That(body.worldCenterOfMass.y, Is.LessThan(.12f), "Cooking prop must reach the floor.");
        var renderer = prop.GetComponentsInChildren<MeshRenderer>().FirstOrDefault(item => item.probeAnchor != null);
        Assert.IsNotNull(renderer, "Cooking prop must sample indirect light above the floor.");
        LightProbes.GetInterpolatedProbe(renderer.probeAnchor.position, renderer, out SphericalHarmonicsL2 probe);
        probe.Evaluate(new[] { Vector3.up }, light);
        Assert.That(light[0].grayscale, Is.GreaterThan(.025f), "Dropped prop lacks indirect light.");
        var settings = Object.FindObjectOfType<KitchenLocomotionSettings>();
        var player = settings.Origin.Origin.transform;
        var safePosition = player.position;
        player.position += Vector3.down * 2f;
        yield return null;
        yield return null;
        Assert.That(Vector3.Distance(player.position, safePosition), Is.LessThan(.05f));
        var envelope = Object.FindObjectOfType<KitchenRoomBounds>();
        Assert.AreEqual(5, envelope.GetComponentsInChildren<BoxCollider>().Length);
    }

    [UnityTest]
    public IEnumerator CookingLightsFollowThePendantAndWorkbench()
    {
        SceneManager.LoadScene("SCN_Kitchen_Cooking");
        yield return null;
        var dining = GameObject.Find("PREF_Furniture_DiningSet").transform;
        var pendant = dining.Find("PREF_Furniture_Pendant");
        var bulb = pendant.Find("VIS_OpalBulb").GetComponent<Renderer>();
        var lamp = pendant.Find("LGT_DiningWarm").GetComponent<Light>();
        var emitter = lamp.transform.position;
        var beneathBulb = bulb.bounds.min.y - emitter.y;
        Assert.That(beneathBulb, Is.InRange(.001f, .02f));
        Assert.That(Vector3.ProjectOnPlane(emitter - bulb.bounds.center, Vector3.up).magnitude, Is.LessThan(.01f));
        Assert.That(Vector3.ProjectOnPlane(emitter - dining.Find("PREF_Furniture_DiningTable").position,
            Vector3.up).magnitude, Is.LessThan(.01f));
        Assert.That(Vector3.Dot(lamp.transform.forward, Vector3.down), Is.GreaterThan(.99f));

        var fill = GameObject.Find("PREF_Lighting_CookingFill").transform;
        var taskLight = fill.GetComponentsInChildren<Light>().Single();
        var workbench = GameObject.Find("PREF_Cooking_Workbench").transform;
        var centre = (workbench.Find("PREF_Cooking_ChoppingBoard").position +
                      workbench.Find("LOC_BurnerSurface").position) * .5f;
        Assert.AreEqual(HDLightTypeAndShape.RectangleArea, taskLight.GetComponent<HDAdditionalLightData>().GetLightTypeAndShape());
        Assert.That(Vector3.Dot(taskLight.transform.forward, Vector3.down), Is.GreaterThan(.99f));
        Assert.That(Vector3.ProjectOnPlane(taskLight.transform.position - centre, Vector3.up).magnitude,
            Is.LessThan(.01f));
        Assert.IsTrue(lamp.bakingOutput.isBaked && taskLight.bakingOutput.isBaked);
    }

    [UnityTest]
    public IEnumerator HandsKeepIndirectLightInTheEntryPassage()
    {
        var directions = new[] { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
        var samples = new Color[directions.Length];
        SceneManager.LoadScene("SCN_Kitchen_Cooking");
        yield return null;
        var origin = Object.FindObjectOfType<XROrigin>();
        // The fixture supplies poses without a tracked controller device.
        origin.GetComponentInChildren<XRInputModalityManager>(true).enabled = false;
        var hands = origin.GetComponentsInChildren<ControllerHandPresentation>(true);
        Assert.AreEqual(2, hands.Length);
        foreach (var hand in hands)
        {
            for (var node = hand.transform; node != origin.transform; node = node.parent)
                node.gameObject.SetActive(true);
            var renderer = hand.transform.Find("Solid Palm").GetComponentInChildren<SkinnedMeshRenderer>();
            Assert.AreSame(origin.Camera.transform, renderer.probeAnchor, hand.name);
        }

        foreach (var y in new[] { 1.2f, 1.65f })
            foreach (var z in new[] { -6.55f, -4.70f, -2.70f, -.70f, 1.80f })
            {
                var point = new Vector3(-.42f, y, z);
                LightProbes.GetInterpolatedProbe(point, null, out SphericalHarmonicsL2 light);
                light.Evaluate(directions, samples);
                Assert.That(samples.Average(colour => colour.grayscale), Is.GreaterThan(.08f),
                    "Cooking scene has a dark gap at " + point.ToString("F2"));
            }
    }

    [UnityTest]
    public IEnumerator LanguageMenuUpdatesCookingInMainScene()
    {
        var saved = KitchenLocale.Current;
        try
        {
            KitchenLocale.Select(KitchenLocale.Language.English);
            SceneManager.LoadScene("SCN_Kitchen_Cooking");
            yield return null;
            yield return null;
            var menu = Object.FindObjectOfType<LocomotionSettingsView>();
            KitchenSceneTestSetup.ChooseStartupLanguage(menu);
            var lab = menu.GetComponentInChildren<KitchenLayoutMenu>(true);
            lab.gameObject.SetActive(true);
            var layoutStatus = lab.transform.Find("Status").GetComponent<TMP_Text>();
            var workbench = GameObject.Find("PREF_Cooking_Workbench");
            var worktopHint = workbench.transform.Find("UI_CookingInstructions/Action").GetComponent<TMP_Text>();
            StringAssert.Contains("Opposite the sink", layoutStatus.text);

            menu.Open();
            yield return null;
            StringAssert.Contains("opposite the sink", worktopHint.text);
            LanguageSelectionView.Show();
            yield return null;
            GameObject.Find("Simplified Chinese").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(KitchenLocale.Language.SimplifiedChinese, KitchenLocale.Current);
            var title = menu.transform.Find("Panel/Title").GetComponent<TMP_Text>();
            Assert.AreEqual("移动设置", title.text);
            title.ForceMeshUpdate();
            Assert.AreEqual('移', title.textInfo.characterInfo[0].character);
            var hint = workbench.transform.Find("UI_FridgeInstructions/Action").GetComponent<TMP_Text>();
            StringAssert.Contains("把手", hint.text);
            StringAssert.Contains("水槽对面", worktopHint.text);
            StringAssert.Contains("冰箱在水槽对面", layoutStatus.text);
            worktopHint.ForceMeshUpdate();
            Assert.IsTrue(worktopHint.textInfo.characterInfo.Take(worktopHint.textInfo.characterCount)
                .Any(character => character.character == '槽'));

            LanguageSelectionView.Show();
            yield return null;
            GameObject.Find("Traditional Chinese").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual("移動設置", menu.transform.Find("Panel/Title").GetComponent<TMP_Text>().text);
            menu.Close();
        }
        finally
        {
            KitchenLocale.Select(saved);
        }
    }

    [UnityTest]
    public IEnumerator KitchenAndDiningWindowsFillTheOpeningsInMainScene()
    {
        SceneManager.LoadScene("SCN_Kitchen_Cooking");
        yield return null;
        KitchenSceneTestSetup.DisableStartupGuide(Object.FindObjectOfType<LocomotionSettingsView>());
        var environment = GameObject.Find("PREF_Kitchen_Environment").transform;
        var windows = environment.Find("PREF_Kitchen_Windows");
        Assert.NotNull(windows);
        Assert.NotNull(windows.Find("Window_Dining/Frame_Mullion"));
        foreach (var (path, centre) in new[] {
            ("Window_Sink/Glass", new Vector3(-5.174f, 1.935f, 1.448f)),
            ("Window_Dining/Glass_Left", new Vector3(-5.125f, 1.76f, -1.40f)),
            ("Window_Dining/Glass_Right", new Vector3(-5.125f, 1.76f, -.32f)) })
        {
            var pane = windows.Find(path);
            Assert.NotNull(pane, path);
            var collider = pane.GetComponent<BoxCollider>();
            Assert.IsTrue(collider.Raycast(new Ray(centre + Vector3.right * .35f, Vector3.left),
                out _, .5f), path + " should close the exterior opening");
            var material = pane.GetComponent<MeshRenderer>().sharedMaterial;
            Assert.That(material.color.a, Is.InRange(.23f, .35f));
            Assert.GreaterOrEqual(material.renderQueue, 3000);
        }
    }
}
