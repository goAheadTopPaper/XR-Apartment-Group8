using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using XRApartment.Data;
using XRApartment.Runtime.UI;
using XRApartment.Simulation;

namespace XRApartment.Tests
{
    public class JourneyWorldSpaceViewTests
    {
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
                Assert.That(instance.transform.Find("Panel/State").GetComponent<Text>().text,
                    Does.Contain("Setup"));
                Assert.That(instance.transform.Find("Panel/Design").GetComponent<Text>().text,
                    Does.Contain("B1"));
                Assert.That(instance.transform.Find("Panel/Progress").GetComponent<Text>().text,
                    Does.Contain("2/8"));
                Assert.That(instance.transform.Find("Panel/Error").GetComponent<Text>().text,
                    Does.Contain("Required setup"));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }
    }
}
