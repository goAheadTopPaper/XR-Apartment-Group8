using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;
using UnityEngine.XR.Interaction.Toolkit.UI;
using XRApartment.Runtime;
using XRApartment.Runtime.UI;
using Object = UnityEngine.Object;

namespace XRApartment.EditorTools
{
    /// <summary>Configures shared rig/floor prefabs without rewriting scenes, meshes, or baked lighting.</summary>
    public static class KitchenLocomotionAuthoring
    {
        const string RigPath = KitchenAuthoring.Prefabs + "PREF_XR_KitchenRig.prefab";
        const string MenuPath = KitchenAuthoring.Prefabs + "UI/PREF_UI_LocomotionSettings.prefab";
        const string TeleportPath = KitchenAuthoring.Prefabs + "PREF_Kitchen_TeleportArea.prefab";
        const string EnvironmentPath = KitchenAuthoring.Prefabs + "PREF_Kitchen_Environment.prefab";

        [MenuItem("XR Apartment/Movement/Configure Movement Modes")]
        public static void Configure()
        {
            KitchenAuthoring.RequireEditMode();
            BuildMenu();
            KitchenAuthoring.EditPrefab(RigPath, root =>
            {
                var provider = root.GetComponentInChildren<TeleportationProvider>(true);
                if (!(provider is KitchenTeleportationProvider))
                    ReplaceScript(provider, "Interaction/KitchenTeleportationProvider.cs");
            });
            KitchenAuthoring.EditPrefab(RigPath, ConfigureRig);
            KitchenAuthoring.EditPrefab(TeleportPath, root =>
            {
                var area = root.GetComponent<TeleportationArea>();
                if (!(area is KitchenTeleportationArea)) ReplaceScript(area, "Interaction/KitchenTeleportationArea.cs");
            });
            KitchenAuthoring.EditPrefab(TeleportPath, root => ConfigureArea(root.GetComponent<KitchenTeleportationArea>()));
            KitchenAuthoring.EditPrefab(EnvironmentPath, root =>
            {
                var floor = root.GetComponentsInChildren<MeshCollider>(true).Single(c => c.name == "SM_Ground");
                var area = floor.GetComponent<KitchenTeleportationArea>();
                if (area == null) area = floor.gameObject.AddComponent<KitchenTeleportationArea>();
                ConfigureArea(area);
            });
            AssetDatabase.SaveAssets();
            Debug.Log("KITCHEN_MOVEMENT_READY shared Rig / floor prefabs configured; no scene or lighting changes.");
        }

        static void ReplaceScript(MonoBehaviour component, string script)
        {
            var fields = new SerializedObject(component);
            Set(fields, "m_Script", KitchenAuthoring.LoadRequired<MonoScript>(KitchenAuthoring.Root + "Scripts/Runtime/" + script));
            fields.ApplyModifiedPropertiesWithoutUndo();
        }

        static void ConfigureArea(KitchenTeleportationArea area)
        {
            area.interactionLayers = 1 << 31; // Existing XRI Teleport interaction layer.
            area.filterSelectionByHitNormal = true;
            area.upNormalToleranceDegrees = 15;
            area.matchOrientation = MatchOrientation.None;
            area.teleportTrigger = BaseTeleportationInteractable.TeleportTrigger.OnSelectExited;
            area.colliders.Clear();
            area.colliders.AddRange(area.GetComponents<Collider>().Where(c => c.enabled));
        }

