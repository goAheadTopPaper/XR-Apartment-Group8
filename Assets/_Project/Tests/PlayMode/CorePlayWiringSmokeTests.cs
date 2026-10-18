using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using TMPro;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using XRApartment.Runtime.Core;
using XRApartment.Runtime;
using XRApartment.Runtime.Debugging;
using XRApartment.Runtime.UI;
using XRApartment.Simulation;

public class CorePlayWiringSmokeTests
{
    [UnityTest]
    public IEnumerator OpenStageRejectsEarlyGrabsAndTakeOutRestoreRearmsBothItems()
    {
        SceneManager.LoadScene("SCN_Kitchen_Review");
        yield return null;
        var host = Object.FindObjectOfType<JourneyRuntimeHost>();
        Assert.IsNull(host.StartupError);
        Assert.IsTrue(host.Submit(new JourneyCommand
        { Type = JourneyCommandType.CompleteSetup, PrerequisitesMet = true }).Accepted);
        Assert.IsTrue(host.Submit(new JourneyCommand
        { Type = JourneyCommandType.AcknowledgeOrientation, PrerequisitesMet = true }).Accepted);
        foreach (var id in new[] { "B1", "B2" })
        {
            MoveHead(-3.0f, 1.08f);
            Assert.IsTrue(host.Submit(new JourneyCommand
            { Type = JourneyCommandType.StartChallenge, BenchmarkId = id }).Accepted);
            var tomato = Identity("ITEM_Tomato");
            var onion = Identity("ITEM_Onion");
            tomato.Body.isKinematic = true;
            onion.Body.isKinematic = true;
            MoveHead(-1.90f, 1.05f);
            yield return null;
            Assert.AreEqual(ChallengeStep.Open, host.Core.Snapshot.CurrentStep);
            var filters = new System.Collections.Generic.List<IXRSelectFilter>();
            tomato.Grab.selectFilters.GetAll(filters);
            Assert.IsTrue(filters.Any(filter => !filter.Process(null, tomato.Grab)),
                id + " must reject early task-item selection");
            Identity("ACCESS_FridgeFreezerDoor").GetComponent<KinematicSwingDoor>()
                .SetOpenImmediate(true);
            Identity("ACCESS_FridgeFreshDoor").GetComponent<KinematicSwingDoor>()
                .SetOpenImmediate(true);
            yield return null;
            Assert.AreEqual(ChallengeStep.TakeOut, host.Core.Snapshot.CurrentStep);
            AssertFreezerSourceItems(id + " take-out start");
            filters.Clear();
            tomato.Grab.selectFilters.GetAll(filters);
            Assert.IsTrue(filters.All(filter => filter.Process(null, tomato.Grab)),
                id + " should unlock task items at TakeOut");
            Place(tomato, -3.65f, 1.06f, 1.50f);
            yield return null;
            Assert.AreEqual(1, host.Core.Snapshot.CompletedItemCount);
            var run = host.Core.Snapshot.ActiveRunId;
            Assert.IsTrue(host.Submit(new JourneyCommand
            { Type = JourneyCommandType.RestoreCurrentStep }).Accepted);
            for (var frame = 0; frame < 10 && host.Core.Snapshot.IsBusy; frame++)
                yield return null;
            Assert.IsFalse(host.Core.Snapshot.IsBusy, "step restore callback did not complete");
            Assert.AreEqual(ChallengeStep.TakeOut, host.Core.Snapshot.CurrentStep);
            Assert.AreEqual(0, host.Core.Snapshot.CompletedItemCount,
                Object.FindObjectOfType<JourneyDebugUI>().LastMessage);
            Assert.AreEqual(run, host.Core.Snapshot.ActiveRunId);
            AssertFreezerSourceItems("after restore");
            for (var frame = 0; frame < 3; frame++)
            {
                yield return null;
                Assert.AreEqual(0, host.Core.Snapshot.CompletedItemCount,
                    "restoring the source must not emit TakeOut evidence");
            }
            Place(tomato, -3.65f, 1.06f, 1.50f);
            Place(onion, -3.55f, 1.06f, 1.50f);
            yield return null;
            Assert.AreEqual(ChallengeStep.TemporaryPlacement, host.Core.Snapshot.CurrentStep);
            Assert.IsTrue(host.Submit(new JourneyCommand
            { Type = JourneyCommandType.SkipChallenge, Reason = "Restore smoke" }).Accepted);
            Assert.IsTrue(host.SubmitFeedback(new FeedbackDraft
            { SkipReason = "Restore smoke" }).Accepted);
            var nextState = id == "B1" ? SessionState.Explore : SessionState.ComparisonSummary;
            for (var frame = 0; frame < 60 && host.Core.Snapshot.State != nextState; frame++)
                yield return null;
            Assert.AreEqual(nextState, host.Core.Snapshot.State);
        }
    }

