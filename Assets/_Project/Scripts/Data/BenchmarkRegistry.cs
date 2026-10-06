using System.Collections.Generic;
using UnityEngine;

namespace XRApartment.Data
{
    /// PRD 13.3: the three standard benchmarks run in one of three rotated orders so that
    /// learning, fatigue and first-option anchoring do not fall on the same combination.
    public enum BenchmarkRotation
    {
        First,
        Second,
        Third
    }

    [CreateAssetMenu(menuName = "XR Apartment/Benchmark Registry", fileName = "REG_Benchmarks")]
    public class BenchmarkRegistry : ScriptableObject
    {
        [SerializeField] private List<BenchmarkDefinition> benchmarks = new List<BenchmarkDefinition>();

        public IReadOnlyList<BenchmarkDefinition> Benchmarks => benchmarks;
        public int Count => benchmarks == null ? 0 : benchmarks.Count;

        public BenchmarkDefinition Find(string benchmarkId)
        {
            if (benchmarks == null) return null;
            for (int i = 0; i < benchmarks.Count; i++)
            {
                if (benchmarks[i] != null && benchmarks[i].Id == benchmarkId) return benchmarks[i];
            }
            return null;
        }

        public List<BenchmarkDefinition> OrderFor(BenchmarkRotation rotation)
        {
            var ordered = new List<BenchmarkDefinition>();
            if (Count < 3) return ordered;

            int offset;
            switch (rotation)
            {
                case BenchmarkRotation.Second: offset = 1; break;
                case BenchmarkRotation.Third: offset = 2; break;
                default: offset = 0; break;
            }

            for (int i = 0; i < 3; i++)
            {
                var def = benchmarks[(offset + i) % benchmarks.Count];
                if (def != null) ordered.Add(def);
            }
            return ordered;
        }

        /// F3 stays in Explore only (D05), so the challenge set must never contain it.
        public List<BenchmarkDefinition> ChallengeOrder(BenchmarkRotation rotation)
        {
            var challenge = new List<BenchmarkDefinition>();
            var ordered = OrderFor(rotation);
            for (int i = 0; i < ordered.Count; i++)
            {
                if (ordered[i].Allows(SessionMode.Challenge)) challenge.Add(ordered[i]);
            }
            return challenge;
        }
    }
}
