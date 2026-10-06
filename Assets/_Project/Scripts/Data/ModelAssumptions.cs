using System;
using System.Collections.Generic;
using UnityEngine;

namespace XRApartment.Data
{
    [Serializable]
    public class AssumptionParameter
    {
        public string key;
        public float valueMetres;
        public string source;
        public string owner;
        public string status;
        public string verification;
    }

    /// Single source of truth for the geometry claims the prototype is allowed to make
    /// (MOD-01 / PRD 7.2). Behaviour code reads lengths from here instead of hard-coding them,
    /// so that a parameter change is one auditable edit rather than a code search.
    [CreateAssetMenu(menuName = "XR Apartment/Model Assumptions", fileName = "ASM_KitchenParameters")]
    public class ModelAssumptions : ScriptableObject
    {
        [SerializeField] private string modelVersion = "0.1-graybox";
        [SerializeField] private List<AssumptionParameter> parameters = new List<AssumptionParameter>();

        public string ModelVersion => modelVersion;
        public IReadOnlyList<AssumptionParameter> Parameters => parameters;

        public bool TryGetLength(string key, out float metres)
        {
            metres = 0f;
            if (parameters == null) return false;
            for (int i = 0; i < parameters.Count; i++)
            {
                if (parameters[i] != null && parameters[i].key == key)
                {
                    metres = parameters[i].valueMetres;
                    return true;
                }
            }
            return false;
        }

        public void SetModelVersion(string version) => modelVersion = version;

        public void Upsert(AssumptionParameter parameter)
        {
            if (parameter == null || string.IsNullOrEmpty(parameter.key)) return;
            if (parameters == null) parameters = new List<AssumptionParameter>();
            for (int i = 0; i < parameters.Count; i++)
            {
                if (parameters[i] != null && parameters[i].key == parameter.key)
                {
                    parameters[i] = parameter;
                    return;
                }
            }
            parameters.Add(parameter);
        }
    }
}
