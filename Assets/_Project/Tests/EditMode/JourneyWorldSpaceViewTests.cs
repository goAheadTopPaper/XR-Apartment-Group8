using NUnit.Framework;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.XR.Interaction.Toolkit.UI;
using XRApartment.Data;
using XRApartment.Runtime.UI;
using XRApartment.Simulation;

namespace XRApartment.Tests
{
    public class JourneyWorldSpaceViewTests
    {
        [Test]
        public void ComparisonInput_EnablesCompletedB1AndDisablesSkippedB2()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/UI/PREF_UI_JourneyPhaseFive.prefab");
            Assert.NotNull(prefab);
            var instance = Object.Instantiate(prefab);
            try
            {
                var shell = instance.GetComponent<JourneyPhaseFiveView>();
                shell.Render(new JourneySnapshot
                {
                    State = XRApartment.Simulation.SessionState.ComparisonSummary,
                    BenchmarkOutcomes = new Dictionary<string, TaskOutcome>
                    { { "B1", TaskOutcome.Completed }, { "B2", TaskOutcome.Skipped } }
                }, JourneyErrorCode.None, string.Empty);
                var input = instance.GetComponentInChildren<JourneyComparisonInputView>(true);
                Assert.NotNull(input, "The final comparison needs an input view.");
                var buttons = input.GetComponentsInChildren<Button>(true);
                Assert.IsTrue(System.Array.Find(buttons, button => button.name == "Prefer B1").interactable);
                Assert.IsFalse(System.Array.Find(buttons, button => button.name == "Prefer B2").interactable);
                Assert.AreEqual("B1", input.Capture().PreferredBenchmarkId);
                var shortcut = System.Array.Find(buttons, button => button.name == "Use skip context");
                Assert.IsTrue(shortcut.gameObject.activeSelf);
                Assert.That(shortcut.GetComponent<Image>().color.grayscale, Is.LessThan(.5f),
                    "The shortcut must have a dark background so its white label is visible.");
                Assert.AreEqual("Use skip context", shortcut.GetComponentInChildren<TMP_Text>().text);
                shortcut.onClick.Invoke();
                Assert.That(input.Capture().TradeOffSummary, Does.Contain("B2 was skipped"));
                input.GetComponentInChildren<TMP_InputField>(true).text = "B1 has easier circulation.";
                Assert.AreEqual("B1 has easier circulation.", input.Capture().TradeOffSummary);
                input.Open(new JourneySnapshot
                {
                    BenchmarkOutcomes = new Dictionary<string, TaskOutcome>
                    { { "B1", TaskOutcome.Skipped }, { "B2", TaskOutcome.Completed } }
                }, new FinalComparison());
                Assert.AreEqual("B2", input.Capture().PreferredBenchmarkId);
                shortcut.onClick.Invoke();
                Assert.That(input.Capture().TradeOffSummary, Does.Contain("B1 was skipped"));
                Assert.IsTrue(instance.transform.Find("ComparisonPanel/ConfirmComparison")
                    .GetComponent<Button>().interactable);
            }
            finally { Object.DestroyImmediate(instance); }
        }

        private const string PrefabPath =
            "Assets/_Project/Prefabs/UI/PREF_UI_JourneyStageOne.prefab";

        [Test]
        public void PrefabHasWorldSpaceCanvasAndWiredSetupControls()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab, "The stage-one UI prefab was not generated.");
            Assert.AreEqual(RenderMode.WorldSpace, prefab.GetComponent<Canvas>().renderMode);
            Assert.IsNotNull(prefab.GetComponent<TrackedDeviceGraphicRaycaster>());
            Assert.IsNotNull(prefab.GetComponent<JourneyWorldSpaceView>());
            Assert.IsNotNull(prefab.transform.Find("Panel/SetupControls/CompleteSetup")
                .GetComponent<Button>());

            var instance = Object.Instantiate(prefab);
            try
            {
                var view = instance.GetComponent<JourneyWorldSpaceView>();
                view.Render(new JourneySnapshot { State = XRApartment.Simulation.SessionState.Setup,
                    DisplayedBenchmarkId = "B1", CurrentStep = ChallengeStep.Open,
                    CompletedItemCount = 1, TotalItemCount = 3 },
                    JourneyErrorCode.InvalidState, "Required setup is incomplete.");
                Assert.That(instance.transform.Find("Panel/State").GetComponent<TMP_Text>().text,
                    Does.Contain("Setup"));
                Assert.That(instance.transform.Find("Panel/Design").GetComponent<TMP_Text>().text,
                    Does.Contain("B1"));
                Assert.That(instance.transform.Find("Panel/Progress").GetComponent<TMP_Text>().text,
                    Does.Contain("2/8"));
                Assert.That(instance.transform.Find("Panel/Error").GetComponent<TMP_Text>().text,
                    Does.Contain("Required setup"));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }
    }
}
