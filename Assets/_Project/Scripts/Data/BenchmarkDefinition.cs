using UnityEngine;

namespace XRApartment.Data
{
    [CreateAssetMenu(menuName = "XR Apartment/Benchmark Definition", fileName = "BENCH_New")]
    public class BenchmarkDefinition : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private CabinetStrategy cabinet;
        [SerializeField] private FridgeArchetype fridge;
        [SerializeField] private SessionMode modes = SessionMode.Both;
        [TextArea]
        [SerializeField] private string controlledQuestion;

        public string Id => id;
        public CabinetStrategy Cabinet => cabinet;
        public FridgeArchetype Fridge => fridge;
        public SessionMode Modes => modes;
        public string ControlledQuestion => controlledQuestion;

        public bool Allows(SessionMode mode) => (Modes & mode) != 0;

        public void Configure(string benchmarkId, CabinetStrategy cabinetStrategy,
            FridgeArchetype archetype, SessionMode allowedModes, string question)
        {
            id = benchmarkId;
            cabinet = cabinetStrategy;
            fridge = archetype;
            modes = allowedModes;
            controlledQuestion = question;
        }
    }
}
