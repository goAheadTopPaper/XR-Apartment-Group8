#if OPENXR_1_17
using UnityEngine;
using UnityEngine.InputSystem;

namespace XRApartment.Runtime
{
    static class OpenXRHeadTrackingLayout
    {
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#else
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
#endif
        static void RegisterUserPresence()
        {
            // OpenXR 1.17's private userPresence property is skipped by layout reflection.
            // Declare it before OpenXR registers the layout. XR device descriptors supply their own offsets.
            InputSystem.RegisterLayoutOverride(@"{
                ""name"": ""XRApartmentOpenXRUserPresence"",
                ""extend"": ""OpenXRHmd"",
                ""controls"": [{ ""name"": ""userPresence"", ""layout"": ""Button"" }]
            }");
        }
    }
}
#endif
