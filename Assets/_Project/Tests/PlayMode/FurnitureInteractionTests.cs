#if UNITY_EDITOR
using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using XRApartment.Runtime;
using Object = UnityEngine.Object;

namespace XRApartment.Tests
{
    public class FurnitureInteractionTests
    {
        GameObject fridge;

        [TearDown]
        public void TearDown()
        {
            if (fridge != null) Object.DestroyImmediate(fridge);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TogglingMotionEnabled_PreservesOtherSelectionListeners(bool useDrawer)
        {
            fridge = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/PREF_Fridge_F4.prefab"));
            fridge.transform.position = new Vector3(100f, 50f, 100f);
            var door = fridge.GetComponentInChildren<KinematicSwingDoor>();
            var drawer = fridge.GetComponentInChildren<KinematicLinearDrawer>();
            Behaviour motion = useDrawer ? (Behaviour)drawer : door;
            Func<bool> isOpen = useDrawer ? (Func<bool>)(() => drawer.IsOpen) : () => door.IsOpen;
            var interaction = motion.GetComponent<XRSimpleInteractable>();
            var notifications = 0;
            interaction.selectEntered.AddListener(_ => notifications++);

            for (var cycle = 0; cycle < 3; cycle++)
            {
                motion.enabled = false;
                motion.enabled = true;
            }
            interaction.selectEntered.Invoke(new SelectEnterEventArgs());
            Assert.AreEqual(1, notifications, "Motion must remove only its own listener.");
            Assert.IsTrue(isOpen(), "Re-enabling must not duplicate the toggle listener.");
            interaction.selectEntered.Invoke(new SelectEnterEventArgs());
            Assert.AreEqual(2, notifications);
            Assert.IsFalse(isOpen());
        }
    }
}
#endif
