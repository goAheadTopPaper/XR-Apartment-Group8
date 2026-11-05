using NUnit.Framework;
using UnityEngine;
using XRApartment.Runtime.Core;

namespace XRApartment.Tests
{
    public class ReviewBoundarySafetyTests
    {
        private static readonly Vector3[] Bounds =
        {
            new Vector3(-1f, 0f, -1.5f), new Vector3(-1f, 0f, 1.5f),
            new Vector3(1f, 0f, 1.5f), new Vector3(1f, 0f, -1.5f)
        };

        [TestCase(0f, 0f, 1f)]
        [TestCase(.8f, 0f, .2f)]
        [TestCase(1f, 0f, 0f)]
        [TestCase(1.2f, 0f, -.2f)]
        [TestCase(1.3f, 1.9f, -.5f)]
        public void ClearanceUsesHorizontalTrackingCoordinates(float x, float z, float expected)
        {
            Assert.IsTrue(ReviewBoundarySafety.TryGetClearance(Bounds, new Vector3(x, 1.7f, z), out var distance));
            Assert.That(distance, Is.EqualTo(expected).Within(.001f));
        }

        [Test]
        public void MissingOrDegenerateBoundsCannotGenerateAWarning()
        {
            Assert.IsFalse(ReviewBoundarySafety.TryGetClearance(null, Vector3.zero, out _));
            Assert.IsFalse(ReviewBoundarySafety.TryGetClearance(new[] { Vector3.zero, Vector3.right,
                Vector3.right * 2f }, Vector3.zero, out _));
        }
    }
}
