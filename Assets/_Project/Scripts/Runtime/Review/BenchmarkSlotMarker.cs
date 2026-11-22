using UnityEngine;
using XRApartment.Data;

namespace XRApartment.Runtime
{
    /// Bookkeeping for a swappable slot (D03 preset switching): the kitchen geometry stays in
    /// place while prefab instances and trim are exchanged per benchmark combination. The
    /// editor swap menu writes this marker; the future runtime configuration switcher reads it.
    [DisallowMultipleComponent]
    public class BenchmarkSlotMarker : MonoBehaviour
    {
        [SerializeField] private string benchmarkId = "B1";
        [SerializeField] private FridgeArchetype fridge = FridgeArchetype.F2;
        [SerializeField] private CabinetStrategy cabinet = CabinetStrategy.Proud;

        public string BenchmarkId { get => benchmarkId; set => benchmarkId = value; }
        public FridgeArchetype Fridge { get => fridge; set => fridge = value; }
        public CabinetStrategy Cabinet { get => cabinet; set => cabinet = value; }
    }
}
