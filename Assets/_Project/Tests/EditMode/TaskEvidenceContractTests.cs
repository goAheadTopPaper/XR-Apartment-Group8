using System;
using NUnit.Framework;
using XRApartment.Data;
using XRApartment.Simulation;

namespace XRApartment.Tests
{
    public class TaskEvidenceContractTests
    {
        [Test]
        public void PublicContracts_KeepDataSimulationRuntimeDependencyDirection()
        {
            Assert.AreEqual("XRApartment.Data", typeof(SessionOutcome).Assembly.GetName().Name);
            Assert.AreEqual("XRApartment.Simulation", typeof(JourneyCoordinator).Assembly.GetName().Name);
            Assert.AreEqual("XRApartment.Simulation", typeof(TaskEvidence).Assembly.GetName().Name);
            foreach (var reference in typeof(JourneyCoordinator).Assembly.GetReferencedAssemblies())
                Assert.IsFalse(reference.Name == "XRApartment.Runtime" || reference.Name == "UnityEngine.CoreModule",
                    "Simulation must not depend on Runtime or UnityEngine: " + reference.Name);
        }

        [Test]
        public void Contract_ProvidesTheExpectedReplaceablePorts()
        {
            Assert.IsTrue(typeof(IReviewScenePort).IsInterface);
            Assert.IsTrue(typeof(IFeedbackPort).IsInterface);
            Assert.IsTrue(typeof(ISessionStore).IsInterface);
            Assert.IsTrue(typeof(IJourneyView).IsInterface);
            Assert.AreEqual(typeof(Action<SceneActionResult>),
                typeof(IReviewScenePort).GetMethod("Execute").GetParameters()[1].ParameterType);
        }
    }
}