    [UnityTest]
    public IEnumerator PreparationItemsStayOnThePlateUntilChallengeStarts()
    {
        SceneManager.LoadScene("SCN_Kitchen_Review");
        yield return null;
        var host = Object.FindObjectOfType<JourneyRuntimeHost>();
        Assert.IsNull(host.StartupError);
        AssertPreparationPlateItems();
        yield return new WaitForSeconds(.4f);
        AssertPreparationPlateItems();
        Assert.IsTrue(host.Submit(new JourneyCommand
        { Type = JourneyCommandType.CompleteSetup, PrerequisitesMet = true }).Accepted);
        AssertPreparationPlateItems();
        Assert.IsTrue(host.Submit(new JourneyCommand
        { Type = JourneyCommandType.AcknowledgeOrientation, PrerequisitesMet = true }).Accepted);
        foreach (var id in new[] { "B1", "B2" })
        {
            Assert.IsTrue(host.Submit(new JourneyCommand
            { Type = JourneyCommandType.ShowBenchmark, BenchmarkId = id }).Accepted);
            yield return null;
            AssertPreparationPlateItems();
        }
        Assert.IsTrue(host.Submit(new JourneyCommand
        { Type = JourneyCommandType.StartChallenge, BenchmarkId = "B1" }).Accepted);
        yield return null;
        AssertFreezerSourceItems();
        Assert.IsTrue(host.Submit(new JourneyCommand
        { Type = JourneyCommandType.ResetCurrentTask }).Accepted);
        yield return null;
        AssertFreezerSourceItems();
        Assert.IsTrue(host.Submit(new JourneyCommand
        { Type = JourneyCommandType.SkipChallenge, Reason = "Placement smoke" }).Accepted);
        Assert.IsTrue(host.SubmitFeedback(new FeedbackDraft
        { SkipReason = "Placement smoke" }).Accepted);
        for (var frame = 0; frame < 60 && host.Core.Snapshot.State != SessionState.Explore; frame++)
            yield return null;
        Assert.AreEqual(SessionState.Explore, host.Core.Snapshot.State);
        AssertPreparationPlateItems();
        Assert.IsTrue(host.Submit(new JourneyCommand
        { Type = JourneyCommandType.StartChallenge, BenchmarkId = "B2" }).Accepted);
        AssertFreezerSourceItems("B2 start");
    }

    private static void AssertPreparationPlateItems()
    {
        var plate = GameObject.Find("SET_Preparation").transform.Find("PREF_Prop_Plate");
        Assert.NotNull(plate);
        foreach (var id in new[] { "ITEM_Tomato", "ITEM_Onion" })
        {
            var position = Identity(id).transform.position;
            Assert.Less(Vector2.Distance(new Vector2(position.x, position.z),
                new Vector2(plate.position.x, plate.position.z)), .11f, id + " must be on the plate");
            Assert.That(position.y, Is.InRange(plate.position.y, plate.position.y + .14f));
        }
        Assert.Greater(Vector3.Distance(Identity("ITEM_Tomato").transform.position,
            Identity("ITEM_Onion").transform.position), .085f);
    }

    private static void AssertFreezerSourceItems(string context = null)
    {
        var freezer = Identity("ZONE_FreezerSource");
        foreach (var id in new[] { "ITEM_Tomato", "ITEM_Onion" })
        {
            var identity = Identity(id);
            var position = identity.Body != null ? identity.Body.position : identity.transform.position;
            Assert.IsTrue(freezer.ContainsPoint(position),
                context + " " + id + " at " + position + "; freezer " + freezer.Sensor.bounds);
        }
    }

    [UnityTest]
    public IEnumerator ZoneHighlightsFollowTheActiveTaskAndNeverDisableSensors()
    {
        SceneManager.LoadScene("SCN_Kitchen_Review");
        yield return null;
        var host = Object.FindObjectOfType<JourneyRuntimeHost>();
        AssertZoneHighlights();
        Assert.IsTrue(host.Submit(new JourneyCommand
        { Type = JourneyCommandType.CompleteSetup, PrerequisitesMet = true }).Accepted);
        yield return null;
        AssertZoneHighlights();
        Assert.IsTrue(host.Submit(new JourneyCommand
        { Type = JourneyCommandType.AcknowledgeOrientation, PrerequisitesMet = true }).Accepted);
        yield return null;
        AssertZoneHighlights();
        Assert.IsTrue(host.Submit(new JourneyCommand
        { Type = JourneyCommandType.StartChallenge, BenchmarkId = "B1" }).Accepted);
        yield return null;
        yield return null;
        AssertZoneHighlights("ZONE_Approach");
        Assert.IsTrue(host.Submit(new JourneyCommand { Type = JourneyCommandType.Pause }).Accepted);
        yield return null;
        yield return null;
        AssertZoneHighlights();
        Assert.IsTrue(host.Submit(new JourneyCommand { Type = JourneyCommandType.Resume }).Accepted);
        yield return null;
        yield return null;
        AssertZoneHighlights("ZONE_Approach");
        Assert.IsTrue(host.Submit(new JourneyCommand { Type = JourneyCommandType.RequestExit }).Accepted);
        yield return null;
        yield return null;
        AssertZoneHighlights();
    }

