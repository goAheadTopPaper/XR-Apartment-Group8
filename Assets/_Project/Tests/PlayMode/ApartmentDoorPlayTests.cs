using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using XRApartment.Runtime;
using XRApartment.Runtime.UI;

namespace XRApartment.Tests
{
    public sealed class ApartmentDoorPlayTests
    {
        [UnityTest]
        public IEnumerator ExteriorDoorsStopWalkingAndTeleportingWhileThePassageStaysOpen()
        {
            foreach (var scene in new[] { "SCN_Kitchen_Cooking", "SCN_Kitchen_Review" })
            {
                SceneManager.LoadScene(scene);
                yield return null;
                var doors = GameObject.Find("ApartmentExteriorDoors").transform;
                var locomotion = Object.FindObjectOfType<KitchenLocomotionSettings>();
                KitchenSceneTestSetup.DisableStartupGuide(locomotion.GetComponentInChildren<LocomotionSettingsView>());
                var teleport = locomotion.TeleportProvider;
                Assert.AreEqual(4, doors.childCount, scene);
                var motor = new GameObject("Door route character").AddComponent<CharacterController>();
                motor.height = locomotion.Character.height;
                motor.radius = locomotion.Character.radius;
                motor.center = locomotion.Character.center;
                motor.skinWidth = locomotion.Character.skinWidth;
                motor.minMoveDistance = 0f;
                try
                {
                    foreach (Transform door in doors)
                    {
                        var barrier = door.GetComponent<BoxCollider>();
                        Assert.IsTrue(barrier.enabled && !barrier.isTrigger, door.name);
                        foreach (var side in new[] { -.35f, 0f, .35f })
                        {
                            var inside = door.TransformPoint(new Vector3(side, .01f, -.34f));
                            SetPosition(motor, inside);
                            var blocked = false;
                            for (var step = 0; step < 36; step++)
                                blocked |= (motor.Move(door.forward * .025f) & CollisionFlags.Sides) != 0;
                            Assert.IsTrue(blocked, scene + " " + door.name + " must stop the character.");
                            Assert.Less(door.InverseTransformPoint(motor.transform.position).z, -.025f,
                                scene + " " + door.name + " was crossed.");
                        }

                        motor.enabled = false;
                        // Aim with the actual player from the same unobstructed approach to each door.
                        var start = door.TransformPoint(new Vector3(0f, .004f, -.42f));
                        var head = locomotion.Origin.Camera.transform.position;
                        locomotion.Origin.transform.position += start - new Vector3(head.x, .004f, head.z);
                        Physics.SyncTransforms();
                        Assert.IsTrue(teleport.CanTeleportTo(start - door.forward * .10f),
                            scene + " " + door.name + " needs a usable interior approach.");
                        Assert.IsFalse(teleport.CanTeleportTo(door.TransformPoint(new Vector3(0f, .004f, .43f))),
                            scene + " " + door.name + " must block an exterior teleport.");
                    }

                    locomotion.Origin.transform.position = new Vector3(-3.15f, 0f, 1.12f);
                    var passageStart = new Vector3(-.43f, .02f, -7.37f);
                    SetPosition(motor, passageStart);
                    for (var step = 0; step < 374; step++)
                        motor.Move(Vector3.forward * .025f);
                    Assert.That(motor.transform.position.z, Is.GreaterThan(1.8f), scene + " passage stays walkable.");
                }
                finally { Object.Destroy(motor.gameObject); }
            }
        }

        static void SetPosition(CharacterController motor, Vector3 position)
        {
            motor.enabled = false;
            motor.transform.position = position;
            motor.enabled = true;
            Physics.SyncTransforms();
        }
    }
}
