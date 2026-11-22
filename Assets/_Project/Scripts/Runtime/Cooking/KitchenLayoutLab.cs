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
            public bool shortenedCounter;
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
        [SerializeField] private Mesh shortenedCounterMesh;
        [SerializeField] private Bounds closedFridgeBounds;
        [SerializeField] private Bounds compactFridgeBounds;
        [SerializeField] private Preset[] presets;
        [SerializeField] private FinishPreset[] finishes;
        [SerializeField] private Renderer[] furnitureFinishTargets;
        [SerializeField] private Renderer[] enamelFinishTargets;

        private MeshFilter counter;
        private MeshCollider counterCollider;
        private Mesh originalCounterMesh, originalCounterColliderMesh;
        private Bounds restoredCounterSection;
        private readonly Collider[] overlaps = new Collider[48];
        private MaterialPropertyBlock finishProperties;
        private float?[] openings;
        public int ActivePresetIndex { get; private set; }
        public int ActiveFinishIndex { get; private set; }
        public bool CompactSelected => station != null && compactFridge != null && station.ActiveFridge == compactFridge;
        public int PresetCount => presets == null ? 0 : presets.Length;
        public int FinishCount => finishes == null ? 0 : finishes.Length;
        public string Label(int index) => presets[index].label;
        public string FinishLabel(int index) => finishes[index].label;
        public string Description(int index) => presets[index].description;

        public string ObservedSwing(bool compact)
        {
            var result = openings[OpeningIndex(ActivePresetIndex, compact)];
            return result.HasValue ? $"{result.Value:0}°" : "try opening";
        }

        internal bool HasObservedOpening(bool compact) => openings[OpeningIndex(ActivePresetIndex, compact)].HasValue;

        private void Awake()
        {
            openings = new float?[PresetCount * 2];
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                if (root.name != "PREF_Kitchen_Environment") continue;
                var worktop = root.transform.Find("SM_KitchenTable");
                if (worktop == null) break;
                counter = worktop.GetComponent<MeshFilter>();
                counterCollider = worktop.GetComponent<MeshCollider>();
                if (counter == null || counterCollider == null) break;
                originalCounterMesh = counter.sharedMesh;
                originalCounterColliderMesh = counterCollider.sharedMesh;
                if (originalCounterColliderMesh != null && shortenedCounterMesh != null)
                {
                    var full = WorldBounds(worktop, originalCounterColliderMesh.bounds);
                    var shortened = WorldBounds(worktop, shortenedCounterMesh.bounds);
                    restoredCounterSection.SetMinMax(new Vector3(shortened.max.x, full.min.y, full.min.z), full.max);
                }
                break;
            }
        }

        private static Bounds WorldBounds(Transform transform, Bounds local)
        {
            var world = new Bounds(transform.TransformPoint(local.center), Vector3.zero);
            for (var i = 0; i < 8; i++)
                world.Encapsulate(transform.TransformPoint(local.center + Vector3.Scale(local.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
            return world;
        }

        private static int OpeningIndex(int preset, bool compact) => preset * 2 + (compact ? 1 : 0);

        public void Configure(Mesh cutMesh, Bounds closedEnvelope, Bounds compactEnvelope, Preset[] layouts,
            FinishPreset[] appearance)
        {
            shortenedCounterMesh = cutMesh;
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
            if (index < 0 || index >= PresetCount || station == null || target == null) return false;
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
            var wasShortened = presets[ActivePresetIndex].shortenedCounter;
            var shorten = preset.shortenedCounter;
            if ((wasShortened || shorten) && (counter == null || counterCollider == null || shortenedCounterMesh == null))
                return false;
            Physics.SyncTransforms();
            // Check the volume that grows back before restoring the original counter. For C,
            // use its shorter geometry for the destination test and roll back if it is blocked.
            var restoringBlocked = wasShortened && !shorten && RestoredSectionOccupied(current);
            if (shorten && !wasShortened) SetCounter(true);
            if (restoringBlocked || Occupied(socket, compact ? compactFridgeBounds : closedFridgeBounds, current))
            {
                if (shorten && !wasShortened) SetCounter(false);
                status = "Move yourself and any loose items away from the new layout.";
                return false;
            }

            current.gameObject.SetActive(false);
            target.transform.SetPositionAndRotation(socket.position, socket.rotation);
            target.ResetFridge();
            if (wasShortened && !shorten) SetCounter(false);
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
            if (door.OpeningBlocker == null && !door.ReachedOpenAngle) return;
            openings[OpeningIndex(ActivePresetIndex, CompactSelected)] = door.Angle;
        }

        private void SetCounter(bool shortened)
        {
            counter.sharedMesh = shortened ? shortenedCounterMesh : originalCounterMesh;
            counterCollider.sharedMesh = shortened ? shortenedCounterMesh : originalCounterColliderMesh;
            Physics.SyncTransforms();
        }

        private bool RestoredSectionOccupied(KitchenFridge current)
        {
            var section = restoredCounterSection;
            var inner = section.extents - Vector3.one * .01f;
            if (inner.x <= 0f || inner.y <= 0f || inner.z <= 0f) return false;
            var camera = player == null ? null : player.Camera;
            if (camera != null && section.SqrDistance(camera.transform.position) < .18f * .18f) return true;
            var count = Physics.OverlapBoxNonAlloc(section.center, inner, overlaps, Quaternion.identity, ~0,
                QueryTriggerInteraction.Ignore);
            if (count == overlaps.Length) return true;
            for (var i = 0; i < count; i++)
                if (overlaps[i].attachedRigidbody != null && !IsFridgeStock(overlaps[i], current)) return true;
            return false;
        }

        private static bool IsFridgeStock(Collider hit, KitchenFridge current) =>
            hit.transform.IsChildOf(current.transform) || (hit.attachedRigidbody != null &&
            hit.attachedRigidbody.TryGetComponent<KitchenStoredIngredient>(out var food) &&
            food.Fridge == current && current.Contains(food.transform.position));

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
                if (IsFridgeStock(hit, current)) continue;
                return true;
            }
            return false;
        }
    }
}
