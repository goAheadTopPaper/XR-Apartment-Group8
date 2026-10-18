using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using XRApartment.Runtime;
using XRApartment.Runtime.Core;
using XRApartment.Runtime.UI;
using XRApartment.Simulation;

public class RecoveryMenuPlayTests
{
    [UnityTest]
    public IEnumerator ExitRequiresConfirmationAndUsesTheSceneHost()
    {
        SceneManager.LoadScene("SCN_Kitchen_Review");
        yield return null;
        var host = Object.FindObjectOfType<JourneyRuntimeHost>();
        var menu = Object.FindObjectOfType<RecoveryMenuView>(true);
        var oldMenu = Object.FindObjectOfType<KitchenMenuController>(true);
        Assert.IsNotNull(host);
        Assert.IsNotNull(menu);
        Assert.IsNotNull(oldMenu);
        Assert.IsFalse(oldMenu.gameObject.activeInHierarchy);
        Assert.AreEqual(1, Object.FindObjectsOfType<EventSystem>(true).Length);
        Assert.AreEqual(RenderMode.WorldSpace, menu.GetComponent<Canvas>().renderMode);
        Assert.IsNotNull(menu.GetComponent<TrackedDeviceGraphicRaycaster>());

        menu.Open();
        var panel = menu.transform.Find("Panel");
        Assert.IsTrue(panel.gameObject.activeInHierarchy);
        panel.Find("SafeExit").GetComponent<Button>().onClick.Invoke();
        Assert.IsTrue(panel.Find("ExitConfirmation").gameObject.activeSelf);
        Assert.AreEqual(SessionState.Setup, host.Core.Snapshot.State);
        panel.Find("ExitConfirmation/ConfirmExit").GetComponent<Button>().onClick.Invoke();
        Assert.AreEqual(SessionState.Exited, host.Core.Snapshot.State);
    }

    [UnityTest]
    public IEnumerator RestoreStepKeepsRunAndResetTaskStartsANewRun()
    {
        SceneManager.LoadScene("SCN_Kitchen_Review");
        yield return null;
        var host = Object.FindObjectOfType<JourneyRuntimeHost>();
        var menu = Object.FindObjectOfType<RecoveryMenuView>(true);
        Assert.IsNull(host.StartupError);
        Assert.IsTrue(host.Submit(new JourneyCommand
            { Type = JourneyCommandType.CompleteSetup, PrerequisitesMet = true }).Accepted);
        Assert.IsTrue(host.Submit(new JourneyCommand
            { Type = JourneyCommandType.AcknowledgeOrientation, PrerequisitesMet = true }).Accepted);
        Assert.IsTrue(host.Submit(new JourneyCommand
            { Type = JourneyCommandType.StartChallenge, BenchmarkId = "B1" }).Accepted);
        yield return null;
        menu.Open();
        var panel = menu.transform.Find("Panel");
        var restore = panel.Find("RestoreStep").GetComponent<Button>();
        var reset = panel.Find("ResetTask").GetComponent<Button>();
        Assert.IsTrue(restore.interactable);
        Assert.IsTrue(reset.interactable);
        Assert.IsFalse(panel.Find("ReturnItem").gameObject.activeSelf);
        var run = host.Core.Snapshot.ActiveRunId;
        var step = host.Core.Snapshot.CurrentStep;
        restore.onClick.Invoke();
        yield return null;
        Assert.AreEqual(run, host.Core.Snapshot.ActiveRunId);
        Assert.AreEqual(step, host.Core.Snapshot.CurrentStep);
        reset.onClick.Invoke();
        yield return null;
        Assert.Greater(host.Core.Snapshot.ActiveRunId, run);
        Assert.AreEqual(ChallengeStep.Approach, host.Core.Snapshot.CurrentStep);
    }
}
