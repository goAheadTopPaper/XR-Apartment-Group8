using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using XRApartment.Data;

namespace XRApartment.Tests
{
    public class BenchmarkRegistryTests
    {
        private readonly List<Object> spawned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in spawned) if (o != null) Object.DestroyImmediate(o);
            spawned.Clear();
        }

        private BenchmarkDefinition Def(string id, CabinetStrategy cabinet, FridgeArchetype fridge,
            SessionMode modes = SessionMode.Both)
        {
            var d = ScriptableObject.CreateInstance<BenchmarkDefinition>();
            d.Configure(id, cabinet, fridge, modes, id + " controlled question");
            spawned.Add(d);
            return d;
        }

        private BenchmarkRegistry RegistryWith(params BenchmarkDefinition[] defs)
        {
            var r = ScriptableObject.CreateInstance<BenchmarkRegistry>();
            spawned.Add(r);
            var so = new SerializedObject(r);
            var list = so.FindProperty("benchmarks");
            list.arraySize = defs.Length;
            for (int i = 0; i < defs.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = defs[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            return r;
        }

        [Test]
        public void StandardBenchmarks_MatchTheFrozenPRDMapping()
        {
            var registry = RegistryWith(
                Def("B1", CabinetStrategy.Proud, FridgeArchetype.F2),
                Def("B2", CabinetStrategy.FlushOriented, FridgeArchetype.F2),
                Def("B3", CabinetStrategy.FlushOriented, FridgeArchetype.F4));

            Assert.AreEqual(CabinetStrategy.Proud, registry.Find("B1").Cabinet);
            Assert.AreEqual(FridgeArchetype.F2, registry.Find("B1").Fridge);
            Assert.AreEqual(CabinetStrategy.FlushOriented, registry.Find("B2").Cabinet);
            Assert.AreEqual(FridgeArchetype.F2, registry.Find("B2").Fridge);
            Assert.AreEqual(CabinetStrategy.FlushOriented, registry.Find("B3").Cabinet);
            Assert.AreEqual(FridgeArchetype.F4, registry.Find("B3").Fridge);
        }

        [Test]
        public void Rotations_CycleTheSameThreeCombinationsWithoutRepetition()
        {
            var registry = RegistryWith(
                Def("B1", CabinetStrategy.Proud, FridgeArchetype.F2),
                Def("B2", CabinetStrategy.FlushOriented, FridgeArchetype.F2),
                Def("B3", CabinetStrategy.FlushOriented, FridgeArchetype.F4));

            Assert.AreEqual("B1,B2,B3", Join(registry.ChallengeOrder(BenchmarkRotation.First)));
            Assert.AreEqual("B2,B3,B1", Join(registry.ChallengeOrder(BenchmarkRotation.Second)));
            Assert.AreEqual("B3,B1,B2", Join(registry.ChallengeOrder(BenchmarkRotation.Third)));
        }

        [Test]
        public void ExploreOnlyArchetype_IsExcludedFromChallengeOrder()
        {
            var registry = RegistryWith(
                Def("B1", CabinetStrategy.Proud, FridgeArchetype.F2),
                Def("B2", CabinetStrategy.FlushOriented, FridgeArchetype.F2),
                Def("E-F3", CabinetStrategy.Proud, FridgeArchetype.F3, SessionMode.Explore));

            var challenge = registry.ChallengeOrder(BenchmarkRotation.First);
            CollectionAssert.DoesNotContain(challenge, registry.Find("E-F3"));
            Assert.AreEqual(2, challenge.Count);
        }

        [Test]
        public void Find_UnknownId_ReturnsNullInsteadOfThrowing()
        {
            var registry = RegistryWith(Def("B1", CabinetStrategy.Proud, FridgeArchetype.F2));
            Assert.IsNull(registry.Find("B9"));
        }

        private static string Join(List<BenchmarkDefinition> defs)
        {
            return string.Join(",", defs.ConvertAll(d => d.Id));
        }
    }
}
