using System;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace XRApartment.Runtime
{
    public sealed class KitchenLayoutLab : MonoBehaviour
    {
        [Serializable]
        public sealed class Preset
        {
            public string label;
            public string description;
            public Transform fridgeSocket;
            public Transform panelSocket;
        }

        [Serializable]
        public struct FinishPreset
        {
            public string label;
            public Color furniture;
            public Color enamel;
        }

        private static readonly int Colour = Shader.PropertyToID("_BaseColor");
        [SerializeField] private KitchenCookingStation station;
        [SerializeField] private KitchenFridge fridge;
        [SerializeField] private KitchenFridge compactFridge;
        [SerializeField] private XROrigin player;
        [SerializeField] private BoxCollider returnPanel;
        [SerializeField] private Bounds closedFridgeBounds;
        [SerializeField] private Bounds compactFridgeBounds;
        [SerializeField] private Preset[] presets;
        [SerializeField] private FinishPreset[] finishes;
        [SerializeField] private Renderer[] furnitureFinishTargets;
        [SerializeField] private Renderer[] enamelFinishTargets;

        private readonly Collider[] overlaps = new Collider[48];
        private MaterialPropertyBlock finishProperties;
        private struct OpeningResult
        {
            public float angle;
            public bool hitPantry;
        }
        private OpeningResult?[] openings;
        public int ActivePresetIndex { get; private set; }
        public int ActiveFinishIndex { get; private set; }
        public bool CompactSelected => station != null && compactFridge != null && station.ActiveFridge == compactFridge;
        public int PresetCount => presets == null ? 0 : presets.Length;
        public int FinishCount => finishes == null ? 0 : finishes.Length;
        public string Label(int index) => presets[index].label;
        public string FinishLabel(int index) => finishes[index].label;
        public string Description(int index)
        {
            var result = openings[OpeningIndex(index, CompactSelected)];
            return result.HasValue && result.Value.hitPantry
                ? $"The pantry stopped the fridge door at {result.Value.angle:0}°."
                : presets[index].description;
        }

        public string ObservedSwing(bool compact)
        {
            var result = openings[OpeningIndex(ActivePresetIndex, compact)];
            return result.HasValue ? $"{result.Value.angle:0}°" : "try opening";
        }

        internal bool HasObservedOpening(bool compact) => openings[OpeningIndex(ActivePresetIndex, compact)].HasValue;

        private void Awake() => openings = new OpeningResult?[PresetCount * 2];

        private static int OpeningIndex(int preset, bool compact) => preset * 2 + (compact ? 1 : 0);

        public void Configure(BoxCollider panel, Bounds closedEnvelope, Bounds compactEnvelope, Preset[] layouts,
            FinishPreset[] appearance)
        {
            returnPanel = panel;
            closedFridgeBounds = closedEnvelope;
            compactFridgeBounds = compactEnvelope;
            presets = layouts;
            finishes = appearance;
        }

        public void ConfigureFinishTargets(Renderer[] furniture, Renderer[] enamel)
        {
            furnitureFinishTargets = furniture;
            enamelFinishTargets = enamel;
        }

        public bool TrySelectFinish(int index)
        {
            if (index < 0 || index >= FinishCount || furnitureFinishTargets == null ||
                furnitureFinishTargets.Length == 0 || enamelFinishTargets == null || enamelFinishTargets.Length == 0)
                return false;
            if (index == ActiveFinishIndex) return true;
            var finish = finishes[index];
            ApplyFinish(furnitureFinishTargets, finish.furniture, index == 0);
            ApplyFinish(enamelFinishTargets, finish.enamel, index == 0);
            ActiveFinishIndex = index;
            return true;
        }

        private void ApplyFinish(Renderer[] targets, Color colour, bool original)
        {
            if (!original)
            {
                if (finishProperties == null) finishProperties = new MaterialPropertyBlock();
                finishProperties.SetColor(Colour, colour);
            }
            foreach (var target in targets)
                if (target != null) target.SetPropertyBlock(original ? null : finishProperties);
        }

        public bool TryApply(int index, out string status) => TryApply(index, CompactSelected, out status);

        public bool TrySelectFridge(bool compact, out string status) => TryApply(ActivePresetIndex, compact, out status);

        private bool TryApply(int index, bool compact, out string status)
        {
            status = "Layout unavailable.";
            var target = compact ? compactFridge : fridge;
            if (index < 0 || index >= PresetCount || station == null || target == null || returnPanel == null)
                return false;
            var current = station.ActiveFridge;
            var preset = presets[index];
            if (current == null || preset == null || preset.fridgeSocket == null) return false;
            if (index == ActivePresetIndex && current == target)
            {
                status = Description(index);
                return true;
            }
            if (station.IsResetting)
            {
                status = "The kitchen is resetting. Try again in a moment.";
                return false;
            }

            var socket = preset.fridgeSocket;
            var panelSocket = preset.panelSocket;
            Physics.SyncTransforms();
            if (Occupied(socket, compact ? compactFridgeBounds : closedFridgeBounds, current) ||
                (panelSocket != null && Occupied(panelSocket, new Bounds(returnPanel.center, returnPanel.size), current)))
            {
                status = "Move yourself and any loose items away from the new layout.";
                return false;
            }

            // The closed destination has been checked, so place it before registering its door and colliders.
            current.gameObject.SetActive(false);
            target.transform.SetPositionAndRotation(socket.position, socket.rotation);
            target.ResetFridge();
            if (panelSocket != null)
                returnPanel.transform.SetPositionAndRotation(panelSocket.position, panelSocket.rotation);
            returnPanel.gameObject.SetActive(panelSocket != null);
            target.gameObject.SetActive(true);
            station.UseFridge(target);
            ActivePresetIndex = index;
            station.ResetStation();
            status = Description(index);
            return true;
        }

        private void LateUpdate()
        {
            var active = station == null ? null : station.ActiveFridge;
            if (active == null || !active.Door.IsOpen) return;
            var door = active.Door;
            var hitPantry = returnPanel != null && door.OpeningBlocker == returnPanel;
            if (!hitPantry && !door.ReachedOpenAngle) return;
            openings[OpeningIndex(ActivePresetIndex, CompactSelected)] = new OpeningResult
            {
                angle = door.Angle,
                hitPantry = hitPantry
            };
        }

        private bool Occupied(Transform socket, Bounds bounds, KitchenFridge current)
        {
            // Leave a small contact band where the feet meet the fixed floor.
            bounds.SetMinMax(bounds.min + Vector3.up * .015f, bounds.max);
            var centre = socket.TransformPoint(bounds.center);
            var half = bounds.extents;
            var camera = player == null ? null : player.Camera;
            if (camera != null)
            {
                var head = socket.InverseTransformPoint(camera.transform.position) - bounds.center;
                var closest = new Vector3(Mathf.Clamp(head.x, -half.x, half.x),
                    Mathf.Clamp(head.y, -half.y, half.y), Mathf.Clamp(head.z, -half.z, half.z));
                if ((head - closest).sqrMagnitude < .18f * .18f) return true;
            }
            var count = Physics.OverlapBoxNonAlloc(centre, half, overlaps, socket.rotation, ~0,
                QueryTriggerInteraction.Ignore);
            if (count == overlaps.Length) return true;
            for (var index = 0; index < count; index++)
            {
                var hit = overlaps[index];
                if (hit == returnPanel || hit.transform.IsChildOf(current.transform)) continue;
                if (hit.attachedRigidbody != null &&
                    hit.attachedRigidbody.TryGetComponent<KitchenStoredIngredient>(out var food) &&
                    food.Fridge == current && current.Contains(food.transform.position)) continue;
                return true;
            }
            return false;
        }
    }
}
