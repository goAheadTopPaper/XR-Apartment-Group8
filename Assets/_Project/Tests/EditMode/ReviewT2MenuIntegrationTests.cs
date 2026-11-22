using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using XRApartment.Runtime.ReviewV2;
using XRApartment.Runtime.ReviewV2.UI;

namespace XRApartment.Tests
{
    public class ReviewT2MenuIntegrationTests
    {
        GameObject root;
        [SetUp] public void SetUp() { root = new GameObject("Menu integration fixture"); root.SetActive(false); }
        [TearDown] public void TearDown() { Object.DestroyImmediate(root); }
        ReviewV2UiController Controller()
        {
            var c = root.AddComponent<ReviewV2UiController>();
            var registrations = new ReviewV2UiController.PanelRegistration[3];
            var ids = new[] { "global_menu", "task_book", "help" };
            for (int i=0;i<3;i++)
            {
                var p=new GameObject(ids[i]); p.transform.SetParent(root.transform); p.SetActive(false);
                registrations[i]=new ReviewV2UiController.PanelRegistration { id=ids[i], panel=p };
            }
            typeof(ReviewV2UiController).GetField("panels",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(c,registrations);
            typeof(ReviewV2UiController).GetMethod("Awake",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(c,null);
            root.SetActive(true);
            return c;
        }
        [Test] public void ChildNavigationNeverReleasesGroupLock()
        {
            var c=Controller(); int changes=0; c.VisibilityChanged+=()=>changes++;
            Assert.True(c.ShowPanel("global_menu")); Assert.True(c.AnyPanelVisible);
            c.ShowPanel("task_book"); c.ShowPanel("help"); c.ShowPanel("global_menu");
            Assert.AreEqual(1,changes); Assert.True(c.AnyPanelVisible);
            c.HideAll(); Assert.False(c.AnyPanelVisible); Assert.AreEqual(2,changes);
            c.HideAll(); Assert.AreEqual(2,changes);
        }
        [Test] public void InvalidNavigationDoesNotUnlockVisibleMenu()
        {
            var c=Controller(); c.ShowPanel("global_menu");
            Assert.False(c.ShowPanel("missing")); Assert.True(c.AnyPanelVisible);
        }
        [Test] public void ExternalBlockPreservesExistingHandButRejectsNewSelection()
        {
            var gate=root.AddComponent<ReviewWorldInteractionGate>();
            gate.host=root.AddComponent<ReviewSessionHost>();
            var a=new GameObject("object"); a.transform.SetParent(root.transform);
            var target=a.AddComponent<XRSimpleInteractable>();
            var b=new GameObject("hand"); b.transform.SetParent(root.transform);
            var hand=b.AddComponent<XRDirectInteractor>();
            gate.SetExternalMenuBlocked(true);
            Assert.False(gate.Process((IXRSelectInteractor)hand,(IXRSelectInteractable)target));
            target.interactorsSelecting.Add(hand);
            Assert.True(gate.Process((IXRSelectInteractor)hand,(IXRSelectInteractable)target));
            Assert.AreEqual(1,target.interactorsSelecting.Count);
            gate.SetExternalMenuBlocked(false);
            Assert.True(gate.Process((IXRSelectInteractor)hand,(IXRSelectInteractable)target));
        }
        [Test] public void ClosingExternalMenuCannotRemoveCoreHardBlock()
        {
            var gate=root.AddComponent<ReviewWorldInteractionGate>();
            gate.host=root.AddComponent<ReviewSessionHost>();
            typeof(ReviewWorldInteractionGate).GetField("blocked",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(gate,true);
            gate.SetExternalMenuBlocked(true); gate.SetExternalMenuBlocked(false);
            Assert.False(gate.Process((IXRSelectInteractor)null,(IXRSelectInteractable)null));
        }
    }
}
