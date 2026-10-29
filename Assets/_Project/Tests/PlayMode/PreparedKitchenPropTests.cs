using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using XRApartment.Data;
using XRApartment.Runtime;

namespace XRApartment.Tests
{
    public sealed class PreparedKitchenPropTests
    {
        GameObject root;
        PreparedPropSettings profile;
        GameObject managerObject, handObject;

        [TearDown]
        public void Cleanup()
        {
            if (root != null) Object.DestroyImmediate(root);
            if (handObject != null) Object.DestroyImmediate(handObject);
            if (managerObject != null) Object.DestroyImmediate(managerObject);
            if (profile != null) Object.DestroyImmediate(profile);
        }

        PreparedKitchenProp Create(PreparedPropKind kind, bool withGrab = false)
        {
            profile = ScriptableObject.CreateInstance<PreparedPropSettings>();
            root = new GameObject("Prepared prop test fixture");
            var body = root.AddComponent<Rigidbody>();
            body.useGravity = false;
            var intact = GameObject.CreatePrimitive(PrimitiveType.Cube);
            intact.transform.SetParent(root.transform, false);
            var group = new GameObject("DerivedPieces");
            group.transform.SetParent(root.transform, false);
            var pieces = new Rigidbody[2];
            for (var i = 0; i < 2; i++)
            {
                var piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
                piece.transform.SetParent(group.transform, false);
                piece.transform.localScale = new Vector3(.5f, 1, 1);
                piece.transform.localPosition = new Vector3(i == 0 ? -.25f : .25f, 0, 0);
                pieces[i] = piece.AddComponent<Rigidbody>();
            }
            group.SetActive(false);
            if (withGrab)
            {
                var grab = root.AddComponent<XRGrabInteractable>();
                grab.colliders.Clear();
                grab.colliders.Add(intact.GetComponent<Collider>());
                grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
                grab.throwOnDetach = false;
            }
            var prop = root.AddComponent<PreparedKitchenProp>();
            prop.Configure(profile, kind, intact.transform, group.transform, pieces);
            return prop;
        }

        [Test]
        public void ImpactSwapsOnceAndResetReusesAlignedPieces()
        {
            var prop = Create(PreparedPropKind.Fracture);
            var count = 0;
            prop.Split += _ => count++;
            Assert.IsFalse(prop.TryImpact(.1f));
            Assert.IsTrue(prop.TryImpact(2));
            Assert.IsFalse(prop.TryImpact(2));
            Assert.AreEqual(1, count);
            Assert.IsFalse(root.transform.Find("Cube").gameObject.activeSelf);
            Assert.IsTrue(root.GetComponent<Rigidbody>().isKinematic);
            prop.ResetProp();
            Assert.IsFalse(prop.IsSplit);
            Assert.IsFalse(root.GetComponent<Rigidbody>().isKinematic);
            var group = root.transform.Find("DerivedPieces");
            Assert.IsFalse(group.gameObject.activeSelf);
            Assert.AreEqual(new Vector3(-.25f, 0, 0), group.GetChild(0).localPosition);
            Assert.IsTrue(prop.TryImpact(2));
            Assert.AreEqual(2, count);
        }

        [Test]
        public void FoodRequiresKnifeSpeedAndIgnoresImpact()
        {
            var prop = Create(PreparedPropKind.Slice);
            Assert.IsFalse(prop.TryImpact(20));
            Assert.IsFalse(prop.TryCut(.01f));
            Assert.IsFalse(prop.TryCut(float.PositiveInfinity));
            Assert.IsFalse(prop.TryCut(float.NaN));
            Assert.IsTrue(prop.TryCut(.3f));
            Assert.IsFalse(prop.TryCrack());
        }

        [UnityTest]
        public IEnumerator ExpiredDebrisIsHiddenAndCanBeReused()
        {
            var prop = Create(PreparedPropKind.Eggshell);
            profile.fragmentLifetime = .02f;
            Assert.IsTrue(prop.TryCrack());
            var fragment = Object.FindObjectsOfType<Rigidbody>();
            var pieces = System.Array.FindAll(fragment, r => r.transform.parent != null && r.transform.parent.name == "DerivedPieces");
            Assert.AreEqual(2, pieces.Length);
            yield return new WaitForSeconds(.05f);
            Assert.IsFalse(pieces[0].gameObject.activeInHierarchy);
            prop.ResetProp();
            Assert.IsTrue(prop.TryCrack());
            Assert.IsTrue(pieces[0].gameObject.activeInHierarchy);
        }

        [UnityTest]
        public IEnumerator CutIngredientsStayAvailableAfterDebrisTimeout()
        {
            var prop = Create(PreparedPropKind.Slice);
            profile.fragmentLifetime = .02f;
            Assert.IsTrue(prop.TryCut(1));
            yield return new WaitForSeconds(.06f);
            Assert.IsTrue(prop.Pieces[0].gameObject.activeInHierarchy, "Food must remain available for cooking after the short debris timeout.");
            prop.ResetProp();
            Assert.IsFalse(prop.Pieces[0].gameObject.activeInHierarchy);
        }

        [UnityTest]
        public IEnumerator SplitCancelsRealXriSelectionAndResetCanBeGrabbedAgain()
        {
            managerObject = new GameObject("Prepared prop interaction manager");
            var manager = managerObject.AddComponent<XRInteractionManager>();
            handObject = new GameObject("Prepared prop driven hand");
            var hand = handObject.AddComponent<DrivenGrabInteractor>();
            hand.interactionManager = manager;
            hand.keepSelectedTargetValid = true;
            var prop = Create(PreparedPropKind.Slice, withGrab: true);
            root.transform.position = new Vector3(100, 50, 100);
            hand.transform.position = root.transform.position;
            var grab = root.GetComponent<XRGrabInteractable>();
            grab.interactionManager = manager;
            yield return null;
            manager.SelectEnter((IXRSelectInteractor)hand, grab);
            Assert.IsTrue(grab.isSelected);
            Assert.IsTrue(prop.TryCut(.3f));
            yield return new WaitForFixedUpdate();
            Assert.IsFalse(grab.isSelected);
            Assert.IsFalse(grab.enabled);
            Assert.IsTrue(root.GetComponent<Rigidbody>().isKinematic);
            prop.ResetProp();
            yield return null;
            Assert.IsFalse(root.GetComponent<Rigidbody>().isKinematic);
            manager.SelectEnter((IXRSelectInteractor)hand, grab);
            Assert.IsTrue(grab.isSelected);
            manager.SelectExit((IXRSelectInteractor)hand, grab);
        }
    }
}
