using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit.UI;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.Interaction.Toolkit.Attachment;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using XRApartment.Runtime.Core;
using XRApartment.Runtime.Feedback;
using XRApartment.Runtime;
using XRApartment.Runtime.Debugging;
using XRApartment.Runtime.Storage;
using XRApartment.Runtime.UI;
using XRApartment.Data;
using XRApartment.Simulation;

public class CorePlayWiringSmokeTests
{
    [UnityTest]
    public IEnumerator DefaultCookingSceneOffersAnXRReachableEntryToTheFormalReview()
    {
        SceneManager.LoadScene(0);
        yield return null;
        Assert.AreEqual("SCN_Kitchen_Cooking", SceneManager.GetActiveScene().name);
        if (!UnityEngine.XR.XRSettings.isDeviceActive)
            Assert.AreEqual(XROrigin.TrackingOriginMode.NotSpecified,
                Object.FindObjectOfType<XROrigin>().RequestedTrackingOriginMode);
        var view = Object.FindObjectOfType<LocomotionSettingsView>();
        view.Open();
        yield return null;
        Canvas.ForceUpdateCanvases();
        var entry = view.transform.Find("Panel/EnterReview").GetComponent<Button>();
        Assert.IsTrue(entry.isActiveAndEnabled && entry.interactable);
        var start = Camera.main.transform.position;
        var aim = entry.transform.position;
        var pointer = new TrackedDeviceEventData(EventSystem.current)
        {
            button = PointerEventData.InputButton.Left,
            layerMask = -1,
            rayPoints = new List<Vector3> { start, aim + (aim - start).normalized * .1f }
        };
        var hits = new List<RaycastResult>();
        view.GetComponent<TrackedDeviceGraphicRaycaster>().Raycast(pointer, hits);
        Assert.IsTrue(hits.Any(hit => hit.gameObject == entry.gameObject));
        ExecuteEvents.Execute<IPointerClickHandler>(entry.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        yield return null;
        Assert.AreEqual("SCN_Kitchen_Review", SceneManager.GetActiveScene().name);
        var host = Object.FindObjectOfType<JourneyRuntimeHost>();
        Assert.IsNull(host.StartupError);
        Assert.AreEqual(SessionState.Setup, host.Core.Snapshot.State);
        Assert.IsNotNull(host.GetComponent<ReviewBoundarySafety>());
        if (!UnityEngine.XR.XRSettings.isDeviceActive)
        {
            Assert.AreEqual(XROrigin.TrackingOriginMode.Floor,
                Object.FindObjectOfType<XROrigin>().RequestedTrackingOriginMode);
            yield return new WaitForSecondsRealtime(.15f);
            Assert.IsNull(host.Core.Snapshot.CurrentSafety, "Simulation has no physical play area.");
            Assert.IsNull(GameObject.Find("Physical Play Area Warning"));
        }
        var reviewMenu = Object.FindObjectOfType<LocomotionSettingsView>();
        reviewMenu.Open();
        Assert.IsFalse(reviewMenu.transform.Find("Panel/EnterReview").gameObject.activeInHierarchy);
        reviewMenu.Close();
    }

    [UnityTest]
    public IEnumerator LowProbesLightTheFloorAndVirtualRigRecoversInsideTheRoom()
    {
        foreach (var name in new[] { "SCN_Kitchen_Cooking", "SCN_Kitchen_Review" })
        {
            SceneManager.LoadScene(name);
            yield return null;
            yield return null;
            var light = new Color[1];
            var prop = GameObject.Find(name == "SCN_Kitchen_Cooking" ? "PREF_Cooking_FryingPan" : "PREF_Prop_Pot");
            Assert.IsNotNull(prop);
            var body = prop.GetComponent<Rigidbody>();
            body.position = new Vector3(-3.95f, .19f, 1.2f);
            body.rotation = Quaternion.identity;
            body.velocity = body.angularVelocity = Vector3.zero;
            Physics.SyncTransforms();
            yield return new WaitForSeconds(.55f);
            Assert.That(body.worldCenterOfMass.y, Is.LessThan(.12f), name + " prop must reach the floor.");
            var renderer = prop.GetComponentsInChildren<MeshRenderer>().FirstOrDefault(item => item.probeAnchor != null);
            Assert.IsNotNull(renderer, name + " prop must sample indirect light above the floor.");
            LightProbes.GetInterpolatedProbe(renderer.probeAnchor.position, renderer, out SphericalHarmonicsL2 probe);
            probe.Evaluate(new[] { Vector3.up }, light);
            Assert.That(light[0].grayscale, Is.GreaterThan(.025f), name + " dropped prop lacks indirect light.");
            var settings = Object.FindObjectOfType<KitchenLocomotionSettings>();
            var player = settings.Origin.Origin.transform;
            var safePosition = player.position;
            player.position += Vector3.down * 2f;
            yield return null;
            yield return null;
            Assert.That(Vector3.Distance(player.position, safePosition), Is.LessThan(.05f), name);
            var envelope = Object.FindObjectOfType<KitchenRoomBounds>();
            Assert.AreEqual(5, envelope.GetComponentsInChildren<BoxCollider>().Length);
        }
    }

    [UnityTest]
    public IEnumerator LanguageMenuUpdatesCookingAndCarriesChineseIntoReview()
    {
        var saved = PlayerPrefs.GetInt(KitchenLocale.Preference, -1);
        PlayerPrefs.DeleteKey(KitchenLocale.Preference);
        try
        {
            SceneManager.LoadScene("SCN_Kitchen_Cooking");
            yield return null;
            var menu = Object.FindObjectOfType<LocomotionSettingsView>();
            menu.Open();
            yield return null;
            var button = menu.transform.Find("Panel/Language");
            ClickTrackedButton(menu.GetComponent<TrackedDeviceGraphicRaycaster>(), button);
            Assert.IsTrue(KitchenLocale.Chinese);
            var title = menu.transform.Find("Panel/Title").GetComponent<TMP_Text>();
            Assert.AreEqual("移动设置", title.text);
            title.ForceMeshUpdate();
            Assert.AreEqual('移', title.textInfo.characterInfo[0].character);
            var workbench = GameObject.Find("PREF_Cooking_Workbench");
            var hint = workbench.transform.Find("UI_FridgeInstructions/Action").GetComponent<TMP_Text>();
            StringAssert.Contains("把手", hint.text);
            SceneManager.LoadScene("SCN_Kitchen_Review");
            yield return null;
            var reviewMenu = Object.FindObjectOfType<LocomotionSettingsView>();
            reviewMenu.Open();
            yield return null;
            Assert.AreEqual("移动设置", reviewMenu.transform.Find("Panel/Title").GetComponent<TMP_Text>().text);
            ClickTrackedButton(reviewMenu.GetComponent<TrackedDeviceGraphicRaycaster>(), reviewMenu.transform.Find("Panel/Language"));
            Assert.AreEqual("MOVEMENT", reviewMenu.transform.Find("Panel/Title").GetComponent<TMP_Text>().text);
            reviewMenu.Close();
        }
        finally
        {
            if (KitchenLocale.Chinese) KitchenLocale.Toggle();
            if (saved < 0) PlayerPrefs.DeleteKey(KitchenLocale.Preference);
            else PlayerPrefs.SetInt(KitchenLocale.Preference, saved);
        }
    }

    [UnityTest]
    public IEnumerator KitchenAndDiningWindowsFillTheOpeningsInBothScenes()
    {
        foreach (var scene in new[] { "SCN_Kitchen_Review", "SCN_Kitchen_Cooking" })
        {
            SceneManager.LoadScene(scene);
            yield return null;
            var environment = GameObject.Find("PREF_Kitchen_Environment").transform;
            var windows = environment.Find("PREF_Kitchen_Windows");
            Assert.NotNull(windows, scene);
            Assert.NotNull(windows.Find("Window_Dining/Frame_Mullion"));
            foreach (var (path, centre) in new[] {
                ("Window_Sink/Glass", new Vector3(-5.174f, 1.935f, 1.448f)),
                ("Window_Dining/Glass_Left", new Vector3(-5.125f, 1.76f, -1.40f)),
                ("Window_Dining/Glass_Right", new Vector3(-5.125f, 1.76f, -.32f)) })
            {
                var pane = windows.Find(path);
                Assert.NotNull(pane, scene + " " + path);
                var collider = pane.GetComponent<BoxCollider>();
                Assert.IsTrue(collider.Raycast(new Ray(centre + Vector3.right * .35f, Vector3.left),
                    out _, .5f), scene + " " + path + " should close the exterior opening");
                var material = pane.GetComponent<MeshRenderer>().sharedMaterial;
                Assert.That(material.color.a, Is.InRange(.01f, .2f));
                Assert.GreaterOrEqual(material.renderQueue, 3000);
            }
        }
    }

    [UnityTest]
    public IEnumerator ComparisonSkipShortcutHasAUsableXRRaycastTarget()
    {
        SceneManager.LoadScene("SCN_Kitchen_Review");
        yield return null;
        var shell = Object.FindObjectOfType<JourneyPhaseFiveView>();
        shell.Render(new JourneySnapshot
        {
            State = SessionState.ComparisonSummary,
            BenchmarkOutcomes = new Dictionary<string, TaskOutcome>
            {
                ["B1"] = TaskOutcome.Skipped,
                ["B2"] = TaskOutcome.Completed
            }
        }, JourneyErrorCode.None, "");
        yield return null;
        var shortcut = shell.GetComponentInChildren<JourneyComparisonInputView>(true)
            .transform.Find("Use skip context");
        var start = Camera.main.transform.position;
        var aim = shortcut.position - Vector3.up * .035f;
        var data = new TrackedDeviceEventData(EventSystem.current)
        {
            rayPoints = new List<Vector3> { start, start + (aim - start).normalized * 3f },
            layerMask = -1
        };
        var hits = new List<RaycastResult>();
        shell.GetComponent<TrackedDeviceGraphicRaycaster>().Raycast(data, hits);
        Assert.IsNotEmpty(hits, "A slightly low hand ray must still hit the skip shortcut.");
        Assert.AreEqual(shortcut.gameObject, hits[0].gameObject);
        data.button = PointerEventData.InputButton.Left;
        ExecuteEvents.Execute<IPointerClickHandler>(hits[0].gameObject, data,
            ExecuteEvents.pointerClickHandler);
        Assert.That(shell.GetComponentInChildren<JourneyComparisonInputView>(true)
            .GetComponentInChildren<TMP_InputField>().text, Does.Contain("B1 was skipped"));

        var confirm = shell.transform.Find("ComparisonPanel/ConfirmComparison");
        aim = confirm.position - Vector3.up * .035f;
        data.rayPoints = new List<Vector3> { start, start + (aim - start).normalized * 3f };
        hits.Clear();
        shell.GetComponent<TrackedDeviceGraphicRaycaster>().Raycast(data, hits);
        Assert.IsNotEmpty(hits, "A slightly low hand ray must still hit Confirm.");
        Assert.AreEqual(confirm.gameObject, hits[0].gameObject);

        var mouse = new PointerEventData(EventSystem.current);
        foreach (var target in new[] { shortcut, confirm })
        {
            mouse.position = Camera.main.WorldToScreenPoint(target.position);
            hits.Clear();
            EventSystem.current.RaycastAll(mouse, hits);
            Assert.IsTrue(hits.Any(hit => hit.gameObject == target.gameObject),
                "Mouse input must reach " + target.name + " in Editor Play Mode.");
        }
        var field = shell.GetComponentInChildren<JourneyComparisonInputView>(true)
            .GetComponentInChildren<TMP_InputField>();
        mouse.position = Camera.main.WorldToScreenPoint(field.transform.position);
        hits.Clear();
        EventSystem.current.RaycastAll(mouse, hits);
        Assert.IsTrue(hits.Any(hit => hit.gameObject.GetComponentInParent<TMP_InputField>() == field),
            "Mouse input must reach the trade-off field in Editor Play Mode.");
        shell.Render(new JourneySnapshot { State = SessionState.ComparisonSummary, Save = SaveStatus.Failed },
            JourneyErrorCode.OperationFailed, "Simulated locked CSV.");
        var write = field.transform.parent.Find("Write in VR").GetComponent<Button>();
        Assert.IsFalse(write.IsInteractable(), "A failed save retains the submitted comparison for Retry.");
        Assert.That(field.text, Does.Contain("B1 was skipped"));
    }

    [UnityTest]
    public IEnumerator EachTaskItemRestingOnEitherFreshShelfIsDetectable()
    {
        SceneManager.LoadScene("SCN_Kitchen_Review");
        yield return null;
        var host = Object.FindObjectOfType<JourneyRuntimeHost>();
        Assert.IsTrue(host.Submit(new JourneyCommand
        { Type = JourneyCommandType.CompleteSetup, PrerequisitesMet = true }).Accepted);
        Assert.IsTrue(host.Submit(new JourneyCommand
        { Type = JourneyCommandType.AcknowledgeOrientation, PrerequisitesMet = true }).Accepted);
        foreach (var benchmark in new[] { "B1", "B2" })
        {
            Assert.IsTrue(host.Submit(new JourneyCommand
            { Type = JourneyCommandType.ShowBenchmark, BenchmarkId = benchmark }).Accepted);
            yield return null;
            var destination = Identity("ZONE_FreshDestination");
            foreach (var shelf in new[] { "SHELF_Fresh_Lower", "SHELF_Fresh_Task" })
                foreach (var itemId in new[] { "ITEM_Tomato", "ITEM_Onion", "ITEM_CerealBox" })
                {
                    var item = Identity(itemId);
                    PlaceRestingOnShelf(item, shelf);
                    var center = item.GetComponentInChildren<Collider>().bounds.center;
                    Assert.IsTrue(destination.ContainsPoint(center),
                        benchmark + " " + shelf + " " + itemId + " resting center is outside Fresh destination: " + center +
                        "; shelf=" + GameObject.Find(shelf).GetComponent<Collider>().bounds +
                        "; sensors=" + string.Join(", ", destination.GetComponents<Collider>()
                            .Select(sensor => sensor.bounds.ToString()).ToArray()));
                }
        }
    }

    [UnityTest]
    public IEnumerator SeparatelyHeldMugAndJuicePackCannotPassThroughEachOther()
    {
        SceneManager.LoadScene("SCN_Kitchen_Review");
        yield return null;

        var manager = Object.FindObjectOfType<UnityEngine.XR.Interaction.Toolkit.XRInteractionManager>();
        var mug = GameObject.Find("PREF_Prop_Mug");
        var juicePack = GameObject.Find("PREF_Prop_JuicePack");
        Assert.NotNull(manager);
        Assert.NotNull(mug);
        Assert.NotNull(juicePack);

        var mugGrab = mug.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        var juiceGrab = juicePack.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        var mugBody = mug.GetComponent<Rigidbody>();
        var juiceBody = juicePack.GetComponent<Rigidbody>();
        Assert.NotNull(mugGrab);
        Assert.NotNull(juiceGrab);
        Assert.NotNull(mugBody);
        Assert.NotNull(juiceBody);

        var start = new Vector3(100f, 10f, 100f);
        mugBody.useGravity = false;
        juiceBody.useGravity = false;
        mugBody.velocity = Vector3.zero;
        juiceBody.velocity = Vector3.zero;
        mugBody.position = start + Vector3.left * .35f;
        juiceBody.position = start + Vector3.right * .35f;
        Physics.SyncTransforms();
        var mugColliders = mug.GetComponentsInChildren<Collider>()
            .Where(c => c.enabled && !c.isTrigger).ToArray();
        var juiceColliders = juicePack.GetComponentsInChildren<Collider>()
            .Where(c => c.enabled && !c.isTrigger).ToArray();

        var leftHand = CreateTestInteractor(manager, mugBody.position);
        var rightHand = CreateTestInteractor(manager, juiceBody.position);
        yield return new WaitForFixedUpdate();
        leftHand.StartManualInteraction((UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable)mugGrab);
        rightHand.StartManualInteraction((UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable)juiceGrab);
        yield return new WaitForFixedUpdate();

        Assert.IsTrue(leftHand.hasSelection);
        Assert.IsTrue(rightHand.hasSelection);
        try
        {
            // The controllers cross each other in one frame and keep pressing.
            leftHand.transform.position = start + Vector3.right * .35f;
            rightHand.transform.position = start + Vector3.left * .35f;
            for (var frame = 0; frame < 30; frame++)
            {
                yield return new WaitForFixedUpdate();
                Assert.Less(mug.transform.position.x, juicePack.transform.position.x,
                    "Separately held props must not exchange sides through each other. " +
                    HeldState(mugGrab) + " / " + HeldState(juiceGrab));
                foreach (var a in mugColliders)
                    foreach (var b in juiceColliders)
                    {
                        var overlapping = Physics.ComputePenetration(a, a.transform.position, a.transform.rotation,
                            b, b.transform.position, b.transform.rotation, out _, out var depth);
                        Assert.IsTrue(!overlapping || depth <= .003f,
                            "The actual prefab colliders penetrated while both controllers kept pressing.");
                    }
            }
            Assert.IsTrue(mug.GetComponent<CollisionConstrainedGrabTransformer>().IsBlocked ||
                          juicePack.GetComponent<CollisionConstrainedGrabTransformer>().IsBlocked);
        }
        finally
        {
            leftHand.EndManualInteraction();
            rightHand.EndManualInteraction();
            Object.Destroy(leftHand.gameObject);
            Object.Destroy(rightHand.gameObject);
        }
        Assert.IsFalse(mugBody.isKinematic, "Released props must resume normal rigidbody simulation.");
        Assert.IsFalse(juiceBody.isKinematic);
    }

    [UnityTest]
    public IEnumerator ConstrainedTaskItemCanBeTakenOutAndRestoredInBothBenchmarks()
    {
        SceneManager.LoadScene("SCN_Kitchen_Review");
        yield return null;
        var host = Object.FindObjectOfType<JourneyRuntimeHost>();
        Assert.IsNull(host.StartupError);
        Assert.IsTrue(host.Submit(new JourneyCommand
        { Type = JourneyCommandType.CompleteSetup, PrerequisitesMet = true }).Accepted);
        Assert.IsTrue(host.Submit(new JourneyCommand
        { Type = JourneyCommandType.AcknowledgeOrientation, PrerequisitesMet = true }).Accepted);
        var manager = Object.FindObjectOfType<UnityEngine.XR.Interaction.Toolkit.XRInteractionManager>();
        foreach (var benchmark in new[] { "B1", "B2" })
        {
            MoveHead(-3.0f, 1.08f);
            Assert.IsTrue(host.Submit(new JourneyCommand
            { Type = JourneyCommandType.StartChallenge, BenchmarkId = benchmark }).Accepted);
            MoveHead(-1.90f, 1.05f);
            yield return null;
            Identity("ACCESS_FridgeDoor").GetComponent<KinematicSwingDoor>().SetOpenImmediate(true);
            yield return null;
            Assert.AreEqual(ChallengeStep.TakeOut, host.Core.Snapshot.CurrentStep);

            var run = host.Core.Snapshot.ActiveRunId;
            var recovery = Object.FindObjectOfType<RecoveryMenuView>();
            recovery.Open();
            yield return null;
            var posture = benchmark == "B1" ? "UseStandingView" : "UseSeatedView";
            ClickTrackedButton(recovery.GetComponent<TrackedDeviceGraphicRaycaster>(),
                recovery.transform.Find("Panel/" + posture));
            Assert.AreEqual(run, host.Core.Snapshot.ActiveRunId);
            Assert.AreEqual(ChallengeStep.TakeOut, host.Core.Snapshot.CurrentStep);
            Assert.That(Camera.main.GetComponentInParent<XROrigin>().CameraInOriginSpaceHeight,
                Is.EqualTo(benchmark == "B1" ? 1.65f : 1.20f).Within(.001f));
            recovery.transform.Find("Panel/Close").GetComponent<Button>().onClick.Invoke();

            var item = Identity("ITEM_Tomato");
            Identity("ITEM_Onion").Body.isKinematic = true;
            var guard = item.GetComponent<CollisionConstrainedGrabTransformer>();
            Assert.IsNotNull(guard);
            item.Body.useGravity = false;
            var hand = CreateTestInteractor(manager, item.transform.position);
            yield return null;
            try
            {
                hand.StartManualInteraction(
                    (UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable)item.Grab);
                Assert.IsTrue(item.Grab.isSelected, benchmark + " must allow the real XRI grab at TakeOut");
                var sourcePosition = hand.transform.position;
                for (var frame = 1; frame <= 12; frame++)
                {
                    // Both runtime fridges face the room towards negative world Z.
                    hand.transform.position = sourcePosition + Vector3.back * (.07f * frame);
                    // XRI reads tracked poses in Dynamic/OnBeforeRender. A
                    // fixed-step catch-up can otherwise skip every input sample.
                    yield return null;
                    yield return new WaitForFixedUpdate();
                }
                hand.EndManualInteraction();
                yield return null;
                Assert.AreEqual(1, host.Core.Snapshot.CompletedItemCount,
                    benchmark + " must accept evidence from the constrained object leaving the fridge. " +
                    HeldState(item.Grab) + "; source=" + Identity("ZONE_FreezerSource").Sensor.bounds);
                Assert.AreEqual(ChallengeStep.TakeOut, host.Core.Snapshot.CurrentStep);
            }
            finally
            {
                if (hand.hasSelection) hand.EndManualInteraction();
                Object.Destroy(hand.gameObject);
            }
            yield return RestoreAtStep(host, ChallengeStep.TakeOut);
            Assert.AreEqual(0, host.Core.Snapshot.CompletedItemCount);
            AssertFreezerSourceItems(benchmark + " constrained grab restore");
            Assert.IsFalse(item.Grab.isSelected);
            Assert.IsFalse(item.transform.Find("COL_GrabSweepProbe").GetComponent<Collider>().enabled);
            item.Body.useGravity = true;
            Identity("ITEM_Onion").Body.isKinematic = false;
            Assert.IsTrue(host.Submit(new JourneyCommand
            { Type = JourneyCommandType.SkipChallenge, Reason = "Integrated grab smoke" }).Accepted);
            Assert.IsTrue(host.SubmitFeedback(new FeedbackDraft
            { SkipReason = "Integrated grab smoke" }).Accepted);
            var nextState = benchmark == "B1" ? SessionState.Explore : SessionState.ComparisonSummary;
            for (var frame = 0; frame < 60 && host.Core.Snapshot.State != nextState; frame++) yield return null;
            Assert.AreEqual(nextState, host.Core.Snapshot.State);
        }
    }

    [UnityTest]
    public IEnumerator UnheldSourceFoodAndImmediateStepRestoreDoNotAdvanceTheTask()
    {
        SceneManager.LoadScene("SCN_Kitchen_Review");
        yield return null;
        var host = Object.FindObjectOfType<JourneyRuntimeHost>();
        Assert.IsTrue(host.Submit(new JourneyCommand
        { Type = JourneyCommandType.CompleteSetup, PrerequisitesMet = true }).Accepted);
        Assert.IsTrue(host.Submit(new JourneyCommand
        { Type = JourneyCommandType.AcknowledgeOrientation, PrerequisitesMet = true }).Accepted);
        var manager = Object.FindObjectOfType<UnityEngine.XR.Interaction.Toolkit.XRInteractionManager>();
        var originalDelta = Time.captureDeltaTime;
        try
        {
            Time.captureDeltaTime = 1f / 90f;
            foreach (var benchmark in new[] { "B1", "B2" })
            {
                MoveHead(-3f, 1.08f);
                Assert.IsTrue(host.Submit(new JourneyCommand
                { Type = JourneyCommandType.StartChallenge, BenchmarkId = benchmark }).Accepted);
                MoveHead(-1.90f, 1.05f);
                yield return null;
                Identity("ACCESS_FridgeDoor").GetComponent<KinematicSwingDoor>().SetOpenImmediate(true);
                yield return null;
                Assert.AreEqual(ChallengeStep.TakeOut, host.Core.Snapshot.CurrentStep);
                for (var frame = 0; frame < 30; frame++)
                {
                    yield return null;
                    yield return new WaitForFixedUpdate();
                    Assert.AreEqual(0, host.Core.Snapshot.CompletedItemCount,
                        benchmark + " resting food must stay in its source zone without a grab.");
                }
                var item = Identity("ITEM_Tomato");
                var hand = CreateTestInteractor(manager, item.transform.position);
                item.Body.useGravity = false;
                try
                {
                    yield return null;
                    hand.StartManualInteraction((IXRSelectInteractable)item.Grab);
                    Assert.IsTrue(item.Grab.isSelected);
                    for (var frame = 0; frame < 12; frame++)
                    {
                        hand.transform.position += Vector3.back * .035f;
                        yield return null;
                    }
                    hand.EndManualInteraction();
                    var run = host.Core.Snapshot.ActiveRunId;
                    Assert.IsTrue(host.Submit(new JourneyCommand
                    { Type = JourneyCommandType.RestoreCurrentStep }).Accepted);
                    var restored = item.transform.position;
                    for (var frame = 0; frame < 8; frame++)
                    {
                        yield return null;
                        yield return new WaitForFixedUpdate();
                        Assert.That(Vector3.Distance(item.transform.position, restored), Is.LessThan(.02f),
                            benchmark + " step restoration replayed an old throw.");
                        Assert.That(item.Body.velocity.magnitude, Is.LessThan(.1f));
                    }
                    Assert.AreEqual(run, host.Core.Snapshot.ActiveRunId);
                    Assert.AreEqual(ChallengeStep.TakeOut, host.Core.Snapshot.CurrentStep);
                    Assert.AreEqual(0, host.Core.Snapshot.CompletedItemCount);
                }
                finally
                {
                    if (hand.hasSelection) hand.EndManualInteraction();
                    Object.Destroy(hand.gameObject);
                    item.Body.useGravity = true;
                }
                Assert.IsTrue(host.Submit(new JourneyCommand
                { Type = JourneyCommandType.SkipChallenge, Reason = "Release timing regression" }).Accepted);
                Assert.IsTrue(host.SubmitFeedback(new FeedbackDraft
                { SkipReason = "Release timing regression" }).Accepted);
                yield return null;
            }
        }
        finally { Time.captureDeltaTime = originalDelta; }
    }

    [UnityTest]
    public IEnumerator SeatedControllerRayCanTakeOutAndReturnAFridgeItemInBothBenchmarks()
    {
        SceneManager.LoadScene("SCN_Kitchen_Review");
        yield return null;
        var host = Object.FindObjectOfType<JourneyRuntimeHost>();
        Assert.IsNull(host.StartupError);
        Assert.IsTrue(host.Submit(new JourneyCommand
        { Type = JourneyCommandType.CompleteSetup, PrerequisitesMet = true }).Accepted);
        Assert.IsTrue(host.Submit(new JourneyCommand
        { Type = JourneyCommandType.AcknowledgeOrientation, PrerequisitesMet = true }).Accepted);
        MoveHead(-1.90f, 1.05f);
        var origin = Camera.main.GetComponentInParent<XROrigin>();
        var recovery = Object.FindObjectOfType<RecoveryMenuView>();
        recovery.Open();
        yield return null;
        ClickTrackedButton(recovery.GetComponent<TrackedDeviceGraphicRaycaster>(),
            recovery.transform.Find("Panel/UseSeatedView"));
        ClickTrackedButton(recovery.GetComponent<TrackedDeviceGraphicRaycaster>(),
            recovery.transform.Find("Panel/Close"));
        Assert.That(origin.CameraInOriginSpaceHeight, Is.EqualTo(1.20f).Within(.001f));

        var controller = origin.CameraFloorOffsetObject.transform.Find("Right Controller");
        Assert.NotNull(controller);
        var tracker = controller.GetComponent<TrackedPoseDriver>();
        Assert.NotNull(tracker);
        var hand = controller.GetComponentInChildren<NearFarInteractor>(true);
        Assert.NotNull(hand);
        var active = controller.gameObject.activeSelf;
        var tracking = tracker.enabled;
        var select = hand.selectInput;
        var inputMode = select.inputSourceMode;
        var attach = hand.GetComponent<InteractionAttachController>();
        Assert.NotNull(attach);
        Assert.IsTrue(attach.useManipulationInput);
        var manipulation = attach.manipulationInput;
        var manipulationMode = manipulation.inputSourceMode;
        var originalDelta = Time.captureDeltaTime;
        try
        {
            tracker.enabled = false;
            controller.gameObject.SetActive(true);
            select.inputSourceMode = XRInputButtonReader.InputSourceMode.ManualValue;
            manipulation.inputSourceMode = XRInputValueReader<Vector2>.InputSourceMode.ManualValue;
            // Maintain realistic Dynamic/Fixed phase sampling in batch mode.
            Time.captureDeltaTime = 1f / 90f;
            var targets = new List<IXRInteractable>();
            foreach (var benchmark in new[] { "B1", "B2" })
            {
                Assert.IsTrue(host.Submit(new JourneyCommand
                { Type = JourneyCommandType.ShowBenchmark, BenchmarkId = benchmark }).Accepted);
                yield return null;
                var item = Identity("ITEM_Tomato");
                var zone = Identity("ZONE_FreshDestination");
                var door = Identity("ACCESS_FridgeDoor").GetComponent<KinematicSwingDoor>();
                PlaceRestingOnShelf(item, "SHELF_Fresh_Lower");
                var target = item.Grab.colliders[0].bounds.center;
                var wrist = new Vector3(target.x, .90f, 1.02f);
                controller.SetPositionAndRotation(wrist, Quaternion.LookRotation(target - wrist));
                Physics.SyncTransforms();
                for (var frame = 0; frame < 18; frame++) yield return null;
                hand.GetValidTargets(targets);
                CollectionAssert.DoesNotContain(targets, item.Grab, benchmark + " closed door must occlude the food");

                door.SetOpenImmediate(true);
                Physics.SyncTransforms();
                for (var frame = 0; frame < 18; frame++) yield return null;
                hand.GetValidTargets(targets);
                CollectionAssert.Contains(targets, item.Grab, benchmark + " low shelf needs a reachable ray target");
                select.QueueManualState(true, 1f);
                for (var frame = 0; frame < 8 && !item.Grab.isSelected; frame++) yield return null;
                Assert.IsTrue(item.Grab.isSelected, benchmark + " grip must select the low item");
                yield return null;
                Assert.AreEqual(NearFarInteractor.Region.Far, hand.selectionRegion.Value);

                // Supply the stick value to the rig's actual attach controller.
                // The wrist stays at seated height while the food moves along the ray.
                manipulation.manualValue = Vector2.down;
                var deadline = Time.realtimeSinceStartup + 3f;
                while (item.Grab.colliders[0].bounds.center.z > target.z - .43f &&
                    Time.realtimeSinceStartup < deadline)
                    yield return null;
                var extracted = item.Grab.colliders[0].bounds.center;
                Assert.IsFalse(zone.ContainsPoint(extracted),
                    benchmark + " ray-held item must physically leave the shelf");
                Assert.That(extracted.z, Is.LessThan(target.z - .40f));

                manipulation.manualValue = Vector2.up;
                deadline = Time.realtimeSinceStartup + 3f;
                while (item.Grab.colliders[0].bounds.center.z < target.z - .025f &&
                    Time.realtimeSinceStartup < deadline)
                    yield return null;
                manipulation.manualValue = Vector2.zero;
                for (var frame = 0; frame < 24; frame++) yield return null;
                var held = item.Grab.colliders[0].bounds.center;
                select.QueueManualState(false, 0f);
                for (var frame = 0; frame < 12; frame++) yield return null;
                Assert.IsFalse(item.Grab.isSelected, benchmark + " grip release must drop the item");
                var stored = item.Grab.colliders[0].bounds.center;
                Assert.IsTrue(zone.ContainsPoint(stored),
                    $"{benchmark} returned food must remain in the refrigerator. " +
                    $"Shelf {target:F3}, extracted {extracted:F3}, before release {held:F3}, settled {stored:F3}.");
                Assert.That(Vector3.Distance(stored, target), Is.LessThan(.09f),
                    benchmark + " returned food must be on the original low shelf");
            }
        }
        finally
        {
            Time.captureDeltaTime = originalDelta;
            manipulation.manualValue = Vector2.zero;
            manipulation.inputSourceMode = manipulationMode;
            select.inputSourceMode = inputMode;
            controller.gameObject.SetActive(active);
            tracker.enabled = tracking;
        }
    }

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
            Identity("ACCESS_FridgeDoor").GetComponent<KinematicSwingDoor>()
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
        var origin = Camera.main.GetComponentInParent<XROrigin>();
        var rig = origin.Origin.transform;
        var floorOffset = origin.CameraFloorOffsetObject.transform;
        var head = origin.Camera.transform;
        var tracked = new[] { head, floorOffset.Find("Left Controller"), floorOffset.Find("Right Controller") };
        var oldLocal = tracked.Select(t => t.localPosition).ToArray();
        var oldWorld = tracked.Select(t => t.position).ToArray();
        var originalRig = rig.position;
        var originalFloorOffset = floorOffset.localPosition;
        setup.Find("Seated").GetComponent<Button>().onClick.Invoke();
        setup.Find("RaiseEye").GetComponent<Button>().onClick.Invoke();
        setup.Find("Calibrate").GetComponent<Button>().onClick.Invoke();
        Assert.AreEqual(originalRig, rig.position);
        Assert.AreEqual(originalFloorOffset, floorOffset.localPosition);
        Assert.That(origin.CameraInOriginSpaceHeight, Is.EqualTo(1.25f).Within(.001f));
        for (var i = 0; i < tracked.Length; i++)
        {
            Assert.AreEqual(oldLocal[i], tracked[i].localPosition, "Device poses must stay in tracking space.");
            Assert.That(Vector3.Distance(tracked[i].position - oldWorld[i], head.position - oldWorld[0]),
                Is.LessThan(.001f), "The headset and both hands must shift together.");
        }
        Assert.That(rig.GetComponent<CharacterController>().height, Is.EqualTo(1.25f).Within(.001f));
        button.onClick.Invoke();
        Assert.AreEqual(SessionState.Orientation, host.Core.Snapshot.State);
        Assert.IsFalse(setup.gameObject.activeSelf);
        Assert.That(panel.Find("State").GetComponent<TMP_Text>().text, Does.Contain("Orientation"));
        var orientation = panel.Find("OrientationControls");
        var landing = GameObject.Find("PREF_Kitchen_TeleportArea").GetComponent<Collider>().bounds.center;
        var landingXZ = new Vector2(landing.x, landing.z);
        Assert.That(Vector2.Distance(landingXZ, new Vector2(head.position.x, head.position.z)), Is.GreaterThan(.1f));
        orientation.Find("PracticeTeleport").GetComponent<Button>().onClick.Invoke();
        var practice = orientation.Find("PracticeStatus").GetComponent<TMP_Text>();
        var deadline = Time.realtimeSinceStartup + 2f;
        while (Time.realtimeSinceStartup < deadline && !practice.text.Contains("Teleport [done]")) yield return null;
        Assert.That(practice.text, Does.Contain("Teleport [done]"), "The calibrated rig must land through the real provider. " +
            panel.Find("Error").GetComponent<TMP_Text>().text);
        Assert.That(Vector2.Distance(landingXZ, new Vector2(head.position.x, head.position.z)), Is.LessThan(.02f));
        Assert.That(origin.CameraInOriginSpaceHeight, Is.EqualTo(1.25f).Within(.001f));
        Assert.IsTrue(host.Submit(new JourneyCommand
        { Type = JourneyCommandType.AcknowledgeOrientation, PrerequisitesMet = true }).Accepted);
        MoveHead(-3.45f, 1.15f);
        var eyeHeight = head.position.y;
        var recovery = Object.FindObjectOfType<RecoveryMenuView>();
        recovery.Open();
        recovery.transform.Find("Panel/Recenter").GetComponent<Button>().onClick.Invoke();
        yield return null;
        var anchor = GameObject.Find("ANCHOR_Rig_Recenter").transform;
        Assert.That(head.position.x, Is.EqualTo(anchor.position.x).Within(.001f));
        Assert.That(head.position.z, Is.EqualTo(anchor.position.z).Within(.001f));
        Assert.That(head.position.y, Is.EqualTo(eyeHeight).Within(.001f), "Recenter must preserve calibrated eye height.");
        recovery.transform.Find("Panel/Close").GetComponent<Button>().onClick.Invoke();
        Assert.IsTrue(host.Submit(new JourneyCommand
        { Type = JourneyCommandType.StartChallenge, BenchmarkId = "B1" }).Accepted);
        yield return null;
        Assert.AreEqual(SessionState.ChallengeActive, host.Core.Snapshot.State);
        Assert.That(panel.Find("State").GetComponent<TMP_Text>().text, Does.Contain("ChallengeActive"));
        Assert.That(panel.Find("Design").GetComponent<TMP_Text>().text, Does.Contain("B1"));
        Assert.That(panel.Find("Progress").GetComponent<TMP_Text>().text, Does.Contain("1/8"));
        recovery.Open();
        yield return null;
        var run = host.Core.Snapshot.ActiveRunId;
        var raycaster = recovery.GetComponent<TrackedDeviceGraphicRaycaster>();
        var standingView = recovery.transform.Find("Panel/UseStandingView");
        var hand = origin.GetComponentInChildren<NearFarInteractor>(true);
        var controller = hand.transform.parent.gameObject;
        var cup = GameObject.Find("PREF_Prop_Cup").GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        var controllerWasActive = controller.activeSelf;
        try
        {
            // A headless run can leave the authored motion controllers dormant.
            controller.SetActive(true);
            Assert.IsTrue(hand.isActiveAndEnabled);
            hand.StartManualInteraction((UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable)cup);
            Assert.IsTrue(cup.isSelected);
            ClickTrackedButton(raycaster, standingView);
            Assert.That(origin.CameraInOriginSpaceHeight, Is.EqualTo(1.25f).Within(.001f));
            Assert.That(recovery.transform.Find("Panel/Operation").GetComponent<TMP_Text>().text, Does.Contain("Put down"));
        }
        finally
        {
            if (hand.hasSelection) hand.EndManualInteraction();
            controller.SetActive(controllerWasActive);
        }
        ClickTrackedButton(raycaster, standingView);
        Assert.That(origin.CameraInOriginSpaceHeight, Is.EqualTo(1.65f).Within(.001f));
        Assert.IsTrue(host.Submit(new JourneyCommand { Type = JourneyCommandType.Pause }).Accepted);
        yield return null;
        Assert.IsEmpty(Object.FindObjectOfType<TaskTargetGuide>().VisibleTargets);
        ClickTrackedButton(raycaster, recovery.transform.Find("Panel/UseSeatedView"));
        Assert.That(origin.CameraInOriginSpaceHeight, Is.EqualTo(1.25f).Within(.001f),
            "The seated height adjusted during Setup should be retained.");
        Assert.IsTrue(host.Submit(new JourneyCommand { Type = JourneyCommandType.Resume }).Accepted);
        Assert.That(panel.Find("State").GetComponent<TMP_Text>().text, Does.Contain("ChallengeActive"));
        Assert.AreEqual(SessionState.ChallengeActive, host.Core.Snapshot.State);
        Assert.AreEqual(run, host.Core.Snapshot.ActiveRunId);
        Assert.AreEqual(ChallengeStep.Approach, host.Core.Snapshot.CurrentStep);
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
            var fridge = GameObject.Find("PREF_Fridge_F2");
            var door = Identity("ACCESS_FridgeDoor").transform;
            Assert.AreSame(fridge.transform, door.parent);
            Assert.AreEqual(1, fridge.GetComponentsInChildren<KinematicSwingDoor>().Length);
            var panel = door.Find("Panel");
            Assert.That(panel.localScale.x, Is.EqualTo(.72f).Within(.001f));
            Assert.That(panel.localScale.y, Is.EqualTo(1.80f).Within(.001f));
            Assert.Less(door.position.x, -2.1f, id + " F2 door must face the room");
            Assert.Less(door.position.z, 1.8f, id + " F2 door must not face the wall");
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
            Assert.NotNull(Identity("ACCESS_FridgeDoor"));
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
            Identity("ACCESS_FridgeDoor").GetComponent<KinematicSwingDoor>().Toggle();
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
        var canvas = GameObject.Find("PREF_UI_ExploreChallenge").transform;
        var raycaster = canvas.GetComponent<TrackedDeviceGraphicRaycaster>();
        var full = canvas.Find("ChallengePanel");
        var compact = canvas.Find("ChallengeCompactPanel");
        var fullConfirm = full.Find("ConfirmReorganisation").GetComponent<Button>();
        var compactConfirm = compact.Find("ConfirmReorganisation").GetComponent<Button>();
        var compactInstruction = compact.Find("Instruction").GetComponent<TMP_Text>();
        Assert.AreEqual("XR Apartment/Review Keyboard Surface", full.GetComponent<Image>().material.shader.name);
        Assert.AreEqual("XR Apartment/Review Keyboard Surface", compact.GetComponent<Image>().material.shader.name);
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
            AssertChallengeTextFits(full);
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
                .Contains("ACCESS_FridgeDoor"));
            var arrow = GameObject.Find("GUIDE_Green_DownTriangle");
            Assert.NotNull(arrow);
            Assert.IsNull(arrow.GetComponent<Collider>());
            Assert.Greater(arrow.GetComponent<MeshFilter>().sharedMesh.normals[0].z, 0f,
                "The triangle front face must point toward the viewer");
            Assert.NotNull(arrow.GetComponent<MeshRenderer>().sharedMaterial);
            var door = Identity("ACCESS_FridgeDoor").GetComponent<KinematicSwingDoor>();
            yield return AssertBlockedOpening(host, id, door);
            door.SetOpen(true);
            var openingDeadline = Time.time + 2.5f;
            while (door.Progress < .999 && Time.time < openingDeadline) yield return null;
            Assert.That(door.Progress, Is.EqualTo(1).Within(.001),
                id + " must open fully after the obstruction is removed. Hinge " + door.transform.localEulerAngles);
            yield return null;
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
            AssertChallengeTextFits(full);
            AssertZoneHighlights("ZONE_Temporary");
            Place(items[0], -3.50f, 1.06f, 1.94f);
            Place(items[1], -3.36f, 1.06f, 1.94f);
            yield return new WaitForSeconds(.35f);
            Assert.AreEqual(ChallengeStep.Reorganise, host.Core.Snapshot.CurrentStep);
            yield return RestoreAtStep(host, ChallengeStep.Reorganise);
            yield return null;
            AssertZoneHighlights("ZONE_Temporary");
            Assert.IsTrue(fullConfirm.gameObject.activeInHierarchy);
            Assert.IsFalse(fullConfirm.interactable);
            ClickTrackedButton(raycaster, full.Find("Compact"));
            yield return null;
            Assert.IsTrue(compactConfirm.gameObject.activeInHierarchy);
            Assert.IsFalse(compactConfirm.interactable);
            AssertChallengeTextFits(compact);
            Assert.AreEqual(0, host.Core.Snapshot.CompletedItemCount);
            Place(items[0], -1.99f, 1.46f, 1.80f);
            yield return new WaitForSeconds(.35f);
            Assert.AreEqual(ChallengeStep.Reorganise, host.Core.Snapshot.CurrentStep,
                "Returning to Freezer is not a Fresh reorganisation");
            Assert.IsFalse(compactConfirm.interactable);
            var shelf = GameObject.Find("SHELF_Fresh_Lower").GetComponent<Collider>().bounds;
            var shelfFront = new Vector3(shelf.max.x - .03f, shelf.max.y + .1f,
                shelf.min.z + .04f);
            Assert.That(shelfFront.z, Is.InRange(shelf.min.z, shelf.max.z));
            Place(items[0], shelfFront.x, shelfFront.y, shelfFront.z);
            yield return new WaitForSeconds(.35f);
            Assert.AreEqual(1, host.Core.Snapshot.CompletedItemCount);
            Assert.IsTrue(fullConfirm.interactable);
            Assert.IsTrue(compactConfirm.interactable);
            StringAssert.Contains("Item moved", compactInstruction.text);
            AssertChallengeTextFits(compact);
            AssertZoneHighlights();
            ClickTrackedButton(raycaster, compactConfirm.transform);
            Assert.AreEqual(ChallengeStep.PutBack, host.Core.Snapshot.CurrentStep);
            Assert.IsFalse(compactConfirm.gameObject.activeSelf);
            yield return null;
            ClickTrackedButton(raycaster, compact.Find("Expand"));
            Assert.IsTrue(full.gameObject.activeInHierarchy);
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
            AssertRestoredStoragePositions(items);
            yield return null;
            AssertZoneHighlights();
            door.SetOpen(false);
            var closingDeadline = Time.time + 2.5f;
            while (door.Progress > .001 && Time.time < closingDeadline) yield return null;
            Assert.That(door.Progress, Is.EqualTo(0).Within(.001),
                id + " must close fully. Hinge " + door.transform.localEulerAngles);
            yield return null;
            Assert.AreEqual(ChallengeStep.Leave, host.Core.Snapshot.CurrentStep);
            yield return RestoreAtStep(host, ChallengeStep.Leave);
            yield return null;
            AssertZoneHighlights("ZONE_Leave");
            MoveHead(-3.0f, 1.08f);
            yield return null;
            Assert.AreEqual(SessionState.FeedbackPending, host.Core.Snapshot.State);
            Assert.NotNull(host.GetComponent<FeedbackFormView>(),
                "A completed challenge must open the formal feedback form.");
            Assert.IsTrue(GameObject.Find("FeedbackFormCanvas") != null,
                "The formal feedback form must be visible while feedback is pending.");
            Assert.IsTrue(Object.FindObjectOfType<JourneyPhaseFiveView>().UseExternalFeedbackView,
                "The phase-five placeholder must yield to the formal feedback form.");
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
            if (id == "B2")
            {
                Assert.IsTrue(host.Core.Snapshot.Conflicts.Single().FeedbackRecorded);
                Assert.That(GameObject.Find("ReviewDesignConflict").GetComponentInChildren<TMP_Text>().text,
                    Does.Contain("Feedback recorded"));
                ReadResult saved = null;
                host.GetComponent<LocalCsvStorageAdapter>().TryRead(host.Core.Snapshot.SessionId, result => saved = result);
                Assert.IsTrue(saved.Succeeded, saved.Message);
                Assert.AreEqual("B2", saved.Record.conflicts.Single().combinationId);
                Assert.IsTrue(saved.Record.conflicts.Single().feedbackRecorded);
            }
        }
        var shell = Object.FindObjectOfType<JourneyPhaseFiveView>();
        var comparison = Object.FindObjectOfType<JourneyComparisonInputView>(true);
        Assert.NotNull(comparison);
        var confirm = shell.transform.Find("ComparisonPanel/ConfirmComparison").GetComponent<Button>();
        Assert.IsTrue(confirm.interactable);
        confirm.onClick.Invoke();
        Assert.AreEqual(SessionState.ComparisonSummary, host.Core.Snapshot.State,
            "An empty trade-off must keep the comparison open.");
        Assert.That(shell.transform.Find("ComparisonPanel/Status").GetComponent<TMP_Text>().text,
            Does.Contain("trade-off"));
        ClickComparisonButton(shell, comparison.transform.Find("Prefer B1"));
        var write = comparison.transform.Find("Write in VR");
        ClickComparisonButton(shell, write);
        var keyboard = shell.transform.Find("TradeOffKeyboard");
        yield return null;
        Assert.AreNotEqual(confirm.gameObject, ComparisonRayHit(shell, confirm.transform),
            "A ray must not be able to confirm while the keyboard is open.");
        EnterTradeOff(shell, keyboard, "B1 room x");
        ClickComparisonButton(shell, keyboard.Find("Backspace"));
        Assert.AreEqual("B1 room ", comparison.Capture().TradeOffSummary);
        Assert.IsTrue(host.Submit(new JourneyCommand { Type = JourneyCommandType.Pause }).Accepted);
        Assert.IsFalse(keyboard.gameObject.activeInHierarchy);
        yield return null;
        var recovery = Object.FindObjectOfType<RecoveryMenuView>();
        Assert.IsTrue(recovery.IsOpen);
        ClickTrackedButton(recovery.GetComponent<TrackedDeviceGraphicRaycaster>(),
            recovery.transform.Find("Panel/UseSeatedView"));
        Assert.AreEqual(SessionState.Paused, host.Core.Snapshot.State);
        Assert.That(Camera.main.GetComponentInParent<XROrigin>().CameraInOriginSpaceHeight,
            Is.EqualTo(1.20f).Within(.001f));
        var resume = recovery.transform.Find("Panel/Resume").GetComponent<Button>();
        Assert.IsTrue(resume.IsInteractable());
        resume.onClick.Invoke();
        yield return null;
        Assert.AreEqual(SessionState.ComparisonSummary, host.Core.Snapshot.State);
        Assert.IsFalse(recovery.IsOpen);
        Assert.IsFalse(keyboard.gameObject.activeInHierarchy);
        Assert.AreEqual("B1 room ", comparison.Capture().TradeOffSummary);
        ClickComparisonButton(shell, write);
        yield return null;
        EnterTradeOff(shell, keyboard, "and B2 fit.");
        ClickComparisonButton(shell, keyboard.Find("Done"));
        Assert.IsFalse(keyboard.gameObject.activeInHierarchy);
        ClickComparisonButton(shell, confirm.transform);
        yield return new WaitForSeconds(.1f);
        Assert.AreEqual(SessionState.Finished, host.Core.Snapshot.State);
        Assert.AreEqual(SaveStatus.Verified, host.Core.Snapshot.Save);
        Assert.AreEqual(XRApartment.Data.SessionOutcome.Completed, host.Core.Snapshot.Outcome);
        ReadResult final = null;
        var session = host.Core.Snapshot.SessionId;
        host.GetComponent<LocalCsvStorageAdapter>().TryRead(session, result => final = result);
        Assert.IsTrue(final.Succeeded, final.Message);
        Assert.AreEqual(session, final.Record.sessionId);
        CollectionAssert.AreEqual(new[] { "B1", "B2" }, final.Record.benchmarkOrder);
        Assert.IsTrue(final.Record.IsComplete(2), "The final CSV must contain both complete feedback entries and a valid comparison.");
        Assert.AreEqual(TaskOutcome.Completed, final.Record.FindFeedback("B1").taskOutcome);
        Assert.AreEqual(TaskOutcome.Completed, final.Record.FindFeedback("B2").taskOutcome);
        Assert.AreEqual("B1", final.Record.finalPreferenceCombinationId);
        Assert.AreEqual("B1 room and B2 fit.", final.Record.finalTradeOffSummary);
        var conflict = final.Record.conflicts.Single();
        Assert.AreEqual("B2", conflict.combinationId);
        Assert.AreEqual("door-sweep/ACCESS_FridgeDoor/cabinet-left", conflict.issueId);
        Assert.AreEqual("ACCESS_FridgeDoor", conflict.objectId);
        Assert.AreEqual(ReviewRole.FridgeDoor, conflict.role);
        Assert.AreEqual("flush_panel_depth", conflict.assumptionKey);
        Assert.AreEqual(final.Record.modelVersion, conflict.modelVersion);
        Assert.That(conflict.measurement, Is.InRange(20f, 40f));
        Assert.That(conflict.sessionSeconds, Is.InRange(0f, final.Record.totalSeconds));
        Assert.IsTrue(conflict.feedbackRecorded);
        var boundaries = final.Record.comfortOrAbort.Where(item => item.type == "boundary_warning").ToArray();
        Assert.AreEqual(2, boundaries.Length, "A second warning after clearance should be recorded separately.");
        Assert.IsTrue(boundaries.All(item => item.combinationId == "B2" &&
            item.sessionSeconds > 0f && item.sessionSeconds <= final.Record.totalSeconds));
    }

    private static void AssertChallengeTextFits(Transform card)
    {
        foreach (var label in card.GetComponentsInChildren<TMP_Text>())
        {
            label.ForceMeshUpdate();
            Assert.IsFalse(label.isTextOverflowing, "Challenge label must fit: " + label.text);
        }
    }

    private static TaskEvidenceIdentity Identity(string id) =>
        Object.FindObjectsOfType<TaskEvidenceIdentity>().Single(item => item.StableId == id);

    private static void EnterTradeOff(JourneyPhaseFiveView shell, Transform keyboard, string text)
    {
        foreach (var character in text)
        {
            if (char.IsUpper(character)) ClickComparisonButton(shell, keyboard.Find("Shift"));
            var key = character == ' ' ? "Space" : "Key " + char.ToUpperInvariant(character);
            ClickComparisonButton(shell, keyboard.Find(key));
        }
    }

    private static void ClickComparisonButton(JourneyPhaseFiveView shell, Transform target) =>
        ClickTrackedButton(shell.GetComponent<TrackedDeviceGraphicRaycaster>(), target);

    private static void ClickTrackedButton(TrackedDeviceGraphicRaycaster raycaster, Transform target)
    {
        var hit = TrackedRayHit(raycaster, target, out var pointer);
        Assert.AreEqual(target.gameObject, hit, "Tracked ray must reach " + target.name);
        ExecuteEvents.Execute<IPointerClickHandler>(hit, pointer, ExecuteEvents.pointerClickHandler);
    }

    private static GameObject ComparisonRayHit(JourneyPhaseFiveView shell, Transform target) =>
        TrackedRayHit(shell.GetComponent<TrackedDeviceGraphicRaycaster>(), target, out _);

    private static GameObject TrackedRayHit(TrackedDeviceGraphicRaycaster raycaster, Transform target,
        out TrackedDeviceEventData pointer)
    {
        Canvas.ForceUpdateCanvases();
        var start = Camera.main.transform.position;
        pointer = new TrackedDeviceEventData(EventSystem.current)
        {
            button = PointerEventData.InputButton.Left,
            layerMask = -1,
            rayPoints = new List<Vector3> { start, start + (target.position - start).normalized * 3f }
        };
        var hits = new List<RaycastResult>();
        raycaster.Raycast(pointer, hits);
        return hits.Count > 0 ? hits[0].gameObject : null;
    }

    private static IEnumerator AssertBlockedOpening(JourneyRuntimeHost host, string benchmark, KinematicSwingDoor door)
    {
        // B1 uses an unregistered object. B2 moves one of its actual registered
        // trim pieces into the arc only for this positive test, then restores it.
        var registered = benchmark == "B2";
        var obstacle = registered ? GameObject.Find("TRIM_Filler_Left") : new GameObject("Loose obstacle in the F2 swing");
        Assert.NotNull(obstacle);
        if (!registered) obstacle.AddComponent<BoxCollider>();
        var pose = obstacle.transform;
        var position = pose.position;
        var scale = pose.localScale;
        var inSwing = Quaternion.Euler(0, 35, 0) * new Vector3(-.6f, 1f, 0);
        pose.position = door.transform.TransformPoint(inSwing);
        pose.localScale = Vector3.one * .09f;
        Physics.SyncTransforms();
        door.SetOpen(true);
        yield return new WaitForSeconds(1.2f);
        Assert.That(door.Progress, Is.InRange(.1, .85));
        Assert.AreEqual(ChallengeStep.Open, host.Core.Snapshot.CurrentStep,
            "A blocked door cannot satisfy the open task");

        var notices = host.Core.Snapshot.Conflicts;
        Assert.AreEqual(registered ? 1 : 0, notices.Count);
        if (registered)
        {
            var notice = notices.Single();
            Assert.AreEqual("B2", notice.BenchmarkId);
            Assert.AreEqual("door-sweep/ACCESS_FridgeDoor/cabinet-left", notice.IssueId);
            Assert.AreEqual("flush_panel_depth", notice.AssumptionKey);
            Assert.That(notice.Measurement, Is.InRange(20f, 40f));
            Assert.IsFalse(notice.FeedbackRecorded);
            var marker = GameObject.Find("ReviewDesignConflict");
            Assert.That(marker.GetComponentInChildren<TMP_Text>().text, Does.Contain("Design conflict"));
            yield return AssertBoundaryWarningDuringConflict(host, marker);
            pose.position = position;
            pose.localScale = scale;
        }
        else Object.Destroy(obstacle);
        yield return null;
        Physics.SyncTransforms();
    }

    private static IEnumerator AssertBoundaryWarningDuringConflict(JourneyRuntimeHost host, GameObject marker)
    {
        var boundary = host.GetComponent<ReviewBoundarySafety>();
        var apply = typeof(ReviewBoundarySafety).GetMethod("ApplyClearanceSample",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(apply);
        var run = host.Core.Snapshot.ActiveRunId;
        // Feed simulated distances through the production warning source and real CSV adapter.
        apply.Invoke(boundary, new object[] { .20f });
        var banner = GameObject.Find("Physical Play Area Warning");
        Assert.NotNull(banner);
        var caption = banner.GetComponent<TMP_Text>();
        Assert.That(caption.text, Does.Contain("SAFETY"));
        Assert.That(caption.color.g, Is.LessThan(.5f));
        Assert.IsFalse(marker.activeSelf);
        Assert.IsFalse(host.Core.Snapshot.Conflicts.Single().FeedbackRecorded);
        Assert.AreEqual(ChallengeStep.Open, host.Core.Snapshot.CurrentStep);

        apply.Invoke(boundary, new object[] { .30f });
        apply.Invoke(boundary, new object[] { null });
        Assert.IsTrue(banner.activeSelf, "Hysteresis and a missing sample must retain the warning.");
        var recovery = Object.FindObjectOfType<RecoveryMenuView>();
        recovery.Open();
        yield return null;
        var raycaster = recovery.GetComponent<TrackedDeviceGraphicRaycaster>();
        ClickTrackedButton(raycaster, recovery.transform.Find("Panel/Pause"));
        Assert.AreEqual(SessionState.Paused, host.Core.Snapshot.State);
        Assert.IsTrue(banner.activeSelf);
        ReadResult paused = null;
        host.GetComponent<LocalCsvStorageAdapter>().TryRead(host.Core.Snapshot.SessionId, value => paused = value);
        Assert.IsTrue(paused.Succeeded, paused.Message);
        var warning = paused.Record.comfortOrAbort.Single(item => item.type == "boundary_warning");
        Assert.AreEqual("B2", warning.combinationId);
        Assert.That(warning.sessionSeconds, Is.GreaterThan(0f));
        Assert.IsFalse(paused.Record.conflicts.Single().feedbackRecorded);

        apply.Invoke(boundary, new object[] { .36f });
        Assert.IsFalse(banner.activeSelf);
        Assert.IsNull(host.Core.Snapshot.CurrentSafety);
        Assert.IsFalse(marker.activeSelf, "Design geometry should stay hidden during pause.");
        yield return null;
        ClickTrackedButton(raycaster, recovery.transform.Find("Panel/Resume"));
        Assert.AreEqual(SessionState.ChallengeActive, host.Core.Snapshot.State);
        Assert.AreEqual(run, host.Core.Snapshot.ActiveRunId);
        Assert.AreEqual(ChallengeStep.Open, host.Core.Snapshot.CurrentStep);
        Assert.IsTrue(marker.activeSelf);
        Assert.IsFalse(recovery.IsOpen);

        apply.Invoke(boundary, new object[] { .20f });
        Assert.IsTrue(banner.activeSelf);
        apply.Invoke(boundary, new object[] { .36f });
        Assert.IsFalse(banner.activeSelf);
        Assert.IsTrue(marker.activeSelf);
    }

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

    private static void AssertRestoredStoragePositions(TaskEvidenceIdentity[] items)
    {
        var zone = Identity("ZONE_FreshDestination");
        var shelves = zone.GetComponents<BoxCollider>();
        var bounds = items.Select(item => item.Grab.colliders[0].bounds).ToArray();
        var usedShelves = bounds.Select(item => shelves.OrderBy(shelf =>
            Mathf.Abs(shelf.bounds.center.y - item.center.y)).First()).ToArray();
        CollectionAssert.AllItemsAreUnique(usedShelves, "Recovery must use the available refrigerator shelves.");
        for (var index = 0; index < bounds.Length; index++)
        {
            var item = bounds[index];
            Assert.IsTrue(zone.ContainsPoint(item.center));
            var supported = Physics.RaycastAll(new Vector3(item.center.x, item.min.y + .01f, item.center.z),
                Vector3.down, .025f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                .Any(hit => hit.collider.attachedRigidbody == null && hit.normal.y > .9f);
            Assert.IsTrue(supported, items[index].StableId + " needs a physical shelf directly below it.");
            for (var other = index + 1; other < bounds.Length; other++)
                Assert.IsFalse(item.Intersects(bounds[other]), "Recovered items must not overlap.");
        }
    }

    private static void MoveHead(float x, float z)
    {
        var rig = GameObject.Find("PREF_XR_KitchenRig").transform;
        var head = rig.GetComponentInChildren<Camera>().transform;
        rig.position += new Vector3(x - head.position.x, 0f, z - head.position.z);
        Physics.SyncTransforms();
    }

    private static string HeldState(UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grab)
    {
        var guard = grab.GetComponent<CollisionConstrainedGrabTransformer>();
        return grab.name + " position=" + grab.transform.position + " selected=" + grab.isSelected +
            " transformers=" + grab.singleGrabTransformersCount + " enabled=" + guard.enabled +
            " canProcess=" + guard.canProcess + " requested=" + guard.RequestedPose.position +
            " accepted=" + guard.AcceptedPose.position + " blocked=" + guard.IsBlocked +
            " saturated=" + guard.QuerySaturated + " scale=" + grab.transform.lossyScale;
    }

    private static XRDirectInteractor CreateTestInteractor(
        UnityEngine.XR.Interaction.Toolkit.XRInteractionManager manager, Vector3 position)
    {
        var hand = new GameObject("TestDirectInteractor");
        hand.transform.position = position;
        var trigger = hand.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = .02f;
        var interactor = hand.AddComponent<XRDirectInteractor>();
        interactor.interactionManager = manager;
        interactor.attachTransform = hand.transform;
        interactor.keepSelectedTargetValid = true;
        return interactor;
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
    public IEnumerator EditorPreviewCommandsRespectTheRuntimeJourney()
    {
        SceneManager.LoadScene("SCN_Kitchen_Review");
        yield return null;
        var host = Object.FindObjectOfType<JourneyRuntimeHost>();
        var adapter = host.GetComponent<RuntimeBenchmarkAdapter>();
        var menu = System.AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("XRApartment.EditorTools.KitchenBenchmarkMenu"))
            .FirstOrDefault(type => type != null);
        Assert.IsNotNull(menu, "Project authoring menu must remain available after the directory move.");
        var apply = menu.GetMethod("ApplyBenchmark");
        Assert.IsNotNull(apply);
        Assert.IsTrue(host.Submit(new JourneyCommand
        { Type = JourneyCommandType.CompleteSetup, PrerequisitesMet = true }).Accepted);
        Assert.IsTrue(host.Submit(new JourneyCommand
        { Type = JourneyCommandType.AcknowledgeOrientation, PrerequisitesMet = true }).Accepted);
        var water = KitchenWaterSystem.Instance;
        foreach (var benchmark in new[] { "B1", "B2" })
        {
            apply.Invoke(null, new object[] { benchmark });
            yield return null;
            Assert.AreEqual(benchmark, adapter.CurrentBenchmarkId);
            Assert.AreEqual(benchmark, host.Core.Snapshot.DisplayedBenchmarkId,
                "The editor menu must update Core as well as the visible fridge.");
            Assert.AreSame(water, KitchenWaterSystem.Instance);
        }
        var start = host.Submit(new JourneyCommand
        { Type = JourneyCommandType.StartChallenge, BenchmarkId = "B1" });
        Assert.IsTrue(start.Accepted, start.Message);
        var activeDoor = Identity("ACCESS_FridgeDoor");
        apply.Invoke(null, new object[] { "B2" });
        yield return null;
        Assert.AreEqual(SessionState.ChallengeActive, host.Core.Snapshot.State);
        Assert.AreEqual("B1", adapter.CurrentBenchmarkId,
            "Preview must not bypass the active task's workflow guard.");
        Assert.AreSame(activeDoor, Identity("ACCESS_FridgeDoor"),
            "Rejected preview must preserve task targets and evidence bindings.");
    }

    [UnityTest]
    public IEnumerator WorldSpaceExplorePreviewsBothBenchmarksAndStartsTheNextChallenge()
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
        yield return null;
        var canvas = GameObject.Find("PREF_UI_ExploreChallenge").transform;
        var raycaster = canvas.GetComponent<TrackedDeviceGraphicRaycaster>();
        ClickTrackedButton(raycaster, canvas.Find("ExplorePanel/BeginExplore"));
        yield return null;
        var toolbar = canvas.Find("ExploreToolbar");
        var title = toolbar.Find("Design").GetComponent<TMP_Text>();
        var status = toolbar.Find("Status").GetComponent<TMP_Text>();
        var challenge = toolbar.Find("Challenge").GetComponent<Button>();
        var adapter = host.GetComponent<RuntimeBenchmarkAdapter>();
        Assert.IsFalse(challenge.interactable);
        Assert.That(status.text, Does.Contain("same F2 fridge"));
        Assert.AreEqual("XR Apartment/Review Keyboard Surface", toolbar.GetComponent<Image>().material.shader.name);

        ClickTrackedButton(raycaster, toolbar.Find("PreviewB2"));
        yield return null;
        Assert.AreEqual("B2", adapter.CurrentBenchmarkId);
        Assert.AreEqual("Preview B2", title.text);
        Assert.That(status.text, Does.Contain("Flush + F2").And.Contain("opening clearance"));
        Assert.That(status.text, Does.Contain("Next Challenge  |  B1"));
        title.ForceMeshUpdate();
        Assert.IsFalse(title.isTextOverflowing);
        Assert.IsFalse(challenge.interactable, "Previewing out of order must leave the task order intact.");
        Assert.AreEqual(0, host.Core.Snapshot.ActiveRunId);

        ClickTrackedButton(raycaster, toolbar.Find("PreviewB1"));
        yield return null;
        Assert.AreEqual("B1", adapter.CurrentBenchmarkId);
        Assert.AreEqual("Preview B1", title.text);
        Assert.That(status.text, Does.Contain("Proud + F2").And.Contain("passing by"));
        status.ForceMeshUpdate();
        Assert.IsFalse(status.isTextOverflowing);
        Assert.IsTrue(challenge.interactable);
        ClickTrackedButton(raycaster, challenge.transform);
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
        Assert.That(status.text, Does.Contain("Next Challenge  |  B2"));
        ClickTrackedButton(raycaster, toolbar.Find("PreviewB2"));
        yield return null;
        Assert.AreEqual("B2", adapter.CurrentBenchmarkId);
        status.ForceMeshUpdate();
        Assert.IsFalse(status.isTextOverflowing);
        ClickTrackedButton(raycaster, challenge.transform);
        yield return null;
        Assert.AreEqual("B2", host.Core.Snapshot.ActiveBenchmarkId);
        Assert.AreEqual(ChallengeStep.Approach, host.Core.Snapshot.CurrentStep);
    }
}
