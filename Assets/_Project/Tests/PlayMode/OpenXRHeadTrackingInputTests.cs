using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.XR;
using InputDeviceCharacteristics = UnityEngine.XR.InputDeviceCharacteristics;

namespace XRApartment.Tests
{
    public sealed class OpenXRHeadTrackingInputTests
    {
        const string HmdLayout = "OpenXRHmd";
        InputDevice device;
        string generatedLayout;

        [TearDown]
        public void TearDown()
        {
            if (device != null)
                InputSystem.RemoveDevice(device);
            if (generatedLayout != null)
                InputSystem.RemoveLayout(generatedLayout);
        }

        [Test]
        public void HmdCanBeCreatedFromTheBaseLayout()
        {
            // The Input System uses this layout directly when rematching an available device.
            device = InputSystem.AddDevice(HmdLayout);

            Assert.That(device, Is.InstanceOf<XRHMD>());
            Assert.That(device.GetChildControl<ButtonControl>("UserPresence"), Is.Not.Null);
        }

        [Test]
        public void HmdUsesTheDeviceReportedUserPresenceOffset()
        {
            var descriptor = new XRDeviceDescriptor
            {
                characteristics = InputDeviceCharacteristics.HeadMounted,
                inputFeatures = new List<XRFeatureDescriptor>
                {
                    new XRFeatureDescriptor { name = "CenterEyePosition", featureType = FeatureType.Axis3D },
                    new XRFeatureDescriptor { name = "UserPresence", featureType = FeatureType.Binary }
                }
            };
            device = InputSystem.AddDevice(new InputDeviceDescription
            {
                // Use a separate generated layout from the current XRInputV1 device.
                interfaceName = XRUtilities.InterfaceV1,
                manufacturer = "OpenXR",
                product = "Head Tracking - OpenXR",
                capabilities = descriptor.ToJson()
            });
            generatedLayout = device.layout;

            Assert.That(InputSystem.IsFirstLayoutBasedOnSecond(generatedLayout, HmdLayout), Is.True);
            var presence = device.GetChildControl<ButtonControl>("UserPresence");
            Assert.That(presence.stateBlock.byteOffset - device.stateBlock.byteOffset, Is.EqualTo(3u * sizeof(float)));
        }
    }
}