    private static void AssertZoneHighlights(params string[] visibleIds)
    {
        foreach (var id in new[]
        {
            "ZONE_Approach", "ZONE_Leave", "ZONE_FreezerSource",
            "ZONE_Temporary", "ZONE_FreshDestination"
        })
        {
            var zone = Identity(id);
            Assert.IsTrue(zone.Sensor.enabled, id + " sensor must remain enabled");
            var hints = zone.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => renderer.name.StartsWith("VIS_ZONE_")).ToArray();
            Assert.IsNotEmpty(hints, id + " needs at least one visual hint");
            foreach (var hint in hints)
                Assert.AreEqual(visibleIds.Contains(id), hint.enabled,
                    id + " should match the active task stage");
        }
    }

    [UnityTest]
    public IEnumerator WorldSpaceSetupButtonAndTaskCardTrackTheJourney()
    {
        SceneManager.LoadScene("SCN_Kitchen_Review");
        yield return null;
        var host = Object.FindObjectOfType<JourneyRuntimeHost>();
        var ui = Object.FindObjectOfType<JourneyWorldSpaceView>();
        Assert.IsNotNull(ui);
        Assert.IsFalse(Object.FindObjectOfType<JourneyDebugUI>().enabled);
        var panel = ui.transform.Find("Panel");
        Assert.That(panel.Find("State").GetComponent<TMP_Text>().text, Does.Contain("Setup"));
        var setup = panel.Find("SetupControls");
        var button = setup.Find("CompleteSetup").GetComponent<Button>();
        button.onClick.Invoke();
        Assert.AreEqual(SessionState.Setup, host.Core.Snapshot.State);
        Assert.That(panel.Find("Error").GetComponent<TMP_Text>().text,
            Does.Contain("Choose a posture"));
        setup.Find("Standing").GetComponent<Button>().onClick.Invoke();
        setup.Find("Calibrate").GetComponent<Button>().onClick.Invoke();
        button.onClick.Invoke();
        Assert.AreEqual(SessionState.Orientation, host.Core.Snapshot.State);
        Assert.IsFalse(setup.gameObject.activeSelf);
        Assert.That(panel.Find("State").GetComponent<TMP_Text>().text, Does.Contain("Orientation"));
        Assert.IsTrue(host.Submit(new JourneyCommand
        { Type = JourneyCommandType.AcknowledgeOrientation, PrerequisitesMet = true }).Accepted);
        Assert.IsTrue(host.Submit(new JourneyCommand
        { Type = JourneyCommandType.StartChallenge, BenchmarkId = "B1" }).Accepted);
        yield return null;
        Assert.AreEqual(SessionState.ChallengeActive, host.Core.Snapshot.State);
        Assert.That(panel.Find("State").GetComponent<TMP_Text>().text, Does.Contain("ChallengeActive"));
        Assert.That(panel.Find("Design").GetComponent<TMP_Text>().text, Does.Contain("B1"));
        Assert.That(panel.Find("Progress").GetComponent<TMP_Text>().text, Does.Contain("1/8"));
        Assert.IsTrue(host.Submit(new JourneyCommand { Type = JourneyCommandType.Pause }).Accepted);
        yield return null;
        Assert.IsEmpty(Object.FindObjectOfType<TaskTargetGuide>().VisibleTargets);
        Assert.IsTrue(host.Submit(new JourneyCommand { Type = JourneyCommandType.Resume }).Accepted);
        Assert.That(panel.Find("State").GetComponent<TMP_Text>().text, Does.Contain("ChallengeActive"));
        Assert.AreEqual(SessionState.ChallengeActive, host.Core.Snapshot.State);
    }

    [UnityTest]
    public IEnumerator SafeExitCanOnlyContinueAsANewTestSession()
    {
        SceneManager.LoadScene("SCN_Kitchen_Review");
        yield return null;
        var host = Object.FindObjectOfType<JourneyRuntimeHost>();
        var oldId = host.Core.Snapshot.SessionId;
        Assert.IsTrue(host.Submit(new JourneyCommand
        { Type = JourneyCommandType.RequestExit }).Accepted);
        Assert.AreEqual(SessionState.Exited, host.Core.Snapshot.State);
        Assert.IsFalse(host.Submit(new JourneyCommand
        { Type = JourneyCommandType.Resume }).Accepted);
        for (var frame = 0; frame < 60 && host.Core.Snapshot.Save == SaveStatus.Pending; frame++)
            yield return null;
        Assert.AreEqual(SaveStatus.Verified, host.Core.Snapshot.Save);
        // The legacy Debug UI is disabled in the integrated scene. A fresh scene load
        // still starts a new session after the exited session has been saved.
        SceneManager.LoadScene("SCN_Kitchen_Review");
        yield return null;
        host = Object.FindObjectOfType<JourneyRuntimeHost>();
        Assert.AreEqual(SessionState.Setup, host.Core.Snapshot.State);
        Assert.AreNotEqual(oldId, host.Core.Snapshot.SessionId);
    }

    [UnityTest]
    public IEnumerator RuntimeBenchmarksFaceTheRoom()
    {
        SceneManager.LoadScene("SCN_Kitchen_Review");
        yield return null;
        var host = Object.FindObjectOfType<JourneyRuntimeHost>();
        Assert.IsNull(host.StartupError);
        Assert.IsTrue(host.Submit(new JourneyCommand
        { Type = JourneyCommandType.CompleteSetup, PrerequisitesMet = true }).Accepted);
        Assert.IsTrue(host.Submit(new JourneyCommand
        { Type = JourneyCommandType.AcknowledgeOrientation, PrerequisitesMet = true }).Accepted);
        foreach (var id in new[] { "B1", "B2" })
        {
            Assert.IsTrue(host.Submit(new JourneyCommand
            { Type = JourneyCommandType.ShowBenchmark, BenchmarkId = id }).Accepted);
            yield return null;
            var door = Identity("ACCESS_FridgeFreshDoor").transform;
            Assert.Less(door.position.x, -2.1f, id + " fresh door must face the room");
            Assert.Less(door.position.z, 1.8f, id + " fresh door must not face the wall");
        }
    }

    [UnityTest]
    public IEnumerator FreezerSourceAndFreshDestinationAreDistinctInBothBenchmarks()
    {
        SceneManager.LoadScene("SCN_Kitchen_Review");
        yield return null;
        var host = Object.FindObjectOfType<JourneyRuntimeHost>();
        Assert.IsNull(host.StartupError);
        Assert.IsTrue(host.Submit(new JourneyCommand
        { Type = JourneyCommandType.CompleteSetup, PrerequisitesMet = true }).Accepted);
        Assert.IsTrue(host.Submit(new JourneyCommand
        { Type = JourneyCommandType.AcknowledgeOrientation, PrerequisitesMet = true }).Accepted);
        foreach (var id in new[] { "B1", "B2" })
        {
            Assert.IsTrue(host.Submit(new JourneyCommand
            { Type = JourneyCommandType.ShowBenchmark, BenchmarkId = id }).Accepted);
            yield return null;
            var freezer = Identity("ZONE_FreezerSource");
            var fresh = Identity("ZONE_FreshDestination");
            Assert.Greater(freezer.transform.position.y, fresh.transform.position.y);
            Assert.AreEqual(3, fresh.GetComponents<Collider>().Length,
                id + " must accept each of the three Fresh shelves");
            foreach (var sensor in fresh.GetComponents<Collider>())
                Assert.IsTrue(fresh.ContainsPoint(sensor.bounds.center));
            Assert.IsFalse(fresh.ContainsPoint(freezer.transform.position),
                id + " must not count Freezer as a Fresh destination");
            AssertPreparationPlateItems();
            Assert.NotNull(Identity("ACCESS_FridgeFreezerDoor"));
            Assert.NotNull(Identity("ACCESS_FridgeFreshDoor"));
        }
    }

    [UnityTest]
    public IEnumerator CounterHintIsAboveTheCounterAndZoneHintsAreTransparent()
    {
        SceneManager.LoadScene("SCN_Kitchen_Review");
        yield return null;
        var temporary = Identity("ZONE_Temporary");
        var hint = temporary.transform.Find("VIS_ZONE_Temporary");
        Assert.NotNull(hint);
        Assert.Greater(hint.position.y, 1.01f,
            "The purple hint must not be hidden inside the 1.00 m counter");
        Assert.Less(hint.localScale.y, .004f, "The hint should be a surface, not a board");
        Assert.IsNull(hint.GetComponent<Collider>());
        Assert.Less(hint.GetComponent<Renderer>().sharedMaterial.color.a, .5f);
        Assert.GreaterOrEqual(hint.GetComponent<Renderer>().sharedMaterial.renderQueue, 3000);
    }

    [UnityTest]
    public IEnumerator FridgeStorageIsVisibleAndMatchesTheTaskZoneInBothBenchmarks()
    {
        SceneManager.LoadScene("SCN_Kitchen_Review");
        yield return null;
        var host = Object.FindObjectOfType<JourneyRuntimeHost>();
        Assert.IsNull(host.StartupError);
        Assert.IsTrue(host.Submit(new JourneyCommand
        { Type = JourneyCommandType.CompleteSetup, PrerequisitesMet = true }).Accepted);
        Assert.IsTrue(host.Submit(new JourneyCommand
        { Type = JourneyCommandType.AcknowledgeOrientation, PrerequisitesMet = true }).Accepted);
        foreach (var id in new[] { "B1", "B2" })
        {
            Assert.IsTrue(host.Submit(new JourneyCommand
            { Type = JourneyCommandType.ShowBenchmark, BenchmarkId = id }).Accepted);
            yield return null;
            var fridge = Object.FindObjectsOfType<Transform>()
                .Single(transform => transform.name == "PREF_Fridge_F2");
            var interior = fridge.Find("INTERIOR_Fridge");
            Assert.NotNull(interior, id + " needs a visible storage cavity");
            Assert.NotNull(interior.GetComponentInChildren<Light>(), id + " interior is unlit");
            Assert.GreaterOrEqual(interior.GetComponentsInChildren<BoxCollider>().Length, 4);
            var back = fridge.Find("BODY_Fridge");
            Assert.Less(back.localScale.z, .1f, id + " solid body blocks the open door");
            var zone = Identity("ZONE_FreezerSource");
            var localZone = fridge.InverseTransformPoint(zone.transform.position);
            Assert.Less(Mathf.Abs(localZone.x), .3f);
            Assert.That(localZone.z, Is.InRange(-.25f, .32f));
            var fresh = Identity("ZONE_FreshDestination");
            Assert.AreEqual(3, fresh.GetComponents<BoxCollider>().Length);
            foreach (var shelfName in new[] { "SHELF_Fresh_Lower", "SHELF_Fresh_Task" })
            {
                var shelf = GameObject.Find(shelfName).GetComponent<Collider>().bounds;
                foreach (var x in new[] { shelf.min.x + .03f, shelf.max.x - .03f })
                    foreach (var z in new[] { shelf.min.z + .03f, shelf.max.z - .03f })
                        Assert.IsTrue(fresh.ContainsPoint(new Vector3(x, shelf.max.y + .1f, z)),
                            id + " " + shelfName + " visible shelf edge must count as Fresh");
            }
            AssertPreparationPlateItems();
        }
    }

    [UnityTest]
    public IEnumerator OpenFridgeStorageIsVisibleFromOutsideInBothBenchmarks()
    {
        SceneManager.LoadScene("SCN_Kitchen_Review");
        yield return null;
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            Assert.Ignore("Exterior visibility needs a graphics device; run PlayMode without -nographics.");
        var host = Object.FindObjectOfType<JourneyRuntimeHost>();
        Assert.IsNull(host.StartupError);
        Assert.IsTrue(host.Submit(new JourneyCommand
        { Type = JourneyCommandType.CompleteSetup, PrerequisitesMet = true }).Accepted);
        Assert.IsTrue(host.Submit(new JourneyCommand
        { Type = JourneyCommandType.AcknowledgeOrientation, PrerequisitesMet = true }).Accepted);
        foreach (var id in new[] { "B1", "B2" })
        {
            Assert.IsTrue(host.Submit(new JourneyCommand
            { Type = JourneyCommandType.ShowBenchmark, BenchmarkId = id }).Accepted);
            yield return null;
            var fridge = Object.FindObjectsOfType<Transform>()
                .Single(transform => transform.name == "PREF_Fridge_F2");
            Assert.IsNull(fridge.Find("OPENING_FridgeDoor_Fresh"));
            Assert.IsNull(fridge.Find("OPENING_FridgeDoor_Freezer"));
            Identity("ACCESS_FridgeFreshDoor").GetComponent<KinematicSwingDoor>().Toggle();
            Identity("ACCESS_FridgeFreezerDoor").GetComponent<KinematicSwingDoor>().Toggle();
            yield return new WaitForSeconds(1.2f);
            AssertExteriorBrightness(fridge, 1.02f, id + " Fresh");
            AssertExteriorBrightness(fridge, 1.57f, id + " Freezer");
        }
    }

    private static void AssertExteriorBrightness(Transform fridge, float height, string label)
    {
        var cameraObject = new GameObject("Fridge exterior visibility test camera");
        var camera = cameraObject.AddComponent<Camera>();
        camera.enabled = false;
        camera.fieldOfView = 44f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.transform.position = fridge.TransformPoint(new Vector3(0f, height, 1.15f));
        camera.transform.LookAt(fridge.TransformPoint(new Vector3(0f, height, -.1f)));
        var target = RenderTexture.GetTemporary(256, 256, 24);
        var pixels = new Texture2D(256, 256, TextureFormat.RGB24, false);
        var oldTarget = RenderTexture.active;
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
            pixels.Apply();
            var colours = pixels.GetPixels(96, 96, 64, 64);
            var brightness = colours.Average(colour => (colour.r + colour.g + colour.b) / 3f);
            Assert.Greater(brightness, .35f,
                label + " fridge interior remains dark from outside: " + brightness);
        }
        finally
        {
            RenderTexture.active = oldTarget;
            camera.targetTexture = null;
            RenderTexture.ReleaseTemporary(target);
            Object.Destroy(pixels);
            Object.Destroy(cameraObject);
        }
    }

    [UnityTest]
    public IEnumerator SensorDrivenEightStepsCompleteBothBenchmarks()
    {
        SceneManager.LoadScene("SCN_Kitchen_Review");
        yield return null;
        var host = Object.FindObjectOfType<JourneyRuntimeHost>();
        Assert.IsNull(host.StartupError);
        Assert.IsTrue(host.Submit(new JourneyCommand
        { Type = JourneyCommandType.CompleteSetup, PrerequisitesMet = true }).Accepted);
        Assert.IsTrue(host.Submit(new JourneyCommand
        { Type = JourneyCommandType.AcknowledgeOrientation, PrerequisitesMet = true }).Accepted);
        foreach (var id in new[] { "B1", "B2" })
        {
            MoveHead(-3.0f, 1.08f);
            Assert.IsTrue(host.Submit(new JourneyCommand
            { Type = JourneyCommandType.StartChallenge, BenchmarkId = id }).Accepted);
            yield return null;
            Assert.AreEqual(ChallengeStep.Approach, host.Core.Snapshot.CurrentStep);
            yield return null;
            AssertZoneHighlights("ZONE_Approach");
            yield return RestoreAtStep(host, ChallengeStep.Approach);
            var items = new[] { Identity("ITEM_Tomato"), Identity("ITEM_Onion"), Identity("ITEM_CerealBox") };
            foreach (var item in items) item.Body.isKinematic = true;
            MoveHead(-1.90f, 1.05f);
            yield return null;
            Assert.AreEqual(ChallengeStep.Open, host.Core.Snapshot.CurrentStep);
            yield return RestoreAtStep(host, ChallengeStep.Open);
            yield return null;
            AssertZoneHighlights();
            Assert.IsTrue(Object.FindObjectOfType<TaskTargetGuide>().VisibleTargets
                .Contains("ACCESS_FridgeFreezerDoor"));
            var arrow = GameObject.Find("GUIDE_Green_DownTriangle");
            Assert.NotNull(arrow);
            Assert.IsNull(arrow.GetComponent<Collider>());
            Assert.Greater(arrow.GetComponent<MeshFilter>().sharedMesh.normals[0].z, 0f,
                "The triangle front face must point toward the viewer");
            Assert.NotNull(arrow.GetComponent<MeshRenderer>().sharedMaterial);
            Identity("ACCESS_FridgeFreezerDoor").GetComponent<KinematicSwingDoor>().Toggle();
            Identity("ACCESS_FridgeFreshDoor").GetComponent<KinematicSwingDoor>().Toggle();
            yield return new WaitForSeconds(1.2f);
            Assert.AreEqual(ChallengeStep.TakeOut, host.Core.Snapshot.CurrentStep);
            yield return RestoreAtStep(host, ChallengeStep.TakeOut);
            yield return null;
            AssertZoneHighlights("ZONE_FreezerSource");
            Assert.IsTrue(Object.FindObjectOfType<TaskTargetGuide>().VisibleTargets.Contains("ITEM_Tomato"));
            Place(items[0], -3.65f, 1.06f, 1.50f);
            Place(items[1], -3.55f, 1.06f, 1.50f);
            for (var frame = 0; frame < 10 &&
                host.Core.Snapshot.CurrentStep == ChallengeStep.TakeOut; frame++)
                yield return null;
            Assert.AreEqual(ChallengeStep.TemporaryPlacement, host.Core.Snapshot.CurrentStep);
            yield return RestoreAtStep(host, ChallengeStep.TemporaryPlacement);
            yield return null;
            AssertZoneHighlights("ZONE_Temporary");
            Place(items[0], -3.50f, 1.06f, 1.94f);
            Place(items[1], -3.36f, 1.06f, 1.94f);
            yield return new WaitForSeconds(.35f);
            Assert.AreEqual(ChallengeStep.Reorganise, host.Core.Snapshot.CurrentStep);
            yield return RestoreAtStep(host, ChallengeStep.Reorganise);
            yield return null;
            AssertZoneHighlights("ZONE_Temporary");
            Place(items[0], -1.99f, 1.46f, 1.80f);
            yield return new WaitForSeconds(.35f);
            Assert.AreEqual(ChallengeStep.Reorganise, host.Core.Snapshot.CurrentStep,
                "Returning to Freezer is not a Fresh reorganisation");
            var shelf = GameObject.Find("SHELF_Fresh_Lower").GetComponent<Collider>().bounds;
            var shelfFront = new Vector3(shelf.max.x - .03f, shelf.max.y + .1f,
                shelf.min.z + .04f);
            Assert.That(shelfFront.z, Is.InRange(shelf.min.z, shelf.max.z));
            Place(items[0], shelfFront.x, shelfFront.y, shelfFront.z);
            yield return new WaitForSeconds(.35f);
            var reorganisation = host.Submit(new JourneyCommand
            { Type = JourneyCommandType.ConfirmReorganisation });
            Assert.IsTrue(reorganisation.Accepted,
                reorganisation.ErrorCode + ": " + reorganisation.Message);
            AssertZoneHighlights();
            Assert.AreEqual(ChallengeStep.PutBack, host.Core.Snapshot.CurrentStep);
            yield return RestoreAtStep(host, ChallengeStep.PutBack);
            yield return null;
            AssertZoneHighlights("ZONE_FreshDestination");
            Place(items[1], -1.76f, .68f, 1.80f);
            yield return new WaitForSeconds(.35f);
            Assert.AreEqual(1, host.Core.Snapshot.CompletedItemCount,
                "One correctly placed item should be counted before testing shelf-resting poses");
            AssertZoneHighlights("ZONE_FreshDestination");
            PlaceRestingOnShelf(items[0], "SHELF_Fresh_Task");
            yield return new WaitForSeconds(.35f);
            Assert.AreEqual(2, host.Core.Snapshot.CompletedItemCount,
                "A released tomato resting on the upper Fresh shelf must increment PutBack");
            AssertZoneHighlights("ZONE_FreshDestination");
            PlaceRestingOnShelf(items[2], "SHELF_Fresh_Lower");
            yield return new WaitForSeconds(.35f);
            Assert.AreEqual(ChallengeStep.Close, host.Core.Snapshot.CurrentStep);
            yield return RestoreAtStep(host, ChallengeStep.Close);
            yield return null;
            AssertZoneHighlights();
            Identity("ACCESS_FridgeFreshDoor").GetComponent<KinematicSwingDoor>().Toggle();
            Identity("ACCESS_FridgeFreezerDoor").GetComponent<KinematicSwingDoor>().Toggle();
            yield return new WaitForSeconds(1.2f);
            Assert.AreEqual(ChallengeStep.Leave, host.Core.Snapshot.CurrentStep);
            yield return RestoreAtStep(host, ChallengeStep.Leave);
            yield return null;
            AssertZoneHighlights("ZONE_Leave");
            MoveHead(-3.0f, 1.08f);
            yield return null;
            Assert.AreEqual(SessionState.FeedbackPending, host.Core.Snapshot.State);
            yield return null;
            AssertZoneHighlights();
            Assert.IsTrue(host.SubmitFeedback(new FeedbackDraft
            {
                Decision = XRApartment.Data.CombinationDecision.Accept,
                Category = XRApartment.Data.FeedbackCategory.OperationalSpace,
                Importance = XRApartment.Data.Importance.Medium,
                ShortExplanation = "Sensor-driven Play smoke"
            }).Accepted);
            yield return null;
            yield return null;
            Assert.AreEqual(id == "B1" ? SessionState.Explore : SessionState.ComparisonSummary,
                host.Core.Snapshot.State);
        }
        Assert.IsTrue(host.Submit(new JourneyCommand
        {
            Type = JourneyCommandType.ConfirmComparison,
            Comparison = new FinalComparison
            { PreferredBenchmarkId = "B1", TradeOffSummary = "B1 versus B2 sensor run" }
        }).Accepted);
        yield return new WaitForSeconds(.1f);
        Assert.AreEqual(SessionState.Finished, host.Core.Snapshot.State);
        Assert.AreEqual(SaveStatus.Verified, host.Core.Snapshot.Save);
        Assert.AreEqual(XRApartment.Data.SessionOutcome.Completed, host.Core.Snapshot.Outcome);
    }

    private static TaskEvidenceIdentity Identity(string id) =>
        Object.FindObjectsOfType<TaskEvidenceIdentity>().Single(item => item.StableId == id);

    private static IEnumerator RestoreAtStep(JourneyRuntimeHost host, ChallengeStep step)
    {
        Assert.AreEqual(step, host.Core.Snapshot.CurrentStep);
        var run = host.Core.Snapshot.ActiveRunId;
        var head = GameObject.Find("PREF_XR_KitchenRig")
            .GetComponentInChildren<Camera>().transform.position;
        Assert.IsTrue(host.Submit(new JourneyCommand
        { Type = JourneyCommandType.RestoreCurrentStep }).Accepted);
        for (var frame = 0; frame < 10 && host.Core.Snapshot.IsBusy; frame++)
            yield return null;
        Assert.IsFalse(host.Core.Snapshot.IsBusy,
            step + ": " + Object.FindObjectOfType<JourneyDebugUI>().LastMessage);
        Assert.AreEqual(step, host.Core.Snapshot.CurrentStep);
        Assert.AreEqual(run, host.Core.Snapshot.ActiveRunId);
        Assert.That(Vector3.Distance(head, GameObject.Find("PREF_XR_KitchenRig")
            .GetComponentInChildren<Camera>().transform.position), Is.LessThan(.001f));
    }

    private static void Place(TaskEvidenceIdentity item, float x, float y, float z)
    {
        item.transform.position = new Vector3(x, y, z);
        Physics.SyncTransforms();
    }

    private static void PlaceRestingOnShelf(TaskEvidenceIdentity item, string shelfName)
    {
        var shelf = GameObject.Find(shelfName).GetComponent<Collider>().bounds;
        var itemCollider = item.GetComponentInChildren<Collider>();
        Assert.NotNull(itemCollider, item.StableId + " needs a physical collider");
        var restingY = item.transform.position.y + shelf.max.y + .005f - itemCollider.bounds.min.y;
        Place(item, shelf.center.x, restingY, shelf.center.z);
    }

    private static void MoveHead(float x, float z)
    {
        var rig = GameObject.Find("PREF_XR_KitchenRig").transform;
        var head = rig.GetComponentInChildren<Camera>().transform;
        rig.position += new Vector3(x - head.position.x, 0f, z - head.position.z);
        Physics.SyncTransforms();
    }

    [UnityTest]
    public IEnumerator SkipReasonUsesRecordedCauseWithEmptyChangedAndMatchingDrafts()
    {
        foreach (var draftReason in new[] { null, "changed later", "comfort stop" })
        {
            SceneManager.LoadScene("SCN_Kitchen_Review");
            yield return null;
            var host = Object.FindObjectOfType<JourneyRuntimeHost>();
            Assert.IsNull(host.StartupError);
            Assert.IsTrue(host.Submit(new JourneyCommand
            { Type = JourneyCommandType.CompleteSetup, PrerequisitesMet = true }).Accepted);
            Assert.IsTrue(host.Submit(new JourneyCommand
            { Type = JourneyCommandType.AcknowledgeOrientation, PrerequisitesMet = true }).Accepted);
            Assert.IsTrue(host.Submit(new JourneyCommand
            { Type = JourneyCommandType.StartChallenge, BenchmarkId = "B1" }).Accepted);
            yield return null;
            Assert.IsTrue(host.Submit(new JourneyCommand
            { Type = JourneyCommandType.SkipChallenge, Reason = "comfort stop" }).Accepted);
            var result = host.SubmitFeedback(new FeedbackDraft { SkipReason = draftReason });
            Assert.IsTrue(result.Accepted, "draft reason=" + (draftReason ?? "<null>") + ": " + result.Message);
            Assert.AreEqual("comfort stop", result.Action.Record.FindFeedback("B1").outcomeReason);
        }
    }

    [UnityTest]
    public IEnumerator HostStartsAndLoadsBothBenchmarks()
    {
        SceneManager.LoadScene("SCN_Kitchen_Review");
        yield return null;
        var host = Object.FindObjectOfType<JourneyRuntimeHost>();
        Assert.NotNull(host);
        Assert.IsNull(host.StartupError);
        Assert.NotNull(host.Core);
        Assert.IsTrue(host.Submit(new JourneyCommand
        { Type = JourneyCommandType.CompleteSetup, PrerequisitesMet = true }).Accepted);
        Assert.IsTrue(host.Submit(new JourneyCommand
        { Type = JourneyCommandType.AcknowledgeOrientation, PrerequisitesMet = true }).Accepted);
        Assert.IsTrue(host.Submit(new JourneyCommand
        { Type = JourneyCommandType.ShowBenchmark, BenchmarkId = "B1" }).Accepted);
        yield return null;
        Assert.AreEqual("B1", host.GetComponent<RuntimeBenchmarkAdapter>().CurrentBenchmarkId);
        Assert.IsTrue(host.Submit(new JourneyCommand
        { Type = JourneyCommandType.StartChallenge, BenchmarkId = "B1" }).Accepted);
        yield return null;
        Assert.AreEqual(SessionState.ChallengeActive, host.Core.Snapshot.State);
        Assert.AreEqual(ChallengeStep.Approach, host.Core.Snapshot.CurrentStep);
        Assert.IsTrue(host.Submit(new JourneyCommand
        { Type = JourneyCommandType.SkipChallenge, Reason = "Play smoke" }).Accepted);
        Assert.AreEqual(SessionState.FeedbackPending, host.Core.Snapshot.State);
        Assert.IsTrue(host.SubmitFeedback(new FeedbackDraft
        { SkipReason = "Play smoke" }).Accepted);
        yield return null;
        yield return null;
        Assert.AreEqual(SessionState.Explore, host.Core.Snapshot.State);
        Assert.IsTrue(host.Submit(new JourneyCommand
        { Type = JourneyCommandType.ShowBenchmark, BenchmarkId = "B2" }).Accepted);
        yield return null;
        Assert.AreEqual("B2", host.GetComponent<RuntimeBenchmarkAdapter>().CurrentBenchmarkId);
    }
}