        static void ConfigureRig(GameObject root)
        {
            var settings = root.GetComponent<KitchenLocomotionSettings>();
            if (settings == null) settings = root.AddComponent<KitchenLocomotionSettings>();
            var move = root.GetComponentInChildren<ContinuousMoveProvider>(true);
            var provider = root.GetComponentInChildren<KitchenTeleportationProvider>(true);
            var snap = root.GetComponentInChildren<SnapTurnProvider>(true);
            move.moveSpeed = 1.5f; move.enableFly = false; move.enableStrafe = true; move.enabled = false;
            provider.delayTime = 0;
            snap.turnAmount = 45; snap.debounceTime = .35f;
            foreach (var turn in root.GetComponentsInChildren<ContinuousTurnProvider>(true)) turn.enabled = false;
            foreach (var grabMove in root.GetComponentsInChildren<GrabMoveProvider>(true)) grabMove.enabled = false;
            foreach (var grabMove in root.GetComponentsInChildren<TwoHandedGrabMoveProvider>(true)) grabMove.enabled = false;
            var managers = root.GetComponentsInChildren<MonoBehaviour>(true)
                .Where(c => c.GetType().Name == "ControllerInputActionManager").ToArray();
            var fields = new SerializedObject(settings);
            Set(fields, "moveProvider", move); Set(fields, "teleportProvider", provider); Set(fields, "snapTurnProvider", snap);
            foreach (var side in new[] { "Left", "Right" })
            {
                var manager = managers.Single(m => m.name == side + " Controller");
                var source = new SerializedObject(manager);
                source.FindProperty("m_SmoothMotionEnabled").boolValue = false;
                source.FindProperty("m_SmoothTurnEnabled").boolValue = false;
                source.ApplyModifiedPropertiesWithoutUndo();
                Set(fields, side.ToLowerInvariant() + "TeleportRay", manager.GetComponentsInChildren<XRRayInteractor>(true)
                    .Single(ray => ray.name == "Teleport Interactor"));
            }
            fields.ApplyModifiedPropertiesWithoutUndo();
            // Bind public Sample APIs once in authoring, without reflection or Sample assembly references in Runtime.
            foreach (var side in new[] { "Left", "Right" })
            {
                var manager = managers.Single(m => m.name == side + " Controller");
                var setter = manager.GetType().GetProperty("smoothMotionEnabled")?.GetSetMethod();
                if (setter == null) throw new InvalidOperationException("XRI manager is missing smoothMotionEnabled.");
                fields.Update();
                var eventField = fields.FindProperty(side.ToLowerInvariant() + "SmoothInput");
                eventField.FindPropertyRelative("m_PersistentCalls.m_Calls").ClearArray();
                fields.ApplyModifiedPropertiesWithoutUndo();
                var eventValue = (UnityEvent<bool>)typeof(KitchenLocomotionSettings)
                    .GetField(side.ToLowerInvariant() + "SmoothInput", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    .GetValue(settings);
                var call = (UnityAction<bool>)Delegate.CreateDelegate(typeof(UnityAction<bool>), manager, setter);
                UnityEventTools.AddPersistentListener(eventValue, call);
            }
            EditorUtility.SetDirty(settings);

            var menu = root.GetComponentInChildren<LocomotionSettingsView>(true);
            if (menu == null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(KitchenAuthoring.LoadRequired<GameObject>(MenuPath), root.transform);
                menu = instance.GetComponent<LocomotionSettingsView>();
            }
            var camera = root.GetComponentInChildren<Camera>(true);
            var blinkTransform = camera.transform.Find("FX_TeleportBlink");
            if (blinkTransform == null)
            {
                var go = new GameObject("FX_TeleportBlink", typeof(RectTransform), typeof(Canvas), typeof(Image));
                go.layer = LayerMask.NameToLayer("UI");
                blinkTransform = go.transform;
                blinkTransform.SetParent(camera.transform, false);
            }
            var blink = blinkTransform.GetComponent<Image>(); blink.color = Color.black; blink.raycastTarget = false;
            var blinkRect = (RectTransform)blinkTransform;
            blinkRect.localPosition = Vector3.forward * .05f;
            blinkRect.localRotation = Quaternion.identity;
            blinkRect.localScale = Vector3.one * .001f; blinkRect.sizeDelta = new Vector2(2000, 2000);
            blinkTransform.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            blinkTransform.GetComponent<Canvas>().sortingOrder = 1000;
            blinkTransform.gameObject.SetActive(false);
            var menuFields = new SerializedObject(menu);
            Set(menuFields, "settings", settings); Set(menuFields, "blink", blink);
            menuFields.ApplyModifiedPropertiesWithoutUndo();
            menu.GetComponent<Canvas>().worldCamera = camera;
            PrefabUtility.RecordPrefabInstancePropertyModifications(menu);
            PrefabUtility.RecordPrefabInstancePropertyModifications(menu.GetComponent<Canvas>());
        }

        static void BuildMenu()
        {
            var root = new GameObject("PREF_UI_LocomotionSettings", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(TrackedDeviceGraphicRaycaster));
            try
            {
                root.layer = LayerMask.NameToLayer("UI");
                var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.sortingOrder = 250;
                // The preference menu must remain usable when opened close to a cabinet or wall.
                root.GetComponent<TrackedDeviceGraphicRaycaster>().checkFor3DOcclusion = false;
                root.GetComponent<TrackedDeviceGraphicRaycaster>().checkFor2DOcclusion = false;
                root.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 20;
                var panel = Rect("Panel", root.transform, 0, 0, 640, 430);
                panel.localScale = Vector3.one * .0008f;
                panel.gameObject.AddComponent<Image>().color = new Color(.04f, .065f, .09f, .98f);
                Label("Title", panel, 0, 162, 570, 48, 34, "MOVEMENT");
                var current = Label("Current", panel, 0, 111, 570, 40, 24, "Current: Teleport");
                var teleport = Button("Teleport", panel, -148, 42, 270, 70, "TELEPORT");
                var smooth = Button("Smooth", panel, 148, 42, 270, 70, "CONTINUOUS");
                var instructions = Label("Instructions", panel, 0, -45, 570, 100, 24,
                    "Push either stick forward to aim.\nRelease to teleport. Right stick: snap turn.");
                Label("Shortcut", panel, -80, -151, 400, 44, 20, "Left Y / Menu  |  Keyboard F8");
                var close = Button("Close", panel, 218, -151, 150, 55, "CLOSE");
                var view = root.AddComponent<LocomotionSettingsView>();
                var data = new SerializedObject(view);
                Set(data, "panel", panel.gameObject); Set(data, "current", current); Set(data, "instructions", instructions);
                Set(data, "teleport", teleport); Set(data, "smooth", smooth); Set(data, "close", close);
                data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, MenuPath);
            }
            finally { Object.DestroyImmediate(root); }
        }

        static RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.gameObject.layer = LayerMask.NameToLayer("UI");
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(x, y); rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        static TMP_Text Label(string name, Transform parent, float x, float y, float width, float height, int size, string value)
        {
            var rect = Rect(name, parent, x, y, width, height);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset; text.fontSize = size; text.alignment = TextAlignmentOptions.Center;
            text.text = value; text.color = new Color(.9f, .94f, .96f); text.raycastTarget = false;
            return text;
        }

        static Button Button(string name, Transform parent, float x, float y, float width, float height, string label)
        {
            var rect = Rect(name, parent, x, y, width, height);
            var image = rect.gameObject.AddComponent<Image>(); image.color = new Color(.1f, .28f, .36f);
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            var colors = button.colors; colors.highlightedColor = new Color(.5f, .9f, 1); colors.disabledColor = new Color(.28f, .42f, .48f);
            button.colors = colors;
            // Unique names let Unity preserve child file IDs when saving over the existing prefab.
            Label(name + "Label", rect, 0, 0, width - 12, height - 8, 25, label);
            return button;
        }

        static void Set(SerializedObject data, string name, Object value) => data.FindProperty(name).objectReferenceValue = value;
    }
}
